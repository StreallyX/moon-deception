using UnityEngine;
using System.Collections;

/// <summary>
/// Manages bullet tracer visual effects.
/// Spawns tracers and impact effects that are synced across network.
/// </summary>
public class BulletTracerManager : MonoBehaviour
{
    public static BulletTracerManager Instance { get; private set; }

    [Header("Tracer Settings")]
    public float tracerDuration = 0.1f;
    public float tracerWidth = 0.02f;
    public Color tracerColor = new Color(1f, 0.8f, 0.3f, 1f);

    [Header("Impact Settings")]
    public GameObject bulletImpactPrefab;
    public GameObject bloodImpactPrefab;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Spawn a bullet tracer line from start to end
    /// </summary>
    public void SpawnTracer(Vector3 startPoint, Vector3 endPoint, bool isHit)
    {
        StartCoroutine(CreateTracerCoroutine(startPoint, endPoint, isHit));
    }

    private IEnumerator CreateTracerCoroutine(Vector3 startPoint, Vector3 endPoint, bool isHit)
    {
        // Create tracer object
        GameObject tracerObj = new GameObject("BulletTracer");
        LineRenderer lineRenderer = tracerObj.AddComponent<LineRenderer>();

        // Configure line renderer
        lineRenderer.startWidth = tracerWidth;
        lineRenderer.endWidth = tracerWidth * 0.5f;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, startPoint);
        lineRenderer.SetPosition(1, endPoint);

        // Create material
        Material tracerMaterial = new Material(Shader.Find("Sprites/Default"));
        tracerMaterial.color = isHit ? new Color(1f, 0.5f, 0.3f, 1f) : tracerColor;
        lineRenderer.material = tracerMaterial;
        lineRenderer.startColor = tracerMaterial.color;
        lineRenderer.endColor = new Color(tracerMaterial.color.r, tracerMaterial.color.g, tracerMaterial.color.b, 0.3f);

        // Wait and fade out
        float elapsed = 0f;
        Color startColor = lineRenderer.startColor;
        Color endColor = lineRenderer.endColor;

        while (elapsed < tracerDuration)
        {
            // Check if object was destroyed
            if (lineRenderer == null || tracerObj == null)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / tracerDuration);

            lineRenderer.startColor = new Color(startColor.r, startColor.g, startColor.b, startColor.a * alpha);
            lineRenderer.endColor = new Color(endColor.r, endColor.g, endColor.b, endColor.a * alpha);

            yield return null;
        }

        // Cleanup
        if (tracerMaterial != null) Destroy(tracerMaterial);
        if (tracerObj != null) Destroy(tracerObj);
    }

    /// <summary>
    /// Spawn impact effect at position
    /// </summary>
    public void SpawnImpact(Vector3 position, Vector3 normal, bool isBlood)
    {
        GameObject prefab = isBlood ? bloodImpactPrefab : bulletImpactPrefab;

        if (prefab != null)
        {
            GameObject impact = Instantiate(prefab, position, Quaternion.LookRotation(normal));
            Destroy(impact, 2f);
        }
        else
        {
            // Create simple particle effect
            CreateSimpleImpactEffect(position, normal, isBlood);
        }
    }

    private void CreateSimpleImpactEffect(Vector3 position, Vector3 normal, bool isBlood)
    {
        GameObject impactObj = new GameObject("ImpactEffect");
        impactObj.transform.position = position;
        impactObj.transform.rotation = Quaternion.LookRotation(normal);

        ParticleSystem ps = impactObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.startLifetime = isBlood ? 0.5f : 0.3f;
        main.startSpeed = isBlood ? 3f : 5f;
        main.startSize = isBlood ? 0.1f : 0.05f;
        main.startColor = isBlood ? new Color(0.5f, 0f, 0f, 1f) : new Color(1f, 0.8f, 0.3f, 1f);
        main.maxParticles = isBlood ? 20 : 10;
        main.gravityModifier = isBlood ? 1f : 0.5f;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)(isBlood ? 15 : 8)) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = isBlood ? 30f : 45f;

        var renderer = impactObj.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));

        ps.Play();
        Destroy(impactObj, 2f);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
