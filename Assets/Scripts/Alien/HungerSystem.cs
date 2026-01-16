using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Alien hunger system.
/// When hunger reaches 0, the alien doesn't die but reveals itself:
/// - Makes hungry/growling sounds
/// - Visual glitches showing alien form
/// - Gives away position to astronaut
/// </summary>
public class HungerSystem : MonoBehaviour
{
    [Header("Hunger Settings")]
    public float maxHunger = 100f;
    public float currentHunger = 100f;
    public float hungerDecayRate = 2f;
    public float coffeeRestoreAmount = 25f;      // Immediate hunger restore
    public float coffeeDecayBonus = 0.5f;        // Each coffee adds +0.5x to decay
    public float maxDecayMultiplier = 5f;        // Cap at 5x decay speed
    public float coffeeEffectDuration = 15f;     // How long each coffee stack lasts
    public float eatRestoreAmount = 40f;

    [Header("Starving Effects")]
    public float starvingSoundInterval = 3f;
    public float starvingGlitchIntensity = 0.5f;
    public Color starvingGlitchColor = new Color(0.5f, 0.1f, 0.1f);

    [Header("UI")]
    public Slider hungerSlider;
    public Image sliderFill;
    public Gradient hungerGradient;

    private float currentDecayMultiplier = 1f;
    private float coffeeEffectTimer = 0f;
    private int coffeeStacks = 0;

    private bool isStarving = false;
    private float lastStarvingSound = 0f;
    private Renderer[] renderers;
    private Color[] originalColors;
    private Coroutine glitchCoroutine;

    public bool IsStarving => isStarving;
    public float HungerPercent => currentHunger / maxHunger;

