using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Defines a map zone with boundaries, spawn points, and patrol waypoints.
/// Place this on an empty GameObject with a BoxCollider to define zone boundaries.
/// </summary>
public class MapZone : MonoBehaviour
{
    public enum ZoneType
    {
        Habitat,
        Research,
        Industrial,
        Command
    }

    [Header("Zone Settings")]
    public ZoneType zoneType;
    public string zoneName = "Zone";
    public Color zoneColor = Color.cyan;

    [Header("Spawn Points")]
    public Transform[] npcSpawnPoints;
    public Transform[] defenseZoneSpawnPoints;
    public Transform[] interactableSpawnPoints;

    [Header("Patrol")]
    public Transform[] patrolWaypoints;

    [Header("Visual (Editor Only)")]
    public bool showBounds = true;
    public bool showSpawnPoints = true;

    private BoxCollider zoneBounds;

    public Bounds Bounds => zoneBounds != null ? zoneBounds.bounds : new Bounds(transform.position, Vector3.one * 10f);

    void Awake()
    {
        // Auto-create BoxCollider if missing
        zoneBounds = GetComponent<BoxCollider>();
        if (zoneBounds == null)
        {
            zoneBounds = gameObject.AddComponent<BoxCollider>();
            zoneBounds.size = new Vector3(20f, 10f, 20f);
            zoneBounds.center = new Vector3(0f, 5f, 0f);
        }
        zoneBounds.isTrigger = true;

        // Auto-set zone name if default
        if (zoneName == "Zone")
        {
            zoneName = zoneType.ToString();
        }
    }

    void Start()
    {
        // Register with MapManager
        if (MapManager.Instance != null)
        {
            MapManager.Instance.RegisterZone(this);
        }
        else
        {
            StartCoroutine(LateRegister());
        }

        Debug.Log($"[MapZone] {zoneName} initialized. NPCs: {npcSpawnPoints?.Length ?? 0}, Defense: {defenseZoneSpawnPoints?.Length ?? 0}, Interactables: {interactableSpawnPoints?.Length ?? 0}");
    }

    System.Collections.IEnumerator LateRegister()
    {
        yield return null;
        yield return null;

        if (MapManager.Instance != null)
        {
            MapManager.Instance.RegisterZone(this);
        }
        else
        {
            Debug.LogWarning($"[MapZone] {zoneName} could not register with MapManager");
        }
    }

    /// <summary>
    /// Check if a position is within this zone's bounds
    /// </summary>
    public bool ContainsPosition(Vector3 position)
    {
        if (zoneBounds == null) return false;
        return zoneBounds.bounds.Contains(position);
    }

    /// <summary>
    /// Get a random NPC spawn point in this zone
    /// </summary>
    public Transform GetRandomNPCSpawnPoint()
    {
        if (npcSpawnPoints == null || npcSpawnPoints.Length == 0) return null;
        return npcSpawnPoints[Random.Range(0, npcSpawnPoints.Length)];
    }

    /// <summary>
    /// Get a random defense zone spawn point
    /// </summary>
    public Transform GetRandomDefenseSpawnPoint()
    {
        if (defenseZoneSpawnPoints == null || defenseZoneSpawnPoints.Length == 0) return null;
        return defenseZoneSpawnPoints[Random.Range(0, defenseZoneSpawnPoints.Length)];
    }

    /// <summary>
    /// Get a random interactable spawn point
    /// </summary>
    public Transform GetRandomInteractableSpawnPoint()
    {
        if (interactableSpawnPoints == null || interactableSpawnPoints.Length == 0) return null;
        return interactableSpawnPoints[Random.Range(0, interactableSpawnPoints.Length)];
    }

    /// <summary>
    /// Get all patrol waypoints for NPCs in this zone
    /// </summary>
    public List<Transform> GetPatrolWaypoints()
    {
        return patrolWaypoints != null ? new List<Transform>(patrolWaypoints) : new List<Transform>();
    }

    // ==================== GIZMOS ====================

    void OnDrawGizmos()
    {
        if (!showBounds) return;

        // Draw zone bounds
        Gizmos.color = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 0.2f);

        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(col.center, col.size);
            Gizmos.color = zoneColor;
            Gizmos.DrawWireCube(col.center, col.size);
        }
        else
        {
            Gizmos.DrawCube(transform.position + Vector3.up * 5f, new Vector3(20f, 10f, 20f));
            Gizmos.color = zoneColor;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 5f, new Vector3(20f, 10f, 20f));
        }
    }

    void OnDrawGizmosSelected()
    {
        // Draw zone name
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 12f, $"[{zoneType}] {zoneName}", new GUIStyle()
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = zoneColor }
        });
        #endif

        if (!showSpawnPoints) return;

        // Draw NPC spawn points
        if (npcSpawnPoints != null)
        {
            Gizmos.color = Color.blue;
            foreach (var point in npcSpawnPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawWireSphere(point.position, 0.5f);
                    Gizmos.DrawLine(point.position, point.position + Vector3.up * 2f);
                }
            }
        }

        // Draw defense spawn points
        if (defenseZoneSpawnPoints != null)
        {
            Gizmos.color = Color.green;
            foreach (var point in defenseZoneSpawnPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawWireSphere(point.position, 1f);
                    Gizmos.DrawCube(point.position, new Vector3(2f, 0.1f, 2f));
                }
            }
        }

        // Draw interactable spawn points
        if (interactableSpawnPoints != null)
        {
            Gizmos.color = Color.yellow;
            foreach (var point in interactableSpawnPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawWireCube(point.position, new Vector3(1f, 1f, 1f));
                }
            }
        }

        // Draw patrol waypoints
        if (patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < patrolWaypoints.Length; i++)
            {
                if (patrolWaypoints[i] != null)
                {
                    Gizmos.DrawWireSphere(patrolWaypoints[i].position, 0.3f);

                    // Draw line to next waypoint
                    int nextIndex = (i + 1) % patrolWaypoints.Length;
                    if (patrolWaypoints[nextIndex] != null)
                    {
                        Gizmos.DrawLine(patrolWaypoints[i].position, patrolWaypoints[nextIndex].position);
                    }
                }
            }
        }
    }
}
