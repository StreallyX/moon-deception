using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float gravity = -19.62f; // Stronger gravity for better ground feel
    public float jumpHeight = 1.5f;

    [Header("Mouse Look Settings")]
    public float mouseSensitivity = 2f;
    public Transform cameraTransform;
    
    [Header("Ground Check")]
    public float groundCheckDistance = 0.2f;

    private CharacterController controller;
    private Vector3 velocity;
    private float xRotation = 0f;
    private bool isGrounded;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        
        // Ensure CharacterController has correct settings for ground collision
        // These can be overridden in Inspector, but set sensible defaults
        if (controller.skinWidth < 0.05f)
            controller.skinWidth = 0.08f;
        if (controller.stepOffset > 0.5f)
            controller.stepOffset = 0.3f;
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Auto-find camera if not assigned
        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
                cameraTransform = cam.transform;
            else
                cameraTransform = Camera.main.transform;
        }
        
        Debug.Log($"[PlayerMovement] Initialized. Camera: {cameraTransform?.name}, Controller grounded: {controller.isGrounded}");
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovement();
        HandleJump();
        ApplyGravity();
    }

    /// <summary>
    /// FPS Mouse Look:
    /// - Player body (this transform) rotates on Y axis (horizontal/yaw)
    /// - Camera rotates ONLY on local X axis (vertical/pitch) - NO Y rotation!
    /// - Camera is child of player, stays at eye position, only tilts up/down
    /// </summary>
    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Vertical look (pitch) - camera only, clamped
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        
        // Camera rotates ONLY on X axis (pitch), never Y
        // This prevents the "orbiting" feel - camera stays fixed at eye position
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        
        // Horizontal look (yaw) - rotate entire player body
        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleMovement()
    {
        // Check grounded state
        isGrounded = controller.isGrounded;

        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        controller.Move(move * walkSpeed * Time.deltaTime);
    }

    void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    void ApplyGravity()
    {
        // Reset vertical velocity when grounded to prevent accumulation
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Small downward force to keep grounded
        }
        
        // Only apply gravity when NOT grounded
        if (!isGrounded)
        {
            velocity.y += gravity * Time.deltaTime;
        }
        
        controller.Move(velocity * Time.deltaTime);
    }
}
