using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Controls post-processing effects dynamically based on game state.
/// Requires URP and a Volume component in the scene.
/// </summary>
public class PostProcessController : MonoBehaviour
{
    public static PostProcessController Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Volume postProcessVolume;

    [Header("Stress Effects")]
    [SerializeField] private float maxStressVignette = 0.5f;
    [SerializeField] private float maxStressChromaticAberration = 0.5f;
    [SerializeField] private Color stressColorFilter = new Color(1f, 0.8f, 0.8f);

    [Header("Damage Effects")]
    [SerializeField] private float damageVignetteDuration = 0.3f;
    [SerializeField] private float damageVignetteIntensity = 0.6f;
    [SerializeField] private Color damageColorFilter = new Color(1f, 0.3f, 0.3f);

    [Header("Chaos Mode Effects")]
    [SerializeField] private Color chaosColorFilter = new Color(0.8f, 0.2f, 0.2f);

    // Post-process components
    private Vignette vignette;
    private ChromaticAberration chromaticAberration;
    private ColorAdjustments colorAdjustments;
    private Bloom bloom;
    private FilmGrain filmGrain;

    // State
    private float currentStressLevel = 0f;
    private float damageTimer = 0f;
    private bool isChaosMode = false;
    private float baseVignetteIntensity = 0.2f;
    private bool hasSubscribedToStress = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        SetupPostProcessing();

        // Try to subscribe to stress system (may not exist yet in multiplayer)
        TrySubscribeToStressSystem();

