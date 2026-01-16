using UnityEngine;
using System.Collections;

/// <summary>
/// Alien chaos abilities to stress the astronaut.
/// 4 distinct powers with individual cooldowns.
/// Only usable during Search phase (not during Chaos phase).
/// </summary>
public class AlienAbilities : MonoBehaviour
{
    [Header("Ability Keys")]
    public KeyCode ability1Key = KeyCode.Alpha1;
    public KeyCode ability2Key = KeyCode.Alpha2;
    public KeyCode ability3Key = KeyCode.Alpha3;
    public KeyCode ability4Key = KeyCode.Alpha4;

    [Header("Cooldowns")]
    public float collisionCooldown = 5f;
    public float glitchCooldown = 8f;
    public float soundCooldown = 6f;
    public float windCooldown = 10f;

    [Header("Effect Ranges")]
    public float collisionRange = 3f;
    public float glitchRange = 15f;
    public float soundRange = 20f;
    public float windRange = 8f;

    [Header("Stress Values")]
    public float collisionStress = 5f;
    public float glitchStress = 8f;
    public float soundStress = 6f;
    public float windStress = 10f;

    // Cooldown timers
    private float collisionTimer = 0f;
    private float glitchTimer = 0f;
    private float soundTimer = 0f;
    private float windTimer = 0f;

    // References
    private AlienController alienController;

    // UI display
    public float Ability1Cooldown => Mathf.Max(0, collisionTimer);
    public float Ability2Cooldown => Mathf.Max(0, glitchTimer);
    public float Ability3Cooldown => Mathf.Max(0, soundTimer);
    public float Ability4Cooldown => Mathf.Max(0, windTimer);

    public bool CanUseAbility1 => collisionTimer <= 0;
    public bool CanUseAbility2 => glitchTimer <= 0;
    public bool CanUseAbility3 => soundTimer <= 0;
    public bool CanUseAbility4 => windTimer <= 0;

    void Start()
    {
        alienController = GetComponent<AlienController>();
        Debug.Log("[AlienAbilities] Initialized - Press 1,2,3,4 to use chaos powers");
        Debug.Log($"[AlienAbilities] AlienController found: {alienController != null}");
    }

    void OnEnable()
    {
        Debug.Log("[AlienAbilities] ENABLED - Abilities now active!");
    }

    void OnDisable()
    {
        Debug.Log("[AlienAbilities] DISABLED");
    }

    void Update()
    {
        // Update cooldowns
        if (collisionTimer > 0) collisionTimer -= Time.deltaTime;
        if (glitchTimer > 0) glitchTimer -= Time.deltaTime;
        if (soundTimer > 0) soundTimer -= Time.deltaTime;
        if (windTimer > 0) windTimer -= Time.deltaTime;

        // Check if we can use abilities (only during search phase)
        if (!CanUseAbilities()) return;

        // Input handling
        if (Input.GetKeyDown(ability1Key) && CanUseAbility1)
        {
            UseCollision();
        }
        if (Input.GetKeyDown(ability2Key) && CanUseAbility2)
        {
            UseGlitch();
        }
        if (Input.GetKeyDown(ability3Key) && CanUseAbility3)
        {
            UseSound();
        }
        if (Input.GetKeyDown(ability4Key) && CanUseAbility4)
        {
            UseWind();
        }
    }

    bool CanUseAbilities()
    {
        // Only usable if alien is being controlled and game is in Playing phase
        if (!AlienController.IsAlienControlled)
        {
            // Debug log every 2 seconds
            if (Time.frameCount % 120 == 0)
            {
                Debug.Log($"[AlienAbilities] CanUseAbilities FALSE - IsAlienControlled=false, ActiveAlien={(AlienController.ActiveAlien != null ? "EXISTS" : "NULL")}, enabled={alienController?.enabled}");
            }
            return false;
        }

        // Check game phase from GameManager OR NetworkGameManager
        bool isPlaying = false;
        bool isChaos = false;

        // Check NetworkGameManager first (for multiplayer)
        if (NetworkGameManager.Instance != null)
        {
            var phase = NetworkGameManager.Instance.GetCurrentPhase();
            isPlaying = (phase == NetworkGameManager.GamePhase.Playing);
            isChaos = (phase == NetworkGameManager.GamePhase.Chaos);
        }
        // Fallback to GameManager (for single player or if NetworkGameManager not present)
        else if (GameManager.Instance != null)
        {
            isPlaying = (GameManager.Instance.CurrentPhase == GameManager.GamePhase.Playing);
            isChaos = (GameManager.Instance.CurrentPhase == GameManager.GamePhase.Chaos);
        }
        else
        {
            // No manager found - allow abilities (testing mode)
            return true;
        }

        // Debug log every 2 seconds
        if (Time.frameCount % 120 == 0)
        {
            Debug.Log($"[AlienAbilities] Phase check - isPlaying={isPlaying}, isChaos={isChaos}");
        }

        // Can't use abilities during Chaos phase - you're hunting now!
        if (isChaos) return false;

        // Must be in Playing phase
        if (!isPlaying)
        {
            if (Time.frameCount % 120 == 0)
            {
                Debug.Log("[AlienAbilities] CanUseAbilities FALSE - Not in Playing phase");
            }
            return false;
        }

        return true;
    }

