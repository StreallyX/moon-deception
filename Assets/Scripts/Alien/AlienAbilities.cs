using UnityEngine;
using System.Collections;
using Unity.Netcode;

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
    public float teleportCooldown = 12f;

    [Header("Effect Ranges")]
    public float collisionRange = 3f;
    public float glitchRange = 15f;
    public float soundRange = 20f;
    public float teleportRange = 15f; // Range to find NPCs to swap with

    [Header("Stress Values")]
    public float collisionStress = 5f;
    public float glitchStress = 8f;
    public float soundStress = 6f;
    public float teleportStress = 4f;

    // Cooldown timers
    private float collisionTimer = 0f;
    private float glitchTimer = 0f;
    private float soundTimer = 0f;
    private float teleportTimer = 0f;

    // References
    private AlienController alienController;

    // UI display
    public float Ability1Cooldown => Mathf.Max(0, collisionTimer);
    public float Ability2Cooldown => Mathf.Max(0, glitchTimer);
    public float Ability3Cooldown => Mathf.Max(0, soundTimer);
    public float Ability4Cooldown => Mathf.Max(0, teleportTimer);

    public bool CanUseAbility1 => collisionTimer <= 0;
    public bool CanUseAbility2 => glitchTimer <= 0;
    public bool CanUseAbility3 => soundTimer <= 0;
    public bool CanUseAbility4 => teleportTimer <= 0;

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
        if (teleportTimer > 0) teleportTimer -= Time.deltaTime;

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
            UseTeleport();
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
        bool isNetworked = NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;

        foreach (var hit in hits)
        {
            NPCBehavior npc = hit.GetComponent<NPCBehavior>();
            if (npc == null) npc = hit.GetComponentInParent<NPCBehavior>();
            if (npc != null && npc.gameObject != gameObject)
            {
                // Push NPC slightly (local visual feedback - server will validate via NPC's NetworkTransform)
                Vector3 pushDir = (npc.transform.position - transform.position).normalized;
                npc.transform.position += pushDir * 0.5f;

                // Make NPC panic - use RPC if networked
                if (isNetworked && npc.IsSpawned)
                {
                    npc.PanicServerRpc();
                }
                else
                {
                    npc.Panic();
                }
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
        bool isNetworked = NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;
        Collider[] hits = Physics.OverlapSphere(soundPos, 8f);
        foreach (var hit in hits)
        {
            NPCBehavior npc = hit.GetComponent<NPCBehavior>();
            if (npc == null) npc = hit.GetComponentInParent<NPCBehavior>();
            if (npc != null)
            {
                // NPC heard something - look towards sound
                npc.transform.LookAt(new Vector3(soundPos.x, npc.transform.position.y, soundPos.z));

                // 30% chance to panic - use RPC if networked
                if (Random.value < 0.3f)
                {
                    if (isNetworked && npc.IsSpawned)
                    {
                        npc.PanicServerRpc();
                    }
                    else
                    {
                        npc.Panic();
                    }
                }
            }
        }

        Debug.Log($"[AlienAbilities] Sound ability used at {soundPos}");
    }

    // ==================== ABILITY 4: TELEPORT ====================
    void UseTeleport()
    {
        // Find a nearby NPC to swap with
        NPCBehavior targetNPC = FindNPCToSwapWith();

        if (targetNPC == null)
        {
            Debug.Log("[AlienAbilities] TELEPORT - No NPC found nearby to swap with!");
            // Don't consume cooldown if no target found
            return;
        }

        teleportTimer = teleportCooldown;
        Debug.Log($"[AlienAbilities] TELEPORT - Swapping with {targetNPC.name}!");

        // Store positions
        Vector3 alienPos = transform.position;
        Vector3 npcPos = targetNPC.transform.position;
        Quaternion alienRot = transform.rotation;
        Quaternion npcRot = targetNPC.transform.rotation;

        // Play teleport effects at BOTH positions (networked - everyone sees particles)
        if (NetworkAudioManager.Instance != null)
        {
            NetworkAudioManager.Instance.UseTeleportAbility(alienPos, npcPos);
        }
        else
        {
            // Local fallback - create particle effects
            CreateTeleportEffect(alienPos);
            CreateTeleportEffect(npcPos);
            AudioManager.Instance?.PlayPowerDown();
        }

        // NETWORK: Sync position via RPC if in multiplayer
        var networkedPlayer = GetComponent<NetworkedPlayer>();
        if (networkedPlayer != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
        {
            // Get NPC's NetworkObject ID for server to find it
            var npcNetObj = targetNPC.GetComponent<NetworkObject>();
            ulong npcNetworkId = npcNetObj != null ? npcNetObj.NetworkObjectId : 0;

            // Send teleport to server - server will sync positions
            networkedPlayer.TeleportSwapServerRpc(npcPos, npcRot, npcNetworkId);
            Debug.Log($"[AlienAbilities] Sent TeleportSwapServerRpc to position {npcPos}");
        }
        else
        {
            // Single player fallback - local teleport
            PerformLocalTeleport(npcPos, npcRot, targetNPC, alienPos, alienRot);
        }

        // Apply small stress to astronaut (they might notice something weird)
        ApplyStressToAstronaut(teleportRange * 2f, teleportStress);

        // Make the swapped NPC act confused (use RPC if available)
        if (targetNPC.IsSpawned && NetworkManager.Singleton != null)
        {
            targetNPC.PanicServerRpc();
        }
        else
        {
            targetNPC.Panic();
        }

        Debug.Log($"[AlienAbilities] Teleported from {alienPos} to {npcPos}");
    }

    /// <summary>
    /// Perform local teleport (single player or called after network sync)
    /// </summary>
    public void PerformLocalTeleport(Vector3 newPos, Quaternion newRot, NPCBehavior targetNPC, Vector3 oldAlienPos, Quaternion oldAlienRot)
    {
        // Disable CharacterController temporarily for teleport
        var charController = GetComponent<CharacterController>();
        bool wasEnabled = charController != null && charController.enabled;
        if (charController != null) charController.enabled = false;

        // Swap positions!
        transform.position = newPos + Vector3.up * 0.1f; // Slight offset to avoid ground clipping
        if (targetNPC != null)
        {
            targetNPC.transform.position = oldAlienPos;
            targetNPC.transform.rotation = oldAlienRot;
        }

        // Optionally swap rotations too (makes it more confusing)
        transform.rotation = newRot;

        // Re-enable CharacterController
        if (charController != null) charController.enabled = wasEnabled;
    }

    /// <summary>
    /// Find a random NPC within range to swap positions with
    /// </summary>
    NPCBehavior FindNPCToSwapWith()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, teleportRange);
        System.Collections.Generic.List<NPCBehavior> validNPCs = new System.Collections.Generic.List<NPCBehavior>();

        foreach (var hit in hits)
        {
            NPCBehavior npc = hit.GetComponent<NPCBehavior>();
            if (npc == null) npc = hit.GetComponentInParent<NPCBehavior>();

            if (npc != null && npc.gameObject != gameObject && !npc.IsDead)
            {
                // Check NPC is not too close (would be obvious) or too far
                float dist = Vector3.Distance(transform.position, npc.transform.position);
                if (dist > 3f) // At least 3m away for meaningful teleport
                {
                    validNPCs.Add(npc);
                }
            }
        }

        if (validNPCs.Count == 0) return null;

        // Pick a random NPC from the valid list
        return validNPCs[Random.Range(0, validNPCs.Count)];
    }

    /// <summary>
    /// Create local teleport particle effect
    /// </summary>
    void CreateTeleportEffect(Vector3 position)
    {
        GameObject fx = new GameObject("TeleportEffect");
        fx.transform.position = position + Vector3.up;

        var ps = fx.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = 0.8f;
        main.startSpeed = 5f;
        main.startSize = 0.3f;
        main.startColor = new Color(0.5f, 0f, 1f, 0.9f); // Purple particles
        main.maxParticles = 50;
        main.gravityModifier = -0.5f; // Float upward

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 40) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;

        var renderer = fx.GetComponent<ParticleSystemRenderer>();
        // Use a safe material
        Shader shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader != null) renderer.material = new Material(shader);

        ps.Play();
        Destroy(fx, 2f);
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
            var player = FindFirstObjectByType<PlayerMovement>();
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

        // Don't show UI if game ended
        if (GameManager.Instance != null && GameManager.Instance.CurrentPhase == GameManager.GamePhase.Ended) return;

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

        DrawAbilityWithBar(x, lineY, "4", "TELEPORT", CanUseAbility4, teleportTimer, teleportCooldown, new Color(0.7f, 0.3f, 1f));
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
