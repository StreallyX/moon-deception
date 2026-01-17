using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Defines a map zone with boundaries, spawn points, and patrol waypoints.
/// Supports both BoxCollider (square/rectangle) and SphereCollider (round) zones.
/// Place this on an empty GameObject with a collider to define zone boundaries.
/// </summary>
public class MapZone : MonoBehaviour
{
    public enum ZoneType
    {
        Habitat,
        Research,
        Industrial,
        Command,
        Tree
    }

    public enum ZoneShape
    {
        Box,
        Sphere
    }

    [Header("Zone Settings")]
    public ZoneType zoneType;
    public string zoneName = "Zone";
    public Color zoneColor = Color.cyan;

    [Header("Zone Shape")]
    public ZoneShape shape = ZoneShape.Box;
    public float sphereRadius = 15f;  // Used when shape is Sphere

    [Header("Spawn Points")]
    public Transform[] npcSpawnPoints;
    public Transform[] defenseZoneSpawnPoints;
    public Transform[] interactableSpawnPoints;

    [Header("Patrol")]
    public Transform[] patrolWaypoints;

    [Header("Visual (Editor Only)")]
    public bool showBounds = true;
    public bool showSpawnPoints = true;

    private BoxCollider boxBounds;
    private SphereCollider sphereBounds;

    public Bounds Bounds
    {
        get
        {
            if (boxBounds != null) return boxBounds.bounds;
            if (sphereBounds != null) return sphereBounds.bounds;
            return new Bounds(transform.position, Vector3.one * 10f);
        }
    }

    void Awake()
    {
        // Check for existing colliders first
        boxBounds = GetComponent<BoxCollider>();
        sphereBounds = GetComponent<SphereCollider>();

        // Auto-detect shape from existing collider
        if (sphereBounds != null)
        {
            shape = ZoneShape.Sphere;
            sphereRadius = sphereBounds.radius;
        }
        else if (boxBounds != null)
        {
            shape = ZoneShape.Box;
        }
        else
        {
            // No collider exists - create based on shape setting
            if (shape == ZoneShape.Sphere)
            {
                sphereBounds = gameObject.AddComponent<SphereCollider>();
                sphereBounds.radius = sphereRadius;
                sphereBounds.center = new Vector3(0f, 5f, 0f);
            }
            else
            {
                boxBounds = gameObject.AddComponent<BoxCollider>();
                boxBounds.size = new Vector3(20f, 10f, 20f);
                boxBounds.center = new Vector3(0f, 5f, 0f);
            }
        }

        // Set as trigger
        if (boxBounds != null) boxBounds.isTrigger = true;
        if (sphereBounds != null) sphereBounds.isTrigger = true;

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
        if (shape == ZoneShape.Sphere || sphereBounds != null)
        {
            // For sphere, check horizontal distance (ignore Y for flat circular zones)
            Vector3 center = transform.position + (sphereBounds != null ? sphereBounds.center : Vector3.up * 5f);
            float radius = sphereBounds != null ? sphereBounds.radius : sphereRadius;

            // 2D distance (X-Z plane) for circular ground zone
            float horizontalDist = Vector2.Distance(
                new Vector2(position.x, position.z),
                new Vector2(center.x, center.z)
            );
            return horizontalDist <= radius;
        }
        else if (boxBounds != null)
        {
            return boxBounds.bounds.Contains(position);
        }

        return false;
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

        // Draw zone bounds based on shape
        Gizmos.color = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 0.2f);

        SphereCollider sphereCol = GetComponent<SphereCollider>();
        BoxCollider boxCol = GetComponent<BoxCollider>();

        if (sphereCol != null || shape == ZoneShape.Sphere)
        {
            // Draw sphere/circle zone
            Vector3 center = transform.position + (sphereCol != null ? sphereCol.center : Vector3.up * 5f);
            float radius = sphereCol != null ? sphereCol.radius : sphereRadius;

            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawSphere(center, radius);
            Gizmos.color = zoneColor;
            Gizmos.DrawWireSphere(center, radius);

            // Draw flat circle on ground for clarity
            Gizmos.color = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 0.5f);
            DrawCircleGizmo(new Vector3(center.x, 0.1f, center.z), radius, 32);
        }
        else if (boxCol != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(boxCol.center, boxCol.size);
            Gizmos.color = zoneColor;
            Gizmos.DrawWireCube(boxCol.center, boxCol.size);
        }
        else
        {
            // Default box
            Gizmos.DrawCube(transform.position + Vector3.up * 5f, new Vector3(20f, 10f, 20f));
            Gizmos.color = zoneColor;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 5f, new Vector3(20f, 10f, 20f));
        }
    }

    /// <summary>
    /// Draw a circle gizmo on the ground (Y=0)
    /// </summary>
    void DrawCircleGizmo(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
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
