using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Server-only spawn manager for networked game.
/// Handles NPC spawning with NetworkObject sync.
/// NPCs are spawned on server and automatically replicated to clients.
///
/// SPAWN POINT SYSTEM:
/// - Each NPC spawn point holds exactly 1 NPC
/// - Players take a random spawn point and replace the NPC
/// - Late joiners despawn an existing NPC and take their place
/// </summary>
public class NetworkSpawnManager : MonoBehaviour
{
    public static NetworkSpawnManager Instance { get; private set; }

    [Header("NPC Prefab (Must have NetworkObject + registered in NetworkManager)")]
    public GameObject npcPrefab;

    [Header("Settings")]
    public int randomSeed = 12345; // Fixed seed for deterministic spawning

    [Header("Debug")]
    [SerializeField] private bool isInitialized = false;
    [SerializeField] private int totalNPCsSpawned = 0;

    // ==================== SPAWN POINT TRACKING ====================

    /// <summary>
    /// Tracks what occupies each spawn point
    /// </summary>
    public enum SpawnPointOccupant
    {
        Empty,
        NPC,
        Player
    }

    /// <summary>
    /// Info about a spawn point and its current occupant
    /// </summary>
    [System.Serializable]
    public class SpawnPointInfo
    {
        public Transform spawnPoint;
        public SpawnPointOccupant occupant = SpawnPointOccupant.Empty;
        public NetworkObject occupantNetObj; // The NPC NetworkObject at this point
        public ulong playerClientId; // If occupied by player, their client ID
        public string zoneName;

        public SpawnPointInfo(Transform point, string zone)
        {
            spawnPoint = point;
            zoneName = zone;
            occupant = SpawnPointOccupant.Empty;
            occupantNetObj = null;
            playerClientId = 0;
        }
    }

    [Header("Spawn Point Tracking")]
    [SerializeField] private List<SpawnPointInfo> allSpawnPoints = new List<SpawnPointInfo>();

