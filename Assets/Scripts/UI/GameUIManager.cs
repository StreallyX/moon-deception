using UnityEngine;
using UnityEngine.UI;

public enum PlayerType
{
    None,
    Astronaut,
    Alien
}

public class GameUIManager : MonoBehaviour
{
    public static GameUIManager Instance { get; private set; }
    
    [Header("UI Containers")]
    [SerializeField] private GameObject astronautUIContainer;
    [SerializeField] private GameObject alienUIContainer;
    
    [Header("Astronaut UI")]
    [SerializeField] private GameObject stressBarPanel;
    [SerializeField] private Slider stressSlider;
    [SerializeField] private Image stressBarFill;
    [SerializeField] private Image stressBarBackground;
    [SerializeField] private Text stressLabel;

    [SerializeField] private GameObject astronautHealthBarPanel;
    [SerializeField] private Slider astronautHealthSlider;
    [SerializeField] private Image astronautHealthBarFill;
    [SerializeField] private Image astronautHealthBarBackground;
    [SerializeField] private Text astronautHealthLabel;

    [Header("Alien UI")]
    [SerializeField] private GameObject hungerBarPanel;
    [SerializeField] private Slider hungerSlider;
    [SerializeField] private Image hungerBarFill;
    [SerializeField] private Image hungerBarBackground;
    [SerializeField] private Text hungerLabel;

    [SerializeField] private GameObject alienHealthBarPanel;
    [SerializeField] private Slider alienHealthSlider;
    [SerializeField] private Image alienHealthBarFill;
    [SerializeField] private Image alienHealthBarBackground;
    [SerializeField] private Text alienHealthLabel;
    
    [Header("Interaction UI")]
    [SerializeField] private GameObject interactionPrompt;
    [SerializeField] private Text interactionText;
    
    [Header("UI Style Settings")]
    [SerializeField] private Color stressBarBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color hungerBarBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color interactionPromptColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private Color interactionTextColor = Color.white;
    
    private PlayerType currentPlayerType = PlayerType.None;
    private Canvas mainCanvas;
    
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
    
    void Start()
    {
        SetupCanvas();
        CreateAstronautUI();
        CreateAlienUI();
        CreateInteractionPrompt();

        HideAllUI();

        Debug.Log("[GameUIManager] Initialized");
    }

    void SetupCanvas()
    {
        mainCanvas = FindFirstObjectByType<Canvas>();
        if (mainCanvas == null)
        {
            GameObject canvasObj = new GameObject("GameCanvas");
            mainCanvas = canvasObj.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();
        }
    }
    
    void CreateAstronautUI()
    {
        astronautUIContainer = new GameObject("AstronautUI");
        astronautUIContainer.transform.SetParent(mainCanvas.transform, false);
        RectTransform containerRect = astronautUIContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0, 1);
        containerRect.anchorMax = new Vector2(0, 1);
        containerRect.pivot = new Vector2(0, 1);
        containerRect.anchoredPosition = new Vector2(20, -20);
        containerRect.sizeDelta = new Vector2(300, 50);

        stressBarPanel = CreateStyledBar(
            astronautUIContainer.transform,
            "StressBarPanel",
            new Vector2(0, -10),
            new Vector2(300, 40),
            out stressSlider,
            out stressBarFill,
            out stressBarBackground,
            out stressLabel,
            "STRESS",
            stressBarBackgroundColor
        );

        astronautHealthBarPanel = CreateStyledBar(
            mainCanvas.transform,
            "AstronautHealthBarPanel",
            new Vector2(0, 30),
            new Vector2(400, 40),
            out astronautHealthSlider,
            out astronautHealthBarFill,
            out astronautHealthBarBackground,
            out astronautHealthLabel,
            "HEALTH",
            new Color(0.2f, 0.2f, 0.2f, 0.8f)
        );

        RectTransform healthRect = astronautHealthBarPanel.GetComponent<RectTransform>();
        healthRect.anchorMin = new Vector2(0.5f, 0);
        healthRect.anchorMax = new Vector2(0.5f, 0);
        healthRect.pivot = new Vector2(0.5f, 0);
        healthRect.anchoredPosition = new Vector2(0, 30);