    // ==================== ABILITY 1: COLLISION ====================
    void UseCollision()
    {
        collisionTimer = collisionCooldown;
        Debug.Log("[AlienAbilities] COLLISION - Bumping nearby NPCs!");

        // Find nearby NPCs and make them react
        Collider[] hits = Physics.OverlapSphere(transform.position, collisionRange);
        int affectedCount = 0;

        foreach (var hit in hits)
        {
            NPCBehavior npc = hit.GetComponent<NPCBehavior>();
            if (npc != null && npc.gameObject != gameObject)
            {
                // Push NPC slightly
                Vector3 pushDir = (npc.transform.position - transform.position).normalized;
                npc.transform.position += pushDir * 0.5f;

                // Make NPC stumble/react
                npc.Panic();
                affectedCount++;
            }
        }

        // Apply stress to astronaut if nearby
        ApplyStressToAstronaut(collisionRange * 2f, collisionStress);

        // Play sound effect (networked - everyone hears it)
        if (NetworkAudioManager.Instance != null)
        {
            NetworkAudioManager.Instance.PlayBulletImpact("Metal", transform.position);
        }
        else if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX3D(AudioManager.Instance.bulletImpactMetal, transform.position);
        }

        // Visual feedback
        StartCoroutine(CollisionVisualEffect());

