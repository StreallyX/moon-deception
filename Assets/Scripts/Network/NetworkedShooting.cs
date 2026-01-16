using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Networked shooting - syncs shots across network.
/// Add to Astronaut prefab alongside PlayerShooting.
/// </summary>
[RequireComponent(typeof(PlayerShooting))]
public class NetworkedShooting : NetworkBehaviour
{
    private PlayerShooting localShooting;
    private Camera playerCamera;

    [Header("Settings")]
    public float damage = 25f;
    public float range = 100f;
    public LayerMask hitLayers = ~0;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        localShooting = GetComponent<PlayerShooting>();
        playerCamera = GetComponentInChildren<Camera>(true);

        if (!IsOwner)
        {
            // Disable local shooting for non-owners
            if (localShooting != null)
            {
                localShooting.enabled = false;
            }
        }

        Debug.Log($"[NetworkedShooting] Spawned - IsOwner: {IsOwner}");
    }

    void Update()
    {
        // Only owner can shoot
        if (!IsOwner || !IsSpawned) return;

        // Check for shoot input
        if (Input.GetButton("Fire1"))
        {
            TryShoot();
        }
    }

    void TryShoot()
    {
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>(true);
            if (playerCamera == null) return;
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        // Send shot to server
        ShootServerRpc(ray.origin, ray.direction);
    }

    [ServerRpc]
    void ShootServerRpc(Vector3 origin, Vector3 direction)
    {
        // Server processes the shot
        Ray ray = new Ray(origin, direction);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, range, hitLayers))
        {
            Debug.Log($"[NetworkedShooting] Server: Hit {hit.collider.gameObject.name}");

            // Try to find damageable (with parent check for child meshes)
            var damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable == null)
                damageable = hit.collider.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                damageable.TakeDamage(damage);

                // Notify all clients of hit
                HitEffectClientRpc(hit.point, hit.normal, true);
            }
            else
            {
                // Environment hit
                HitEffectClientRpc(hit.point, hit.normal, false);
            }
        }

        // Play shoot effects on all clients
        ShootEffectClientRpc();
    }

    [ClientRpc]
    void ShootEffectClientRpc()
    {
        // Play muzzle flash and sound on all clients
        if (localShooting != null && localShooting.muzzleFlash != null)
        {
            localShooting.muzzleFlash.Play();
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayGunshot();
        }
    }

    [ClientRpc]
    void HitEffectClientRpc(Vector3 hitPoint, Vector3 hitNormal, bool isBlood)
    {
        // Play hit effects on all clients
        if (AudioManager.Instance != null)
        {
            string surfaceType = isBlood ? "Flesh" : "Metal";
            AudioManager.Instance.PlayBulletImpact(surfaceType, hitPoint);
        }

        // Create impact particle (simplified)
        CreateImpactEffect(hitPoint, hitNormal, isBlood);
    }

    void CreateImpactEffect(Vector3 position, Vector3 normal, bool isBlood)
    {
        GameObject impactObj = new GameObject("NetworkedImpact");
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
}
