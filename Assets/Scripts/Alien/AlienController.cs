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
    
    [Header("Gravity")]
    public float gravity = -9.81f;
    public float jumpHeight = 1.5f;
    
    [Header("TPS Camera")]
    public Camera alienCamera;
    public Vector3 cameraOffset = new Vector3(0f, 2f, -4f);
    public float cameraSensitivity = 2f;
    public float minPitch = -30f;
    public float maxPitch = 60f;
    
    [Header("References")]
    public HungerSystem hungerSystem;
    
    [Header("Debug")]
    public bool showDebugLogs = true;
    
    private CharacterController characterController;
    private Transform cameraTransform;
    private float yaw = 0f;
    private float pitch = 20f;
    private Vector3 velocity;
    
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
        characterController.center = new Vector3(0, 0, 0); // Center at chest height for proper raycast hits

        // Ensure AlienHealth exists for damage system
        if (GetComponent<AlienHealth>() == null)
        {
            gameObject.AddComponent<AlienHealth>();
            Debug.Log("[AlienController] Added AlienHealth component");
        }
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
        velocity = Vector3.zero;
        
        // Force Unity to resolve collision on first frame
        characterController.Move(Vector3.down * 0.5f);
        
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

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.SetPlayerType(PlayerType.Alien);
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

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.SetPlayerType(PlayerType.None);
        }

        Debug.Log("[AlienController] Disabled - Alien control released");
    }
    
    void Update()
    {
        if (!isControlled) return;
        
        // === FIXED: Move() called EVERY frame to keep isGrounded updated ===
        
        // 1. Handle camera rotation (mouse only)
        HandleCameraRotation();
        
        // 2. Get input (can be zero)
        float h = Input.GetAxisRaw("Horizontal"); // A/D = strafe
        float v = Input.GetAxisRaw("Vertical");   // W/S = forward/back
        
        // 3. Jump check FIRST (before gravity reset)
        if (Input.GetButtonDown("Jump") && characterController.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            Debug.Log("JUMP triggered!");
        }
        
        // 4. Gravity logic
        if (characterController.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }
        
        // 5. Calculate horizontal movement (can be zero)
        Vector3 move = transform.forward * v + transform.right * h;
        move = move.normalized;
        
        // 6. ALWAYS call Move() - even if move is zero!
        characterController.Move(move * moveSpeed * Time.deltaTime);
        
        // 7. ALWAYS apply vertical velocity
        characterController.Move(new Vector3(0, velocity.y, 0) * Time.deltaTime);
        
        // 8. Update camera position
        UpdateCameraPosition();
        
        // Debug logging
        if (showDebugLogs && Time.frameCount % 60 == 0)
        {
            //Debug.Log($"[Alien] isGrounded={characterController.isGrounded}, velocity.y={velocity.y:F2}, pos.y={transform.position.y:F2}");
        }
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
