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

    // Disconnect handling
    private const float DISCONNECT_GRACE_PERIOD = 5f; // Seconds to wait before ending game on astronaut disconnect
    private bool astronautDisconnectPending = false;
    private float astronautDisconnectTimer = 0f;

    // Subscription tracking to prevent double-subscription
    private bool hasSubscribedToClientEvents = false;

    // Player join wait settings
    [Header("Role Assignment Settings")]
    [SerializeField] private float waitForPlayersTime = 5f; // Time to wait for all players to connect
    [SerializeField] private int expectedPlayerCount = 0; // 0 = use lobby count

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
    public bool IsGameInitialized => isInitialized;

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

        // Guard against double subscription (if server restarts without object destruction)
        if (!hasSubscribedToClientEvents)
        {
            hasSubscribedToClientEvents = true;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

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

        // Ensure SpawnManager collects spawn points from zones
        if (SpawnManager.Instance != null)
        {
            Debug.Log("[NetworkGame] SERVER: Collecting spawn points from zones...");
            SpawnManager.Instance.CollectAllSpawnPoints();
            Debug.Log($"[NetworkGame] SERVER: SpawnManager has {SpawnManager.Instance.SpawnPointCount} spawn points");
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

        // Wait for all players to connect before starting
        yield return StartCoroutine(WaitForPlayersAndStartGame());
    }

    /// <summary>
    /// Wait for all players from the Steam lobby to connect before starting the game.
    /// This ensures random role assignment works with all players present.
    /// </summary>
    System.Collections.IEnumerator WaitForPlayersAndStartGame()
    {
        // Get expected player count from Steam lobby
        int expectedPlayers = expectedPlayerCount;

        if (expectedPlayers <= 0 && SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.InLobby)
        {
            expectedPlayers = SteamLobbyManager.Instance.Players.Count;
            Debug.Log($"[NetworkGame] Expected {expectedPlayers} players from Steam lobby");
        }

        // Minimum 1 player (solo testing)
        if (expectedPlayers < 1) expectedPlayers = 1;

        Debug.Log($"[NetworkGame] Waiting for {expectedPlayers} players to connect...");

        float elapsed = 0f;
        int connectedCount = 0;

        // Wait for all expected players or timeout
        while (elapsed < waitForPlayersTime)
        {
            connectedCount = NetworkManager.Singleton.ConnectedClientsIds.Count;

            if (connectedCount >= expectedPlayers)
            {
                Debug.Log($"[NetworkGame] All {expectedPlayers} players connected!");
                break;
            }

            Debug.Log($"[NetworkGame] {connectedCount}/{expectedPlayers} players connected, waiting... ({elapsed:F1}s)");
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }

        // Final count
        connectedCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
        Debug.Log($"[NetworkGame] Starting game with {connectedCount} players (expected {expectedPlayers})");

        // Now start the game with all connected players
        StartGame();
    }

    void OnDestroy()
    {
        // CRITICAL: Clear static instance to prevent stale references after scene reload
        if (Instance == this)
        {
            Instance = null;
        }

        // Unsubscribe from NetworkManager events
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= OnNetworkServerStarted;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnNetworkClientConnected;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        // Stop all coroutines to prevent state corruption
        StopAllCoroutines();
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

                // Notify all other players about the new player
                NotifyPlayerConnected(clientId, role == PlayerRole.Astronaut);
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

    /// <summary>
    /// Notify all clients that a player connected
    /// </summary>
    void NotifyPlayerConnected(ulong clientId, bool isAstronaut)
    {
        // Find a NetworkedPlayer to send the RPC (any will do)
        var players = FindObjectsByType<NetworkedPlayer>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player.IsSpawned)
            {
                player.NotifyPlayerConnectedClientRpc(clientId, isAstronaut);
                break;
            }
        }
    }

    void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[NetworkGame] Client disconnected: {clientId}");
        playerRoles.Remove(clientId);

        // Notify all players about the disconnect
        NotifyPlayerDisconnected(clientId);

        // Check if astronaut left
        if (clientId == astronautClientId && currentPhase != GamePhase.Ended)
        {
            Debug.Log($"[NetworkGame] Astronaut disconnected! Starting {DISCONNECT_GRACE_PERIOD}s grace period...");
            astronautDisconnectPending = true;
            astronautDisconnectTimer = DISCONNECT_GRACE_PERIOD;
            // Don't immediately end game - give grace period for reconnection or graceful exit
        }
    }

    /// <summary>
    /// Notify all clients that a player disconnected
    /// </summary>
    void NotifyPlayerDisconnected(ulong clientId)
    {
        // Find a NetworkedPlayer to send the RPC (any will do)
        var players = FindObjectsByType<NetworkedPlayer>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player.IsSpawned)
            {
                player.NotifyPlayerDisconnectedClientRpc(clientId);
                break;
            }
        }
    }

    void StartGame()
    {
        if (!IsServer || gameStarted) return;
        gameStarted = true;

        Debug.Log("[NetworkGame] ========================================");
        Debug.Log("[NetworkGame] STARTING GAME - ASSIGNING ROLES");
        Debug.Log("[NetworkGame] ========================================");

        // Clear any previous role assignments
        playerRoles.Clear();

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
        for (int i = 0; i < clients.Count; i++)
        {
            Debug.Log($"[NetworkGame]   [{i}] Client {clients[i]}");
        }

        if (clients.Count == 0)
        {
            Debug.LogError("[NetworkGame] No clients connected!");
            return;
        }

        // IMPORTANT: Reseed random with current time for TRUE randomness
        // Use multiple time components for better seed
        int seed = System.DateTime.Now.Millisecond +
                   System.DateTime.Now.Second * 1000 +
                   System.DateTime.Now.Minute * 60000 +
                   (int)(Time.realtimeSinceStartup * 1000);
        Random.InitState(seed);
        Debug.Log($"[NetworkGame] Random seed: {seed}");

        // Randomly pick astronaut from ALL connected clients
        int astronautIndex = Random.Range(0, clients.Count);
        ulong astronautId = clients[astronautIndex];
        astronautClientId = astronautId;

        Debug.Log($"[NetworkGame] === ROLE ASSIGNMENT ===");
        Debug.Log($"[NetworkGame] {clients.Count} players, random index = {astronautIndex}");
        Debug.Log($"[NetworkGame] Astronaut assigned to client {astronautId}");

        // Assign roles first (before spawning to avoid race conditions)
        int astronautCount = 0;
        int alienCount = 0;

        foreach (var clientId in clients)
        {
            PlayerRole role = (clientId == astronautId) ? PlayerRole.Astronaut : PlayerRole.Alien;
            playerRoles[clientId] = role;

            if (role == PlayerRole.Astronaut)
                astronautCount++;
            else
                alienCount++;

            Debug.Log($"[NetworkGame] Role assigned: Client {clientId} = {role}");
        }

        // VALIDATION: Ensure exactly one astronaut
        if (astronautCount != 1)
        {
            Debug.LogError($"[NetworkGame] CRITICAL: Role assignment failed! Astronauts={astronautCount}, Aliens={alienCount}");
            Debug.LogError($"[NetworkGame] Forcing reassignment - Client {clients[0]} will be Astronaut");

            // Force first client to be astronaut
            playerRoles.Clear();
            astronautClientId = clients[0];
            playerRoles[astronautClientId] = PlayerRole.Astronaut;
            for (int i = 1; i < clients.Count; i++)
            {
                playerRoles[clients[i]] = PlayerRole.Alien;
            }
            astronautCount = 1;
            alienCount = clients.Count - 1;
        }

        Debug.Log($"[NetworkGame] Final roles: {astronautCount} Astronaut(s), {alienCount} Alien(s)");

        // Now spawn all players
        foreach (var clientId in clients)
        {
            PlayerRole role = playerRoles[clientId];
            Debug.Log($"[NetworkGame] Spawning {role} for client {clientId}...");
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

        // Get spawn point using NetworkSpawnManager (this despawns any NPC at that location)
        Vector3 spawnPos = GetSpawnPositionAndDespawnNPC(clientId);
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

    /// <summary>
    /// Get a random spawn position for a player and despawn any NPC at that location.
    /// Uses NetworkSpawnManager to properly track spawn points.
    /// </summary>
    Vector3 GetSpawnPositionAndDespawnNPC(ulong clientId)
    {
        // Use NetworkSpawnManager if available (handles NPC despawning)
        if (NetworkSpawnManager.Instance != null)
        {
            Vector3 pos = NetworkSpawnManager.Instance.GetPlayerSpawnPosition(clientId);
            Debug.Log($"[NetworkGame] Got spawn position from NetworkSpawnManager for client {clientId}: {pos}");
            return pos;
        }

        // Fallback: use SpawnManager (single player style)
        if (SpawnManager.Instance != null)
        {
            var spawnInfo = SpawnManager.Instance.ReserveSpawnPointForPlayer();
            if (spawnInfo != null && spawnInfo.spawnPoint != null)
            {
                Debug.Log($"[NetworkGame] Got spawn position from SpawnManager: {spawnInfo.spawnPoint.position}");
                return spawnInfo.spawnPoint.position;
            }
        }

        // Last fallback: random position near origin
        Debug.LogWarning("[NetworkGame] No spawn manager available, using fallback position");
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

        // Handle astronaut disconnect grace period
        if (astronautDisconnectPending)
        {
            astronautDisconnectTimer -= Time.deltaTime;
            if (astronautDisconnectTimer <= 0)
            {
                Debug.Log("[NetworkGame] Astronaut disconnect grace period expired - ending game");
                astronautDisconnectPending = false;
                EndGame(false); // Aliens win
            }
        }
    }

    /// <summary>
    /// Reset all game state for a new game. Call this when returning to lobby.
    /// </summary>
    public void ResetState()
    {
        Debug.Log("[NetworkGame] Resetting all game state");
        currentPhase = GamePhase.WaitingForPlayers;
        gameTimer = 600f;
        astronautClientId = ulong.MaxValue;
        worldIsReady = false;
        playerRoles.Clear();
        pendingClients.Clear();
        gameStarted = false;
        isInitialized = false;
        astronautDisconnectPending = false;
        astronautDisconnectTimer = 0f;
        hasSubscribedToClientEvents = false;
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

        // NETWORK: Broadcast game end to ALL clients via RPC
        // The RPC will handle showing UI on ALL clients (including host)
        // This prevents duplicate UI on host
        var networkedPlayers = FindObjectsByType<NetworkedPlayer>(FindObjectsSortMode.None);
        if (networkedPlayers.Length > 0 && IsServer)
        {
            // alienWins is the inverse of astronautWins
            bool alienWins = !astronautWins;
            networkedPlayers[0].EndGameServerRpc(alienWins);
            Debug.Log($"[NetworkGame] Broadcast EndGameServerRpc (alienWins={alienWins}) to all clients");
        }

        // NOTE: Don't fire OnGameEnded event here - it will be fired via RPC on all clients
        // This prevents duplicate UI (RoleAnnouncementUI + MenuManager) on the host

        // Start return to lobby countdown (server manages this)
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

        Debug.Log("[NetworkGame] Returning to lobby - cleaning up all state...");

        // Reset our own state first
        ResetState();

        // Reset SteamLobbyManager state
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.ResetGameState();
        }

        // Reset SpawnManager state
        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.ResetAll();
        }

        // Disconnect from network AFTER state reset
        if (NetworkManager.Singleton != null)
        {
            // Unsubscribe before shutdown to prevent errors
            NetworkManager.Singleton.OnClientConnectedCallback -= OnNetworkClientConnected;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnServerStarted -= OnNetworkServerStarted;
            hasSubscribedToClientEvents = false;

            NetworkManager.Singleton.Shutdown();
            Debug.Log("[NetworkGame] NetworkManager shutdown complete");
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
