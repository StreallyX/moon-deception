using UnityEngine;

/// <summary>
/// Controls switching between Player and Alien control.
/// Press TAB to switch between characters.
/// Properly enables/disables cameras for each character.
/// </summary>
public class GameController : MonoBehaviour
{
    [Header("Character References")]
    public PlayerMovement playerMovement;
    public AlienController alienController;
    
    [Header("Camera References")]
    public Camera playerCamera;
    public Camera alienCamera;
    
    [Header("Settings")]
    public KeyCode switchKey = KeyCode.Tab;
    public bool startWithPlayer = true;
    
    [Header("Debug")]
    public bool isPlayerActive = true;
    
    private static GameController instance;
    public static GameController Instance => instance;
    
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    void Start()
    {
        // Auto-find references if not assigned
        if (playerMovement == null)
        {
            playerMovement = FindObjectOfType<PlayerMovement>();
        }
        
        if (alienController == null)
        {
            alienController = FindObjectOfType<AlienController>();
        }
        
        // Auto-find cameras if not assigned
        if (playerCamera == null && playerMovement != null)
        {
            playerCamera = playerMovement.GetCamera();
            if (playerCamera == null)
            {
                playerCamera = playerMovement.GetComponentInChildren<Camera>();
            }
        }
        
        if (alienCamera == null && alienController != null)
        {
            alienCamera = alienController.GetCamera();
            if (alienCamera == null)
            {
                // Try to find alien camera by name
                Camera[] cameras = FindObjectsOfType<Camera>(true);
                foreach (Camera cam in cameras)
                {
                    if (cam.name.ToLower().Contains("alien"))
                    {
                        alienCamera = cam;
                        break;
                    }
                }
            }
        }
        
        // Initialize control state
        if (startWithPlayer)
        {
            SwitchToPlayer();
        }
        else
        {
            SwitchToAlien();
        }
        
        Debug.Log("[GameController] Initialized - Press TAB to switch characters");
    }
    
    void Update()
    {
        if (Input.GetKeyDown(switchKey))
        {
            ToggleControl();
        }
    }
    
    public void ToggleControl()
    {
        if (isPlayerActive)
        {
            SwitchToAlien();
        }
        else
        {
            SwitchToPlayer();
        }
    }
    
    public void SwitchToPlayer()
    {
        isPlayerActive = true;
        
        // Disable alien controller and camera
        if (alienController != null)
        {
            alienController.enabled = false;
        }
        if (alienCamera != null)
        {
            alienCamera.gameObject.SetActive(false);
        }
        
        // Enable player controller and camera
        if (playerMovement != null)
        {
            playerMovement.enabled = true;
        }
        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(true);
        }
        
        Debug.Log("[GameController] Switched to PLAYER control");
    }
    
    public void SwitchToAlien()
    {
        isPlayerActive = false;
        
        // Disable player controller and camera
        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }
        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(false);
        }
        
        // Enable alien controller and camera
        if (alienController != null)
        {
            alienController.enabled = true;
        }
        if (alienCamera != null)
        {
            alienCamera.gameObject.SetActive(true);
        }
        
        Debug.Log("[GameController] Switched to ALIEN control");
    }
    
    public bool IsPlayerControlled()
    {
        return isPlayerActive;
    }
    
    public bool IsAlienControlled()
    {
        return !isPlayerActive;
    }
}
