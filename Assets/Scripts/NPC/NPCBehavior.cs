using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Basic AI behavior for civilian NPCs on the moon base.
/// NPCs patrol between waypoints and perform idle activities.
/// Works without NavMesh - uses simple movement.
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
    [SerializeField] private float waypointReachDistance = 1f;
    [SerializeField] private float idleTimeMin = 2f;
    [SerializeField] private float idleTimeMax = 5f;
    
    [Header("Auto Patrol (if no waypoints)")]
    [SerializeField] private bool autoPatrol = true;
    [SerializeField] private float patrolRadius = 5f;
    private Vector3 autoPatrolTarget;
    private bool hasAutoTarget = false;

    [Header("State")]
    [SerializeField] private NPCState currentState = NPCState.Idle;
    
    [Header("Behavior Flags")]
    [SerializeField] private bool isAlien = false;

    [Header("Zone")]
    [SerializeField] private MapZone assignedZone;

    private int currentWaypointIndex = 0;
    private float idleTimer = 0f;
    private float stateTimer = 0f;
    private Vector3 startPosition;
    private Animator animator;
    private CharacterController characterController;

    public NPCState CurrentState => currentState;
    public bool IsAlien => isAlien;
    public string Name => npcName;
    public MapZone AssignedZone => assignedZone;

    void Start()
    {
        startPosition = transform.position;
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
        
        idleTimer = Random.Range(idleTimeMin, idleTimeMax);
        SetState(NPCState.Idle);
        
        Debug.Log($"[NPC] {npcName} initialized at {startPosition}. Waypoints: {waypoints.Count}, AutoPatrol: {autoPatrol}");
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

    private void HandleIdleState()
    {
        if (stateTimer >= idleTimer)
        {
            if (waypoints.Count > 0)
            {
                SetState(NPCState.Walking);
            }
            else if (autoPatrol)
            {
                // Generate random patrol target
                GenerateAutoPatrolTarget();
                SetState(NPCState.Walking);
            }
            else
            {
                SetState(NPCState.Working);
            }
        }
    }

    private void GenerateAutoPatrolTarget()
    {
        Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;
        autoPatrolTarget = startPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);
        hasAutoTarget = true;
    }

    private void HandleWalkingState()
    {
        Vector3 targetPosition;
        
        if (waypoints.Count > 0)
        {
            if (waypoints[currentWaypointIndex] == null)
            {
                SetState(NPCState.Idle);
                return;
            }
            targetPosition = waypoints[currentWaypointIndex].position;
        }
        else if (autoPatrol && hasAutoTarget)
        {
            targetPosition = autoPatrolTarget;
        }
        else
        {
            SetState(NPCState.Idle);
            return;
        }

        // Calculate direction (ignore Y for flat movement)
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0;
        
        float distance = direction.magnitude;
        
        if (distance > waypointReachDistance)
        {
            direction.Normalize();
            
            // Move NPC
            Vector3 movement = direction * walkSpeed * Time.deltaTime;
            
            if (characterController != null)
            {
                characterController.Move(movement);
            }
            else
            {
                transform.position += movement;
            }
            
            // Rotate towards target
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 5f * Time.deltaTime);
            }
        }
        else
        {
            // Reached destination
            if (waypoints.Count > 0)
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Count;
            }
            else
            {
                hasAutoTarget = false;
            }
            
            idleTimer = Random.Range(idleTimeMin, idleTimeMax);
            SetState(NPCState.Idle);
        }
    }

    private void HandleWorkingState()
    {
        if (stateTimer >= 5f)
        {
            idleTimer = Random.Range(idleTimeMin, idleTimeMax);
            SetState(NPCState.Idle);
        }
    }

    private void HandlePanickingState()
    {
        if (stateTimer < 3f)
        {
            // Run away from start position
            Vector3 runDir = (transform.position - startPosition).normalized;
            if (runDir.magnitude < 0.1f)
            {
                runDir = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
            }

            Vector3 movement = runDir * runSpeed * Time.deltaTime;
            
            if (characterController != null)
            {
                characterController.Move(movement);
            }
            else
            {
                transform.position += movement;
            }
        }
        else
        {
            SetState(NPCState.Idle);
        }
    }

    private void SetState(NPCState newState)
    {
        if (currentState != newState)
        {
            //Debug.Log($"[NPC] {npcName} state: {currentState} -> {newState}");
        }
        currentState = newState;
        stateTimer = 0f;

        if (animator != null)
        {
            animator.SetInteger("State", (int)newState);
        }
    }

    public void Panic()
    {
        if (currentState != NPCState.Dead)
        {
            SetState(NPCState.Panicking);
        }
    }

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

    private void Die()
    {
        SetState(NPCState.Dead);
        Debug.Log($"[NPC] {npcName} died! IsAlien: {isAlien}");

        var gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnNPCKilled(this);
        }

        var collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        Destroy(gameObject, 5f);
    }

    public void SetAsAlien(bool alien)
    {
        isAlien = alien;
    }

    public void AddWaypoint(Transform waypoint)
    {
        waypoints.Add(waypoint);
    }

    /// <summary>
    /// Assign this NPC to a zone and use its patrol waypoints
    /// </summary>
    public void SetZone(MapZone zone)
    {
        assignedZone = zone;

        if (zone != null && zone.patrolWaypoints != null && zone.patrolWaypoints.Length > 0)
        {
            waypoints.Clear();
            foreach (var wp in zone.patrolWaypoints)
            {
                if (wp != null)
                {
                    waypoints.Add(wp);
                }
            }

            // Update patrol center to zone center
            startPosition = zone.transform.position;

            Debug.Log($"[NPC] {npcName} assigned to zone '{zone.zoneName}' with {waypoints.Count} waypoints");
        }
    }

    /// <summary>
    /// Clear zone assignment
    /// </summary>
    public void ClearZone()
    {
        assignedZone = null;
    }

    void OnDrawGizmosSelected()
    {
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
        else if (autoPatrol)
        {
            // Show patrol radius
            Gizmos.color = Color.yellow;
            Vector3 center = Application.isPlaying ? startPosition : transform.position;
            Gizmos.DrawWireSphere(center, patrolRadius);
        }
    }
}
