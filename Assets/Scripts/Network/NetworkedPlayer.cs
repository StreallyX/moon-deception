using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Networked player component - handles ownership and syncs state.
/// Attach to both Astronaut and Alien prefabs.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkedPlayer : NetworkBehaviour
{
    private bool hasSetupCompleted = false;
    [Header("Player Type (Set by prefab)")]
    public bool isAstronaut = true;

    [Header("Astronaut Components")]
    public PlayerMovement playerMovement;
    public PlayerShooting playerShooting;
    public StressSystem stressSystem;

    [Header("Alien Components")]
    public AlienController alienController;
    public HungerSystem hungerSystem;
    public AlienHealth alienHealth;
    public AlienAbilities alienAbilities;

    [Header("Shared Components")]
    public Camera playerCamera;
    public AudioListener audioListener;

    [Header("Visual")]
    public GameObject playerModel;

    // Synced role
    public NetworkVariable<bool> IsAstronautRole = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Synced death state - for spectator system
    public NetworkVariable<bool> IsDead = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Event for death state changes
    public event System.Action<bool> OnDeathStateChanged;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // CRITICAL: Check if we have the right NetworkTransform type
        CheckNetworkTransformAuthority();

        // Subscribe to role changes FIRST (so we catch any updates)
        IsAstronautRole.OnValueChanged += OnRoleChanged;

        // Subscribe to death state changes (for spectator mode)
        IsDead.OnValueChanged += OnDeathStateValueChanged;

        Debug.Log($"[NetworkedPlayer] OnNetworkSpawn - IsOwner: {IsOwner}, IsServer: {IsServer}, ClientId: {OwnerClientId}");
        Debug.Log($"[NetworkedPlayer] Prefab isAstronaut={isAstronaut}, NetworkVar={IsAstronautRole.Value}");

        // Find ALL components
        FindAllComponents();

        // FIRST: Disable ALL gameplay components
        DisableAllComponents();

        if (IsOwner)
        {
            // For clients, wait a moment for NetworkVariable to sync before setup
            if (!IsServer)
            {
                Debug.Log("[NetworkedPlayer] CLIENT: Starting delayed setup to wait for NetworkVariable sync...");
                StartCoroutine(DelayedClientSetup());
            }
            else
            {
                // Server/Host - setup immediately
                // IMPORTANT: Use the isAstronaut field that was set BEFORE spawn by NetworkGameManager
                // Don't use IsAstronautRole.Value here as the NetworkVariable may not be set yet
                Debug.Log($"[NetworkedPlayer] SERVER/HOST: Using prefab isAstronaut={isAstronaut}");
                if (!hasSetupCompleted)
                {
                    EnableLocalPlayer();
                }
            }
        }
        else
        {
            // This is another player - keep everything disabled, just show the model
            DisableRemotePlayer();
        }
    }

    private IEnumerator DelayedClientSetup()
    {
        // If setup already completed (e.g., by OnRoleChanged), skip
        if (hasSetupCompleted)
        {
            Debug.Log("[NetworkedPlayer] CLIENT: Setup already completed by OnRoleChanged, skipping DelayedClientSetup");
            yield break;
        }

        // Wait for NetworkVariable to sync from server
        float timeout = 2f;
        float elapsed = 0f;
        bool initialValue = IsAstronautRole.Value;

        Debug.Log($"[NetworkedPlayer] CLIENT: Waiting for role sync... current={initialValue}");

        // Wait until either timeout or we get a different value (meaning sync happened)
        // Or if value matches what the prefab expects (alienController exists = should be alien)
        while (elapsed < timeout && !hasSetupCompleted)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;

            // Check if setup was completed by OnRoleChanged while we were waiting
            if (hasSetupCompleted)
            {
                Debug.Log("[NetworkedPlayer] CLIENT: Setup completed by OnRoleChanged during wait");
                yield break;
            }

            // Check if NetworkVariable has a sensible value
            bool hasAlienComponents = (alienController != null);
            bool hasAstronautComponents = (playerMovement != null);
            bool networkSaysAstronaut = IsAstronautRole.Value;

            Debug.Log($"[NetworkedPlayer] CLIENT sync check: NetworkVar={networkSaysAstronaut}, hasAlien={hasAlienComponents}, hasAstro={hasAstronautComponents}");

            // If we have alien components but network says alien (false), we're synced
            if (hasAlienComponents && !networkSaysAstronaut)
            {
                Debug.Log("[NetworkedPlayer] CLIENT: Alien role confirmed!");
                break;
            }
            // If we have astronaut components and network says astronaut (true), we're synced
            if (hasAstronautComponents && !hasAlienComponents && networkSaysAstronaut)
            {
                Debug.Log("[NetworkedPlayer] CLIENT: Astronaut role confirmed!");
                break;
            }
        }

        // Final check - if setup completed during our checks, don't do it again
        if (hasSetupCompleted)
        {
            Debug.Log("[NetworkedPlayer] CLIENT: Setup already completed, skipping EnableLocalPlayer");
            yield break;
        }

        // Now setup with the synced role
        isAstronaut = IsAstronautRole.Value;
        Debug.Log($"[NetworkedPlayer] CLIENT: Final role = {(isAstronaut ? "Astronaut" : "Alien")}");

        EnableLocalPlayer();
    }

    void FindAllComponents()
    {
        // Astronaut
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        if (playerShooting == null) playerShooting = GetComponent<PlayerShooting>();
        if (stressSystem == null) stressSystem = GetComponent<StressSystem>();

        // Alien - find existing components
        if (alienController == null) alienController = GetComponent<AlienController>();
        if (hungerSystem == null) hungerSystem = GetComponent<HungerSystem>();
        if (alienHealth == null) alienHealth = GetComponent<AlienHealth>();
        if (alienAbilities == null) alienAbilities = GetComponent<AlienAbilities>();

        // Add PlayerCollision for player-to-player collision (both astronaut and alien)
        var playerCollision = GetComponent<PlayerCollision>();
        if (playerCollision == null)
        {
            gameObject.AddComponent<PlayerCollision>();
            Debug.Log("[NetworkedPlayer] Auto-added PlayerCollision component");
        }

        // CRITICAL: Add NetworkAnimator for animation sync (like NPCs have)
        SetupNetworkAnimator();

        // Auto-add missing alien components if this is an alien prefab (has AlienController)
        if (alienController != null)
        {
            if (alienHealth == null)
            {
                alienHealth = gameObject.AddComponent<AlienHealth>();
                Debug.Log("[NetworkedPlayer] Auto-added AlienHealth to alien");
            }

            if (alienAbilities == null)
            {
                alienAbilities = gameObject.AddComponent<AlienAbilities>();
                Debug.Log("[NetworkedPlayer] Auto-added AlienAbilities to alien");
            }

            // AlienTransformation
            var alienTransformation = GetComponent<AlienTransformation>();
            if (alienTransformation == null)
            {
                gameObject.AddComponent<AlienTransformation>();
                Debug.Log("[NetworkedPlayer] Auto-added AlienTransformation to alien");
            }
        }

        // Auto-add missing astronaut components if this is an astronaut prefab (has PlayerMovement)
        if (playerMovement != null)
        {
            var astronautHealth = GetComponent<AstronautHealth>();
            if (astronautHealth == null)
            {
                gameObject.AddComponent<AstronautHealth>();
                Debug.Log("[NetworkedPlayer] Auto-added AstronautHealth to astronaut");
            }
        }

        // Shared
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);
        if (audioListener == null) audioListener = GetComponentInChildren<AudioListener>(true);

        // DEBUG: Log what we found
        Debug.Log($"[NetworkedPlayer] FindAllComponents on {gameObject.name}:");
        Debug.Log($"  - PlayerMovement: {(playerMovement != null ? "FOUND" : "NULL")}");
        Debug.Log($"  - PlayerShooting: {(playerShooting != null ? "FOUND" : "NULL")}");
        Debug.Log($"  - AlienController: {(alienController != null ? "FOUND" : "NULL")}");
        Debug.Log($"  - AlienHealth: {(alienHealth != null ? "FOUND" : "NULL")}");
        Debug.Log($"  - AlienAbilities: {(alienAbilities != null ? "FOUND" : "NULL")}");
        Debug.Log($"  - HungerSystem: {(hungerSystem != null ? "FOUND" : "NULL")}");
        Debug.Log($"  - Camera: {(playerCamera != null ? playerCamera.gameObject.name : "NULL")}");
    }

    /// <summary>
    /// Setup NetworkAnimator for animation sync (like NPCs have).
    /// This ensures all players see each other's walking/running animations.
    /// </summary>
    void SetupNetworkAnimator()
    {
        // Find the Animator (on self or children)
        Animator animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator == null)
        {
            Debug.LogWarning($"[NetworkedPlayer] No Animator found on {gameObject.name} - cannot setup NetworkAnimator");
            return;
        }

        // Check if NetworkAnimator already exists
        NetworkAnimator networkAnimator = GetComponent<NetworkAnimator>();
        if (networkAnimator == null)
        {
            // Add NetworkAnimator component
            networkAnimator = gameObject.AddComponent<NetworkAnimator>();
            Debug.Log($"[NetworkedPlayer] Auto-added NetworkAnimator to {gameObject.name}");
        }

        // CRITICAL: Assign the animator to NetworkAnimator
        // NetworkAnimator needs a reference to the Animator to sync its parameters
        if (networkAnimator.Animator == null)
        {
            // Use reflection to set the animator since it might be a private field
            var animatorField = typeof(NetworkAnimator).GetField("m_Animator",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (animatorField != null)
            {
                animatorField.SetValue(networkAnimator, animator);
                Debug.Log($"[NetworkedPlayer] Assigned Animator to NetworkAnimator via reflection");
            }
            else
            {
                Debug.LogWarning("[NetworkedPlayer] Could not find m_Animator field in NetworkAnimator");
            }
        }

        Debug.Log($"[NetworkedPlayer] NetworkAnimator setup complete. Animator: {animator.gameObject.name}");
    }

    void DisableAllComponents()
    {
        // Disable ALL movement/control components
        if (playerMovement != null) playerMovement.enabled = false;
        if (playerShooting != null) playerShooting.enabled = false;
        if (stressSystem != null) stressSystem.enabled = false;

        if (alienController != null) alienController.enabled = false;
        if (hungerSystem != null) hungerSystem.enabled = false;
        if (alienAbilities != null) alienAbilities.enabled = false;

        // Disable camera and audio
        if (playerCamera != null) playerCamera.gameObject.SetActive(false);
        if (audioListener != null) audioListener.enabled = false;

        Debug.Log($"[NetworkedPlayer] Disabled all components");
    }

    public override void OnNetworkDespawn()
    {
        IsAstronautRole.OnValueChanged -= OnRoleChanged;
        base.OnNetworkDespawn();
    }

    void EnableLocalPlayer()
    {
        Debug.Log($"[NetworkedPlayer] Enabling LOCAL player controls (Astronaut: {isAstronaut})");

        if (isAstronaut)
        {
            // === ASTRONAUT SETUP ===
            Debug.Log("[NetworkedPlayer] Setting up ASTRONAUT controls");

            // Enable Astronaut components
            if (playerMovement != null) playerMovement.enabled = true;
            if (playerShooting != null) playerShooting.enabled = true;
            if (stressSystem != null) stressSystem.enabled = true;

            // Make sure Alien components stay DISABLED
            if (alienController != null) alienController.enabled = false;
            if (hungerSystem != null) hungerSystem.enabled = false;
            if (alienAbilities != null) alienAbilities.enabled = false;

            // Update UI for Astronaut
            if (GameUIManager.Instance != null)
            {
                GameUIManager.Instance.SetPlayerType(PlayerType.Astronaut);
            }

            // CRITICAL: Ensure GameManager is in Playing phase for the astronaut player
            if (GameManager.Instance != null && GameManager.Instance.CurrentPhase != GameManager.GamePhase.Playing)
            {
                GameManager.Instance.StartGame();
                Debug.Log("[NetworkedPlayer] Called GameManager.StartGame() for astronaut");
            }

            // Also sync NetworkGameManager phase
            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.SetLocalPlayingPhase();
            }
        }
        else
        {
            // === ALIEN SETUP ===
            Debug.Log("[NetworkedPlayer] Setting up ALIEN controls");

            // Make sure Astronaut components stay DISABLED FIRST
            if (playerMovement != null) playerMovement.enabled = false;
            if (playerShooting != null) playerShooting.enabled = false;
            if (stressSystem != null) stressSystem.enabled = false;

            // Enable Alien components
            if (alienController != null)
            {
                // IMPORTANT: AlienController.OnEnable() will handle camera setup
                // So we enable it and let it do its thing
                alienController.enabled = true;
                Debug.Log($"[NetworkedPlayer] ENABLED AlienController - it will handle camera");
            }
            else
            {
                Debug.LogError("[NetworkedPlayer] AlienController is NULL! Cannot enable alien movement!");
            }

            if (hungerSystem != null)
            {
                hungerSystem.enabled = true;
                Debug.Log("[NetworkedPlayer] ENABLED HungerSystem");
            }

            if (alienAbilities != null)
            {
                alienAbilities.enabled = true;
                Debug.Log($"[NetworkedPlayer] ENABLED AlienAbilities! enabled={alienAbilities.enabled}");
            }
            else
            {
                Debug.LogError("[NetworkedPlayer] AlienAbilities is NULL! Component missing on alien prefab!");
                // Try to add it dynamically
                alienAbilities = gameObject.AddComponent<AlienAbilities>();
                if (alienAbilities != null)
                {
                    alienAbilities.enabled = true;
                    Debug.Log("[NetworkedPlayer] Added AlienAbilities dynamically and enabled it");
                }
            }

            // Update UI for Alien
            if (GameUIManager.Instance != null)
            {
                GameUIManager.Instance.SetPlayerType(PlayerType.Alien);
            }

            // CRITICAL: Ensure GameManager is in Playing phase for the alien player
            if (GameManager.Instance != null && GameManager.Instance.CurrentPhase != GameManager.GamePhase.Playing)
            {
                GameManager.Instance.StartGame();
                Debug.Log("[NetworkedPlayer] Called GameManager.StartGame() for alien");
            }

            // Also sync NetworkGameManager phase
            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.SetLocalPlayingPhase();
            }

            // For alien, AlienController handles camera - skip the camera section below
            // Lock cursor for gameplay
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Debug.Log($"[NetworkedPlayer] Alien setup complete");
            hasSetupCompleted = true;

            // Show role announcement
            if (RoleAnnouncementUI.Instance != null)
            {
                RoleAnnouncementUI.Instance.ShowRole(false); // false = alien
            }
            return; // Exit early - AlienController handles camera
        }

        // Enable camera and audio for local player (ASTRONAUT ONLY - alien handled above)
        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(true);
            playerCamera.enabled = true;
            Debug.Log($"[NetworkedPlayer] Enabled camera: {playerCamera.gameObject.name}");
        }
        else
        {
            Debug.LogWarning("[NetworkedPlayer] playerCamera is NULL for astronaut!");
        }

        if (audioListener != null)
        {
            audioListener.enabled = true;
        }

        // Lock cursor for gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log($"[NetworkedPlayer] Local player setup complete - isAstronaut: {isAstronaut}");
        hasSetupCompleted = true;

        // Show role announcement
        if (RoleAnnouncementUI.Instance != null)
        {
            RoleAnnouncementUI.Instance.ShowRole(isAstronaut);
        }
    }

    void DisableRemotePlayer()
    {
        Debug.Log($"[NetworkedPlayer] Setting up REMOTE player (no controls)");

        // All components should already be disabled by DisableAllComponents()
        // Just make sure camera and audio are off

        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(false);
        }

        if (audioListener != null)
        {
            audioListener.enabled = false;
        }

        // Remote players should still be visible (model stays active)
        Debug.Log($"[NetworkedPlayer] Remote player setup complete");
    }

    void OnRoleChanged(bool oldValue, bool newValue)
    {
        isAstronaut = newValue;
        Debug.Log($"[NetworkedPlayer] Role changed to: {(newValue ? "Astronaut" : "Alien")}");

        // Only call EnableLocalPlayer if setup hasn't completed yet
        // This prevents double-setup from both OnRoleChanged and DelayedClientSetup
        if (IsOwner && !hasSetupCompleted)
        {
            Debug.Log("[NetworkedPlayer] OnRoleChanged triggering EnableLocalPlayer");
            EnableLocalPlayer();
        }
    }

    void OnDeathStateValueChanged(bool oldValue, bool newValue)
    {
        Debug.Log($"[NetworkedPlayer] Death state changed: {oldValue} -> {newValue}");

        if (newValue && IsOwner)
        {
            // Local player died - trigger spectator mode
            Debug.Log("[NetworkedPlayer] LOCAL PLAYER DIED - Entering spectator mode");
            EnterSpectatorMode();
        }

        // Notify listeners
        OnDeathStateChanged?.Invoke(newValue);
    }

    /// <summary>
    /// Enter spectator mode when local player dies.
    /// Disables gameplay controls and enables spectator camera.
    /// </summary>
    void EnterSpectatorMode()
    {
        // Disable all gameplay components
        if (playerMovement != null) playerMovement.enabled = false;
        if (playerShooting != null) playerShooting.enabled = false;
        if (alienController != null) alienController.enabled = false;
        if (alienAbilities != null) alienAbilities.enabled = false;

        // Create or activate SpectatorController
        SpectatorController spectator = SpectatorController.Instance;
        if (spectator == null)
        {
            GameObject spectatorObj = new GameObject("SpectatorController");
            spectator = spectatorObj.AddComponent<SpectatorController>();
        }

        // Activate spectator mode - SpectatorController creates its own camera
        // Pass the player camera so it can copy position and disable it
        spectator.Activate(playerCamera);

        // Disable player camera (spectator has its own)
        if (playerCamera != null)
        {
            playerCamera.enabled = false;
        }
        if (audioListener != null)
        {
            audioListener.enabled = false;
        }

        // Show spectator UI
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.ShowNotification("VOUS ÊTES MORT - MODE SPECTATEUR", 5f);
        }

        Debug.Log("[NetworkedPlayer] Spectator mode activated");
    }

    // Called by server to set role
    public void SetRole(bool astronaut)
    {
        if (IsServer)
        {
            IsAstronautRole.Value = astronaut;
            isAstronaut = astronaut;
        }
    }

    /// <summary>
    /// Check if we have the correct NetworkTransform setup.
    /// The default NetworkTransform is Server Authoritative - clients can't move!
    /// We need OwnerNetworkTransform for client movement.
    /// </summary>
    void CheckNetworkTransformAuthority()
    {
        var netTransform = GetComponent<NetworkTransform>();
        if (netTransform == null)
        {
            Debug.LogWarning($"[NetworkedPlayer] No NetworkTransform found on {gameObject.name}!");
            return;
        }

        // Check if it's our custom owner-authoritative transform
        if (netTransform is OwnerNetworkTransform)
        {
            Debug.Log($"[NetworkedPlayer] {gameObject.name} has OwnerNetworkTransform - GOOD!");
            return;
        }

        // We have the old server-authoritative NetworkTransform - THIS IS THE BUG!
        Debug.LogError($"[NetworkedPlayer] {gameObject.name} has SERVER AUTHORITATIVE NetworkTransform!");
        Debug.LogError($"[NetworkedPlayer] Clients CANNOT move with this setup!");
        Debug.LogError($"[NetworkedPlayer] Fix: On the prefab, REMOVE NetworkTransform and ADD OwnerNetworkTransform");
    }

    // ==================== NETWORK AUDIO RPCs ====================
    // These RPCs allow any client to broadcast sounds to all players.
    // Call the ServerRpc from any client, server broadcasts via ClientRpc.

    #region Audio RPCs

    // --- Gunshots ---
    [ServerRpc(RequireOwnership = false)]
    public void PlayGunshotServerRpc(Vector3 position, bool isMinigun)
    {
        PlayGunshotClientRpc(position, isMinigun);
    }

    [ClientRpc]
    private void PlayGunshotClientRpc(Vector3 position, bool isMinigun)
    {
        AudioManager.Instance?.PlayGunshot3D(position, isMinigun);
    }

    // --- Reload ---
    [ServerRpc(RequireOwnership = false)]
    public void PlayReloadServerRpc(Vector3 position)
    {
        PlayReloadClientRpc(position);
    }

    [ClientRpc]
    private void PlayReloadClientRpc(Vector3 position)
    {
        AudioManager.Instance?.PlayReload();
    }

    // --- Bullet Impact ---
    [ServerRpc(RequireOwnership = false)]
    public void PlayBulletImpactServerRpc(string surfaceType, Vector3 position)
    {
        PlayBulletImpactClientRpc(surfaceType, position);
    }

    [ClientRpc]
    private void PlayBulletImpactClientRpc(string surfaceType, Vector3 position)
    {
        AudioManager.Instance?.PlayBulletImpact(surfaceType, position);
    }

    // --- NPC Sounds ---
    [ServerRpc(RequireOwnership = false)]
    public void PlayNPCDeathServerRpc(Vector3 position)
    {
        PlayNPCDeathClientRpc(position);
    }

    [ClientRpc]
    private void PlayNPCDeathClientRpc(Vector3 position)
    {
        AudioManager.Instance?.PlayNPCDeath(position);
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayNPCPanicServerRpc(Vector3 position)
    {
        PlayNPCPanicClientRpc(position);
    }

    [ClientRpc]
    private void PlayNPCPanicClientRpc(Vector3 position)
    {
        AudioManager.Instance?.PlayNPCPanic(position);
    }

    // --- Alien Sounds ---
    [ServerRpc(RequireOwnership = false)]
    public void PlayAlienRevealServerRpc()
    {
        PlayAlienRevealClientRpc();
    }

    [ClientRpc]
    private void PlayAlienRevealClientRpc()
    {
        AudioManager.Instance?.PlayAlienReveal();
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayAlienGrowlServerRpc(Vector3 position)
    {
        PlayAlienGrowlClientRpc(position);
    }

    [ClientRpc]
    private void PlayAlienGrowlClientRpc(Vector3 position)
    {
        AudioManager.Instance?.PlayAlienGrowl(position);
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayAlienAttackServerRpc(Vector3 position)
    {
        PlayAlienAttackClientRpc(position);
    }

    [ClientRpc]
    private void PlayAlienAttackClientRpc(Vector3 position)
    {
        AudioManager.Instance?.PlayAlienAttack();
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayAlienKilledServerRpc(Vector3 position)
    {
        PlayAlienKilledClientRpc(position);
    }

    [ClientRpc]
    private void PlayAlienKilledClientRpc(Vector3 position)
    {
        AudioManager.Instance?.PlayAlienKilled();
    }

    // --- Interactable Sounds ---
    [ServerRpc(RequireOwnership = false)]
    public void PlayCoffeeMachineServerRpc(Vector3 position)
    {
        PlayCoffeeMachineClientRpc(position);
    }

    [ClientRpc]
    private void PlayCoffeeMachineClientRpc(Vector3 position)
    {
        AudioManager.Instance?.PlayCoffeeMachine(position);
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayAlarmTriggerServerRpc(Vector3 position)
    {
        PlayAlarmTriggerClientRpc(position);
    }

    [ClientRpc]
    private void PlayAlarmTriggerClientRpc(Vector3 position)
    {
        AudioManager.Instance?.PlayAlarmTrigger(position);
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayTerminalBeepServerRpc(Vector3 position)
    {
        PlayTerminalBeepClientRpc(position);
    }

    [ClientRpc]
    private void PlayTerminalBeepClientRpc(Vector3 position)
    {
        AudioManager.Instance?.PlayTerminalBeep(position);
    }

    // --- Game Event Sounds ---
    [ServerRpc(RequireOwnership = false)]
    public void PlayPowerDownServerRpc()
    {
        PlayPowerDownClientRpc();
    }

    [ClientRpc]
    private void PlayPowerDownClientRpc()
    {
        AudioManager.Instance?.PlayPowerDown();
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayLightsEmergencyServerRpc()
    {
        PlayLightsEmergencyClientRpc();
    }

    [ClientRpc]
    private void PlayLightsEmergencyClientRpc()
    {
        AudioManager.Instance?.PlayLightsEmergency();
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayAlarmServerRpc()
    {
        PlayAlarmClientRpc();
    }

    [ClientRpc]
    private void PlayAlarmClientRpc()
    {
        AudioManager.Instance?.PlayAlarm();
    }

    // Victory/Defeat are server-only broadcasts
    public void BroadcastVictory()
    {
        if (IsServer)
        {
            PlayVictoryClientRpc();
        }
    }

    [ClientRpc]
    private void PlayVictoryClientRpc()
    {
        AudioManager.Instance?.PlayVictory();
    }

    public void BroadcastDefeat()
    {
        if (IsServer)
        {
            PlayDefeatClientRpc();
        }
    }

    [ClientRpc]
    private void PlayDefeatClientRpc()
    {
        AudioManager.Instance?.PlayDefeat();
    }

    // --- Chaos Phase Ambient ---
    public void BroadcastStartChaosAmbient()
    {
        if (IsServer)
        {
            StartChaosAmbientClientRpc();
        }
    }

    [ClientRpc]
    private void StartChaosAmbientClientRpc()
    {
        AudioManager.Instance?.StartChaosAmbient();
    }

    public void BroadcastStartNormalAmbient()
    {
        if (IsServer)
        {
            StartNormalAmbientClientRpc();
        }
    }

    [ClientRpc]
    private void StartNormalAmbientClientRpc()
    {
        AudioManager.Instance?.StartNormalAmbient();
    }

    #endregion

    #region Effects RPCs (Blood Decals, etc.)

    /// <summary>
    /// Spawn blood decal at position - synced to all clients
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void SpawnBloodDecalServerRpc(Vector3 position)
    {
        SpawnBloodDecalClientRpc(position);
    }

    [ClientRpc]
    private void SpawnBloodDecalClientRpc(Vector3 position)
    {
        BloodDecalManager.Instance?.SpawnBloodDecal(position);
    }

    #endregion

    #region Stress/Ability RPCs

    /// <summary>
    /// Apply stress to astronaut - used by alien abilities
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void ApplyStressServerRpc(float amount)
    {
        ApplyStressClientRpc(amount);
    }

    [ClientRpc]
    private void ApplyStressClientRpc(float amount)
    {
        // Only apply to astronaut player
        if (StressSystem.Instance != null)
        {
            StressSystem.Instance.AddStress(amount);
        }
    }

    /// <summary>
    /// Trigger camera shake on astronaut
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void TriggerCameraShakeServerRpc(float duration, float magnitude)
    {
        TriggerCameraShakeClientRpc(duration, magnitude);
    }

    [ClientRpc]
    private void TriggerCameraShakeClientRpc(float duration, float magnitude)
    {
        // Only shake for astronaut player
        if (PlayerMovement.IsPlayerControlled && CameraShake.Instance != null)
        {
            CameraShake.Instance.Shake(duration, magnitude);
        }
    }

    /// <summary>
    /// Trigger visual glitch effect on astronaut
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void TriggerGlitchEffectServerRpc()
    {
        TriggerGlitchEffectClientRpc();
    }

    [ClientRpc]
    private void TriggerGlitchEffectClientRpc()
    {
        // Only affect astronaut player
        if (PlayerMovement.IsPlayerControlled && PostProcessController.Instance != null)
        {
            PostProcessController.Instance.TriggerDamageEffect();
        }
    }

    #endregion

    #region Chaos Phase RPCs

    /// <summary>
    /// Trigger chaos phase on all clients
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void TriggerChaosPhaseServerRpc()
    {
        // Also trigger on NetworkGameManager (server-side)
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.TriggerChaosPhase();
        }
        TriggerChaosPhaseClientRpc();
    }

    [ClientRpc]
    private void TriggerChaosPhaseClientRpc()
    {
        Debug.Log("[NetworkedPlayer] TriggerChaosPhaseClientRpc received!");

        // Update NetworkGameManager phase locally (even though it's not a NetworkBehaviour)
        // This ensures all systems that check NetworkGameManager phase see Chaos
        if (NetworkGameManager.Instance != null)
        {
            // Directly update the local phase via a public method
            NetworkGameManager.Instance.SetLocalChaosPhase();
        }

        // Trigger GameManager chaos phase with all effects
        if (GameManager.Instance != null && GameManager.Instance.CurrentPhase != GameManager.GamePhase.Chaos)
        {
            GameManager.Instance.TriggerChaosPhase();
            Debug.Log("[NetworkedPlayer] Called GameManager.TriggerChaosPhase()");
        }

        // Also trigger chaos lighting directly in case GameManager didn't
        if (ChaosLightingController.Instance != null)
        {
            ChaosLightingController.Instance.StartChaosLighting();
            Debug.Log("[NetworkedPlayer] Called ChaosLightingController.StartChaosLighting()");
        }
    }

    #endregion

    #region Combat RPCs

    /// <summary>
    /// Alien attacks astronaut - damage synced
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void AlienAttackAstronautServerRpc(float damage)
    {
        AlienAttackAstronautClientRpc(damage);
    }

    [ClientRpc]
    private void AlienAttackAstronautClientRpc(float damage)
    {
        // Apply damage to local astronaut if controlled
        if (AstronautHealth.Instance != null)
        {
            AstronautHealth.Instance.TakeDamage(damage);

            // Show enemy HP bar to the alien (attacker)
            // Alien player is !PlayerMovement.IsPlayerControlled
            if (!PlayerMovement.IsPlayerControlled && GameUIManager.Instance != null)
            {
                GameUIManager.Instance.ShowEnemyHP(
                    AstronautHealth.Instance.currentHealth,
                    AstronautHealth.Instance.maxHealth,
                    "ASTRONAUT"
                );
            }
        }
    }

    /// <summary>
    /// Astronaut shoots alien - damage synced
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void AstronautShootAlienServerRpc(ulong alienNetworkId, float damage)
    {
        AstronautShootAlienClientRpc(alienNetworkId, damage);
    }

    [ClientRpc]
    private void AstronautShootAlienClientRpc(ulong alienNetworkId, float damage)
    {
        // Find the alien by network ID and apply damage
        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(alienNetworkId, out NetworkObject alienObj))
            {
                AlienHealth alienHealth = alienObj.GetComponent<AlienHealth>();
                if (alienHealth != null)
                {
                    alienHealth.TakeDamage(damage);

                    // Show enemy HP bar to the astronaut (attacker)
                    if (PlayerMovement.IsPlayerControlled && GameUIManager.Instance != null)
                    {
                        GameUIManager.Instance.ShowEnemyHP(alienHealth.currentHealth, alienHealth.maxHealth, "ALIEN");
                    }
                }
            }
        }
    }

    #endregion

    #region Player Death RPCs

    /// <summary>
    /// Called when an alien player dies. Server processes death and checks win condition.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void AlienDiedServerRpc(ulong alienClientId)
    {
        Debug.Log($"[NetworkedPlayer] SERVER: Alien {alienClientId} died!");

        // Set IsDead on the dead player's NetworkedPlayer
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(alienClientId, out var deadClient))
        {
            var deadPlayer = deadClient.PlayerObject?.GetComponent<NetworkedPlayer>();
            if (deadPlayer != null)
            {
                deadPlayer.IsDead.Value = true;
                Debug.Log($"[NetworkedPlayer] SERVER: Set IsDead=true for client {alienClientId}");
            }
        }

        // Notify all clients about the death
        AlienDiedClientRpc(alienClientId);

        // Server checks win condition: count alive aliens (using IsDead)
        int aliveAliens = 0;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
            {
                var networkedPlayer = client.PlayerObject.GetComponent<NetworkedPlayer>();
                if (networkedPlayer != null && !networkedPlayer.IsAstronautRole.Value && !networkedPlayer.IsDead.Value)
                {
                    aliveAliens++;
                }
            }
        }

        Debug.Log($"[NetworkedPlayer] SERVER: Remaining alive aliens: {aliveAliens}");

        // If no aliens left, astronaut wins!
        if (aliveAliens == 0 && NetworkGameManager.Instance != null)
        {
            Debug.Log("[NetworkedPlayer] SERVER: All aliens eliminated! Astronaut wins!");
            NetworkGameManager.Instance.EndGame(true); // true = astronaut wins
        }
    }

    [ClientRpc]
    private void AlienDiedClientRpc(ulong alienClientId)
    {
        Debug.Log($"[NetworkedPlayer] Alien {alienClientId} died - all clients notified");

        // Reduce astronaut stress (only on astronaut's client)
        if (StressSystem.Instance != null)
        {
            StressSystem.Instance.ReduceStress(10f); // Stress relief for killing alien
            Debug.Log("[NetworkedPlayer] Astronaut stress reduced for alien kill");
        }

        // Add to kill feed
        if (KillFeedManager.Instance != null)
        {
            KillFeedManager.Instance.AddAstronautKilledAlien();
        }

        // Show notification
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.ShowNotification("ALIEN ELIMINATED!", 2f);
        }
    }

    /// <summary>
    /// Called when the astronaut player dies. Server ends the game.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void AstronautDiedServerRpc(ServerRpcParams rpcParams = default)
    {
        Debug.Log("[NetworkedPlayer] SERVER: Astronaut died! Aliens win!");

        // Set IsDead on the astronaut player
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(senderId, out var deadClient))
        {
            var deadPlayer = deadClient.PlayerObject?.GetComponent<NetworkedPlayer>();
            if (deadPlayer != null)
            {
                deadPlayer.IsDead.Value = true;
                Debug.Log($"[NetworkedPlayer] SERVER: Set IsDead=true for astronaut (client {senderId})");
            }
        }

        // Notify all clients
        AstronautDiedClientRpc();

        // End the game - aliens win
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.EndGame(false); // false = aliens win
        }
    }

    [ClientRpc]
    private void AstronautDiedClientRpc()
    {
        Debug.Log("[NetworkedPlayer] Astronaut died - all clients notified");

        // Add to kill feed
        if (KillFeedManager.Instance != null)
        {
            KillFeedManager.Instance.AddAlienKilledAstronaut();
        }

        // Show notification
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.ShowNotification("ASTRONAUT ELIMINATED!", 2f);
        }
    }

    #endregion

    #region Alien Ability RPCs

    /// <summary>
    /// Ability 1: Collision - push NPCs, everyone sees/hears
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void UseCollisionAbilityServerRpc(Vector3 position)
    {
        UseCollisionAbilityClientRpc(position);
    }

    [ClientRpc]
    private void UseCollisionAbilityClientRpc(Vector3 position)
    {
        Debug.Log($"[NetworkedPlayer] COLLISION effect received at {position}! IsAstronaut={PlayerMovement.IsPlayerControlled}");

        // Play impact sound at position
        AudioManager.Instance?.PlaySFX3D(AudioManager.Instance?.bulletImpactMetal, position);

        // Create visual shockwave effect
        CreateShockwaveEffect(position, 3f, new Color(1f, 0.4f, 0.4f, 0.8f));

        // Shake camera if astronaut is nearby
        if (PlayerMovement.IsPlayerControlled)
        {
            float dist = Vector3.Distance(position, Camera.main?.transform.position ?? Vector3.zero);
            if (dist < 10f && CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.15f, 0.04f * (1f - dist / 10f));
            }
        }
    }

    /// <summary>
    /// Ability 2: Glitch - visual distortion
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void UseGlitchAbilityServerRpc(Vector3 position)
    {
        UseGlitchAbilityClientRpc(position);
    }

    [ClientRpc]
    private void UseGlitchAbilityClientRpc(Vector3 position)
    {
        Debug.Log($"[NetworkedPlayer] GLITCH effect received at {position}! IsAstronaut={PlayerMovement.IsPlayerControlled}");

        // Create glitch visual at position (for all players to see)
        CreateGlitchEffect(position, 15f);

        // Only affect astronaut player with post-process
        if (PlayerMovement.IsPlayerControlled)
        {
            float dist = Vector3.Distance(position, Camera.main?.transform.position ?? Vector3.zero);
            if (dist < 15f)
            {
                PostProcessController.Instance?.TriggerDamageEffect();
                CameraShake.Instance?.Shake(0.3f, 0.05f);
            }
        }
    }

    /// <summary>
    /// Ability 3: Sound - creepy noise
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void UseSoundAbilityServerRpc(Vector3 soundPosition)
    {
        UseSoundAbilityClientRpc(soundPosition);
    }

    [ClientRpc]
    private void UseSoundAbilityClientRpc(Vector3 soundPosition)
    {
        Debug.Log($"[NetworkedPlayer] SOUND effect received at {soundPosition}! IsAstronaut={PlayerMovement.IsPlayerControlled}");

        // Play creepy sound - everyone hears it
        AudioManager.Instance?.PlayAlienGrowl(soundPosition);

        // Create subtle visual indicator where sound came from
        CreateSoundIndicator(soundPosition);
    }

    /// <summary>
    /// Ability 4: Wind - environmental disturbance
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void UseWindAbilityServerRpc(Vector3 position)
    {
        UseWindAbilityClientRpc(position);
    }

    [ClientRpc]
    private void UseWindAbilityClientRpc(Vector3 position)
    {
        Debug.Log($"[NetworkedPlayer] WIND effect received at {position}! IsAstronaut={PlayerMovement.IsPlayerControlled}");

        // Play power down sound
        AudioManager.Instance?.PlayPowerDown();

        // Create wind particle effect
        CreateWindEffect(position, 8f);

        // Flicker lights for all players
        StartCoroutine(FlickerNearbyLights(position, 16f));

        // Camera shake for astronaut
        if (PlayerMovement.IsPlayerControlled)
        {
            float dist = Vector3.Distance(position, Camera.main?.transform.position ?? Vector3.zero);
            if (dist < 16f && CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.4f, 0.06f * (1f - dist / 16f));
            }
        }
    }

    /// <summary>
    /// Ability 4: Teleport - swap positions with NPC
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void UseTeleportAbilityServerRpc(Vector3 alienPosition, Vector3 npcPosition)
    {
        UseTeleportAbilityClientRpc(alienPosition, npcPosition);
    }

    [ClientRpc]
    private void UseTeleportAbilityClientRpc(Vector3 alienPosition, Vector3 npcPosition)
    {
        Debug.Log($"[NetworkedPlayer] TELEPORT effect received! Alien: {alienPosition}, NPC: {npcPosition}");

        // Create teleport effects at BOTH positions (visible to everyone)
        CreateTeleportEffect(alienPosition);
        CreateTeleportEffect(npcPosition);

        // Play sound effect
        AudioManager.Instance?.PlayPowerDown();

        // Camera shake for nearby astronaut
        if (PlayerMovement.IsPlayerControlled)
        {
            var cam = Camera.main?.transform.position ?? Vector3.zero;
            float distToAlien = Vector3.Distance(alienPosition, cam);
            float distToNpc = Vector3.Distance(npcPosition, cam);
            float minDist = Mathf.Min(distToAlien, distToNpc);

            if (minDist < 15f && CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.2f, 0.03f * (1f - minDist / 15f));
            }
        }
    }

    /// <summary>
    /// Teleport position swap - SERVER validates and syncs the position change.
    /// This ensures the alien's new position is properly synced to all clients.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void TeleportSwapServerRpc(Vector3 newPosition, Quaternion newRotation, ulong npcNetworkId, ServerRpcParams rpcParams = default)
    {
        // Get the sender's client ID
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[NetworkedPlayer] SERVER: TeleportSwap from client {senderClientId} to position {newPosition}");

        // Find the sender's player object
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(senderClientId, out var client))
        {
            Debug.LogWarning($"[NetworkedPlayer] SERVER: Client {senderClientId} not found!");
            return;
        }

        var playerObj = client.PlayerObject;
        if (playerObj == null)
        {
            Debug.LogWarning($"[NetworkedPlayer] SERVER: Player object for client {senderClientId} is null!");
            return;
        }

        // Store old position for NPC swap
        Vector3 oldPosition = playerObj.transform.position;
        Quaternion oldRotation = playerObj.transform.rotation;

        // Validate teleport distance (anti-cheat: max 20m teleport)
        float teleportDistance = Vector3.Distance(oldPosition, newPosition);
        if (teleportDistance > 20f)
        {
            Debug.LogWarning($"[NetworkedPlayer] SERVER: Teleport distance too far ({teleportDistance}m)! Rejecting.");
            return;
        }

        // Apply position change to the player
        // Disable CharacterController temporarily
        var charController = playerObj.GetComponent<CharacterController>();
        bool wasEnabled = charController != null && charController.enabled;
        if (charController != null) charController.enabled = false;

        playerObj.transform.position = newPosition + Vector3.up * 0.1f;
        playerObj.transform.rotation = newRotation;

        if (charController != null) charController.enabled = wasEnabled;

        // Move NPC to old position if valid
        if (npcNetworkId != 0 && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(npcNetworkId, out var npcNetObj))
        {
            npcNetObj.transform.position = oldPosition;
            npcNetObj.transform.rotation = oldRotation;
        }

        Debug.Log($"[NetworkedPlayer] SERVER: Teleport applied. Player now at {newPosition}");

        // Broadcast to all clients (NetworkTransform should handle sync, but this ensures immediate update)
        TeleportSwapClientRpc(senderClientId, newPosition, newRotation);
    }

    [ClientRpc]
    private void TeleportSwapClientRpc(ulong playerId, Vector3 newPosition, Quaternion newRotation)
    {
        Debug.Log($"[NetworkedPlayer] CLIENT: Player {playerId} teleported to {newPosition}");

        // Only the owner should apply the teleport (client authoritative with OwnerNetworkTransform)
        if (NetworkManager.Singleton.LocalClientId != playerId) return;

        // Find our local player and teleport it
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(playerId, out var client))
        {
            var playerObj = client.PlayerObject;
            if (playerObj != null)
            {
                // Disable CharacterController temporarily
                var charController = playerObj.GetComponent<CharacterController>();
                bool wasEnabled = charController != null && charController.enabled;
                if (charController != null) charController.enabled = false;

                // Apply teleport
                playerObj.transform.position = newPosition + Vector3.up * 0.1f;
                playerObj.transform.rotation = newRotation;

                // Re-enable CharacterController
                if (charController != null) charController.enabled = wasEnabled;

                Debug.Log($"[NetworkedPlayer] CLIENT: Local teleport applied to {newPosition}");
            }
        }
    }

    /// <summary>
    /// Create purple teleport particle effect
    /// </summary>
    void CreateTeleportEffect(Vector3 position)
    {
        GameObject fx = new GameObject("TeleportEffect");
        fx.transform.position = position + Vector3.up;

        var ps = fx.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = 0.8f;
        main.startSpeed = 5f;
        main.startSize = 0.3f;
        main.startColor = new Color(0.5f, 0f, 1f, 0.9f); // Purple particles
        main.maxParticles = 50;
        main.gravityModifier = -0.5f; // Float upward

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 40) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;

        var renderer = fx.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetSafeParticleMaterial();

        ps.Play();
        Destroy(fx, 2f);
    }

    // ========== VISUAL EFFECT HELPERS ==========

    void CreateShockwaveEffect(Vector3 position, float radius, Color color)
    {
        GameObject fx = new GameObject("ShockwaveEffect");
        fx.transform.position = position;

        // Create expanding ring
        var lineRenderer = fx.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 32;
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.startColor = color;
        lineRenderer.endColor = new Color(color.r, color.g, color.b, 0f);
        lineRenderer.material = GetSafeMaterial();
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = true;

        // Draw circle
        for (int i = 0; i < 32; i++)
        {
            float angle = (i / 32f) * Mathf.PI * 2f;
            Vector3 pos = position + new Vector3(Mathf.Cos(angle), 0.1f, Mathf.Sin(angle)) * radius;
            lineRenderer.SetPosition(i, pos);
        }

        Destroy(fx, 0.5f);
    }

    /// <summary>
    /// Get a material that works in builds (fallback chain)
    /// </summary>
    Material GetSafeMaterial()
    {
        // Try different shaders in order of preference
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("UI/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");

        if (shader != null)
        {
            return new Material(shader);
        }

        // Ultimate fallback - use Unity's built-in default material
        Debug.LogWarning("[NetworkedPlayer] No shader found, using default sprite material");
        return new Material(Shader.Find("Hidden/InternalErrorShader"));
    }

    /// <summary>
    /// Get a particle material that works in builds
    /// </summary>
    Material GetSafeParticleMaterial()
    {
        Shader shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        if (shader != null)
        {
            return new Material(shader);
        }

        Debug.LogWarning("[NetworkedPlayer] No particle shader found");
        return new Material(Shader.Find("Hidden/InternalErrorShader"));
    }

    void CreateGlitchEffect(Vector3 position, float radius)
    {
        // Create brief visual distortion indicator
        GameObject fx = new GameObject("GlitchEffect");
        fx.transform.position = position + Vector3.up;

        // Simple particle burst
        var ps = fx.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.startLifetime = 0.5f;
        main.startSpeed = 8f;
        main.startSize = 0.2f;
        main.startColor = new Color(0.4f, 0.8f, 1f, 0.8f);
        main.maxParticles = 30;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 20) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;

        var renderer = fx.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetSafeParticleMaterial();

        ps.Play();
        Destroy(fx, 1f);
    }

    void CreateSoundIndicator(Vector3 position)
    {
        // Create brief sound wave visual
        GameObject fx = new GameObject("SoundIndicator");
        fx.transform.position = position + Vector3.up * 0.5f;

        // Simple expanding ring on ground
        var lineRenderer = fx.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 32;
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
        lineRenderer.startColor = new Color(1f, 0.9f, 0.3f, 0.6f);
        lineRenderer.endColor = new Color(1f, 0.9f, 0.3f, 0f);
        lineRenderer.material = GetSafeMaterial();
        lineRenderer.loop = true;

        // Animate expansion via coroutine
        StartCoroutine(AnimateSoundWave(fx, position, 0f, 5f, 0.5f));
    }

    System.Collections.IEnumerator AnimateSoundWave(GameObject fx, Vector3 center, float startRadius, float endRadius, float duration)
    {
        var lr = fx.GetComponent<LineRenderer>();
        float elapsed = 0f;

        while (elapsed < duration && fx != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float radius = Mathf.Lerp(startRadius, endRadius, t);
            float alpha = 1f - t;

            lr.startColor = new Color(1f, 0.9f, 0.3f, alpha * 0.6f);
            lr.endColor = new Color(1f, 0.9f, 0.3f, alpha * 0.3f);

            for (int i = 0; i < 32; i++)
            {
                float angle = (i / 32f) * Mathf.PI * 2f;
                Vector3 pos = center + new Vector3(Mathf.Cos(angle), 0.1f, Mathf.Sin(angle)) * radius;
                lr.SetPosition(i, pos);
            }

            yield return null;
        }

        if (fx != null) Destroy(fx);
    }

    void CreateWindEffect(Vector3 position, float radius)
    {
        GameObject fx = new GameObject("WindEffect");
        fx.transform.position = position;

        var ps = fx.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.startLifetime = 1.5f;
        main.startSpeed = 5f;
        main.startSize = 0.15f;
        main.startColor = new Color(0.5f, 1f, 0.5f, 0.5f);
        main.maxParticles = 80;
        main.gravityModifier = -0.2f;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 50) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;

        var renderer = fx.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetSafeParticleMaterial();

        ps.Play();
        Destroy(fx, 2f);
    }

    System.Collections.IEnumerator FlickerNearbyLights(Vector3 position, float radius)
    {
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        List<Light> nearbyLights = new List<Light>();
        Dictionary<Light, float> originalIntensities = new Dictionary<Light, float>();

        foreach (var light in lights)
        {
            if (Vector3.Distance(light.transform.position, position) < radius)
            {
                nearbyLights.Add(light);
                originalIntensities[light] = light.intensity;
            }
        }

        // Flicker 3 times
        for (int i = 0; i < 3; i++)
        {
            foreach (var light in nearbyLights)
            {
                if (light != null) light.intensity = 0f;
            }
            yield return new WaitForSeconds(0.1f);

            foreach (var light in nearbyLights)
            {
                if (light != null && originalIntensities.ContainsKey(light))
                    light.intensity = originalIntensities[light];
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    #endregion

    #region Interactable RPCs

    /// <summary>
    /// Coffee machine interaction - server validates then broadcasts
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void UseCoffeeMachineServerRpc(ulong interactableNetId, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[NetworkedPlayer] SERVER: UseCoffeeMachine from client {senderId}");

        // Validate: Is the sender an alien?
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(senderId, out var client))
        {
            Debug.LogWarning($"[NetworkedPlayer] SERVER: Client {senderId} not found!");
            return;
        }

        var playerNetworked = client.PlayerObject?.GetComponent<NetworkedPlayer>();
        if (playerNetworked == null || playerNetworked.IsAstronautRole.Value)
        {
            Debug.LogWarning($"[NetworkedPlayer] SERVER: Client {senderId} is not an alien!");
            return;
        }

        // Broadcast to all clients (so everyone sees/hears the coffee machine)
        UseCoffeeMachineClientRpc(interactableNetId, senderId);
    }

    [ClientRpc]
    private void UseCoffeeMachineClientRpc(ulong interactableNetId, ulong userClientId)
    {
        Debug.Log($"[NetworkedPlayer] CoffeeMachine used by client {userClientId}");

        // Only apply hunger effect to the user's local hunger system
        if (NetworkManager.Singleton.LocalClientId == userClientId)
        {
            var hunger = FindFirstObjectByType<HungerSystem>();
            if (hunger != null)
            {
                hunger.DrinkCoffee();
                Debug.Log($"[NetworkedPlayer] Applied coffee effect to local alien");
            }
        }

        // Play sound for everyone
        if (interactableNetId != 0 && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(interactableNetId, out var netObj))
        {
            AudioManager.Instance?.PlayCoffeeMachine(netObj.transform.position);
        }
    }

    /// <summary>
    /// Alarm terminal interaction - server validates then broadcasts
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void UseAlarmTerminalServerRpc(ulong interactableNetId, Vector3 alarmPosition, float alarmRadius, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[NetworkedPlayer] SERVER: UseAlarmTerminal from client {senderId}");

        // Validate: Is the sender an alien?
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(senderId, out var client))
        {
            Debug.LogWarning($"[NetworkedPlayer] SERVER: Client {senderId} not found!");
            return;
        }

        var playerNetworked = client.PlayerObject?.GetComponent<NetworkedPlayer>();
        if (playerNetworked == null || playerNetworked.IsAstronautRole.Value)
        {
            Debug.LogWarning($"[NetworkedPlayer] SERVER: Client {senderId} is not an alien!");
            return;
        }

        // Server panics NPCs in range
        PanicNPCsInRadius(alarmPosition, alarmRadius);

        // Broadcast to all clients
        UseAlarmTerminalClientRpc(alarmPosition);
    }

    [ClientRpc]
    private void UseAlarmTerminalClientRpc(Vector3 alarmPosition)
    {
        Debug.Log($"[NetworkedPlayer] AlarmTerminal triggered at {alarmPosition}");

        // Play sounds for everyone
        AudioManager.Instance?.PlayAlarmTrigger(alarmPosition);
        AudioManager.Instance?.PlayTerminalBeep(alarmPosition);
    }

    /// <summary>
    /// Helper: Panic all NPCs within radius (server-side)
    /// </summary>
    private void PanicNPCsInRadius(Vector3 position, float radius)
    {
        if (!IsServer) return;

        var npcs = FindObjectsByType<NPCBehavior>(FindObjectsSortMode.None);
        int count = 0;
        foreach (var npc in npcs)
        {
            if (npc == null || !npc.IsSpawned) continue;

            float dist = Vector3.Distance(position, npc.transform.position);
            if (dist <= radius)
            {
                // Use server-side panic which will sync to clients
                npc.Panic();
                count++;
            }
        }
        Debug.Log($"[NetworkedPlayer] SERVER: Panicked {count} NPCs in radius {radius}m");
    }

    #endregion

    // NOTE: Kill Feed entries are already added in AlienDiedClientRpc, AstronautDiedClientRpc, and AlienEatClientRpc
    // No separate Kill Feed RPCs needed - they would cause duplication

    #region Game End RPCs

    /// <summary>
    /// End game - synced to all clients
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void EndGameServerRpc(bool alienWins)
    {
        EndGameClientRpc(alienWins);
    }

    [ClientRpc]
    private void EndGameClientRpc(bool alienWins)
    {
        if (GameManager.Instance != null)
        {
            GameManager.WinCondition winner = alienWins ?
                GameManager.WinCondition.AliensWin :
                GameManager.WinCondition.AstronautWins;
            GameManager.Instance.EndGame(winner);
        }
    }

    #endregion

    #region Player Connection/Disconnect RPCs

    /// <summary>
    /// Notify all clients that a player has connected
    /// </summary>
    [ClientRpc]
    public void NotifyPlayerConnectedClientRpc(ulong connectedClientId, bool isAstronaut)
    {
        Debug.Log($"[NetworkedPlayer] Player {connectedClientId} connected (Astronaut: {isAstronaut})");

        // Don't show notification to the player themselves
        if (NetworkManager.Singleton.LocalClientId == connectedClientId) return;

        // Show notification to other players
        if (GameUIManager.Instance != null)
        {
            string role = isAstronaut ? "L'ASTRONAUTE" : "UN ALIEN";
            GameUIManager.Instance.ShowNotification($"{role} A REJOINT LA PARTIE", 3f);
        }

        // Play connection sound
        AudioManager.Instance?.PlayTerminalBeep(Vector3.zero);
    }

    /// <summary>
    /// Notify all clients that a player has disconnected
    /// </summary>
    [ClientRpc]
    public void NotifyPlayerDisconnectedClientRpc(ulong disconnectedClientId)
    {
        Debug.Log($"[NetworkedPlayer] Player {disconnectedClientId} disconnected from the game");

        // Show notification to remaining players
        if (GameUIManager.Instance != null)
        {
            bool wasAstronaut = NetworkGameManager.Instance != null &&
                               NetworkGameManager.Instance.IsAstronaut(disconnectedClientId);
            string role = wasAstronaut ? "L'ASTRONAUTE" : "UN ALIEN";
            GameUIManager.Instance.ShowNotification($"{role} A QUITTÉ LA PARTIE", 3f);
        }

        // Play disconnect sound (warning)
        AudioManager.Instance?.PlayAlarm();
    }

    /// <summary>
    /// Notify all clients of server/host status change
    /// </summary>
    [ClientRpc]
    public void NotifyHostMigrationClientRpc(ulong newHostId)
    {
        Debug.Log($"[NetworkedPlayer] Host migration! New host: {newHostId}");

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.ShowNotification("MIGRATION D'HÔTE EN COURS...", 3f);
        }
    }

    #endregion

    #region Bullet Tracer RPCs

    /// <summary>
    /// Spawn bullet tracer visible to all players
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void SpawnBulletTracerServerRpc(Vector3 startPoint, Vector3 endPoint, bool isHit)
    {
        SpawnBulletTracerClientRpc(startPoint, endPoint, isHit);
    }

    [ClientRpc]
    private void SpawnBulletTracerClientRpc(Vector3 startPoint, Vector3 endPoint, bool isHit)
    {
        // Create bullet tracer effect on all clients
        BulletTracerManager.Instance?.SpawnTracer(startPoint, endPoint, isHit);
    }

    /// <summary>
    /// Spawn impact effect visible to all players
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void SpawnImpactEffectServerRpc(Vector3 position, Vector3 normal, bool isBlood)
    {
        SpawnImpactEffectClientRpc(position, normal, isBlood);
    }

    [ClientRpc]
    private void SpawnImpactEffectClientRpc(Vector3 position, Vector3 normal, bool isBlood)
    {
        // Create impact effect on all clients
        BulletTracerManager.Instance?.SpawnImpact(position, normal, isBlood);
    }

    #endregion

    #region Animation Sync RPCs

    // Cache animator reference
    private Animator cachedAnimator;
    private float lastAnimSyncTime = 0f;
    private const float ANIM_SYNC_INTERVAL = 0.1f; // Sync every 100ms

    /// <summary>
    /// Call this from AlienController/PlayerMovement to sync animation state to all clients.
    /// </summary>
    public void SyncAnimationState(float speed, bool isMoving)
    {
        // Only the owner should send animation updates
        if (!IsOwner) return;

        // Throttle updates to avoid network spam
        if (Time.time - lastAnimSyncTime < ANIM_SYNC_INTERVAL) return;
        lastAnimSyncTime = Time.time;

        // Send to server which will broadcast to all clients
        SyncAnimationServerRpc(speed, isMoving);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SyncAnimationServerRpc(float speed, bool isMoving)
    {
        // Broadcast to all clients (including sender for consistency)
        SyncAnimationClientRpc(speed, isMoving);
    }

    [ClientRpc]
    private void SyncAnimationClientRpc(float speed, bool isMoving)
    {
        // Don't apply to the owner (they already set it locally)
        if (IsOwner) return;

        // Apply animation state
        ApplyAnimationState(speed, isMoving);
    }

    /// <summary>
    /// Apply animation state to this player's animator
    /// </summary>
    private void ApplyAnimationState(float speed, bool isMoving)
    {
        if (cachedAnimator == null)
        {
            cachedAnimator = GetComponent<Animator>();
            if (cachedAnimator == null)
            {
                cachedAnimator = GetComponentInChildren<Animator>();
            }
        }

        if (cachedAnimator != null)
        {
            cachedAnimator.SetFloat("Speed", speed);
            cachedAnimator.SetBool("IsMoving", isMoving);

            // Adjust animation speed based on movement speed
            float walkSpeed = 2f; // Default walk speed
            float speedRatio = Mathf.Clamp(speed / walkSpeed, 0.5f, 2f);
            cachedAnimator.speed = isMoving ? speedRatio : 1f;
        }
    }

    #endregion

    #region Weapon Upgrade RPCs (Phase F2/F3)

    /// <summary>
    /// Sync weapon upgrade to all clients (Defense Zone minigun pickup)
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void UpgradeToMinigunServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[NetworkedPlayer] SERVER: Minigun upgrade for client {senderId}");

        // Broadcast to all clients
        UpgradeToMinigunClientRpc(senderId);
    }

    [ClientRpc]
    private void UpgradeToMinigunClientRpc(ulong playerClientId)
    {
        Debug.Log($"[NetworkedPlayer] Client {playerClientId} upgraded to MINIGUN");

        // Find the player and upgrade their weapon
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(playerClientId, out var client))
        {
            var playerObj = client.PlayerObject;
            if (playerObj != null)
            {
                var shooting = playerObj.GetComponent<PlayerShooting>();
                if (shooting != null)
                {
                    shooting.UpgradeToMinigun();
                    Debug.Log($"[NetworkedPlayer] Applied minigun upgrade to client {playerClientId}");
                }
            }
        }

        // Show notification
        if (GameUIManager.Instance != null && playerClientId != NetworkManager.Singleton.LocalClientId)
        {
            GameUIManager.Instance.ShowNotification("L'ASTRONAUTE A UN MINIGUN!", 2f);
        }
    }

    #endregion

    #region Alien Transformation Visual RPCs (Phase G1)

    /// <summary>
    /// Sync alien transformation visuals to all clients
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void SyncTransformationServerRpc(bool isTransformed, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        SyncTransformationClientRpc(senderId, isTransformed);
    }

    [ClientRpc]
    private void SyncTransformationClientRpc(ulong alienClientId, bool isTransformed)
    {
        // Don't apply to the local owner (they already transformed locally)
        if (alienClientId == NetworkManager.Singleton.LocalClientId) return;

        Debug.Log($"[NetworkedPlayer] Alien {alienClientId} transformation: {isTransformed}");

        // Find the alien player and apply visual transformation
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(alienClientId, out var client))
        {
            var playerNetObj = client.PlayerObject;
            if (playerNetObj != null)
            {
                var transformation = playerNetObj.GetComponent<AlienTransformation>();
                if (transformation != null)
                {
                    if (isTransformed)
                    {
                        // Apply remote transformation visuals
                        ApplyRemoteTransformationVisuals(playerNetObj.gameObject);
                    }
                    else
                    {
                        transformation.ResetTransformation();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Apply transformation visuals to a remote alien player (without running full sequence)
    /// </summary>
    private void ApplyRemoteTransformationVisuals(GameObject alienObj)
    {
        var renderers = alienObj.GetComponentsInChildren<Renderer>();
        Color transformedColor = new Color(0.8f, 0.1f, 0.1f);
        Color emissionColor = new Color(1f, 0.2f, 0.2f);

        foreach (var renderer in renderers)
        {
            if (renderer != null && renderer.material != null)
            {
                renderer.material.color = transformedColor;

                if (renderer.material.HasProperty("_EmissionColor"))
                {
                    renderer.material.EnableKeyword("_EMISSION");
                    renderer.material.SetColor("_EmissionColor", emissionColor * 2f);
                }
            }
        }

        // Scale up
        alienObj.transform.localScale = alienObj.transform.localScale * 1.3f;

        // Create glow light
        var existingGlow = alienObj.GetComponentInChildren<Light>();
        if (existingGlow == null || existingGlow.name != "AlienGlow")
        {
            GameObject glowObj = new GameObject("AlienGlow");
            glowObj.transform.SetParent(alienObj.transform);
            glowObj.transform.localPosition = Vector3.up;
            var glow = glowObj.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.range = 8f;
            glow.color = emissionColor;
            glow.intensity = 2f;
        }

        Debug.Log($"[NetworkedPlayer] Applied remote transformation visuals to {alienObj.name}");
    }

    #endregion

    #region Alien Eat RPCs (Phase G2)

    /// <summary>
    /// Broadcast alien eating action to all clients
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void AlienEatServerRpc(Vector3 eatPosition, ulong npcNetworkId, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[NetworkedPlayer] SERVER: Alien {senderId} ate NPC at {eatPosition}");

        // Despawn NPC if server
        if (npcNetworkId != 0 && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(npcNetworkId, out var npcObj))
        {
            npcObj.Despawn();
        }

        // Broadcast to all clients
        AlienEatClientRpc(senderId, eatPosition);
    }

    [ClientRpc]
    private void AlienEatClientRpc(ulong alienClientId, Vector3 eatPosition)
    {
        Debug.Log($"[NetworkedPlayer] Alien {alienClientId} ate at {eatPosition}");

        // Play eating sound
        AudioManager.Instance?.PlayAlienGrowl(eatPosition);

        // Spawn blood decal
        BloodDecalManager.Instance?.SpawnBloodDecal(eatPosition);

        // Add to kill feed
        KillFeedManager.Instance?.AddAlienAteNPC();

        // Only apply hunger restore to the local alien player
        if (alienClientId == NetworkManager.Singleton.LocalClientId)
        {
            var hunger = FindFirstObjectByType<HungerSystem>();
            if (hunger != null)
            {
                hunger.Eat();
            }
        }
    }

    #endregion

    #region Hunger Sync RPCs (Phase F1)

    /// <summary>
    /// Sync hunger value to server (for other systems that need to know)
    /// Note: Hunger is primarily local, but we sync for debugging/spectator purposes
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void SyncHungerServerRpc(float hungerValue, bool isStarving, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        // Broadcast starving state to all (important for game mechanics)
        if (isStarving)
        {
            SyncStarvingStateClientRpc(senderId, isStarving);
        }
    }

    [ClientRpc]
    private void SyncStarvingStateClientRpc(ulong alienClientId, bool isStarving)
    {
        // Don't process for own client
        if (alienClientId == NetworkManager.Singleton.LocalClientId) return;

        if (isStarving)
        {
            Debug.Log($"[NetworkedPlayer] Alien {alienClientId} is STARVING!");

            // Play starving sound at alien position (all clients hear this)
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(alienClientId, out var client))
            {
                var alienPos = client.PlayerObject?.transform.position ?? Vector3.zero;
                AudioManager.Instance?.PlayAlienGrowl(alienPos);
            }
        }
    }

    /// <summary>
    /// Sync coffee drinking to all clients
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void DrinkCoffeeServerRpc(Vector3 machinePosition, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        DrinkCoffeeClientRpc(senderId, machinePosition);
    }

    [ClientRpc]
    private void DrinkCoffeeClientRpc(ulong alienClientId, Vector3 machinePosition)
    {
        Debug.Log($"[NetworkedPlayer] Alien {alienClientId} drank coffee at {machinePosition}");

        // Play coffee sound for everyone
        AudioManager.Instance?.PlayCoffeeMachine(machinePosition);

        // Only apply hunger effect to the alien who drank
        if (alienClientId == NetworkManager.Singleton.LocalClientId)
        {
            var hunger = FindFirstObjectByType<HungerSystem>();
            if (hunger != null)
            {
                hunger.DrinkCoffee();
            }
        }
    }

    #endregion
}
