using UnityEngine;

/// <summary>
/// Handles alien eating mechanics - detecting and consuming NPCs/other aliens.
/// Raycast originates from ALIEN's position (eye level), NOT from camera.
/// </summary>
public class AlienEatSystem : MonoBehaviour
{
    [Header("Detection")]
    public float detectRange = 3f;
    public LayerMask edibleLayers;
    public string[] edibleTags = { "NPC", "Alien" };
    public string playerTag = "Player";
    
    [Header("References")]
    public HungerSystem hungerSystem;
    public GameObject eatPromptObject;
    
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
        
        if (eatPromptObject != null)
        {
            eatPromptUI = eatPromptObject.GetComponent<EatPromptUI>();
        }
        if (eatPromptUI == null)
        {
            eatPromptUI = FindObjectOfType<EatPromptUI>();
        }
        
        if (eatPromptUI != null)
        {
            eatPromptUI.SetVisible(false);
        }
        
        Debug.Log("[AlienEatSystem] Initialized - Raycast from alien position");
    }
    
    void Update()
    {
        DetectEdibleTarget();
        HandleInput();
    }
    
    void DetectEdibleTarget()
    {
        GameObject newTarget = null;
        
        // Raycast from ALIEN's position at eye level, looking FORWARD (alien's facing direction)
        // NOT from camera position!
        Vector3 rayOrigin = transform.position + Vector3.up * 1.5f; // Eye level
        Vector3 rayDirection = transform.forward; // Alien's facing direction
        
        Ray ray = new Ray(rayOrigin, rayDirection);
        RaycastHit[] hits = Physics.RaycastAll(ray, detectRange);
        
        // Debug visualization
        Debug.DrawRay(rayOrigin, rayDirection * detectRange, Color.green);
        
        // Sort by distance to get closest first
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        
        foreach (RaycastHit hit in hits)
        {
            // Skip self
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
        
        // Update target highlighting
        if (newTarget != currentTarget)
        {
            if (currentTarget != null && currentHighlight != null)
            {
                currentHighlight.RemoveHighlight();
            }
            
            currentTarget = newTarget;
            
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
        if (target == null)
        {
            Debug.LogWarning("[AlienEatSystem] EatTarget called with null target!");
            return;
        }
        
        if (target == gameObject || target.transform.IsChildOf(transform) || target.transform.root == transform.root)
        {
            Debug.LogError("[AlienEatSystem] Attempted to eat self! Aborting.");
            return;
        }
        
        Debug.Log($"[AlienEatSystem] Eating: {target.name}");
        
        Vector3 targetPosition = target.transform.position;
        NPCBehavior npc = target.GetComponent<NPCBehavior>();
        
        if (hungerSystem != null)
        {
            hungerSystem.Eat();
        }
        
        if (bloodDecalPrefab != null)
        {
            Instantiate(bloodDecalPrefab, targetPosition, Quaternion.identity);
        }
        else
        {
            CreateBloodPlaceholder(targetPosition);
        }
        
        if (npc != null)
        {
            GameManager gm = FindObjectOfType<GameManager>();
            if (gm != null)
            {
                gm.OnNPCKilled(npc);
            }
        }
        
        currentTarget = null;
        currentHighlight = null;
        
        if (eatPromptUI != null)
        {
            eatPromptUI.SetVisible(false);
        }
        
        Destroy(target);
    }
    
    void CreateBloodPlaceholder(Vector3 position)
    {
        GameObject blood = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        blood.name = "BloodDecal";
        blood.transform.position = position + Vector3.down * 0.9f;
        blood.transform.localScale = new Vector3(1f, 0.1f, 1f);
        
        Renderer renderer = blood.GetComponent<Renderer>();
        renderer.material.color = new Color(0.5f, 0f, 0f, 1f);
        
        Collider col = blood.GetComponent<Collider>();
        if (col != null) Destroy(col);
        
        Destroy(blood, 30f);
    }
}
