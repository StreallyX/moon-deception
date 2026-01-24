using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Handles alien eating mechanics - detecting and consuming NPCs/other aliens.
/// Raycast originates from ALIEN's position (eye level), NOT from camera.
/// SERVER-AUTHORITATIVE: Eating destroys NetworkObjects via Despawn().
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
    public AlienController alienController;
    
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
        
        if (alienController == null)
        {
            alienController = GetComponent<AlienController>();
        }
        
        if (eatPromptObject != null)
        {
            eatPromptUI = eatPromptObject.GetComponent<EatPromptUI>();
        }
        if (eatPromptUI == null)
        {
            eatPromptUI = FindFirstObjectByType<EatPromptUI>();
        }
        
        if (eatPromptUI != null)
        {
            eatPromptUI.SetVisible(false);
        }
        
        Debug.Log("[AlienEatSystem] Initialized - Raycast from alien position");
    }
    
    void Update()
    {
        // Only detect and highlight when alien is being controlled locally
        if (!AlienController.IsAlienControlled)
        {
            // Clear any existing highlight when not controlled
            if (currentTarget != null)
            {
                if (currentHighlight != null)
                {
                    currentHighlight.RemoveHighlight();
                }
                currentTarget = null;
                currentHighlight = null;

                if (eatPromptUI != null)
                {
                    eatPromptUI.SetVisible(false);
                }
            }
            return;
        }

        DetectEdibleTarget();
        HandleInput();
    }
    
    void DetectEdibleTarget()
    {
        GameObject newTarget = null;
        
        Camera alienCamera = alienController != null ? alienController.GetCamera() : null;
        
        Vector3 rayOrigin;
        Vector3 rayDirection;
        
        if (alienCamera != null)
        {
            rayOrigin = transform.position + Vector3.up * 1.5f;
            rayDirection = alienCamera.transform.forward;
        }
        else
        {
            rayOrigin = transform.position + Vector3.up * 1.5f;
            rayDirection = transform.forward;
        }
        
        Ray ray = new Ray(rayOrigin, rayDirection);
        RaycastHit[] hits = Physics.RaycastAll(ray, detectRange);
        
        Debug.DrawRay(rayOrigin, rayDirection * detectRange, Color.green);
        
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == transform) continue;
            if (hit.transform.root == transform.root) continue;
            if (hit.transform.IsChildOf(transform)) continue;
            if (transform.IsChildOf(hit.transform)) continue;
            
            if (IsEdible(hit.collider.gameObject))
            {
                newTarget = hit.collider.gameObject;
                break;
            }
        }
        
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
        if (target.CompareTag(playerTag))
        {
            return false;
        }
        
        if (target == gameObject || target.transform.root == transform.root)
        {
            return false;
        }
        
        foreach (string tag in edibleTags)
        {
            if (target.CompareTag(tag))
            {
                return true;
            }
        }
        
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
        NetworkObject netObj = target.GetComponent<NetworkObject>();
        ulong npcNetworkId = (netObj != null && netObj.IsSpawned) ? netObj.NetworkObjectId : 0;

        // NETWORK: Broadcast eating to all clients via RPC
        var networkedPlayer = GetComponent<NetworkedPlayer>();
        if (networkedPlayer != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
        {
            // RPC handles: hunger restore, blood decal, kill feed, NPC despawn
            networkedPlayer.AlienEatServerRpc(targetPosition, npcNetworkId);
            Debug.Log($"[AlienEatSystem] Sent eat RPC for NPC {npcNetworkId}");
        }
        else
        {
            // Single player fallback
            if (hungerSystem != null)
            {
                hungerSystem.Eat();
            }

            // Spawn blood decal
            if (BloodDecalManager.Instance != null)
            {
                BloodDecalManager.Instance.SpawnBloodDecal(targetPosition);
            }
            else if (bloodDecalPrefab != null)
            {
                Instantiate(bloodDecalPrefab, targetPosition, Quaternion.identity);
            }
            else
            {
                CreateBloodPlaceholder(targetPosition);
            }

            // Destroy target in single player
            Destroy(target);
            Debug.Log($"[AlienEatSystem] Destroyed non-networked: {target.name}");
        }

        if (npc != null)
        {
            GameManager gm = FindFirstObjectByType<GameManager>();
            if (gm != null)
            {
                // Use the new method that only adds stress if astronaut is nearby
                // This prevents aliens from maxing stress by eating 5 NPCs quickly
                gm.OnNPCEatenByAlien(npc, targetPosition);
            }
        }

        currentTarget = null;
        currentHighlight = null;

        if (eatPromptUI != null)
        {
            eatPromptUI.SetVisible(false);
        }
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

    // ==================== UI FALLBACK ====================
    void OnGUI()
    {
        // Only show when alien is controlled and has a valid target
        if (!AlienController.IsAlienControlled) return;
        if (currentTarget == null) return;

        // Don't show UI if game ended
        if (GameManager.Instance != null && GameManager.Instance.CurrentPhase == GameManager.GamePhase.Ended) return;

        // Create styles for the eat prompt
        GUIStyle promptStyle = new GUIStyle(GUI.skin.label);
        promptStyle.fontSize = 28;
        promptStyle.fontStyle = FontStyle.Bold;
        promptStyle.alignment = TextAnchor.MiddleCenter;
        promptStyle.normal.textColor = Color.red;

        GUIStyle shadowStyle = new GUIStyle(promptStyle);
        shadowStyle.normal.textColor = Color.black;

        // Position at center-bottom of screen
        float width = 350;
        float height = 40;
        float x = (Screen.width - width) / 2;
        float y = Screen.height * 0.65f;

        // Draw shadow
        GUI.Label(new Rect(x + 2, y + 2, width, height), "Appuie sur E pour MANGER", shadowStyle);

        // Draw text with pulsing effect
        float pulse = 0.7f + Mathf.PingPong(Time.time * 2f, 0.3f);
        promptStyle.normal.textColor = new Color(1f, 0f, 0f, pulse);
        GUI.Label(new Rect(x, y, width, height), "Appuie sur E pour MANGER", promptStyle);

        // Also show target name
        GUIStyle nameStyle = new GUIStyle(GUI.skin.label);
        nameStyle.fontSize = 18;
        nameStyle.alignment = TextAnchor.MiddleCenter;
        nameStyle.normal.textColor = new Color(1f, 0.5f, 0.5f);
        GUI.Label(new Rect(x, y + 35, width, 25), $"Cible: {currentTarget.name}", nameStyle);
    }
}
