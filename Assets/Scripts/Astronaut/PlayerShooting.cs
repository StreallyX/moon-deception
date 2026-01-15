using UnityEngine;
using System.Collections;

public class PlayerShooting : MonoBehaviour
{
    [Header("Shooting Settings")]
    public float range = 100f;
    public float damage = 25f;
    public LayerMask hitLayers = ~0;
    public float fireRate = 0.15f;

    [Header("Visual Effects")]
    public ParticleSystem muzzleFlash;
    public GameObject bulletImpactPrefab;
    public GameObject bloodImpactPrefab;

    [Header("Hit Marker")]
    public bool showHitMarker = true;
    public float hitMarkerDuration = 0.1f;
    public Color hitMarkerColor = Color.white;
    public Color hitMarkerKillColor = Color.red;

    [Header("Camera Shake")]
    public bool enableCameraShake = true;
    public float shakeDuration = 0.05f;
    public float shakeMagnitude = 0.02f;

    [Header("Debug Visuals")]
    public bool showDebugRay = false;
    public float debugRayDuration = 1f;
    public Color hitColor = Color.red;
    public Color missColor = Color.yellow;

    private Camera playerCamera;
    private LineRenderer debugLineRenderer;
    private float nextFireTime = 0f;
    private bool showingHitMarker = false;
    private Color currentHitMarkerColor;
    private Texture2D hitMarkerTexture;

    void Start()
    {
        playerCamera = Camera.main;

        debugLineRenderer = gameObject.AddComponent<LineRenderer>();
        debugLineRenderer.startWidth = 0.02f;
        debugLineRenderer.endWidth = 0.02f;
        debugLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        debugLineRenderer.positionCount = 2;
        debugLineRenderer.enabled = false;

        hitMarkerTexture = new Texture2D(1, 1);
        hitMarkerTexture.SetPixel(0, 0, Color.white);
        hitMarkerTexture.Apply();

        if (muzzleFlash == null)
        {
            CreateMuzzleFlash();
        }
    }

