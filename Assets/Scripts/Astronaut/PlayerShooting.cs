using UnityEngine;

public class PlayerShooting : MonoBehaviour
{
    [Header("Shooting Settings")]
    public float range = 100f;
    public float damage = 25f;
    public LayerMask hitLayers;

    [Header("Debug Visuals")]
    public bool showDebugRay = true;
    public float debugRayDuration = 0.5f;
    public Color hitColor = Color.red;
    public Color missColor = Color.yellow;

    private Camera playerCamera;

    void Start()
    {
        playerCamera = Camera.main;
    }

    void Update()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            Shoot();
        }
    }

    void Shoot()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, range, hitLayers))
        {
            Debug.Log($"Hit: {hit.collider.name} at distance {hit.distance:F2}m");

            if (showDebugRay)
            {
                Debug.DrawLine(ray.origin, hit.point, hitColor, debugRayDuration);
            }

            // Apply damage if target has health component
            var health = hit.collider.GetComponent<IDamageable>();
            if (health != null)
            {
                health.TakeDamage(damage);
            }
        }
        else
        {
            Debug.Log("Shot missed");

            if (showDebugRay)
            {
                Debug.DrawRay(ray.origin, ray.direction * range, missColor, debugRayDuration);
            }
        }
    }
}

public interface IDamageable
{
    void TakeDamage(float amount);
}