        astronautHealthBarPanel.SetActive(false);
        astronautUIContainer.SetActive(false);
    }
    
    void CreateAlienUI()
    {
        alienUIContainer = new GameObject("AlienUI");
        alienUIContainer.transform.SetParent(mainCanvas.transform, false);
        RectTransform containerRect = alienUIContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0, 1);
        containerRect.anchorMax = new Vector2(0, 1);
        containerRect.pivot = new Vector2(0, 1);
        containerRect.anchoredPosition = new Vector2(20, -20);
        containerRect.sizeDelta = new Vector2(300, 80);

        hungerBarPanel = CreateStyledBar(
            alienUIContainer.transform,
            "HungerBarPanel",
            new Vector2(0, -10),
            new Vector2(300, 40),
            out hungerSlider,
            out hungerBarFill,
            out hungerBarBackground,
            out hungerLabel,
            "HUNGER",
            hungerBarBackgroundColor
        );
        hungerSlider.direction = Slider.Direction.RightToLeft;

        alienHealthBarPanel = CreateStyledBar(
            mainCanvas.transform,
            "AlienHealthBarPanel",
            new Vector2(0, 30),
            new Vector2(400, 40),
            out alienHealthSlider,
            out alienHealthBarFill,
            out alienHealthBarBackground,
            out alienHealthLabel,
            "HEALTH",
            new Color(0.2f, 0.2f, 0.2f, 0.8f)
        );

        RectTransform healthRect = alienHealthBarPanel.GetComponent<RectTransform>();
        healthRect.anchorMin = new Vector2(0.5f, 0);
        healthRect.anchorMax = new Vector2(0.5f, 0);
        healthRect.pivot = new Vector2(0.5f, 0);
        healthRect.anchoredPosition = new Vector2(0, 30);

        alienHealthBarPanel.SetActive(false);
        alienUIContainer.SetActive(false);
    }
    
    GameObject CreateStyledBar(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        out Slider slider,
        out Image fillImage,
        out Image backgroundImage,
        out Text label,
        string labelText,
        Color bgColor
    )
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 1);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = position;
        panelRect.sizeDelta = size;
        
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(panel.transform, false);
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 1);
        labelRect.anchorMax = new Vector2(0, 1);
        labelRect.pivot = new Vector2(0, 1);
        labelRect.anchoredPosition = new Vector2(0, 25);
        labelRect.sizeDelta = new Vector2(size.x, 20);
        
        label = labelObj.AddComponent<Text>();
        label.text = labelText;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 16;
        label.fontStyle = FontStyle.Bold;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleLeft;
        
        Outline labelOutline = labelObj.AddComponent<Outline>();
        labelOutline.effectColor = Color.black;
        labelOutline.effectDistance = new Vector2(1, -1);
        
        GameObject sliderObj = new GameObject("Slider");
        sliderObj.transform.SetParent(panel.transform, false);
        
        RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.anchorMin = Vector2.zero;
        sliderRect.anchorMax = Vector2.one;
        sliderRect.sizeDelta = Vector2.zero;
        sliderRect.anchoredPosition = Vector2.zero;
        
        slider = sliderObj.AddComponent<Slider>();
        slider.minValue = 0;
        slider.maxValue = 100;
        slider.value = 100;
        slider.interactable = false;

        GameObject background = new GameObject("Background");
        background.transform.SetParent(sliderObj.transform, false);
        RectTransform bgRect = background.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        
        backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = new Color(bgColor.r, bgColor.g, bgColor.b, 1f); // opaque
        backgroundImage.type = Image.Type.Sliced;

        Outline bgOutline = background.AddComponent<Outline>();
        bgOutline.effectColor = Color.black;
        bgOutline.effectDistance = new Vector2(2, -2);




        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.sizeDelta = new Vector2(-10, -10);
        fillAreaRect.anchoredPosition = Vector2.zero;

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(sliderObj.transform, false);

        RectTransform fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0, 0);
        fillRect.anchorMax = new Vector2(1, 1);
        fillRect.pivot = new Vector2(0, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = Vector2.zero;
        fillRect.offsetMin = new Vector2(3, 3);
        fillRect.offsetMax = new Vector2(-3, -3);


        fillImage = fill.AddComponent<Image>();
        fillImage.color = Color.green;
        fillImage.type = Image.Type.Simple;

        // ✅ MAINTENANT seulement
        slider.fillRect = fillRect;
        slider.targetGraphic = fillImage;
        slider.direction = Slider.Direction.LeftToRight;



        return panel;
    }
    
    void CreateInteractionPrompt()
    {
        interactionPrompt = new GameObject("InteractionPrompt");
        interactionPrompt.transform.SetParent(mainCanvas.transform, false);
        
        RectTransform promptRect = interactionPrompt.AddComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0.5f, 0.5f);
        promptRect.anchorMax = new Vector2(0.5f, 0.5f);
        promptRect.pivot = new Vector2(0.5f, 0.5f);
        promptRect.anchoredPosition = new Vector2(0, -150);
        promptRect.sizeDelta = new Vector2(400, 80);
        
        Image promptBg = interactionPrompt.AddComponent<Image>();
        promptBg.color = new Color(0, 0, 0, 0.7f);
        
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(interactionPrompt.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        
        interactionText = textObj.AddComponent<Text>();
        interactionText.text = "Appuyer sur E";
        interactionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        interactionText.fontSize = 32;
        interactionText.fontStyle = FontStyle.Bold;
        interactionText.color = interactionTextColor;
        interactionText.alignment = TextAnchor.MiddleCenter;
        
        Outline textOutline = textObj.AddComponent<Outline>();
        textOutline.effectColor = Color.black;
        textOutline.effectDistance = new Vector2(2, -2);
        
        interactionPrompt.SetActive(false);
    }
    
    public void SetPlayerType(PlayerType type)
    {
        currentPlayerType = type;
        
        switch (type)
        {
            case PlayerType.Astronaut:
                ShowAstronautUI();
                HideAlienUI();
                break;
            case PlayerType.Alien:
                ShowAlienUI();
                HideAstronautUI();
                break;
            case PlayerType.None:
                HideAllUI();
                break;
        }
        
        Debug.Log($"[GameUIManager] Player type set to: {type}");
    }
    
    void ShowAstronautUI()
    {
        if (astronautUIContainer != null)
            astronautUIContainer.SetActive(true);
        if (astronautHealthBarPanel != null)
            astronautHealthBarPanel.SetActive(true);
    }

    void HideAstronautUI()
    {
        if (astronautUIContainer != null)
            astronautUIContainer.SetActive(false);
        if (astronautHealthBarPanel != null)
            astronautHealthBarPanel.SetActive(false);
    }

    void ShowAlienUI()
    {
        if (alienUIContainer != null)
            alienUIContainer.SetActive(true);
        if (alienHealthBarPanel != null)
            alienHealthBarPanel.SetActive(true);
    }

    void HideAlienUI()
    {
        if (alienUIContainer != null)
            alienUIContainer.SetActive(false);
        if (alienHealthBarPanel != null)
            alienHealthBarPanel.SetActive(false);
    }
    
    public void HideAllUI()
    {
        HideAstronautUI();
        HideAlienUI();
        HideInteractionPrompt();
    }

    /// <summary>
    /// Call when game ends to hide all gameplay UI
    /// </summary>
    public void OnGameEnded()
    {
        HideAllUI();
        currentPlayerType = PlayerType.None;
        Debug.Log("[GameUIManager] Game ended - all UI hidden");
    }
    
    public void ShowInteractionPrompt(string message = "Appuyer sur E")
    {
        if (interactionPrompt != null)
        {
            interactionText.text = message;
            interactionPrompt.SetActive(true);
        }
    }
    
    public void HideInteractionPrompt()
    {
        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);
    }
    
    public void UpdateStressBar(float value, float maxValue)
    {
        if (stressBarFill != null)
        {
            float percent = value / maxValue;
            stressSlider.maxValue = maxValue;
            stressSlider.value = value;
            stressBarFill.color = Color.Lerp(Color.green, Color.red, percent);
        }
    }

    public void UpdateHungerBar(float value, float maxValue)
    {
        hungerSlider.maxValue = maxValue;
        hungerSlider.value = value;

        float percent = value / maxValue;
        hungerBarFill.color = Color.Lerp(Color.red, Color.green, percent);
    }