    // Track spawned NPCs
    private List<NetworkObject> spawnedNPCs = new List<NetworkObject>();

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
        // Only run on server/host
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            Debug.Log("[NetworkSpawnManager] Not server - this manager only runs on server");
            enabled = false;
            return;
        }

        Debug.Log("[NetworkSpawnManager] SERVER: Starting initialization...");
        StartCoroutine(InitializeWorld());
    }

    /// <summary>
    /// Main initialization - waits for zones then spawns everything
    /// </summary>
    IEnumerator InitializeWorld()
    {
        Debug.Log("[NetworkSpawnManager] SERVER: Waiting for zones to load...");

        // Step 1: Wait for MapManager
        float timeout = 10f;
        float elapsed = 0f;

        while (MapManager.Instance == null && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
            Debug.Log("[NetworkSpawnManager] Waiting for MapManager...");
        }

        if (MapManager.Instance == null)
        {
            Debug.LogError("[NetworkSpawnManager] MapManager not found! Cannot spawn NPCs.");
            yield break;
        }

        // Step 2: Wait for zones to be found
        elapsed = 0f;
        while (MapManager.Instance.ZoneCount == 0 && elapsed < timeout)
        {
            MapManager.Instance.FindAllZonesInScene();
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
            Debug.Log($"[NetworkSpawnManager] Looking for zones... found {MapManager.Instance.ZoneCount}");
        }

        if (MapManager.Instance.ZoneCount == 0)
        {
            Debug.LogWarning("[NetworkSpawnManager] No zones found - creating default zones via MapManager...");
            MapManager.Instance.CreateDefaultZones();
        }

        if (MapManager.Instance.ZoneCount == 0)
        {
            Debug.LogError("[NetworkSpawnManager] Still no zones after creation! Cannot spawn NPCs.");
            yield break;
        }

        Debug.Log($"[NetworkSpawnManager] SERVER: Found {MapManager.Instance.ZoneCount} zones!");

        // Step 3: Check NPC prefab
        if (npcPrefab == null)
        {
            Debug.LogError("[NetworkSpawnManager] NPC Prefab not assigned! Please assign it in Inspector.");
            yield break;
        }

        // Check if prefab has NetworkObject
        NetworkObject prefabNetObj = npcPrefab.GetComponent<NetworkObject>();
        if (prefabNetObj == null)
        {
            Debug.LogWarning("[NetworkSpawnManager] NPC Prefab missing NetworkObject - adding it now...");
            // Note: This won't work for network spawning! Prefab needs to be registered.
            // Add it anyway for local testing
            npcPrefab.AddComponent<NetworkObject>();
        }

        // Ensure prefab has NetworkTransform
        if (npcPrefab.GetComponent<Unity.Netcode.Components.NetworkTransform>() == null)
        {
            Debug.Log("[NetworkSpawnManager] Adding NetworkTransform to NPC prefab...");
            npcPrefab.AddComponent<Unity.Netcode.Components.NetworkTransform>();
        }

        // Step 4: Spawn NPCs with deterministic positions
        Debug.Log("[NetworkSpawnManager] SERVER: Spawning NPCs...");
        SpawnAllNPCs();

        // Step 5: Mark as initialized
        isInitialized = true;
        Debug.Log($"[NetworkSpawnManager] SERVER: World ready! {totalNPCsSpawned} NPCs spawned.");

        // Step 6: Notify clients (via NetworkGameManager)
        if (NetworkGameManager.Instance != null)
        {
            // NetworkGameManager handles the WorldReady notification
            Debug.Log("[NetworkSpawnManager] NetworkGameManager will notify clients.");
        }
    }

    // ==================== SPAWN POINT COLLECTION ====================

    /// <summary>
    /// Collect all NPC spawn points from all zones
    /// </summary>
    void CollectAllSpawnPoints()
    {
        allSpawnPoints.Clear();

        if (MapManager.Instance == null || MapManager.Instance.ZoneCount == 0)
        {
            Debug.LogWarning("[NetworkSpawnManager] No zones found for spawn point collection");
            return;
        }

        foreach (var zone in MapManager.Instance.AllZones)
        {
            if (zone == null || zone.npcSpawnPoints == null) continue;

            foreach (var spawnPoint in zone.npcSpawnPoints)
            {
                if (spawnPoint != null)
                {
                    allSpawnPoints.Add(new SpawnPointInfo(spawnPoint, zone.zoneName));
                }
            }
        }

        Debug.Log($"[NetworkSpawnManager] Collected {allSpawnPoints.Count} spawn points from {MapManager.Instance.ZoneCount} zones");
    }

    /// <summary>
    /// Get list of spawn points with NPCs (for late join replacement)
    /// </summary>
    public List<SpawnPointInfo> GetNPCOccupiedSpawnPoints()
    {
        List<SpawnPointInfo> npcPoints = new List<SpawnPointInfo>();
        foreach (var info in allSpawnPoints)
        {
            if (info.occupant == SpawnPointOccupant.NPC && info.occupantNetObj != null)
            {
                npcPoints.Add(info);
            }
        }
        return npcPoints;
    }

    /// <summary>
    /// Get list of empty spawn points
    /// </summary>
    public List<SpawnPointInfo> GetEmptySpawnPoints()
    {
        List<SpawnPointInfo> emptyPoints = new List<SpawnPointInfo>();
        foreach (var info in allSpawnPoints)
        {
            if (info.occupant == SpawnPointOccupant.Empty)
            {
                emptyPoints.Add(info);
            }
        }
        return emptyPoints;
    }

    /// <summary>
    /// Spawn NPCs at all spawn points (1 NPC per spawn point)
    /// </summary>
    void SpawnAllNPCs()
    {
        // First collect all spawn points
        CollectAllSpawnPoints();

        if (allSpawnPoints.Count == 0)
        {
            Debug.LogWarning("[NetworkSpawnManager] No spawn points found! Add npcSpawnPoints to your MapZones.");
            return;
        }

        // Use fixed seed for deterministic spawning
        Random.InitState(randomSeed);

        // Spawn 1 NPC at each spawn point
        for (int i = 0; i < allSpawnPoints.Count; i++)
        {
            var spawnInfo = allSpawnPoints[i];
            if (spawnInfo.spawnPoint == null) continue;

            Vector3 position = new Vector3(
                spawnInfo.spawnPoint.position.x,
                0f,
                spawnInfo.spawnPoint.position.z
            );
            string npcName = $"NPC_{spawnInfo.zoneName}_{i + 1}";

            NetworkObject netObj = SpawnSingleNPC(position, npcName);

            if (netObj != null)
            {
                // Track this spawn point as occupied by NPC
                spawnInfo.occupant = SpawnPointOccupant.NPC;
                spawnInfo.occupantNetObj = netObj;
            }
        }

        // Reset random state
        Random.InitState((int)System.DateTime.Now.Ticks);

        Debug.Log($"[NetworkSpawnManager] Total NPCs spawned: {totalNPCsSpawned} (1 per spawn point)");
    }

    /// <summary>
    /// Spawn a single NPC and register it on the network
    /// Returns the NetworkObject for tracking
    /// </summary>
    NetworkObject SpawnSingleNPC(Vector3 position, string npcName)
    {
        // Instantiate the prefab
        GameObject npcObj = Instantiate(npcPrefab, position, Quaternion.identity);
        npcObj.name = npcName;

        // Ensure NetworkTransform exists for position sync
        if (npcObj.GetComponent<Unity.Netcode.Components.NetworkTransform>() == null)
        {
            npcObj.AddComponent<Unity.Netcode.Components.NetworkTransform>();
            Debug.Log($"[NetworkSpawnManager] Added NetworkTransform to NPC '{npcName}'");
        }

        // Get NetworkObject and spawn on network
        NetworkObject netObj = npcObj.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            // Spawn on network - this replicates to all clients automatically!
            netObj.Spawn();
            spawnedNPCs.Add(netObj);
            totalNPCsSpawned++;

            Debug.Log($"[NetworkSpawnManager] Spawned NPC '{npcName}' at {position}");
        }
        else
        {
            Debug.LogError($"[NetworkSpawnManager] NPC prefab missing NetworkObject!");
            Destroy(npcObj);
            return null;
        }

        // Setup NPC behavior if exists
        NPCBehavior npcBehavior = npcObj.GetComponent<NPCBehavior>();
        if (npcBehavior != null)
        {
            // Set NPC name via reflection (if private field)
            var field = typeof(NPCBehavior).GetField("npcName",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(npcBehavior, npcName);
            }
        }

        return netObj;
    }

    // ==================== PLAYER SPAWNING ====================

    /// <summary>
    /// Reserve a spawn point for a player. If NPCs already spawned, despawns the NPC at that point.
    /// Returns the spawn point info, or null if no points available.
    /// </summary>
    public SpawnPointInfo ReserveSpawnPointForPlayer(ulong clientId)
    {
        // If spawn points not collected yet, do it now
        if (allSpawnPoints.Count == 0)
        {
            CollectAllSpawnPoints();
        }

        // Ensure random is truly random (not using deterministic seed)
        Random.InitState((int)System.DateTime.Now.Ticks + System.Environment.TickCount + (int)clientId);

        // First try to find an empty spawn point
        var emptyPoints = GetEmptySpawnPoints();
        if (emptyPoints.Count > 0)
        {
            // Shuffle the list for extra randomness
            ShuffleList(emptyPoints);
            var chosen = emptyPoints[0];
            chosen.occupant = SpawnPointOccupant.Player;
            chosen.playerClientId = clientId;
            Debug.Log($"[NetworkSpawnManager] Reserved empty spawn point for client {clientId} at {chosen.spawnPoint.position} (zone: {chosen.zoneName})");
            return chosen;
        }

        // No empty points - take over a NPC spawn point
        var npcPoints = GetNPCOccupiedSpawnPoints();
        if (npcPoints.Count > 0)
        {
            // Shuffle the list for extra randomness
            ShuffleList(npcPoints);
            var chosen = npcPoints[0];

            // Despawn the NPC at this point
            if (chosen.occupantNetObj != null && chosen.occupantNetObj.IsSpawned)
            {
                Debug.Log($"[NetworkSpawnManager] Despawning NPC to make room for client {clientId}");
                chosen.occupantNetObj.Despawn();
                spawnedNPCs.Remove(chosen.occupantNetObj);
                totalNPCsSpawned--;
            }

            chosen.occupant = SpawnPointOccupant.Player;
            chosen.occupantNetObj = null;
            chosen.playerClientId = clientId;
            Debug.Log($"[NetworkSpawnManager] Reserved NPC spawn point for client {clientId} at {chosen.spawnPoint.position} (zone: {chosen.zoneName})");
            return chosen;
        }

        Debug.LogWarning($"[NetworkSpawnManager] No spawn points available for client {clientId}!");
        return null;
    }

    /// <summary>
    /// Fisher-Yates shuffle for lists
    /// </summary>
    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    /// <summary>
    /// Get spawn position for a player. Reserves a spawn point and returns position.
    /// </summary>
    public Vector3 GetPlayerSpawnPosition(ulong clientId)
    {
        var spawnInfo = ReserveSpawnPointForPlayer(clientId);
        if (spawnInfo != null && spawnInfo.spawnPoint != null)
        {
            return new Vector3(
                spawnInfo.spawnPoint.position.x,
                0f,
                spawnInfo.spawnPoint.position.z
            );
        }

        // Fallback to center
        Debug.LogWarning($"[NetworkSpawnManager] No spawn point available for client {clientId}, using fallback");
        return Vector3.zero;
    }

    /// <summary>
    /// Handle late join - find an NPC to replace with player.
    /// Returns the spawn point where player should spawn.
    /// </summary>
    public SpawnPointInfo HandleLateJoin(ulong clientId)
    {
        var npcPoints = GetNPCOccupiedSpawnPoints();

        if (npcPoints.Count == 0)
        {
            Debug.LogWarning($"[NetworkSpawnManager] Late join failed for client {clientId}: no NPCs to replace!");
            return null;
        }

        // Pick a random NPC to replace
        int randomIndex = Random.Range(0, npcPoints.Count);
        var chosen = npcPoints[randomIndex];

        // Despawn the NPC
        if (chosen.occupantNetObj != null && chosen.occupantNetObj.IsSpawned)
        {
            Debug.Log($"[NetworkSpawnManager] Late join: despawning NPC for client {clientId}");
            chosen.occupantNetObj.Despawn();
            spawnedNPCs.Remove(chosen.occupantNetObj);
            totalNPCsSpawned--;
        }

        chosen.occupant = SpawnPointOccupant.Player;
        chosen.occupantNetObj = null;
        chosen.playerClientId = clientId;

        return chosen;
    }

    /// <summary>
    /// Called when a client connects - they automatically receive all spawned NPCs via Netcode
    /// </summary>
    public void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[NetworkSpawnManager] Client {clientId} connected - they will receive {spawnedNPCs.Count} NPCs automatically via Netcode");
        // Netcode automatically syncs NetworkObjects to new clients
        // Player spawn is handled separately via NetworkGameManager
    }

    /// <summary>
    /// Despawn all NPCs (for cleanup)
    /// </summary>
    public void DespawnAllNPCs()
    {
        foreach (var netObj in spawnedNPCs)
        {
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn();
            }
        }
        spawnedNPCs.Clear();
        totalNPCsSpawned = 0;
        Debug.Log("[NetworkSpawnManager] All NPCs despawned");
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Public getters
    public bool IsInitialized => isInitialized;
    public int NPCCount => totalNPCsSpawned;
    public int SpawnPointCount => allSpawnPoints.Count;
    public List<SpawnPointInfo> AllSpawnPoints => allSpawnPoints;
}
