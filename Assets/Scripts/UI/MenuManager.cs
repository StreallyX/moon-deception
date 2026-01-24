using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Central menu management system.
/// Handles Main Menu, Pause Menu, Settings, and Game Over screens.
/// Creates all UI programmatically for a consistent Steam-quality look.
/// </summary>
public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance { get; private set; }

    [Header("Menu State")]
    public bool isMainMenu = false;
    public bool isPaused = false;

    [Header("Style")]
    public Color backgroundColor = new Color(0.05f, 0.05f, 0.1f, 0.95f);
    public Color primaryColor = new Color(0.2f, 0.6f, 0.9f);
    public Color accentColor = new Color(0.9f, 0.3f, 0.3f);
    public Color textColor = Color.white;
    public Font menuFont;

    // UI References
    private Canvas menuCanvas;
    private GameObject mainMenuPanel;
    private GameObject pauseMenuPanel;
    private GameObject settingsPanel;
    private GameObject gameOverPanel;

    // Settings sliders
    private Slider masterVolumeSlider;
    private Slider sfxVolumeSlider;
    private Slider musicVolumeSlider;
    private Slider sensitivitySlider;

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
        CreateMenuCanvas();
        CreateMainMenu();
        CreatePauseMenu();
        CreateSettingsPanel();
        CreateGameOverPanel();

        // Hide all menus initially
        HideAllMenus();

        // Show main menu if we're in the main menu scene
        if (isMainMenu)
        {
            ShowMainMenu();
        }

        // Subscribe to scene changes to reset menu state
        SceneManager.sceneLoaded += OnSceneLoaded;

        Debug.Log("[MenuManager] Initialized");
    }

    void OnDestroy()
    {
        // CRITICAL: Clear static instance to prevent stale references after scene reload
        if (Instance == this)
        {
            Instance = null;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[MenuManager] Scene loaded: {scene.name}");

        // Always hide game over panel when changing scenes
        HideAllMenus();

        // Check if we're in the main menu scene
        if (scene.name == "MainMenu")
        {
            isMainMenu = true;
            isPaused = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;
            // Don't show main menu here - let MainMenuUI handle it
        }
        else
        {
            isMainMenu = false;
        }
    }

    void Update()
    {
        // Escape key handling
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingsPanel.activeSelf)
            {
                CloseSettings();
            }
            else if (!isMainMenu && !gameOverPanel.activeSelf)
            {
                // Don't pause if in spectator mode - spectator handles Escape differently
                if (SpectatorController.Instance != null && SpectatorController.Instance.IsActive)
                {
                    return; // Let spectator controller handle it
                }
                TogglePause();
            }
        }
    }

    void CreateMenuCanvas()
    {
        GameObject canvasObj = new GameObject("MenuCanvas");
        canvasObj.transform.SetParent(transform);

        menuCanvas = canvasObj.AddComponent<Canvas>();
        menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        menuCanvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();
    }

    // ==================== MAIN MENU ====================

    void CreateMainMenu()
    {
        mainMenuPanel = CreatePanel("MainMenuPanel");

        // Title
        CreateText(mainMenuPanel.transform, "MOON DECEPTION", 72, new Vector2(0, 200), FontStyle.Bold);
        CreateText(mainMenuPanel.transform, "Trust No One", 24, new Vector2(0, 130), FontStyle.Italic);

        // Buttons
        CreateButton(mainMenuPanel.transform, "PLAY", new Vector2(0, 0), () => StartGame());
        CreateButton(mainMenuPanel.transform, "SETTINGS", new Vector2(0, -80), () => ShowSettings());
        CreateButton(mainMenuPanel.transform, "QUIT", new Vector2(0, -160), () => QuitGame(), true);

        // Version
        CreateText(mainMenuPanel.transform, "v0.1 - Phase 1", 16, new Vector2(0, -350), FontStyle.Normal);
    }

    // ==================== PAUSE MENU ====================

    void CreatePauseMenu()
    {
        pauseMenuPanel = CreatePanel("PauseMenuPanel");

        // Title
        CreateText(pauseMenuPanel.transform, "PAUSED", 56, new Vector2(0, 150), FontStyle.Bold);

        // Buttons
        CreateButton(pauseMenuPanel.transform, "RESUME", new Vector2(0, 30), () => Resume());
        CreateButton(pauseMenuPanel.transform, "SETTINGS", new Vector2(0, -50), () => ShowSettings());
        CreateButton(pauseMenuPanel.transform, "MAIN MENU", new Vector2(0, -130), () => ReturnToMainMenu(), true);
    }

    // ==================== SETTINGS ====================

    void CreateSettingsPanel()
    {
        settingsPanel = CreatePanel("SettingsPanel");

        // Title
        CreateText(settingsPanel.transform, "SETTINGS", 56, new Vector2(0, 250), FontStyle.Bold);

        // Audio section
        CreateText(settingsPanel.transform, "AUDIO", 24, new Vector2(-200, 150), FontStyle.Bold, TextAnchor.MiddleLeft);

        masterVolumeSlider = CreateSlider(settingsPanel.transform, "Master Volume", new Vector2(0, 100),
            PlayerPrefs.GetFloat("MasterVolume", 1f), (v) => AudioManager.Instance?.SetMasterVolume(v));

        sfxVolumeSlider = CreateSlider(settingsPanel.transform, "SFX Volume", new Vector2(0, 40),
            PlayerPrefs.GetFloat("SFXVolume", 1f), (v) => AudioManager.Instance?.SetSFXVolume(v));

        musicVolumeSlider = CreateSlider(settingsPanel.transform, "Music Volume", new Vector2(0, -20),
            PlayerPrefs.GetFloat("MusicVolume", 0.5f), (v) => AudioManager.Instance?.SetMusicVolume(v));

        // Controls section
        CreateText(settingsPanel.transform, "CONTROLS", 24, new Vector2(-200, -100), FontStyle.Bold, TextAnchor.MiddleLeft);

        sensitivitySlider = CreateSlider(settingsPanel.transform, "Mouse Sensitivity", new Vector2(0, -150),
            PlayerPrefs.GetFloat("MouseSensitivity", 2f) / 5f, (v) => SetMouseSensitivity(v * 5f));

        // Back button
        CreateButton(settingsPanel.transform, "BACK", new Vector2(0, -280), () => CloseSettings());
    }

    // ==================== GAME OVER ====================

    void CreateGameOverPanel()
    {
        gameOverPanel = CreatePanel("GameOverPanel");

        // Will be populated dynamically
    }

    public void ShowGameOver(bool victory, int aliensKilled = 0, int innocentsKilled = 0, float timePlayed = 0f)
    {
        // Clear previous content
        foreach (Transform child in gameOverPanel.transform)
        {
            if (child.name != "Background")
                Destroy(child.gameObject);
        }

        // Determine who the local player was
        bool wasAstronaut = PlayerMovement.IsPlayerControlled ||
                           (PlayerMovement.ActivePlayer != null && PlayerMovement.ActivePlayer.enabled);
        bool wasAlien = AlienController.IsAlienControlled;

        // Calculate personal victory
        bool personalVictory = false;
        string title = "";
        string subtitle = "";
        Color titleColor;

        if (victory) // Astronaut wins (aliens eliminated)
        {
            if (wasAstronaut)
            {
                personalVictory = true;
                title = "VICTOIRE!";
                subtitle = "Bravo! Tu as éliminé tous les aliens!";
            }
            else if (wasAlien)
            {
                personalVictory = false;
                title = "DÉFAITE...";
                subtitle = "L'astronaute t'a démasqué et éliminé!";
            }
        }
        else // Aliens win (astronaut killed or time up)
        {
            if (wasAlien)
            {
                personalVictory = true;
                title = "VICTOIRE!";
                subtitle = "Bravo! L'astronaute n'a pas survécu!";
            }
            else if (wasAstronaut)
            {
                personalVictory = false;
                title = "DÉFAITE...";
                subtitle = "Les aliens t'ont eu...";
            }
        }

        // Fallback if neither was controlled (spectator?)
        if (string.IsNullOrEmpty(title))
        {
            title = victory ? "ASTRONAUTE GAGNE" : "ALIENS GAGNENT";
            subtitle = victory ? "Tous les aliens éliminés!" : "L'astronaute est mort!";
            personalVictory = victory;
        }

        titleColor = personalVictory ? new Color(0.3f, 0.9f, 0.3f) : accentColor;
        var titleText = CreateText(gameOverPanel.transform, title, 72, new Vector2(0, 200), FontStyle.Bold);
        titleText.color = titleColor;

        CreateText(gameOverPanel.transform, subtitle, 28, new Vector2(0, 120), FontStyle.Italic);

        // Stats
        CreateText(gameOverPanel.transform, "STATISTIQUES", 24, new Vector2(0, 40), FontStyle.Bold);

        int minutes = Mathf.FloorToInt(timePlayed / 60f);
        int seconds = Mathf.FloorToInt(timePlayed % 60f);
        CreateText(gameOverPanel.transform, $"Temps: {minutes:00}:{seconds:00}", 20, new Vector2(0, 0), FontStyle.Normal);
        CreateText(gameOverPanel.transform, $"Aliens Éliminés: {aliensKilled}", 20, new Vector2(0, -30), FontStyle.Normal);
        CreateText(gameOverPanel.transform, $"Innocents Tués: {innocentsKilled}", 20, new Vector2(0, -60), FontStyle.Normal);

        // Buttons - different based on connection type
        bool isSteamLobby = IsSteamLobbyGame();
        bool isTestMode = IsTestModeGame();

        if (isSteamLobby)
        {
            CreateButton(gameOverPanel.transform, "RETOUR AU LOBBY", new Vector2(0, -150), () => ReturnToSteamLobby());
        }
        else
        {
            CreateButton(gameOverPanel.transform, "REJOUER", new Vector2(0, -150), () => RestartGame());
            CreateButton(gameOverPanel.transform, "MENU PRINCIPAL", new Vector2(0, -230), () => ReturnToMainMenu());
        }

        // Play sound (networked)
        if (NetworkAudioManager.Instance != null)
        {
            if (personalVictory)
                NetworkAudioManager.Instance.PlayVictory();
            else
                NetworkAudioManager.Instance.PlayDefeat();
        }
        else if (AudioManager.Instance != null)
        {
            if (personalVictory)
                AudioManager.Instance.PlayVictory();
            else
                AudioManager.Instance.PlayDefeat();
        }

        gameOverPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    /// <summary>
    /// Check if this is a Steam lobby game
    /// </summary>
    private bool IsSteamLobbyGame()
    {
        // Check if SteamLobbyManager exists and we're in a lobby
        if (SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.InLobby)
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Check if this is a test mode game (H/J keys)
    /// </summary>
    private bool IsTestModeGame()
    {
        // If we're in a Steam lobby, it's not test mode
        if (IsSteamLobbyGame())
        {
            return false;
        }

        // Check if NetworkManager exists and connected (H/J keys)
        if (Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsConnectedClient)
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Return to Steam lobby with same players (don't leave the lobby)
    /// </summary>
    public void ReturnToSteamLobby()
    {
        Debug.Log("[MenuManager] Returning to Steam lobby...");

        Time.timeScale = 1f;
        isPaused = false;
        HideAllMenus();

        // Reset NetworkGameManager state BEFORE shutting down
        if (NetworkGameManager.Instance != null)
        {
            Debug.Log("[MenuManager] Resetting NetworkGameManager state");
            NetworkGameManager.Instance.ResetState();
        }

        // Reset SpawnManager - clears spawn point references that become invalid after scene reload
        if (SpawnManager.Instance != null)
        {
            Debug.Log("[MenuManager] Resetting SpawnManager");
            SpawnManager.Instance.ResetAll();
        }

        // Reset MapManager zones - they become invalid after scene reload
        if (MapManager.Instance != null)
        {
            Debug.Log("[MenuManager] Clearing MapManager zones");
            MapManager.Instance.RefreshZones();
        }

        // Disconnect from game network but KEEP the Steam lobby
        if (Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsConnectedClient)
        {
            Debug.Log("[MenuManager] Shutting down NetworkManager (keeping Steam lobby)");
            Unity.Netcode.NetworkManager.Singleton.Shutdown();
        }

        // Reset game state
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetGame();
        }

        // Reset lobby game state so players can start a new game
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.ResetGameState();
        }

        // Load the main menu scene (which has the Steam lobby UI)
        SceneManager.LoadScene("MainMenu");
    }

    // ==================== UI CREATION HELPERS ====================

    GameObject CreatePanel(string name)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(menuCanvas.transform, false);

        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;

        // Background
        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(panel.transform, false);

        RectTransform bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        Image bgImage = bg.AddComponent<Image>();
        bgImage.color = backgroundColor;

        return panel;
    }

    Text CreateText(Transform parent, string content, int fontSize, Vector2 position, FontStyle style, TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        GameObject textObj = new GameObject("Text_" + content.Replace(" ", ""));
        textObj.transform.SetParent(parent, false);

        RectTransform rect = textObj.AddComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(800, fontSize + 20);

        Text text = textObj.AddComponent<Text>();
        text.text = content;
        text.font = menuFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = textColor;
        text.alignment = alignment;

        // Add outline for better readability
        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, -2);

        return text;
    }

    Button CreateButton(Transform parent, string label, Vector2 position, System.Action onClick, bool isDestructive = false)
    {
        GameObject buttonObj = new GameObject("Button_" + label);
        buttonObj.transform.SetParent(parent, false);

        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(300, 60);

        Image image = buttonObj.AddComponent<Image>();
        image.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);

        Button button = buttonObj.AddComponent<Button>();

        // Hover colors
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.15f, 0.15f, 0.2f, 0.9f);
        colors.highlightedColor = isDestructive ? accentColor : primaryColor;
        colors.pressedColor = isDestructive ? new Color(0.7f, 0.2f, 0.2f) : new Color(0.1f, 0.4f, 0.7f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        // Button text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        Text text = textObj.AddComponent<Text>();
        text.text = label;
        text.font = menuFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 28;
        text.fontStyle = FontStyle.Bold;
        text.color = textColor;
        text.alignment = TextAnchor.MiddleCenter;

        // Click handler with sound
        button.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlayUIClick();
            onClick?.Invoke();
        });

        return button;
    }

    Slider CreateSlider(Transform parent, string label, Vector2 position, float defaultValue, System.Action<float> onValueChanged)
    {
        GameObject container = new GameObject("Slider_" + label.Replace(" ", ""));
        container.transform.SetParent(parent, false);

        RectTransform containerRect = container.AddComponent<RectTransform>();
        containerRect.anchoredPosition = position;
        containerRect.sizeDelta = new Vector2(500, 40);

        // Label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(container.transform, false);

        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 0.5f);
        labelRect.anchorMax = new Vector2(0, 0.5f);
        labelRect.pivot = new Vector2(0, 0.5f);
        labelRect.anchoredPosition = new Vector2(-200, 0);
        labelRect.sizeDelta = new Vector2(180, 30);

        Text labelText = labelObj.AddComponent<Text>();
        labelText.text = label;
        labelText.font = menuFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = 20;
        labelText.color = textColor;
        labelText.alignment = TextAnchor.MiddleRight;

        // Slider
        GameObject sliderObj = new GameObject("Slider");
        sliderObj.transform.SetParent(container.transform, false);

        RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.anchoredPosition = new Vector2(100, 0);
        sliderRect.sizeDelta = new Vector2(300, 20);

        // Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);

        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.15f);

        // Fill
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);

        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.sizeDelta = new Vector2(-10, -10);

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillArea.transform, false);

        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0, 1);
        fillRect.sizeDelta = Vector2.zero;

        Image fillImage = fillObj.AddComponent<Image>();
        fillImage.color = primaryColor;

        // Slider component
        Slider slider = sliderObj.AddComponent<Slider>();
        slider.fillRect = fillRect;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = defaultValue;

        slider.onValueChanged.AddListener((v) => onValueChanged?.Invoke(v));

        return slider;
    }

    // ==================== MENU ACTIONS ====================

    void HideAllMenus()
    {
        mainMenuPanel?.SetActive(false);
        pauseMenuPanel?.SetActive(false);
        settingsPanel?.SetActive(false);
        gameOverPanel?.SetActive(false);
    }

    public void ShowMainMenu()
    {
        HideAllMenus();
        mainMenuPanel.SetActive(true);
        isMainMenu = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;
    }

    public void StartGame()
    {
        HideAllMenus();
        isMainMenu = false;
        isPaused = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;

        // Start game via GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartGame();
        }
    }

    public void TogglePause()
    {
        if (isPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    public void Pause()
    {
        isPaused = true;
        pauseMenuPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        isPaused = false;
        HideAllMenus();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;
    }

    public void ShowSettings()
    {
        pauseMenuPanel?.SetActive(false);
        mainMenuPanel?.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);

        if (isMainMenu)
        {
            mainMenuPanel.SetActive(true);
        }
        else if (isPaused)
        {
            pauseMenuPanel.SetActive(true);
        }
    }

    public void ReturnToMainMenu()
    {
        Debug.Log("[MenuManager] Returning to main menu...");

        Time.timeScale = 1f;
        isPaused = false;
        HideAllMenus();

        // === CLEANUP ALL GAMEPLAY UI ===
        // Clear kill feed
        if (KillFeedManager.Instance != null)
        {
            KillFeedManager.Instance.ClearAll();
            Debug.Log("[MenuManager] Cleared kill feed");
        }

        // Deactivate spectator mode
        if (SpectatorController.Instance != null)
        {
            SpectatorController.Instance.Deactivate();
            Debug.Log("[MenuManager] Deactivated spectator mode");
        }

        // Hide all gameplay UI
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.OnGameEnded();
            Debug.Log("[MenuManager] Hidden all gameplay UI");
        }

        // Reset NetworkGameManager state BEFORE shutting down
        if (NetworkGameManager.Instance != null)
        {
            Debug.Log("[MenuManager] Resetting NetworkGameManager state");
            NetworkGameManager.Instance.ResetState();
        }

        // Reset SpawnManager - clears spawn point references that become invalid after scene reload
        if (SpawnManager.Instance != null)
        {
            Debug.Log("[MenuManager] Resetting SpawnManager");
            SpawnManager.Instance.ResetAll();
        }

        // Reset MapManager zones - they become invalid after scene reload
        if (MapManager.Instance != null)
        {
            Debug.Log("[MenuManager] Clearing MapManager zones");
            MapManager.Instance.RefreshZones();
        }

        // Disconnect from network if connected
        if (Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsConnectedClient)
        {
            Debug.Log("[MenuManager] Disconnecting from network...");
            Unity.Netcode.NetworkManager.Singleton.Shutdown();
        }

        // Leave Steam lobby if we're in one (we want to go to main menu, not lobby)
        if (SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.InLobby)
        {
            Debug.Log("[MenuManager] Leaving Steam lobby...");
            SteamLobbyManager.Instance.LeaveLobby();
        }

        // Reset game
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetGame();
        }

        // Load main menu scene
        SceneManager.LoadScene("MainMenu");
    }

    public void RestartGame()
    {
        HideAllMenus();
        Time.timeScale = 1f;
        isPaused = false;
        isMainMenu = false;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetGame();
            GameManager.Instance.StartGame();
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void QuitGame()
    {
        Debug.Log("[MenuManager] Quitting game...");
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    void SetMouseSensitivity(float value)
    {
        PlayerPrefs.SetFloat("MouseSensitivity", value);
        PlayerPrefs.Save();

        // Apply to player
        if (PlayerMovement.ActivePlayer != null)
        {
            PlayerMovement.ActivePlayer.mouseSensitivity = value;
        }
    }
}
