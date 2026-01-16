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
            if (npc == null) npc = hit.GetComponentInParent<NPCBehavior>();
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

        // Play networked effects (everyone sees/hears)
        if (NetworkAudioManager.Instance != null)
        {
            NetworkAudioManager.Instance.UseCollisionAbility(transform.position);
        }
        else if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX3D(AudioManager.Instance.bulletImpactMetal, transform.position);
        }

        Debug.Log($"[AlienAbilities] Collision affected {affectedCount} NPCs");
    }

    // ==================== ABILITY 2: GLITCH ====================
    void UseGlitch()
    {
        glitchTimer = glitchCooldown;
        Debug.Log("[AlienAbilities] GLITCH - Visual disturbance!");

        // Apply stress
        ApplyStressToAstronaut(glitchRange, glitchStress);

        // Play networked effects (everyone sees visual, astronaut gets post-process)
        if (NetworkAudioManager.Instance != null)
        {
            NetworkAudioManager.Instance.UseGlitchAbility(transform.position);
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

        // Play networked sound effects (everyone hears and sees indicator)
        if (NetworkAudioManager.Instance != null)
        {
            NetworkAudioManager.Instance.UseSoundAbility(soundPos);
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
            if (npc == null) npc = hit.GetComponentInParent<NPCBehavior>();
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

        // Play networked effects (everyone sees particles and light flicker)
        if (NetworkAudioManager.Instance != null)
        {
            NetworkAudioManager.Instance.UseWindAbility(transform.position);
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
            if (npc == null) npc = hit.GetComponentInParent<NPCBehavior>();
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

        Debug.Log($"[AlienAbilities] Wind affected {affectedCount} NPCs");
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

    // Textures for UI
    private Texture2D bgTexture;
    private Texture2D cooldownBgTexture;
    private Texture2D cooldownFillTexture;

    void CreateUITextures()
    {
        if (bgTexture == null)
        {
            bgTexture = new Texture2D(1, 1);
            bgTexture.SetPixel(0, 0, new Color(0.1f, 0.05f, 0.15f, 0.85f));
            bgTexture.Apply();
        }

        if (cooldownBgTexture == null)
        {
            cooldownBgTexture = new Texture2D(1, 1);
            cooldownBgTexture.SetPixel(0, 0, new Color(0.2f, 0.2f, 0.2f, 0.8f));
            cooldownBgTexture.Apply();
        }

        if (cooldownFillTexture == null)
        {
            cooldownFillTexture = new Texture2D(1, 1);
            cooldownFillTexture.SetPixel(0, 0, new Color(0.8f, 0.3f, 1f, 0.9f));
            cooldownFillTexture.Apply();
        }
    }

    void OnGUI()
    {
        // CRITICAL: Check if this component is enabled (means we're the local alien player)
        if (!enabled) return;

        // Create textures if needed
        CreateUITextures();

        // Check if in chaos mode (don't show abilities UI during chaos - show attack info instead)
        bool isChaos = false;
        if (NetworkGameManager.Instance != null)
        {
            isChaos = (NetworkGameManager.Instance.GetCurrentPhase() == NetworkGameManager.GamePhase.Chaos);
        }
        else if (GameManager.Instance != null)
        {
            isChaos = (GameManager.Instance.CurrentPhase == GameManager.GamePhase.Chaos);
        }

        if (isChaos)
        {
            DrawChaosUI();
            return;
        }

        // ========== ABILITIES UI - BOTTOM RIGHT ==========
        float boxWidth = 200;
        float boxHeight = 160;
        float padding = 15;
        float x = Screen.width - boxWidth - padding;
        float y = Screen.height - boxHeight - padding;

        // Background
        GUI.DrawTexture(new Rect(x - 10, y - 10, boxWidth + 20, boxHeight + 20), bgTexture);

        // Title
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 16;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = new Color(0.9f, 0.5f, 1f);
        titleStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(x, y, boxWidth, 22), "POUVOIRS ALIEN", titleStyle);

        float lineY = y + 28;
        float lineHeight = 32;

        // Draw each ability with cooldown bar
        DrawAbilityWithBar(x, lineY, "1", "COLLISION", CanUseAbility1, collisionTimer, collisionCooldown, new Color(1f, 0.4f, 0.4f));
        lineY += lineHeight;

        DrawAbilityWithBar(x, lineY, "2", "GLITCH", CanUseAbility2, glitchTimer, glitchCooldown, new Color(0.4f, 0.8f, 1f));
        lineY += lineHeight;

        DrawAbilityWithBar(x, lineY, "3", "SON", CanUseAbility3, soundTimer, soundCooldown, new Color(1f, 0.9f, 0.3f));
        lineY += lineHeight;

        DrawAbilityWithBar(x, lineY, "4", "VENT", CanUseAbility4, windTimer, windCooldown, new Color(0.5f, 1f, 0.5f));
    }

    void DrawAbilityWithBar(float x, float y, string key, string name, bool ready, float timer, float maxCooldown, Color abilityColor)
    {
        float barWidth = 180;
        float barHeight = 8;
        float keyBoxSize = 22;

        // Key box
        GUIStyle keyStyle = new GUIStyle(GUI.skin.box);
        keyStyle.fontSize = 14;
        keyStyle.fontStyle = FontStyle.Bold;
        keyStyle.alignment = TextAnchor.MiddleCenter;
        keyStyle.normal.textColor = ready ? Color.white : Color.gray;

        GUI.backgroundColor = ready ? abilityColor : new Color(0.3f, 0.3f, 0.3f);
        GUI.Box(new Rect(x, y, keyBoxSize, keyBoxSize), key, keyStyle);
        GUI.backgroundColor = Color.white;

        // Ability name
        GUIStyle nameStyle = new GUIStyle(GUI.skin.label);
        nameStyle.fontSize = 12;
        nameStyle.fontStyle = FontStyle.Bold;
        nameStyle.normal.textColor = ready ? abilityColor : Color.gray;
        GUI.Label(new Rect(x + keyBoxSize + 5, y, 80, 18), name, nameStyle);

        // Status text
        GUIStyle statusStyle = new GUIStyle(GUI.skin.label);
        statusStyle.fontSize = 10;
        statusStyle.alignment = TextAnchor.MiddleRight;
        statusStyle.normal.textColor = ready ? Color.green : Color.gray;

        string statusText = ready ? "PRÊT" : $"{timer:F1}s";
        GUI.Label(new Rect(x + barWidth - 40, y, 40, 18), statusText, statusStyle);

        // Cooldown bar background
        float barY = y + 18;
        GUI.DrawTexture(new Rect(x, barY, barWidth, barHeight), cooldownBgTexture);

        // Cooldown bar fill
        float fillPercent = ready ? 1f : 1f - (timer / maxCooldown);
        if (fillPercent > 0)
        {
            Texture2D fillTex = new Texture2D(1, 1);
            fillTex.SetPixel(0, 0, ready ? abilityColor : new Color(abilityColor.r * 0.5f, abilityColor.g * 0.5f, abilityColor.b * 0.5f, 0.8f));
            fillTex.Apply();
            GUI.DrawTexture(new Rect(x, barY, barWidth * fillPercent, barHeight), fillTex);
        }
    }

    void DrawChaosUI()
    {
        // During chaos, show attack info instead
        float boxWidth = 180;
        float boxHeight = 60;
        float padding = 15;
        float x = Screen.width - boxWidth - padding;
        float y = Screen.height - boxHeight - padding;

        // Background
        GUI.DrawTexture(new Rect(x - 10, y - 10, boxWidth + 20, boxHeight + 20), bgTexture);

        // Title
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 18;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.red;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(x, y, boxWidth, 25), "MODE CHAOS", titleStyle);

        // Attack info
        GUIStyle infoStyle = new GUIStyle(GUI.skin.label);
        infoStyle.fontSize = 14;
        infoStyle.normal.textColor = new Color(1f, 0.8f, 0.8f);
        infoStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(x, y + 28, boxWidth, 20), "CLIC GAUCHE = Attaque", infoStyle);
    }
}
