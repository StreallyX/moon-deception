using UnityEngine;
using System.Collections;

/// <summary>
/// Camera shake effect for shooting, explosions, impacts.
/// Attach to the camera or use the static Shake method.
/// </summary>
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("Shake Settings")]
    [SerializeField] private float defaultDuration = 0.1f;
    [SerializeField] private float defaultMagnitude = 0.1f;
    [SerializeField] private AnimationCurve shakeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    private Vector3 originalPosition;
    private Coroutine shakeCoroutine;
    private bool isShaking = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    void Start()
    {
        originalPosition = transform.localPosition;
    }

    /// <summary>
    /// Trigger camera shake with default settings
    /// </summary>
    public void Shake()
    {
        Shake(defaultDuration, defaultMagnitude);
    }

    /// <summary>
    /// Trigger camera shake with custom duration and magnitude
    /// </summary>
    public void Shake(float duration, float magnitude)
    {
        // Don't shake if camera is inactive
        if (!gameObject.activeInHierarchy) return;

        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
        }
        shakeCoroutine = StartCoroutine(ShakeCoroutine(duration, magnitude));
    }

    /// <summary>
    /// Small shake for shooting
    /// </summary>
    public void ShakeShoot()
    {
        Shake(0.08f, 0.03f);
    }

    /// <summary>
    /// Medium shake for impacts
    /// </summary>
    public void ShakeImpact()
    {
        Shake(0.15f, 0.08f);
    }

    /// <summary>
    /// Big shake for explosions or stress events
    /// </summary>
    public void ShakeHeavy()
    {
        Shake(0.3f, 0.2f);
    }

    /// <summary>
    /// Continuous subtle shake for stress
    /// </summary>
    public void StartStressShake(float intensity = 0.02f)
    {
        if (!isShaking)
        {
            StartCoroutine(ContinuousShake(intensity));
        }
    }

    public void StopStressShake()
    {
        isShaking = false;
    }

    private IEnumerator ShakeCoroutine(float duration, float magnitude)
    {
        float elapsed = 0f;
        Vector3 startPos = transform.localPosition;

        while (elapsed < duration)
        {
            float curveValue = shakeCurve.Evaluate(elapsed / duration);
            float x = Random.Range(-1f, 1f) * magnitude * curveValue;
            float y = Random.Range(-1f, 1f) * magnitude * curveValue;

            transform.localPosition = startPos + new Vector3(x, y, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = startPos;
        shakeCoroutine = null;
    }

    private IEnumerator ContinuousShake(float intensity)
    {
        isShaking = true;
        Vector3 startPos = transform.localPosition;

        while (isShaking)
        {
            float x = Random.Range(-1f, 1f) * intensity;
            float y = Random.Range(-1f, 1f) * intensity;
            transform.localPosition = startPos + new Vector3(x, y, 0f);
            yield return null;
        }

        transform.localPosition = startPos;
    }

    /// <summary>
    /// Static method to shake camera from anywhere
    /// </summary>
    public static void TriggerShake(float duration = 0.1f, float magnitude = 0.1f)
    {
        if (Instance != null)
        {
            Instance.Shake(duration, magnitude);
        }
    }

    void OnDestroy()
    {
        // CRITICAL: Clear static instance to prevent stale references after scene reload
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
