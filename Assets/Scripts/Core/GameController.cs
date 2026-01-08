using UnityEngine;

/// <summary>
/// Controls switching between Player and Alien control.
/// Press TAB to switch between characters for testing.
/// </summary>
public class GameController : MonoBehaviour
{
    [Header("Character References")]
    public PlayerMovement playerMovement;
    public AlienController alienController;
    
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
        // TAB to switch between Player and Alien
        if (Input.GetKeyDown(switchKey))
        {
            ToggleControl();
        }
    }
    
    /// <summary>
    /// Toggle between Player and Alien control
    /// </summary>
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
    
    /// <summary>
    /// Switch control to the Player
    /// </summary>
    public void SwitchToPlayer()
    {
        isPlayerActive = true;
        
        if (alienController != null)
        {
            alienController.enabled = false;
        }
        
        if (playerMovement != null)
        {
            playerMovement.enabled = true;
        }
        
        Debug.Log("[GameController] Switched to PLAYER control");
    }
    
    /// <summary>
    /// Switch control to the Alien
    /// </summary>
    public void SwitchToAlien()
    {
        isPlayerActive = false;
        
        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }
        
        if (alienController != null)
        {
            alienController.enabled = true;
        }
        
        Debug.Log("[GameController] Switched to ALIEN control");
    }
    
    /// <summary>
    /// Check if player is currently controlled
    /// </summary>
    public bool IsPlayerControlled()
    {
        return isPlayerActive;
    }
    
    /// <summary>
    /// Check if alien is currently controlled
    /// </summary>
    public bool IsAlienControlled()
    {
        return !isPlayerActive;
    }
}
