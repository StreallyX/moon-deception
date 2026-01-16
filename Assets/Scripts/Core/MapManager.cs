using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Central manager for all map zones.
/// Handles zone registration, queries, and spawn point management.
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
        // Auto-find all zones in the scene if none registered yet
        StartCoroutine(AutoFindZones());
    }

    System.Collections.IEnumerator AutoFindZones()
    {
        // Wait for zones to register themselves
        yield return new WaitForSeconds(0.5f);

        // If no zones registered, find them manually
        if (allZones.Count == 0)
        {
            Debug.Log("[MapManager] No zones registered, searching scene...");
            FindAllZonesInScene();
        }

        // If still no zones, create default zones
        if (allZones.Count == 0)
        {
            Debug.Log("[MapManager] No zones found - creating default zones...");
            CreateDefaultZones();
        }

        Debug.Log($"[MapManager] Total zones registered: {allZones.Count}");
        foreach (var zone in allZones)
        {
            if (zone != null)
            {
                Debug.Log($"  - {zone.zoneName} ({zone.zoneType})");
            }
        }
    }

    /// <summary>
    /// Create default zones if none exist in the scene
    /// </summary>
    public void CreateDefaultZones()
    {
        Debug.Log("[MapManager] Creating default zones...");

        // Create 4 zones in a grid pattern
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

            // Create spawn points as child objects
            zone.npcSpawnPoints = new Transform[5];
            for (int j = 0; j < 5; j++)
            {
                GameObject spawnPoint = new GameObject($"NPCSpawn_{j + 1}");
                spawnPoint.transform.SetParent(zoneObj.transform);
                float offset = (j - 2f) * 4f;
                spawnPoint.transform.localPosition = new Vector3(offset, 1f, 0f);
                zone.npcSpawnPoints[j] = spawnPoint.transform;
            }

            // Create patrol waypoints
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

    /// <summary>
    /// Find all MapZone components in the scene and register them
    /// </summary>
    public void FindAllZonesInScene()
    {
        MapZone[] zones = FindObjectsOfType<MapZone>(true); // true = include inactive
        Debug.Log($"[MapManager] Found {zones.Length} zones in scene");

        foreach (var zone in zones)
        {
            if (!allZones.Contains(zone))
            {
                allZones.Add(zone);
                Debug.Log($"[MapManager] Auto-registered zone: {zone.zoneName} ({zone.zoneType})");
            }
        }
    }

    /// <summary>
    /// Clear all zones and re-find them. Use this when scene changes or for clients joining.
    /// </summary>
    public void RefreshZones()
    {
        Debug.Log("[MapManager] Refreshing zones - clearing stale references...");

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
