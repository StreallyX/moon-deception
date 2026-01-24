using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controls lighting during Chaos phase.
/// Turns off main lights, enables emergency red lighting.
/// Aliens get night vision (brighter view).
/// </summary>
public class ChaosLightingController : MonoBehaviour
{
    public static ChaosLightingController Instance { get; private set; }

    [Header("Lighting Settings")]
    public float normalAmbientIntensity = 1f;
    public float chaosAmbientIntensity = 0.1f;
    public Color normalAmbientColor = Color.white;
    public Color chaosAmbientColor = new Color(0.2f, 0.05f, 0.05f);

    [Header("Emergency Lights")]
    public Color emergencyLightColor = new Color(1f, 0.1f, 0.1f);
    public float emergencyLightIntensity = 0.5f;
    public float flickerSpeed = 2f;

    [Header("Alien Night Vision")]
    public float alienVisionBrightness = 0.8f;
    public Color alienVisionTint = new Color(0.3f, 1f, 0.3f);

    [Header("Transition")]
    public float transitionDuration = 2f;

    // Tracked lights
    private List<Light> sceneLights = new List<Light>();
    private Dictionary<Light, float> originalIntensities = new Dictionary<Light, float>();
    private Dictionary<Light, Color> originalColors = new Dictionary<Light, Color>();
    private List<Light> emergencyLights = new List<Light>();

    // State
    private bool isChaosMode = false;
    private float originalAmbientIntensity;
    private Color originalAmbientColor;

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
        // Store original ambient settings
        originalAmbientIntensity = RenderSettings.ambientIntensity;
        originalAmbientColor = RenderSettings.ambientLight;

        // Find all lights in scene
        FindAllLights();

        // Subscribe to game events (both GameManager AND NetworkGameManager for multiplayer)
        SubscribeToEvents();

