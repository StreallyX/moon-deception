using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using System.Collections;

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
        // If we have the old server-authoritative NetworkTransform, warn loudly!
        CheckNetworkTransformAuthority();

        // Subscribe to role changes FIRST (so we catch any updates)
        IsAstronautRole.OnValueChanged += OnRoleChanged;

        // CRITICAL: Sync local field from NetworkVariable IMMEDIATELY
        // On server, the field is already set by NetworkSpawnManager before spawn
        // On client, we need to read from the NetworkVariable which syncs from server
        if (!IsServer)
        {
            isAstronaut = IsAstronautRole.Value;
            Debug.Log($"[NetworkedPlayer] CLIENT: Synced isAstronaut from NetworkVariable = {isAstronaut}");
        }

        Debug.Log($"[NetworkedPlayer] Spawned - IsOwner: {IsOwner}, IsServer: {IsServer}, ClientId: {OwnerClientId}, isAstronaut: {isAstronaut}, NetworkVar: {IsAstronautRole.Value}");

        // Find ALL components
        FindAllComponents();

        // FIRST: Disable ALL gameplay components
        DisableAllComponents();

        if (IsOwner)
        {
            // This is OUR player - enable the RIGHT controls and camera
            EnableLocalPlayer();

            // Safety check: If we're a client and NetworkVariable hasn't synced yet (still default true),
            // start a coroutine to check again after a brief delay
            if (!IsServer && IsAstronautRole.Value == true && alienController != null)
            {
                Debug.Log("[NetworkedPlayer] CLIENT: NetworkVariable might not have synced yet, starting delayed check...");
                StartCoroutine(DelayedRoleCheck());
            }
        }
        else
        {
            // This is another player - keep everything disabled, just show the model
            DisableRemotePlayer();
        }
    }

    private IEnumerator DelayedRoleCheck()
    {
        yield return new WaitForSeconds(0.2f);

        // Check if the role has changed from default
        if (!hasSetupCompleted && IsOwner && IsAstronautRole.Value != isAstronaut)
        {
            Debug.Log($"[NetworkedPlayer] Delayed check: Role changed! NetworkVar={IsAstronautRole.Value}, local={isAstronaut}");
            isAstronaut = IsAstronautRole.Value;
            DisableAllComponents();
            EnableLocalPlayer();
        }
    }

    void FindAllComponents()
    {
        // Astronaut
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        if (playerShooting == null) playerShooting = GetComponent<PlayerShooting>();
        if (stressSystem == null) stressSystem = GetComponent<StressSystem>();

        // Alien
        if (alienController == null) alienController = GetComponent<AlienController>();
        if (hungerSystem == null) hungerSystem = GetComponent<HungerSystem>();
        if (alienHealth == null) alienHealth = GetComponent<AlienHealth>();
        if (alienAbilities == null) alienAbilities = GetComponent<AlienAbilities>();

        // Shared
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);
        if (audioListener == null) audioListener = GetComponentInChildren<AudioListener>(true);

        // DEBUG: Log what we found
        Debug.Log($"[NetworkedPlayer] FindAllComponents on {gameObject.name}:");
        Debug.Log($"  - PlayerMovement: {(playerMovement != null ? "FOUND" : "NULL")}");
        Debug.Log($"  - PlayerShooting: {(playerShooting != null ? "FOUND" : "NULL")}");
        Debug.Log($"  - AlienController: {(alienController != null ? "FOUND" : "NULL")}");
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
        }
        else
        {
            // === ALIEN SETUP ===
            Debug.Log("[NetworkedPlayer] Setting up ALIEN controls");

            // Enable Alien components
            if (alienController != null)
            {
                alienController.enabled = true;
                Debug.Log($"[NetworkedPlayer] ENABLED AlienController (was: {!alienController.enabled})");
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
            }

            // Make sure Astronaut components stay DISABLED
            if (playerMovement != null) playerMovement.enabled = false;
            if (playerShooting != null) playerShooting.enabled = false;
            if (stressSystem != null) stressSystem.enabled = false;

            // Update UI for Alien
            if (GameUIManager.Instance != null)
            {
                GameUIManager.Instance.SetPlayerType(PlayerType.Alien);
            }
        }

        // Enable camera and audio for local player
        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(true);
            playerCamera.enabled = true;
            Debug.Log($"[NetworkedPlayer] Enabled camera: {playerCamera.gameObject.name}");
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
}
