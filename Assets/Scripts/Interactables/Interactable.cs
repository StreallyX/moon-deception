using UnityEngine;

/// <summary>
/// Base class for all interactable objects in the game.
/// Handles proximity detection, input handling, and cooldowns.
/// </summary>
public abstract class Interactable : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float interactionRange = 2.5f;
    public string interactionPrompt = "Press E to interact";
    public bool astronautCanUse = true;
    public bool alienCanUse = true;

    [Header("Cooldown")]
    public float cooldownTime = 5f;
    protected float lastUseTime = -999f;

    [Header("Visual")]
    public Color highlightColor = Color.cyan;
    public Color usedColor = Color.gray;

    protected bool isPlayerNearby = false;
    protected bool isAlienNearby = false;
    protected bool isUsable = true;

    private Transform playerTransform;
    private Transform alienTransform;
    private Collider interactionCollider;

    public bool CanUse => isUsable && Time.time - lastUseTime >= cooldownTime;
    public float CooldownRemaining => Mathf.Max(0f, cooldownTime - (Time.time - lastUseTime));

    protected virtual void Start()
    {
        // Look for existing trigger collider in this object or children
        // The prefab should already have the trigger configured
        Collider[] allColliders = GetComponentsInChildren<Collider>();
        foreach (var col in allColliders)
        {
            if (col.isTrigger)
            {
                interactionCollider = col;
                break;
            }
        }

        // Warn if no trigger found (prefab not configured correctly)
        if (interactionCollider == null)
        {
            Debug.LogWarning($"[Interactable] {gameObject.name} has no trigger collider! Add a SphereCollider with isTrigger=true");
        }

        // Find player and alien transforms
        var player = FindObjectOfType<PlayerMovement>();
        if (player != null)
        {
            playerTransform = player.transform;
        }

        var alien = FindObjectOfType<AlienController>();
        if (alien != null)
        {
            alienTransform = alien.transform;
        }

        Debug.Log($"[Interactable] {gameObject.name} initialized. Range: {interactionRange}m");
    }

    protected virtual void Update()
    {
        CheckProximity();
        HandleInput();
        UpdateVisuals();
    }

    /// <summary>
    /// Check proximity to player and alien
    /// </summary>
    protected virtual void CheckProximity()
    {
        // Check player proximity
        if (playerTransform != null)
        {
            float playerDist = Vector3.Distance(transform.position, playerTransform.position);
            isPlayerNearby = playerDist <= interactionRange;
        }
        else
        {
            // Try to find player if not cached
            var player = FindObjectOfType<PlayerMovement>();
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        // Check alien proximity
        if (alienTransform != null)
        {
            float alienDist = Vector3.Distance(transform.position, alienTransform.position);
            isAlienNearby = alienDist <= interactionRange;
        }
        else
        {
            // Try to find alien if not cached
            var alien = FindObjectOfType<AlienController>();
            if (alien != null)
            {
                alienTransform = alien.transform;
            }
        }
    }

    /// <summary>
    /// Handle player input for interaction
    /// </summary>
    protected virtual void HandleInput()
    {
        if (!CanUse) return;

        bool shouldShowPrompt = false;
        bool isAlienInteracting = false;

        // Check if alien is nearby and controlled
        if (isAlienNearby && alienCanUse && AlienController.IsAlienControlled)
        {
            shouldShowPrompt = true;
            isAlienInteracting = true;
        }
        // Check if astronaut is nearby and controlled
        else if (isPlayerNearby && astronautCanUse && !AlienController.IsAlienControlled)
        {
            shouldShowPrompt = true;
            isAlienInteracting = false;
        }

        if (shouldShowPrompt)
        {
            // Show interaction prompt
            if (GameUIManager.Instance != null)
            {
                GameUIManager.Instance.ShowInteractionPrompt(interactionPrompt);
            }

            // Handle E key press
            if (Input.GetKeyDown(KeyCode.E))
            {
                OnInteract(isAlienInteracting);
            }
        }
        else
        {
            // Hide prompt if we were showing it
            HidePromptIfNeeded();
        }
    }

    /// <summary>
    /// Update visual state (override in subclasses)
    /// </summary>
    protected virtual void UpdateVisuals()
    {
        // Base implementation does nothing
        // Subclasses can override to add visual feedback
    }

    /// <summary>
    /// Called when player interacts with this object
    /// </summary>
    protected abstract void OnInteract(bool isAlien);

    /// <summary>
    /// Mark this interactable as used (starts cooldown)
    /// </summary>
    protected void MarkAsUsed()
    {
        lastUseTime = Time.time;
    }

    /// <summary>
    /// Disable this interactable permanently
    /// </summary>
    public void Disable()
    {
        isUsable = false;
    }

    /// <summary>
    /// Re-enable this interactable
    /// </summary>
    public void Enable()
    {
        isUsable = true;
    }

    /// <summary>
    /// Hide interaction prompt if it's being shown
    /// </summary>
    protected void HidePromptIfNeeded()
    {
        // Only hide if we're not showing another prompt
        // This is handled by GameUIManager
    }

    // ==================== TRIGGER DETECTION ====================

    void OnTriggerEnter(Collider other)
    {
        // Detect astronaut
        if (other.GetComponent<PlayerMovement>() != null ||
            other.GetComponent<PlayerShooting>() != null)
        {
            if (playerTransform == null)
            {
                playerTransform = other.transform;
            }
        }

        // Detect alien
        if (other.GetComponent<AlienController>() != null)
        {
            if (alienTransform == null)
            {
                alienTransform = other.transform;
            }
        }
    }

    // ==================== GIZMOS ====================

    void OnDrawGizmos()
    {
        Gizmos.color = CanUse ? highlightColor : usedColor;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0.2f);
        Gizmos.DrawSphere(transform.position, interactionRange);

        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, $"{gameObject.name}\n{interactionPrompt}");
        #endif
    }
}