public void UpdateAstronautHealthBar(float value, float maxValue)
    {
    if (astronautHealthSlider != null)
    {
    astronautHealthSlider.maxValue = maxValue;
astronautHealthSlider.value = value;

float percent = value / maxValue;
if (astronautHealthBarFill != null)
{
astronautHealthBarFill.color = Color.Lerp(Color.red, Color.green, percent);
}
}
}

public void UpdateAlienHealthBar(float value, float maxValue)
{
if (alienHealthSlider != null)
{
alienHealthSlider.maxValue = maxValue;
alienHealthSlider.value = value;

float percent = value / maxValue;
if (alienHealthBarFill != null)
{
alienHealthBarFill.color = Color.Lerp(Color.red, Color.green, percent);
}
}
}

public Slider GetStressSlider() => stressSlider;
public Slider GetHungerSlider() => hungerSlider;
public Image GetStressBarFill() => stressBarFill;
public Image GetHungerBarFill() => hungerBarFill;

/// <summary>
/// Switch to chaos mode - hide hunger bar, show only HP for alien
/// </summary>
public void SetChaosMode(bool isChaos)
{
    if (currentPlayerType == PlayerType.Alien)
    {
        // In chaos mode: hide hunger, show HP
        if (hungerBarPanel != null)
            hungerBarPanel.SetActive(!isChaos);

        if (alienHealthBarPanel != null)
            alienHealthBarPanel.SetActive(true);
    }

    Debug.Log($"[GameUIManager] Chaos mode: {isChaos}");
}

