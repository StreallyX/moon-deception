using UnityEngine;
using System.Collections;

/// <summary>
/// Handles alien transformation during Chaos phase.
/// - Dramatic visual transformation
/// - Speed boost
/// - Fluorescent wall-hack vision (see astronaut through walls)
/// - Attack capability
/// </summary>
public class AlienTransformation : MonoBehaviour
{
    [Header("Transformation Stats")]
    public float normalSpeed = 5f;
    public float transformedSpeed = 8f;
    public float attackDamage = 35f;
    public float attackRange = 2.5f;
    public float attackCooldown = 0.8f;
    public float chaosHealthBoost = 100f; // Extra HP when transformed (100 + 100 = 200)

    [Header("Transformation Visuals")]
    public Color transformedColor = new Color(0.8f, 0.1f, 0.1f);
    public Color transformedEmissionColor = new Color(1f, 0.2f, 0.2f);
    public float transformedScale = 1.3f;
    public float glowIntensity = 2f;

    [Header("Wall-Hack Settings")]
    public Color astronautOutlineColor = new Color(1f, 0.3f, 0.3f, 0.8f);
    public float outlinePulseSpeed = 2f;

    [Header("Attack Key")]
    public KeyCode attackKey = KeyCode.Mouse0;

    // State
    private bool isTransformed = false;
    private float attackTimer = 0f;
    private Vector3 originalScale;
    private AlienController alienController;
    private Renderer[] renderers;
    private Color[] originalColors;
    private Light alienGlow;

    // Wall-hack
    private GameObject astronautOutline;
    private Transform astronautTransform;
    private LineRenderer outlineRenderer;

    public bool IsTransformed => isTransformed;

