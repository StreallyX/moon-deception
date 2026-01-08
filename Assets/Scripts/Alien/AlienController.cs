using UnityEngine;

/// <summary>
/// Third-person controller for the Alien player.
/// WASD movement with TPS camera that follows behind.
/// A/D = STRAFE (move sideways), ONLY mouse rotates camera/character facing.
/// </summary>
public class AlienController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    
    [Header("TPS Camera")]
    public Camera alienCamera;
    public Vector3 cameraOffset = new Vector3(0f, 2f, -4f);
    public float cameraSensitivity = 2f;
    public float minPitch = -30f;
    public float maxPitch = 60f;
    
    [Header("References")]
    public HungerSystem hungerSystem;
    
    private CharacterController characterController;
    private Transform cameraTransform;
    private float yaw = 0f;
    private float pitch = 20f;
    private float verticalVelocity = 0f;
    private float gravity = -19.62f;
    
    public static AlienController ActiveAlien { get; private set; }
    public static bool IsAlienControlled => ActiveAlien != null && ActiveAlien.enabled;
    
    private bool isControlled = false;
    
    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = gameObject.AddComponent<CharacterController>();
        }
        
        // FORCE CharacterController settings to prevent falling through ground
        characterController.skinWidth = 0.08f;
        characterController.stepOffset = 0.3f;
        characterController.minMoveDistance = 0.001f;
        characterController.height = 2f;
        characterController.radius = 0.5f;
        characterController.center = new Vector3(0, 1f, 0);
    }
    
    void Start()
    {
        // Find alien camera if not assigned
        if (alienCamera == null)
        {
            // Look for a camera tagged or named for alien
            Camera[] cameras = FindObjectsOfType<Camera>(true);
            foreach (Camera cam in cameras)
            {
                if (cam.name.ToLower().Contains("alien"))
                {
                    alienCamera = cam;
                    break;
                }
            }
            // Fallback to main camera if no alien-specific camera
            if (alienCamera == null)
            {
                alienCamera = Camera.main;
            }
        }
        
        if (alienCamera != null)
        {
            cameraTransform = alienCamera.transform;
        }
        
        if (hungerSystem == null)
        {
            hungerSystem = GetComponent<HungerSystem>();
        }
        
        yaw = transform.eulerAngles.y;
        
        Debug.Log("[AlienController] Initialized");
    }
    
    void OnEnable()
    {
        ActiveAlien = this;
        isControlled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Enable alien camera when alien is controlled
        if (alienCamera != null)
        {
            alienCamera.gameObject.SetActive(true);
        }
        
        Debug.Log("[AlienController] Enabled - Alien is now controlled");
    }
    
    void OnDisable()
    {
        if (ActiveAlien == this)
        {
            ActiveAlien = null;
        }
        isControlled = false;
        
        // Disable alien camera when not controlled
        if (alienCamera != null)
        {
            alienCamera.gameObject.SetActive(false);
        }
        
        Debug.Log("[AlienController] Disabled - Alien control released");
    }
    
    void Update()
    {
        if (!isControlled) return;
        
        HandleCameraRotation();
        HandleMovement();
        UpdateCameraPosition();
    }
    
    /// <summary>
    /// ONLY mouse controls camera/character rotation.
    /// A/D keys do NOT affect rotation - only strafe movement.
    /// </summary>
    void HandleCameraRotation()
    {
        float mouseX = Input.GetAxis("Mouse X") * cameraSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * cameraSensitivity;
        
        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        
        // Rotate alien to face camera yaw direction (mouse controlled only)
        transform.rotation = Quaternion.Euler(0, yaw, 0);
    }
    
    /// <summary>
    /// WASD movement relative to camera direction.
    /// A/D = STRAFE (move sideways), W/S = forward/backward.
    /// Character always faces camera direction (set by mouse).
    /// </summary>
    void HandleMovement()
    {
        float horizontal = Input.GetAxisRaw("Horizontal"); // A/D = strafe
        float vertical = Input.GetAxisRaw("Vertical");     // W/S = forward/back
        
        // Movement relative to where character is facing (which is camera yaw)
        Vector3 moveDirection = transform.forward * vertical + transform.right * horizontal;
        moveDirection = moveDirection.normalized;
        
        // Ground check and gravity
        if (!characterController.isGrounded)
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
        else
        {
            verticalVelocity = -2f; // Small downward force to stay grounded
        }
        
        // Apply movement
        Vector3 velocity = moveDirection * moveSpeed + Vector3.up * verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
    }
    
    void UpdateCameraPosition()
    {
        if (cameraTransform == null) return;
        
        // Calculate camera position based on yaw and pitch (orbiting behind character)
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 targetPosition = transform.position + Vector3.up * 1.5f + rotation * new Vector3(0, 0, cameraOffset.z);
        
        // Smooth camera follow
        cameraTransform.position = Vector3.Lerp(cameraTransform.position, targetPosition, 10f * Time.deltaTime);
        cameraTransform.LookAt(transform.position + Vector3.up * 1.5f);
    }
    
    public void EnableControl()
    {
        enabled = true;
    }
    
    public void DisableControl()
    {
        enabled = false;
    }
    
    public Camera GetCamera()
    {
        return alienCamera;
    }

    public void Transform()
    {
        Debug.Log("[AlienController] Alien transforming!");
    }
}
