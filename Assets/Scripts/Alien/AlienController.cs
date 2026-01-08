using UnityEngine;

/// <summary>
/// Controls the alien player in third-person perspective.
/// Handles disguise mechanics, movement, and transformation.
/// </summary>
public class AlienController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float runSpeed = 7f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float gravity = -9.81f;

    [Header("TPS Camera Settings")]
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private float cameraDistance = 5f;
    [SerializeField] private float cameraHeight = 2f;
    [SerializeField] private float cameraSensitivity = 2f;
    [SerializeField] private float minVerticalAngle = -30f;
    [SerializeField] private float maxVerticalAngle = 60f;

    [Header("Disguise")]
    [SerializeField] private GameObject disguiseModel;
    [SerializeField] private GameObject alienModel;
    [SerializeField] private bool isDisguised = true;

    [Header("References")]
    [SerializeField] private HungerSystem hungerSystem;
    [SerializeField] private CharacterController characterController;

    // State
    private Vector3 velocity;
    private float horizontalAngle = 0f;
    private float verticalAngle = 20f;
    private bool isTransformed = false;
    private Camera mainCamera;

    public bool IsDisguised => isDisguised;
    public bool IsTransformed => isTransformed;

    void Start()
    {
        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        mainCamera = Camera.main;
        
        if (hungerSystem == null)
            hungerSystem = GetComponent<HungerSystem>();

        SetDisguise(true);
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        if (!isTransformed)
        {
            HandleTPSCamera();
            HandleMovement();
            HandleAbilities();
        }
        else
        {
            HandleTransformedState();
        }

        ApplyGravity();
    }

    /// <summary>
    /// Handles third-person camera orbit around player
    /// </summary>
    private void HandleTPSCamera()
    {
        float mouseX = Input.GetAxis("Mouse X") * cameraSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * cameraSensitivity;

        horizontalAngle += mouseX;
        verticalAngle -= mouseY;
        verticalAngle = Mathf.Clamp(verticalAngle, minVerticalAngle, maxVerticalAngle);

        if (mainCamera != null)
        {
            Vector3 targetPosition = transform.position + Vector3.up * cameraHeight;
            Quaternion rotation = Quaternion.Euler(verticalAngle, horizontalAngle, 0f);
            Vector3 offset = rotation * new Vector3(0f, 0f, -cameraDistance);

            mainCamera.transform.position = targetPosition + offset;
            mainCamera.transform.LookAt(targetPosition);
        }
    }

    /// <summary>
    /// Handles WASD movement relative to camera direction
    /// </summary>
    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 forward = mainCamera.transform.forward;
        Vector3 right = mainCamera.transform.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = (forward * vertical + right * horizontal).normalized;

        if (moveDirection.magnitude > 0.1f)
        {
            // Rotate character to face movement direction
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            // Apply movement
            float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
            characterController.Move(moveDirection * currentSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// Handles alien abilities when disguised
    /// </summary>
    private void HandleAbilities()
    {
        // E - Interact / Eat
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryInteract();
        }

        // Q - Create chaos
        if (Input.GetKeyDown(KeyCode.Q))
        {
            CreateChaos();
        }

        // F - Drink coffee
        if (Input.GetKeyDown(KeyCode.F))
        {
            TryDrinkCoffee();
        }
    }

    /// <summary>
    /// Handles movement/attack in transformed state
    /// </summary>
    private void HandleTransformedState()
    {
        // Faster, more aggressive movement when transformed
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 move = transform.right * horizontal + transform.forward * vertical;
        characterController.Move(move * runSpeed * 1.5f * Time.deltaTime);

        // Attack on click
        if (Input.GetButtonDown("Fire1"))
        {
            PerformAttack();
        }
    }

    /// <summary>
    /// Apply gravity to character
    /// </summary>
    private void ApplyGravity()
    {
        if (characterController.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }

    /// <summary>
    /// Toggle or set disguise state
    /// </summary>
    public void SetDisguise(bool disguised)
    {
        isDisguised = disguised;

        if (disguiseModel != null)
            disguiseModel.SetActive(disguised);

        if (alienModel != null)
            alienModel.SetActive(!disguised);

        Debug.Log($"[AlienController] Disguise: {(disguised ? "ON" : "OFF")}");
    }

    /// <summary>
    /// Called when stress maxes and aliens transform
    /// </summary>
    public void Transform()
    {
        isTransformed = true;
        SetDisguise(false);
        Debug.Log("[AlienController] TRANSFORMED! Hunt mode activated!");
    }

    /// <summary>
    /// Attempt to interact with nearby objects or eat target
    /// </summary>
    private void TryInteract()
    {
        Collider[] nearby = Physics.OverlapSphere(transform.position, 2f);
        foreach (var col in nearby)
        {
            // Check for astronaut to eat
            if (col.CompareTag("Player"))
            {
                EatTarget(col.gameObject);
                return;
            }

            // Check for interactable objects
            var interactable = col.GetComponent<IInteractable>();
            if (interactable != null)
            {
                interactable.Interact(gameObject);
                return;
            }
        }
    }

    /// <summary>
    /// Eat a target to satisfy hunger (leaves blood trace)
    /// </summary>
    private void EatTarget(GameObject target)
    {
        if (hungerSystem != null)
        {
            hungerSystem.Eat();
        }

        // Leave blood evidence
        SpawnBloodTrace(target.transform.position);

        Debug.Log($"[AlienController] Ate target: {target.name}");
    }

    /// <summary>
    /// Spawn blood evidence at location
    /// </summary>
    private void SpawnBloodTrace(Vector3 position)
    {
        // TODO: Instantiate blood prefab
        Debug.Log($"[AlienController] Blood trace left at {position}");
    }

    /// <summary>
    /// Try to drink coffee to manage hunger
    /// </summary>
    private void TryDrinkCoffee()
    {
        Collider[] nearby = Physics.OverlapSphere(transform.position, 2f);
        foreach (var col in nearby)
        {
            if (col.CompareTag("CoffeeMachine"))
            {
                if (hungerSystem != null)
                {
                    hungerSystem.DrinkCoffee();
                }
                Debug.Log("[AlienController] Drank coffee");
                return;
            }
        }
    }

    /// <summary>
    /// Create a chaos event to stress the astronaut
    /// </summary>
    private void CreateChaos()
    {
        // TODO: Implement chaos events (collision, noise, bugs, wind)
        Debug.Log("[AlienController] Creating chaos event!");

        // Notify GameManager of chaos event
        var gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnChaosEventTriggered(transform.position);
        }
    }

    /// <summary>
    /// Attack in transformed state
    /// </summary>
    private void PerformAttack()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward, 1.5f);
        foreach (var hit in hits)
        {
            var damageable = hit.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(50f);
                Debug.Log($"[AlienController] Attacked {hit.name}!");
            }
        }
    }
}

/// <summary>
/// Interface for interactable objects in the world
/// </summary>
public interface IInteractable
{
    void Interact(GameObject interactor);
}