    void Start()
    {
        currentHunger = maxHunger;

        // Get renderers for glitch effect
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].material != null)
            {
                originalColors[i] = renderers[i].material.color;
            }
        }

        if (GameUIManager.Instance != null)
        {
            hungerSlider = GameUIManager.Instance.GetHungerSlider();
            sliderFill = GameUIManager.Instance.GetHungerBarFill();
        }
        else
        {
            if (hungerSlider == null)
            {
                GameObject sliderObj = GameObject.Find("HungerBar");
                if (sliderObj != null)
                {
                    hungerSlider = sliderObj.GetComponent<Slider>();
                }
            }

            if (hungerSlider != null)
            {
                hungerSlider.maxValue = maxHunger;
                hungerSlider.value = currentHunger;

                if (sliderFill == null)
                {
                    sliderFill = hungerSlider.fillRect?.GetComponent<Image>();
                }
            }
        }

        if (hungerGradient == null)
        {
            hungerGradient = new Gradient();
            hungerGradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.red, 0f),
                    new GradientColorKey(Color.yellow, 0.5f),
                    new GradientColorKey(Color.green, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                }
            );
        }

        UpdateUI();
        Debug.Log("[HungerSystem] Initialized");
    }

    void Update()
    {
        // Coffee effect decay - timer counts down, when it hits 0 remove one stack
        if (coffeeEffectTimer > 0)
        {
            coffeeEffectTimer -= Time.deltaTime;
            if (coffeeEffectTimer <= 0 && coffeeStacks > 0)
            {
                coffeeStacks--;
                if (coffeeStacks > 0)
                {
                    // More stacks remain, reset timer
                    coffeeEffectTimer = coffeeEffectDuration;
                }
                // Recalculate multiplier
                currentDecayMultiplier = 1f + (coffeeStacks * coffeeDecayBonus);
                Debug.Log($"[HungerSystem] Coffee stack expired. Stacks: {coffeeStacks}, Multiplier: {currentDecayMultiplier:F1}x");
            }
        }

        // Hunger decay
        currentHunger -= hungerDecayRate * currentDecayMultiplier * Time.deltaTime;
        currentHunger = Mathf.Clamp(currentHunger, 0, maxHunger);

        UpdateUI();

        // Check starving state
        if (currentHunger <= 0 && !isStarving)
        {
            StartStarving();
        }
        else if (currentHunger > 0 && isStarving)
        {
            StopStarving();
        }

        // Starving effects
        if (isStarving)
        {
            UpdateStarvingEffects();
        }
    }

    void UpdateUI()
    {
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.UpdateHungerBar(currentHunger, maxHunger);
        }
        else
        {
            if (hungerSlider != null)
            {
                hungerSlider.value = currentHunger;

                if (sliderFill != null && hungerGradient != null)
                {
                    sliderFill.color = hungerGradient.Evaluate(currentHunger / maxHunger);
                }
            }
        }
    }

    // ==================== STARVING STATE ====================

    void StartStarving()
    {
        isStarving = true;
        Debug.Log("[HungerSystem] STARVING! Alien is revealing itself!");

        // Start glitch effect
        if (glitchCoroutine != null)
        {
            StopCoroutine(glitchCoroutine);
        }
        glitchCoroutine = StartCoroutine(StarvingGlitchEffect());

        // Play initial starving sound
        PlayStarvingSound();
    }

    void StopStarving()
    {
        isStarving = false;
        Debug.Log("[HungerSystem] No longer starving");

        // Stop glitch effect
        if (glitchCoroutine != null)
        {
            StopCoroutine(glitchCoroutine);
            glitchCoroutine = null;
        }

        // Reset colors
        ResetColors();
    }

    void UpdateStarvingEffects()
    {
        // Play sounds periodically
        if (Time.time - lastStarvingSound > starvingSoundInterval)
        {
            PlayStarvingSound();
            lastStarvingSound = Time.time;
        }
    }

    void PlayStarvingSound()
    {
        if (AudioManager.Instance != null)
        {
            // Play alien growl sound when starving
            AudioManager.Instance.PlayAlienGrowl(transform.position);
        }

        // Also stress the astronaut if nearby (they hear the alien sounds)
        if (StressSystem.Instance != null)
        {
            float dist = Vector3.Distance(transform.position, StressSystem.Instance.transform.position);
            if (dist < 15f)
            {
                StressSystem.Instance.AddStress(3f);
                Debug.Log("[HungerSystem] Astronaut heard starving alien sounds!");
            }
        }
    }

    IEnumerator StarvingGlitchEffect()
    {
        while (isStarving)
        {
            // Random glitch timing
            float glitchDuration = Random.Range(0.1f, 0.3f);
            float waitTime = Random.Range(0.5f, 2f);

            // Show alien form briefly
            ShowAlienGlitch(true);
            yield return new WaitForSeconds(glitchDuration);

            // Return to normal
            ShowAlienGlitch(false);
            yield return new WaitForSeconds(waitTime);
        }
    }

    void ShowAlienGlitch(bool showAlien)
    {
        if (showAlien)
        {
            // Glitch to alien appearance
            foreach (var renderer in renderers)
            {
                if (renderer != null && renderer.material != null)
                {
                    // Flash red/dark color
                    renderer.material.color = starvingGlitchColor;

                    // Add emission glow
                    if (renderer.material.HasProperty("_EmissionColor"))
                    {
                        renderer.material.EnableKeyword("_EMISSION");
                        renderer.material.SetColor("_EmissionColor", starvingGlitchColor * 0.5f);
                    }
                }
            }

            // Scale glitch
            transform.localScale = transform.localScale * (1f + Random.Range(-0.1f, 0.1f));
        }
        else
        {
            ResetColors();
        }
    }

    void ResetColors()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].material != null && i < originalColors.Length)
            {
                renderers[i].material.color = originalColors[i];

                if (renderers[i].material.HasProperty("_EmissionColor"))
                {
                    renderers[i].material.SetColor("_EmissionColor", Color.black);
                }
            }
        }
    }

    // ==================== ACTIONS ====================

    public void DrinkCoffee()
    {
        // Immediate hunger restore (the benefit)
        currentHunger = Mathf.Min(currentHunger + coffeeRestoreAmount, maxHunger);

        // Add a coffee stack - increases hunger decay (the cost)
        coffeeStacks++;
        currentDecayMultiplier = 1f + (coffeeStacks * coffeeDecayBonus);
        currentDecayMultiplier = Mathf.Min(currentDecayMultiplier, maxDecayMultiplier);

        // Reset/extend timer
        coffeeEffectTimer = coffeeEffectDuration;

        Debug.Log($"[HungerSystem] Drank coffee! +{coffeeRestoreAmount} hunger, but decay now {currentDecayMultiplier:F1}x faster!");
    }

    public int CoffeeStacks => coffeeStacks;
    public float DecayMultiplier => currentDecayMultiplier;

    public void Eat()
    {
        currentHunger = Mathf.Min(currentHunger + eatRestoreAmount, maxHunger);
        Debug.Log($"[HungerSystem] Ate target. Hunger: {currentHunger}");
    }

    public void AddHunger(float amount)
    {
        currentHunger = Mathf.Min(currentHunger + amount, maxHunger);
        UpdateUI();
    }

    // ==================== UI WARNING ====================

    void OnGUI()
    {
        // Don't show UI if disabled (chaos mode) or not controlled
        if (!enabled) return;
        if (!AlienController.IsAlienControlled) return;

        // Show coffee stacks warning
        if (coffeeStacks > 0)
        {
            GUIStyle coffeeStyle = new GUIStyle(GUI.skin.label);
            coffeeStyle.fontSize = 16;
            coffeeStyle.fontStyle = FontStyle.Bold;
            coffeeStyle.alignment = TextAnchor.MiddleRight;
            coffeeStyle.normal.textColor = new Color(0.6f, 0.4f, 0.2f);

            GUI.Label(new Rect(Screen.width - 220, 120, 200, 25),
                $"Coffee x{coffeeStacks} ({currentDecayMultiplier:F1}x decay)", coffeeStyle);
        }

        // Show warning when low hunger
        if (currentHunger < maxHunger * 0.3f)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 20;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;

            float pulse = Mathf.PingPong(Time.time * 3f, 1f);
            style.normal.textColor = new Color(1f, 0.5f, 0f, 0.5f + pulse * 0.5f);

            string warning = isStarving ? "STARVING! YOU ARE EXPOSED!" : "LOW HUNGER!";
            GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height - 100, 300, 30), warning, style);
        }
    }
}