    void Start()
    {
        alienController = GetComponent<AlienController>();
        renderers = GetComponentsInChildren<Renderer>();
        originalScale = transform.localScale;

        // Store original colors
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].material != null)
            {
                originalColors[i] = renderers[i].material.color;
            }
        }

        // Subscribe to chaos phase
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnChaosPhase.AddListener(OnChaosPhaseStarted);
            GameManager.Instance.OnGameEnd.AddListener(OnGameEnded);
            Debug.Log("[AlienTransformation] Subscribed to GameManager events");
        }
        else
        {
            Debug.LogWarning("[AlienTransformation] GameManager.Instance is null, will retry...");
            StartCoroutine(LateSubscribe());
        }

        // Find astronaut
        var stressSystem = FindObjectOfType<StressSystem>();
        if (stressSystem != null)
        {
            astronautTransform = stressSystem.transform;
        }

        Debug.Log("[AlienTransformation] Initialized");
    }

    System.Collections.IEnumerator LateSubscribe()
    {
        // Wait for GameManager to be ready
        yield return null;
        yield return null;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnChaosPhase.AddListener(OnChaosPhaseStarted);
            GameManager.Instance.OnGameEnd.AddListener(OnGameEnded);
            Debug.Log("[AlienTransformation] Late-subscribed to GameManager events");
        }
        else
        {
            Debug.LogError("[AlienTransformation] Still can't find GameManager!");
        }
    }

    void Update()
    {
        if (!isTransformed) return;
        if (!AlienController.IsAlienControlled) return;

        // Update attack cooldown
        if (attackTimer > 0)
            attackTimer -= Time.deltaTime;

        // Attack input
        if (Input.GetKeyDown(attackKey) && attackTimer <= 0)
        {
            TryAttack();
        }

        // Update wall-hack
        UpdateWallHackOutline();

        // Pulsing glow effect
        UpdateTransformedGlow();
    }

    void OnChaosPhaseStarted()
    {
        StartCoroutine(TransformSequence());
    }

    void OnGameEnded(GameManager.WinCondition winner)
    {
        ResetTransformation();
    }

    IEnumerator TransformSequence()
    {
        Debug.Log("[AlienTransformation] TRANSFORMING!");

        // Play transformation sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayAlienReveal();
        }

        // Dramatic camera shake
        if (CameraShake.Instance != null)
        {
            CameraShake.Instance.ShakeHeavy();
        }

        // Create glow light
        if (alienGlow == null)
        {
            GameObject glowObj = new GameObject("AlienGlow");
            glowObj.transform.SetParent(transform);
            glowObj.transform.localPosition = Vector3.up;
            alienGlow = glowObj.AddComponent<Light>();
            alienGlow.type = LightType.Point;
            alienGlow.range = 8f;
            alienGlow.color = transformedEmissionColor;
            alienGlow.intensity = 0f;
        }

        // Transformation animation
        float duration = 1f;
        float elapsed = 0f;
        Vector3 targetScale = originalScale * transformedScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Scale up with bounce
            float bounceT = 1f - Mathf.Pow(1f - t, 3f);
            transform.localScale = Vector3.Lerp(originalScale, targetScale, bounceT);

            // Color transition with flashing
            float flash = Mathf.PingPong(t * 8f, 1f);
            Color currentColor = Color.Lerp(Color.white, transformedColor, t);
            currentColor = Color.Lerp(currentColor, Color.white, flash * (1f - t));

            foreach (var renderer in renderers)
            {
                if (renderer != null && renderer.material != null)
                {
                    renderer.material.color = currentColor;

                    if (renderer.material.HasProperty("_EmissionColor"))
                    {
                        renderer.material.EnableKeyword("_EMISSION");
                        renderer.material.SetColor("_EmissionColor", transformedEmissionColor * flash * 3f);
                    }
                }
            }

            // Glow light intensity
            if (alienGlow != null)
            {
                alienGlow.intensity = glowIntensity * t;
            }

            yield return null;
        }

        // Final transformation state
        transform.localScale = targetScale;

        foreach (var renderer in renderers)
        {
            if (renderer != null && renderer.material != null)
            {
                renderer.material.color = transformedColor;

                if (renderer.material.HasProperty("_EmissionColor"))
                {
                    renderer.material.EnableKeyword("_EMISSION");
                    renderer.material.SetColor("_EmissionColor", transformedEmissionColor * glowIntensity);
                }
            }
        }

        // Apply transformation buffs
        isTransformed = true;

        if (alienController != null)
        {
            alienController.moveSpeed = transformedSpeed;
        }

        // Create wall-hack system
        CreateWallHackOutline();

        // Disable hunger system during chaos (hide bar, stop decay)
        var hungerSystem = GetComponent<HungerSystem>();
        if (hungerSystem != null)
        {
            hungerSystem.enabled = false;
        }

        // Boost alien HP for chaos phase
        var alienHealth = GetComponent<AlienHealth>();
        if (alienHealth != null)
        {
            alienHealth.maxHealth += chaosHealthBoost;
            alienHealth.currentHealth = alienHealth.maxHealth; // Full heal + boost
            Debug.Log($"[AlienTransformation] HP boosted to {alienHealth.maxHealth}!");

            // Update UI to show new max HP
            if (GameUIManager.Instance != null)
            {
                GameUIManager.Instance.UpdateAlienHealthBar(alienHealth.currentHealth, alienHealth.maxHealth);
            }
        }

        // Hide hunger bar, show only HP in chaos mode
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.SetChaosMode(true);
        }

        Debug.Log("[AlienTransformation] TRANSFORMATION COMPLETE! HUNT THE ASTRONAUT!");
    }

    void UpdateTransformedGlow()
    {
        if (alienGlow == null) return;

        // Pulsing glow
        float pulse = 0.7f + Mathf.Sin(Time.time * 3f) * 0.3f;
        alienGlow.intensity = glowIntensity * pulse;
    }

    // ==================== ATTACK SYSTEM ====================

    void TryAttack()
    {
        attackTimer = attackCooldown;

        // Play attack sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.bulletImpactFlesh);
        }

        // Attack animation
        StartCoroutine(AttackAnimation());

        // Check for astronaut in range
        if (astronautTransform == null) return;

        float dist = Vector3.Distance(transform.position, astronautTransform.position);

        if (dist <= attackRange)
        {
            // Attack hit!
            Debug.Log("[AlienTransformation] ATTACK HIT!");

            // Damage astronaut
            var astronautHealth = astronautTransform.GetComponent<AstronautHealth>();
            if (astronautHealth != null)
            {
                astronautHealth.TakeDamage(attackDamage);
            }

            // Also try IDamageable
            var damageable = astronautTransform.GetComponent<IDamageable>();
            if (damageable != null && astronautHealth == null)
            {
                damageable.TakeDamage(attackDamage);
            }

            // Visual feedback for hit
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.ShakeImpact();
            }
        }
        else
        {
            Debug.Log($"[AlienTransformation] Attack missed - distance: {dist:F1}m (need: {attackRange}m)");
        }
    }

    IEnumerator AttackAnimation()
    {
        Vector3 startPos = transform.position;
        Vector3 lungeDir = transform.forward;

        if (astronautTransform != null)
        {
            lungeDir = (astronautTransform.position - transform.position).normalized;
            lungeDir.y = 0;
        }

        Vector3 lungePos = startPos + lungeDir * 1f;

        // Quick lunge forward
        float elapsed = 0f;
        float duration = 0.1f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, lungePos, elapsed / duration);
            yield return null;
        }

        // Return
        elapsed = 0f;
        duration = 0.15f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(lungePos, startPos, elapsed / duration);
            yield return null;
        }

        transform.position = startPos;
    }

    // ==================== WALL-HACK VISION ====================

    void CreateWallHackOutline()
    {
        if (astronautTransform == null) return;

        // Create outline container
        astronautOutline = new GameObject("AstronautWallHack");

        // Create multiple layers for glow effect
        CreateOutlineSphere(astronautOutline.transform, 2.5f, 0.4f);
        CreateOutlineSphere(astronautOutline.transform, 2.0f, 0.6f);
        CreateOutlineSphere(astronautOutline.transform, 1.5f, 0.8f);

        // Create line renderer to show direction
        GameObject lineObj = new GameObject("DirectionLine");
        lineObj.transform.SetParent(astronautOutline.transform);

        outlineRenderer = lineObj.AddComponent<LineRenderer>();
        outlineRenderer.startWidth = 0.1f;
        outlineRenderer.endWidth = 0.02f;
        outlineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        outlineRenderer.startColor = astronautOutlineColor;
        outlineRenderer.endColor = new Color(astronautOutlineColor.r, astronautOutlineColor.g, astronautOutlineColor.b, 0.2f);
        outlineRenderer.positionCount = 2;

        Debug.Log("[AlienTransformation] Wall-hack vision ACTIVATED!");
    }

    void CreateOutlineSphere(Transform parent, float size, float alpha)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "OutlineLayer";
        sphere.transform.SetParent(parent);
        sphere.transform.localPosition = Vector3.zero;
        sphere.transform.localScale = Vector3.one * size;

        // Remove collider
        var collider = sphere.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        // Create glowing material
        var renderer = sphere.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = new Color(astronautOutlineColor.r, astronautOutlineColor.g, astronautOutlineColor.b, alpha * 0.5f);
        mat.renderQueue = 4000; // Render on top of everything
        renderer.material = mat;
    }

    void UpdateWallHackOutline()
    {
        if (astronautOutline == null || astronautTransform == null) return;

        // Only visible when alien is controlled
        bool shouldShow = isTransformed && AlienController.IsAlienControlled;
        astronautOutline.SetActive(shouldShow);

        if (!shouldShow) return;

        // Follow astronaut
        astronautOutline.transform.position = astronautTransform.position + Vector3.up * 1f;

        // Pulsing effect
        float pulse = 0.8f + Mathf.Sin(Time.time * outlinePulseSpeed) * 0.2f;
        astronautOutline.transform.localScale = Vector3.one * pulse;

        // Update direction line from alien to astronaut
        if (outlineRenderer != null)
        {
            outlineRenderer.SetPosition(0, transform.position + Vector3.up);
            outlineRenderer.SetPosition(1, astronautTransform.position + Vector3.up);
        }

        // Color pulse
        foreach (var renderer in astronautOutline.GetComponentsInChildren<Renderer>())
        {
            if (renderer != null && renderer.material != null)
            {
                Color pulseColor = astronautOutlineColor;
                pulseColor.a = 0.3f + Mathf.Sin(Time.time * 4f) * 0.2f;
                renderer.material.color = pulseColor;
            }
        }
    }

    // ==================== RESET ====================

    public void ResetTransformation()
    {
        isTransformed = false;
        transform.localScale = originalScale;

        if (alienController != null)
        {
            alienController.moveSpeed = normalSpeed;
        }

        // Reset colors
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].material != null)
            {
                renderers[i].material.color = originalColors[i];

                if (renderers[i].material.HasProperty("_EmissionColor"))
                {
                    renderers[i].material.SetColor("_EmissionColor", Color.black);
                }
            }
        }

        // Remove glow
        if (alienGlow != null)
        {
            Destroy(alienGlow.gameObject);
            alienGlow = null;
        }

        // Remove wall-hack
        if (astronautOutline != null)
        {
            Destroy(astronautOutline);
            astronautOutline = null;
        }

        // Re-enable hunger
        var hungerSystem = GetComponent<HungerSystem>();
        if (hungerSystem != null)
        {
            hungerSystem.enabled = true;
        }

        // Reset HP boost
        var alienHealth = GetComponent<AlienHealth>();
        if (alienHealth != null)
        {
            alienHealth.maxHealth = 100f; // Back to normal
            alienHealth.currentHealth = Mathf.Min(alienHealth.currentHealth, alienHealth.maxHealth);
        }

        // Show hunger bar again
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.SetChaosMode(false);
        }
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnChaosPhase.RemoveListener(OnChaosPhaseStarted);
            GameManager.Instance.OnGameEnd.RemoveListener(OnGameEnded);
        }

        if (astronautOutline != null)
        {
            Destroy(astronautOutline);
        }

        if (alienGlow != null)
        {
            Destroy(alienGlow.gameObject);
        }
    }

    // ==================== UI ====================

    void OnGUI()
    {
        if (!isTransformed || !AlienController.IsAlienControlled) return;

        // Hunt mode indicator
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 32;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;

        // Pulsing red color
        float pulse = 0.7f + Mathf.Sin(Time.time * 4f) * 0.3f;
        titleStyle.normal.textColor = new Color(1f, 0.2f, 0.2f, pulse);

        GUI.Label(new Rect(Screen.width / 2 - 150, 30, 300, 50), "HUNT MODE", titleStyle);

        // Attack info
        GUIStyle infoStyle = new GUIStyle(GUI.skin.label);
        infoStyle.fontSize = 18;
        infoStyle.alignment = TextAnchor.MiddleCenter;
        infoStyle.normal.textColor = Color.white;

        string attackStatus = attackTimer <= 0 ? "READY" : attackTimer.ToString("F1") + "s";
        GUI.Label(new Rect(Screen.width / 2 - 100, 75, 200, 30), $"[CLICK] Attack: {attackStatus}", infoStyle);

        // Distance to astronaut
        if (astronautTransform != null)
        {
            float dist = Vector3.Distance(transform.position, astronautTransform.position);
            string distColor = dist <= attackRange ? "<color=green>" : "<color=white>";
            GUI.Label(new Rect(Screen.width / 2 - 100, 100, 200, 30), $"Distance: {dist:F1}m", infoStyle);
        }
    }
}