// ==================== ENEMY HP BAR ====================
// Shows enemy HP when they take damage (visible to attacker)

private GameObject enemyHPBarPanel;
private Slider enemyHPSlider;
private Image enemyHPBarFill;
private Text enemyHPLabel;
private float enemyHPShowTimer = 0f;
private float enemyHPShowDuration = 3f;

void CreateEnemyHPBar()
{
    if (mainCanvas == null) return;

    // Create panel at top center
    enemyHPBarPanel = new GameObject("EnemyHPBarPanel");
    enemyHPBarPanel.transform.SetParent(mainCanvas.transform, false);

    RectTransform panelRect = enemyHPBarPanel.AddComponent<RectTransform>();
    panelRect.anchorMin = new Vector2(0.5f, 1f);
    panelRect.anchorMax = new Vector2(0.5f, 1f);
    panelRect.pivot = new Vector2(0.5f, 1f);
    panelRect.anchoredPosition = new Vector2(0, -20);
    panelRect.sizeDelta = new Vector2(300, 40);

    // Background
    Image bgImage = enemyHPBarPanel.AddComponent<Image>();
    bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

    // Label
    GameObject labelObj = new GameObject("EnemyHPLabel");
    labelObj.transform.SetParent(enemyHPBarPanel.transform, false);
    RectTransform labelRect = labelObj.AddComponent<RectTransform>();
    labelRect.anchorMin = Vector2.zero;
    labelRect.anchorMax = new Vector2(1f, 0.4f);
    labelRect.offsetMin = Vector2.zero;
    labelRect.offsetMax = Vector2.zero;

    enemyHPLabel = labelObj.AddComponent<Text>();
    enemyHPLabel.text = "ENNEMI";
    enemyHPLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    enemyHPLabel.fontSize = 14;
    enemyHPLabel.alignment = TextAnchor.MiddleCenter;
    enemyHPLabel.color = Color.white;

    // Slider background
    GameObject sliderBg = new GameObject("SliderBg");
    sliderBg.transform.SetParent(enemyHPBarPanel.transform, false);
    RectTransform sliderBgRect = sliderBg.AddComponent<RectTransform>();
    sliderBgRect.anchorMin = new Vector2(0.05f, 0.45f);
    sliderBgRect.anchorMax = new Vector2(0.95f, 0.9f);
    sliderBgRect.offsetMin = Vector2.zero;
    sliderBgRect.offsetMax = Vector2.zero;

    Image sliderBgImage = sliderBg.AddComponent<Image>();
    sliderBgImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);

    // Slider
    GameObject sliderObj = new GameObject("EnemyHPSlider");
    sliderObj.transform.SetParent(enemyHPBarPanel.transform, false);
    RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
    sliderRect.anchorMin = new Vector2(0.05f, 0.45f);
    sliderRect.anchorMax = new Vector2(0.95f, 0.9f);
    sliderRect.offsetMin = Vector2.zero;
    sliderRect.offsetMax = Vector2.zero;

    enemyHPSlider = sliderObj.AddComponent<Slider>();
    enemyHPSlider.minValue = 0;
    enemyHPSlider.maxValue = 100;
    enemyHPSlider.value = 100;
    enemyHPSlider.interactable = false;

    // Fill area
    GameObject fillArea = new GameObject("FillArea");
    fillArea.transform.SetParent(sliderObj.transform, false);
    RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
    fillAreaRect.anchorMin = Vector2.zero;
    fillAreaRect.anchorMax = Vector2.one;
    fillAreaRect.offsetMin = Vector2.zero;
    fillAreaRect.offsetMax = Vector2.zero;

    // Fill
    GameObject fill = new GameObject("Fill");
    fill.transform.SetParent(fillArea.transform, false);
    RectTransform fillRect = fill.AddComponent<RectTransform>();
    fillRect.anchorMin = Vector2.zero;
    fillRect.anchorMax = Vector2.one;
    fillRect.offsetMin = Vector2.zero;
    fillRect.offsetMax = Vector2.zero;

    enemyHPBarFill = fill.AddComponent<Image>();
    enemyHPBarFill.color = Color.red;

    enemyHPSlider.fillRect = fillRect;

    enemyHPBarPanel.SetActive(false);
}

