using UnityEngine;

/// <summary>
/// Handles alien eating mechanics - detecting and consuming NPCs/other aliens.
/// NEVER allows eating the player or self.
/// </summary>
public class AlienEatSystem : MonoBehaviour
{
    [Header("Detection")]
    public float detectRange = 3f;
    public LayerMask edibleLayers; // Set to NPC layer
    public string[] edibleTags = { "NPC", "Alien" };
    public string playerTag = "Player";
    
    [Header("References")]
    public HungerSystem hungerSystem;
    public GameObject eatPromptObject; // Changed to GameObject for easy Inspector assignment
    public Transform cameraTransform;
    
    [Header("Effects")]
    public GameObject bloodDecalPrefab;
    
    private GameObject currentTarget;
    private TargetHighlight currentHighlight;
    private EatPromptUI eatPromptUI;
    
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
        
        // Get EatPromptUI from GameObject reference or find it
        if (eatPromptObject != null)
        {
            eatPromptUI = eatPromptObject.GetComponent<EatPromptUI>();
        }
        if (eatPromptUI == null)
        {
            eatPromptUI = FindObjectOfType<EatPromptUI>();
        }
        
        // Hide prompt by default on start
        if (eatPromptUI != null)
        {
            eatPromptUI.SetVisible(false);
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
        
        // Raycast from camera forward - use precise raycast, not sphere cast
        if (cameraTransform != null)
        {
            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, detectRange);
            
            // Sort by distance to get closest first
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            
            foreach (RaycastHit hit in hits)
            {
                // CRITICAL: Skip self - check if hit is this gameObject or any child/parent
                if (hit.transform == transform) continue;
                if (hit.transform.root == transform.root) continue;
                if (hit.transform.IsChildOf(transform)) continue;
                if (transform.IsChildOf(hit.transform)) continue;
                
                // Check if edible
                if (IsEdible(hit.collider.gameObject))
                {
                    newTarget = hit.collider.gameObject;
                    break;
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
        
        // NEVER eat self
        if (target == gameObject || target.transform.root == transform.root)
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
            // Skip if parent is self
            if (parent == transform)
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
        // CRITICAL: Null check - don't proceed if target is null or is self
        if (target == null)
        {
            Debug.LogWarning("[AlienEatSystem] EatTarget called with null target!");
            return;
        }
        
        // SAFETY: Never destroy self or our own gameObject
        if (target == gameObject || target.transform.IsChildOf(transform) || target.transform.root == transform.root)
        {
            Debug.LogError("[AlienEatSystem] Attempted to eat self! Aborting.");
            return;
        }
        
        Debug.Log($"[AlienEatSystem] Eating: {target.name}");
        
        // Store target info before destroying
        Vector3 targetPosition = target.transform.position;
        NPCBehavior npc = target.GetComponent<NPCBehavior>();
        
        // Restore hunger
        if (hungerSystem != null)
        {
            hungerSystem.Eat();
        }
        
        // Spawn blood decal at target position (before destroying)
        if (bloodDecalPrefab != null)
        {
            Instantiate(bloodDecalPrefab, targetPosition, Quaternion.identity);
        }
        else
        {
            // Create placeholder blood effect
            CreateBloodPlaceholder(targetPosition);
        }
        
        // Notify GameManager if it's an NPC
        if (npc != null)
        {
            GameManager gm = FindObjectOfType<GameManager>();
            if (gm != null)
            {
                gm.OnNPCKilled(npc);
            }
        }
        
        // Clear references BEFORE destroying to avoid stale references
        currentTarget = null;
        currentHighlight = null;
        
        if (eatPromptUI != null)
        {
            eatPromptUI.SetVisible(false);
        }
        
        // Destroy target LAST (not self!)
        Destroy(target);
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
