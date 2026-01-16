using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Lobby Room UI - Shows players in lobby, ready status, start game.
/// </summary>
public class LobbyRoomUI : MonoBehaviour
{
    [Header("References")]
    public MainMenuUI mainMenuUI;

    [Header("Lobby Info")]
    public TextMeshProUGUI lobbyNameText;
    public TextMeshProUGUI playerCountText;

    [Header("Player List")]
    public Transform playerListContent;
    public GameObject playerItemPrefab;

    [Header("Buttons")]
    public Button readyButton;
    public Button startGameButton;
    public Button leaveButton;

    [Header("Ready Button Colors")]
    public Color notReadyColor = new Color(0.8f, 0.3f, 0.3f);
    public Color readyColor = new Color(0.3f, 0.8f, 0.3f);

    private List<GameObject> playerItems = new List<GameObject>();
    private bool isReady = false;
    private TextMeshProUGUI readyButtonText;

    void OnEnable()
    {
        // Subscribe to events
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.OnPlayerListUpdated += UpdatePlayerList;
            SteamLobbyManager.Instance.OnLobbyLeft += OnLobbyLeft;
            SteamLobbyManager.Instance.OnGameStarting += OnGameStarting;
            SteamLobbyManager.Instance.OnError += OnError;
        }

        // Initial update
        UpdateLobbyInfo();
        UpdatePlayerList();
        UpdateButtons();

