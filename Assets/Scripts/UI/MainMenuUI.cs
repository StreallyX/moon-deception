using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Steamworks;
using Unity.Netcode;

/// <summary>
/// Main Menu UI Controller.
/// Handles Play, Settings, Quit buttons.
/// Also supports local testing with H (Host) and J (Join) keys.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject lobbyBrowserPanel;
    public GameObject lobbyRoomPanel;
    public GameObject settingsPanel;

    [Header("Main Menu Buttons")]
    public Button playButton;
    public Button settingsButton;
    public Button quitButton;

    [Header("Steam Status")]
    public TextMeshProUGUI steamStatusText;
    public TextMeshProUGUI playerNameText;

    [Header("Settings")]
    public Button settingsBackButton;

    [Header("Local Test Settings")]
    public string gameSceneName = "SampleScene";

    void Awake()
    {
        // Ensure there's an AudioListener ASAP
        EnsureAudioListener();
    }

    void Start()
    {
        // Setup button listeners
        if (playButton != null)
            playButton.onClick.AddListener(OnPlayClicked);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettingsClicked);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);

        if (settingsBackButton != null)
            settingsBackButton.onClick.AddListener(OnSettingsBackClicked);

        // Create status texts if not assigned
        EnsureStatusTexts();

        // Show main menu
        ShowMainMenu();

        // Update Steam status
        UpdateSteamStatus();
    }

    void EnsureStatusTexts()
    {
        if (mainMenuPanel == null) return;

        // Create steam status text if missing
        if (steamStatusText == null)
        {
            GameObject steamObj = new GameObject("SteamStatusText");
            steamObj.transform.SetParent(mainMenuPanel.transform, false);

            RectTransform rect = steamObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(20, -20);
            rect.sizeDelta = new Vector2(300, 30);

            steamStatusText = steamObj.AddComponent<TextMeshProUGUI>();
            steamStatusText.fontSize = 18;
            steamStatusText.color = new Color(0.7f, 0.7f, 0.7f);
            steamStatusText.alignment = TextAlignmentOptions.Left;
        }

        // Create player name text if missing
        if (playerNameText == null)
        {
            GameObject nameObj = new GameObject("PlayerNameText");
            nameObj.transform.SetParent(mainMenuPanel.transform, false);

            RectTransform rect = nameObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(20, -50);
            rect.sizeDelta = new Vector2(400, 30);

            playerNameText = nameObj.AddComponent<TextMeshProUGUI>();
            playerNameText.fontSize = 20;
            playerNameText.color = Color.white;
            playerNameText.alignment = TextAlignmentOptions.Left;
        }

        // Create settings back button if missing
        EnsureSettingsBackButton();
    }

    void EnsureSettingsBackButton()
    {
        if (settingsPanel == null) return;
        if (settingsBackButton != null) return;

        // Look for existing back button in settings panel
        Button[] buttons = settingsPanel.GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            TextMeshProUGUI btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null && (btnText.text.ToLower().Contains("back") || btnText.text.ToLower().Contains("retour")))
            {
                settingsBackButton = btn;
                settingsBackButton.onClick.AddListener(OnSettingsBackClicked);
                return;
            }
        }

        // Create back button if not found
        GameObject btnObj = new GameObject("BackButton");
        btnObj.transform.SetParent(settingsPanel.transform, false);

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0);
        rect.anchorMax = new Vector2(0.5f, 0);
        rect.pivot = new Vector2(0.5f, 0);
        rect.anchoredPosition = new Vector2(0, 50);
        rect.sizeDelta = new Vector2(200, 50);

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.3f, 0.3f, 0.35f);

        settingsBackButton = btnObj.AddComponent<Button>();
        ColorBlock colors = settingsBackButton.colors;
        colors.normalColor = new Color(0.3f, 0.3f, 0.35f);
        colors.highlightedColor = new Color(0.4f, 0.4f, 0.5f);
        colors.pressedColor = new Color(0.2f, 0.2f, 0.25f);
        settingsBackButton.colors = colors;

        // Button text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "RETOUR";
        text.fontSize = 24;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;

        settingsBackButton.onClick.AddListener(OnSettingsBackClicked);
    }

    void EnsureAudioListener()
    {
        if (FindObjectOfType<AudioListener>() == null)
        {
            // Try to add to main camera first
            Camera mainCam = Camera.main;
            if (mainCam != null && mainCam.GetComponent<AudioListener>() == null)
            {
                mainCam.gameObject.AddComponent<AudioListener>();
            }
            else
            {
                // Create standalone AudioListener
                GameObject audioObj = new GameObject("AudioListener");
                audioObj.AddComponent<AudioListener>();
                DontDestroyOnLoad(audioObj);
            }
        }
    }

    void Update()
    {
        // Local test mode - H to Host, J to Join (localhost)
        if (mainMenuPanel != null && mainMenuPanel.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.H))
            {
                StartLocalHost();
            }
            else if (Input.GetKeyDown(KeyCode.J))
            {
                StartLocalClient();
            }
        }
    }

    // ==================== LOCAL TEST MODE ====================

    void StartLocalHost()
    {
        Debug.Log("[MainMenu] Starting LOCAL HOST (test mode)...");
        StartCoroutine(LoadSceneAndStartNetwork(true));
    }

    void StartLocalClient()
    {
        Debug.Log("[MainMenu] Starting LOCAL CLIENT (test mode)...");
        StartCoroutine(LoadSceneAndStartNetwork(false));
    }

    System.Collections.IEnumerator LoadSceneAndStartNetwork(bool asHost)
    {
        // Hide all menu panels
        HideAllPanels();

        // Also hide the canvas itself
        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas != null) canvas.gameObject.SetActive(false);

        // Find and hide any other menu canvases
        Canvas[] allCanvases = FindObjectsOfType<Canvas>();
        foreach (var c in allCanvases)
        {
            if (c.gameObject.name.Contains("Menu") || c.gameObject.name.Contains("Canvas"))
            {
                c.gameObject.SetActive(false);
            }
        }

        // Load game scene
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(gameSceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        yield return null; // Wait one frame

        // Create NetworkManager if it doesn't exist
        if (NetworkManager.Singleton == null)
        {
            GameObject nmObj = new GameObject("NetworkManager");
            nmObj.AddComponent<NetworkManager>();
            var transport = nmObj.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            NetworkManager.Singleton.NetworkConfig.NetworkTransport = transport;
            DontDestroyOnLoad(nmObj);
        }

        // Ensure there's an AudioListener in the scene
        EnsureAudioListener();

        // Start network
        if (NetworkManager.Singleton != null)
        {
            if (asHost)
            {
                Debug.Log("[MainMenu] Starting as HOST...");
                NetworkManager.Singleton.StartHost();
            }
            else
            {
                Debug.Log("[MainMenu] Starting as CLIENT...");
                NetworkManager.Singleton.StartClient();
            }
        }
        else
        {
            Debug.LogError("[MainMenu] Failed to create NetworkManager!");
        }
    }

    void HideAllPanels()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (lobbyBrowserPanel != null) lobbyBrowserPanel.SetActive(false);
        if (lobbyRoomPanel != null) lobbyRoomPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    void UpdateSteamStatus()
    {
        bool steamAvailable = false;
        string playerName = "";

        try
        {
            steamAvailable = SteamManager.Initialized;
            if (steamAvailable)
            {
                playerName = SteamFriends.GetPersonaName();
            }
        }
        catch (System.Exception)
        {
            steamAvailable = false;
        }

        if (steamAvailable)
        {
            if (steamStatusText != null)
            {
                steamStatusText.text = "Steam: Connecté";
                steamStatusText.color = new Color(0.5f, 0.9f, 0.5f); // Green
            }

            if (playerNameText != null)
            {
                playerNameText.text = $"Bienvenue, {playerName}";
                playerNameText.color = Color.white;
            }
        }
        else
        {
            if (steamStatusText != null)
            {
                steamStatusText.text = "Steam: Non connecté";
                steamStatusText.color = new Color(0.9f, 0.5f, 0.5f); // Red
            }

            if (playerNameText != null)
            {
                playerNameText.text = "Joueur local";
                playerNameText.color = new Color(0.7f, 0.7f, 0.7f);
            }
        }
    }

    void OnSettingsBackClicked()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUIClick();

        ShowMainMenu();
    }

    // ==================== BUTTON HANDLERS ====================

    void OnPlayClicked()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogWarning("[MainMenu] Steam not initialized!");
            return;
        }

        // Play click sound
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUIClick();

        // Show lobby browser
        ShowLobbyBrowser();
    }

    void OnSettingsClicked()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUIClick();

        ShowSettings();
    }

    void OnQuitClicked()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUIClick();

        Debug.Log("[MainMenu] Quitting game...");

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    // ==================== PANEL MANAGEMENT ====================

    public void ShowMainMenu()
    {
        SetActivePanel(mainMenuPanel);
    }

    public void ShowLobbyBrowser()
    {
        SetActivePanel(lobbyBrowserPanel);

        // Request lobby list
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.RequestLobbyList();
        }
    }

    public void ShowLobbyRoom()
    {
        SetActivePanel(lobbyRoomPanel);
    }

    public void ShowSettings()
    {
        SetActivePanel(settingsPanel);
    }

    void SetActivePanel(GameObject activePanel)
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(mainMenuPanel == activePanel);

        if (lobbyBrowserPanel != null)
            lobbyBrowserPanel.SetActive(lobbyBrowserPanel == activePanel);

        if (lobbyRoomPanel != null)
            lobbyRoomPanel.SetActive(lobbyRoomPanel == activePanel);

        if (settingsPanel != null)
            settingsPanel.SetActive(settingsPanel == activePanel);
    }
}
