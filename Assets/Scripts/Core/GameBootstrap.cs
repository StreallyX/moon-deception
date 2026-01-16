using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Bootstrap script that initializes all game systems.
/// Add this to an empty GameObject in your scene.
/// Creates all necessary managers if they don't exist.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    [Header("Auto-Create Managers")]
    public bool createGameLoader = true;
    public bool createAudioManager = true;
    public bool createNetworkAudioManager = true;
    public bool createMenuManager = true;
    public bool createPostProcessController = true;
    public bool createChaosLightingController = true;
    public bool createMapManager = true;
    public bool createSpawnManager = true;
    public bool createBloodDecalManager = true;
    public bool createNetworkManager = true;
    public bool createNetworkGameManager = true;
    public bool createRoleAnnouncementUI = true;

    [Header("Player Setup")]
    public bool addCameraShakeToPlayer = true;
    public bool addHealthToAstronaut = true;
    public bool addAbilitiesToAlien = true;

    private static bool hasInitialized = false;

    void Awake()
    {
        if (hasInitialized)
        {
            Destroy(gameObject);
            return;
        }
        hasInitialized = true;

        DontDestroyOnLoad(gameObject);
        InitializeSystems();
    }

    void InitializeSystems()
    {
        Debug.Log("[GameBootstrap] Initializing game systems...");

        // Game Loader (creates loading screen and manages initialization)
        if (createGameLoader && GameLoader.Instance == null)
        {
            GameObject loaderObj = new GameObject("GameLoader");
            loaderObj.AddComponent<GameLoader>();
            loaderObj.AddComponent<LoadingScreen>();
            DontDestroyOnLoad(loaderObj);
            Debug.Log("[GameBootstrap] Created GameLoader with LoadingScreen");
        }

        // Audio Manager
        if (createAudioManager && AudioManager.Instance == null)
        {
            GameObject audioManagerObj = new GameObject("AudioManager");
            audioManagerObj.AddComponent<AudioManager>();
            DontDestroyOnLoad(audioManagerObj);
            Debug.Log("[GameBootstrap] Created AudioManager");
        }

        // Network Audio Manager (local singleton that delegates RPCs through NetworkedPlayer)
        if (createNetworkAudioManager && NetworkAudioManager.Instance == null)
        {
            GameObject networkAudioObj = new GameObject("NetworkAudioManager");
            networkAudioObj.AddComponent<NetworkAudioManager>();
            DontDestroyOnLoad(networkAudioObj);
            Debug.Log("[GameBootstrap] Created NetworkAudioManager");
        }

        // Menu Manager
        if (createMenuManager && MenuManager.Instance == null)
        {
            GameObject menuManagerObj = new GameObject("MenuManager");
            menuManagerObj.AddComponent<MenuManager>();
            DontDestroyOnLoad(menuManagerObj);
            Debug.Log("[GameBootstrap] Created MenuManager");
        }

        // Post Process Controller
        if (createPostProcessController && PostProcessController.Instance == null)
        {
            GameObject postProcessObj = new GameObject("PostProcessController");
            postProcessObj.AddComponent<PostProcessController>();
            DontDestroyOnLoad(postProcessObj);
            Debug.Log("[GameBootstrap] Created PostProcessController");
        }

        // Chaos Lighting Controller
        if (createChaosLightingController && ChaosLightingController.Instance == null)
        {
            GameObject chaosLightingObj = new GameObject("ChaosLightingController");
            chaosLightingObj.AddComponent<ChaosLightingController>();
            DontDestroyOnLoad(chaosLightingObj);
            Debug.Log("[GameBootstrap] Created ChaosLightingController");
        }

        // Map Manager
        if (createMapManager && MapManager.Instance == null)
        {
            GameObject mapManagerObj = new GameObject("MapManager");
            mapManagerObj.AddComponent<MapManager>();
            DontDestroyOnLoad(mapManagerObj);
            Debug.Log("[GameBootstrap] Created MapManager");
        }

        // Spawn Manager
        if (createSpawnManager && SpawnManager.Instance == null)
        {
            GameObject spawnManagerObj = new GameObject("SpawnManager");
            spawnManagerObj.AddComponent<SpawnManager>();
            DontDestroyOnLoad(spawnManagerObj);
            Debug.Log("[GameBootstrap] Created SpawnManager");
        }

        // Blood Decal Manager
        if (createBloodDecalManager && BloodDecalManager.Instance == null)
        {
            GameObject bloodDecalObj = new GameObject("BloodDecalManager");
            bloodDecalObj.AddComponent<BloodDecalManager>();
            DontDestroyOnLoad(bloodDecalObj);
            Debug.Log("[GameBootstrap] Created BloodDecalManager");
        }

        // Network Manager
        if (createNetworkManager && NetworkManager.Singleton == null)
        {
            GameObject networkManagerObj = new GameObject("NetworkManager");
            networkManagerObj.AddComponent<NetworkManager>();
            networkManagerObj.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            networkManagerObj.AddComponent<NetworkManagerSetup>();
            networkManagerObj.AddComponent<NetworkConnectionUI>();
            DontDestroyOnLoad(networkManagerObj);
            Debug.Log("[GameBootstrap] Created NetworkManager");
        }

        // Network Game Manager (handles roles and game state)
        // NOTE: NetworkGameManager should be created as a PREFAB in the scene for proper sync.
        // For now, we create it locally on both server and client.
        // The server version will handle spawning players, client version will receive RPCs.
        if (createNetworkGameManager && NetworkGameManager.Instance == null)
        {
            GameObject networkGameObj = new GameObject("NetworkGameManager");
            networkGameObj.AddComponent<NetworkGameManager>();
            // NOTE: NOT adding NetworkObject here - it needs to be a registered prefab for proper sync.
            // For now, NetworkGameManager handles local state only.
            // Server uses it to manage game, clients receive updates via other means.
            DontDestroyOnLoad(networkGameObj);
            Debug.Log("[GameBootstrap] Created NetworkGameManager (local singleton)");
        }

        // Role Announcement UI
        if (createRoleAnnouncementUI && RoleAnnouncementUI.Instance == null)
        {
            GameObject roleUIObj = new GameObject("RoleAnnouncementUI");
            roleUIObj.AddComponent<RoleAnnouncementUI>();
            DontDestroyOnLoad(roleUIObj);
            Debug.Log("[GameBootstrap] Created RoleAnnouncementUI");
        }

        // Add CameraShake to player camera
        if (addCameraShakeToPlayer)
        {
            StartCoroutine(SetupCameraShake());
        }

        // Add components to player and alien
        StartCoroutine(SetupPlayerAndAlien());

        Debug.Log("[GameBootstrap] All systems initialized");
    }

    System.Collections.IEnumerator SetupCameraShake()
    {
        // Wait a frame for player to be set up
        yield return null;

        // Find player camera and add CameraShake
        PlayerMovement player = FindObjectOfType<PlayerMovement>();
        if (player != null)
        {
            Camera playerCam = player.GetCamera();
            if (playerCam == null)
            {
                playerCam = player.GetComponentInChildren<Camera>();
            }

            if (playerCam != null && playerCam.GetComponent<CameraShake>() == null)
            {
                playerCam.gameObject.AddComponent<CameraShake>();
                Debug.Log("[GameBootstrap] Added CameraShake to player camera");
            }
        }

        // Also check for alien camera
        AlienController alien = FindObjectOfType<AlienController>();
        if (alien != null)
        {
            Camera alienCam = alien.GetCamera();
            if (alienCam != null && alienCam.GetComponent<CameraShake>() == null)
            {
                alienCam.gameObject.AddComponent<CameraShake>();
                Debug.Log("[GameBootstrap] Added CameraShake to alien camera");
            }
        }
    }

    System.Collections.IEnumerator SetupPlayerAndAlien()
    {
        yield return null;

        // SKIP setup if we're in networked mode - NetworkedPlayer handles this
        if (NetworkManager.Singleton != null &&
            (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
        {
            Debug.Log("[GameBootstrap] Skipping player setup - networked mode active");
            yield break;
        }

        // Only setup in single player / testing mode
        Debug.Log("[GameBootstrap] Setting up players for single player mode");

        // Setup astronaut
        if (addHealthToAstronaut)
        {
            PlayerMovement player = FindObjectOfType<PlayerMovement>();
            if (player != null && player.GetComponent<AstronautHealth>() == null)
            {
                player.gameObject.AddComponent<AstronautHealth>();
                Debug.Log("[GameBootstrap] Added AstronautHealth to player");
            }
        }

        // Setup alien
        if (addAbilitiesToAlien)
        {
            AlienController alien = FindObjectOfType<AlienController>();
            if (alien != null)
            {
                if (alien.GetComponent<AlienAbilities>() == null)
                {
                    alien.gameObject.AddComponent<AlienAbilities>();
                    Debug.Log("[GameBootstrap] Added AlienAbilities to alien");
                }

                if (alien.GetComponent<AlienTransformation>() == null)
                {
                    alien.gameObject.AddComponent<AlienTransformation>();
                    Debug.Log("[GameBootstrap] Added AlienTransformation to alien");
                }

                if (alien.GetComponent<AlienHealth>() == null)
                {
                    alien.gameObject.AddComponent<AlienHealth>();
                    Debug.Log("[GameBootstrap] Added AlienHealth to alien");
                }
            }
        }
    }

    void OnDestroy()
    {
        if (hasInitialized)
        {
            hasInitialized = false;
        }
    }
}
