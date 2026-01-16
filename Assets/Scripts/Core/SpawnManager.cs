using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using Unity.Netcode;

/// <summary>
/// Handles spawning of entities (NPCs, defense zones, interactables) with randomization rules.
/// Works with MapManager to respect zone boundaries and distance requirements.
/// </summary>
public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

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
        var player = FindObjectOfType<PlayerMovement>();
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
    /// Called by NetworkGameManager when server says world is ready.
    /// Client can now safely spawn local entities.
    /// </summary>
    public void OnWorldReady()
    {
        Debug.Log("[SpawnManager] OnWorldReady called!");

        bool isClient = NetworkManager.Singleton != null &&
                       NetworkManager.Singleton.IsClient &&
                       !NetworkManager.Singleton.IsServer;

        if (isClient)
        {
            // Client: spawn local entities now that world is ready
            StartCoroutine(SpawnClientEntities());
        }
        else if (NetworkManager.Singleton == null)
        {
            // Single player
            SpawnAllEntities();
        }
        // Server doesn't need to do anything here - it already spawned via NetworkSpawnManager
    }

    System.Collections.IEnumerator SpawnClientEntities()
    {
        Debug.Log("[SpawnManager] CLIENT: Setting up local view...");

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
                // Pure client: wait for WorldReady signal from server
                Debug.Log("[SpawnManager] CLIENT mode - waiting for WorldReady signal from server...");

                // Check if world is already ready (we joined late)
                if (NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsWorldReady)
                {
                    Debug.Log("[SpawnManager] CLIENT: World already ready!");
                    OnWorldReady();
                }
                // Otherwise, WorldReady will be called via ClientRpc when server is ready
                yield break;
            }
        }

        Debug.LogWarning("[SpawnManager] Timeout waiting for network role determination");
    }

    // ==================== NPC SPAWNING ====================

    [Header("NPC Settings")]
    public int npcsPerZone = 5;

    /// <summary>
    /// Spawn NPCs locally (for clients or single player)
    /// Uses deterministic positioning based on zone
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
        int totalSpawned = 0;

        foreach (var zone in MapManager.Instance.AllZones)
        {
            if (zone == null) continue;

            // Use zone name as seed for deterministic positioning
            int seed = zone.zoneName.GetHashCode();
            Random.InitState(seed);

            for (int i = 0; i < npcsPerZone; i++)
            {
                Vector3 pos = GetRandomPositionInZone(zone);
                string npcName = $"Crew_{zone.zoneName}_{i + 1}";

                CreateLocalNPC(pos, npcName);
                totalSpawned++;
            }
        }

        // Reset random state
        Random.InitState((int)System.DateTime.Now.Ticks);

        Debug.Log($"[SpawnManager] Spawned {totalSpawned} NPCs locally");
    }

    Vector3 GetRandomPositionInZone(MapZone zone)
    {
        Bounds bounds = zone.Bounds;
        float x = Random.Range(bounds.min.x + 2f, bounds.max.x - 2f);
        float z = Random.Range(bounds.min.z + 2f, bounds.max.z - 2f);
        float y = 1f; // Ground level

        return new Vector3(x, y, z);
    }

    void CreateLocalNPC(Vector3 position, string npcName)
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
            var player = FindObjectOfType<PlayerMovement>();
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
        NPCBehavior[] allNPCs = FindObjectsOfType<NPCBehavior>();

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
            DefenseZone[] existingZones = FindObjectsOfType<DefenseZone>();
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
            zoneObj = new GameObject($"DefenseZone_{spawnedDefenseZones.Count + 1}");
            zoneObj.transform.position = position;
            zoneObj.AddComponent<DefenseZone>();

            // Add visual so it's visible
            CreateInteractableVisual(zoneObj, Color.green); // Green for defense
        }

        // NOTE: Don't spawn as NetworkObject - dynamically created objects can't be networked
        // without registered prefabs. Defense zones will be created locally on each client.

        var zone = zoneObj.GetComponent<DefenseZone>();
        if (zone != null)
        {
            zone.zoneName = $"Defense Point {(char)('A' + spawnedDefenseZones.Count)}";
            Debug.Log($"[SpawnManager] Created defense zone '{zone.zoneName}' at {position}");
        }

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
            coffeeObj = new GameObject($"CoffeeMachine_{spawnedCoffeeMachines.Count + 1}");
            coffeeObj.transform.position = position;
            coffeeObj.AddComponent<CoffeeMachine>();

            // Add visual so it's visible
            CreateInteractableVisual(coffeeObj, new Color(0.6f, 0.4f, 0.2f)); // Brown for coffee
        }

        // NOTE: Don't spawn as NetworkObject - dynamically created objects can't be networked
        // without registered prefabs. Interactables will be created locally on each client.

        var coffee = coffeeObj.GetComponent<CoffeeMachine>();
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
            alarmObj = new GameObject($"AlarmTerminal_{spawnedAlarmTerminals.Count + 1}");
            alarmObj.transform.position = position;
            alarmObj.AddComponent<AlarmTerminal>();

            // Add visual so it's visible
            CreateInteractableVisual(alarmObj, Color.red); // Red for alarm
        }

        // NOTE: Don't spawn as NetworkObject - dynamically created objects can't be networked
        // without registered prefabs. Alarm terminals will be created locally on each client.

        var alarm = alarmObj.GetComponent<AlarmTerminal>();
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
}
