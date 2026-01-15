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
