using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Defense zone where astronaut can get a better weapon during Chaos phase.
/// Place this script on a trigger collider in strategic locations.
/// </summary>
public class DefenseZone : MonoBehaviour
{
    [Header("Zone Settings")]
    public string zoneName = "Defense Point Alpha";
    public bool isActive = false;

    [Header("Weapon Upgrade")]
    public float upgradedDamage = 50f;
    public float upgradedFireRate = 0.08f;
    public float upgradedRange = 150f;

    [Header("Visual")]
    public Color inactiveColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
    public Color activeColor = new Color(0.2f, 0.8f, 0.2f, 0.8f);
    public Color warningColor = new Color(0.9f, 0.2f, 0.2f, 0.8f);

    [Header("References")]
    public Light zoneLight;
    public Transform weaponPickupPoint;

    private bool astronautInZone = false;
    private bool weaponCollected = false;
    private MeshRenderer zoneRenderer;
    private Material zoneMaterial;

    // Static tracking
    public static DefenseZone NearestActiveZone { get; private set; }

    void Start()
    {
        // Auto-create collider if none exists
        var collider = GetComponent<Collider>();
        if (collider == null)
        {
            var boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.size = new Vector3(5f, 3f, 5f);
            boxCollider.center = new Vector3(0f, 1.5f, 0f);
            collider = boxCollider;
        }
        collider.isTrigger = true;

        // Create visual indicator if none exists
        SetupVisuals();

        // Subscribe to chaos phase (both GameManager and NetworkGameManager)
        SubscribeToChaosPhase();

        Debug.Log($"[DefenseZone] {zoneName} initialized");
    }

    void SubscribeToChaosPhase()
    {
        bool subscribed = false;

        // Subscribe to GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnChaosPhase.AddListener(ActivateZone);
            GameManager.Instance.OnGameStart.AddListener(ResetZone);
            Debug.Log($"[DefenseZone] {zoneName} subscribed to GameManager events");
            subscribed = true;
        }

