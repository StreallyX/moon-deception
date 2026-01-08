using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float gravity = -19.62f;
    public float jumpHeight = 1.5f;

    [Header("Mouse Look Settings")]
    public float mouseSensitivity = 2f;
    public Transform cameraTransform;
    
    [Header("Camera Reference")]
    public Camera playerCamera;
    
    [Header("Ground Check")]
    public float groundCheckDistance = 0.2f;

    private CharacterController controller;
    private Vector3 velocity;
    private float xRotation = 0f;
    
    public static PlayerMovement ActivePlayer { get; private set; }
    public static bool IsPlayerControlled => ActivePlayer != null && ActivePlayer.enabled;
    
    private bool isControlled = false;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        
        // FORCE CharacterController settings to prevent falling through ground
        controller.skinWidth = 0.08f;
        controller.stepOffset = 0.3f;
        controller.minMoveDistance = 0.001f;
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
        
        Debug.Log("[PlayerMovement] Disabled - Player control released");
    }

    void Update()
    {
        if (!isControlled) return;
        
        HandleMouseLook();
        HandleMovement();
        HandleJump();
        ApplyGravity();
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

    void HandleMovement()
    {
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        controller.Move(move * walkSpeed * Time.deltaTime);
    }

    void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    void ApplyGravity()
    {
        if (!controller.isGrounded)
        {
            velocity.y += gravity * Time.deltaTime;
        }
        else
        {
            velocity.y = -2f; // Small downward force to stay grounded
        }
        
        controller.Move(velocity * Time.deltaTime);
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
