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
        characterController.center = new Vector3(0, 1, 0); // Centered at y=1 for height=2 capsule (feet at 0, head at 2)

        // Ensure AlienHealth exists for damage system
        if (GetComponent<AlienHealth>() == null)
        {
            gameObject.AddComponent<AlienHealth>();
            Debug.Log("[AlienController] Added AlienHealth component");
        }
    }

    
    void Start()
    {
        FindCamera();

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

        // CRITICAL: Ensure CharacterController exists (Awake might not have run yet on network spawn)
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
            if (characterController == null)
            {
                characterController = gameObject.AddComponent<CharacterController>();
                Debug.Log("[AlienController] Created CharacterController in OnEnable (Awake didn't run)");
            }

            // Configure CharacterController
            characterController.skinWidth = 0.08f;
            characterController.stepOffset = 0.3f;
            characterController.minMoveDistance = 0.001f;
            characterController.height = 2f;
            characterController.radius = 0.5f;
            characterController.center = new Vector3(0, 1, 0);
        }

        // Find camera if not found yet
        FindCamera();

        // Enable alien camera when alien is controlled
        // CRITICAL: Only use cameras that are children of this object or specifically for the alien
        // If we ended up with Camera.main that belongs to another player, create our own!
        bool needsNewCamera = (alienCamera == null);

        if (alienCamera != null && !alienCamera.transform.IsChildOf(transform))
        {
            // This camera doesn't belong to us - might be Astronaut's camera
            Debug.LogWarning($"[AlienController] Found camera {alienCamera.gameObject.name} but it's NOT a child of Alien - creating our own!");
            needsNewCamera = true;
        }

        // 🔥 STEP 1: Disable ALL cameras and AudioListeners FIRST
        Debug.Log("[AlienController] Disabling all other cameras...");
        Camera[] allCameras = FindObjectsOfType<Camera>(true);
        foreach (Camera cam in allCameras)
        {
            if (cam.gameObject != gameObject && (alienCamera == null || cam != alienCamera))
            {
                cam.gameObject.SetActive(false);
                Debug.Log($"[AlienController] Disabled camera GameObject: {cam.gameObject.name}");
            }
        }

        AudioListener[] allListeners = FindObjectsOfType<AudioListener>(true);
        foreach (var listener in allListeners)
        {
            listener.enabled = false;
        }

        // 🔥 STEP 2: Create camera if needed
        if (needsNewCamera)
        {
            Debug.Log("[AlienController] Creating runtime camera for Alien...");
            GameObject camObj = new GameObject("AlienCamera_Runtime");
            camObj.transform.SetParent(null);
            camObj.tag = "MainCamera"; // Tag as main camera!

            alienCamera = camObj.AddComponent<Camera>();
            alienCamera.depth = 10; // High depth to render on top
            alienCamera.nearClipPlane = 0.1f;
            alienCamera.farClipPlane = 1000f;
            alienCamera.clearFlags = CameraClearFlags.Skybox;

            camObj.AddComponent<AudioListener>();
            cameraTransform = alienCamera.transform;

            Debug.Log($"[AlienController] Created runtime camera: {camObj.name}");
        }

        // 🔥 STEP 3: Activate alien camera
        if (alienCamera != null)
        {
            alienCamera.gameObject.SetActive(true);
            alienCamera.enabled = true;
            alienCamera.gameObject.tag = "MainCamera";
            cameraTransform = alienCamera.transform;

            // Enable AudioListener on our camera
            AudioListener ourListener = alienCamera.GetComponent<AudioListener>();
            if (ourListener == null)
            {
                ourListener = alienCamera.gameObject.AddComponent<AudioListener>();
            }
            ourListener.enabled = true;

            Debug.Log($"[AlienController] ✅ ALIEN CAMERA ACTIVE: {alienCamera.gameObject.name}");
        }
        else
        {
            Debug.LogError("[AlienController] ❌ FAILED TO CREATE/FIND CAMERA!");
        }

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.SetPlayerType(PlayerType.Alien);

            // Check if we're in chaos mode and update UI
            var transformation = GetComponent<AlienTransformation>();
            if (transformation != null && transformation.IsTransformed)
            {
                GameUIManager.Instance.SetChaosMode(true);

                // Update HP bar with current values
                var alienHealth = GetComponent<AlienHealth>();
                if (alienHealth != null)
                {
                    GameUIManager.Instance.UpdateAlienHealthBar(alienHealth.currentHealth, alienHealth.maxHealth);
                }
            }
        }

        // Initialize yaw from current rotation
        yaw = transform.eulerAngles.y;

        Debug.Log("[AlienController] Enabled - Alien is now controlled");

        // Debug: Check critical components
        Debug.Log($"[AlienController] CharacterController: {(characterController != null ? "OK" : "NULL!")}");
        Debug.Log($"[AlienController] Camera: {(alienCamera != null ? alienCamera.gameObject.name : "NULL!")}");
        Debug.Log($"[AlienController] CameraTransform: {(cameraTransform != null ? "OK" : "NULL!")}");
        Debug.Log($"[AlienController] isControlled: {isControlled}");
    }

    void FindCamera()
    {
        Debug.Log($"[AlienController] FindCamera called. Current alienCamera={(alienCamera != null ? alienCamera.gameObject.name : "NULL")}");

        // First try to find camera as child of this object (prefab camera)
        if (alienCamera == null)
        {
            alienCamera = GetComponentInChildren<Camera>(true); // true = include inactive
            if (alienCamera != null)
            {
                Debug.Log($"[AlienController] Found CHILD camera: {alienCamera.gameObject.name}, active={alienCamera.gameObject.activeInHierarchy}");
            }
            else
            {
                Debug.Log("[AlienController] No child camera found on Alien prefab!");
            }
        }

        // Fallback: Look for a camera tagged or named for alien in scene
        if (alienCamera == null)
        {
            Camera[] cameras = FindObjectsOfType<Camera>(true);
            Debug.Log($"[AlienController] Searching {cameras.Length} cameras in scene...");
            foreach (Camera cam in cameras)
            {
                Debug.Log($"[AlienController]   - Camera: {cam.gameObject.name}, active={cam.gameObject.activeInHierarchy}");
                if (cam.name.ToLower().Contains("alien"))
                {
                    alienCamera = cam;
                    Debug.Log($"[AlienController] Found scene camera with 'alien' in name: {cam.gameObject.name}");
                    break;
                }
            }
        }

        // Last fallback: main camera (WARNING: this might be the Astronaut's camera!)
        if (alienCamera == null)
        {
            alienCamera = Camera.main;
            if (alienCamera != null)
            {
                Debug.LogWarning($"[AlienController] Using Camera.main as FALLBACK: {alienCamera.gameObject.name} - THIS MAY BE WRONG!");
            }
        }

        // Set camera transform reference
        if (alienCamera != null)
        {
            cameraTransform = alienCamera.transform;
            Debug.Log($"[AlienController] cameraTransform set to: {cameraTransform.gameObject.name}");
        }
        else
        {
            Debug.LogError("[AlienController] NO CAMERA FOUND AT ALL!");
        }
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
        if (!isControlled)
        {
            if (showDebugLogs && Time.frameCount % 120 == 0)
            {
                Debug.Log("[AlienController] Update skipped - not controlled");
            }
            return;
        }

        if (characterController == null)
        {
            Debug.LogError("[AlienController] CharacterController is NULL in Update!");
            return;
        }

        // === FIXED: Move() called EVERY frame to keep isGrounded updated ===

        // 1. Handle camera rotation (mouse only)
        HandleCameraRotation();

        // 2. Get input (can be zero)
        float h = Input.GetAxisRaw("Horizontal"); // A/D = strafe
        float v = Input.GetAxisRaw("Vertical");   // W/S = forward/back

        // Debug input every 2 seconds
        if (showDebugLogs && Time.frameCount % 120 == 0)
        {
            string camStatus = "NULL";
            if (alienCamera != null)
            {
                camStatus = $"{alienCamera.gameObject.name}, enabled={alienCamera.enabled}, GO.active={alienCamera.gameObject.activeInHierarchy}";
            }
            Debug.Log($"[AlienController] Input: H={h}, V={v}, Pos={transform.position}, Camera=[{camStatus}]");
        }
        
        // 3. Jump check FIRST (before gravity reset)
        if (Input.GetButtonDown("Jump") && characterController.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            Debug.Log($"JUMP triggered! Pos={transform.position}, cameraTransform={(cameraTransform != null ? "OK" : "NULL")}, alienCamera={(alienCamera != null ? alienCamera.gameObject.name : "NULL")}");
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
        if (cameraTransform == null)
        {
            if (showDebugLogs && Time.frameCount % 60 == 0)
            {
                Debug.LogError("[AlienController] cameraTransform is NULL! Camera cannot follow.");
            }
            // Try to find camera again
            FindCamera();
            return;
        }

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