        Debug.Log("[PostProcessController] Initialized");
    }

    void TrySubscribeToStressSystem()
    {
        if (hasSubscribedToStress) return;

        if (StressSystem.Instance != null)
        {
            StressSystem.Instance.OnStressChanged.AddListener(OnStressChanged);
            StressSystem.Instance.OnStressMaxed.AddListener(OnChaosMode);
            hasSubscribedToStress = true;
            Debug.Log("[PostProcessController] Subscribed to StressSystem events");
        }
    }

    void SetupPostProcessing()
    {
        // Find or create volume
        if (postProcessVolume == null)
        {
            postProcessVolume = FindFirstObjectByType<Volume>();

            if (postProcessVolume == null)
            {
                GameObject volumeObj = new GameObject("PostProcessVolume");
                postProcessVolume = volumeObj.AddComponent<Volume>();
                postProcessVolume.isGlobal = true;
                postProcessVolume.priority = 1;

                // Create profile
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                postProcessVolume.profile = profile;

                // Add effects
                vignette = profile.Add<Vignette>(true);
                chromaticAberration = profile.Add<ChromaticAberration>(true);
                colorAdjustments = profile.Add<ColorAdjustments>(true);
                bloom = profile.Add<Bloom>(true);
                filmGrain = profile.Add<FilmGrain>(true);

                // Configure defaults
                ConfigureDefaults();

                Debug.Log("[PostProcessController] Created new Volume with profile");
            }
        }

        // Get existing components from profile
        if (postProcessVolume.profile != null)
        {
            postProcessVolume.profile.TryGet(out vignette);
            postProcessVolume.profile.TryGet(out chromaticAberration);
            postProcessVolume.profile.TryGet(out colorAdjustments);
            postProcessVolume.profile.TryGet(out bloom);
            postProcessVolume.profile.TryGet(out filmGrain);

            if (vignette == null || bloom == null)
            {
                // Add missing effects
                var profile = postProcessVolume.profile;
                if (vignette == null) vignette = profile.Add<Vignette>(true);
                if (chromaticAberration == null) chromaticAberration = profile.Add<ChromaticAberration>(true);
                if (colorAdjustments == null) colorAdjustments = profile.Add<ColorAdjustments>(true);
                if (bloom == null) bloom = profile.Add<Bloom>(true);
                if (filmGrain == null) filmGrain = profile.Add<FilmGrain>(true);

                ConfigureDefaults();
            }
        }
    }

    void ConfigureDefaults()
    {
        // Vignette - subtle edge darkening
        if (vignette != null)
        {
            vignette.active = true;
            vignette.intensity.Override(baseVignetteIntensity);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(Color.black);
        }

        // Bloom - subtle glow on lights
        if (bloom != null)
        {
            bloom.active = true;
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.5f);
            bloom.scatter.Override(0.7f);
        }

        // Chromatic aberration - off by default
        if (chromaticAberration != null)
        {
            chromaticAberration.active = true;
            chromaticAberration.intensity.Override(0f);
        }

        // Color adjustments - neutral
        if (colorAdjustments != null)
        {
            colorAdjustments.active = true;
            colorAdjustments.postExposure.Override(0f);
            colorAdjustments.contrast.Override(10f);
            colorAdjustments.saturation.Override(0f);
        }

        // Film grain - subtle for atmosphere
        if (filmGrain != null)
        {
            filmGrain.active = true;
            filmGrain.type.Override(FilmGrainLookup.Medium1);
            filmGrain.intensity.Override(0.15f);
            filmGrain.response.Override(0.8f);
        }
    }

    void Update()
    {
        // Try to subscribe if we haven't yet (late subscription for multiplayer)
        if (!hasSubscribedToStress)
        {
            TrySubscribeToStressSystem();
        }

        // Handle damage effect fade
        if (damageTimer > 0)
        {
            damageTimer -= Time.deltaTime;
            float damageIntensity = damageTimer / damageVignetteDuration;
            ApplyDamageEffect(damageIntensity);
        }
    }

    void OnStressChanged(float stressLevel)
    {
        currentStressLevel = stressLevel;
        ApplyStressEffects();
    }

    void ApplyStressEffects()
    {
        if (StressSystem.Instance == null) return;

        float stressPercent = StressSystem.Instance.StressPercent;

        // ========== VIGNETTE - Tunnel vision effect ==========
        // Starts at 50% stress, gets darker as stress increases
        if (vignette != null)
        {
            float vignetteIntensity = baseVignetteIntensity;

            if (stressPercent >= 0.5f)
            {
                // From 50% to 100%: vignette goes from base to very dark (0.6)
                float t = (stressPercent - 0.5f) / 0.5f;
                vignetteIntensity = Mathf.Lerp(baseVignetteIntensity, 0.6f, t);

                // Add red tint to vignette at high stress
                if (stressPercent >= 0.7f)
                {
                    float redT = (stressPercent - 0.7f) / 0.3f;
                    vignette.color.Override(Color.Lerp(Color.black, new Color(0.3f, 0f, 0f), redT));
                }
            }

            vignette.intensity.Override(Mathf.Lerp(vignette.intensity.value, vignetteIntensity, Time.deltaTime * 3f));
        }

        // ========== CHROMATIC ABERRATION - Vision distortion ==========
        // Starts at 50% stress
        if (chromaticAberration != null)
        {
            float targetChroma = 0f;
            if (stressPercent >= 0.5f)
            {
                // From 50% to 100%: chromatic aberration increases
                float t = (stressPercent - 0.5f) / 0.5f;
                targetChroma = Mathf.Lerp(0f, maxStressChromaticAberration, t);
            }
            chromaticAberration.intensity.Override(Mathf.Lerp(chromaticAberration.intensity.value, targetChroma, Time.deltaTime * 3f));
        }

        // ========== COLOR ADJUSTMENTS - Desaturation and exposure ==========
        if (colorAdjustments != null)
        {
            // Desaturate starting at 50%
            if (stressPercent >= 0.5f)
            {
                float t = (stressPercent - 0.5f) / 0.5f;
                colorAdjustments.saturation.Override(Mathf.Lerp(0f, -40f, t));

                // Darken screen at very high stress (80%+)
                if (stressPercent >= 0.8f)
                {
                    float darkT = (stressPercent - 0.8f) / 0.2f;
                    colorAdjustments.postExposure.Override(Mathf.Lerp(0f, -0.5f, darkT));
                }
                else
                {
                    colorAdjustments.postExposure.Override(0f);
                }
            }
            else
            {
                colorAdjustments.saturation.Override(0f);
                colorAdjustments.postExposure.Override(0f);
            }
        }

        // ========== FILM GRAIN - Increasing noise ==========
        if (filmGrain != null)
        {
            // Film grain increases with stress
            float grainIntensity = 0.15f;
            if (stressPercent >= 0.5f)
            {
                float t = (stressPercent - 0.5f) / 0.5f;
                grainIntensity = Mathf.Lerp(0.15f, 0.5f, t);
            }
            filmGrain.intensity.Override(grainIntensity);
        }

        // ========== BLOOM - Pulsing effect at high stress ==========
        if (bloom != null && stressPercent >= 0.7f)
        {
            // Pulsing bloom at high stress
            float t = (stressPercent - 0.7f) / 0.3f;
            float pulse = 1f + Mathf.Sin(Time.time * 3f) * 0.2f * t;
            bloom.intensity.Override(0.5f * pulse);
        }
    }

    void OnChaosMode()
    {
        isChaosMode = true;
        ApplyChaosEffects();
    }

    void ApplyChaosEffects()
    {
        // Intense vignette
        if (vignette != null)
        {
            vignette.intensity.Override(0.6f);
            vignette.color.Override(new Color(0.3f, 0f, 0f));
        }

        // Heavy chromatic aberration
        if (chromaticAberration != null)
        {
            chromaticAberration.intensity.Override(0.7f);
        }

        // Red tint
        if (colorAdjustments != null)
        {
            colorAdjustments.colorFilter.Override(chaosColorFilter);
            colorAdjustments.saturation.Override(-20f);
        }

        // Heavy film grain
        if (filmGrain != null)
        {
            filmGrain.intensity.Override(0.4f);
        }
    }

    public void TriggerDamageEffect()
    {
        damageTimer = damageVignetteDuration;
    }

    void ApplyDamageEffect(float intensity)
    {
        if (vignette != null && !isChaosMode)
        {
            float baseIntensity = baseVignetteIntensity + (StressSystem.Instance?.StressPercent ?? 0f) * maxStressVignette;
            vignette.intensity.Override(baseIntensity + (damageVignetteIntensity * intensity));
            vignette.color.Override(Color.Lerp(Color.black, new Color(0.5f, 0f, 0f), intensity));
        }
    }

    public void ResetEffects()
    {
        isChaosMode = false;
        currentStressLevel = 0f;
        damageTimer = 0f;

        ConfigureDefaults();
    }

    void OnDestroy()
    {
        // CRITICAL: Clear static instance to prevent stale references after scene reload
        if (Instance == this)
        {
            Instance = null;
        }

        if (hasSubscribedToStress && StressSystem.Instance != null)
        {
            StressSystem.Instance.OnStressChanged.RemoveListener(OnStressChanged);
            StressSystem.Instance.OnStressMaxed.RemoveListener(OnChaosMode);
        }
    }
}
