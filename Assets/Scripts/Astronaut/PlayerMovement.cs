using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 2f;      // Same as NPC walk speed
    public float runSpeed = 5f;       // Sprint speed when holding Shift
    public float gravity = -9.81f;
    public float jumpHeight = 1.5f;

    private bool isRunning = false;

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
    private Renderer[] modelRenderers; // Pour cacher le modèle en FPS

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        controller.skinWidth = 0.08f;
        controller.stepOffset = 0.3f;
        controller.minMoveDistance = 0.001f;
        controller.height = 2f;
        controller.radius = 0.5f;
        controller.center = new Vector3(0, 1, 0); // Center at y=1 for height=2 capsule (feet at 0, head at 2)

    }


    void Start()
    {
        // Force walk/run speeds (override any serialized values)
        walkSpeed = 2f;
        runSpeed = 5f;

        // Auto-find camera if not assigned
        FindCamera();

        velocity = Vector3.zero;

        // Force Unity to resolve collision on first frame
        controller.Move(Vector3.down * 0.5f);

        // Cache all renderers for hiding in FPS mode
        modelRenderers = GetComponentsInChildren<Renderer>(true);

        Debug.Log($"[PlayerMovement] Initialized. Camera: {cameraTransform?.name}");
    }
    
    void OnEnable()
    {
        ActivePlayer = this;
        isControlled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Find camera if not found yet
        FindCamera();

        // STEP 1: Disable ALL other cameras and AudioListeners FIRST
        Debug.Log("[PlayerMovement] Disabling all other cameras...");
        Camera[] allCameras = FindObjectsOfType<Camera>(true);
        foreach (Camera cam in allCameras)
        {
            if (playerCamera == null || cam != playerCamera)
            {
                cam.gameObject.SetActive(false);
                Debug.Log($"[PlayerMovement] Disabled camera: {cam.gameObject.name}");
            }
        }

        AudioListener[] allListeners = FindObjectsOfType<AudioListener>(true);
        foreach (var listener in allListeners)
        {
            listener.enabled = false;
        }

        // STEP 2: Enable player camera when player is controlled
        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(true);
            playerCamera.enabled = true;

            // Ensure our camera has AudioListener and it's enabled
            AudioListener ourListener = playerCamera.GetComponent<AudioListener>();
            if (ourListener == null)
            {
                ourListener = playerCamera.gameObject.AddComponent<AudioListener>();
            }
            ourListener.enabled = true;

            Debug.Log($"[PlayerMovement] Enabled camera: {playerCamera.gameObject.name}");
        }

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.SetPlayerType(PlayerType.Astronaut);
        }

        // Hide own model for FPS view (player shouldn't see their own body)
        SetModelVisible(false);

        Debug.Log("[PlayerMovement] Enabled - Player is now controlled");
    }

    /// <summary>
    /// Show/hide the player's 3D model (for FPS view)
    /// </summary>
    private void SetModelVisible(bool visible)
    {
        if (modelRenderers == null)
        {
            modelRenderers = GetComponentsInChildren<Renderer>(true);
        }

        foreach (var renderer in modelRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }
        Debug.Log($"[PlayerMovement] Model visibility set to: {visible}");
    }

    void FindCamera()
    {
        // First try to find camera as child of this object (prefab camera)
        if (cameraTransform == null || playerCamera == null)
        {
            Camera cam = GetComponentInChildren<Camera>(true); // true = include inactive
            if (cam != null)
            {
                cameraTransform = cam.transform;
                playerCamera = cam;
                Debug.Log($"[PlayerMovement] Found child camera: {cam.gameObject.name}");
            }
        }

        // Fallback: main camera
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
            playerCamera = Camera.main;
            Debug.Log("[PlayerMovement] Using main camera as fallback");
        }
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

        // Show model again (for other players to see in multiplayer)
        SetModelVisible(true);

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
        
        // 5. Check for sprint (Shift key)
        isRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float currentSpeed = isRunning ? runSpeed : walkSpeed;

        // 6. Calculate horizontal movement (can be zero)
        Vector3 move = transform.right * h + transform.forward * v;

        // 7. ALWAYS call Move() - even if move is zero!
        controller.Move(move * currentSpeed * Time.deltaTime);

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

    void OnGUI()
    {
        if (!isControlled) return;

        // Sprint hint - bottom left
        GUIStyle hintStyle = new GUIStyle(GUI.skin.label);
        hintStyle.fontSize = 16;
        hintStyle.fontStyle = FontStyle.Bold;
        hintStyle.normal.textColor = isRunning ? Color.yellow : Color.white;

        string sprintText = isRunning ? "[SHIFT] COURSE" : "[SHIFT] Courir";
        GUI.Label(new Rect(20, Screen.height - 40, 200, 30), sprintText, hintStyle);
    }
}
