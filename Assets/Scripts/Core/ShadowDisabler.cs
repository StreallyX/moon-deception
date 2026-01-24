using UnityEngine;

/// <summary>
/// Disables all shadows at startup for performance.
/// Runs BEFORE any scene loads.
/// </summary>
public static class ShadowDisabler
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void DisableShadowsBeforeSceneLoad()
    {
        // Disable shadows in QualitySettings
        QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
        QualitySettings.shadowDistance = 0f;

        Debug.Log("[ShadowDisabler] Shadows disabled globally");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void DisableShadowsAfterSceneLoad()
    {
        DisableAllLightShadows();
    }

    static void DisableAllLightShadows()
    {
        Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        int count = 0;
        foreach (var light in allLights)
        {
            if (light != null && light.shadows != LightShadows.None)
            {
                light.shadows = LightShadows.None;
                count++;
            }
        }
        if (count > 0)
        {
            Debug.Log($"[ShadowDisabler] Disabled shadows on {count} lights");
        }
    }
}
