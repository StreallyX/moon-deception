using UnityEngine;

/// <summary>
/// Simple crosshair UI - draws a crosshair at screen center.
/// Add this script to any GameObject in the scene.
/// </summary>
public class SimpleCrosshair : MonoBehaviour
{
    [Header("Crosshair Settings")]
    public Color crosshairColor = Color.white;
    public float size = 10f;
    public float thickness = 2f;
    public float gap = 5f;
    
    [Header("Optional")]
    public bool showDot = true;
    public float dotSize = 2f;

    private Texture2D crosshairTexture;
    private GUIStyle crosshairStyle;

    void Start()
    {
        // Create a 1x1 white texture for drawing
        crosshairTexture = new Texture2D(1, 1);
        crosshairTexture.SetPixel(0, 0, Color.white);
        crosshairTexture.Apply();
    }

    void OnGUI()
    {
        if (crosshairTexture == null) return;

        GUI.color = crosshairColor;
        
        float centerX = Screen.width / 2f;
        float centerY = Screen.height / 2f;

        // Top line
        GUI.DrawTexture(new Rect(centerX - thickness / 2f, centerY - gap - size, thickness, size), crosshairTexture);
        
        // Bottom line
        GUI.DrawTexture(new Rect(centerX - thickness / 2f, centerY + gap, thickness, size), crosshairTexture);
        
        // Left line
        GUI.DrawTexture(new Rect(centerX - gap - size, centerY - thickness / 2f, size, thickness), crosshairTexture);
        
        // Right line
        GUI.DrawTexture(new Rect(centerX + gap, centerY - thickness / 2f, size, thickness), crosshairTexture);

        // Center dot
        if (showDot)
        {
            GUI.DrawTexture(new Rect(centerX - dotSize / 2f, centerY - dotSize / 2f, dotSize, dotSize), crosshairTexture);
        }
    }

    void OnDestroy()
    {
        if (crosshairTexture != null)
        {
            Destroy(crosshairTexture);
        }
    }
}
