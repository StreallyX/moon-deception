using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
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

    [Header("Search")]
    public TMP_InputField searchInputField;

    [Header("Buttons")]
    public Button createLobbyButton;
    public Button refreshButton;
    public Button backButton;

    private List<GameObject> lobbyItems = new List<GameObject>();
    private List<SteamLobbyManager.LobbyInfo> allLobbies = new List<SteamLobbyManager.LobbyInfo>();

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
            SetStatus("Recherche de lobbies...");
        }

        // Clear search field
        if (searchInputField != null)
            searchInputField.text = "";

        allLobbies.Clear();
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

        // Setup search field
        EnsureSearchField();

        // Setup lobby list content layout
        SetupLobbyListLayout();
    }

    void EnsureSearchField()
    {
        if (searchInputField != null)
        {
            searchInputField.onValueChanged.AddListener(OnSearchTextChanged);
            return;
        }

        // Create search field if not assigned
        if (lobbyListContent == null) return;

        Transform parent = lobbyListContent.parent;
        if (parent == null) parent = transform;

        // Create search container
        GameObject searchContainer = new GameObject("SearchContainer");
        searchContainer.transform.SetParent(parent, false);
        searchContainer.transform.SetAsFirstSibling();

        RectTransform containerRect = searchContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 1);
        containerRect.anchorMax = new Vector2(0.5f, 1);
        containerRect.pivot = new Vector2(0.5f, 1);
        containerRect.anchoredPosition = new Vector2(0, -10);
        containerRect.sizeDelta = new Vector2(420, 50);

        // Background
        Image bgImage = searchContainer.AddComponent<Image>();
        bgImage.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);

        // Create input field
        GameObject inputObj = new GameObject("SearchInputField");
        inputObj.transform.SetParent(searchContainer.transform, false);

        RectTransform inputRect = inputObj.AddComponent<RectTransform>();
        inputRect.anchorMin = Vector2.zero;
        inputRect.anchorMax = Vector2.one;
        inputRect.sizeDelta = new Vector2(-20, -10);
        inputRect.anchoredPosition = Vector2.zero;

        // Text Area
        GameObject textArea = new GameObject("Text Area");
        textArea.transform.SetParent(inputObj.transform, false);

        RectTransform textAreaRect = textArea.AddComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.sizeDelta = new Vector2(-20, 0);
        textAreaRect.anchoredPosition = new Vector2(10, 0);

        RectMask2D mask = textArea.AddComponent<RectMask2D>();

        // Placeholder
        GameObject placeholder = new GameObject("Placeholder");
        placeholder.transform.SetParent(textArea.transform, false);

        RectTransform placeholderRect = placeholder.AddComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.sizeDelta = Vector2.zero;
        placeholderRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI placeholderText = placeholder.AddComponent<TextMeshProUGUI>();
        placeholderText.text = "Rechercher un lobby...";
        placeholderText.fontSize = 18;
        placeholderText.color = new Color(0.5f, 0.5f, 0.5f);
        placeholderText.alignment = TextAlignmentOptions.Left;
        placeholderText.verticalAlignment = VerticalAlignmentOptions.Middle;

        // Text component
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(textArea.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI inputText = textObj.AddComponent<TextMeshProUGUI>();
        inputText.fontSize = 18;
        inputText.color = Color.white;
        inputText.alignment = TextAlignmentOptions.Left;
        inputText.verticalAlignment = VerticalAlignmentOptions.Middle;

        // Input Field component
        searchInputField = inputObj.AddComponent<TMP_InputField>();
        searchInputField.textViewport = textAreaRect;
        searchInputField.textComponent = inputText;
        searchInputField.placeholder = placeholderText;
        searchInputField.fontAsset = inputText.font;
        searchInputField.pointSize = 18;
        searchInputField.caretColor = Color.white;
        searchInputField.selectionColor = new Color(0.2f, 0.6f, 0.9f, 0.5f);

        searchInputField.onValueChanged.AddListener(OnSearchTextChanged);

        // Adjust lobby list position
        if (lobbyListContent != null)
        {
            RectTransform listRect = lobbyListContent.GetComponent<RectTransform>();
            if (listRect != null)
            {
                listRect.anchoredPosition = new Vector2(listRect.anchoredPosition.x, listRect.anchoredPosition.y - 60);
            }
        }
    }

    void OnSearchTextChanged(string searchText)
    {
        FilterAndDisplayLobbies(searchText);
    }

    void FilterAndDisplayLobbies(string searchText)
    {
        ClearLobbyList();

        if (allLobbies.Count == 0)
        {
            SetStatus("Aucun lobby trouvé. Créez-en un!");
            return;
        }

        List<SteamLobbyManager.LobbyInfo> filtered;

        if (string.IsNullOrWhiteSpace(searchText))
        {
            filtered = allLobbies;
        }
        else
        {
            string search = searchText.ToLower().Trim();
            filtered = allLobbies.Where(l =>
                l.lobbyName.ToLower().Contains(search)
            ).ToList();
        }

        if (filtered.Count == 0)
        {
            SetStatus($"Aucun lobby trouvé pour \"{searchText}\"");
            return;
        }

        SetStatus($"{filtered.Count} lobby(s) trouvé(s)");

        foreach (var lobby in filtered)
        {
            CreateLobbyItem(lobby);
        }
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

        SetStatus("Création du lobby...");

        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.CreateLobby();
        }
    }

    void OnRefreshClicked()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUIClick();

        SetStatus("Actualisation...");
        ClearLobbyList();
        allLobbies.Clear();

        // Clear search field
        if (searchInputField != null)
            searchInputField.text = "";

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
        // Store all lobbies for filtering
        allLobbies = new List<SteamLobbyManager.LobbyInfo>(lobbies);

        // Apply current search filter
        string currentSearch = searchInputField != null ? searchInputField.text : "";
        FilterAndDisplayLobbies(currentSearch);
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

        SetStatus("Connexion au lobby...");

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
