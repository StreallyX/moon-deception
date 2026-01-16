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

        // Show main menu
        ShowMainMenu();

        // Update Steam status
        UpdateSteamStatus();
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
        if (SteamManager.Initialized)
        {
            if (steamStatusText != null)
                steamStatusText.text = "Steam: Connected";

            if (playerNameText != null)
                playerNameText.text = $"Welcome, {SteamFriends.GetPersonaName()}";
        }
        else
        {
            if (steamStatusText != null)
                steamStatusText.text = "Steam: Not Connected";

            if (playerNameText != null)
                playerNameText.text = "Steam required to play online";
        }
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
