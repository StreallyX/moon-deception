using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Simplified network test - handles network startup.
/// Player spawning is handled by NetworkSpawnManager.
/// Press H to Host, J to Join.
/// </summary>
public class SimpleNetworkTest : MonoBehaviour
{
    [Header("Network Spawn Manager (auto-creates if null)")]
    public NetworkSpawnManager networkSpawnManager;

    [Header("Player Prefabs (for NetworkSpawnManager)")]
    public GameObject astronautPrefab;
    public GameObject alienPrefab;

    [Header("Optional NPC Prefab")]
    public GameObject npcPrefab;

    private bool networkStarted = false;
    private bool prefabsRegistered = false;

    void Awake()
    {
        // Create camera if none exists
        if (Camera.main == null)
        {
            Debug.Log("[NetworkTest] Creating temporary camera...");
            GameObject camObj = new GameObject("TempCamera");
            Camera cam = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
            camObj.tag = "MainCamera";
            cam.transform.position = new Vector3(0, 5, -10);
            cam.transform.LookAt(Vector3.zero);
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.2f);
        }

        // Ensure RoleAnnouncementUI exists
        if (RoleAnnouncementUI.Instance == null)
        {
            GameObject uiObj = new GameObject("RoleAnnouncementUI");
            uiObj.AddComponent<RoleAnnouncementUI>();
        }
    }

    void Start()
    {
        Debug.Log("[NetworkTest] Ready. Press H to Host, J to Join.");
        StartCoroutine(EnsureNetworkManager());
    }

    System.Collections.IEnumerator EnsureNetworkManager()
    {
        yield return new WaitForSeconds(0.2f);

        if (NetworkManager.Singleton == null)
        {
            Debug.Log("[NetworkTest] Creating NetworkManager...");
            GameObject nmObj = new GameObject("NetworkManager");
            DontDestroyOnLoad(nmObj);

            NetworkManager nm = nmObj.AddComponent<NetworkManager>();
            var transport = nmObj.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            nm.NetworkConfig.NetworkTransport = transport;

            yield return null;
        }

        // Register prefabs with NetworkManager
        RegisterPrefabs();

        // IMPORTANT: Assign prefabs to managers NOW, before network starts
        AssignPrefabsToManagers();

        // Subscribe to events
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    void AssignPrefabsToManagers()
    {
        // Wait for NetworkGameManager to exist, then assign prefabs
        StartCoroutine(AssignPrefabsWhenReady());
    }

    System.Collections.IEnumerator AssignPrefabsWhenReady()
    {
        // Wait for NetworkGameManager
        float timeout = 5f;
        float elapsed = 0f;
        while (NetworkGameManager.Instance == null && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.astronautPrefab = astronautPrefab;
            NetworkGameManager.Instance.alienPrefab = alienPrefab;
            Debug.Log("[NetworkTest] Assigned player prefabs to NetworkGameManager");
        }
        else
        {
            Debug.LogWarning("[NetworkTest] NetworkGameManager not found - prefabs not assigned!");
        }

        // Also prepare NetworkSpawnManager reference
        if (networkSpawnManager == null)
        {
            networkSpawnManager = FindFirstObjectByType<NetworkSpawnManager>();
        }
    }

    void RegisterPrefabs()
    {
        if (NetworkManager.Singleton == null) return;
        if (prefabsRegistered) return;

        Debug.Log("[NetworkTest] Registering prefabs...");

        // Register prefabs - use try/catch to handle duplicates gracefully
        if (astronautPrefab != null)
        {
            var netObj = astronautPrefab.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                if (!IsPrefabRegistered(astronautPrefab))
                {
                    try
                    {
                        NetworkManager.Singleton.AddNetworkPrefab(astronautPrefab);
                        Debug.Log("[NetworkTest] Registered Astronaut prefab");
                    }
                    catch (System.Exception e)
                    {
                        Debug.Log($"[NetworkTest] Astronaut prefab already registered: {e.Message}");
                    }
                }
                else
                {
                    Debug.Log("[NetworkTest] Astronaut prefab already in list");
                }
            }
            else
            {
                Debug.LogError("[NetworkTest] Astronaut prefab missing NetworkObject!");
            }
        }
        else
        {
            Debug.LogError("[NetworkTest] Astronaut prefab is NULL!");
        }

        if (alienPrefab != null)
        {
            var netObj = alienPrefab.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                if (!IsPrefabRegistered(alienPrefab))
                {
                    try
                    {
                        NetworkManager.Singleton.AddNetworkPrefab(alienPrefab);
                        Debug.Log("[NetworkTest] Registered Alien prefab");
                    }
                    catch (System.Exception e)
                    {
                        Debug.Log($"[NetworkTest] Alien prefab already registered: {e.Message}");
                    }
                }
                else
                {
                    Debug.Log("[NetworkTest] Alien prefab already in list");
                }
            }
            else
            {
                Debug.LogError("[NetworkTest] Alien prefab missing NetworkObject!");
            }
        }
        else
        {
            Debug.LogError("[NetworkTest] Alien prefab is NULL!");
        }

        if (npcPrefab != null)
        {
            var netObj = npcPrefab.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                if (!IsPrefabRegistered(npcPrefab))
                {
                    try
                    {
                        NetworkManager.Singleton.AddNetworkPrefab(npcPrefab);
                        Debug.Log("[NetworkTest] Registered NPC prefab");
                    }
                    catch { }
                }
            }
        }

        prefabsRegistered = true;
    }

    bool IsPrefabRegistered(GameObject prefab)
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.NetworkConfig == null)
            return false;

        var prefabsList = NetworkManager.Singleton.NetworkConfig.Prefabs;
        if (prefabsList == null || prefabsList.Prefabs == null) return false;

        foreach (var networkPrefab in prefabsList.Prefabs)
        {
            if (networkPrefab.Prefab == prefab)
                return true;
        }
        return false;
    }

    void OnServerStarted()
    {
        Debug.Log("[NetworkTest] Server started - creating NetworkSpawnManager...");

        // Create NetworkSpawnManager if needed (NOT as a NetworkObject - just local)
        if (networkSpawnManager == null)
        {
            GameObject spawnMgrObj = new GameObject("NetworkSpawnManager");
            networkSpawnManager = spawnMgrObj.AddComponent<NetworkSpawnManager>();
            networkSpawnManager.npcPrefab = npcPrefab;
            // Note: NPC count is now determined by spawn points in MapZones (1 NPC per spawn point)

            // DON'T spawn as NetworkObject - it doesn't need to be synced
            // The manager just runs on the server and spawns players
            Debug.Log("[NetworkTest] NetworkSpawnManager created (server-only)");
        }

        // Set prefabs on NetworkGameManager (it handles player spawning)
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.astronautPrefab = astronautPrefab;
            NetworkGameManager.Instance.alienPrefab = alienPrefab;
        }

        // Destroy temp camera (player will have their own)
        DestroyTempCamera();
    }

    void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[NetworkTest] Client {clientId} connected (local: {NetworkManager.Singleton.LocalClientId})");

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            // We connected - destroy temp camera
            DestroyTempCamera();
        }
    }

    void DestroyTempCamera()
    {
        // Wait a moment for player camera to be ready
        StartCoroutine(DestroyTempCameraDelayed());
    }

    System.Collections.IEnumerator DestroyTempCameraDelayed()
    {
        yield return new WaitForSeconds(0.5f);

        GameObject tempCam = GameObject.Find("TempCamera");
        if (tempCam != null)
        {
            Destroy(tempCam);
            Debug.Log("[NetworkTest] Destroyed temp camera");
        }
    }

    void Update()
    {
        if (networkStarted) return;

        if (Input.GetKeyDown(KeyCode.H))
        {
            StartHost();
        }
        else if (Input.GetKeyDown(KeyCode.J))
        {
            StartClient();
        }
    }

    void StartHost()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NetworkTest] NetworkManager not ready!");
            return;
        }

        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[NetworkTest] Already connected!");
            return;
        }

        Debug.Log("[NetworkTest] Starting as HOST...");
        networkStarted = true;
        NetworkManager.Singleton.StartHost();
    }

    void StartClient()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NetworkTest] NetworkManager not ready!");
            return;
        }

        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[NetworkTest] Already connected!");
            return;
        }

        Debug.Log("[NetworkTest] Starting as CLIENT...");
        networkStarted = true;
        NetworkManager.Singleton.StartClient();
    }

    void OnGUI()
    {
        // Hide debug UI during gameplay - only show in lobby/menu
        if (GameManager.Instance != null)
        {
            var phase = GameManager.Instance.CurrentPhase;
            if (phase == GameManager.GamePhase.Playing ||
                phase == GameManager.GamePhase.Chaos ||
                phase == GameManager.GamePhase.Ended)
            {
                return; // Don't show debug UI during game
            }
        }

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 18;
        style.normal.textColor = Color.white;

        GUIStyle bigStyle = new GUIStyle(GUI.skin.label);
        bigStyle.fontSize = 32;
        bigStyle.fontStyle = FontStyle.Bold;

        string status = "";
        string role = "";
        Color roleColor = Color.white;

        if (NetworkManager.Singleton == null)
        {
            status = "Press H = Host | J = Join";
        }
        else if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            status = "Press H = Host | J = Join";
        }
        else if (NetworkManager.Singleton.IsHost)
        {
            int clients = NetworkManager.Singleton.ConnectedClientsIds.Count;
            status = $"HOST | Players: {clients}";
            role = "ASTRONAUT";
            roleColor = Color.cyan;

            if (clients < 2)
                status += "\nWaiting for player 2...";
        }
        else if (NetworkManager.Singleton.IsConnectedClient)
        {
            status = "CLIENT | Connected";
            role = "ALIEN";
            roleColor = new Color(0.8f, 0.3f, 0.8f);
        }
        else if (NetworkManager.Singleton.IsClient)
        {
            status = "Connecting...";
        }

        GUI.Box(new Rect(10, 10, 320, 100), "");
        GUI.Label(new Rect(20, 15, 300, 30), status, style);

        if (!string.IsNullOrEmpty(role))
        {
            bigStyle.normal.textColor = roleColor;
            GUI.Label(new Rect(20, 50, 300, 40), role, bigStyle);
        }

        GUIStyle smallStyle = new GUIStyle(GUI.skin.label);
        smallStyle.fontSize = 12;
        smallStyle.normal.textColor = Color.gray;
        GUI.Label(new Rect(20, 85, 300, 20), "Networked multiplayer test", smallStyle);
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }
}
