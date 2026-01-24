using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Handles player-to-player collision using OverlapSphere detection.
/// CharacterControllers don't collide with each other by default, so we
/// manually detect nearby players and push them apart each FixedUpdate.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerCollision : MonoBehaviour
{
    [Header("Collision Settings")]
    public float detectionRadius = 0.6f;   // Slightly larger than CharacterController radius
    public float pushStrength = 3f;        // Push force
    public float minDistance = 0.8f;       // Minimum allowed distance between players

    private CharacterController characterController;

    // Static list of all players for efficient collision checking
    private static List<PlayerCollision> allPlayers = new List<PlayerCollision>();

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    void OnEnable()
    {
        if (!allPlayers.Contains(this))
        {
            allPlayers.Add(this);
        }
        Debug.Log($"[PlayerCollision] Registered {gameObject.name}. Total players: {allPlayers.Count}");
    }

    void OnDisable()
    {
        allPlayers.Remove(this);
        Debug.Log($"[PlayerCollision] Unregistered {gameObject.name}. Total players: {allPlayers.Count}");
    }

    void FixedUpdate()
    {
        if (characterController == null || !characterController.enabled) return;

        // Check distance to all other players
        Vector3 totalPush = Vector3.zero;

        foreach (var other in allPlayers)
        {
            if (other == null || other == this) continue;
            if (other.characterController == null || !other.characterController.enabled) continue;

            // Calculate horizontal distance only
            Vector3 toOther = other.transform.position - transform.position;
            toOther.y = 0;
            float distance = toOther.magnitude;

            // If too close, calculate push direction
            if (distance < minDistance && distance > 0.01f)
            {
                // Push strength increases as players get closer
                float pushAmount = (minDistance - distance) / minDistance;
                Vector3 pushDir = -toOther.normalized;
                totalPush += pushDir * pushAmount * pushStrength * Time.fixedDeltaTime;
            }
        }

        // Apply push if needed
        if (totalPush.sqrMagnitude > 0.0001f)
        {
            characterController.Move(totalPush);
        }
    }

    // Also check NPCs using the same logic
    void Update()
    {
        if (characterController == null || !characterController.enabled) return;

        // Check for nearby NPCs and players using OverlapSphere (backup detection)
        Collider[] nearby = Physics.OverlapSphere(transform.position + characterController.center, detectionRadius);

        foreach (var col in nearby)
        {
            if (col.gameObject == gameObject) continue;

            // Check if it's an NPC or another player
            bool isNPC = col.GetComponent<NPCBehavior>() != null || col.GetComponentInParent<NPCBehavior>() != null;
            bool isPlayer = col.GetComponent<PlayerCollision>() != null || col.GetComponentInParent<PlayerCollision>() != null;

            if (!isNPC && !isPlayer) continue;

            // Get the root transform of the other entity
            Transform otherRoot = isNPC ?
                (col.GetComponentInParent<NPCBehavior>()?.transform ?? col.transform) :
                (col.GetComponentInParent<PlayerCollision>()?.transform ?? col.transform);

            if (otherRoot == transform) continue;

            Vector3 toOther = otherRoot.position - transform.position;
            toOther.y = 0;
            float distance = toOther.magnitude;

            if (distance < minDistance && distance > 0.01f)
            {
                float pushAmount = (minDistance - distance) / minDistance;
                Vector3 pushDir = -toOther.normalized;
                characterController.Move(pushDir * pushAmount * pushStrength * Time.deltaTime * 0.5f);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        var cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            // Detection radius
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + cc.center, detectionRadius);

            // Minimum distance
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position + cc.center, minDistance * 0.5f);
        }
    }
}