        Debug.Log($"[AlienAbilities] Collision affected {affectedCount} NPCs");
    }

    IEnumerator CollisionVisualEffect()
    {
        // Quick camera shake for nearby astronaut
        if (StressSystem.Instance != null)
        {
            float dist = Vector3.Distance(transform.position, StressSystem.Instance.transform.position);
            if (dist < collisionRange * 2f && CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.1f, 0.03f);
            }
        }
        yield return null;
    }

    // ==================== ABILITY 2: GLITCH ====================
    void UseGlitch()
    {
        glitchTimer = glitchCooldown;
        Debug.Log("[AlienAbilities] GLITCH - Visual disturbance!");

        // Apply stress
        ApplyStressToAstronaut(glitchRange, glitchStress);

        // Trigger visual glitch effect on astronaut's screen
        StartCoroutine(GlitchVisualEffect());
    }

    IEnumerator GlitchVisualEffect()
    {
        // Check if astronaut is in range
        Vector3 astronautPos = Vector3.zero;
        bool foundAstronaut = false;

        if (StressSystem.Instance != null)
        {
            astronautPos = StressSystem.Instance.transform.position;
            foundAstronaut = true;
        }
        else
        {
            var player = FindObjectOfType<PlayerMovement>();
            if (player != null)
            {
                astronautPos = player.transform.position;
                foundAstronaut = true;
            }
        }

        if (!foundAstronaut) yield break;

        float dist = Vector3.Distance(transform.position, astronautPos);
        if (dist > glitchRange) yield break;

        // Apply glitch via post-processing (networked)
        if (NetworkAudioManager.Instance != null)
        {
            NetworkAudioManager.Instance.TriggerGlitchEffect();
            NetworkAudioManager.Instance.TriggerCameraShake(0.3f, 0.05f);
        }
        else
        {
            if (PostProcessController.Instance != null)
            {
                PostProcessController.Instance.TriggerDamageEffect();
            }
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.3f, 0.05f);
            }
        }

        yield return null;
    }

    // ==================== ABILITY 3: SOUND ====================
    void UseSound()
    {
        soundTimer = soundCooldown;
        Debug.Log("[AlienAbilities] SOUND - Suspicious noise!");

        // Create a sound at a random nearby position (misdirection)
        Vector3 soundPos = transform.position + Random.insideUnitSphere * 5f;
        soundPos.y = transform.position.y;

        // Apply stress
        ApplyStressToAstronaut(soundRange, soundStress);

        // Play creepy sound (networked - everyone hears it)
        if (NetworkAudioManager.Instance != null)
        {
            NetworkAudioManager.Instance.PlayAlienGrowl(soundPos);
        }
        else if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayAlienGrowl(soundPos);
        }

        // Make nearby NPCs look around nervously and some panic
        Collider[] hits = Physics.OverlapSphere(soundPos, 8f);
        foreach (var hit in hits)
        {
            NPCBehavior npc = hit.GetComponent<NPCBehavior>();
            if (npc != null)
            {
                // NPC heard something - look towards sound
                npc.transform.LookAt(new Vector3(soundPos.x, npc.transform.position.y, soundPos.z));

                // 30% chance to panic
                if (Random.value < 0.3f)
                {
                    npc.Panic();
                }
            }
        }

        Debug.Log($"[AlienAbilities] Sound ability used at {soundPos}");
    }

    // ==================== ABILITY 4: WIND ====================
    void UseWind()
    {
        windTimer = windCooldown;
        Debug.Log("[AlienAbilities] WIND - Environmental disturbance!");

        // Apply stress (highest value)
        ApplyStressToAstronaut(windRange, windStress);

        // Play power down sound (networked - everyone hears it)
        if (NetworkAudioManager.Instance != null)
        {
            NetworkAudioManager.Instance.PlayPowerDown();
        }
        else if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayPowerDown();
        }

        // Affect all NPCs in range - make them stumble and panic
        Collider[] hits = Physics.OverlapSphere(transform.position, windRange);
        int affectedCount = 0;
        foreach (var hit in hits)
        {
            NPCBehavior npc = hit.GetComponent<NPCBehavior>();
            if (npc != null && npc.gameObject != gameObject)
            {
                // Random push direction
                Vector3 pushDir = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
                npc.transform.position += pushDir * 0.3f;

                // 50% chance to panic
                if (Random.value < 0.5f)
                {
                    npc.Panic();
                }
                affectedCount++;
            }
        }

        // Visual effect - dust/particles
        StartCoroutine(WindVisualEffect());

        // Flicker lights briefly
        StartCoroutine(FlickerLights());

        Debug.Log($"[AlienAbilities] Wind affected {affectedCount} NPCs");
    }

    IEnumerator WindVisualEffect()
    {
        // Create simple particle burst
        GameObject windFx = new GameObject("WindEffect");
        windFx.transform.position = transform.position;

        ParticleSystem ps = windFx.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = 1f;
        main.startSpeed = 5f;
        main.startSize = 0.1f;
        main.startColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        main.maxParticles = 50;
        main.gravityModifier = -0.1f;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 30) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = windRange;

        var renderer = windFx.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));

        ps.Play();
        Destroy(windFx, 2f);

        yield return null;
    }

    IEnumerator FlickerLights()
    {
        // Find all lights in range and flicker them
        Light[] lights = FindObjectsOfType<Light>();

        for (int i = 0; i < 3; i++)
        {
            foreach (var light in lights)
            {
                if (Vector3.Distance(light.transform.position, transform.position) < windRange * 2f)
                {
                    light.enabled = false;
                }
            }

            yield return new WaitForSeconds(0.1f);

            foreach (var light in lights)
            {
                light.enabled = true;
            }

            yield return new WaitForSeconds(0.1f);
        }
    }

    // ==================== HELPER ====================
    void ApplyStressToAstronaut(float range, float stressAmount)
    {
        // Try to find astronaut position for distance check
        Vector3 astronautPos = Vector3.zero;
        bool foundAstronaut = false;

        if (StressSystem.Instance != null)
        {
            astronautPos = StressSystem.Instance.transform.position;
            foundAstronaut = true;
        }
        else
        {
            // Try to find via PlayerMovement
            var player = FindObjectOfType<PlayerMovement>();
            if (player != null)
            {
                astronautPos = player.transform.position;
                foundAstronaut = true;
            }
        }

        if (!foundAstronaut) return;

        float dist = Vector3.Distance(transform.position, astronautPos);
        if (dist <= range)
        {
            // Stress decreases with distance
            float stressMultiplier = 1f - (dist / range) * 0.5f;
            float finalStress = stressAmount * stressMultiplier;

            // Use networked stress application
            if (NetworkAudioManager.Instance != null)
            {
                NetworkAudioManager.Instance.ApplyStressToAstronaut(finalStress);
            }
            else if (StressSystem.Instance != null)
            {
                StressSystem.Instance.AddStress(finalStress);
            }

            // Notify game manager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnChaosEventTriggered(transform.position);
            }
        }
    }

    // ==================== UI HELPER ====================
    void OnGUI()
    {
        // CRITICAL: Check if this component is enabled (means we're the local alien player)
        if (!enabled) return;

        // If AlienController.IsAlienControlled is false but we're enabled, show debug info
        if (!AlienController.IsAlienControlled)
        {
            // Debug: Show why abilities might not work
            GUIStyle debugStyle = new GUIStyle(GUI.skin.label);
            debugStyle.fontSize = 12;
            debugStyle.normal.textColor = Color.yellow;
            GUI.Label(new Rect(10, Screen.height - 180, 300, 60),
                $"[DEBUG] AlienAbilities enabled but:\n" +
                $"ActiveAlien={(AlienController.ActiveAlien != null ? AlienController.ActiveAlien.gameObject.name : "NULL")}\n" +
                $"AlienController.enabled={(alienController != null ? alienController.enabled.ToString() : "null")}",
                debugStyle);

            // Still show abilities UI even if IsAlienControlled is false
            // This helps debug the issue
        }

        // Check if in chaos mode (don't show abilities UI during chaos)
        bool isChaos = false;
        if (NetworkGameManager.Instance != null)
        {
            isChaos = (NetworkGameManager.Instance.GetCurrentPhase() == NetworkGameManager.GamePhase.Chaos);
        }
        else if (GameManager.Instance != null)
        {
            isChaos = (GameManager.Instance.CurrentPhase == GameManager.GamePhase.Chaos);
        }

        if (isChaos) return;

        // Background box for better visibility
        float boxWidth = 220;
        float boxHeight = 130;
        float x = 15;
        float y = Screen.height - boxHeight - 15;

        // Semi-transparent background
        GUI.Box(new Rect(x - 5, y - 5, boxWidth, boxHeight), "");

        // Title
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 18;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = new Color(1f, 0.5f, 0f); // Orange
        GUI.Label(new Rect(x, y, boxWidth, 25), "POUVOIRS CHAOS", titleStyle);

        // Ability styles
        GUIStyle readyStyle = new GUIStyle(GUI.skin.label);
        readyStyle.fontSize = 16;
        readyStyle.fontStyle = FontStyle.Bold;
        readyStyle.normal.textColor = Color.green;

        GUIStyle cooldownStyle = new GUIStyle(GUI.skin.label);
        cooldownStyle.fontSize = 16;
        cooldownStyle.normal.textColor = Color.gray;

        float lineY = y + 28;
        float lineHeight = 22;

        // Ability 1 - Collision
        DrawAbilityLine(x, lineY, "[1] Collision", CanUseAbility1, collisionTimer, readyStyle, cooldownStyle);
        lineY += lineHeight;

        // Ability 2 - Glitch
        DrawAbilityLine(x, lineY, "[2] Glitch", CanUseAbility2, glitchTimer, readyStyle, cooldownStyle);
        lineY += lineHeight;

        // Ability 3 - Sound
        DrawAbilityLine(x, lineY, "[3] Son", CanUseAbility3, soundTimer, readyStyle, cooldownStyle);
        lineY += lineHeight;

        // Ability 4 - Wind
        DrawAbilityLine(x, lineY, "[4] Vent", CanUseAbility4, windTimer, readyStyle, cooldownStyle);
    }

    void DrawAbilityLine(float x, float y, string name, bool ready, float timer, GUIStyle readyStyle, GUIStyle cooldownStyle)
    {
        if (ready)
        {
            GUI.Label(new Rect(x, y, 200, 22), $"{name}: PRÊT", readyStyle);
        }
        else
        {
            GUI.Label(new Rect(x, y, 200, 22), $"{name}: {timer:F1}s", cooldownStyle);
        }
    }
}
