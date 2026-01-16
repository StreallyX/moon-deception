using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Server-only spawn manager for networked game.
/// Handles NPC spawning with NetworkObject sync.
/// NPCs are spawned on server and automatically replicated to clients.
/// </summary>
public class NetworkSpawnManager : MonoBehaviour
{
    public static NetworkSpawnManager Instance { get; private set; }

    [Header("NPC Prefab (Must have NetworkObject + registered in NetworkManager)")]
    public GameObject npcPrefab;

    [Header("Settings")]
    public int npcsPerZone = 5;
    public int randomSeed = 12345; // Fixed seed for deterministic spawning

    [Header("Debug")]
    [SerializeField] private bool isInitialized = false;
    [SerializeField] private int totalNPCsSpawned = 0;

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

    /// <summary>
    /// Spawn NPCs in all zones using deterministic positions
    /// </summary>
    void SpawnAllNPCs()
    {
        // Use fixed seed for deterministic spawning (same positions every game)
        Random.InitState(randomSeed);

        foreach (var zone in MapManager.Instance.AllZones)
        {
            if (zone == null) continue;

            SpawnNPCsInZone(zone);
        }

        // Reset random state
        Random.InitState((int)System.DateTime.Now.Ticks);

        Debug.Log($"[NetworkSpawnManager] Total NPCs spawned: {totalNPCsSpawned}");
    }

    /// <summary>
    /// Spawn NPCs in a specific zone
    /// </summary>
    void SpawnNPCsInZone(MapZone zone)
    {
        Debug.Log($"[NetworkSpawnManager] Spawning {npcsPerZone} NPCs in zone '{zone.zoneName}'");

        for (int i = 0; i < npcsPerZone; i++)
        {
            Vector3 position = GetSpawnPositionInZone(zone, i);
            string npcName = $"NPC_{zone.zoneName}_{i + 1}";

            SpawnSingleNPC(position, npcName);
        }
    }

    /// <summary>
    /// Get a spawn position in a zone (deterministic based on index)
    /// </summary>
    Vector3 GetSpawnPositionInZone(MapZone zone, int index)
    {
        // First try to use defined spawn points
        if (zone.npcSpawnPoints != null && index < zone.npcSpawnPoints.Length)
        {
            Transform point = zone.npcSpawnPoints[index];
            if (point != null)
            {
                return point.position;
            }
        }

        // Fallback: random position within zone bounds
        Bounds bounds = zone.Bounds;
        float x = Random.Range(bounds.min.x + 2f, bounds.max.x - 2f);
        float z = Random.Range(bounds.min.z + 2f, bounds.max.z - 2f);
        float y = bounds.center.y;

        return new Vector3(x, y, z);
    }

    /// <summary>
    /// Spawn a single NPC and register it on the network
    /// </summary>
    void SpawnSingleNPC(Vector3 position, string npcName)
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
    }

    /// <summary>
    /// Called when a client connects - they automatically receive all spawned NPCs via Netcode
    /// </summary>
    public void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[NetworkSpawnManager] Client {clientId} connected - they will receive {spawnedNPCs.Count} NPCs automatically via Netcode");
        // No action needed - Netcode automatically syncs NetworkObjects to new clients
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
}
