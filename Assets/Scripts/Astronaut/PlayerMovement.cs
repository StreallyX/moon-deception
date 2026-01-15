using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.5f;

    [Header("Mouse Look Settings")]
    public float mouseSensitivity = 2f;
    public Transform cameraTransform;

    [Header("Camera Reference")]
    public Camera playerCamera;

    [Header("Footsteps")]
    public float footstepInterval = 0.4f;
    public string surfaceType = "Metal";

    [Header("Debug")]
    public bool showDebugLogs = false;

    private CharacterController controller;
    private Vector3 velocity;
    private float xRotation = 0f;
    private float footstepTimer = 0f;
    private bool wasMoving = false;

    public static PlayerMovement ActivePlayer { get; private set; }
    public static bool IsPlayerControlled => ActivePlayer != null && ActivePlayer.enabled;

    private bool isControlled = false;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        controller.skinWidth = 0.08f;
        controller.stepOffset = 0.3f;
        controller.minMoveDistance = 0.001f;
        controller.height = 2f;
        controller.radius = 0.5f;
        controller.center = new Vector3(0, 0, 0);
    }


    void Start()
    {
        // Auto-find camera if not assigned
        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                cameraTransform = cam.transform;
                playerCamera = cam;
            }
            else if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
                playerCamera = Camera.main;
            }
        }
        else if (playerCamera == null)
        {
            playerCamera = cameraTransform.GetComponent<Camera>();
        }
        
        velocity = Vector3.zero;
        
        // Force Unity to resolve collision on first frame
        controller.Move(Vector3.down * 0.5f);
        
        Debug.Log($"[PlayerMovement] Initialized. Camera: {cameraTransform?.name}");
    }
    
    void OnEnable()
    {
        ActivePlayer = this;
        isControlled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Enable player camera when player is controlled
        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(true);
        }

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.SetPlayerType(PlayerType.Astronaut);
        }

        Debug.Log("[PlayerMovement] Enabled - Player is now controlled");
    }

    void OnDisable()
    {
        if (ActivePlayer == this)
        {
            ActivePlayer = null;
        }
        isControlled = false;

        // Disable player camera when not controlled
        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(false);
        }

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.SetPlayerType(PlayerType.None);
        }

        Debug.Log("[PlayerMovement] Disabled - Player control released");
    }

    void Update()
    {
        if (!isControlled) return;
        
        // === FIXED: Move() called EVERY frame to keep isGrounded updated ===
        
        // 1. Handle mouse look
        HandleMouseLook();
        
        // 2. Get input (can be zero)
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        
        // 3. Jump check FIRST (before gravity reset)
        if (Input.GetButtonDown("Jump") && controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            Debug.Log("JUMP triggered!");
        }
        
        // 4. Gravity logic
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }
        
        // 5. Calculate horizontal movement (can be zero)
        Vector3 move = transform.right * h + transform.forward * v;

        // 6. ALWAYS call Move() - even if move is zero!
        controller.Move(move * walkSpeed * Time.deltaTime);

        // 7. ALWAYS apply vertical velocity
        controller.Move(new Vector3(0, velocity.y, 0) * Time.deltaTime);

        // 8. Footstep sounds
        bool isMoving = move.magnitude > 0.1f && controller.isGrounded;
        if (isMoving)
        {
            footstepTimer += Time.deltaTime;
            if (footstepTimer >= footstepInterval)
            {
                PlayFootstep();
                footstepTimer = 0f;
            }
        }
        else
        {
            footstepTimer = footstepInterval * 0.5f; // Ready for next step
        }
        wasMoving = isMoving;
    }

    void PlayFootstep()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFootstep(surfaceType);
        }
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
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
        return playerCamera;
    }
}
