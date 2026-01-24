using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using Unity.Netcode;

/// <summary>
/// Handles spawning of entities (NPCs, defense zones, interactables) with randomization rules.
/// Works with MapManager to respect zone boundaries and distance requirements.
///
/// SPAWN POINT SYSTEM:
/// - Each NPC spawn point holds exactly 1 NPC
/// - Players (astronaut/alien) take a random spawn point and replace the NPC
/// - Late joiners despawn an existing NPC and take their place
/// </summary>
public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

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
        public GameObject occupantObject; // The NPC or Player at this point
        public string zoneName;

        public SpawnPointInfo(Transform point, string zone)
        {
            spawnPoint = point;
            zoneName = zone;
            occupant = SpawnPointOccupant.Empty;
            occupantObject = null;
        }
    }

    [Header("Spawn Point Tracking")]
    [SerializeField] private List<SpawnPointInfo> allSpawnPoints = new List<SpawnPointInfo>();

    // ==================== SETTINGS ====================

    [Header("Spawn Settings")]
    [Tooltip("Number of aliens to assign among NPCs")]
    public int aliensToAssign = 3;

    [Tooltip("Minimum distance defense zones must spawn from astronaut")]
    public float minDefenseZoneDistance = 20f;

    [Tooltip("Number of defense zones to spawn")]
    public int defenseZonesToSpawn = 2;

    [Header("Interactables Per Zone")]
    public int coffeeMachinesPerZone = 2;
    public int alarmTerminalsPerZone = 1;

    [Header("Prefabs (Optional)")]
    [Tooltip("If null, existing scene NPCs will be used")]
    public GameObject npcPrefab;
    public GameObject defenseZonePrefab;
    public GameObject coffeeMachinePrefab;
    public GameObject alarmTerminalPrefab;

    [Header("Events")]
    public UnityEvent OnSpawningComplete;
    public UnityEvent<GameObject, Vector3> OnPlayerSpawned; // Player object, spawn position

    [Header("Debug")]
    [SerializeField] private List<DefenseZone> spawnedDefenseZones = new List<DefenseZone>();
    [SerializeField] private List<CoffeeMachine> spawnedCoffeeMachines = new List<CoffeeMachine>();
    [SerializeField] private List<AlarmTerminal> spawnedAlarmTerminals = new List<AlarmTerminal>();

    private Transform astronautTransform;
    private bool hasSpawnedEntities = false;
    private bool hasSpawnedInteractables = false;
    private bool hasSpawnedNPCs = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // Find astronaut
        var player = FindFirstObjectByType<PlayerMovement>();
        if (player != null)
        {
            astronautTransform = player.transform;
        }

        // If we're a client (not server), spawn interactables locally after a delay
        // Server spawning is handled by NetworkSpawnManager
        StartCoroutine(ClientSideSpawnCheck());
    }

    /// <summary>
    /// Reset all spawn flags - call when starting a new game or joining
    /// </summary>
    public void ResetSpawnFlags()
    {
        hasSpawnedEntities = false;
        hasSpawnedInteractables = false;
        hasSpawnedNPCs = false;
        Debug.Log("[SpawnManager] Spawn flags reset");
    }

    /// <summary>
    /// Full reset of SpawnManager state - call when returning to lobby
    /// This clears all spawn point references which become invalid after scene reload
    /// </summary>
    public void ResetAll()
    {
        Debug.Log("[SpawnManager] Full reset - clearing all spawn data");

        // Reset flags
        hasSpawnedEntities = false;
        hasSpawnedInteractables = false;
        hasSpawnedNPCs = false;

        // Clear spawn points (they become null after scene reload)
        allSpawnPoints.Clear();

        // Clear astronaut reference
        astronautTransform = null;

        Debug.Log("[SpawnManager] Full reset complete");
    }

    /// <summary>
    /// Called by NetworkGameManager when server says world is ready.
    /// Both server and client spawn local entities (interactables, defense zones).
    /// NPCs are handled separately by NetworkSpawnManager (server only, synced via Netcode).
    /// </summary>
    public void OnWorldReady()
    {
        Debug.Log("[SpawnManager] OnWorldReady called!");

        bool isServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        bool isClient = NetworkManager.Singleton != null &&
                       NetworkManager.Singleton.IsClient &&
                       !NetworkManager.Singleton.IsServer;

        if (isServer)
        {
            // Server/Host: spawn interactables and defense zones (NPCs handled by NetworkSpawnManager)
            Debug.Log("[SpawnManager] SERVER: Spawning interactables and defense zones...");
            StartCoroutine(SpawnServerEntities());
        }
        else if (isClient)
        {
            // Client: spawn local entities now that world is ready
            Debug.Log("[SpawnManager] CLIENT: Spawning local interactables...");
            StartCoroutine(SpawnClientEntities());
        }
        else if (NetworkManager.Singleton == null)
        {
            // Single player
            SpawnAllEntities();
        }
    }

    System.Collections.IEnumerator SpawnServerEntities()
    {
        Debug.Log("[SpawnManager] SERVER: Setting up interactables...");

        // Wait for zones to be ready
        float timeout = 5f;
        float elapsed = 0f;
        while ((MapManager.Instance == null || MapManager.Instance.ZoneCount == 0) && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.2f);
            elapsed += 0.2f;
        }

        if (MapManager.Instance == null || MapManager.Instance.ZoneCount == 0)
        {
            Debug.LogError("[SpawnManager] SERVER: No zones found! Cannot spawn interactables.");
            yield break;
        }

        Debug.Log($"[SpawnManager] SERVER: Found {MapManager.Instance.ZoneCount} zones");

        // Spawn defense zones and interactables
        if (!hasSpawnedInteractables)
        {
            hasSpawnedInteractables = true;
            SpawnDefenseZones();
            SpawnInteractables();
            Debug.Log("[SpawnManager] SERVER: Interactables spawned!");
        }

        OnSpawningComplete?.Invoke();
    }

    System.Collections.IEnumerator SpawnClientEntities()
    {
        Debug.Log("[SpawnManager] CLIENT: Setting up local interactables...");

        // Reset flags
        ResetSpawnFlags();

        // Wait a moment for scene to be fully ready
        yield return new WaitForSeconds(0.5f);

        // Find zones - they should exist in the scene
        if (MapManager.Instance != null)
        {
            MapManager.Instance.RefreshZones();
            Debug.Log($"[SpawnManager] CLIENT: Found {MapManager.Instance.ZoneCount} zones");
        }

        // NOTE: NPCs are spawned by SERVER via NetworkSpawnManager
        // Clients receive them automatically via Netcode - no local spawn needed!

        // Only spawn interactables locally (they don't need to sync)
        if (!hasSpawnedInteractables && MapManager.Instance != null && MapManager.Instance.ZoneCount > 0)
        {
            hasSpawnedInteractables = true;
            // NO SpawnNPCsLocally() - server handles NPCs!
            SpawnDefenseZones();
            SpawnInteractables();
            Debug.Log("[SpawnManager] CLIENT: Local interactables spawned!");
        }
        else
        {
            Debug.LogWarning($"[SpawnManager] CLIENT: Cannot spawn - zones: {MapManager.Instance?.ZoneCount ?? 0}");
        }
    }

    System.Collections.IEnumerator ClientSideSpawnCheck()
    {
        Debug.Log("[SpawnManager] ClientSideSpawnCheck starting...");

        // Wait for network to initialize - need to wait for connection to complete
        float timeout = 10f;
        float elapsed = 0f;

        // Wait until we know our network role
        while (elapsed < timeout)
        {
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;

            // No network = single player
            if (NetworkManager.Singleton == null)
            {
                Debug.Log("[SpawnManager] Single player mode - spawning entities");
                yield return new WaitForSeconds(0.5f);
                SpawnAllEntities();
                yield break;
            }

            // Check if we're connected
            bool isConnected = NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer;
            if (!isConnected)
            {
                Debug.Log($"[SpawnManager] Waiting for network connection... ({elapsed}s)");
                continue;
            }

            // We're connected - determine role
            bool isClientOnly = NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer;
            bool isServer = NetworkManager.Singleton.IsServer;

            Debug.Log($"[SpawnManager] Connected! IsClient={NetworkManager.Singleton.IsClient}, IsServer={isServer}, IsHost={NetworkManager.Singleton.IsHost}");

            if (isServer)
            {
                // Server/Host: NetworkSpawnManager handles everything
                Debug.Log("[SpawnManager] SERVER/HOST mode - NetworkSpawnManager handles spawning");
                yield break;
            }

            if (isClientOnly)
            {
                // Pure client: wait for zones to be ready, then spawn interactables
                Debug.Log("[SpawnManager] CLIENT mode - waiting for zones to be ready...");

                // Wait for MapManager and zones
                float zoneTimeout = 10f;
                float zoneElapsed = 0f;
                while (zoneElapsed < zoneTimeout)
                {
                    if (MapManager.Instance != null && MapManager.Instance.ZoneCount > 0)
                    {
                        Debug.Log($"[SpawnManager] CLIENT: Zones ready! ({MapManager.Instance.ZoneCount} zones)");
                        break;
                    }
                    yield return new WaitForSeconds(0.5f);
                    zoneElapsed += 0.5f;
                    Debug.Log($"[SpawnManager] CLIENT: Waiting for zones... ({zoneElapsed}s)");
                }

                // Spawn interactables locally (same positions as server due to deterministic seeding)
                if (MapManager.Instance != null && MapManager.Instance.ZoneCount > 0)
                {
                    Debug.Log("[SpawnManager] CLIENT: Spawning local interactables...");
                    yield return StartCoroutine(SpawnClientEntities());
                }
                else
                {
                    Debug.LogError("[SpawnManager] CLIENT: No zones found after timeout!");
                }
                yield break;
            }
        }

        Debug.LogWarning("[SpawnManager] Timeout waiting for network role determination");
    }

    // ==================== SPAWN POINT COLLECTION ====================

    /// <summary>
    /// Collect all NPC spawn points from all zones
    /// </summary>
    public void CollectAllSpawnPoints()
    {
        allSpawnPoints.Clear();

        if (MapManager.Instance == null)
        {
            Debug.LogWarning("[SpawnManager] MapManager.Instance is null - cannot collect spawn points");
            return;
        }

        // Try to refresh zones if none found (may happen after scene reload)
        if (MapManager.Instance.ZoneCount == 0)
        {
            Debug.Log("[SpawnManager] No zones found, asking MapManager to find zones in scene...");
            MapManager.Instance.FindAllZonesInScene();
        }

        if (MapManager.Instance.ZoneCount == 0)
        {
            Debug.LogWarning("[SpawnManager] Still no zones found after refresh - spawn point collection failed");
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

        Debug.Log($"[SpawnManager] Collected {allSpawnPoints.Count} spawn points from {MapManager.Instance.ZoneCount} zones");
    }

    /// <summary>
    /// Get list of spawn points with NPCs (for late join replacement)
    /// Filters out invalid spawn points (null Transform after scene reload)
    /// </summary>
    public List<SpawnPointInfo> GetNPCOccupiedSpawnPoints()
    {
        List<SpawnPointInfo> npcPoints = new List<SpawnPointInfo>();
        foreach (var info in allSpawnPoints)
        {
            // Filter out invalid spawn points (null Transform after scene reload)
            if (info.occupant == SpawnPointOccupant.NPC && info.occupantObject != null && info.spawnPoint != null)
            {
                npcPoints.Add(info);
            }
        }
        return npcPoints;
    }

    /// <summary>
    /// Get list of empty spawn points (filters out null/invalid spawn points)
    /// </summary>
    public List<SpawnPointInfo> GetEmptySpawnPoints()
    {
        List<SpawnPointInfo> emptyPoints = new List<SpawnPointInfo>();
        foreach (var info in allSpawnPoints)
        {
            // Filter out invalid spawn points (null Transform after scene reload)
            if (info.occupant == SpawnPointOccupant.Empty && info.spawnPoint != null)
            {
                emptyPoints.Add(info);
            }
        }
        return emptyPoints;
    }

    // ==================== NPC SPAWNING ====================

    /// <summary>
    /// Spawn NPCs locally (for clients or single player)
    /// Spawns exactly 1 NPC per spawn point defined in zones
    /// </summary>
    public void SpawnNPCsLocally()
    {
        // Prevent double spawning
        if (hasSpawnedNPCs)
        {
            Debug.Log("[SpawnManager] NPCs already spawned, skipping");
            return;
        }

        if (MapManager.Instance == null || MapManager.Instance.ZoneCount == 0)
        {
            Debug.LogWarning("[SpawnManager] No zones found for NPC spawning");
            return;
        }

        hasSpawnedNPCs = true;

        // First collect all spawn points
        CollectAllSpawnPoints();

        if (allSpawnPoints.Count == 0)
        {
            Debug.LogWarning("[SpawnManager] No spawn points found in zones! Add npcSpawnPoints to your MapZones.");
            return;
        }

        int totalSpawned = 0;

        // Spawn 1 NPC at each spawn point
        for (int i = 0; i < allSpawnPoints.Count; i++)
        {
            var spawnInfo = allSpawnPoints[i];
            if (spawnInfo.spawnPoint == null) continue;

            Vector3 pos = spawnInfo.spawnPoint.position;
            string npcName = $"Crew_{spawnInfo.zoneName}_{i + 1}";

            GameObject npcObj = CreateLocalNPC(pos, npcName);

            if (npcObj != null)
            {
                // Track this spawn point as occupied by NPC
                spawnInfo.occupant = SpawnPointOccupant.NPC;
                spawnInfo.occupantObject = npcObj;
                totalSpawned++;
            }
        }

        Debug.Log($"[SpawnManager] Spawned {totalSpawned} NPCs (1 per spawn point)");
    }

    /// <summary>
    /// Create a local NPC and return the GameObject
    /// </summary>
    GameObject CreateLocalNPC(Vector3 position, string npcName)
    {
        GameObject npcObj;

        if (npcPrefab != null)
        {
            npcObj = Instantiate(npcPrefab, position, Quaternion.identity);
        }
        else
        {
            // Create basic NPC
            npcObj = new GameObject(npcName);
            npcObj.transform.position = position;

            // Add visual (capsule)
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.transform.SetParent(npcObj.transform);
            visual.transform.localPosition = new Vector3(0, 1f, 0);
            visual.transform.localScale = new Vector3(0.8f, 1f, 0.8f);

            // Set color (crew uniform - blue/grey)
            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.3f, 0.4f, 0.6f);
            }

            // Remove collider from visual
            var visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null) Destroy(visualCollider);

            // Add CharacterController to main object
            var cc = npcObj.AddComponent<CharacterController>();
            cc.center = new Vector3(0, 1f, 0);
            cc.height = 2f;
            cc.radius = 0.4f;
        }

        npcObj.name = npcName;

        // Add NPCBehavior
        var npc = npcObj.GetComponent<NPCBehavior>();
        if (npc == null)
        {
            npc = npcObj.AddComponent<NPCBehavior>();
        }

        // Set NPC name
        var field = typeof(NPCBehavior).GetField("npcName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(npc, npcName);
        }

        return npcObj;
    }

    // ==================== PLAYER SPAWNING ====================

    /// <summary>
    /// Get a random spawn point for a new player.
    /// The NPC at that spawn point will NOT be spawned (or will be removed if already exists).
    /// Returns null if no spawn points available.
    /// </summary>
    public SpawnPointInfo ReserveSpawnPointForPlayer()
    {
        // If spawn points not collected yet, do it now
        if (allSpawnPoints.Count == 0)
        {
            CollectAllSpawnPoints();
        }

        // Ensure random is truly random (not using deterministic seed)
        Random.InitState((int)System.DateTime.Now.Ticks + System.Environment.TickCount);

        // First try to find an empty spawn point
        var emptyPoints = GetEmptySpawnPoints();
        if (emptyPoints.Count > 0)
        {
            // Shuffle the list for extra randomness
            ShuffleList(emptyPoints);
            var chosen = emptyPoints[0];
            chosen.occupant = SpawnPointOccupant.Player;
            Debug.Log($"[SpawnManager] Reserved empty spawn point for player at {chosen.spawnPoint.position} (zone: {chosen.zoneName})");
            return chosen;
        }

        // No empty points - take over a NPC spawn point
        var npcPoints = GetNPCOccupiedSpawnPoints();
        if (npcPoints.Count > 0)
        {
            // Shuffle the list for extra randomness
            ShuffleList(npcPoints);
            var chosen = npcPoints[0];

            // Destroy the NPC at this point
            if (chosen.occupantObject != null)
            {
                Debug.Log($"[SpawnManager] Despawning NPC '{chosen.occupantObject.name}' to make room for player");

                var netObj = chosen.occupantObject.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                        netObj.Despawn(true);
                }
                else
                {
                    Destroy(chosen.occupantObject);
                }
            }

            chosen.occupant = SpawnPointOccupant.Player;
            chosen.occupantObject = null;
            Debug.Log($"[SpawnManager] Reserved NPC spawn point for player at {chosen.spawnPoint.position} (zone: {chosen.zoneName})");
            return chosen;
        }

        // If no valid spawn points found, try to recollect (scene might have just loaded)
        Debug.LogWarning("[SpawnManager] No valid spawn points found, attempting to recollect...");
        CollectAllSpawnPoints();

        // Try one more time after recollecting
        emptyPoints = GetEmptySpawnPoints();
        if (emptyPoints.Count > 0)
        {
            ShuffleList(emptyPoints);
            var chosen = emptyPoints[0];
            chosen.occupant = SpawnPointOccupant.Player;
            Debug.Log($"[SpawnManager] Reserved spawn point after recollect at {chosen.spawnPoint.position}");
            return chosen;
        }

        Debug.LogWarning("[SpawnManager] No spawn points available for player after recollect!");
        return null;
    }

    /// <summary>
    /// Spawn a player at a reserved spawn point.
    /// Call ReserveSpawnPointForPlayer() first to get the spawn point.
    /// </summary>
    public void SpawnPlayerAtPoint(GameObject player, SpawnPointInfo spawnInfo)
    {
        if (spawnInfo == null || spawnInfo.spawnPoint == null)
        {
            Debug.LogError("[SpawnManager] Invalid spawn point for player!");
            return;
        }

        // Move player to spawn point
        Vector3 spawnPos = spawnInfo.spawnPoint.position;
        player.transform.position = spawnPos;

        // Update tracking
        spawnInfo.occupant = SpawnPointOccupant.Player;
        spawnInfo.occupantObject = player;

        Debug.Log($"[SpawnManager] Player '{player.name}' spawned at {spawnPos} (zone: {spawnInfo.zoneName})");
        OnPlayerSpawned?.Invoke(player, spawnPos);
    }

    /// <summary>
    /// Called when a player joins late - finds an NPC to replace.
    /// Returns the spawn point where player should spawn, or null if failed.
    /// </summary>
    public SpawnPointInfo HandleLateJoin()
    {
        var npcPoints = GetNPCOccupiedSpawnPoints();

        if (npcPoints.Count == 0)
        {
            Debug.LogWarning("[SpawnManager] Late join failed: no NPCs to replace!");
            return null;
        }

        // Pick a random NPC to replace
        int randomIndex = Random.Range(0, npcPoints.Count);
        var chosen = npcPoints[randomIndex];

        // Destroy the NPC
        if (chosen.occupantObject != null)
        {
            Debug.Log($"[SpawnManager] Late join: despawning NPC '{chosen.occupantObject.name}'");
            var netObj = chosen.occupantObject.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                    netObj.Despawn(true);
            }
            else
            {
                Destroy(chosen.occupantObject);
            }

        }

        chosen.occupant = SpawnPointOccupant.Player;
        chosen.occupantObject = null;

        return chosen;
    }

    /// <summary>
    /// Get the position for initial player spawn (before game starts).
    /// Reserves a spawn point and returns position.
    /// </summary>
    public Vector3 GetPlayerSpawnPosition()
    {
        var spawnInfo = ReserveSpawnPointForPlayer();
        if (spawnInfo != null && spawnInfo.spawnPoint != null)
        {
            return spawnInfo.spawnPoint.position;
        }

        // Fallback to center
        Debug.LogWarning("[SpawnManager] No spawn point available, using fallback position");
        return Vector3.zero;
    }

    /// <summary>
    /// Main entry point: spawns/assigns all entities
    /// Called by GameManager.StartGame()
    /// </summary>
    public void SpawnAllEntities()
    {
        if (hasSpawnedEntities)
        {
            Debug.Log("[SpawnManager] Already spawned entities, skipping...");
            return;
        }
        hasSpawnedEntities = true;
        hasSpawnedInteractables = true; // Also mark interactables as spawned

        Debug.Log("[SpawnManager] ========== SPAWNING ENTITIES ==========");

        // Find astronaut position
        if (astronautTransform == null)
        {
            var player = FindFirstObjectByType<PlayerMovement>();
            if (player != null)
            {
                astronautTransform = player.transform;
            }
        }

        // 1. Spawn NPCs (only in single player mode - NetworkSpawnManager handles multiplayer NPCs)
        bool isMultiplayer = NetworkManager.Singleton != null &&
                            (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient);
        if (!isMultiplayer)
        {
            // Single player only - spawn NPCs locally
            SpawnNPCsLocally();
        }
        else
        {
            Debug.Log("[SpawnManager] Multiplayer mode - skipping NPC spawn (NetworkSpawnManager handles it)");
        }

        // 2. Assign aliens to NPCs
        AssignAliensToNPCs(aliensToAssign);

        // 3. Spawn defense zones
        SpawnDefenseZones();

        // 4. Spawn interactables
        SpawnInteractables();

        Debug.Log("[SpawnManager] ========== SPAWNING COMPLETE ==========");
        OnSpawningComplete?.Invoke();
    }

    // ==================== ALIEN ASSIGNMENT ====================

    /// <summary>
    /// Randomly assigns aliens among existing NPCs using Fisher-Yates shuffle
    /// </summary>
    public void AssignAliensToNPCs(int alienCount)
    {
        NPCBehavior[] allNPCs = FindObjectsByType<NPCBehavior>(FindObjectsSortMode.None);

        if (allNPCs.Length == 0)
        {
            Debug.LogWarning("[SpawnManager] No NPCs found to assign as aliens!");
            return;
        }

        // Create shuffled list
        List<NPCBehavior> shuffledNPCs = new List<NPCBehavior>(allNPCs);
        ShuffleList(shuffledNPCs);

        // Assign first N as aliens
        int assigned = 0;
        for (int i = 0; i < Mathf.Min(alienCount, shuffledNPCs.Count); i++)
        {
            if (shuffledNPCs[i] != null)
            {
                shuffledNPCs[i].SetAsAlien(true);
                assigned++;
                Debug.Log($"[SpawnManager] NPC '{shuffledNPCs[i].Name}' assigned as ALIEN");
            }
        }

        Debug.Log($"[SpawnManager] Assigned {assigned} aliens among {allNPCs.Length} NPCs");
    }

    // ==================== DEFENSE ZONE SPAWNING ====================

    /// <summary>
    /// Spawn defense zones at valid positions (respecting min distance from astronaut)
    /// </summary>
    public void SpawnDefenseZones()
    {
        spawnedDefenseZones.Clear();

        Vector3 astronautPos = astronautTransform != null ? astronautTransform.position : Vector3.zero;

        // Get valid spawn points
        List<Transform> validPoints = new List<Transform>();

        if (MapManager.Instance != null)
        {
            validPoints = MapManager.Instance.GetValidDefenseZoneSpawnPoints(astronautPos);
        }

        // If no MapManager or no valid points, find existing DefenseZones and use fallback
        if (validPoints.Count == 0)
        {
            Debug.Log("[SpawnManager] No valid defense zone spawn points from MapManager, using fallback");

            // Check for existing defense zones in scene
            DefenseZone[] existingZones = FindObjectsByType<DefenseZone>(FindObjectsSortMode.None);
            if (existingZones.Length > 0)
            {
                spawnedDefenseZones.AddRange(existingZones);
                Debug.Log($"[SpawnManager] Found {existingZones.Length} existing defense zones");
                return;
            }

            // Create fallback spawn points if nothing exists
            validPoints = CreateFallbackDefenseSpawnPoints(astronautPos);
        }

        // Shuffle valid points
        ShuffleList(validPoints);

        // Spawn defense zones at selected points
        int spawned = 0;
        for (int i = 0; i < Mathf.Min(defenseZonesToSpawn, validPoints.Count); i++)
        {
            if (validPoints[i] == null) continue;

            DefenseZone zone = SpawnDefenseZoneAt(validPoints[i].position);
            if (zone != null)
            {
                spawnedDefenseZones.Add(zone);
                spawned++;
            }
        }

        Debug.Log($"[SpawnManager] Spawned {spawned} defense zones (target: {defenseZonesToSpawn})");
    }

    DefenseZone SpawnDefenseZoneAt(Vector3 position)
    {
        GameObject zoneObj;

        if (defenseZonePrefab != null)
        {
            zoneObj = Instantiate(defenseZonePrefab, position, Quaternion.identity);
        }
        else
        {
            // Try to load 3D model from Resources
            GameObject modelPrefab = Resources.Load<GameObject>("Interactables/DefenceZone");

            if (modelPrefab != null)
            {
                Vector3 adjustedPos = position + Vector3.up * 0.2f; // Lift above ground
                zoneObj = Instantiate(modelPrefab, adjustedPos, Quaternion.Euler(0f, 0f, 0f));
                zoneObj.transform.localScale = Vector3.one * 1f; // Half size
                zoneObj.name = $"DefenseZone_{spawnedDefenseZones.Count + 1}";
                Debug.Log($"[SpawnManager] Loaded DefenseZone 3D model from Resources");
            }
            else
            {
                zoneObj = new GameObject($"DefenseZone_{spawnedDefenseZones.Count + 1}");
                zoneObj.transform.position = position;
                // Visual will be created by DefenseZone.SetupVisuals()
                Debug.Log($"[SpawnManager] DefenseZone model not found, using fallback cylinder");
            }
        }

        // Add component if not already present
        var zone = zoneObj.GetComponent<DefenseZone>();
        if (zone == null)
        {
            zone = zoneObj.AddComponent<DefenseZone>();
        }

        // NOTE: Don't spawn as NetworkObject - dynamically created objects can't be networked
        // without registered prefabs. Defense zones will be created locally on each client.

        zone.zoneName = $"Defense Point {(char)('A' + spawnedDefenseZones.Count)}";
        Debug.Log($"[SpawnManager] Created defense zone '{zone.zoneName}' at {position}");

        return zone;
    }

    List<Transform> CreateFallbackDefenseSpawnPoints(Vector3 astronautPos)
    {
        List<Transform> fallbackPoints = new List<Transform>();

        // Create spawn points in cardinal directions, away from astronaut
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

        foreach (var dir in directions)
        {
            Vector3 spawnPos = astronautPos + dir * (minDefenseZoneDistance + 10f);

            GameObject pointObj = new GameObject($"FallbackDefenseSpawn");
            pointObj.transform.position = spawnPos;
            pointObj.transform.SetParent(transform);

            fallbackPoints.Add(pointObj.transform);
        }

        return fallbackPoints;
    }

    // ==================== INTERACTABLE SPAWNING ====================

    /// <summary>
    /// Spawn coffee machines and alarm terminals in each zone
    /// </summary>
    public void SpawnInteractables()
    {
        spawnedCoffeeMachines.Clear();
        spawnedAlarmTerminals.Clear();

        if (MapManager.Instance == null || MapManager.Instance.ZoneCount == 0)
        {
            Debug.Log("[SpawnManager] No zones registered, spawning interactables at fallback positions");
            SpawnInteractablesAtFallback();
            return;
        }

        // Spawn in each zone
        foreach (var zone in MapManager.Instance.AllZones)
        {
            if (zone == null) continue;

            SpawnInteractablesInZone(zone);
        }

        Debug.Log($"[SpawnManager] Spawned {spawnedCoffeeMachines.Count} coffee machines, {spawnedAlarmTerminals.Count} alarm terminals");
    }

    void SpawnInteractablesInZone(MapZone zone)
    {
        List<Transform> spawnPoints = new List<Transform>(zone.interactableSpawnPoints ?? new Transform[0]);

        if (spawnPoints.Count == 0)
        {
            Debug.Log($"[SpawnManager] Zone '{zone.zoneName}' has no interactable spawn points");
            return;
        }

        // DETERMINISTIC: Use zone name as seed for consistent positions across server/client
        int seed = zone.zoneName.GetHashCode();
        Random.InitState(seed);
        ShuffleList(spawnPoints);

        int pointIndex = 0;

        // Spawn coffee machines
        for (int i = 0; i < coffeeMachinesPerZone && pointIndex < spawnPoints.Count; i++)
        {
            if (spawnPoints[pointIndex] != null)
            {
                var coffee = SpawnCoffeeMachineAt(spawnPoints[pointIndex].position);
                if (coffee != null)
                {
                    spawnedCoffeeMachines.Add(coffee);
                }
            }
            pointIndex++;
        }

        // Spawn alarm terminals
        for (int i = 0; i < alarmTerminalsPerZone && pointIndex < spawnPoints.Count; i++)
        {
            if (spawnPoints[pointIndex] != null)
            {
                var alarm = SpawnAlarmTerminalAt(spawnPoints[pointIndex].position);
                if (alarm != null)
                {
                    spawnedAlarmTerminals.Add(alarm);
                }
            }
            pointIndex++;
        }

        // Reset random state
        Random.InitState((int)System.DateTime.Now.Ticks);

        Debug.Log($"[SpawnManager] Zone '{zone.zoneName}': spawned interactables");
    }

    void SpawnInteractablesAtFallback()
    {
        // Spawn a few interactables near center if no zones exist
        Vector3 center = Vector3.zero;

        if (astronautTransform != null)
        {
            center = astronautTransform.position;
        }

        // Spawn 2 coffee machines
        for (int i = 0; i < 2; i++)
        {
            Vector3 pos = center + new Vector3(Random.Range(-15f, 15f), 0f, Random.Range(-15f, 15f));
            var coffee = SpawnCoffeeMachineAt(pos);
            if (coffee != null)
            {
                spawnedCoffeeMachines.Add(coffee);
            }
        }

        // Spawn 1 alarm terminal
        Vector3 alarmPos = center + new Vector3(Random.Range(-20f, 20f), 0f, Random.Range(-20f, 20f));
        var alarm = SpawnAlarmTerminalAt(alarmPos);
        if (alarm != null)
        {
            spawnedAlarmTerminals.Add(alarm);
        }
    }

    CoffeeMachine SpawnCoffeeMachineAt(Vector3 position)
    {
        GameObject coffeeObj;

        if (coffeeMachinePrefab != null)
        {
            coffeeObj = Instantiate(coffeeMachinePrefab, position, Quaternion.identity);
        }
        else
        {
            // Try to load 3D model from Resources
            GameObject modelPrefab = Resources.Load<GameObject>("Interactables/CoffeeMachine");

            if (modelPrefab != null)
            {
                Vector3 adjustedPos = position + Vector3.up * 0f; // Lift above ground
                coffeeObj = Instantiate(modelPrefab, adjustedPos, Quaternion.Euler(0f, 0f, 0f));
                coffeeObj.transform.localScale = Vector3.one * 1f; // Half size
                coffeeObj.name = $"CoffeeMachine_{spawnedCoffeeMachines.Count + 1}";
                Debug.Log($"[SpawnManager] Loaded CoffeeMachine 3D model from Resources");
            }
            else
            {
                coffeeObj = new GameObject($"CoffeeMachine_{spawnedCoffeeMachines.Count + 1}");
                coffeeObj.transform.position = position;
                // Visual will be created by CoffeeMachine.SetupVisuals()
                Debug.Log($"[SpawnManager] CoffeeMachine model not found, using fallback cube");
            }
        }

        // Add component if not already present
        var coffee = coffeeObj.GetComponent<CoffeeMachine>();
        if (coffee == null)
        {
            coffee = coffeeObj.AddComponent<CoffeeMachine>();
        }

        // NOTE: Don't spawn as NetworkObject - dynamically created objects can't be networked
        // without registered prefabs. Interactables will be created locally on each client.

        Debug.Log($"[SpawnManager] Created coffee machine at {position}");
        return coffee;
    }

    AlarmTerminal SpawnAlarmTerminalAt(Vector3 position)
    {
        GameObject alarmObj;

        if (alarmTerminalPrefab != null)
        {
            alarmObj = Instantiate(alarmTerminalPrefab, position, Quaternion.identity);
        }
        else
        {
            // Try to load 3D model from Resources
            GameObject modelPrefab = Resources.Load<GameObject>("Interactables/Terminal");

            if (modelPrefab != null)
            {
                Vector3 adjustedPos = position + Vector3.up * 0f; // Lift above ground
                alarmObj = Instantiate(modelPrefab, adjustedPos, Quaternion.Euler(0f, 0f, 0f));
                alarmObj.transform.localScale = Vector3.one * 1f; // Half size
                alarmObj.name = $"AlarmTerminal_{spawnedAlarmTerminals.Count + 1}";
                Debug.Log($"[SpawnManager] Loaded AlarmTerminal 3D model from Resources");
            }
            else
            {
                alarmObj = new GameObject($"AlarmTerminal_{spawnedAlarmTerminals.Count + 1}");
                alarmObj.transform.position = position;
                // Visual will be created by AlarmTerminal.SetupVisuals()
                Debug.Log($"[SpawnManager] AlarmTerminal model not found, using fallback cube");
            }
        }

        // Add component if not already present
        var alarm = alarmObj.GetComponent<AlarmTerminal>();
        if (alarm == null)
        {
            alarm = alarmObj.AddComponent<AlarmTerminal>();
        }

        // NOTE: Don't spawn as NetworkObject - dynamically created objects can't be networked
        // without registered prefabs. Alarm terminals will be created locally on each client.

        Debug.Log($"[SpawnManager] Created alarm terminal at {position}");
        return alarm;
    }

    /// <summary>
    /// Create a simple visual for interactables that don't have prefabs
    /// </summary>
    void CreateInteractableVisual(GameObject obj, Color color)
    {
        try
        {
            // Create a simple cube as visual
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(obj.transform);
            visual.transform.localPosition = Vector3.up * 0.5f;
            visual.transform.localScale = new Vector3(1f, 1f, 1f);

            // Set color - use the default material that comes with the primitive
            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
            {
                // Just change color on existing material (safer for builds)
                renderer.material.color = color;
            }

            // Remove collider from visual (parent has collider)
            var collider = visual.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SpawnManager] Could not create visual: {e.Message}");
        }
    }

    // ==================== UTILITY ====================

    /// <summary>
    /// Fisher-Yates shuffle
    /// </summary>
    public static void ShuffleList<T>(List<T> list)
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
    /// Clear all spawned entities (for game reset)
    /// </summary>
    public void ClearSpawnedEntities()
    {
        foreach (var zone in spawnedDefenseZones)
        {
            if (zone != null) Destroy(zone.gameObject);
        }
        spawnedDefenseZones.Clear();

        foreach (var coffee in spawnedCoffeeMachines)
        {
            if (coffee != null) Destroy(coffee.gameObject);
        }
        spawnedCoffeeMachines.Clear();

        foreach (var alarm in spawnedAlarmTerminals)
        {
            if (alarm != null) Destroy(alarm.gameObject);
        }
        spawnedAlarmTerminals.Clear();

        Debug.Log("[SpawnManager] Cleared all spawned entities");
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ==================== PUBLIC GETTERS ====================

    /// <summary>
    /// Total number of spawn points collected from all zones
    /// </summary>
    public int SpawnPointCount => allSpawnPoints.Count;

    /// <summary>
    /// Number of NPCs currently spawned
    /// </summary>
    public int NPCCount => GetNPCOccupiedSpawnPoints().Count;

    /// <summary>
    /// Number of players currently spawned
    /// </summary>
    public int PlayerCount
    {
        get
        {
            int count = 0;
            foreach (var info in allSpawnPoints)
            {
                if (info.occupant == SpawnPointOccupant.Player)
                    count++;
            }
            return count;
        }
    }

    /// <summary>
    /// All spawn point info (read-only access)
    /// </summary>
    public List<SpawnPointInfo> AllSpawnPoints => allSpawnPoints;

    // ==================== NPC CLEANUP ====================

    /// <summary>
    /// Remove any NPCs that are too close to a player position.
    /// Call this after spawning a player to ensure no NPC is inside them.
    /// </summary>
    public void ClearNPCsNearPosition(Vector3 position, float radius = 3f)
    {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        NPCBehavior[] allNPCs = FindObjectsByType<NPCBehavior>(FindObjectsSortMode.None);
        int cleared = 0;

        foreach (var npc in allNPCs)
        {
            if (npc == null || npc.IsDead) continue;

            float distance = Vector3.Distance(npc.transform.position, position);
            if (distance < radius)
            {
                Debug.Log($"[SpawnManager] Removing NPC '{npc.Name}' too close to player spawn (distance: {distance:F1}m)");

                // Update spawn point tracking
                foreach (var spawnInfo in allSpawnPoints)
                {
                    if (spawnInfo.occupantObject == npc.gameObject)
                    {
                        spawnInfo.occupant = SpawnPointOccupant.Empty;
                        spawnInfo.occupantObject = null;
                        break;
                    }
                }

                // Destroy the NPC
                if (npc.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                {
                    npc.NetworkObject.Despawn(true);
                }

                cleared++;
            }
        }

        if (cleared > 0)
        {
            Debug.Log($"[SpawnManager] Cleared {cleared} NPCs near player spawn position");
        }
    }

    /// <summary>
    /// Remove NPCs near all players in the scene.
    /// Call this at game start to ensure no player is inside an NPC.
    /// </summary>
    public void ClearNPCsNearAllPlayers(float radius = 3f)
    {
        // Clear near astronaut
        var astronaut = FindFirstObjectByType<PlayerMovement>();
        if (astronaut != null)
        {
            ClearNPCsNearPosition(astronaut.transform.position, radius);
        }

        // Clear near alien
        var alien = FindFirstObjectByType<AlienController>();
        if (alien != null)
        {
            ClearNPCsNearPosition(alien.transform.position, radius);
        }

        // Clear near any networked players
        var networkedPlayers = FindObjectsByType<NetworkedPlayer>(FindObjectsSortMode.None);
        foreach (var player in networkedPlayers)
        {
            if (player != null)
            {
                ClearNPCsNearPosition(player.transform.position, radius);
            }
        }
    }
}
