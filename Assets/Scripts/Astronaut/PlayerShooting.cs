using UnityEngine;
using System.Collections;

public class PlayerShooting : MonoBehaviour
{
    [Header("Weapon Settings")]
    public float range = 100f;
    public float damage = 25f;
    public LayerMask hitLayers = ~0;
    public float fireRate = 0.15f;

    [Header("Magazine & Reload")]
    public int magazineSize = 8;
    public int currentAmmo = 8;
    public float reloadTime = 1.5f;
    public bool infiniteAmmo = false;

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
    private float lastShotTime = -999f; // Simple time tracking
    private bool showingHitMarker = false;
    private Color currentHitMarkerColor;
    private Texture2D hitMarkerTexture;

    // Reload state
    private bool isReloading = false;
    private float reloadProgress = 0f;
    private string weaponName = "PISTOL";

    // Events for UI
    public System.Action<int, int> OnAmmoChanged;
    public System.Action<float> OnReloadProgress;

    void Start()
    {
        // DEBUG: Check how many PlayerShooting scripts exist
        var allShooters = FindObjectsByType<PlayerShooting>(FindObjectsSortMode.None);
        if (allShooters.Length > 1)
        {
            Debug.LogError($"[PlayerShooting] WARNING: {allShooters.Length} PlayerShooting scripts found! This will cause issues!");
        }

        FindCamera();
        currentAmmo = magazineSize;

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

    void OnEnable()
    {
        // Find camera when enabled
        FindCamera();
    }

    void FindCamera()
    {
        // First try to find camera as child of this object
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>(true);
        }

        // Fallback: main camera
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (playerCamera != null)
        {
            Debug.Log($"[PlayerShooting] Found camera: {playerCamera.gameObject.name}");
        }
    }

    void Update()
    {
        // Make sure we have a camera
        if (playerCamera == null)
        {
            FindCamera();
            if (playerCamera == null) return;
        }

        // Don't shoot while reloading
        if (isReloading) return;

        // Manual reload with R
        if (Input.GetKeyDown(KeyCode.R) && currentAmmo < magazineSize && !infiniteAmmo)
        {
            StartCoroutine(Reload());
            return;
        }

        // Shoot - simple time-based check (most reliable)
        float timeSinceLastShot = Time.time - lastShotTime;

        if (Input.GetButton("Fire1") && timeSinceLastShot >= fireRate && !isReloading)
        {
            if (currentAmmo > 0 || infiniteAmmo)
            {
                lastShotTime = Time.time;
                Shoot();
            }
            else
            {
                // Auto reload when empty
                StartCoroutine(Reload());
            }
        }
    }

    IEnumerator Reload()
    {
        if (isReloading || currentAmmo >= magazineSize) yield break;

        isReloading = true;
        reloadProgress = 0f;

        Debug.Log($"[PlayerShooting] Reloading {weaponName}...");

        // Play reload sound (networked - all players hear it)
        if (NetworkAudioManager.Instance != null)
        {
            NetworkAudioManager.Instance.PlayReload(transform.position);
        }
        else if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayReload();
        }

        float elapsed = 0f;
        while (elapsed < reloadTime)
        {
            elapsed += Time.deltaTime;
            reloadProgress = elapsed / reloadTime;
            OnReloadProgress?.Invoke(reloadProgress);
            yield return null;
        }

        currentAmmo = magazineSize;
        isReloading = false;
        reloadProgress = 0f;

