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
        if (!AlienController.IsAlienControlled) return false;

        if (GameManager.Instance != null)
        {
            // Can't use abilities during Chaos phase - you're hunting now!
            if (GameManager.Instance.CurrentPhase == GameManager.GamePhase.Chaos)
                return false;
            if (GameManager.Instance.CurrentPhase != GameManager.GamePhase.Playing)
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

        // Play sound effect
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX3D(AudioManager.Instance.bulletImpactMetal, transform.position);
        }

        // Visual feedback
        StartCoroutine(CollisionVisualEffect());
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

        // Play creepy sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX3D(AudioManager.Instance.npcDeath, soundPos, 1.5f);
        }

        // Make nearby NPCs look around nervously
        Collider[] hits = Physics.OverlapSphere(soundPos, 8f);
        foreach (var hit in hits)
        {
            NPCBehavior npc = hit.GetComponent<NPCBehavior>();
            if (npc != null)
            {
                // NPC heard something - look towards sound
                npc.transform.LookAt(new Vector3(soundPos.x, npc.transform.position.y, soundPos.z));
            }
        }
    }

    // ==================== ABILITY 4: WIND ====================
    void UseWind()
    {
        windTimer = windCooldown;
        Debug.Log("[AlienAbilities] WIND - Environmental disturbance!");

        // Apply stress (highest value)
        ApplyStressToAstronaut(windRange, windStress);

        // Affect all NPCs in range - make them stumble
        Collider[] hits = Physics.OverlapSphere(transform.position, windRange);
        foreach (var hit in hits)
        {
            NPCBehavior npc = hit.GetComponent<NPCBehavior>();
            if (npc != null && npc.gameObject != gameObject)
            {
                // Random push direction
                Vector3 pushDir = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
                npc.transform.position += pushDir * 0.3f;
            }
        }

        // Visual effect - dust/particles
        StartCoroutine(WindVisualEffect());

        // Flicker lights briefly
        StartCoroutine(FlickerLights());
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
        if (!AlienController.IsAlienControlled) return;

        // Show ability cooldowns
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 16;
        style.normal.textColor = Color.white;

        float y = Screen.height - 120;
        float x = 20;

        GUI.Label(new Rect(x, y, 200, 25), $"[1] Collision: {(CanUseAbility1 ? "READY" : collisionTimer.ToString("F1") + "s")}", style);
        GUI.Label(new Rect(x, y + 25, 200, 25), $"[2] Glitch: {(CanUseAbility2 ? "READY" : glitchTimer.ToString("F1") + "s")}", style);
        GUI.Label(new Rect(x, y + 50, 200, 25), $"[3] Sound: {(CanUseAbility3 ? "READY" : soundTimer.ToString("F1") + "s")}", style);
        GUI.Label(new Rect(x, y + 75, 200, 25), $"[4] Wind: {(CanUseAbility4 ? "READY" : windTimer.ToString("F1") + "s")}", style);
    }
}
