using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Simple network test - spawns a cube for each connected player.
/// Add this to a GameObject in your game scene.
/// </summary>
public class SimpleNetworkTest : MonoBehaviour
{
    [Header("Player Prefabs (Assign in Inspector)")]
    public GameObject astronautPrefab;
    public GameObject alienPrefab;

    [Header("Spawn Points")]
    public Transform astronautSpawnPoint;
    public Transform alienSpawnPoint;

    private bool hasStartedNetwork = false;

    void Awake()
    {
        // Create a camera if there isn't one
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
    }

    void Start()
    {
        Debug.Log("[NetworkTest] Start called");

        // Try to start network if not already started
        StartCoroutine(TryStartNetwork());
    }

    System.Collections.IEnumerator TryStartNetwork()
    {
        yield return new WaitForSeconds(0.5f);

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NetworkTest] NetworkManager.Singleton is NULL! Creating one...");

            GameObject nmObj = new GameObject("NetworkManager");
            nmObj.AddComponent<NetworkManager>();
            var transport = nmObj.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();

            yield return null;

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.NetworkConfig.NetworkTransport = transport;
            }
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            Debug.Log($"[NetworkTest] NetworkManager found. IsServer: {NetworkManager.Singleton.IsServer}, IsClient: {NetworkManager.Singleton.IsClient}, IsConnectedClient: {NetworkManager.Singleton.IsConnectedClient}");
        }
    }

    private bool gameStarted = false;
    private string myRole = "";

    void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[NetworkTest] === CLIENT {clientId} CONNECTED! ===");

        // Check if this is the local client
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            // Host (clientId 0) = Astronaut, Others = Alien
            bool isAstronaut = (clientId == 0);
            myRole = isAstronaut ? "ASTRONAUT" : "ALIEN";

            Debug.Log($"[NetworkTest] I am the {myRole}!");

            // Start the game for this player
            StartGameForRole(isAstronaut);
        }

        // If we're host and 2 players connected, game is ready
        if (NetworkManager.Singleton.IsHost && NetworkManager.Singleton.ConnectedClientsIds.Count >= 2)
        {
            Debug.Log("[NetworkTest] 2 players connected - GAME START!");
        }
    }

    void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[NetworkTest] Client disconnected: {clientId}");
    }

    void StartGameForRole(bool isAstronaut)
    {
        if (gameStarted) return;
        gameStarted = true;

        Debug.Log($"[NetworkTest] Starting game as {(isAstronaut ? "Astronaut" : "Alien")}...");

        GameObject spawnedPlayer = null;
        Vector3 spawnPos = GetRandomSpawnPoint(isAstronaut);

        if (isAstronaut)
        {
            // Spawn Astronaut
            if (astronautPrefab != null)
            {
                spawnedPlayer = Instantiate(astronautPrefab, spawnPos, Quaternion.identity);
                spawnedPlayer.name = "Player_Astronaut";
                Debug.Log($"[NetworkTest] Astronaut SPAWNED at {spawnPos}");
            }
            else
            {
                Debug.LogError("[NetworkTest] Astronaut Prefab not assigned!");
            }
        }
        else
        {
            // Spawn Alien
            if (alienPrefab != null)
            {
                spawnedPlayer = Instantiate(alienPrefab, spawnPos, Quaternion.identity);
                spawnedPlayer.name = "Player_Alien";
                Debug.Log($"[NetworkTest] Alien SPAWNED at {spawnPos}");
            }
            else
            {
                Debug.LogError("[NetworkTest] Alien Prefab not assigned!");
            }
        }

        // Check if spawned player has camera
        if (spawnedPlayer != null)
        {
            Camera playerCam = spawnedPlayer.GetComponentInChildren<Camera>(true);
            if (playerCam != null)
            {
                playerCam.gameObject.SetActive(true);

                // Destroy temp camera
                Camera tempCam = GameObject.Find("TempCamera")?.GetComponent<Camera>();
                if (tempCam != null)
                {
                    Destroy(tempCam.gameObject);
                }
            }
        }

        // Start GameManager if exists
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartGame();
        }
    }

    Vector3 GetRandomSpawnPoint(bool isAstronaut)
    {
        // Try to use MapManager spawn points
        if (MapManager.Instance != null)
        {
            var zones = MapManager.Instance.AllZones;
            if (zones != null && zones.Count > 0)
            {
                // Astronaut spawns in Command zone, Alien in random other zone
                foreach (var zone in zones)
                {
                    if (isAstronaut && zone.zoneType == MapZone.ZoneType.Command)
                    {
                        if (zone.npcSpawnPoints != null && zone.npcSpawnPoints.Length > 0)
                        {
                            Transform point = zone.npcSpawnPoints[Random.Range(0, zone.npcSpawnPoints.Length)];
                            Debug.Log($"[NetworkTest] Using spawn point from {zone.zoneType} zone");
                            return point.position;
                        }
                    }
                    else if (!isAstronaut && zone.zoneType != MapZone.ZoneType.Command)
                    {
                        if (zone.npcSpawnPoints != null && zone.npcSpawnPoints.Length > 0)
                        {
                            Transform point = zone.npcSpawnPoints[Random.Range(0, zone.npcSpawnPoints.Length)];
                            Debug.Log($"[NetworkTest] Using spawn point from {zone.zoneType} zone");
                            return point.position;
                        }
                    }
                }

                // Fallback: use any zone with spawn points
                foreach (var zone in zones)
                {
                    if (zone.npcSpawnPoints != null && zone.npcSpawnPoints.Length > 0)
                    {
                        Transform point = zone.npcSpawnPoints[Random.Range(0, zone.npcSpawnPoints.Length)];
                        return point.position;
                    }
                }
            }
        }

        // Manual spawn points fallback
        if (isAstronaut && astronautSpawnPoint != null)
            return astronautSpawnPoint.position;
        if (!isAstronaut && alienSpawnPoint != null)
            return alienSpawnPoint.position;

        // Default fallback
        Debug.LogWarning("[NetworkTest] No spawn points found, using default position");
        return isAstronaut ? new Vector3(0, 1, 0) : new Vector3(5, 1, 5);
    }

    void Update()
    {
        // Manual network start controls
        if (Input.GetKeyDown(KeyCode.H))
        {
            StartAsHost();
        }
        else if (Input.GetKeyDown(KeyCode.J))
        {
            StartAsClient();
        }
    }

    void StartAsHost()
    {
        EnsureNetworkManager();

        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            Debug.Log("[NetworkTest] Starting as HOST...");
            NetworkManager.Singleton.StartHost();
        }
    }

    void StartAsClient()
    {
        EnsureNetworkManager();

        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            Debug.Log("[NetworkTest] Starting as CLIENT...");
            NetworkManager.Singleton.StartClient();
        }
    }

    void EnsureNetworkManager()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.Log("[NetworkTest] Creating NetworkManager...");
            GameObject nmObj = new GameObject("NetworkManager");
            DontDestroyOnLoad(nmObj);

            NetworkManager nm = nmObj.AddComponent<NetworkManager>();
            var transport = nmObj.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            nm.NetworkConfig.NetworkTransport = transport;
        }
    }

    void OnGUI()
    {
        // Show connection status
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
        else if (NetworkManager.Singleton.IsHost)
        {
            int clients = NetworkManager.Singleton.ConnectedClientsIds.Count;

            if (gameStarted)
            {
                status = $"PLAYING | Players: {clients}";
                role = "ASTRONAUT";
                roleColor = Color.cyan;
            }
            else
            {
                status = $"HOST | Players: {clients}";
                role = "YOU ARE THE ASTRONAUT";
                roleColor = Color.cyan;

                if (clients < 2)
                    status += "\nWaiting for player 2...";
                else
                    status += "\nGAME STARTING!";
            }
        }
        else if (NetworkManager.Singleton.IsConnectedClient)
        {
            if (gameStarted)
            {
                status = "PLAYING";
                role = "ALIEN";
                roleColor = new Color(0.8f, 0.3f, 0.8f);
            }
            else
            {
                status = "CLIENT | Connected!";
                role = "YOU ARE AN ALIEN";
                roleColor = new Color(0.8f, 0.3f, 0.8f);
            }
        }
        else if (NetworkManager.Singleton.IsClient)
        {
            status = "Connecting...";
        }
        else
        {
            status = "Press H = Host | J = Join";
        }

        // Draw background
        GUI.Box(new Rect(10, 10, 320, 120), "");
        GUI.Label(new Rect(20, 15, 300, 30), status, style);

        // Draw role
        if (!string.IsNullOrEmpty(role))
        {
            bigStyle.normal.textColor = roleColor;
            GUI.Label(new Rect(20, 50, 300, 40), role, bigStyle);
        }

        // Instructions
        GUIStyle smallStyle = new GUIStyle(GUI.skin.label);
        smallStyle.fontSize = 12;
        smallStyle.normal.textColor = Color.gray;
        GUI.Label(new Rect(20, 95, 300, 20), "Network test - Cubes show connected players", smallStyle);
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
}