        // Also subscribe to NetworkGameManager phase changes
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.OnPhaseChanged += OnNetworkPhaseChanged;
            Debug.Log($"[DefenseZone] {zoneName} subscribed to NetworkGameManager events");
            subscribed = true;
        }

        if (!subscribed)
        {
            Debug.LogWarning($"[DefenseZone] {zoneName} - No manager found! Will try again...");
            StartCoroutine(LateSubscribe());
        }
    }

    void OnNetworkPhaseChanged(NetworkGameManager.GamePhase phase)
    {
        if (phase == NetworkGameManager.GamePhase.Chaos)
        {
            Debug.Log($"[DefenseZone] {zoneName} received NetworkGameManager Chaos phase!");
            ActivateZone();
        }
    }

    System.Collections.IEnumerator LateSubscribe()
    {
        // Wait for managers to initialize
        float timeout = 5f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;

            bool subscribed = false;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnChaosPhase.RemoveListener(ActivateZone);
                GameManager.Instance.OnGameStart.RemoveListener(ResetZone);
                GameManager.Instance.OnChaosPhase.AddListener(ActivateZone);
                GameManager.Instance.OnGameStart.AddListener(ResetZone);
                Debug.Log($"[DefenseZone] {zoneName} late-subscribed to GameManager events");
                subscribed = true;
            }

            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.OnPhaseChanged -= OnNetworkPhaseChanged;
                NetworkGameManager.Instance.OnPhaseChanged += OnNetworkPhaseChanged;
                Debug.Log($"[DefenseZone] {zoneName} late-subscribed to NetworkGameManager events");
                subscribed = true;
            }

            if (subscribed) yield break;
        }

        Debug.LogError($"[DefenseZone] {zoneName} - Failed to subscribe to any manager!");
    }

    void SetupVisuals()
    {
        // Create floor marker if no renderer
        zoneRenderer = GetComponent<MeshRenderer>();

        if (zoneRenderer == null)
        {
            // Create a simple cylinder as zone marker
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.transform.SetParent(transform);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localScale = new Vector3(4f, 0.1f, 4f);

            // Remove collider from marker
            var markerCollider = marker.GetComponent<Collider>();
            if (markerCollider != null) Destroy(markerCollider);

            zoneRenderer = marker.GetComponent<MeshRenderer>();
        }

        // Create material - try URP shader first, fallback to Sprites/Default
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Standard");

        zoneMaterial = new Material(shader);

        // Configure for transparency
        if (zoneMaterial.HasProperty("_Surface"))
        {
            // URP Lit shader
            zoneMaterial.SetFloat("_Surface", 1); // Transparent
            zoneMaterial.SetFloat("_Blend", 0); // Alpha
            zoneMaterial.SetFloat("_AlphaClip", 0);
            zoneMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            zoneMaterial.renderQueue = 3000;
        }
        else
        {
            // Standard shader fallback
            zoneMaterial.SetFloat("_Mode", 3); // Transparent
            zoneMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            zoneMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            zoneMaterial.SetInt("_ZWrite", 0);
            zoneMaterial.DisableKeyword("_ALPHATEST_ON");
            zoneMaterial.EnableKeyword("_ALPHABLEND_ON");
            zoneMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            zoneMaterial.renderQueue = 3000;
        }

        zoneMaterial.color = inactiveColor;
        zoneRenderer.material = zoneMaterial;

        // Create zone light if none - make it BRIGHT for chaos visibility
        if (zoneLight == null)
        {
            GameObject lightObj = new GameObject("ZoneLight");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = Vector3.up * 3f;

            zoneLight = lightObj.AddComponent<Light>();
            zoneLight.type = LightType.Point;
            zoneLight.range = 15f; // Larger range
            zoneLight.intensity = 0f;
            zoneLight.color = activeColor;
        }

        Debug.Log($"[DefenseZone] Visual setup complete. Shader: {shader?.name ?? "NULL"}");
    }

    void Update()
    {
        // Pulse effect when active - VERY bright during chaos
        if (isActive && !weaponCollected)
        {
            float pulse = Mathf.PingPong(Time.time * 2f, 1f);

            if (zoneMaterial != null)
            {
                // Bright pulsing color - fully opaque for visibility
                Color currentColor = Color.Lerp(activeColor, warningColor, pulse);
                currentColor.a = 0.9f; // Almost fully opaque
                zoneMaterial.color = currentColor;

                // Add emission for URP
                if (zoneMaterial.HasProperty("_EmissionColor"))
                {
                    zoneMaterial.EnableKeyword("_EMISSION");
                    zoneMaterial.SetColor("_EmissionColor", currentColor * 2f);
                }
            }

            if (zoneLight != null)
            {
                // VERY bright pulsing light (visible even in chaos darkness)
                zoneLight.intensity = 3f + pulse * 2f;
            }
        }

        // Check for weapon pickup - ONLY if astronaut is in zone
        if (isActive && astronautInZone && !weaponCollected)
        {
            // Show prompt (only for astronaut - alien won't have astronautInZone=true)
            if (GameUIManager.Instance != null)
            {
                GameUIManager.Instance.ShowInteractionPrompt("Appuie sur E pour MINIGUN");
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                Debug.Log($"[DefenseZone] E pressed! Collecting weapon...");
                CollectWeapon();
            }
        }

        // Debug: Manual activation with F5 for testing
        if (Input.GetKeyDown(KeyCode.F5) && !isActive)
        {
            Debug.Log("[DefenseZone] Manual activation via F5");
            ActivateZone();
        }
    }

    void ActivateZone()
    {
        isActive = true;
        weaponCollected = false;

        Debug.Log($"[DefenseZone] {zoneName} ActivateZone called! zoneMaterial={zoneMaterial != null}, zoneRenderer={zoneRenderer != null}");

        // Ensure visuals are set up
        if (zoneMaterial == null || zoneRenderer == null)
        {
            SetupVisuals();
        }

        if (zoneMaterial != null)
        {
            // Set bright active color
            Color brightActive = activeColor;
            brightActive.a = 0.9f;
            zoneMaterial.color = brightActive;

            // Force apply material to renderer
            if (zoneRenderer != null)
            {
                zoneRenderer.material = zoneMaterial;
                zoneRenderer.material.color = brightActive;
            }

            // Add emission for URP visibility
            if (zoneMaterial.HasProperty("_EmissionColor"))
            {
                zoneMaterial.EnableKeyword("_EMISSION");
                zoneMaterial.SetColor("_EmissionColor", activeColor * 2f);
            }

            Debug.Log($"[DefenseZone] Material color set to activeColor: {activeColor}");
        }

        if (zoneLight != null)
        {
            // VERY bright for chaos visibility
            zoneLight.intensity = 5f;
            zoneLight.range = 20f;
            zoneLight.color = activeColor;
        }

        // Set as nearest active zone (simplified - would need distance check for multiple zones)
        NearestActiveZone = this;

        Debug.Log($"[DefenseZone] {zoneName} ACTIVATED! Run here for weapons!");

        // Play activation sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayAlarm();
        }
    }

    void ResetZone()
    {
        isActive = false;
        weaponCollected = false;
        astronautInZone = false;

        zoneMaterial.color = inactiveColor;

        if (zoneLight != null)
        {
            zoneLight.intensity = 0f;
        }

        if (NearestActiveZone == this)
        {
            NearestActiveZone = null;
        }
    }

    void CollectWeapon()
    {
        weaponCollected = true;

        Debug.Log($"[DefenseZone] Weapon collected at {zoneName}!");

        // Upgrade player's weapon to MINIGUN (infinite ammo, no reload)
        var playerShooting = FindObjectOfType<PlayerShooting>();
        if (playerShooting != null)
        {
            playerShooting.UpgradeToMinigun();
        }

        // Visual feedback
        zoneMaterial.color = new Color(0.2f, 0.5f, 0.2f, 0.3f);

        if (zoneLight != null)
        {
            zoneLight.intensity = 0.5f;
            zoneLight.color = Color.green;
        }

        // Hide prompt
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.HideInteractionPrompt();
        }

        // Play pickup sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayUIClick();
        }

        // Show message
        StartCoroutine(ShowWeaponMessage());
    }

    System.Collections.IEnumerator ShowWeaponMessage()
    {
        yield return new WaitForSeconds(0.5f);

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.ShowInteractionPrompt("MINIGUN OBTENU!");
        }

        yield return new WaitForSeconds(2f);

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.HideInteractionPrompt();
        }
    }

    /// <summary>
    /// Check if the collider belongs to a LOCAL astronaut player (not alien, not remote player)
    /// </summary>
    bool IsLocalAstronaut(Collider other)
    {
        // FIRST: Check if it's an alien - if so, REJECT immediately
        var alienController = other.GetComponent<AlienController>();
        if (alienController == null) alienController = other.GetComponentInParent<AlienController>();
        if (alienController != null)
        {
            // This is an alien, not an astronaut
            return false;
        }

        // Check for NetworkedPlayer - most reliable in multiplayer
        var networkedPlayer = other.GetComponentInParent<NetworkedPlayer>();
        if (networkedPlayer != null)
        {
            // Must be: 1) astronaut role, 2) local owner
            bool isAstro = networkedPlayer.isAstronaut && networkedPlayer.IsOwner;
            if (isAstro)
            {
                Debug.Log($"[DefenseZone] Detected LOCAL astronaut via NetworkedPlayer");
            }
            return isAstro;
        }

        // Fallback for single player: check for astronaut components that are ENABLED
        var player = other.GetComponent<PlayerMovement>();
        if (player == null) player = other.GetComponentInParent<PlayerMovement>();
        if (player != null && player.enabled)
        {
            Debug.Log($"[DefenseZone] Detected astronaut via PlayerMovement (single player)");
            return true;
        }

        var playerShooting = other.GetComponent<PlayerShooting>();
        if (playerShooting == null) playerShooting = other.GetComponentInParent<PlayerShooting>();
        if (playerShooting != null && playerShooting.enabled)
        {
            Debug.Log($"[DefenseZone] Detected astronaut via PlayerShooting (single player)");
            return true;
        }

        var stressSystem = other.GetComponent<StressSystem>();
        if (stressSystem == null) stressSystem = other.GetComponentInParent<StressSystem>();
        if (stressSystem != null && stressSystem.enabled)
        {
            Debug.Log($"[DefenseZone] Detected astronaut via StressSystem (single player)");
            return true;
        }

        return false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsLocalAstronaut(other))
        {
            astronautInZone = true;
            Debug.Log($"[DefenseZone] Astronaut entered {zoneName} (isActive={isActive}, weaponCollected={weaponCollected})");
        }
    }

    void OnTriggerStay(Collider other)
    {
        // Backup detection in case OnTriggerEnter missed it
        if (!astronautInZone && IsLocalAstronaut(other))
        {
            astronautInZone = true;
            Debug.Log($"[DefenseZone] Astronaut detected via OnTriggerStay in {zoneName}");
        }
    }

    void OnTriggerExit(Collider other)
    {
        // Only reset if the exiting collider is an astronaut
        if (!IsLocalAstronaut(other)) return;

        astronautInZone = false;
        Debug.Log($"[DefenseZone] Astronaut exited {zoneName}");

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.HideInteractionPrompt();
        }
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnChaosPhase.RemoveListener(ActivateZone);
            GameManager.Instance.OnGameStart.RemoveListener(ResetZone);
        }

        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.OnPhaseChanged -= OnNetworkPhaseChanged;
        }

        if (NearestActiveZone == this)
        {
            NearestActiveZone = null;
        }
    }

    // ==================== GIZMOS ====================

    void OnDrawGizmos()
    {
        Gizmos.color = isActive ? activeColor : inactiveColor;
        Gizmos.DrawWireSphere(transform.position, 2f);
        Gizmos.DrawWireCube(transform.position, new Vector3(4f, 0.2f, 4f));
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 5f);

        // Draw label
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, zoneName);
        #endif
    }
}
