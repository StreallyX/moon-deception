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
        Debug.Log($"[SteamLobby] Loading scene: {gameSceneName}...");
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(gameSceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        Debug.Log("[SteamLobby] Scene loaded, waiting for initialization...");

        // Wait for scene to fully initialize (managers, zones, etc.)
        float initDelay = asHost ? 3f : 1f; // Host needs more time to setup
        yield return new WaitForSeconds(initDelay);

        // Wait for NetworkManager
        float timeout = 5f;
        float elapsed = 0f;
        while (NetworkManager.Singleton == null && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[SteamLobby] NetworkManager not found after timeout!");
            yield break;
        }

        // Try to get Steam transport first (preferred for internet play)
        var steamTransport = NetworkManager.Singleton.GetComponent<SteamNetworkTransport>();

        // If Steam transport doesn't exist, create it!
        if (steamTransport == null && SteamManager.Initialized)
        {
            Debug.Log("[SteamLobby] Adding SteamNetworkTransport to NetworkManager...");
            steamTransport = NetworkManager.Singleton.gameObject.AddComponent<SteamNetworkTransport>();
        }

        if (steamTransport != null && SteamManager.Initialized)
        {
            // Use Steam Relay - no port forwarding needed!
            yield return StartCoroutine(StartWithSteamTransport(steamTransport, asHost));
        }
        else
        {
            // Fallback to Unity Transport (for LAN or testing)
            Debug.LogWarning("[SteamLobby] Steam not available, falling back to UnityTransport (LAN only)");
            var unityTransport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            if (unityTransport == null)
            {
                unityTransport = NetworkManager.Singleton.gameObject.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            }
            yield return StartCoroutine(StartWithUnityTransport(unityTransport, asHost));
        }
    }

    /// <summary>
    /// Start networking using Steam Relay (P2P via SteamID)
    /// </summary>
    IEnumerator StartWithSteamTransport(SteamNetworkTransport transport, bool asHost)
    {
        Debug.Log($"[SteamLobby] Using Steam Relay! SteamManager.Initialized={SteamManager.Initialized}");

        if (asHost)
        {
            // HOST: Start hosting and store our SteamID in lobby
            CSteamID mySteamID = SteamUser.GetSteamID();
            Debug.Log($"[SteamLobby] Starting as HOST with Steam Relay... My SteamID: {mySteamID}");

            // Set this transport as active
            NetworkManager.Singleton.NetworkConfig.NetworkTransport = transport;

            Debug.Log("[SteamLobby] HOST: Starting NetworkManager.StartHost()...");
            NetworkManager.Singleton.StartHost();

            // Store host SteamID in lobby data for clients
            CSteamID hostSteamID = SteamUser.GetSteamID();
            if (InLobby)
            {
                SteamMatchmaking.SetLobbyData(CurrentLobbyID, "hostSteamID", hostSteamID.m_SteamID.ToString());
                SteamMatchmaking.SetLobbyData(CurrentLobbyID, "useSteamRelay", "1");
                Debug.Log($"[SteamLobby] HOST started with Steam Relay. SteamID: {hostSteamID}");
            }

            // Wait for NetworkSpawnManager to be ready
            yield return new WaitForSeconds(2f);
            Debug.Log("[SteamLobby] HOST fully ready with Steam Relay!");
        }
        else
        {
            // CLIENT: Get host SteamID from lobby and connect via Steam
            CSteamID mySteamID = SteamUser.GetSteamID();
            Debug.Log($"[SteamLobby] Starting as CLIENT with Steam Relay... My SteamID: {mySteamID}");

            CSteamID hostSteamID = CSteamID.Nil;

            if (InLobby)
            {
                string hostSteamIDStr = SteamMatchmaking.GetLobbyData(CurrentLobbyID, "hostSteamID");
                Debug.Log($"[SteamLobby] CLIENT: Got hostSteamID from lobby data: '{hostSteamIDStr}'");

                if (!string.IsNullOrEmpty(hostSteamIDStr) && ulong.TryParse(hostSteamIDStr, out ulong steamIdValue))
                {
                    hostSteamID = new CSteamID(steamIdValue);
                }
            }
            else
            {
                Debug.LogWarning("[SteamLobby] CLIENT: Not in lobby!");
            }

            // Wait for host to save their SteamID (might not be ready immediately)
            float waitForHost = 0f;
            while (!hostSteamID.IsValid() && waitForHost < 10f)
            {
                yield return new WaitForSeconds(0.5f);
                waitForHost += 0.5f;

                if (InLobby)
                {
                    string hostSteamIDStr = SteamMatchmaking.GetLobbyData(CurrentLobbyID, "hostSteamID");
                    if (!string.IsNullOrEmpty(hostSteamIDStr) && ulong.TryParse(hostSteamIDStr, out ulong steamIdValue))
                    {
                        hostSteamID = new CSteamID(steamIdValue);
                        Debug.Log($"[SteamLobby] CLIENT: Got host SteamID after {waitForHost}s: {hostSteamID.m_SteamID}");
                    }
                }
            }

            if (!hostSteamID.IsValid())
            {
                Debug.LogError("[SteamLobby] No valid host SteamID found in lobby data after 10s! Cannot connect via Steam Relay.");
                yield break;
            }

            Debug.Log($"[SteamLobby] CLIENT connecting via Steam Relay to host SteamID: {hostSteamID.m_SteamID}");

            // Set host SteamID on transport and connect
            transport.HostSteamID = hostSteamID;
            NetworkManager.Singleton.NetworkConfig.NetworkTransport = transport;

            Debug.Log("[SteamLobby] CLIENT: Starting NetworkManager.StartClient()...");
            NetworkManager.Singleton.StartClient();

            // Wait for connection
            float connectTimeout = 30f; // Steam P2P might take longer
            float connectElapsed = 0f;
            while (!NetworkManager.Singleton.IsConnectedClient && connectElapsed < connectTimeout)
            {
                yield return new WaitForSeconds(0.5f);
                connectElapsed += 0.5f;
                if ((int)connectElapsed % 5 == 0)
                {
                    Debug.Log($"[SteamLobby] CLIENT waiting for Steam Relay connection... ({connectElapsed}s)");
                }
            }

            if (NetworkManager.Singleton.IsConnectedClient)
            {
                Debug.Log("[SteamLobby] CLIENT connected via Steam Relay!");
            }
            else
            {
                Debug.LogError("[SteamLobby] CLIENT failed to connect via Steam Relay!");
            }
        }
    }

    /// <summary>
    /// Fallback: Start networking using Unity Transport (IP-based, LAN only)
    /// </summary>
    IEnumerator StartWithUnityTransport(Unity.Netcode.Transports.UTP.UnityTransport transport, bool asHost)
    {
        if (asHost)
        {
            Debug.Log("[SteamLobby] Starting as HOST with UnityTransport (LAN)...");

            transport.ConnectionData.Address = "0.0.0.0";
            transport.ConnectionData.Port = 7777;

            NetworkManager.Singleton.StartHost();

            string hostIP = GetLocalIPAddress();
            if (InLobby)
            {
                SteamMatchmaking.SetLobbyData(CurrentLobbyID, "hostIP", hostIP);
                SteamMatchmaking.SetLobbyData(CurrentLobbyID, "hostPort", "7777");
                SteamMatchmaking.SetLobbyData(CurrentLobbyID, "useSteamRelay", "0");
                Debug.Log($"[SteamLobby] HOST started (LAN). IP: {hostIP}:7777");
            }

            yield return new WaitForSeconds(2f);
            Debug.Log("[SteamLobby] HOST fully ready (LAN)!");
        }
        else
        {
            Debug.Log("[SteamLobby] Starting as CLIENT with UnityTransport (LAN)...");

            string hostIP = "";
            string hostPort = "7777";

            if (InLobby)
            {
                hostIP = SteamMatchmaking.GetLobbyData(CurrentLobbyID, "hostIP");
                hostPort = SteamMatchmaking.GetLobbyData(CurrentLobbyID, "hostPort");
            }

            if (string.IsNullOrEmpty(hostIP))
            {
                Debug.LogError("[SteamLobby] No host IP found!");
                hostIP = "127.0.0.1";
            }

            Debug.Log($"[SteamLobby] CLIENT connecting to: {hostIP}:{hostPort}");

            transport.ConnectionData.Address = hostIP;
            if (ushort.TryParse(hostPort, out ushort port))
            {
                transport.ConnectionData.Port = port;
            }

            NetworkManager.Singleton.StartClient();

            float connectTimeout = 10f;
            float connectElapsed = 0f;
            while (!NetworkManager.Singleton.IsConnectedClient && connectElapsed < connectTimeout)
            {
                yield return new WaitForSeconds(0.5f);
                connectElapsed += 0.5f;
            }

            if (NetworkManager.Singleton.IsConnectedClient)
            {
                Debug.Log("[SteamLobby] CLIENT connected (LAN)!");
            }
            else
            {
                Debug.LogError("[SteamLobby] CLIENT failed to connect (LAN)!");
            }
        }
    }

    /// <summary>
    /// Get local IP address for LAN play
    /// </summary>
    string GetLocalIPAddress()
    {
        try
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    // Prefer 192.168.x.x or 10.x.x.x addresses
                    string ipStr = ip.ToString();
                    if (ipStr.StartsWith("192.168.") || ipStr.StartsWith("10."))
                    {
                        return ipStr;
                    }
                }
            }
            // Fallback to first IPv4
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SteamLobby] Failed to get local IP: {e.Message}");
        }
        return "127.0.0.1";
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
        // Only leave lobby if Steam is still initialized
        if (InLobby && SteamManager.Initialized)
        {
            try
            {
                LeaveLobby();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SteamLobby] Could not leave lobby on destroy: {e.Message}");
            }
        }
    }
}
