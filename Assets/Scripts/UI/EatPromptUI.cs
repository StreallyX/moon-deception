using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple UI that shows/hides "Press E to EAT" prompt.
/// </summary>
public class EatPromptUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject promptPanel;
    public Text promptText;
    
    [Header("Settings")]
    public string promptMessage = "Press E to EAT";
    public Color textColor = Color.red;
    
    private bool isVisible = false;
    
    void Start()
    {
        // Auto-find or create UI elements
        if (promptPanel == null)
        {
            promptPanel = GameObject.Find("EatPromptPanel");
        }
        
        if (promptPanel == null)
        {
            CreatePromptUI();
        }
        
        // CRITICAL: Hide prompt by default - only show when looking at valid target
        Hide();
        Debug.Log("[EatPromptUI] Initialized - prompt hidden by default");
    }
    
    public void Show()
    {
        SetVisible(true);
    }
    
    public void Hide()
    {
        SetVisible(false);
    }
    
    void CreatePromptUI()
    {
        // Find or create canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }
        
        // Create panel
        promptPanel = new GameObject("EatPromptPanel");
        promptPanel.transform.SetParent(canvas.transform, false);
        
        RectTransform rectTransform = promptPanel.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.3f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.3f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(300, 50);
        
        // Create text
        GameObject textObj = new GameObject("PromptText");
        textObj.transform.SetParent(promptPanel.transform, false);
        
        promptText = textObj.AddComponent<Text>();
        promptText.text = promptMessage;
        promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        promptText.fontSize = 28;
        promptText.color = textColor;
        promptText.alignment = TextAnchor.MiddleCenter;
        promptText.fontStyle = FontStyle.Bold;
        
        // Add outline for visibility
        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, -2);
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
    }
    
    public void SetVisible(bool visible)
    {
        isVisible = visible;
        if (promptPanel != null)
        {
            promptPanel.SetActive(visible);
        }
    }
    
    public bool IsVisible()
    {
        return isVisible;
    }
}
