using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages networked game state - role assignment, spawning, win conditions.
/// Server authoritative.
/// </summary>
public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance { get; private set; }

    [Header("Player Prefabs")]
    public GameObject astronautPrefab;
    public GameObject alienPrefab;

    [Header("Settings")]
    public string menuSceneName = "MainMenu";

    // Network synced game state
    private NetworkVariable<GamePhase> currentPhase = new NetworkVariable<GamePhase>(
        GamePhase.WaitingForPlayers,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<float> gameTimer = new NetworkVariable<float>(
        600f, // 10 minutes
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<ulong> astronautClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Local state
    private Dictionary<ulong, PlayerRole> playerRoles = new Dictionary<ulong, PlayerRole>();
    private bool gameStarted = false;

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

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        currentPhase.OnValueChanged += OnPhaseValueChanged;

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            // Start game when scene loads
            StartGame();
        }

        Debug.Log($"[NetworkGame] Spawned. IsServer: {IsServer}, IsHost: {IsHost}, IsClient: {IsClient}");
    }

    public override void OnNetworkDespawn()
    {
        currentPhase.OnValueChanged -= OnPhaseValueChanged;

        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        base.OnNetworkDespawn();
    }

    // ==================== SERVER METHODS ====================

    void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[NetworkGame] Client connected: {clientId}");
    }

    void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[NetworkGame] Client disconnected: {clientId}");
        playerRoles.Remove(clientId);

        // Check if astronaut left
        if (clientId == astronautClientId.Value)
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

        // Get all connected clients
        var clients = NetworkManager.Singleton.ConnectedClientsIds.ToList();

        if (clients.Count == 0)
        {
            Debug.LogError("[NetworkGame] No clients connected!");
            return;
        }

        // Randomly pick astronaut
        int astronautIndex = Random.Range(0, clients.Count);
        ulong astronautId = clients[astronautIndex];
        astronautClientId.Value = astronautId;

        // Assign roles
        foreach (var clientId in clients)
        {
            PlayerRole role = (clientId == astronautId) ? PlayerRole.Astronaut : PlayerRole.Alien;
            playerRoles[clientId] = role;

            // Spawn player
            SpawnPlayerForClient(clientId, role);
        }

        // Start playing
        currentPhase.Value = GamePhase.Playing;

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

        if (netObj != null)
        {
            netObj.SpawnAsPlayerObject(clientId);
            Debug.Log($"[NetworkGame] Spawned {role} for client {clientId} at {spawnPos}");
        }
        else
        {
            Debug.LogError($"[NetworkGame] Prefab missing NetworkObject component!");
            Destroy(playerObj);
        }

        // Notify client of their role
        NotifyRoleClientRpc(role, new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        });
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
        if (currentPhase.Value != GamePhase.Playing) return;

        currentPhase.Value = GamePhase.Chaos;
        Debug.Log("[NetworkGame] CHAOS PHASE!");
    }

    public void EndGame(bool astronautWins)
    {
        if (!IsServer) return;

        currentPhase.Value = GamePhase.Ended;
        GameEndedClientRpc(astronautWins);
    }

    void Update()
    {
        if (!IsServer) return;
        if (currentPhase.Value != GamePhase.Playing && currentPhase.Value != GamePhase.Chaos) return;

        // Update timer
        gameTimer.Value -= Time.deltaTime;

        if (gameTimer.Value <= 0)
        {
            // Time's up - Aliens win (astronaut didn't find them)
            EndGame(false);
        }
    }

    // ==================== CLIENT RPCs ====================

    [ClientRpc]
    void NotifyRoleClientRpc(PlayerRole role, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log($"[NetworkGame] You are: {role}");
        OnLocalRoleAssigned?.Invoke(role);

        // Show role UI
        ShowRoleUI(role);
    }

    [ClientRpc]
    void GameEndedClientRpc(bool astronautWins)
    {
        Debug.Log($"[NetworkGame] Game ended! Astronaut wins: {astronautWins}");
        OnGameEnded?.Invoke(astronautWins);

        // Show end game UI
        ShowEndGameUI(astronautWins);
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

    public GamePhase GetCurrentPhase() => currentPhase.Value;
    public float GetTimeRemaining() => gameTimer.Value;
    public bool IsAstronaut(ulong clientId) => astronautClientId.Value == clientId;
    public bool IsLocalPlayerAstronaut() => IsAstronaut(NetworkManager.Singleton.LocalClientId);

    public PlayerRole GetLocalRole()
    {
        if (NetworkManager.Singleton == null) return PlayerRole.None;
        ulong localId = NetworkManager.Singleton.LocalClientId;
        return playerRoles.ContainsKey(localId) ? playerRoles[localId] : PlayerRole.None;
    }

    // ==================== PHASE CHANGE ====================

    void OnPhaseValueChanged(GamePhase oldPhase, GamePhase newPhase)
    {
        Debug.Log($"[NetworkGame] Phase changed: {oldPhase} -> {newPhase}");
        OnPhaseChanged?.Invoke(newPhase);

        // Trigger local game manager events
        if (newPhase == GamePhase.Chaos && GameManager.Instance != null)
        {
            GameManager.Instance.TriggerChaosPhase();
        }
    }
}