/// <summary>
/// Show enemy HP bar with current values (called when enemy takes damage)
/// </summary>
public void ShowEnemyHP(float currentHP, float maxHP, string enemyName = "ENNEMI")
{
    if (enemyHPBarPanel == null)
    {
        CreateEnemyHPBar();
    }

    if (enemyHPBarPanel != null)
    {
        enemyHPBarPanel.SetActive(true);
        enemyHPSlider.maxValue = maxHP;
        enemyHPSlider.value = currentHP;

        float percent = currentHP / maxHP;
        enemyHPBarFill.color = Color.Lerp(Color.red, Color.green, percent);

        if (enemyHPLabel != null)
        {
            enemyHPLabel.text = $"{enemyName} - {currentHP:F0}/{maxHP:F0}";
        }

        enemyHPShowTimer = enemyHPShowDuration;
    }
}

void Update()
{
    // Auto-hide enemy HP bar after duration
    if (enemyHPShowTimer > 0)
    {
        enemyHPShowTimer -= Time.deltaTime;
        if (enemyHPShowTimer <= 0 && enemyHPBarPanel != null)
        {
            enemyHPBarPanel.SetActive(false);
        }
    }

    // Auto-hide notification after duration
    if (notificationShowTimer > 0)
    {
        notificationShowTimer -= Time.deltaTime;
        if (notificationShowTimer <= 0 && notificationPanel != null)
        {
            notificationPanel.SetActive(false);
        }
    }
}

// ==================== NOTIFICATION SYSTEM ====================
// Shows temporary notifications to the player

private GameObject notificationPanel;
private Text notificationText;
private float notificationShowTimer = 0f;

void CreateNotificationPanel()
{
    if (mainCanvas == null) return;

    // Create panel at top center (below enemy HP bar)
    notificationPanel = new GameObject("NotificationPanel");
    notificationPanel.transform.SetParent(mainCanvas.transform, false);

    RectTransform panelRect = notificationPanel.AddComponent<RectTransform>();
    panelRect.anchorMin = new Vector2(0.5f, 1f);
    panelRect.anchorMax = new Vector2(0.5f, 1f);
    panelRect.pivot = new Vector2(0.5f, 1f);
    panelRect.anchoredPosition = new Vector2(0, -80);
    panelRect.sizeDelta = new Vector2(500, 50);

    // Background
    Image bgImage = notificationPanel.AddComponent<Image>();
    bgImage.color = new Color(0.8f, 0.2f, 0.2f, 0.9f);

    // Text
    GameObject textObj = new GameObject("NotificationText");
    textObj.transform.SetParent(notificationPanel.transform, false);
    RectTransform textRect = textObj.AddComponent<RectTransform>();
    textRect.anchorMin = Vector2.zero;
    textRect.anchorMax = Vector2.one;
    textRect.offsetMin = new Vector2(10, 5);
    textRect.offsetMax = new Vector2(-10, -5);

    notificationText = textObj.AddComponent<Text>();
    notificationText.text = "";
    notificationText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    notificationText.fontSize = 24;
    notificationText.fontStyle = FontStyle.Bold;
    notificationText.alignment = TextAnchor.MiddleCenter;
    notificationText.color = Color.white;

    Outline textOutline = textObj.AddComponent<Outline>();
    textOutline.effectColor = Color.black;
    textOutline.effectDistance = new Vector2(2, -2);

    notificationPanel.SetActive(false);
}

/// <summary>
/// Show a temporary notification message to the player
/// </summary>
public void ShowNotification(string message, float duration = 3f)
{
    if (notificationPanel == null)
    {
        CreateNotificationPanel();
    }

    if (notificationPanel != null)
    {
        notificationText.text = message;
        notificationPanel.SetActive(true);
        notificationShowTimer = duration;
        Debug.Log($"[GameUIManager] Notification: {message}");
    }
}

void OnDestroy()
{
    // CRITICAL: Clear static instance to prevent stale references after scene reload
    if (Instance == this)
    {
        Instance = null;
    }
}
}
