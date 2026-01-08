using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// Manages the alien's hunger system.
/// Hunger increases over time, coffee helps temporarily but accelerates decay,
/// eating fully satisfies but leaves evidence.
/// </summary>
public class HungerSystem : MonoBehaviour
{
    [Header("Hunger Settings")]
    [SerializeField] private float maxHunger = 100f;
    [SerializeField] private float currentHunger = 0f;
    [SerializeField] private float baseHungerRate = 2f; // per second
    
    [Header("Coffee Effects")]
    [SerializeField] private float coffeeRelief = 30f;
    [SerializeField] private float coffeeHungerMultiplier = 1.5f; // accelerates hunger after drinking
    [SerializeField] private float coffeeDuration = 30f; // how long the accelerated effect lasts
    
    [Header("Eating")]
    [SerializeField] private float eatingSatisfaction = 100f; // full satisfaction
    
    [Header("UI Reference")]
    [SerializeField] private Slider hungerSlider;
    [SerializeField] private Image hungerBarFill;
    [SerializeField] private Gradient hungerColorGradient;

    [Header("Events")]
    public UnityEvent OnHungerMaxed;
    public UnityEvent OnAteTarget;
    public UnityEvent OnDrankCoffee;
    public UnityEvent<float> OnHungerChanged;

    // State
    private float currentHungerRate;
    private float coffeeEffectEndTime = 0f;
    private bool isStarving = false;
    private int coffeeCount = 0;

    public float CurrentHunger => currentHunger;
    public float HungerPercent => currentHunger / maxHunger;
    public bool IsStarving => isStarving;
    public int CoffeeConsumed => coffeeCount;

    void Start()
    {
        currentHunger = 0f;
        currentHungerRate = baseHungerRate;
        UpdateUI();
    }

    void Update()
    {
        ProcessHunger();
        CheckCoffeeEffect();
    }

    /// <summary>
    /// Process hunger increase over time
    /// </summary>
    private void ProcessHunger()
    {
        if (isStarving) return;

        currentHunger += currentHungerRate * Time.deltaTime;
        currentHunger = Mathf.Clamp(currentHunger, 0f, maxHunger);

        OnHungerChanged?.Invoke(currentHunger);
        UpdateUI();

        if (currentHunger >= maxHunger)
        {
            TriggerStarvation();
        }
    }

    /// <summary>
    /// Check and manage coffee effect duration
    /// </summary>
    private void CheckCoffeeEffect()
    {
        if (Time.time > coffeeEffectEndTime && currentHungerRate != baseHungerRate)
        {
            currentHungerRate = baseHungerRate;
            Debug.Log("[HungerSystem] Coffee effect wore off");
        }
    }

    /// <summary>
    /// Drink coffee - provides temporary relief but accelerates hunger afterward
    /// </summary>
    public void DrinkCoffee()
    {
        if (isStarving) return;

        // Reduce current hunger
        currentHunger = Mathf.Max(0f, currentHunger - coffeeRelief);
        
        // But accelerate hunger rate
        currentHungerRate = baseHungerRate * coffeeHungerMultiplier;
        coffeeEffectEndTime = Time.time + coffeeDuration;
        coffeeCount++;

        OnDrankCoffee?.Invoke();
        UpdateUI();

        Debug.Log($"[HungerSystem] Drank coffee! Hunger -{coffeeRelief}, but rate accelerated. Total coffees: {coffeeCount}");
    }

    /// <summary>
    /// Eat to fully satisfy hunger (leaves blood evidence)
    /// </summary>
    public void Eat()
    {
        currentHunger = Mathf.Max(0f, currentHunger - eatingSatisfaction);
        
        // Reset hunger rate if it was accelerated
        currentHungerRate = baseHungerRate;
        coffeeEffectEndTime = 0f;
        
        // Reset starving state if applicable
        isStarving = false;

        OnAteTarget?.Invoke();
        UpdateUI();

        Debug.Log("[HungerSystem] Ate target! Hunger fully satisfied (blood trace left)");
    }

    /// <summary>
    /// Triggered when hunger reaches maximum
    /// </summary>
    private void TriggerStarvation()
    {
        isStarving = true;
        Debug.Log("[HungerSystem] STARVING! Need to eat soon!");
        OnHungerMaxed?.Invoke();
    }

    /// <summary>
    /// Reset hunger system (e.g., for new round)
    /// </summary>
    public void ResetHunger()
    {
        currentHunger = 0f;
        currentHungerRate = baseHungerRate;
        isStarving = false;
        coffeeCount = 0;
        coffeeEffectEndTime = 0f;
        UpdateUI();
    }

    /// <summary>
    /// Get current hunger rate (useful for UI feedback)
    /// </summary>
    public float GetCurrentHungerRate()
    {
        return currentHungerRate;
    }

    /// <summary>
    /// Check if under coffee effect
    /// </summary>
    public bool IsCoffeeEffectActive()
    {
        return Time.time < coffeeEffectEndTime;
    }

    /// <summary>
    /// Update UI elements
    /// </summary>
    private void UpdateUI()
    {
        if (hungerSlider != null)
        {
            hungerSlider.value = HungerPercent;
        }

        if (hungerBarFill != null && hungerColorGradient != null)
        {
            hungerBarFill.color = hungerColorGradient.Evaluate(HungerPercent);
        }
    }
}
