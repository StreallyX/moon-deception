using UnityEngine;

/// <summary>
/// Third-person controller for the Alien player.
/// WASD movement with TPS camera that follows behind.
/// </summary>
public class AlienController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    
    [Header("TPS Camera")]
    public Transform cameraTransform;
    public Vector3 cameraOffset = new Vector3(0f, 2f, -4f);
    public float cameraSensitivity = 2f;
    public float minPitch = -30f;
    public float maxPitch = 60f;
    
    [Header("References")]
    public HungerSystem hungerSystem;
    
    private CharacterController characterController;
    private float yaw = 0f;
    private float pitch = 20f;
    private float verticalVelocity = 0f;
    private float gravity = -19.62f;
    
    // Static reference to track active controlled character
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
        characterController.height = 2f;
        characterController.radius = 0.5f;
        characterController.center = new Vector3(0, 1f, 0);
    }
    
    void Start()
    {
        if (cameraTransform == null)
        {
            cameraTransform = Camera.main?.transform;
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
        Debug.Log("[AlienController] Enabled - Alien is now controlled");
    }
    
    void OnDisable()
    {
        if (ActiveAlien == this)
        {
            ActiveAlien = null;
        }
        isControlled = false;
        Debug.Log("[AlienController] Disabled - Alien control released");
    }
    
    void Update()
    {
        // Only process input if this is the active controlled character
        if (!isControlled) return;
        
        HandleCameraRotation();
        HandleMovement();
        UpdateCameraPosition();
    }
    
    void HandleCameraRotation()
    {
        float mouseX = Input.GetAxis("Mouse X") * cameraSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * cameraSensitivity;
        
        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }
    
    void HandleMovement()
    {
        // Get input
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        
        // Calculate movement direction relative to camera yaw
        Vector3 forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        Vector3 right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
        Vector3 moveDirection = (forward * vertical + right * horizontal).normalized;
        
        // Rotate alien to face movement direction
        if (moveDirection.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        
        // Ground check with small distance tolerance
        bool isGrounded = characterController.isGrounded;
        
        // Apply gravity only when not grounded
        if (isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f; // Small downward force to keep grounded
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
        
        // Move
        Vector3 velocity = moveDirection * moveSpeed + Vector3.up * verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
    }
    
    void UpdateCameraPosition()
    {
        if (cameraTransform == null) return;
        
        // Calculate camera position based on yaw and pitch
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 targetPosition = transform.position + Vector3.up * 1.5f + rotation * new Vector3(0, 0, cameraOffset.z);
        
        // Smooth camera follow
        cameraTransform.position = Vector3.Lerp(cameraTransform.position, targetPosition, 10f * Time.deltaTime);
        cameraTransform.LookAt(transform.position + Vector3.up * 1.5f);
    }
    
    /// <summary>
    /// Enable control of this alien
    /// </summary>
    public void EnableControl()
    {
        enabled = true;
    }
    
    /// <summary>
    /// Disable control of this alien
    /// </summary>
    public void DisableControl()
    {
        enabled = false;
    }

    public void Transform()
    {
        Debug.Log("[AlienController] Alien transforming!");
    }
}
