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

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // CRITICAL: Check if we have the right NetworkTransform type
        CheckNetworkTransformAuthority();

        // Subscribe to role changes FIRST (so we catch any updates)
        IsAstronautRole.OnValueChanged += OnRoleChanged;

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
                // Server/Host - setup immediately (role is already set)
                isAstronaut = IsAstronautRole.Value;
                EnableLocalPlayer();
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
        // Wait for NetworkVariable to sync from server
        float timeout = 2f;
        float elapsed = 0f;
        bool initialValue = IsAstronautRole.Value;

        Debug.Log($"[NetworkedPlayer] CLIENT: Waiting for role sync... current={initialValue}");

        // Wait until either timeout or we get a different value (meaning sync happened)
        // Or if value matches what the prefab expects (alienController exists = should be alien)
        while (elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;

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

        if (IsOwner)
        {
            EnableLocalPlayer();
        }
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
        Light[] lights = FindObjectsOfType<Light>();
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
}
