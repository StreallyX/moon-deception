using UnityEngine;

/// <summary>
/// Coffee machine that aliens can use to restore hunger.
/// Only usable by aliens, not astronauts.
/// </summary>
public class CoffeeMachine : Interactable
{
    [Header("Coffee Settings")]
    public float hungerRestoreAmount = 40f;
    public float coffeeCooldown = 10f;

    [Header("Effects")]
    public ParticleSystem steamParticles;
    public Light machineLight;
    public Color activeColor = new Color(0.2f, 0.8f, 0.2f);
    public Color cooldownColor = new Color(0.8f, 0.2f, 0.2f);

    private MeshRenderer machineRenderer;
    private Material machineMaterial;

    protected override void Start()
    {
        // Set up as alien-only
        astronautCanUse = false;
        alienCanUse = true;
        cooldownTime = coffeeCooldown;
        interactionPrompt = "Press E to drink coffee";
        highlightColor = new Color(0.6f, 0.4f, 0.2f); // Coffee brown

        base.Start();

        SetupVisuals();
    }

    void SetupVisuals()
    {
        // Create visual if none exists
        machineRenderer = GetComponent<MeshRenderer>();

        if (machineRenderer == null)
        {
            // Create a simple coffee machine placeholder
            GameObject meshObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            meshObj.transform.SetParent(transform);
            meshObj.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            meshObj.transform.localScale = new Vector3(0.8f, 1.2f, 0.6f);

            // Remove collider from mesh (we have our own trigger)
            var meshCollider = meshObj.GetComponent<Collider>();
            if (meshCollider != null) Destroy(meshCollider);

            machineRenderer = meshObj.GetComponent<MeshRenderer>();
        }

        // Create material
        machineMaterial = new Material(Shader.Find("Standard"));
        machineMaterial.color = highlightColor;
        machineRenderer.material = machineMaterial;

        // Create light if none
        if (machineLight == null)
        {
            GameObject lightObj = new GameObject("MachineLight");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = Vector3.up * 1.5f;

            machineLight = lightObj.AddComponent<Light>();
            machineLight.type = LightType.Point;
            machineLight.range = 3f;
            machineLight.intensity = 1f;
            machineLight.color = activeColor;
        }

        UpdateVisuals();
    }

    protected override void UpdateVisuals()
    {
        if (machineLight == null) return;

        if (CanUse)
        {
            machineLight.color = activeColor;
            machineLight.intensity = 1f + Mathf.PingPong(Time.time, 0.3f);

            if (machineMaterial != null)
            {
                machineMaterial.color = highlightColor;
            }
        }
        else
        {
            // Show cooldown state
            float cooldownPercent = CooldownRemaining / cooldownTime;
            machineLight.color = Color.Lerp(activeColor, cooldownColor, cooldownPercent);
            machineLight.intensity = 0.5f;

            if (machineMaterial != null)
            {
                machineMaterial.color = Color.Lerp(highlightColor, Color.gray, cooldownPercent);
            }
        }
    }

    protected override void OnInteract(bool isAlien)
    {
        if (!isAlien)
        {
            Debug.Log("[CoffeeMachine] Astronaut cannot use coffee machine");
            return;
        }

        if (!CanUse)
        {
            Debug.Log($"[CoffeeMachine] On cooldown. {CooldownRemaining:F1}s remaining");
            return;
        }

        // Find alien's hunger system
        HungerSystem hunger = FindObjectOfType<HungerSystem>();
        if (hunger != null)
        {
            hunger.AddHunger(hungerRestoreAmount);
            MarkAsUsed();

            Debug.Log($"[CoffeeMachine] Alien drank coffee! +{hungerRestoreAmount} hunger");

            // Visual effects
            PlayCoffeeEffects();

            // Show feedback
            if (GameUIManager.Instance != null)
            {
                GameUIManager.Instance.ShowInteractionPrompt($"+{hungerRestoreAmount} Hunger!");
            }

            // Hide prompt after short delay
            StartCoroutine(HidePromptDelayed());
        }
        else
        {
            Debug.LogWarning("[CoffeeMachine] Could not find HungerSystem!");
        }
    }

    void PlayCoffeeEffects()
    {
        // Play steam particles
        if (steamParticles != null)
        {
            steamParticles.Play();
        }

        // Play sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayUIClick(); // Placeholder - ideally a coffee/liquid sound
        }

        // Flash light
        if (machineLight != null)
        {
            StartCoroutine(FlashLight());
        }
    }

    System.Collections.IEnumerator FlashLight()
    {
        Color originalColor = machineLight.color;
        float originalIntensity = machineLight.intensity;

        machineLight.color = Color.white;
        machineLight.intensity = 3f;

        yield return new WaitForSeconds(0.2f);

        machineLight.color = originalColor;
        machineLight.intensity = originalIntensity;
    }

    System.Collections.IEnumerator HidePromptDelayed()
    {
        yield return new WaitForSeconds(1.5f);

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.HideInteractionPrompt();
        }
    }

    // ==================== GIZMOS ====================

    void OnDrawGizmos()
    {
        Gizmos.color = highlightColor;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.6f, new Vector3(0.8f, 1.2f, 0.6f));
        Gizmos.DrawWireSphere(transform.position, interactionRange);

        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, "Coffee Machine");
        #endif
    }
}
