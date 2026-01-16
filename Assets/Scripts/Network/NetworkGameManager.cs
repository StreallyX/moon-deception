using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages networked game state - role assignment, spawning, win conditions.
/// Server authoritative. Works as local singleton, with network events handled separately.
/// </summary>
public class NetworkGameManager : MonoBehaviour
{
    public static NetworkGameManager Instance { get; private set; }

    [Header("Player Prefabs")]
    public GameObject astronautPrefab;
    public GameObject alienPrefab;

    [Header("Settings")]
    public string menuSceneName = "MainMenu";

    // Game state (local, synced via events)
    [SerializeField] private GamePhase currentPhase = GamePhase.WaitingForPlayers;
    [SerializeField] private float gameTimer = 600f; // 10 minutes
    [SerializeField] private ulong astronautClientId = ulong.MaxValue;
    [SerializeField] private bool worldIsReady = false;

    // Local state
    private Dictionary<ulong, PlayerRole> playerRoles = new Dictionary<ulong, PlayerRole>();
    private List<ulong> pendingClients = new List<ulong>(); // Clients waiting for game to start
    private bool gameStarted = false;
    private bool isInitialized = false;

    public enum GamePhase
    {
        WaitingForPlayers,
        Starting,
        Playing,
        Chaos,
        Ended
    }

    public enum PlayerRole
    {
        None,
        Astronaut,
        Alien
    }

    // Events
    public event System.Action<PlayerRole> OnLocalRoleAssigned;
    public event System.Action<GamePhase> OnPhaseChanged;
    public event System.Action<bool> OnGameEnded; // true = astronaut wins
    public event System.Action OnWorldReady; // Called when server says world is ready

    public bool IsWorldReady => worldIsReady;

