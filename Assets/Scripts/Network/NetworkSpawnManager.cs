using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Server-only spawn manager for networked game.
/// Handles player spawning, role assignment, and NPC spawning.
/// This is NOT a NetworkBehaviour - it only runs on the server/host.
/// </summary>
public class NetworkSpawnManager : MonoBehaviour
{
    public static NetworkSpawnManager Instance { get; private set; }

    [Header("Player Prefabs (Must have NetworkObject)")]
    public GameObject astronautPrefab;
    public GameObject alienPrefab;

    [Header("NPC Prefab (Must have NetworkObject)")]
    public GameObject npcPrefab;

    [Header("Settings")]
    public int npcCount = 10;

    // Track spawned players
    private Dictionary<ulong, NetworkObject> spawnedPlayers = new Dictionary<ulong, NetworkObject>();
    private ulong astronautClientId = ulong.MaxValue;
    private List<NetworkObject> spawnedNPCs = new List<NetworkObject>();
    private bool initialized = false;

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
        // Only run on server
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[NetworkSpawnManager] Not running on server, destroying...");
            Destroy(gameObject);
            return;
        }

        Initialize();
    }

    void Initialize()
    {
        if (initialized) return;
        initialized = true;

        Debug.Log($"[NetworkSpawnManager] Initializing on server...");

        // Subscribe to client connections
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        // Spawn NPCs once
        SpawnNPCs();

        // Spawn interactables (coffee machines, alarm terminals, defense zones)
        SpawnInteractables();

        // Spawn host's player immediately
        SpawnPlayerForClient(NetworkManager.Singleton.LocalClientId);
    }

    void SpawnInteractables()
    {
        // Use SpawnManager if available
        if (SpawnManager.Instance != null)
        {
            Debug.Log("[NetworkSpawnManager] Calling SpawnManager.SpawnAllEntities()...");
            SpawnManager.Instance.SpawnAllEntities();
        }
        else
        {
            // Try to find or create SpawnManager
            SpawnManager spawnMgr = FindObjectOfType<SpawnManager>();
            if (spawnMgr == null)
            {
                Debug.Log("[NetworkSpawnManager] Creating SpawnManager...");
                GameObject spawnMgrObj = new GameObject("SpawnManager");
                spawnMgr = spawnMgrObj.AddComponent<SpawnManager>();
            }

            // Wait a frame for initialization then spawn
            StartCoroutine(DelayedSpawnInteractables(spawnMgr));
        }
    }

    System.Collections.IEnumerator DelayedSpawnInteractables(SpawnManager spawnMgr)
    {
        yield return null; // Wait one frame

        if (spawnMgr != null)
        {
            Debug.Log("[NetworkSpawnManager] Spawning interactables (delayed)...");
            spawnMgr.SpawnAllEntities();
        }
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        Debug.Log($"[NetworkSpawnManager] Client {clientId} connected");

        // Don't spawn for host again (already spawned in OnNetworkSpawn)
        if (clientId == NetworkManager.Singleton.LocalClientId && NetworkManager.Singleton.IsHost)
        {
            return;
        }

        SpawnPlayerForClient(clientId);
    }

    void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        Debug.Log($"[NetworkSpawnManager] Client {clientId} disconnected");

        // Despawn their player
        if (spawnedPlayers.TryGetValue(clientId, out NetworkObject netObj))
        {
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn();
            }
            spawnedPlayers.Remove(clientId);
        }
    }

    void SpawnPlayerForClient(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        // First player is Astronaut, others are Aliens
        bool isAstronaut = (astronautClientId == ulong.MaxValue);

        if (isAstronaut)
        {
            astronautClientId = clientId;
        }

        GameObject prefab = isAstronaut ? astronautPrefab : alienPrefab;

        if (prefab == null)
        {
            Debug.LogError($"[NetworkSpawnManager] {(isAstronaut ? "Astronaut" : "Alien")} prefab not assigned!");
            return;
        }

        // Get spawn position
        Vector3 spawnPos = GetSpawnPosition(isAstronaut);
        Quaternion spawnRot = Quaternion.identity;

        // Spawn the player
        GameObject playerObj = Instantiate(prefab, spawnPos, spawnRot);
        NetworkObject netObj = playerObj.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError($"[NetworkSpawnManager] Prefab missing NetworkObject!");
            Destroy(playerObj);
            return;
        }

        // Get NetworkedPlayer and set role BEFORE spawning
        NetworkedPlayer networkedPlayer = playerObj.GetComponent<NetworkedPlayer>();
        if (networkedPlayer != null)
        {
            networkedPlayer.isAstronaut = isAstronaut;

            // Try to set the NetworkVariable value before spawn (works in Netcode 1.5+)
            // In older versions, this may fail - we'll set it after spawn as fallback
            try
            {
                networkedPlayer.IsAstronautRole.Value = isAstronaut;
                Debug.Log($"[NetworkSpawnManager] Set NetworkVariable BEFORE spawn: {(isAstronaut ? "Astronaut" : "Alien")}");
            }
            catch (System.Exception e)
            {
                Debug.Log($"[NetworkSpawnManager] Could not set NetworkVariable before spawn (will set after): {e.Message}");
            }
        }

        // Spawn as player object (gives ownership to that client)
        netObj.SpawnAsPlayerObject(clientId);
        spawnedPlayers[clientId] = netObj;

        // Set NetworkVariable after spawn (works in all Netcode versions)
        // This ensures the value is set even if the pre-spawn set failed
        if (networkedPlayer != null)
        {
            networkedPlayer.SetRole(isAstronaut);
            Debug.Log($"[NetworkSpawnManager] Set NetworkVariable AFTER spawn: {(isAstronaut ? "Astronaut" : "Alien")}");
        }

        Debug.Log($"[NetworkSpawnManager] Spawned {(isAstronaut ? "ASTRONAUT" : "ALIEN")} for client {clientId} at {spawnPos}");

        // Role notification is handled by NetworkedPlayer via NetworkVariable sync
    }

    Vector3 GetSpawnPosition(bool isAstronaut)
    {
        Debug.Log($"[NetworkSpawnManager] Getting spawn position for {(isAstronaut ? "ASTRONAUT" : "ALIEN")}");

        // Try MapManager spawn points
        if (MapManager.Instance != null)
        {
            var zones = MapManager.Instance.AllZones;
            Debug.Log($"[NetworkSpawnManager] Found {zones?.Count ?? 0} zones");

            if (zones != null && zones.Count > 0)
            {
                // Collect valid spawn points
                System.Collections.Generic.List<Vector3> validPoints = new System.Collections.Generic.List<Vector3>();

                foreach (var zone in zones)
                {
                    bool isValidZone = false;

                    // Astronaut in Command zone
                    if (isAstronaut && zone.zoneType == MapZone.ZoneType.Command)
                    {
                        isValidZone = true;
                    }
                    // Alien in non-Command zones (prefer Industrial or Research)
                    else if (!isAstronaut && zone.zoneType != MapZone.ZoneType.Command)
                    {
                        isValidZone = true;
                    }

                    if (isValidZone && zone.npcSpawnPoints != null && zone.npcSpawnPoints.Length > 0)
                    {
                        foreach (var point in zone.npcSpawnPoints)
                        {
                            if (point != null)
                            {
                                validPoints.Add(point.position);
                            }
                        }
                        Debug.Log($"[NetworkSpawnManager] Zone {zone.zoneType}: added {zone.npcSpawnPoints.Length} spawn points");
                    }
                }

                // Pick random from valid points
                if (validPoints.Count > 0)
                {
                    Vector3 pos = validPoints[Random.Range(0, validPoints.Count)];
                    Debug.Log($"[NetworkSpawnManager] Selected spawn point: {pos}");
                    return pos;
                }

                // Fallback: any zone with spawn points
                Debug.Log("[NetworkSpawnManager] No valid zone found, using fallback");
                foreach (var zone in zones)
                {
                    if (zone.npcSpawnPoints != null && zone.npcSpawnPoints.Length > 0)
                    {
                        Transform point = zone.npcSpawnPoints[Random.Range(0, zone.npcSpawnPoints.Length)];
                        if (point != null) return point.position;
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("[NetworkSpawnManager] MapManager.Instance is NULL!");
        }

        // Default fallback - DIFFERENT positions for each role
        Vector3 fallbackPos = isAstronaut
            ? new Vector3(0, 1, 0)
            : new Vector3(10, 1, 10);  // Alien spawns 10 units away

        Debug.Log($"[NetworkSpawnManager] Using fallback position: {fallbackPos}");
        return fallbackPos;
    }

    void SpawnNPCs()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (npcPrefab == null)
        {
            Debug.LogWarning("[NetworkSpawnManager] NPC prefab not assigned, skipping NPC spawn");
            return;
        }

        // Get all spawn points from zones
        List<Vector3> spawnPoints = new List<Vector3>();

        if (MapManager.Instance != null)
        {
            foreach (var zone in MapManager.Instance.AllZones)
            {
                if (zone.npcSpawnPoints != null)
                {
                    foreach (var point in zone.npcSpawnPoints)
                    {
                        if (point != null)
                            spawnPoints.Add(point.position);
                    }
                }
            }
        }

        // Fallback if no spawn points
        if (spawnPoints.Count == 0)
        {
            for (int i = 0; i < npcCount; i++)
            {
                spawnPoints.Add(new Vector3(Random.Range(-20f, 20f), 1f, Random.Range(-20f, 20f)));
            }
        }

        // Spawn NPCs
        int spawned = 0;
        foreach (var pos in spawnPoints)
        {
            if (spawned >= npcCount) break;

            GameObject npcObj = Instantiate(npcPrefab, pos, Quaternion.identity);
            NetworkObject netObj = npcObj.GetComponent<NetworkObject>();

            if (netObj != null)
            {
                netObj.Spawn();
                spawnedNPCs.Add(netObj);
                spawned++;
            }
            else
            {
                Debug.LogWarning("[NetworkSpawnManager] NPC prefab missing NetworkObject!");
                Destroy(npcObj);
            }
        }

        Debug.Log($"[NetworkSpawnManager] Spawned {spawned} NPCs");
    }

    // Public getters
    public bool IsClientAstronaut(ulong clientId) => clientId == astronautClientId;
    public ulong GetAstronautClientId() => astronautClientId;
}
