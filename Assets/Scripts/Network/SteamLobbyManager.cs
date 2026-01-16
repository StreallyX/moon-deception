using UnityEngine;
using UnityEngine.SceneManagement;
using Steamworks;
using Unity.Netcode;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages Steam Lobbies - create, join, leave, and sync players.
/// </summary>
public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager Instance { get; private set; }

    [Header("Lobby Settings")]
    public int maxPlayers = 6; // 1 Astronaut + 5 Aliens
    public string gameSceneName = "SampleScene";

    // Current lobby info
    public CSteamID CurrentLobbyID { get; private set; } = CSteamID.Nil;
    public bool InLobby => CurrentLobbyID.IsValid();
    public bool IsHost { get; private set; } = false;

    // Player list
    public List<LobbyPlayer> Players { get; private set; } = new List<LobbyPlayer>();

    // Events for UI
    public event Action OnLobbyCreated;
    public event Action OnLobbyJoined;
    public event Action OnLobbyLeft;
    public event Action OnPlayerListUpdated;
    public event Action<List<LobbyInfo>> OnLobbyListReceived;
    public event Action OnGameStarting;
    public event Action<string> OnError;

    // Steam Callbacks
    private Callback<LobbyCreated_t> lobbyCreatedCallback;
    private Callback<LobbyEnter_t> lobbyEnteredCallback;
    private Callback<LobbyChatUpdate_t> lobbyChatUpdateCallback;
    private Callback<LobbyDataUpdate_t> lobbyDataUpdateCallback;
    private Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequestedCallback;
    private CallResult<LobbyMatchList_t> lobbyMatchListCallResult;

    [Serializable]
    public class LobbyPlayer
    {
        public CSteamID steamID;
        public string name;
        public bool isReady;
        public bool isHost;
    }

    [Serializable]
    public class LobbyInfo
    {
        public CSteamID lobbyID;
        public string lobbyName;
        public int playerCount;
        public int maxPlayers;
    }

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
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamLobby] Steam not initialized!");
            return;
        }

        // Register Steam callbacks
        lobbyCreatedCallback = Callback<LobbyCreated_t>.Create(OnLobbyCreatedCallback);
        lobbyEnteredCallback = Callback<LobbyEnter_t>.Create(OnLobbyEnteredCallback);
        lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdateCallback);
        lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdateCallback);
        gameLobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequestedCallback);
        lobbyMatchListCallResult = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchListCallback);

        Debug.Log("[SteamLobby] Initialized");
    }

    // ==================== PUBLIC METHODS ====================

    /// <summary>
    /// Create a new lobby
    /// </summary>
    public void CreateLobby(string lobbyName = "")
    {
        if (!SteamManager.Initialized)
        {
            OnError?.Invoke("Steam not initialized!");
            return;
        }

        if (InLobby)
        {
            LeaveLobby();
        }

        Debug.Log("[SteamLobby] Creating lobby...");
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, maxPlayers);
    }

    /// <summary>
    /// Join an existing lobby
    /// </summary>
    public void JoinLobby(CSteamID lobbyID)
    {
        if (!SteamManager.Initialized)
        {
            OnError?.Invoke("Steam not initialized!");
            return;
        }

        if (InLobby)
        {
            LeaveLobby();
        }

        Debug.Log($"[SteamLobby] Joining lobby {lobbyID}...");
        SteamMatchmaking.JoinLobby(lobbyID);
    }

    /// <summary>
    /// Leave current lobby
    /// </summary>
    public void LeaveLobby()
    {
        if (!InLobby) return;

        Debug.Log($"[SteamLobby] Leaving lobby {CurrentLobbyID}");
        SteamMatchmaking.LeaveLobby(CurrentLobbyID);

        CurrentLobbyID = CSteamID.Nil;
        IsHost = false;
        Players.Clear();

        OnLobbyLeft?.Invoke();
    }

    /// <summary>
    /// Request list of available lobbies
    /// </summary>
    public void RequestLobbyList()
    {
        if (!SteamManager.Initialized)
        {
            OnError?.Invoke("Steam not initialized!");
            return;
        }

        Debug.Log("[SteamLobby] Requesting lobby list...");

        // Filter for our game
        SteamMatchmaking.AddRequestLobbyListStringFilter("game", "MoonDeception", ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);

        SteamAPICall_t handle = SteamMatchmaking.RequestLobbyList();
        lobbyMatchListCallResult.Set(handle);
    }

    /// <summary>
    /// Set player ready status
    /// </summary>
    public void SetReady(bool ready)
    {
        if (!InLobby) return;

        string readyValue = ready ? "1" : "0";
        SteamMatchmaking.SetLobbyMemberData(CurrentLobbyID, "ready", readyValue);

        Debug.Log($"[SteamLobby] Set ready: {ready}");
    }

    /// <summary>
    /// Check if all players are ready (host only)
    /// </summary>
    public bool AllPlayersReady()
    {
        if (Players.Count < 2) return false; // Need at least 2 players

        foreach (var player in Players)
        {
            if (!player.isReady) return false;
        }
        return true;
    }

    /// <summary>
    /// Start the game (host only)
    /// </summary>
    public void StartGame()
    {
        if (!IsHost)
        {
            OnError?.Invoke("Only host can start the game!");
            return;
        }

        if (!AllPlayersReady())
        {
            OnError?.Invoke("Not all players are ready!");
            return;
        }

        Debug.Log("[SteamLobby] Starting game!");

        // Set lobby data to signal game start
        SteamMatchmaking.SetLobbyData(CurrentLobbyID, "gameStarted", "1");

        // Load game scene and start network
        OnGameStarting?.Invoke();
        StartCoroutine(LoadGameAndStartNetwork(true));
    }

    IEnumerator LoadGameAndStartNetwork(bool asHost)
    {
        // Load game scene
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(gameSceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // Wait a frame for scene to initialize
        yield return null;

        // Start network
        if (NetworkManager.Singleton != null)
        {
            if (asHost)
            {
                Debug.Log("[SteamLobby] Starting as HOST");
                NetworkManager.Singleton.StartHost();
            }
            else
            {
                Debug.Log("[SteamLobby] Starting as CLIENT");
                NetworkManager.Singleton.StartClient();
            }
        }
        else
        {
            Debug.LogError("[SteamLobby] NetworkManager not found in game scene!");
        }
    }

    // ==================== STEAM CALLBACKS ====================

    void OnLobbyCreatedCallback(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError($"[SteamLobby] Failed to create lobby: {callback.m_eResult}");
            OnError?.Invoke($"Failed to create lobby: {callback.m_eResult}");
            return;
        }

        CurrentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
        IsHost = true;

        // Set lobby data
        SteamMatchmaking.SetLobbyData(CurrentLobbyID, "game", "MoonDeception");
        SteamMatchmaking.SetLobbyData(CurrentLobbyID, "name", $"{SteamFriends.GetPersonaName()}'s Lobby");
        SteamMatchmaking.SetLobbyData(CurrentLobbyID, "hostName", SteamFriends.GetPersonaName());
        SteamMatchmaking.SetLobbyData(CurrentLobbyID, "gameStarted", "0");

        // Mark ourselves as ready by default (host)
        SteamMatchmaking.SetLobbyMemberData(CurrentLobbyID, "ready", "1");

        Debug.Log($"[SteamLobby] Lobby created: {CurrentLobbyID}");
        UpdatePlayerList();
        OnLobbyCreated?.Invoke();
    }

    void OnLobbyEnteredCallback(LobbyEnter_t callback)
    {
        CurrentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);

        // Check if we're the host
        CSteamID hostID = SteamMatchmaking.GetLobbyOwner(CurrentLobbyID);
        IsHost = (hostID == SteamUser.GetSteamID());

        Debug.Log($"[SteamLobby] Entered lobby: {CurrentLobbyID}, IsHost: {IsHost}");

        // Check if game already started
        string gameStarted = SteamMatchmaking.GetLobbyData(CurrentLobbyID, "gameStarted");
        if (gameStarted == "1")
        {
            Debug.Log("[SteamLobby] Game already in progress, joining...");
            StartCoroutine(LoadGameAndStartNetwork(false)); // Join as client
            return;
        }

        UpdatePlayerList();
        OnLobbyJoined?.Invoke();
    }

    void OnLobbyChatUpdateCallback(LobbyChatUpdate_t callback)
    {
        // Player joined or left
        Debug.Log($"[SteamLobby] Chat update - State: {callback.m_rgfChatMemberStateChange}");
        UpdatePlayerList();
    }

    void OnLobbyDataUpdateCallback(LobbyDataUpdate_t callback)
    {
        if (callback.m_ulSteamIDLobby != CurrentLobbyID.m_SteamID) return;

        // Check if game started
        string gameStarted = SteamMatchmaking.GetLobbyData(CurrentLobbyID, "gameStarted");
        if (gameStarted == "1" && !IsHost)
        {
            Debug.Log("[SteamLobby] Host started the game!");
            OnGameStarting?.Invoke();
            StartCoroutine(LoadGameAndStartNetwork(false)); // Start as client
            return;
        }

        UpdatePlayerList();
    }

    void OnGameLobbyJoinRequestedCallback(GameLobbyJoinRequested_t callback)
    {
        // Steam friend invited us - auto join
        Debug.Log($"[SteamLobby] Join requested via Steam overlay");
        JoinLobby(callback.m_steamIDLobby);
    }

    void OnLobbyMatchListCallback(LobbyMatchList_t callback, bool ioFailure)
    {
        if (ioFailure)
        {
            OnError?.Invoke("Failed to get lobby list");
            return;
        }

        List<LobbyInfo> lobbies = new List<LobbyInfo>();

        for (int i = 0; i < callback.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyID = SteamMatchmaking.GetLobbyByIndex(i);

            LobbyInfo info = new LobbyInfo
            {
                lobbyID = lobbyID,
                lobbyName = SteamMatchmaking.GetLobbyData(lobbyID, "name"),
                playerCount = SteamMatchmaking.GetNumLobbyMembers(lobbyID),
                maxPlayers = SteamMatchmaking.GetLobbyMemberLimit(lobbyID)
            };

            if (string.IsNullOrEmpty(info.lobbyName))
            {
                info.lobbyName = "Unknown Lobby";
            }

            lobbies.Add(info);
        }

        Debug.Log($"[SteamLobby] Found {lobbies.Count} lobbies");
        OnLobbyListReceived?.Invoke(lobbies);
    }

    // ==================== HELPERS ====================

    void UpdatePlayerList()
    {
        if (!InLobby) return;

        Players.Clear();

        int memberCount = SteamMatchmaking.GetNumLobbyMembers(CurrentLobbyID);
        CSteamID hostID = SteamMatchmaking.GetLobbyOwner(CurrentLobbyID);

        for (int i = 0; i < memberCount; i++)
        {
            CSteamID memberID = SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobbyID, i);
            string memberName = SteamFriends.GetFriendPersonaName(memberID);
            string readyStr = SteamMatchmaking.GetLobbyMemberData(CurrentLobbyID, memberID, "ready");

            LobbyPlayer player = new LobbyPlayer
            {
                steamID = memberID,
                name = memberName,
                isReady = readyStr == "1",
                isHost = memberID == hostID
            };

            Players.Add(player);
        }

        Debug.Log($"[SteamLobby] Player list updated: {Players.Count} players");
        OnPlayerListUpdated?.Invoke();
    }

    public string GetLobbyName()
    {
        if (!InLobby) return "";
        return SteamMatchmaking.GetLobbyData(CurrentLobbyID, "name");
    }

    void OnDestroy()
    {
        if (InLobby)
        {
            LeaveLobby();
        }
    }
}