        OnAmmoChanged?.Invoke(currentAmmo, magazineSize);
        Debug.Log($"[PlayerShooting] Reload complete! Ammo: {currentAmmo}/{magazineSize}");
    }

    void Shoot()
    {
        // Consume ammo
        if (!infiniteAmmo)
        {
            currentAmmo--;
            OnAmmoChanged?.Invoke(currentAmmo, magazineSize);
        }

        // Play gunshot sound (networked - all players hear it)
        bool isMinigun = (weaponName == "MINIGUN");
        if (NetworkAudioManager.Instance != null)
        {
            NetworkAudioManager.Instance.PlayGunshot(transform.position, isMinigun);
        }
        else if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayGunshot3D(transform.position, isMinigun);
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

        // Raycast stops at the first collider it hits (walls, objects, NPCs, etc.)
        if (Physics.Raycast(ray, out hit, range, hitLayers, QueryTriggerInteraction.Ignore))
        {
            // DEBUG: Log what we hit
            Debug.Log($"[Shooting] HIT: {hit.collider.gameObject.name} (Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)})");

            if (showDebugRay)
            {
                Debug.DrawLine(ray.origin, hit.point, hitColor, debugRayDuration);
                ShowShotLine(ray.origin, hit.point, hitColor);
            }

            // Try to get components on the hit object OR its parent (for child meshes)
            var damageable = hit.collider.GetComponent<IDamageable>();
            var npc = hit.collider.GetComponent<NPCBehavior>();
            var alienController = hit.collider.GetComponent<AlienController>();
            var alienHealth = hit.collider.GetComponent<AlienHealth>();

            // If not found on hit object, check parent hierarchy
            if (damageable == null)
                damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (npc == null)
                npc = hit.collider.GetComponentInParent<NPCBehavior>();
            if (alienController == null)
                alienController = hit.collider.GetComponentInParent<AlienController>();
            if (alienHealth == null)
                alienHealth = hit.collider.GetComponentInParent<AlienHealth>();

            // DEBUG: Log what components we found
            Debug.Log($"[Shooting] Components (with parent check) - IDamageable:{damageable != null}, NPC:{npc != null}, AlienController:{alienController != null}, AlienHealth:{alienHealth != null}");

            if (damageable != null)
            {
                bool wasKill = npc != null || alienHealth != null;
                bool isAlien = alienController != null || alienHealth != null || (npc != null && npc.IsAlien);

                // MULTIPLAYER: Sync damage via RPC for aliens
                if (isAlien && alienHealth != null)
                {
                    // Check if alien has NetworkObject for multiplayer sync
                    var alienNetObj = alienHealth.GetComponent<Unity.Netcode.NetworkObject>();
                    if (alienNetObj != null && alienNetObj.IsSpawned && NetworkAudioManager.Instance != null)
                    {
                        // Use RPC to sync damage to all clients
                        var player = FindAnyNetworkedPlayer();
                        if (player != null)
                        {
                            player.AstronautShootAlienServerRpc(alienNetObj.NetworkObjectId, damage);
                            Debug.Log($"[Shooting] Sent damage RPC for alien {alienNetObj.NetworkObjectId}");
                        }
                        else
                        {
                            // Fallback: local damage
                            damageable.TakeDamage(damage);
                        }
                    }
                    else
                    {
                        // Single player or no network - local damage
                        damageable.TakeDamage(damage);
                    }
                }
                else
                {
                    // NPCs and other targets - local damage is fine
                    damageable.TakeDamage(damage);
                }

                if (showHitMarker)
                {
                    ShowHitMarker(wasKill);
                }

                // Play bullet impact sound (networked)
                if (NetworkAudioManager.Instance != null)
                {
                    NetworkAudioManager.Instance.PlayBulletImpact("Flesh", hit.point);
                    // Spawn networked impact effect (visible to all players)
                    NetworkAudioManager.Instance.SpawnImpactEffect(hit.point, hit.normal, true);
                }
                else if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayBulletImpact("Flesh", hit.point);
                    SpawnImpactEffect(hit.point, hit.normal, true);
                }

                // Handle stress changes
                if (StressSystem.Instance != null)
                {
                    if (isAlien)
                    {
                        // Hit an alien - reduce stress
                        StressSystem.Instance.ReduceStress(10f);
                        Debug.Log("[Shooting] Hit Alien - Stress reduced by 10");
                    }
                    else if (npc != null && !npc.IsAlien)
                    {
                        // Hit innocent NPC - increase stress
                        StressSystem.Instance.AddStress(10f);
                        Debug.Log("[Shooting] Hit Innocent - Stress increased by 10");
                    }
                }
            }
            else
            {
                string surfaceType = GetSurfaceType(hit);

                // Play bullet impact sound (networked)
                if (NetworkAudioManager.Instance != null)
                {
                    NetworkAudioManager.Instance.PlayBulletImpact(surfaceType, hit.point);
                    // Spawn networked impact effect (visible to all players)
                    NetworkAudioManager.Instance.SpawnImpactEffect(hit.point, hit.normal, false);
                }
                else if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayBulletImpact(surfaceType, hit.point);
                    SpawnImpactEffect(hit.point, hit.normal, false);
                }
            }

            // Spawn bullet tracer (visible to all players)
            Vector3 muzzlePos = playerCamera.transform.position + playerCamera.transform.forward * 0.5f;
            if (NetworkAudioManager.Instance != null)
            {
                NetworkAudioManager.Instance.SpawnBulletTracer(muzzlePos, hit.point, true);
            }
            else if (BulletTracerManager.Instance != null)
            {
                BulletTracerManager.Instance.SpawnTracer(muzzlePos, hit.point, true);
            }
        }
        else
        {
            // Miss - no hit
            Vector3 endPoint = ray.origin + ray.direction * range;

            if (showDebugRay)
            {
                Debug.DrawLine(ray.origin, endPoint, missColor, debugRayDuration);
                ShowShotLine(ray.origin, endPoint, missColor);
            }

            // Spawn bullet tracer for miss (visible to all players)
            Vector3 muzzlePos = playerCamera.transform.position + playerCamera.transform.forward * 0.5f;
            if (NetworkAudioManager.Instance != null)
            {
                NetworkAudioManager.Instance.SpawnBulletTracer(muzzlePos, endPoint, false);
            }
            else if (BulletTracerManager.Instance != null)
            {
                BulletTracerManager.Instance.SpawnTracer(muzzlePos, endPoint, false);
            }
        }
    }

    // ==================== WEAPON UPGRADE (called by DefenseZone) ====================

    /// <summary>
    /// Upgrade to machine gun - high damage, fast fire rate, no reload needed
    /// </summary>
    public void UpgradeToMinigun()
    {
        weaponName = "MINIGUN";
        damage = 50f;
        fireRate = 0.08f;
        range = 150f;
        infiniteAmmo = true;
        magazineSize = 999;
        currentAmmo = 999;
        reloadTime = 0f;

        // Bigger muzzle flash for minigun
        shakeMagnitude = 0.04f;

        Debug.Log("[PlayerShooting] MINIGUN ACQUIRED! Infinite ammo, no reload!");
    }

    /// <summary>
    /// Reset to default pistol
    /// </summary>
    public void ResetToPistol()
    {
        weaponName = "PISTOL";
        damage = 25f;
        fireRate = 0.15f;
        range = 100f;
        infiniteAmmo = false;
        magazineSize = 8;
        currentAmmo = 8;
        reloadTime = 1.5f;
        shakeMagnitude = 0.02f;
    }

    // ==================== EFFECTS ====================

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

    string GetSurfaceType(RaycastHit hit)
    {
        try
        {
            string tag = hit.collider.tag;
            if (tag == "Metal") return "Metal";
            if (tag == "Concrete") return "Concrete";
        }
        catch { }

        var renderer = hit.collider.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
        {
            string matName = renderer.material.name.ToLower();
            if (matName.Contains("metal")) return "Metal";
            if (matName.Contains("concrete") || matName.Contains("floor")) return "Concrete";
        }

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

    // ==================== UI ====================

    void OnGUI()
    {
        // Don't show UI if game ended
        if (GameManager.Instance != null && GameManager.Instance.CurrentPhase == GameManager.GamePhase.Ended) return;

        // Hit marker
        if (showingHitMarker && hitMarkerTexture != null)
        {
            GUI.color = currentHitMarkerColor;
            float size = 20f;
            float thickness = 2f;
            float gap = 5f;
            float centerX = Screen.width / 2f;
            float centerY = Screen.height / 2f;

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

        // Ammo display
        GUIStyle ammoStyle = new GUIStyle(GUI.skin.label);
        ammoStyle.fontSize = 24;
        ammoStyle.fontStyle = FontStyle.Bold;
        ammoStyle.alignment = TextAnchor.LowerRight;
        ammoStyle.normal.textColor = Color.white;

        string ammoText = infiniteAmmo ? "∞" : $"{currentAmmo}/{magazineSize}";
        GUI.Label(new Rect(Screen.width - 200, Screen.height - 80, 180, 40), $"{weaponName}", ammoStyle);

        ammoStyle.fontSize = 32;
        GUI.Label(new Rect(Screen.width - 200, Screen.height - 50, 180, 40), ammoText, ammoStyle);

        // Reload indicator
        if (isReloading)
        {
            GUIStyle reloadStyle = new GUIStyle(GUI.skin.label);
            reloadStyle.fontSize = 20;
            reloadStyle.fontStyle = FontStyle.Bold;
            reloadStyle.alignment = TextAnchor.MiddleCenter;
            reloadStyle.normal.textColor = Color.yellow;

            GUI.Label(new Rect(Screen.width / 2 - 100, Screen.height / 2 + 50, 200, 30), "RELOADING...", reloadStyle);

            // Progress bar
            float barWidth = 150f;
            float barHeight = 10f;
            float barX = Screen.width / 2 - barWidth / 2;
            float barY = Screen.height / 2 + 80;

            // Background
            GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            GUI.DrawTexture(new Rect(barX, barY, barWidth, barHeight), hitMarkerTexture);

            // Fill
            GUI.color = Color.yellow;
            GUI.DrawTexture(new Rect(barX, barY, barWidth * reloadProgress, barHeight), hitMarkerTexture);

            GUI.color = Color.white;
        }

        // Low ammo warning
        if (!infiniteAmmo && currentAmmo <= 2 && currentAmmo > 0 && !isReloading)
        {
            GUIStyle lowAmmoStyle = new GUIStyle(GUI.skin.label);
            lowAmmoStyle.fontSize = 16;
            lowAmmoStyle.alignment = TextAnchor.LowerRight;
            float pulse = Mathf.PingPong(Time.time * 3f, 1f);
            lowAmmoStyle.normal.textColor = new Color(1f, 0.5f, 0f, 0.5f + pulse * 0.5f);

            GUI.Label(new Rect(Screen.width - 200, Screen.height - 100, 180, 20), "[R] RELOAD", lowAmmoStyle);
        }
    }

    void OnDestroy()
    {
        if (hitMarkerTexture != null)
        {
            Destroy(hitMarkerTexture);
        }
    }

    /// <summary>
    /// Find any NetworkedPlayer to send RPCs through
    /// </summary>
    NetworkedPlayer FindAnyNetworkedPlayer()
    {
        if (Unity.Netcode.NetworkManager.Singleton == null) return null;

        foreach (var client in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
            {
                var networkedPlayer = client.PlayerObject.GetComponent<NetworkedPlayer>();
                if (networkedPlayer != null && networkedPlayer.IsSpawned)
                {
                    return networkedPlayer;
                }
            }
        }

        return FindFirstObjectByType<NetworkedPlayer>();
    }
}

public interface IDamageable
{
    void TakeDamage(float amount);
}
