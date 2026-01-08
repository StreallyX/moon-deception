using UnityEngine;

/// <summary>
/// Handles alien eating mechanics - detecting and consuming NPCs/other aliens.
/// NEVER allows eating the player.
/// </summary>
public class AlienEatSystem : MonoBehaviour
{
    [Header("Detection")]
    public float detectRange = 3f;
    public float detectRadius = 1f;
    public LayerMask edibleLayers; // Set to NPC layer
    public string[] edibleTags = { "NPC", "Alien" };
    public string playerTag = "Player";
    
    [Header("References")]
    public HungerSystem hungerSystem;
    public EatPromptUI eatPromptUI;
    public Transform cameraTransform;
    
    [Header("Effects")]
    public GameObject bloodDecalPrefab;
    
    private GameObject currentTarget;
    private TargetHighlight currentHighlight;
    
    void Start()
    {
        if (hungerSystem == null)
        {
            hungerSystem = GetComponent<HungerSystem>();
        }
        
        if (cameraTransform == null)
        {
            cameraTransform = Camera.main?.transform;
        }
        
        if (eatPromptUI == null)
        {
            eatPromptUI = FindObjectOfType<EatPromptUI>();
        }
        
        Debug.Log("[AlienEatSystem] Initialized");
    }
    
    void Update()
    {
        DetectEdibleTarget();
        HandleInput();
    }
    
    void DetectEdibleTarget()
    {
        GameObject newTarget = null;
        
        // Raycast from camera forward
        if (cameraTransform != null)
        {
            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            RaycastHit hit;
            
            // First try direct raycast
            if (Physics.Raycast(ray, out hit, detectRange))
            {
                if (IsEdible(hit.collider.gameObject))
                {
                    newTarget = hit.collider.gameObject;
                }
            }
            
            // If no direct hit, try sphere cast
            if (newTarget == null)
            {
                if (Physics.SphereCast(ray, detectRadius, out hit, detectRange))
                {
                    if (IsEdible(hit.collider.gameObject))
                    {
                        newTarget = hit.collider.gameObject;
                    }
                }
            }
        }
        
        // Update target highlighting
        if (newTarget != currentTarget)
        {
            // Remove old highlight
            if (currentTarget != null && currentHighlight != null)
            {
                currentHighlight.RemoveHighlight();
            }
            
            currentTarget = newTarget;
            
            // Add new highlight
            if (currentTarget != null)
            {
                currentHighlight = currentTarget.GetComponent<TargetHighlight>();
                if (currentHighlight == null)
                {
                    currentHighlight = currentTarget.AddComponent<TargetHighlight>();
                }
                currentHighlight.ApplyHighlight();
            }
            else
            {
                currentHighlight = null;
            }
        }
        
        // Update UI prompt
        if (eatPromptUI != null)
        {
            eatPromptUI.SetVisible(currentTarget != null);
        }
    }
    
    bool IsEdible(GameObject target)
    {
        // NEVER eat the player
        if (target.CompareTag(playerTag))
        {
            return false;
        }
        
        // Check if target has edible tag
        foreach (string tag in edibleTags)
        {
            if (target.CompareTag(tag))
            {
                return true;
            }
        }
        
        // Also check parent objects
        Transform parent = target.transform.parent;
        while (parent != null)
        {
            if (parent.CompareTag(playerTag))
            {
                return false;
            }
            foreach (string tag in edibleTags)
            {
                if (parent.CompareTag(tag))
                {
                    return true;
                }
            }
            parent = parent.parent;
        }
        
        return false;
    }
    
    void HandleInput()
    {
        if (currentTarget != null && Input.GetKeyDown(KeyCode.E))
        {
            EatTarget(currentTarget);
        }
    }
    
    void EatTarget(GameObject target)
    {
        Debug.Log($"[AlienEatSystem] Eating: {target.name}");
        
        // Restore hunger
        if (hungerSystem != null)
        {
            hungerSystem.Eat();
        }
        
        // Spawn blood decal
        if (bloodDecalPrefab != null)
        {
            Instantiate(bloodDecalPrefab, target.transform.position, Quaternion.identity);
        }
        else
        {
            // Create placeholder blood effect
            CreateBloodPlaceholder(target.transform.position);
        }
        
        // Notify GameManager if it's an NPC
        NPCBehavior npc = target.GetComponent<NPCBehavior>();
        if (npc != null)
        {
            GameManager gm = FindObjectOfType<GameManager>();
            if (gm != null)
            {
                gm.OnNPCKilled();
            }
        }
        
        // Destroy target
        Destroy(target);
        
        // Clear references
        currentTarget = null;
        currentHighlight = null;
        
        if (eatPromptUI != null)
        {
            eatPromptUI.SetVisible(false);
        }
    }
    
    void CreateBloodPlaceholder(Vector3 position)
    {
        // Simple red sphere as blood placeholder
        GameObject blood = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        blood.name = "BloodDecal";
        blood.transform.position = position + Vector3.down * 0.9f;
        blood.transform.localScale = new Vector3(1f, 0.1f, 1f);
        
        Renderer renderer = blood.GetComponent<Renderer>();
        renderer.material.color = new Color(0.5f, 0f, 0f, 1f);
        
        // Remove collider
        Collider col = blood.GetComponent<Collider>();
        if (col != null) Destroy(col);
        
        // Auto-destroy after 30 seconds
        Destroy(blood, 30f);
    }
}
