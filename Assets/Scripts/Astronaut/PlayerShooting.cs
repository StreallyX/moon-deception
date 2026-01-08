using UnityEngine;

public class PlayerShooting : MonoBehaviour
{
    [Header("Shooting Settings")]
    public float range = 100f;
    public float damage = 25f;
    public LayerMask hitLayers = ~0; // Default to all layers

    [Header("Debug Visuals")]
    public bool showDebugRay = true;
    public float debugRayDuration = 1f;
    public Color hitColor = Color.red;
    public Color missColor = Color.yellow;

    private Camera playerCamera;
    private LineRenderer debugLineRenderer;

    void Start()
    {
        playerCamera = Camera.main;
        
        // Create LineRenderer for visible debug rays in game view
        debugLineRenderer = gameObject.AddComponent<LineRenderer>();
        debugLineRenderer.startWidth = 0.02f;
        debugLineRenderer.endWidth = 0.02f;
        debugLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        debugLineRenderer.positionCount = 2;
        debugLineRenderer.enabled = false;
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

        Debug.Log($"[Shooting] Firing from {ray.origin} in direction {ray.direction}");

        if (Physics.Raycast(ray, out hit, range, hitLayers))
        {
            Debug.Log($"[Shooting] Hit: {hit.collider.name} at distance {hit.distance:F2}m");

            // Draw debug line (visible in Scene view)
            Debug.DrawLine(ray.origin, hit.point, hitColor, debugRayDuration);
            
            // Also draw in Game view using LineRenderer
            if (showDebugRay)
            {
                ShowShotLine(ray.origin, hit.point, hitColor);
            }

            // Apply damage if target has health component
            var damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage);
                
                // Check if it's an NPC and notify stress system
                var npc = hit.collider.GetComponent<NPCBehavior>();
                if (npc != null)
                {
                    // Check health after damage to see if killed
                    StartCoroutine(CheckKill(npc));
                }
            }
        }
        else
        {
            Debug.Log("[Shooting] Shot missed");

            Vector3 endPoint = ray.origin + ray.direction * range;
            Debug.DrawLine(ray.origin, endPoint, missColor, debugRayDuration);
            
            if (showDebugRay)
            {
                ShowShotLine(ray.origin, endPoint, missColor);
            }
        }
    }

    private void ShowShotLine(Vector3 start, Vector3 end, Color color)
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

    private System.Collections.IEnumerator HideShotLine()
    {
        yield return new WaitForSeconds(debugRayDuration);
        if (debugLineRenderer != null)
        {
            debugLineRenderer.enabled = false;
        }
    }

    private System.Collections.IEnumerator CheckKill(NPCBehavior npc)
    {
        yield return null; // Wait one frame for damage to apply
        
        if (npc == null || npc.CurrentState == NPCBehavior.NPCState.Dead)
        {
            // NPC was killed, notify stress system
            if (StressSystem.Instance != null)
            {
                if (npc != null && npc.IsAlien)
                {
                    StressSystem.Instance.OnAlienKilled();
                }
                else
                {
                    StressSystem.Instance.OnInnocentKilled();
                }
            }
        }
    }
}

public interface IDamageable
{
    void TakeDamage(float amount);
}
