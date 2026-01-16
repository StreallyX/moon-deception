using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// Manages the astronaut's stress level.
/// Stress increases when killing innocents or witnessing chaos.
/// Stress decreases when killing aliens or staying calm.
/// Includes visual/audio effects (heartbeat, screen darkening).
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

    [Header("Stress Effects")]
    [SerializeField] private float heartbeatStartPercent = 0.5f; // Start heartbeat at 50% stress
    [SerializeField] private float heartbeatMinInterval = 1.2f;  // Slow heartbeat
    [SerializeField] private float heartbeatMaxInterval = 0.4f;  // Fast heartbeat at max stress
    [SerializeField] private float screenShakeStartPercent = 0.7f; // Start shaking at 70%

    [Header("UI Reference")]
    [SerializeField] private Slider stressSlider;
    [SerializeField] private Image stressBarFill;
    [SerializeField] private Gradient stressColorGradient;

    [Header("Events")]
    public UnityEvent OnStressMaxed;
    public UnityEvent<float> OnStressChanged;

    private float lastStressTime;
    private bool isMaxed = false;
    private float heartbeatTimer = 0f;
    private AudioSource heartbeatSource;

    public static StressSystem Instance { get; private set; }

    public float CurrentStress => currentStress;
    public float StressPercent => currentStress / maxStress;
    public bool IsStressMaxed => isMaxed;

    void Awake()
    {
        // Don't set Instance here - it will be set in OnEnable
        // This prevents remote player's StressSystem from overwriting the local one

        // Initialize events if null
        if (OnStressMaxed == null) OnStressMaxed = new UnityEvent();
        if (OnStressChanged == null) OnStressChanged = new UnityEvent<float>();
    }

    void OnEnable()
    {
        // Only set Instance when enabled (local player only)
        Instance = this;
        Debug.Log("[StressSystem] Instance set (OnEnable)");

        // Auto-register with GameManager for chaos trigger
        RegisterWithGameManager();
    }

    void RegisterWithGameManager()
    {
        if (GameManager.Instance != null)
        {
            // Subscribe OnStressMaxed to trigger chaos phase
            OnStressMaxed.RemoveListener(GameManager.Instance.TriggerChaosPhase); // Avoid duplicates
            OnStressMaxed.AddListener(GameManager.Instance.TriggerChaosPhase);
            Debug.Log("[StressSystem] Registered with GameManager for chaos trigger");
        }
        else
        {
            // GameManager not ready yet, try again later
            StartCoroutine(LateRegisterWithGameManager());
        }
    }

    System.Collections.IEnumerator LateRegisterWithGameManager()
    {
        yield return new WaitForSeconds(0.5f);

        if (GameManager.Instance != null)
        {
            OnStressMaxed.RemoveListener(GameManager.Instance.TriggerChaosPhase);
            OnStressMaxed.AddListener(GameManager.Instance.TriggerChaosPhase);
            Debug.Log("[StressSystem] Late-registered with GameManager for chaos trigger");
        }
        else
        {
            Debug.LogWarning("[StressSystem] Could not register with GameManager - not found!");
        }
    }

    void OnDisable()
    {
        // Clear Instance if this was the active one
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Start()
    {
        currentStress = 0f;

        if (GameUIManager.Instance != null)
        {
            stressSlider = GameUIManager.Instance.GetStressSlider();
            stressBarFill = GameUIManager.Instance.GetStressBarFill();
        }
        else
        {
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

            if (stressSlider != null && stressBarFill == null)
            {
                stressBarFill = stressSlider.fillRect?.GetComponent<Image>();
            }
        }

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
        HandleStressEffects();
    }

    /// <summary>
    /// Handle heartbeat sound and screen effects based on stress level
    /// </summary>
    private void HandleStressEffects()
    {
        if (isMaxed) return; // Chaos mode has its own effects

        float stressPercent = StressPercent;

        // Heartbeat sound when stress is high
        if (stressPercent >= heartbeatStartPercent)
        {
            heartbeatTimer -= Time.deltaTime;

            if (heartbeatTimer <= 0f)
            {
                PlayHeartbeat();

                // Calculate interval based on stress (faster as stress increases)
                float t = (stressPercent - heartbeatStartPercent) / (1f - heartbeatStartPercent);
                float interval = Mathf.Lerp(heartbeatMinInterval, heartbeatMaxInterval, t);
                heartbeatTimer = interval;
            }
        }
        else
        {
            heartbeatTimer = 0f; // Reset when stress drops
        }

        // Screen shake at very high stress
        if (stressPercent >= screenShakeStartPercent && CameraShake.Instance != null)
        {
            float shakeIntensity = (stressPercent - screenShakeStartPercent) / (1f - screenShakeStartPercent);
            // Very subtle constant shake
            if (Random.value < 0.05f) // 5% chance per frame
            {
                CameraShake.Instance.Shake(0.1f, 0.01f * shakeIntensity);
            }
        }
    }

    private void PlayHeartbeat()
    {
        // Play heartbeat sound
        if (AudioManager.Instance != null && AudioManager.Instance.heartbeatLoop != null)
        {
            // Create or reuse audio source for heartbeat
            if (heartbeatSource == null)
            {
                GameObject heartbeatObj = new GameObject("HeartbeatSource");
                heartbeatObj.transform.SetParent(transform);
                heartbeatSource = heartbeatObj.AddComponent<AudioSource>();
                heartbeatSource.spatialBlend = 0f; // 2D sound
                heartbeatSource.loop = false;
            }

            // Adjust volume based on stress
            float volume = 0.3f + (StressPercent * 0.5f);
            heartbeatSource.volume = volume;
            heartbeatSource.pitch = 0.9f + (StressPercent * 0.3f); // Slightly faster pitch at high stress
            heartbeatSource.clip = AudioManager.Instance.heartbeatLoop;
            heartbeatSource.Play();
        }
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

        // Use networked chaos phase trigger if available
        if (NetworkAudioManager.Instance != null)
        {
            NetworkAudioManager.Instance.TriggerChaosPhase();
        }
        else
        {
            OnStressMaxed?.Invoke();
        }
    }

    public void ResetStress()
    {
        currentStress = 0f;
        isMaxed = false;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.UpdateStressBar(currentStress, maxStress);
        }
        else
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
    }

    public void TakeDamage(float amount)
    {
        AddStress(amount);
    }
}
