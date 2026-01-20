using UnityEngine;
using System.Collections;

/// <summary>
/// Alarm terminal that aliens can activate to stress the astronaut.
/// Triggers an alarm that adds stress if astronaut is in range.
/// Also panics nearby NPCs.
/// </summary>
public class AlarmTerminal : Interactable
{
    [Header("Alarm Settings")]
    public float stressAmount = 10f;
    public float alarmRadius = 30f;
    public float alarmDuration = 5f;
    public float alarmCooldown = 30f;

    [Header("Effects")]
    public Light alarmLight;
    public Color alarmColor = new Color(1f, 0.2f, 0.2f);
    public Color idleColor = new Color(0.2f, 0.2f, 0.8f);

    private bool isAlarming = false;
    private MeshRenderer terminalRenderer;
    private Material terminalMaterial;

    protected override void Start()
    {
        // Set up as alien-only
        astronautCanUse = false;
        alienCanUse = true;
        cooldownTime = alarmCooldown;
        interactionPrompt = "Press E to trigger alarm";
        highlightColor = idleColor;

        base.Start();

        SetupVisuals();
    }

    void SetupVisuals()
    {
        // Get renderer from prefab (should already exist)
        terminalRenderer = GetComponentInChildren<MeshRenderer>();

        if (terminalRenderer != null)
        {
            terminalMaterial = terminalRenderer.material;
        }

        // Get or create light for visual feedback
        alarmLight = GetComponentInChildren<Light>();
        if (alarmLight == null)
        {
            GameObject lightObj = new GameObject("AlarmLight");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = Vector3.up * 2f;

            alarmLight = lightObj.AddComponent<Light>();
            alarmLight.type = LightType.Point;
            alarmLight.range = 10f;
            alarmLight.intensity = 0.5f;
            alarmLight.color = alarmColor;
        }

        UpdateVisuals();
    }

    protected override void UpdateVisuals()
    {
        if (alarmLight == null) return;

        if (isAlarming)
        {
            // Flashing alarm light handled by coroutine
            return;
        }

        if (CanUse)
        {
            alarmLight.color = idleColor;
            alarmLight.intensity = 0.5f + Mathf.PingPong(Time.time * 0.5f, 0.3f);
        }
        else
        {
            // Cooldown state
            float cooldownPercent = CooldownRemaining / cooldownTime;
            alarmLight.intensity = 0.2f;
            alarmLight.color = Color.Lerp(idleColor, Color.gray, cooldownPercent);
        }
    }

    protected override void OnInteract(bool isAlien)
    {
        if (!isAlien)
        {
            Debug.Log("[AlarmTerminal] Astronaut cannot use alarm terminal");
            return;
        }

        if (!CanUse || isAlarming)
        {
            Debug.Log($"[AlarmTerminal] On cooldown. {CooldownRemaining:F1}s remaining");
            return;
        }

        StartCoroutine(TriggerAlarm());
    }

    IEnumerator TriggerAlarm()
    {
        isAlarming = true;
        MarkAsUsed();

        Debug.Log("[AlarmTerminal] ALARM TRIGGERED!");

        // Play alarm trigger sound (networked - all players hear it)
        if (NetworkAudioManager.Instance != null)
        {
            NetworkAudioManager.Instance.PlayTerminalBeep(transform.position);
            NetworkAudioManager.Instance.PlayAlarmTrigger(transform.position);
        }
        else if (AudioManager.Instance != null)
        {
            // Fallback for single player
            AudioManager.Instance.PlayTerminalBeep(transform.position);
            AudioManager.Instance.PlayAlarmTrigger(transform.position);
        }

        // Show feedback
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.ShowInteractionPrompt("ALARM ACTIVATED!");
        }

        // Start visual effects
        StartCoroutine(FlashAlarmLight());

        // Add stress to astronaut if in range
        StressAstronautIfInRange();

        // Panic nearby NPCs
        PanicNearbyNPCs();

        // Wait for alarm duration
        yield return new WaitForSeconds(alarmDuration);

        isAlarming = false;

        // Reset light
        if (alarmLight != null)
        {
            alarmLight.intensity = 0.5f;
            alarmLight.color = idleColor;
        }

        // Hide prompt
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.HideInteractionPrompt();
        }

        Debug.Log("[AlarmTerminal] Alarm ended");
    }

    void StressAstronautIfInRange()
    {
        StressSystem stress = StressSystem.Instance;
        if (stress == null)
        {
            stress = FindObjectOfType<StressSystem>();
        }

        if (stress != null)
        {
            float distance = Vector3.Distance(transform.position, stress.transform.position);

            if (distance <= alarmRadius)
            {
                // Full stress if close, reduced stress at edge
                float distanceMultiplier = 1f - (distance / alarmRadius) * 0.5f;
                float actualStress = stressAmount * distanceMultiplier;

                stress.AddStress(actualStress);
                Debug.Log($"[AlarmTerminal] Astronaut stressed! +{actualStress:F1} (distance: {distance:F1}m)");
            }
            else
            {
                Debug.Log($"[AlarmTerminal] Astronaut too far ({distance:F1}m > {alarmRadius}m)");
            }
        }
    }

    void PanicNearbyNPCs()
    {
        NPCBehavior[] npcs = FindObjectsOfType<NPCBehavior>();

        int panickedCount = 0;
        foreach (var npc in npcs)
        {
            if (npc == null) continue;

            float distance = Vector3.Distance(transform.position, npc.transform.position);
            if (distance <= alarmRadius)
            {
                npc.Panic();
                panickedCount++;
            }
        }

        Debug.Log($"[AlarmTerminal] {panickedCount} NPCs panicked");
    }

    IEnumerator FlashAlarmLight()
    {
        float elapsed = 0f;

        while (elapsed < alarmDuration && alarmLight != null)
        {
            // Toggle light
            alarmLight.enabled = !alarmLight.enabled;
            alarmLight.intensity = alarmLight.enabled ? 3f : 0f;
            alarmLight.color = alarmColor;

            yield return new WaitForSeconds(0.3f);
            elapsed += 0.3f;
        }

        // Ensure light ends in proper state
        if (alarmLight != null)
        {
            alarmLight.enabled = true;
            alarmLight.intensity = 0.5f;
        }
    }

    // ==================== GIZMOS ====================

    void OnDrawGizmos()
    {
        // Draw terminal
        Gizmos.color = idleColor;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.75f, new Vector3(0.5f, 1.5f, 0.3f));

        // Draw interaction range
        Gizmos.color = highlightColor;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }

    void OnDrawGizmosSelected()
    {
        // Draw alarm radius
        Gizmos.color = new Color(alarmColor.r, alarmColor.g, alarmColor.b, 0.2f);
        Gizmos.DrawSphere(transform.position, alarmRadius);

        Gizmos.color = alarmColor;
        Gizmos.DrawWireSphere(transform.position, alarmRadius);

        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 3f, $"Alarm Terminal\nRadius: {alarmRadius}m\nStress: +{stressAmount}");
        #endif
    }
}
