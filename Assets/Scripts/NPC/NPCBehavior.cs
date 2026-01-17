using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

/// <summary>
/// Basic AI behavior for civilian NPCs on the moon base.
/// NPCs patrol between waypoints and perform idle activities.
/// Works without NavMesh - uses simple movement.
/// SERVER-AUTHORITATIVE: AI runs on server, positions sync via NetworkTransform.
/// </summary>
public class NPCBehavior : NetworkBehaviour, IDamageable
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

    [Header("Unpredictable Behaviors")]
    [SerializeField] private float quirkChance = 0.02f; // 2% chance per frame to do something weird
    [SerializeField] private float minTimeBetweenQuirks = 3f;
    [SerializeField] private float maxTimeBetweenQuirks = 10f;
    private float nextQuirkTime = 0f;
    private bool isDoingQuirk = false;
    private float quirkTimer = 0f;
    private QuirkType currentQuirk = QuirkType.None;
    private float quirkTargetRotation = 0f;
    private bool isRunning = false;
    private float runTimer = 0f;
    private Vector2 moveInput;
    private float inputChangeTimer = 0f;


    public enum QuirkType
    {
        None,
        Spin360,
        Turn180,
        RandomTurn,
        Jump,
        SuddenStop,
        LookAround,
        SprintBurst
    }

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
    public bool IsDead => currentState == NPCState.Dead;
    public bool IsAlien => isAlien;
    public string Name => npcName;
    public MapZone AssignedZone => assignedZone;

    void Start()
    {
        startPosition = transform.position;
        animator = GetComponent<Animator>();

        // Get or create CharacterController
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = gameObject.AddComponent<CharacterController>();
        }

        // FORCE CharacterController settings to prevent wall climbing/phasing
        characterController.center = new Vector3(0, 1f, 0);
        characterController.height = 2f;
        characterController.radius = 0.4f;
        characterController.stepOffset = 0.1f;      // Very low - prevents climbing walls
        characterController.slopeLimit = 30f;       // Can't climb steep slopes
        characterController.skinWidth = 0.02f;      // Collision skin
        characterController.minMoveDistance = 0f;   // Always process movement

        // Add a CapsuleCollider for physical collisions (CharacterControllers don't collide with each other)
        // This prevents players from walking through NPCs
        // NO Rigidbody needed - CharacterController will collide with static colliders
        /*
        CapsuleCollider physicsCollider = GetComponent<CapsuleCollider>();
        if (physicsCollider == null)
        {
            physicsCollider = gameObject.AddComponent<CapsuleCollider>();
            physicsCollider.center = new Vector3(0, 1f, 0);
            physicsCollider.height = 2f;
            physicsCollider.radius = 0.4f;
        }*/

        idleTimer = Random.Range(idleTimeMin, idleTimeMax);
        SetState(NPCState.Idle);

        // Initialize quirk timing with random offset so NPCs don't all quirk at once
        nextQuirkTime = Time.time + Random.Range(minTimeBetweenQuirks, maxTimeBetweenQuirks);

        Debug.Log($"[NPC] {npcName} initialized at {startPosition}. Waypoints: {waypoints.Count}, AutoPatrol: {autoPatrol}");
    }

    void Update()
    {
        // Only server runs AI logic - clients receive synced position via NetworkTransform
        // Also check IsSpawned to avoid errors during initialization
        if (!IsSpawned || !IsServer) return;

        if (currentState == NPCState.Dead) return;

        stateTimer += Time.deltaTime;

        // Apply gravity
        ApplyGravity();

        // Handle ongoing quirks
        if (isDoingQuirk)
        {
            HandleOngoingQuirk();
            return; // Don't do normal behavior while quirking
        }

        // Check for random quirks (only while walking or idle)
        if ((currentState == NPCState.Walking || currentState == NPCState.Idle) && Time.time >= nextQuirkTime)
        {
            TryStartQuirk();
        }

        // Update run timer
        if (isRunning)
        {
            runTimer -= Time.deltaTime;
            if (runTimer <= 0f)
            {
                isRunning = false;
            }
        }

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

    private float verticalVelocity = 0f;
    private const float gravity = -20f;

    private void ApplyGravity()
    {
        if (characterController == null) return;

        // Proper gravity using CharacterController
        if (characterController.isGrounded)
        {
            if (verticalVelocity < 0f)
                verticalVelocity = -1f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

    }

    private void MoveNPC(Vector3 horizontal)
    {
        // 1️⃣ MOVE HORIZONTAL (murs)
        CollisionFlags sideFlags = characterController.Move(horizontal);

        if ((sideFlags & CollisionFlags.Sides) != 0)
        {
            // Mur touché → stop + nouvelle direction
            isRunning = false;
            autoPatrolTarget = transform.position;
            PickNewDirection();
        }

        // 2️⃣ MOVE VERTICAL (sol / plafond)
        Vector3 verticalMove = Vector3.up * verticalVelocity * Time.deltaTime;
        CollisionFlags verticalFlags = characterController.Move(verticalMove);

        // Tête contre plafond → stop montée
        if ((verticalFlags & CollisionFlags.Above) != 0)
        {
            verticalVelocity = 0f;
        }

        // Pieds au sol → reset gravité
        if ((verticalFlags & CollisionFlags.Below) != 0)
        {
            if (verticalVelocity < 0f)
                verticalVelocity = -1f;
        }
    }


    private void PickNewDirection()
    {
        Vector3 randomDir = new Vector3(
            Random.Range(-1f, 1f),
            0f,
            Random.Range(-1f, 1f)
        ).normalized;

        autoPatrolTarget = transform.position + randomDir * patrolRadius;
        hasAutoTarget = true;
    }


    // ==================== UNPREDICTABLE BEHAVIORS ====================
    private void UpdateRandomInput()
    {
        inputChangeTimer -= Time.deltaTime;

        if (inputChangeTimer <= 0f)
        {
            moveInput = Random.insideUnitCircle.normalized;
            inputChangeTimer = Random.Range(1.5f, 4f);
        }
    }

    private void MoveLikePlayer()
    {
        Vector3 move =
            transform.forward * moveInput.y +
            transform.right * moveInput.x;

        float speed = isRunning ? runSpeed : walkSpeed;

        characterController.SimpleMove(move * speed);
    }


    private void TryStartQuirk()
    {
        // Random chance to do a quirk
        if (Random.value > quirkChance * 10f) // Boost chance when timer triggers
        {
            // Schedule next check
            nextQuirkTime = Time.time + Random.Range(minTimeBetweenQuirks, maxTimeBetweenQuirks);
            return;
        }

        // Pick a random quirk
        QuirkType[] quirks = new QuirkType[]
        {
            QuirkType.Spin360,
            QuirkType.Turn180,
            QuirkType.RandomTurn,
            QuirkType.Jump,
            QuirkType.SuddenStop,
            QuirkType.LookAround,
            QuirkType.SprintBurst
        };

        currentQuirk = quirks[Random.Range(0, quirks.Length)];
        isDoingQuirk = true;
        quirkTimer = 0f;

        // Initialize quirk-specific data
        switch (currentQuirk)
        {
            case QuirkType.Spin360:
                quirkTargetRotation = transform.eulerAngles.y + 360f;
                break;
            case QuirkType.Turn180:
                quirkTargetRotation = transform.eulerAngles.y + 180f;
                break;
            case QuirkType.RandomTurn:
                quirkTargetRotation = transform.eulerAngles.y + Random.Range(45f, 270f) * (Random.value > 0.5f ? 1f : -1f);
                break;
            case QuirkType.SprintBurst:
                isRunning = true;
                runTimer = Random.Range(1f, 3f);
                isDoingQuirk = false; // Sprint continues during normal walking
                break;
        }

        // Schedule next quirk
        nextQuirkTime = Time.time + Random.Range(minTimeBetweenQuirks, maxTimeBetweenQuirks);

        //Debug.Log($"[NPC] {npcName} doing quirk: {currentQuirk}");
    }

    private void HandleOngoingQuirk()
    {
        quirkTimer += Time.deltaTime;

        switch (currentQuirk)
        {
            case QuirkType.Spin360:
            case QuirkType.Turn180:
            case QuirkType.RandomTurn:
                HandleRotationQuirk();
                break;
            case QuirkType.Jump:
                HandleJumpQuirk();
                break;
            case QuirkType.SuddenStop:
                HandleSuddenStopQuirk();
                break;
            case QuirkType.LookAround:
                HandleLookAroundQuirk();
                break;
        }
    }

    private void HandleRotationQuirk()
    {
        float rotationSpeed = currentQuirk == QuirkType.Spin360 ? 400f : 250f;
        float currentY = transform.eulerAngles.y;
        float newY = Mathf.MoveTowardsAngle(currentY, quirkTargetRotation, rotationSpeed * Time.deltaTime);
        transform.eulerAngles = new Vector3(0f, newY, 0f);

        // Check if rotation complete
        if (Mathf.Abs(Mathf.DeltaAngle(newY, quirkTargetRotation)) < 5f)
        {
            EndQuirk();
        }

        // Timeout safety
        if (quirkTimer > 3f)
        {
            EndQuirk();
        }
    }

    private void HandleJumpQuirk()
    {
        if (characterController.isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(1.5f * -2f * gravity);
        }
        EndQuirk();
    }

    private void HandleSuddenStopQuirk()
    {
        // Just stand still for a moment
        if (quirkTimer > Random.Range(0.5f, 1.5f))
        {
            EndQuirk();
        }
    }

    private void HandleLookAroundQuirk()
    {
        float lookDuration = 2f;

        if (quirkTimer < lookDuration)
        {
            // Look left and right
            float lookAngle = Mathf.Sin(quirkTimer * 4f) * 45f;
            transform.eulerAngles = new Vector3(0f, transform.eulerAngles.y + lookAngle * Time.deltaTime * 2f, 0f);
        }
        else
        {
            EndQuirk();
        }
    }

    private void EndQuirk()
    {
        isDoingQuirk = false;
        currentQuirk = QuirkType.None;
        quirkTimer = 0f;
    }

    // ==================== STATE HANDLERS ====================

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

            // Move NPC - use run speed if sprinting
            float currentSpeed = isRunning ? runSpeed : walkSpeed;
            float moveDistance = currentSpeed * Time.deltaTime;

            // Check for obstacles before moving
            

            if (characterController != null)
            {
                MoveNPC(direction * moveDistance);
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

            // Check for obstacles before moving (prevents passing through walls)
            float moveDistance = runSpeed * Time.deltaTime;


            if (characterController != null)
            {
                MoveNPC(runDir * moveDistance);
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
        // Must be spawned to use RPCs
        if (!IsSpawned)
        {
            // Not networked or not spawned yet - apply directly (single player mode)
            ApplyPanic();
            return;
        }

        // Server-authoritative panic - clients request via RPC
        if (!IsServer)
        {
            PanicServerRpc();
            return;
        }

        ApplyPanic();
    }

    [ServerRpc(RequireOwnership = false)]
    private void PanicServerRpc()
    {
        ApplyPanic();
    }

    private void ApplyPanic()
    {
        if (currentState != NPCState.Dead)
        {
            SetState(NPCState.Panicking);

            // Play panic sound (networked)
            if (NetworkAudioManager.Instance != null)
            {
                NetworkAudioManager.Instance.PlayNPCPanic(transform.position);
            }
            else if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayNPCPanic(transform.position);
            }
        }
    }

    public void TakeDamage(float amount)
    {
        // Must be spawned to use RPCs
        if (!IsSpawned)
        {
            // Not networked or not spawned yet - apply damage directly (single player mode)
            ApplyDamage(amount);
            return;
        }

        // Server-authoritative damage - clients request damage via RPC
        if (!IsServer)
        {
            // Client hit detection - request server to apply damage
            TakeDamageServerRpc(amount);
            return;
        }

        // Server processes damage
        ApplyDamage(amount);
    }

    [ServerRpc(RequireOwnership = false)]
    private void TakeDamageServerRpc(float amount)
    {
        ApplyDamage(amount);
    }

    private void ApplyDamage(float amount)
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

        // Spawn blood decal (networked)
        if (NetworkAudioManager.Instance != null)
        {
            NetworkAudioManager.Instance.SpawnBloodDecal(transform.position);
        }
        else if (BloodDecalManager.Instance != null)
        {
            BloodDecalManager.Instance.SpawnBloodDecal(transform.position);
        }

        // Play death sound (networked)
        if (NetworkAudioManager.Instance != null)
        {
            NetworkAudioManager.Instance.PlayNPCDeath(transform.position);
        }
        else if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayNPCDeath(transform.position);
        }

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

        // Handle destruction based on network state - IMMEDIATE despawn
        if (!IsSpawned)
        {
            // Single player or not networked - destroy immediately
            Destroy(gameObject);
        }
        else if (IsServer)
        {
            // Only server can destroy NetworkObjects - despawn immediately
            NetworkObject.Despawn();
        }
        // Client does nothing - server will despawn and sync to all clients
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