    // Network helpers
    private bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
    private bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
    private bool IsClient => NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient;
    private bool IsNetworked => NetworkManager.Singleton != null &&
                               (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient);

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // Subscribe to NetworkManager events when it's ready
        StartCoroutine(SubscribeToNetworkEvents());
    }

    System.Collections.IEnumerator SubscribeToNetworkEvents()
    {
        // Wait for NetworkManager to exist
        while (NetworkManager.Singleton == null)
        {
            yield return new WaitForSeconds(0.1f);
        }

        // Subscribe to server/client start events
        NetworkManager.Singleton.OnServerStarted += OnNetworkServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback += OnNetworkClientConnected;

        Debug.Log("[NetworkGame] Subscribed to NetworkManager events. Waiting for Host/Join...");
    }

    void OnNetworkServerStarted()
    {
        Debug.Log("[NetworkGame] SERVER STARTED - Initializing...");
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        StartCoroutine(InitializeWorldAndStart());
        isInitialized = true;
    }

    void OnNetworkClientConnected(ulong clientId)
    {
        // Only care about our own connection as a client (not host)
        if (!IsServer && clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("[NetworkGame] CLIENT CONNECTED - Ready to receive game state");
            isInitialized = true;
        }
    }

    System.Collections.IEnumerator InitializeWorldAndStart()
    {
        Debug.Log("[NetworkGame] SERVER: Initializing world...");

        // Wait for player prefabs to be assigned (SimpleNetworkTest assigns them)
        float timeout = 5f;
        float elapsed = 0f;
        Debug.Log("[NetworkGame] SERVER: Waiting for player prefabs...");

        while ((astronautPrefab == null || alienPrefab == null) && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (astronautPrefab == null || alienPrefab == null)
        {
            Debug.LogError("[NetworkGame] Player prefabs not assigned! Make sure SimpleNetworkTest has prefabs set in Inspector.");
            Debug.LogError($"  - astronautPrefab: {(astronautPrefab != null ? "OK" : "MISSING")}");
            Debug.LogError($"  - alienPrefab: {(alienPrefab != null ? "OK" : "MISSING")}");
            yield break;
        }
        Debug.Log("[NetworkGame] SERVER: Player prefabs ready!");

        // Wait for MapManager to find zones
        timeout = 10f;
        elapsed = 0f;

        while (elapsed < timeout)
        {
            if (MapManager.Instance != null && MapManager.Instance.ZoneCount > 0)
            {
                Debug.Log($"[NetworkGame] SERVER: Found {MapManager.Instance.ZoneCount} zones");
                break;
            }

            // Try to find zones
            if (MapManager.Instance != null)
            {
                MapManager.Instance.FindAllZonesInScene();
            }

            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }

        // Spawn NPCs on server
        if (NetworkSpawnManager.Instance != null)
        {
            Debug.Log("[NetworkGame] SERVER: NetworkSpawnManager will handle spawning");
        }

        // Mark world as ready
        worldIsReady = true;
        Debug.Log("[NetworkGame] SERVER: World is READY!");

        // Notify local and clients
        OnWorldReady?.Invoke();
        NotifyWorldReadyToClients();

        // Now start the game
        StartGame();
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= OnNetworkServerStarted;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnNetworkClientConnected;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    // ==================== SERVER METHODS ====================

    void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[NetworkGame] Client connected: {clientId}, gameStarted={gameStarted}");

        if (gameStarted)
        {
            // Game already started - spawn immediately as alien
            if (!playerRoles.ContainsKey(clientId))
            {
                Debug.Log($"[NetworkGame] Late joiner {clientId} - spawning their alien player...");
                PlayerRole role = PlayerRole.Alien;
                playerRoles[clientId] = role;
                SpawnPlayerForClient(clientId, role);
            }
        }
        else
        {
            // Game not started yet - add to pending list
            if (!pendingClients.Contains(clientId))
            {
                pendingClients.Add(clientId);
                Debug.Log($"[NetworkGame] Client {clientId} added to pending list (waiting for game start)");
            }
        }
    }

    void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[NetworkGame] Client disconnected: {clientId}");
        playerRoles.Remove(clientId);

        // Check if astronaut left
        if (clientId == astronautClientId)
        {
            // Aliens win by default
            EndGame(false);
        }
    }

    void StartGame()
    {
        if (!IsServer || gameStarted) return;
        gameStarted = true;

        Debug.Log("[NetworkGame] Starting game, assigning roles...");

        // Get all connected clients + any pending clients
        var clients = NetworkManager.Singleton.ConnectedClientsIds.ToList();

        // Also add any pending clients that might have been missed
        foreach (var pendingId in pendingClients)
        {
            if (!clients.Contains(pendingId))
            {
                clients.Add(pendingId);
                Debug.Log($"[NetworkGame] Added pending client {pendingId} to spawn list");
            }
        }
        pendingClients.Clear();

        Debug.Log($"[NetworkGame] Total clients to spawn: {clients.Count}");
        foreach (var c in clients)
        {
            Debug.Log($"[NetworkGame]   - Client {c}");
        }

        if (clients.Count == 0)
        {
            Debug.LogError("[NetworkGame] No clients connected!");
            return;
        }

        // Randomly pick astronaut (host/first client is usually astronaut for testing)
        int astronautIndex = Random.Range(0, clients.Count);
        ulong astronautId = clients[astronautIndex];
        astronautClientId = astronautId;

        Debug.Log($"[NetworkGame] Astronaut assigned to client {astronautId}");

        // Assign roles and spawn
        foreach (var clientId in clients)
        {
            PlayerRole role = (clientId == astronautId) ? PlayerRole.Astronaut : PlayerRole.Alien;
            playerRoles[clientId] = role;
            Debug.Log($"[NetworkGame] Spawning {role} for client {clientId}...");

            // Spawn player
            SpawnPlayerForClient(clientId, role);
        }

        // Start playing
        SetPhase(GamePhase.Playing);

        // IMPORTANT: Also start GameManager so all systems know we're playing!
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartGame();
            Debug.Log("[NetworkGame] Called GameManager.StartGame() to sync phases");
        }

        Debug.Log($"[NetworkGame] Game started! Astronaut: Client {astronautId}");
    }

    void SpawnPlayerForClient(ulong clientId, PlayerRole role)
    {
        GameObject prefab = (role == PlayerRole.Astronaut) ? astronautPrefab : alienPrefab;

        if (prefab == null)
        {
            Debug.LogError($"[NetworkGame] Missing prefab for role {role}!");
            return;
        }

        // Get spawn point
        Vector3 spawnPos = GetSpawnPosition(role);
        Quaternion spawnRot = Quaternion.identity;

        // Spawn networked player
        GameObject playerObj = Instantiate(prefab, spawnPos, spawnRot);
        NetworkObject netObj = playerObj.GetComponent<NetworkObject>();
        NetworkedPlayer networkedPlayer = playerObj.GetComponent<NetworkedPlayer>();

        if (netObj != null)
        {
            // IMPORTANT: Set the role BEFORE spawning so it syncs to clients!
            if (networkedPlayer != null)
            {
                bool isAstronaut = (role == PlayerRole.Astronaut);
                networkedPlayer.isAstronaut = isAstronaut;
                // The NetworkVariable will be set in OnNetworkSpawn after we call Spawn
                Debug.Log($"[NetworkGame] Set isAstronaut={isAstronaut} on prefab before spawn");
            }

            netObj.SpawnAsPlayerObject(clientId);

            // Set the NetworkVariable AFTER spawn (it needs to be spawned to sync)
            if (networkedPlayer != null)
            {
                networkedPlayer.SetRole(role == PlayerRole.Astronaut);
                Debug.Log($"[NetworkGame] Set NetworkVariable role for client {clientId}: {role}");
            }

            Debug.Log($"[NetworkGame] Spawned {role} for client {clientId} at {spawnPos}");
        }
        else
        {
            Debug.LogError($"[NetworkGame] Prefab missing NetworkObject component!");
            Destroy(playerObj);
        }

        // For local client (host), notify role directly
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            NotifyRoleLocally(role);
        }
    }

    Vector3 GetSpawnPosition(PlayerRole role)
    {
        // Try to use MapManager spawn points
        if (MapManager.Instance != null)
        {
            var zones = MapManager.Instance.AllZones;
            if (zones != null && zones.Count > 0)
            {
                // Astronaut spawns in Command, Aliens in random other zones
                foreach (var zone in zones)
                {
                    if (role == PlayerRole.Astronaut && zone.zoneType == MapZone.ZoneType.Command)
                    {
                        if (zone.npcSpawnPoints != null && zone.npcSpawnPoints.Length > 0)
                        {
                            return zone.npcSpawnPoints[Random.Range(0, zone.npcSpawnPoints.Length)].position;
                        }
                    }
                    else if (role == PlayerRole.Alien && zone.zoneType != MapZone.ZoneType.Command)
                    {
                        if (zone.npcSpawnPoints != null && zone.npcSpawnPoints.Length > 0)
                        {
                            return zone.npcSpawnPoints[Random.Range(0, zone.npcSpawnPoints.Length)].position;
                        }
                    }
                }
            }
        }

        // Fallback: random position near origin
        return new Vector3(Random.Range(-5f, 5f), 1f, Random.Range(-5f, 5f));
    }

    public void TriggerChaosPhase()
    {
        if (!IsServer) return;
        if (currentPhase != GamePhase.Playing) return;

        SetPhase(GamePhase.Chaos);
        Debug.Log("[NetworkGame] CHAOS PHASE!");
    }

    public void EndGame(bool astronautWins)
    {
        if (!IsServer) return;

        SetPhase(GamePhase.Ended);
        NotifyGameEnded(astronautWins);
    }

    void Update()
    {
        if (!IsServer) return;
        if (currentPhase != GamePhase.Playing && currentPhase != GamePhase.Chaos) return;

        // Update timer
        gameTimer -= Time.deltaTime;

        if (gameTimer <= 0)
        {
            // Time's up - Aliens win (astronaut didn't find them)
            EndGame(false);
        }
    }

    // ==================== NOTIFICATIONS (Local - sync handled via NetworkedPlayer) ====================

    /// <summary>
    /// Called on server to notify a specific client of their role.
    /// In current implementation, this is called locally after spawning player.
    /// </summary>
    void NotifyRoleLocally(PlayerRole role)
    {
        Debug.Log($"[NetworkGame] Role assigned: {role}");
        OnLocalRoleAssigned?.Invoke(role);
        ShowRoleUI(role);
    }

    /// <summary>
    /// Called when game ends - updates local state and shows UI.
    /// </summary>
    void NotifyGameEnded(bool astronautWins)
    {
        Debug.Log($"[NetworkGame] Game ended! Astronaut wins: {astronautWins}");
        OnGameEnded?.Invoke(astronautWins);
        ShowEndGameUI(astronautWins);
    }

    /// <summary>
    /// Notify clients that world is ready (via SpawnManager callback).
    /// </summary>
    void NotifyWorldReadyToClients()
    {
        Debug.Log("[NetworkGame] Notifying: World is ready!");

        // Tell SpawnManager to spawn local entities (interactables, defense zones)
        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.OnWorldReady();
        }
    }

    // ==================== UI ====================

    void ShowRoleUI(PlayerRole role)
    {
        // Create simple role announcement UI
        StartCoroutine(ShowRoleAnnouncement(role));
    }

    System.Collections.IEnumerator ShowRoleAnnouncement(PlayerRole role)
    {
        // This will be replaced with proper UI later
        Debug.Log($"=== YOU ARE THE {role.ToString().ToUpper()} ===");

        yield return new WaitForSeconds(3f);
    }

    void ShowEndGameUI(bool astronautWins)
    {
        string winner = astronautWins ? "ASTRONAUT" : "ALIENS";
        Debug.Log($"=== {winner} WIN! ===");

        // Return to lobby after delay
        StartCoroutine(ReturnToLobby());
    }

    System.Collections.IEnumerator ReturnToLobby()
    {
        yield return new WaitForSeconds(5f);

        // Disconnect from network
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        // Return to menu
        SceneManager.LoadScene(menuSceneName);
    }

    // ==================== PUBLIC GETTERS ====================

    public GamePhase GetCurrentPhase() => currentPhase;
    public float GetTimeRemaining() => gameTimer;
    public bool IsAstronaut(ulong clientId) => astronautClientId == clientId;
    public bool IsLocalPlayerAstronaut() => IsAstronaut(NetworkManager.Singleton.LocalClientId);

    public PlayerRole GetLocalRole()
    {
        if (NetworkManager.Singleton == null) return PlayerRole.None;
        ulong localId = NetworkManager.Singleton.LocalClientId;
        return playerRoles.ContainsKey(localId) ? playerRoles[localId] : PlayerRole.None;
    }

    // ==================== PHASE CHANGE ====================

    /// <summary>
    /// Sets the game phase and fires events.
    /// </summary>
    void SetPhase(GamePhase newPhase)
    {
        if (currentPhase == newPhase) return;

        GamePhase oldPhase = currentPhase;
        currentPhase = newPhase;

        Debug.Log($"[NetworkGame] Phase changed: {oldPhase} -> {newPhase}");
        OnPhaseChanged?.Invoke(newPhase);

        // Trigger local game manager events
        if (newPhase == GamePhase.Chaos && GameManager.Instance != null)
        {
            GameManager.Instance.TriggerChaosPhase();
        }
    }

    /// <summary>
    /// Called by NetworkedPlayer RPC to set local phase to Chaos on clients.
    /// This is used because NetworkGameManager is not a NetworkBehaviour.
    /// </summary>
    public void SetLocalChaosPhase()
    {
        if (currentPhase == GamePhase.Chaos) return;

        Debug.Log("[NetworkGame] SetLocalChaosPhase called (client sync)");
        currentPhase = GamePhase.Chaos;
        OnPhaseChanged?.Invoke(GamePhase.Chaos);
    }

    /// <summary>
    /// Called by NetworkedPlayer RPC to set local phase to Playing on clients.
    /// </summary>
    public void SetLocalPlayingPhase()
    {
        if (currentPhase == GamePhase.Playing) return;

        Debug.Log("[NetworkGame] SetLocalPlayingPhase called (client sync)");
        currentPhase = GamePhase.Playing;
        OnPhaseChanged?.Invoke(GamePhase.Playing);
    }
}
