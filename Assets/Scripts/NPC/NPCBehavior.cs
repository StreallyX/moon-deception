using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Basic AI behavior for civilian NPCs on the moon base.
/// NPCs patrol between waypoints and perform idle activities.
/// </summary>
public class NPCBehavior : MonoBehaviour, IDamageable
{
    public enum NPCState
    {
        Idle,
        Walking,
        Working,
        Panicking,
        Dead
    }

    [Header("NPC Settings")]
    [SerializeField] private string npcName = "Crew Member";
    [SerializeField] private float health = 100f;
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float runSpeed = 5f;

    [Header("Patrol")]
    [SerializeField] private List<Transform> waypoints = new List<Transform>();
    [SerializeField] private float waypointReachDistance = 0.5f;
    [SerializeField] private float idleTimeMin = 2f;
    [SerializeField] private float idleTimeMax = 5f;

    [Header("State")]
    [SerializeField] private NPCState currentState = NPCState.Idle;
    
    [Header("Behavior Flags")]
    [SerializeField] private bool isAlien = false; // Hidden flag, can be set by GameManager

    // Internal state
    private int currentWaypointIndex = 0;
    private float idleTimer = 0f;
    private float stateTimer = 0f;
    private Vector3 startPosition;
    private Animator animator;

    public NPCState CurrentState => currentState;
    public bool IsAlien => isAlien;
    public string Name => npcName;

    void Start()
    {
        startPosition = transform.position;
        animator = GetComponent<Animator>();
        
        // Start with random idle time
        idleTimer = Random.Range(idleTimeMin, idleTimeMax);
        SetState(NPCState.Idle);
    }

    void Update()
    {
        if (currentState == NPCState.Dead) return;

        stateTimer += Time.deltaTime;

        switch (currentState)
        {
            case NPCState.Idle:
                HandleIdleState();
                break;
            case NPCState.Walking:
                HandleWalkingState();
                break;
            case NPCState.Working:
                HandleWorkingState();
                break;
            case NPCState.Panicking:
                HandlePanickingState();
                break;
        }
    }

    /// <summary>
    /// Handle idle behavior - wait then start walking
    /// </summary>
    private void HandleIdleState()
    {
        if (stateTimer >= idleTimer)
        {
            if (waypoints.Count > 0)
            {
                SetState(NPCState.Walking);
            }
            else
            {
                // No waypoints, transition to working
                SetState(NPCState.Working);
            }
        }
    }

    /// <summary>
    /// Handle walking to waypoints
    /// </summary>
    private void HandleWalkingState()
    {
        if (waypoints.Count == 0)
        {
            SetState(NPCState.Idle);
            return;
        }

        Transform targetWaypoint = waypoints[currentWaypointIndex];
        Vector3 direction = (targetWaypoint.position - transform.position).normalized;
        direction.y = 0; // Keep on ground plane

        // Move towards waypoint
        transform.position += direction * walkSpeed * Time.deltaTime;

        // Face movement direction
        if (direction.magnitude > 0.1f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direction),
                10f * Time.deltaTime
            );
        }

        // Check if reached waypoint
        float distance = Vector3.Distance(transform.position, targetWaypoint.position);
        if (distance <= waypointReachDistance)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Count;
            idleTimer = Random.Range(idleTimeMin, idleTimeMax);
            SetState(NPCState.Idle);
        }
    }

    /// <summary>
    /// Handle working behavior (interact with environment)
    /// </summary>
    private void HandleWorkingState()
    {
        // Simulate working animation duration
        if (stateTimer >= 5f)
        {
            idleTimer = Random.Range(idleTimeMin, idleTimeMax);
            SetState(NPCState.Idle);
        }
    }

    /// <summary>
    /// Handle panic behavior (run away from threats)
    /// </summary>
    private void HandlePanickingState()
    {
        // Run in a random direction
        if (stateTimer < 3f)
        {
            Vector3 randomDir = new Vector3(
                Random.Range(-1f, 1f),
                0f,
                Random.Range(-1f, 1f)
            ).normalized;

            transform.position += randomDir * runSpeed * Time.deltaTime;
        }
        else
        {
            SetState(NPCState.Idle);
        }
    }

    /// <summary>
    /// Set NPC state
    /// </summary>
    private void SetState(NPCState newState)
    {
        currentState = newState;
        stateTimer = 0f;

        // Trigger animation if available
        if (animator != null)
        {
            animator.SetInteger("State", (int)newState);
        }
    }

    /// <summary>
    /// Trigger panic response
    /// </summary>
    public void Panic()
    {
        if (currentState != NPCState.Dead)
        {
            SetState(NPCState.Panicking);
        }
    }

    /// <summary>
    /// IDamageable implementation
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (currentState == NPCState.Dead) return;

        health -= amount;
        Debug.Log($"[NPC] {npcName} took {amount} damage. Health: {health}");

        if (health <= 0)
        {
            Die();
        }
        else
        {
            Panic();
        }
    }

    /// <summary>
    /// Handle NPC death
    /// </summary>
    private void Die()
    {
        SetState(NPCState.Dead);
        Debug.Log($"[NPC] {npcName} died! IsAlien: {isAlien}");

        // Notify GameManager
        var gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnNPCKilled(this);
        }

        // Disable collider and start death sequence
        var collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        // Could add death animation, ragdoll, etc.
        Destroy(gameObject, 5f); // Remove after 5 seconds
    }

    /// <summary>
    /// Mark this NPC as an alien (called by GameManager during setup)
    /// </summary>
    public void SetAsAlien(bool alien)
    {
        isAlien = alien;
    }

    /// <summary>
    /// Add waypoint for patrol route
    /// </summary>
    public void AddWaypoint(Transform waypoint)
    {
        waypoints.Add(waypoint);
    }

    /// <summary>
    /// Debug visualization
    /// </summary>
    void OnDrawGizmosSelected()
    {
        // Draw waypoint path
        if (waypoints.Count > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] != null)
                {
                    Gizmos.DrawWireSphere(waypoints[i].position, 0.3f);
                    if (i < waypoints.Count - 1 && waypoints[i + 1] != null)
                    {
                        Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
                    }
                }
            }
        }
    }
}