        Debug.Log($"[ChaosLightingController] Initialized. Found {sceneLights.Count} lights.");
    }

    void SubscribeToEvents()
    {
        bool subscribed = false;

        // Subscribe to GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnChaosPhase.AddListener(StartChaosLighting);
            GameManager.Instance.OnGameStart.AddListener(ResetLighting);
            subscribed = true;
            Debug.Log("[ChaosLightingController] Subscribed to GameManager events");
        }

        // Also subscribe to NetworkGameManager for multiplayer
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.OnPhaseChanged += OnNetworkPhaseChanged;
            subscribed = true;
            Debug.Log("[ChaosLightingController] Subscribed to NetworkGameManager events");
        }

        if (!subscribed)
        {
            StartCoroutine(LateSubscribeToEvents());
        }
    }

    void OnNetworkPhaseChanged(NetworkGameManager.GamePhase phase)
    {
        if (phase == NetworkGameManager.GamePhase.Chaos && !isChaosMode)
        {
            Debug.Log("[ChaosLightingController] Received NetworkGameManager Chaos phase!");
            StartChaosLighting();
        }
    }

    System.Collections.IEnumerator LateSubscribeToEvents()
    {
        float timeout = 5f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnChaosPhase.RemoveListener(StartChaosLighting);
                GameManager.Instance.OnGameStart.RemoveListener(ResetLighting);
                GameManager.Instance.OnChaosPhase.AddListener(StartChaosLighting);
                GameManager.Instance.OnGameStart.AddListener(ResetLighting);
                Debug.Log("[ChaosLightingController] Late-subscribed to GameManager");
            }

            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.OnPhaseChanged -= OnNetworkPhaseChanged;
                NetworkGameManager.Instance.OnPhaseChanged += OnNetworkPhaseChanged;
                Debug.Log("[ChaosLightingController] Late-subscribed to NetworkGameManager");
                yield break;
            }
        }
    }

    void FindAllLights()
    {
        sceneLights.Clear();
        originalIntensities.Clear();
        originalColors.Clear();

        Light[] allLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var light in allLights)
        {
            // Skip lights that are part of UI or particles
            if (light.GetComponentInParent<Canvas>() != null) continue;
            if (light.GetComponentInParent<ParticleSystem>() != null) continue;

            sceneLights.Add(light);
            originalIntensities[light] = light.intensity;
            originalColors[light] = light.color; // Save original color!

            // Disable shadows for performance
            light.shadows = LightShadows.None;
        }
    }

    void Update()
    {
        if (!isChaosMode) return;

        // Flicker emergency lights
        FlickerEmergencyLights();

        // Adjust brightness based on who's controlled
        if (AlienController.IsAlienControlled)
        {
            // Alien can see better in darkness
            RenderSettings.ambientIntensity = alienVisionBrightness;
            RenderSettings.ambientLight = alienVisionTint;
        }
        else if (PlayerMovement.IsPlayerControlled)
        {
            // Astronaut struggles in darkness
            RenderSettings.ambientIntensity = chaosAmbientIntensity;
            RenderSettings.ambientLight = chaosAmbientColor;
        }
    }

    void FlickerEmergencyLights()
    {
        foreach (var light in emergencyLights)
        {
            if (light != null)
            {
                // Random flicker
                float flicker = Mathf.PerlinNoise(Time.time * flickerSpeed + light.GetHashCode(), 0f);
                light.intensity = emergencyLightIntensity * (0.5f + flicker * 0.5f);
            }
        }
    }

    public void StartChaosLighting()
    {
        StartCoroutine(TransitionToChaos());
    }

    IEnumerator TransitionToChaos()
    {
        isChaosMode = true;
        Debug.Log("[ChaosLightingController] Transitioning to chaos lighting...");

        // Play sounds with slight delays to avoid audio spike
        if (NetworkAudioManager.Instance != null)
        {
            NetworkAudioManager.Instance.PlayPowerDown();
            yield return new WaitForSeconds(0.1f);
            NetworkAudioManager.Instance.StartChaosAmbient();
        }
        else if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayPowerDown();
            yield return new WaitForSeconds(0.1f);
            AudioManager.Instance.StartChaosAmbient();
        }

        float elapsed = 0f;

        // Quick flicker before blackout
        for (int i = 0; i < 5; i++)
        {
            SetAllLightsIntensity(0.2f);
            yield return new WaitForSeconds(0.1f);
            SetAllLightsIntensity(1f);
            yield return new WaitForSeconds(0.1f);
        }

        // Gradual dimming
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionDuration;

            // Dim all main lights
            foreach (var light in sceneLights)
            {
                if (light != null && originalIntensities.ContainsKey(light))
                {
                    light.intensity = Mathf.Lerp(originalIntensities[light], 0f, t);
                }
            }

            // Transition ambient
            RenderSettings.ambientIntensity = Mathf.Lerp(originalAmbientIntensity, chaosAmbientIntensity, t);
            RenderSettings.ambientLight = Color.Lerp(originalAmbientColor, chaosAmbientColor, t);

            yield return null;
        }

        // All main lights off
        SetAllLightsIntensity(0f);

        // Create emergency lights
        CreateEmergencyLights();

        // Play emergency lights sound (networked)
        if (NetworkAudioManager.Instance != null)
        {
            NetworkAudioManager.Instance.PlayLightsEmergency();
        }
        else if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayLightsEmergency();
        }

        Debug.Log("[ChaosLightingController] Chaos lighting active!");
    }

    void SetAllLightsIntensity(float multiplier)
    {
        foreach (var light in sceneLights)
        {
            if (light != null && originalIntensities.ContainsKey(light))
            {
                light.intensity = originalIntensities[light] * multiplier;
            }
        }
    }

    void CreateEmergencyLights()
    {
        // Create red emergency lights at key positions
        // In a real game, you'd place these manually or at predefined positions

        // For now, convert some existing lights to emergency mode
        int emergencyCount = 0;
        foreach (var light in sceneLights)
        {
            if (light != null && emergencyCount < 5)
            {
                // Convert every 3rd light to emergency
                if (emergencyCount % 3 == 0)
                {
                    light.color = emergencyLightColor;
                    light.intensity = emergencyLightIntensity;
                    emergencyLights.Add(light);
                }
                emergencyCount++;
            }
        }

        // Also create some new emergency lights if we don't have many
        if (emergencyLights.Count < 3)
        {
            // Create at corners of play area (simplified)
            Vector3[] positions = new Vector3[]
            {
                new Vector3(10, 3, 10),
                new Vector3(-10, 3, 10),
                new Vector3(10, 3, -10),
                new Vector3(-10, 3, -10)
            };

            foreach (var pos in positions)
            {
                GameObject lightObj = new GameObject("EmergencyLight");
                lightObj.transform.position = pos;

                Light light = lightObj.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = emergencyLightColor;
                light.intensity = emergencyLightIntensity;
                light.range = 15f;

                emergencyLights.Add(light);
            }
        }
    }

    public void ResetLighting()
    {
        StopAllCoroutines();
        isChaosMode = false;

        // Restore normal ambient sound (networked)
        if (NetworkAudioManager.Instance != null)
        {
            NetworkAudioManager.Instance.StartNormalAmbient();
        }
        else if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StartNormalAmbient();
        }

        // Restore all lights to their original state
        foreach (var light in sceneLights)
        {
            if (light != null && originalIntensities.ContainsKey(light))
            {
                light.intensity = originalIntensities[light];
                // Restore ORIGINAL color (not white!)
                if (originalColors.ContainsKey(light))
                {
                    light.color = originalColors[light];
                }
                // Keep shadows disabled for performance
                light.shadows = LightShadows.None;
            }
        }

        // Destroy created emergency lights
        foreach (var light in emergencyLights)
        {
            if (light != null && !sceneLights.Contains(light))
            {
                Destroy(light.gameObject);
            }
        }
        emergencyLights.Clear();

        // Reset ambient
        RenderSettings.ambientIntensity = originalAmbientIntensity;
        RenderSettings.ambientLight = originalAmbientColor;

        Debug.Log("[ChaosLightingController] Lighting reset to normal");
    }

    void OnDestroy()
    {
        // CRITICAL: Clear static instance to prevent stale references after scene reload
        if (Instance == this)
        {
            Instance = null;
        }

        // Restore settings
        RenderSettings.ambientIntensity = originalAmbientIntensity;
        RenderSettings.ambientLight = originalAmbientColor;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnChaosPhase.RemoveListener(StartChaosLighting);
            GameManager.Instance.OnGameStart.RemoveListener(ResetLighting);
        }

        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.OnPhaseChanged -= OnNetworkPhaseChanged;
        }
    }
}