    void CreateMuzzleFlash()
    {
        GameObject muzzleObj = new GameObject("MuzzleFlash");
        muzzleObj.transform.SetParent(playerCamera.transform);
        muzzleObj.transform.localPosition = new Vector3(0.3f, -0.2f, 0.5f);

        // Stop the default particle system first before configuring
        muzzleFlash = muzzleObj.AddComponent<ParticleSystem>();
        muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = muzzleFlash.main;
        main.duration = 0.05f;
        main.loop = false;
        main.startLifetime = 0.05f;
        main.startSpeed = 0f;
        main.startSize = 0.3f;
        main.startColor = new Color(1f, 0.8f, 0.3f, 1f);
        main.maxParticles = 1;
        main.playOnAwake = false;

        var emission = muzzleFlash.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

        var shape = muzzleFlash.shape;
        shape.enabled = false;

        var renderer = muzzleObj.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        renderer.material.SetColor("_Color", new Color(1f, 0.8f, 0.3f, 1f));

        var light = muzzleObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.7f, 0.3f);
        light.intensity = 0;
        light.range = 5f;
    }

    void Update()
    {
        if (Input.GetButton("Fire1") && Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + fireRate;
        }
    }

    void Shoot()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayGunshot();
        }

        if (enableCameraShake && CameraShake.Instance != null)
        {
            CameraShake.Instance.Shake(shakeDuration, shakeMagnitude);
        }

        if (muzzleFlash != null)
        {
            muzzleFlash.Play();
            StartCoroutine(MuzzleFlashLight());
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, range, hitLayers))
        {
            if (showDebugRay)
            {
                Debug.DrawLine(ray.origin, hit.point, hitColor, debugRayDuration);
                ShowShotLine(ray.origin, hit.point, hitColor);
            }

            var damageable = hit.collider.GetComponent<IDamageable>();
            var npc = hit.collider.GetComponent<NPCBehavior>();

            if (damageable != null)
            {
                bool wasKill = npc != null;

                damageable.TakeDamage(damage);

                if (showHitMarker)
                {
                    ShowHitMarker(wasKill);
                }

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayBulletImpact("Flesh", hit.point);
                }

                SpawnImpactEffect(hit.point, hit.normal, true);

                if (npc != null && StressSystem.Instance != null)
                {
                    if (npc.IsAlien)
                    {
                        StressSystem.Instance.OnAlienKilled();
                    }
                    else
                    {
                        StressSystem.Instance.AddStress(5f);
                    }
                }
            }
            else
            {
                string surfaceType = GetSurfaceType(hit);

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayBulletImpact(surfaceType, hit.point);
                }

                SpawnImpactEffect(hit.point, hit.normal, false);
            }
        }
        else
        {
            if (showDebugRay)
            {
                Vector3 endPoint = ray.origin + ray.direction * range;
                Debug.DrawLine(ray.origin, endPoint, missColor, debugRayDuration);
                ShowShotLine(ray.origin, endPoint, missColor);
            }
        }
    }

    IEnumerator MuzzleFlashLight()
    {
        Light flashLight = muzzleFlash.GetComponent<Light>();
        if (flashLight != null)
        {
            flashLight.intensity = 3f;
            yield return new WaitForSeconds(0.03f);
            flashLight.intensity = 0f;
        }
    }

    void ShowHitMarker(bool isKill)
    {
        currentHitMarkerColor = isKill ? hitMarkerKillColor : hitMarkerColor;
        showingHitMarker = true;
        StartCoroutine(HideHitMarkerAfterDelay());
    }

    IEnumerator HideHitMarkerAfterDelay()
    {
        yield return new WaitForSeconds(hitMarkerDuration);
        showingHitMarker = false;
    }

    void OnGUI()
    {
        if (showingHitMarker && hitMarkerTexture != null)
        {
            GUI.color = currentHitMarkerColor;
            float size = 20f;
            float thickness = 2f;
            float gap = 5f;
            float centerX = Screen.width / 2f;
            float centerY = Screen.height / 2f;

            // Draw X pattern for hit marker
            GUI.DrawTexture(new Rect(centerX - gap - size, centerY - gap - size, size, thickness), hitMarkerTexture);
            GUI.DrawTexture(new Rect(centerX - gap - size, centerY - gap - size, thickness, size), hitMarkerTexture);
            GUI.DrawTexture(new Rect(centerX + gap, centerY - gap - size, size, thickness), hitMarkerTexture);
            GUI.DrawTexture(new Rect(centerX + gap + size - thickness, centerY - gap - size, thickness, size), hitMarkerTexture);
            GUI.DrawTexture(new Rect(centerX - gap - size, centerY + gap + size - thickness, size, thickness), hitMarkerTexture);
            GUI.DrawTexture(new Rect(centerX - gap - size, centerY + gap, thickness, size), hitMarkerTexture);
            GUI.DrawTexture(new Rect(centerX + gap, centerY + gap + size - thickness, size, thickness), hitMarkerTexture);
            GUI.DrawTexture(new Rect(centerX + gap + size - thickness, centerY + gap, thickness, size), hitMarkerTexture);

            GUI.color = Color.white;
        }
    }

    string GetSurfaceType(RaycastHit hit)
    {
        // Check tag safely (avoid error if tag doesn't exist)
        try
        {
            string tag = hit.collider.tag;
            if (tag == "Metal") return "Metal";
            if (tag == "Concrete") return "Concrete";
        }
        catch { }

        // Check material name as fallback
        var renderer = hit.collider.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
        {
            string matName = renderer.material.name.ToLower();
            if (matName.Contains("metal")) return "Metal";
            if (matName.Contains("concrete") || matName.Contains("floor")) return "Concrete";
        }

        // Default to metal for space station
        return "Metal";
    }

    void SpawnImpactEffect(Vector3 position, Vector3 normal, bool isBlood)
    {
        GameObject prefab = isBlood ? bloodImpactPrefab : bulletImpactPrefab;

        if (prefab != null)
        {
            GameObject impact = Instantiate(prefab, position, Quaternion.LookRotation(normal));
            Destroy(impact, 2f);
        }
        else
        {
            CreateSimpleImpactEffect(position, normal, isBlood);
        }
    }

    void CreateSimpleImpactEffect(Vector3 position, Vector3 normal, bool isBlood)
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

    void ShowShotLine(Vector3 start, Vector3 end, Color color)
    {
        if (debugLineRenderer != null)
        {
            debugLineRenderer.enabled = true;
            debugLineRenderer.startColor = color;
            debugLineRenderer.endColor = color;
            debugLineRenderer.SetPosition(0, start);
            debugLineRenderer.SetPosition(1, end);
            StartCoroutine(HideShotLine());
        }
    }

    IEnumerator HideShotLine()
    {
        yield return new WaitForSeconds(debugRayDuration);
        if (debugLineRenderer != null)
        {
            debugLineRenderer.enabled = false;
        }
    }

    void OnDestroy()
    {
        if (hitMarkerTexture != null)
        {
            Destroy(hitMarkerTexture);
        }
    }
}

public interface IDamageable
{
    void TakeDamage(float amount);
}
