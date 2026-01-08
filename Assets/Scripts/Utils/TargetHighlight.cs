using UnityEngine;

/// <summary>
/// Adds/removes red outline/highlight effect on GameObjects.
/// Uses emission color change for simplicity.
/// </summary>
public class TargetHighlight : MonoBehaviour
{
    [Header("Highlight Settings")]
    public Color highlightColor = Color.red;
    public float emissionIntensity = 2f;
    
    private Renderer[] renderers;
    private Material[][] originalMaterials;
    private bool isHighlighted = false;
    
    void Awake()
    {
        CacheRenderers();
    }
    
    void CacheRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>();
        originalMaterials = new Material[renderers.Length][];
        
        for (int i = 0; i < renderers.Length; i++)
        {
            originalMaterials[i] = renderers[i].materials;
        }
    }
    
    public void ApplyHighlight()
    {
        if (isHighlighted) return;
        
        if (renderers == null)
        {
            CacheRenderers();
        }
        
        foreach (Renderer rend in renderers)
        {
            foreach (Material mat in rend.materials)
            {
                // Enable emission
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", highlightColor * emissionIntensity);
            }
        }
        
        isHighlighted = true;
    }
    
    public void RemoveHighlight()
    {
        if (!isHighlighted) return;
        
        if (renderers == null) return;
        
        foreach (Renderer rend in renderers)
        {
            foreach (Material mat in rend.materials)
            {
                // Disable emission
                mat.SetColor("_EmissionColor", Color.black);
            }
        }
        
        isHighlighted = false;
    }
    
    void OnDestroy()
    {
        // Clean up - remove highlight on destroy
        if (isHighlighted)
        {
            RemoveHighlight();
        }
    }
}
