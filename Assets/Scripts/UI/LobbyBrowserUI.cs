using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Steamworks;

/// <summary>
/// Lobby Browser UI - Shows available lobbies and create option.
/// </summary>
public class LobbyBrowserUI : MonoBehaviour
{
    [Header("References")]
    public MainMenuUI mainMenuUI;

    [Header("UI Elements")]
    public Transform lobbyListContent;
    public GameObject lobbyItemPrefab;
    public TextMeshProUGUI statusText;

    [Header("Buttons")]
    public Button createLobbyButton;
    public Button refreshButton;
    public Button backButton;

    private List<GameObject> lobbyItems = new List<GameObject>();

    void OnEnable()
    {
        // Subscribe to events
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.OnLobbyListReceived += OnLobbyListReceived;
            SteamLobbyManager.Instance.OnLobbyCreated += OnLobbyCreated;
            SteamLobbyManager.Instance.OnLobbyJoined += OnLobbyJoined;
            SteamLobbyManager.Instance.OnError += OnError;

            // Request lobby list
            SteamLobbyManager.Instance.RequestLobbyList();
            SetStatus("Searching for lobbies...");
        }
    }

    void OnDisable()
    {
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.OnLobbyListReceived -= OnLobbyListReceived;
            SteamLobbyManager.Instance.OnLobbyCreated -= OnLobbyCreated;
            SteamLobbyManager.Instance.OnLobbyJoined -= OnLobbyJoined;
            SteamLobbyManager.Instance.OnError -= OnError;
        }
    }

    void Start()
    {
        // Setup buttons
        if (createLobbyButton != null)
            createLobbyButton.onClick.AddListener(OnCreateLobbyClicked);

        if (refreshButton != null)
            refreshButton.onClick.AddListener(OnRefreshClicked);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);

        // Setup lobby list content layout
        SetupLobbyListLayout();
    }

    void SetupLobbyListLayout()
    {
        if (lobbyListContent == null) return;

        // Ensure content has proper width
        RectTransform contentRect = lobbyListContent.GetComponent<RectTransform>();
        if (contentRect != null)
        {
            // Set minimum width
            if (contentRect.sizeDelta.x < 400)
            {
                contentRect.sizeDelta = new Vector2(420, contentRect.sizeDelta.y);
            }
        }

        // Add VerticalLayoutGroup if missing
        VerticalLayoutGroup layout = lobbyListContent.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = lobbyListContent.gameObject.AddComponent<VerticalLayoutGroup>();
        }
        layout.spacing = 8;
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;  // Don't control width - use fixed size
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        Debug.Log("[LobbyBrowser] Setup VerticalLayoutGroup");

        // Add ContentSizeFitter if missing
        ContentSizeFitter fitter = lobbyListContent.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = lobbyListContent.gameObject.AddComponent<ContentSizeFitter>();
        }
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Debug.Log("[LobbyBrowser] Setup ContentSizeFitter");
    }

    // ==================== BUTTON HANDLERS ====================

    void OnCreateLobbyClicked()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUIClick();

        SetStatus("Creating lobby...");

        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.CreateLobby();
        }
    }

    void OnRefreshClicked()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUIClick();

        SetStatus("Refreshing...");
        ClearLobbyList();

        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.RequestLobbyList();
        }
    }

    void OnBackClicked()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUIClick();

        if (mainMenuUI != null)
        {
            mainMenuUI.ShowMainMenu();
        }
    }

    // ==================== EVENT HANDLERS ====================

    void OnLobbyListReceived(List<SteamLobbyManager.LobbyInfo> lobbies)
    {
        ClearLobbyList();

        if (lobbies.Count == 0)
        {
            SetStatus("No lobbies found. Create one!");
            return;
        }

        SetStatus($"Found {lobbies.Count} lobby(s)");

        foreach (var lobby in lobbies)
        {
            CreateLobbyItem(lobby);
        }
    }

    void OnLobbyCreated()
    {
        // Go to lobby room
        if (mainMenuUI != null)
        {
            mainMenuUI.ShowLobbyRoom();
        }
    }

    void OnLobbyJoined()
    {
        // Go to lobby room
        if (mainMenuUI != null)
        {
            mainMenuUI.ShowLobbyRoom();
        }
    }

    void OnError(string error)
    {
        SetStatus($"Error: {error}");
    }

    // ==================== LOBBY LIST ====================

    void CreateLobbyItem(SteamLobbyManager.LobbyInfo lobby)
    {
        if (lobbyListContent == null) return;

        GameObject item;

        if (lobbyItemPrefab != null)
        {
            item = Instantiate(lobbyItemPrefab, lobbyListContent);
        }
        else
        {
            // Create simple item if no prefab
            item = new GameObject("LobbyItem");
            item.transform.SetParent(lobbyListContent, false);

            // Setup RectTransform with FIXED width
            RectTransform rect = item.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400, 60); // Fixed 400px width

            // Add LayoutElement to enforce size
            LayoutElement layoutElement = item.AddComponent<LayoutElement>();
            layoutElement.minWidth = 400;
            layoutElement.preferredWidth = 400;
            layoutElement.minHeight = 60;
            layoutElement.preferredHeight = 60;

            // Add background image (needed for button raycast)
            Image bg = item.AddComponent<Image>();
            bg.color = new Color(0.25f, 0.25f, 0.3f, 1f);
            bg.raycastTarget = true;

            // Create lobby name text (left side, fixed position)
            GameObject nameObj = new GameObject("LobbyName");
            nameObj.transform.SetParent(item.transform, false);
            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0);
            nameRect.anchorMax = new Vector2(0, 1);
            nameRect.pivot = new Vector2(0, 0.5f);
            nameRect.anchoredPosition = new Vector2(15, 0);
            nameRect.sizeDelta = new Vector2(280, 50);

            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = lobby.lobbyName;
            nameText.fontSize = 20;
            nameText.color = Color.white;
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.verticalAlignment = VerticalAlignmentOptions.Middle;
            nameText.overflowMode = TextOverflowModes.Ellipsis;
            nameText.raycastTarget = false;

            // Create player count text (right side, fixed position)
            GameObject playersObj = new GameObject("PlayerCount");
            playersObj.transform.SetParent(item.transform, false);
            RectTransform playersRect = playersObj.AddComponent<RectTransform>();
            playersRect.anchorMin = new Vector2(1, 0);
            playersRect.anchorMax = new Vector2(1, 1);
            playersRect.pivot = new Vector2(1, 0.5f);
            playersRect.anchoredPosition = new Vector2(-15, 0);
            playersRect.sizeDelta = new Vector2(60, 50);

            TextMeshProUGUI playersText = playersObj.AddComponent<TextMeshProUGUI>();
            playersText.text = $"{lobby.playerCount}/{lobby.maxPlayers}";
            playersText.fontSize = 20;
            playersText.color = new Color(0.7f, 0.9f, 0.7f);
            playersText.alignment = TextAlignmentOptions.Right;
            playersText.verticalAlignment = VerticalAlignmentOptions.Middle;
            playersText.raycastTarget = false;

            // Add button component
            Button button = item.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.25f, 0.25f, 0.3f, 1f);
            colors.highlightedColor = new Color(0.35f, 0.35f, 0.5f, 1f);
            colors.pressedColor = new Color(0.2f, 0.4f, 0.2f, 1f);
            colors.selectedColor = new Color(0.3f, 0.3f, 0.4f, 1f);
            button.colors = colors;
            button.targetGraphic = bg;

            // Add click listener
            CSteamID lobbyID = lobby.lobbyID;
            button.onClick.AddListener(() => OnLobbyItemClicked(lobbyID));

            Debug.Log($"[LobbyBrowser] Created lobby item: {lobby.lobbyName}");
        }

        // If using prefab, set up texts and button
        if (lobbyItemPrefab != null)
        {
            var texts = item.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 1) texts[0].text = lobby.lobbyName;
            if (texts.Length >= 2) texts[1].text = $"{lobby.playerCount}/{lobby.maxPlayers}";

            Button btn = item.GetComponent<Button>();
            if (btn == null) btn = item.AddComponent<Button>();

            CSteamID lobbyID = lobby.lobbyID;
            btn.onClick.AddListener(() => OnLobbyItemClicked(lobbyID));
        }

        lobbyItems.Add(item);
    }

    void OnLobbyItemClicked(CSteamID lobbyID)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUIClick();

        SetStatus("Joining lobby...");

        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.JoinLobby(lobbyID);
        }
    }

    void ClearLobbyList()
    {
        foreach (var item in lobbyItems)
        {
            if (item != null)
                Destroy(item);
        }
        lobbyItems.Clear();
    }

    void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;

        Debug.Log($"[LobbyBrowser] {message}");
    }
}
