using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages blood decals when NPCs are killed.
/// Blood traces are visible to astronaut to help track alien activity.
/// </summary>
public class BloodDecalManager : MonoBehaviour
{
    public static BloodDecalManager Instance { get; private set; }

    [Header("Decal Settings")]
    [SerializeField] private int maxDecals = 50;
    [SerializeField] private float decalSize = 1.5f;
    [SerializeField] private float decalLifetime = 120f; // 2 minutes
    [SerializeField] private float fadeStartTime = 90f; // Start fading at 1.5 minutes
    [SerializeField] private Color bloodColor = new Color(0.5f, 0f, 0f, 0.8f);

    [Header("Splatter Settings")]
    [SerializeField] private int splatterCount = 3; // Additional small splatters around main decal
    [SerializeField] private float splatterRadius = 2f;
    [SerializeField] private float splatterSizeMin = 0.3f;
    [SerializeField] private float splatterSizeMax = 0.7f;

    private Queue<BloodDecal> decalPool = new Queue<BloodDecal>();
    private List<BloodDecal> activeDecals = new List<BloodDecal>();
    private Material bloodMaterial;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        CreateBloodMaterial();
    }

    void CreateBloodMaterial()
    {
        // Create a simple unlit material for blood
        bloodMaterial = new Material(Shader.Find("Unlit/Transparent"));
        if (bloodMaterial == null)
        {
            // Fallback to standard shader
            bloodMaterial = new Material(Shader.Find("Standard"));
            bloodMaterial.SetFloat("_Mode", 3); // Transparent mode
            bloodMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            bloodMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            bloodMaterial.SetInt("_ZWrite", 0);
            bloodMaterial.DisableKeyword("_ALPHATEST_ON");
            bloodMaterial.EnableKeyword("_ALPHABLEND_ON");
            bloodMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            bloodMaterial.renderQueue = 3000;
        }
        bloodMaterial.color = bloodColor;

        // Create a procedural blood texture
        Texture2D bloodTexture = CreateBloodTexture();
        bloodMaterial.mainTexture = bloodTexture;
    }

    Texture2D CreateBloodTexture()
    {
        int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

        Color transparent = new Color(0, 0, 0, 0);
        Color blood = bloodColor;

        // Create circular gradient with rough edges
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float maxRadius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);

                // Add noise to radius for organic shape
                float noise = Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.3f;
                float adjustedRadius = maxRadius * (0.7f + noise);

                if (dist < adjustedRadius)
                {
                    float alpha = 1f - (dist / adjustedRadius);
                    alpha = Mathf.Pow(alpha, 0.5f); // Softer falloff

                    // Add some variation in color
                    float colorNoise = Mathf.PerlinNoise(x * 0.1f + 100, y * 0.1f) * 0.2f;
                    Color pixelColor = new Color(
                        blood.r + colorNoise,
                        blood.g,
                        blood.b,
                        blood.a * alpha
                    );
                    texture.SetPixel(x, y, pixelColor);
                }
                else
                {
                    texture.SetPixel(x, y, transparent);
                }
            }
        }

        texture.Apply();
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }

    /// <summary>
    /// Spawn blood decal at position (called when NPC dies)
    /// </summary>
    public void SpawnBloodDecal(Vector3 position)
    {
        Debug.Log($"[BloodDecalManager] Spawning blood decal at {position}");

        // Raycast down to find ground
        Vector3 groundPos = position;
        if (Physics.Raycast(position + Vector3.up, Vector3.down, out RaycastHit hit, 10f))
        {
            groundPos = hit.point + Vector3.up * 0.01f; // Slight offset to avoid z-fighting
        }

        // Spawn main decal
        SpawnSingleDecal(groundPos, decalSize);

        // Spawn splatters
        for (int i = 0; i < splatterCount; i++)
        {
            Vector2 offset = Random.insideUnitCircle * splatterRadius;
            Vector3 splatterPos = groundPos + new Vector3(offset.x, 0, offset.y);

            // Raycast for splatter position
            if (Physics.Raycast(splatterPos + Vector3.up, Vector3.down, out RaycastHit splatterHit, 10f))
            {
                splatterPos = splatterHit.point + Vector3.up * 0.01f;
            }

            float size = Random.Range(splatterSizeMin, splatterSizeMax);
            SpawnSingleDecal(splatterPos, size);
        }
    }

    void SpawnSingleDecal(Vector3 position, float size)
    {
        BloodDecal decal;

        // Reuse from pool or create new
        if (decalPool.Count > 0)
        {
            decal = decalPool.Dequeue();
            decal.gameObject.SetActive(true);
        }
        else if (activeDecals.Count >= maxDecals)
        {
            // Remove oldest decal
            decal = activeDecals[0];
            activeDecals.RemoveAt(0);
        }
        else
        {
            decal = CreateDecalObject();
        }

        // Setup decal
        decal.transform.position = position;
        decal.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
        decal.transform.localScale = new Vector3(size, size, size);
        decal.spawnTime = Time.time;
        decal.lifetime = decalLifetime;
        decal.fadeStartTime = fadeStartTime;
        decal.ResetAlpha();

        activeDecals.Add(decal);
    }

    BloodDecal CreateDecalObject()
    {
        GameObject decalObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        decalObj.name = "BloodDecal";
        decalObj.transform.SetParent(transform);

        // Remove collider
        var collider = decalObj.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        // Set material
        var renderer = decalObj.GetComponent<MeshRenderer>();
        renderer.material = bloodMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        // Add BloodDecal component
        BloodDecal decal = decalObj.AddComponent<BloodDecal>();
        decal.originalColor = bloodColor;

        return decal;
    }

    void Update()
    {
        // Update active decals (fade and remove expired)
        for (int i = activeDecals.Count - 1; i >= 0; i--)
        {
            BloodDecal decal = activeDecals[i];
            float age = Time.time - decal.spawnTime;

            if (age >= decal.lifetime)
            {
                // Return to pool
                decal.gameObject.SetActive(false);
                decalPool.Enqueue(decal);
                activeDecals.RemoveAt(i);
            }
            else if (age >= decal.fadeStartTime)
            {
                // Fade out
                float fadeProgress = (age - decal.fadeStartTime) / (decal.lifetime - decal.fadeStartTime);
                decal.SetAlpha(1f - fadeProgress);
            }
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        // Cleanup material
        if (bloodMaterial != null)
        {
            Destroy(bloodMaterial);
        }
    }
}

/// <summary>
/// Component attached to each blood decal for tracking
/// </summary>
public class BloodDecal : MonoBehaviour
{
    public float spawnTime;
    public float lifetime;
    public float fadeStartTime;
    public Color originalColor;

    private MeshRenderer meshRenderer;

    void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
    }

    public void SetAlpha(float alpha)
    {
        if (meshRenderer != null && meshRenderer.material != null)
        {
            Color color = originalColor;
            color.a = originalColor.a * alpha;
            meshRenderer.material.color = color;
        }
    }

    public void ResetAlpha()
    {
        if (meshRenderer != null && meshRenderer.material != null)
        {
            meshRenderer.material.color = originalColor;
        }
    }
}
