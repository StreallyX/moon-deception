using UnityEngine;

public class NPCInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private string interactionMessage = "Appuyer sur E";
    
    [Header("NPC Info")]
    [SerializeField] private string npcName = "NPC";
    [SerializeField] private bool isInteractable = true;
    
    private bool isPlayerNearby = false;
    private Transform playerTransform;
    
    void Update()
    {
        if (!isInteractable) return;
        
        CheckPlayerProximity();
        
        if (isPlayerNearby)
        {
            if (GameUIManager.Instance != null)
            {
                GameUIManager.Instance.ShowInteractionPrompt(interactionMessage);
            }
            
            if (Input.GetKeyDown(KeyCode.E))
            {
                OnInteract();
            }
        }
        else
        {
            if (GameUIManager.Instance != null)
            {
                GameUIManager.Instance.HideInteractionPrompt();
            }
        }
    }
    
    void CheckPlayerProximity()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                isPlayerNearby = false;
                return;
            }
        }
        
        float distance = Vector3.Distance(transform.position, playerTransform.position);
        
        bool wasNearby = isPlayerNearby;
        isPlayerNearby = distance <= interactionDistance;
        
        if (isPlayerNearby && !wasNearby)
        {
            Debug.Log($"[NPCInteraction] Player near {npcName}");
        }
        else if (!isPlayerNearby && wasNearby)
        {
            Debug.Log($"[NPCInteraction] Player left {npcName}");
        }
    }
    
    void OnInteract()
    {
        Debug.Log($"[NPCInteraction] Interacted with {npcName}");
    }
    
    public void SetInteractable(bool value)
    {
        isInteractable = value;
        if (!isInteractable && GameUIManager.Instance != null)
        {
            GameUIManager.Instance.HideInteractionPrompt();
        }
    }
    
    void OnDisable()
    {
        if (isPlayerNearby && GameUIManager.Instance != null)
        {
            GameUIManager.Instance.HideInteractionPrompt();
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
