using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// Manages the astronaut's stress level.
/// Stress increases when killing innocents or witnessing chaos.
/// Stress decreases when killing aliens or staying calm.
/// </summary>
public class StressSystem : MonoBehaviour, IDamageable
{
    [Header("Stress Settings")]
    [SerializeField] private float maxStress = 100f;
    [SerializeField] private float currentStress = 0f;
    [SerializeField] private float passiveRecoveryRate = 1f; // per second
    [SerializeField] private float recoveryDelay = 3f; // seconds before passive recovery starts

    [Header("Stress Modifiers")]
    [SerializeField] private float innocentKillStress = 25f;
    [SerializeField] private float alienKillStressRelief = 15f;
    [SerializeField] private float chaosEventStress = 10f;
    [SerializeField] private float witnessDeathStress = 5f;

    [Header("UI Reference")]
    [SerializeField] private Slider stressSlider;
    [SerializeField] private Image stressBarFill;
    [SerializeField] private Gradient stressColorGradient;

    [Header("Events")]
    public UnityEvent OnStressMaxed;
    public UnityEvent<float> OnStressChanged;

    private float lastStressTime;
    private bool isMaxed = false;

    public float CurrentStress => currentStress;
    public float StressPercent => currentStress / maxStress;
    public bool IsStressMaxed => isMaxed;

    void Start()
    {
        currentStress = 0f;
        UpdateUI();
    }

    void Update()
    {
        HandlePassiveRecovery();
    }

    /// <summary>
    /// Handles passive stress recovery when player stays calm
    /// </summary>
    private void HandlePassiveRecovery()
    {
        if (isMaxed) return;

        if (Time.time - lastStressTime > recoveryDelay && currentStress > 0)
        {
            ReduceStress(passiveRecoveryRate * Time.deltaTime);
        }
    }

    /// <summary>
    /// Add stress to the astronaut
    /// </summary>
    public void AddStress(float amount)
    {
        if (isMaxed) return;

        currentStress = Mathf.Clamp(currentStress + amount, 0f, maxStress);
        lastStressTime = Time.time;
        
        OnStressChanged?.Invoke(currentStress);
        UpdateUI();

        if (currentStress >= maxStress)
        {
            TriggerStressOverload();
        }
    }

    /// <summary>
    /// Reduce stress (e.g., from killing an alien)
    /// </summary>
    public void ReduceStress(float amount)
    {
        if (isMaxed) return;

        currentStress = Mathf.Clamp(currentStress - amount, 0f, maxStress);
        OnStressChanged?.Invoke(currentStress);
        UpdateUI();
    }

    /// <summary>
    /// Called when an innocent NPC is killed
    /// </summary>
    public void OnInnocentKilled()
    {
        AddStress(innocentKillStress);
        Debug.Log($"[StressSystem] Innocent killed! Stress +{innocentKillStress}");
    }

    /// <summary>
    /// Called when an alien is killed
    /// </summary>
    public void OnAlienKilled()
    {
        ReduceStress(alienKillStressRelief);
        Debug.Log($"[StressSystem] Alien killed! Stress -{alienKillStressRelief}");
    }

    /// <summary>
    /// Called when a chaos event occurs nearby
    /// </summary>
    public void OnChaosEvent()
    {
        AddStress(chaosEventStress);
        Debug.Log($"[StressSystem] Chaos event! Stress +{chaosEventStress}");
    }

    /// <summary>
    /// Called when witnessing another NPC's death
    /// </summary>
    public void OnWitnessDeath()
    {
        AddStress(witnessDeathStress);
    }

    /// <summary>
    /// Triggers the stress overload event (aliens transform, lights out)
    /// </summary>
    private void TriggerStressOverload()
    {
        isMaxed = true;
        Debug.Log("[StressSystem] STRESS MAXED! Triggering chaos phase!");
        OnStressMaxed?.Invoke();
    }

    /// <summary>
    /// Reset stress system (e.g., for new round)
    /// </summary>
    public void ResetStress()
    {
        currentStress = 0f;
        isMaxed = false;
        UpdateUI();
    }

    /// <summary>
    /// Update UI elements
    /// </summary>
    private void UpdateUI()
    {
        if (stressSlider != null)
        {
            stressSlider.value = StressPercent;
        }

        if (stressBarFill != null && stressColorGradient != null)
        {
            stressBarFill.color = stressColorGradient.Evaluate(StressPercent);
        }
    }

    // IDamageable implementation - astronaut takes stress as damage
    public void TakeDamage(float amount)
    {
        AddStress(amount);
    }
}
