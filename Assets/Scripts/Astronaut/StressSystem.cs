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
    [SerializeField] private float passiveRecoveryRate = 1f;
    [SerializeField] private float recoveryDelay = 3f;

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

    public static StressSystem Instance { get; private set; }

    public float CurrentStress => currentStress;
    public float StressPercent => currentStress / maxStress;
    public bool IsStressMaxed => isMaxed;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        currentStress = 0f;
        
        // Auto-find UI if not assigned
        if (stressSlider == null)
        {
            stressSlider = GameObject.Find("StressBar")?.GetComponent<Slider>();
            if (stressSlider == null)
            {
                var sliders = FindObjectsOfType<Slider>();
                foreach (var s in sliders)
                {
                    if (s.name.ToLower().Contains("stress"))
                    {
                        stressSlider = s;
                        break;
                    }
                }
            }
        }
        
        // Get fill image from slider
        if (stressSlider != null && stressBarFill == null)
        {
            stressBarFill = stressSlider.fillRect?.GetComponent<Image>();
        }
        
        // Create default gradient if not set
        if (stressColorGradient == null)
        {
            stressColorGradient = new Gradient();
            GradientColorKey[] colorKeys = new GradientColorKey[3];
            colorKeys[0] = new GradientColorKey(Color.green, 0f);
            colorKeys[1] = new GradientColorKey(Color.yellow, 0.5f);
            colorKeys[2] = new GradientColorKey(Color.red, 1f);
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(1f, 0f);
            alphaKeys[1] = new GradientAlphaKey(1f, 1f);
            stressColorGradient.SetKeys(colorKeys, alphaKeys);
        }
        
        UpdateUI();
        Debug.Log($"[StressSystem] Initialized. Slider found: {stressSlider != null}");
    }

    void Update()
    {
        HandlePassiveRecovery();
    }

    private void HandlePassiveRecovery()
    {
        if (isMaxed) return;

        if (Time.time - lastStressTime > recoveryDelay && currentStress > 0)
        {
            ReduceStress(passiveRecoveryRate * Time.deltaTime);
        }
    }

    public void AddStress(float amount)
    {
        if (isMaxed) return;

        currentStress = Mathf.Clamp(currentStress + amount, 0f, maxStress);
        lastStressTime = Time.time;
        
        Debug.Log($"[StressSystem] Stress added: {amount}. Current: {currentStress}/{maxStress}");
        OnStressChanged?.Invoke(currentStress);
        UpdateUI();

        if (currentStress >= maxStress)
        {
            TriggerStressOverload();
        }
    }

    public void ReduceStress(float amount)
    {
        if (isMaxed) return;

        float oldStress = currentStress;
        currentStress = Mathf.Clamp(currentStress - amount, 0f, maxStress);
        
        if (amount > 0.1f) // Only log significant changes
        {
            Debug.Log($"[StressSystem] Stress reduced: {amount}. Current: {currentStress}/{maxStress}");
        }
        
        OnStressChanged?.Invoke(currentStress);
        UpdateUI();
    }

    public void OnInnocentKilled()
    {
        AddStress(innocentKillStress);
        Debug.Log($"[StressSystem] Innocent killed! Stress +{innocentKillStress}");
    }

    public void OnAlienKilled()
    {
        ReduceStress(alienKillStressRelief);
        Debug.Log($"[StressSystem] Alien killed! Stress -{alienKillStressRelief}");
    }

    public void OnChaosEvent()
    {
        AddStress(chaosEventStress);
    }

    public void OnWitnessDeath()
    {
        AddStress(witnessDeathStress);
    }

    private void TriggerStressOverload()
    {
        isMaxed = true;
        Debug.Log("[StressSystem] STRESS MAXED! Triggering chaos phase!");
        OnStressMaxed?.Invoke();
    }

    public void ResetStress()
    {
        currentStress = 0f;
        isMaxed = false;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (stressSlider != null)
        {
            stressSlider.minValue = 0f;
            stressSlider.maxValue = 1f;
            stressSlider.value = StressPercent;
        }

        if (stressBarFill != null && stressColorGradient != null)
        {
            stressBarFill.color = stressColorGradient.Evaluate(StressPercent);
        }
    }

    public void TakeDamage(float amount)
    {
        AddStress(amount);
    }
}
