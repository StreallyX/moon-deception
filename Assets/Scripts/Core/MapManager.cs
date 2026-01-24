using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

/// <summary>
/// Central manager for all map zones.
/// Handles zone registration, queries, and spawn point management.
/// NETWORK-AWARE: Only SERVER can create zones. Clients just find existing ones.
/// </summary>
public class MapManager : MonoBehaviour
{
    public static MapManager Instance { get; private set; }

    [Header("Registered Zones")]
    [SerializeField] private List<MapZone> allZones = new List<MapZone>();

    [Header("Configuration")]
    [Tooltip("Minimum distance defense zones must spawn from astronaut")]
    public float minDefenseZoneDistance = 20f;

    [Tooltip("Number of defense zones to spawn per game")]
    public int defenseZonesPerGame = 2;

    [Tooltip("Number of coffee machines per zone")]
    public int coffeeMachinesPerZone = 2;

    [Tooltip("Number of alarm terminals per zone")]
    public int alarmTerminalsPerZone = 1;

    public List<MapZone> AllZones => allZones;
    public int ZoneCount => allZones.Count;

    // Network helpers
    private bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
    private bool IsClient => NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient;
    private bool IsNetworked => NetworkManager.Singleton != null &&
                               (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient);

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
        // Wait for network to be ready before initializing
        StartCoroutine(WaitForNetworkAndInitialize());
    }

    System.Collections.IEnumerator WaitForNetworkAndInitialize()
    {
        Debug.Log("[MapManager] Waiting for network to be ready...");

        // Wait for NetworkManager to exist
        float timeout = 10f;
        float elapsed = 0f;
        while (NetworkManager.Singleton == null && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        // Wait for network to start (Host/Client)
        elapsed = 0f;
        while (!IsNetworked && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (!IsNetworked)
        {
            Debug.Log("[MapManager] Not in networked mode - initializing for single player");
            yield return StartCoroutine(InitializeZones(allowCreate: true));
            yield break;
        }

        Debug.Log($"[MapManager] Network ready! IsServer={IsServer}, IsClient={IsClient}");

        // Initialize zones based on role
        // SERVER: Can create zones if needed
        // CLIENT: Only find existing zones (they're scene objects, identical on all machines)
        yield return StartCoroutine(InitializeZones(allowCreate: IsServer));
    }

    System.Collections.IEnumerator InitializeZones(bool allowCreate)
    {
        Debug.Log($"[MapManager] InitializeZones - IsServer={IsServer}, IsClient={IsClient}");

        // Wait a moment for scene to be fully loaded
        yield return new WaitForSeconds(0.3f);

        // First, try to find zones already in the scene
        FindAllZonesInScene();

        if (allZones.Count > 0)
        {
            Debug.Log($"[MapManager] Found {allZones.Count} zones in scene - using those");
            LogAllZones();
            yield break;
        }

        // No zones in scene - BOTH server and client create identical local zones!
        // Zones are just configuration data (spawn points, boundaries) - not gameplay objects.
        // They must be identical on all machines for spawn positions to match.
        Debug.Log($"[MapManager] No zones in scene - creating default zones locally...");
        CreateDefaultZonesLocal();

        LogAllZones();
    }

    /// <summary>
    /// Create default zones as LOCAL objects (not networked).
    /// These are configuration data - spawn points, boundaries, etc.
    /// Both server AND client create the same zones for consistency.
    /// </summary>
    void CreateDefaultZonesLocal()
    {
        Debug.Log("[MapManager] Creating default zones (local)...");

        // Create 4 zones in a grid pattern - DETERMINISTIC positions
        Vector3[] zonePositions = new Vector3[]
        {
            new Vector3(-30f, 0f, 30f),   // Habitat
            new Vector3(30f, 0f, 30f),    // Research
            new Vector3(-30f, 0f, -30f),  // Industrial
            new Vector3(30f, 0f, -30f)    // Command
        };

        MapZone.ZoneType[] zoneTypes = new MapZone.ZoneType[]
        {
            MapZone.ZoneType.Habitat,
            MapZone.ZoneType.Research,
            MapZone.ZoneType.Industrial,
            MapZone.ZoneType.Command
        };

        Color[] zoneColors = new Color[]
        {
            Color.green,
            Color.blue,
            Color.yellow,
            Color.red
        };

        for (int i = 0; i < zonePositions.Length; i++)
        {
            GameObject zoneObj = new GameObject($"Zone_{zoneTypes[i]}");
            zoneObj.transform.position = zonePositions[i];

            MapZone zone = zoneObj.AddComponent<MapZone>();
            zone.zoneType = zoneTypes[i];
            zone.zoneName = zoneTypes[i].ToString();
            zone.zoneColor = zoneColors[i];

            // Create BoxCollider for zone bounds
            BoxCollider col = zoneObj.GetComponent<BoxCollider>();
            if (col == null) col = zoneObj.AddComponent<BoxCollider>();
            col.size = new Vector3(40f, 10f, 40f);
            col.center = new Vector3(0f, 5f, 0f);
            col.isTrigger = true;

            // Create spawn points as child objects - DETERMINISTIC positions
            zone.npcSpawnPoints = new Transform[5];
            for (int j = 0; j < 5; j++)
            {
                GameObject spawnPoint = new GameObject($"NPCSpawn_{j + 1}");
                spawnPoint.transform.SetParent(zoneObj.transform);
                float offset = (j - 2f) * 4f;
                spawnPoint.transform.localPosition = new Vector3(offset, 1f, 0f);
                zone.npcSpawnPoints[j] = spawnPoint.transform;
            }

            // Create interactable spawn points - DETERMINISTIC positions
            zone.interactableSpawnPoints = new Transform[4];
            Vector3[] interactableOffsets = new Vector3[]
            {
                new Vector3(-12f, 1f, 12f),
                new Vector3(12f, 1f, 12f),
                new Vector3(-12f, 1f, -12f),
                new Vector3(12f, 1f, -12f)
            };
            for (int j = 0; j < 4; j++)
            {
                GameObject interactPoint = new GameObject($"InteractSpawn_{j + 1}");
                interactPoint.transform.SetParent(zoneObj.transform);
                interactPoint.transform.localPosition = interactableOffsets[j];
                zone.interactableSpawnPoints[j] = interactPoint.transform;
            }

            // Create patrol waypoints - DETERMINISTIC positions
            zone.patrolWaypoints = new Transform[4];
            Vector3[] patrolOffsets = new Vector3[]
            {
                new Vector3(-10f, 1f, -10f),
                new Vector3(10f, 1f, -10f),
                new Vector3(10f, 1f, 10f),
                new Vector3(-10f, 1f, 10f)
            };
            for (int j = 0; j < 4; j++)
            {
                GameObject waypoint = new GameObject($"Waypoint_{j + 1}");
                waypoint.transform.SetParent(zoneObj.transform);
                waypoint.transform.localPosition = patrolOffsets[j];
                zone.patrolWaypoints[j] = waypoint.transform;
            }

            // Register the zone
            RegisterZone(zone);

            Debug.Log($"[MapManager] Created zone '{zone.zoneName}' at {zonePositions[i]}");
        }
    }

    void LogAllZones()
    {
        Debug.Log($"[MapManager] Total zones: {allZones.Count}");
        foreach (var zone in allZones)
        {
            if (zone != null)
            {
                Debug.Log($"  - {zone.zoneName} ({zone.zoneType}) at {zone.transform.position}");
            }
        }
    }

    /// <summary>
    /// Public method to create default zones - for external callers.
    /// Calls the local zone creation method.
    /// </summary>
    public void CreateDefaultZones()
    {
        if (allZones.Count > 0)
        {
            Debug.Log("[MapManager] Zones already exist, skipping creation");
            return;
        }
        CreateDefaultZonesLocal();
    }

    /// <summary>
    /// Find all MapZone components in the scene and register them
    /// </summary>
    public void FindAllZonesInScene()
    {
        MapZone[] zones = FindObjectsByType<MapZone>(FindObjectsInactive.Include, FindObjectsSortMode.None); // true = include inactive
        Debug.Log($"[MapManager] Found {zones.Length} MapZone objects in scene");

        foreach (var zone in zones)
        {
            if (!allZones.Contains(zone))
            {
                allZones.Add(zone);
                Debug.Log($"[MapManager] Registered zone: {zone.zoneName} ({zone.zoneType})");
            }
        }
    }

    /// <summary>
    /// Clear all zones and re-find them. Use this when scene changes or for clients joining.
    /// </summary>
    public void RefreshZones()
    {
        Debug.Log("[MapManager] Refreshing zones...");

        // Remove null/destroyed zone references
        allZones.RemoveAll(z => z == null);

        // Clear and re-find all zones
        allZones.Clear();
        FindAllZonesInScene();

        Debug.Log($"[MapManager] Refresh complete: {allZones.Count} zones found");
    }

    /// <summary>
    /// Register a zone with the manager
    /// </summary>
    public void RegisterZone(MapZone zone)
    {
        if (zone == null) return;

        if (!allZones.Contains(zone))
        {
            allZones.Add(zone);
            Debug.Log($"[MapManager] Registered zone: {zone.zoneName} ({zone.zoneType})");
        }
    }

    /// <summary>
    /// Unregister a zone from the manager
    /// </summary>
    public void UnregisterZone(MapZone zone)
    {
        if (zone == null) return;

        if (allZones.Contains(zone))
        {
            allZones.Remove(zone);
            Debug.Log($"[MapManager] Unregistered zone: {zone.zoneName}");
        }
    }

    /// <summary>
    /// Get a zone by its type
    /// </summary>
    public MapZone GetZoneByType(MapZone.ZoneType type)
    {
        foreach (var zone in allZones)
        {
            if (zone != null && zone.zoneType == type)
            {
                return zone;
            }
        }
        return null;
    }

    /// <summary>
    /// Get all zones of a specific type
    /// </summary>
    public List<MapZone> GetZonesByType(MapZone.ZoneType type)
    {
        List<MapZone> result = new List<MapZone>();
        foreach (var zone in allZones)
        {
            if (zone != null && zone.zoneType == type)
            {
                result.Add(zone);
            }
        }
        return result;
    }

    /// <summary>
    /// Get a random zone
    /// </summary>
    public MapZone GetRandomZone()
    {
        if (allZones.Count == 0) return null;
        return allZones[Random.Range(0, allZones.Count)];
    }

    /// <summary>
    /// Get the zone containing a specific position
    /// </summary>
    public MapZone GetZoneAtPosition(Vector3 position)
    {
        foreach (var zone in allZones)
        {
            if (zone != null && zone.ContainsPosition(position))
            {
                return zone;
            }
        }
        return null;
    }

    /// <summary>
    /// Check if a position is within any zone
    /// </summary>
    public bool IsPositionInAnyZone(Vector3 position)
    {
        return GetZoneAtPosition(position) != null;
    }

    /// <summary>
    /// Get all NPC spawn points across all zones
    /// </summary>
    public List<Transform> GetAllNPCSpawnPoints()
    {
        List<Transform> points = new List<Transform>();
        foreach (var zone in allZones)
        {
            if (zone != null && zone.npcSpawnPoints != null)
            {
                points.AddRange(zone.npcSpawnPoints);
            }
        }
        return points;
    }

    /// <summary>
    /// Get all defense zone spawn points that meet distance requirements
    /// </summary>
    public List<Transform> GetValidDefenseZoneSpawnPoints(Vector3 astronautPosition)
    {
        List<Transform> validPoints = new List<Transform>();

        foreach (var zone in allZones)
        {
            if (zone == null || zone.defenseZoneSpawnPoints == null) continue;

            foreach (var point in zone.defenseZoneSpawnPoints)
            {
                if (point == null) continue;

                float distance = Vector3.Distance(point.position, astronautPosition);
                if (distance >= minDefenseZoneDistance)
                {
                    validPoints.Add(point);
                }
            }
        }

        return validPoints;
    }

    /// <summary>
    /// Get all interactable spawn points across all zones
    /// </summary>
    public List<Transform> GetAllInteractableSpawnPoints()
    {
        List<Transform> points = new List<Transform>();
        foreach (var zone in allZones)
        {
            if (zone != null && zone.interactableSpawnPoints != null)
            {
                points.AddRange(zone.interactableSpawnPoints);
            }
        }
        return points;
    }

    /// <summary>
    /// Get spawn points for a specific zone
    /// </summary>
    public List<Transform> GetInteractableSpawnPointsForZone(MapZone zone)
    {
        if (zone == null || zone.interactableSpawnPoints == null)
        {
            return new List<Transform>();
        }
        return new List<Transform>(zone.interactableSpawnPoints);
    }

    /// <summary>
    /// Shuffle a list using Fisher-Yates algorithm
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
    /// Clear all registered zones (useful for scene reload)
    /// </summary>
    public void ClearZones()
    {
        allZones.Clear();
        Debug.Log("[MapManager] All zones cleared");
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