        // Host is always ready by default
        if (SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.IsHost)
        {
            isReady = true;
        }
    }

    void OnDisable()
    {
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.OnPlayerListUpdated -= UpdatePlayerList;
            SteamLobbyManager.Instance.OnLobbyLeft -= OnLobbyLeft;
            SteamLobbyManager.Instance.OnGameStarting -= OnGameStarting;
            SteamLobbyManager.Instance.OnError -= OnError;
        }
    }

    void Start()
    {
        // Setup buttons
        if (readyButton != null)
        {
            readyButton.onClick.AddListener(OnReadyClicked);
            readyButtonText = readyButton.GetComponentInChildren<TextMeshProUGUI>();
        }

        if (startGameButton != null)
            startGameButton.onClick.AddListener(OnStartGameClicked);

        if (leaveButton != null)
            leaveButton.onClick.AddListener(OnLeaveClicked);

        // Setup player list layout
        SetupPlayerListLayout();
    }

    void SetupPlayerListLayout()
    {
        if (playerListContent == null) return;

        // Ensure content has proper width
        RectTransform contentRect = playerListContent.GetComponent<RectTransform>();
        if (contentRect != null && contentRect.sizeDelta.x < 400)
        {
            contentRect.sizeDelta = new Vector2(420, contentRect.sizeDelta.y);
        }

        // Add VerticalLayoutGroup if missing
        VerticalLayoutGroup layout = playerListContent.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = playerListContent.gameObject.AddComponent<VerticalLayoutGroup>();
        }
        layout.spacing = 8;
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        // Add ContentSizeFitter if missing
        ContentSizeFitter fitter = playerListContent.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = playerListContent.gameObject.AddComponent<ContentSizeFitter>();
        }
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    void Update()
    {
        // Update buttons state
        UpdateButtons();
    }

    // ==================== BUTTON HANDLERS ====================

    void OnReadyClicked()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUIClick();

        isReady = !isReady;

        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.SetReady(isReady);
        }

        UpdateReadyButton();
    }

    void OnStartGameClicked()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUIClick();

        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.StartGame();
        }
    }

    void OnLeaveClicked()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUIClick();

        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.LeaveLobby();
        }
    }

    // ==================== EVENT HANDLERS ====================

    void OnLobbyLeft()
    {
        isReady = false;

        if (mainMenuUI != null)
        {
            mainMenuUI.ShowLobbyBrowser();
        }
    }

    void OnGameStarting()
    {
        Debug.Log("[LobbyRoom] Game is starting!");
        // Scene will be loaded by SteamLobbyManager
    }

    void OnError(string error)
    {
        Debug.LogError($"[LobbyRoom] Error: {error}");
        // Could show error popup here
    }

    // ==================== UI UPDATES ====================

    void UpdateLobbyInfo()
    {
        if (SteamLobbyManager.Instance == null || !SteamLobbyManager.Instance.InLobby)
            return;

        if (lobbyNameText != null)
        {
            lobbyNameText.text = SteamLobbyManager.Instance.GetLobbyName();
        }
    }

    void UpdatePlayerList()
    {
        ClearPlayerList();

        if (SteamLobbyManager.Instance == null)
            return;

        var players = SteamLobbyManager.Instance.Players;

        if (playerCountText != null)
        {
            playerCountText.text = $"Players: {players.Count}/6";
        }

        foreach (var player in players)
        {
            CreatePlayerItem(player);
        }
    }

    void CreatePlayerItem(SteamLobbyManager.LobbyPlayer player)
    {
        if (playerListContent == null) return;

        GameObject item;

        if (playerItemPrefab != null)
        {
            item = Instantiate(playerItemPrefab, playerListContent);

            // Set up prefab texts
            var texts = item.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 1)
            {
                string hostTag = player.isHost ? " [HOST]" : "";
                texts[0].text = player.name + hostTag;
            }
            if (texts.Length >= 2)
            {
                texts[1].text = player.isReady ? "READY" : "Not Ready";
                texts[1].color = player.isReady ? Color.green : Color.gray;
            }
        }
        else
        {
            // Create simple item if no prefab
            item = new GameObject("PlayerItem");
            item.transform.SetParent(playerListContent, false);

            // Setup RectTransform with FIXED size
            RectTransform rect = item.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400, 50);

            // Add LayoutElement to enforce size
            LayoutElement layoutElement = item.AddComponent<LayoutElement>();
            layoutElement.minWidth = 400;
            layoutElement.preferredWidth = 400;
            layoutElement.minHeight = 50;
            layoutElement.preferredHeight = 50;

            // Add background
            Image bg = item.AddComponent<Image>();
            bg.color = player.isReady ? new Color(0.2f, 0.5f, 0.2f, 1f) : new Color(0.3f, 0.3f, 0.35f, 1f);

            // Create player name text (left side, fixed position)
            GameObject nameObj = new GameObject("PlayerName");
            nameObj.transform.SetParent(item.transform, false);
            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0);
            nameRect.anchorMax = new Vector2(0, 1);
            nameRect.pivot = new Vector2(0, 0.5f);
            nameRect.anchoredPosition = new Vector2(15, 0);
            nameRect.sizeDelta = new Vector2(250, 40);

            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            string hostTag = player.isHost ? " <color=#FFD700>[HOST]</color>" : "";
            nameText.text = player.name + hostTag;
            nameText.fontSize = 18;
            nameText.color = Color.white;
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.verticalAlignment = VerticalAlignmentOptions.Middle;
            nameText.overflowMode = TextOverflowModes.Ellipsis;
            nameText.richText = true;

            // Create status text (right side, fixed position)
            GameObject statusObj = new GameObject("Status");
            statusObj.transform.SetParent(item.transform, false);
            RectTransform statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(1, 0);
            statusRect.anchorMax = new Vector2(1, 1);
            statusRect.pivot = new Vector2(1, 0.5f);
            statusRect.anchoredPosition = new Vector2(-15, 0);
            statusRect.sizeDelta = new Vector2(100, 40);

            TextMeshProUGUI statusText = statusObj.AddComponent<TextMeshProUGUI>();
            statusText.text = player.isReady ? "READY" : "Not Ready";
            statusText.fontSize = 16;
            statusText.color = player.isReady ? new Color(0.4f, 1f, 0.4f) : new Color(0.6f, 0.6f, 0.6f);
            statusText.alignment = TextAlignmentOptions.Right;
            statusText.verticalAlignment = VerticalAlignmentOptions.Middle;
            statusText.fontStyle = player.isReady ? FontStyles.Bold : FontStyles.Normal;
        }

        playerItems.Add(item);
    }

    void ClearPlayerList()
    {
        foreach (var item in playerItems)
        {
            if (item != null)
                Destroy(item);
        }
        playerItems.Clear();
    }

    void UpdateButtons()
    {
        if (SteamLobbyManager.Instance == null) return;

        bool isHost = SteamLobbyManager.Instance.IsHost;
        bool allReady = SteamLobbyManager.Instance.AllPlayersReady();

        // Start button - only visible for host
        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(isHost);
            startGameButton.interactable = allReady;
        }

        UpdateReadyButton();
    }

    void UpdateReadyButton()
    {
        if (readyButton == null) return;

        var image = readyButton.GetComponent<Image>();
        if (image != null)
        {
            image.color = isReady ? readyColor : notReadyColor;
        }

        if (readyButtonText != null)
        {
            readyButtonText.text = isReady ? "READY!" : "Click to Ready";
        }
    }
}
