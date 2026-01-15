using UnityEngine;

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

        // Subscribe to chaos phase
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnChaosPhase.AddListener(ActivateZone);
            GameManager.Instance.OnGameStart.AddListener(ResetZone);
            Debug.Log($"[DefenseZone] {zoneName} subscribed to GameManager events");
        }
        else
        {
            Debug.LogWarning($"[DefenseZone] {zoneName} - GameManager.Instance is null! Will try again...");
            StartCoroutine(LateSubscribe());
        }

        Debug.Log($"[DefenseZone] {zoneName} initialized");
    }

    System.Collections.IEnumerator LateSubscribe()
    {
        // Wait a frame for GameManager to initialize
        yield return null;
        yield return null;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnChaosPhase.AddListener(ActivateZone);
            GameManager.Instance.OnGameStart.AddListener(ResetZone);
            Debug.Log($"[DefenseZone] {zoneName} late-subscribed to GameManager events");
        }
        else
        {
            Debug.LogError($"[DefenseZone] {zoneName} - Still can't find GameManager!");
        }
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

        // Create material
        zoneMaterial = new Material(Shader.Find("Standard"));
        zoneMaterial.SetFloat("_Mode", 3); // Transparent
        zoneMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        zoneMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        zoneMaterial.SetInt("_ZWrite", 0);
        zoneMaterial.DisableKeyword("_ALPHATEST_ON");
        zoneMaterial.EnableKeyword("_ALPHABLEND_ON");
        zoneMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        zoneMaterial.renderQueue = 3000;
        zoneMaterial.color = inactiveColor;

        zoneRenderer.material = zoneMaterial;

        // Create zone light if none
        if (zoneLight == null)
        {
            GameObject lightObj = new GameObject("ZoneLight");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = Vector3.up * 3f;

            zoneLight = lightObj.AddComponent<Light>();
            zoneLight.type = LightType.Point;
            zoneLight.range = 10f;
            zoneLight.intensity = 0f;
            zoneLight.color = activeColor;
        }
    }

    void Update()
    {
        // Pulse effect when active
        if (isActive && !weaponCollected)
        {
            float pulse = Mathf.PingPong(Time.time * 2f, 1f);
            if (zoneMaterial != null)
            {
                Color currentColor = Color.Lerp(activeColor, warningColor, pulse);
                zoneMaterial.color = currentColor;
            }

            if (zoneLight != null)
            {
                zoneLight.intensity = 1f + pulse;
            }
        }

        // Check for weapon pickup
        if (isActive && astronautInZone && !weaponCollected)
        {
            // Show prompt
            if (GameUIManager.Instance != null)
            {
                GameUIManager.Instance.ShowInteractionPrompt("Press E to grab MACHINE GUN");
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
            zoneMaterial.color = activeColor;

            // Force apply material to renderer
            if (zoneRenderer != null)
            {
                zoneRenderer.material = zoneMaterial;
                zoneRenderer.material.color = activeColor;
            }

            Debug.Log($"[DefenseZone] Material color set to activeColor: {activeColor}");
        }

        if (zoneLight != null)
        {
            zoneLight.intensity = 2f;
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
            GameUIManager.Instance.ShowInteractionPrompt("MACHINE GUN ACQUIRED!");
        }

        yield return new WaitForSeconds(2f);

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.HideInteractionPrompt();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Check if astronaut entered - try multiple detection methods
        var player = other.GetComponent<PlayerMovement>();
        var playerShooting = other.GetComponent<PlayerShooting>();
        var stressSystem = other.GetComponent<StressSystem>();

        if (player != null || playerShooting != null || stressSystem != null)
        {
            astronautInZone = true;
            Debug.Log($"[DefenseZone] Astronaut entered {zoneName}");
        }
    }

    void OnTriggerStay(Collider other)
    {
        // Backup detection in case OnTriggerEnter missed it
        if (!astronautInZone)
        {
            var player = other.GetComponent<PlayerMovement>();
            var playerShooting = other.GetComponent<PlayerShooting>();

            if (player != null || playerShooting != null)
            {
                astronautInZone = true;
                Debug.Log($"[DefenseZone] Astronaut detected via OnTriggerStay in {zoneName}");
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        var player = other.GetComponent<PlayerMovement>();
        var playerShooting = other.GetComponent<PlayerShooting>();

        if (player != null || playerShooting != null)
        {
            astronautInZone = false;

            if (GameUIManager.Instance != null)
            {
                GameUIManager.Instance.HideInteractionPrompt();
            }
        }
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnChaosPhase.RemoveListener(ActivateZone);
            GameManager.Instance.OnGameStart.RemoveListener(ResetZone);
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
