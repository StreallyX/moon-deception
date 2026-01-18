using UnityEngine;
using Unity.Netcode;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// Sets up the NetworkManager with proper configuration.
/// Attach to a GameObject with NetworkManager component.
/// Supports both Steam Relay (internet) and Unity Transport (LAN).
/// </summary>
public class NetworkManagerSetup : MonoBehaviour
{
    [Header("Network Settings")]
    public int maxPlayers = 6; // 1 Astronaut + 5 Aliens

    [Header("Transport Selection")]
    [Tooltip("Prefer Steam transport when Steam is available (recommended for internet play)")]
    public bool preferSteamTransport = true;

    void Awake()
    {
        // Ensure NetworkManager exists
        var networkManager = GetComponent<NetworkManager>();
        if (networkManager == null)
        {
            networkManager = gameObject.AddComponent<NetworkManager>();
        }

        // Configure Unity Transport (fallback for LAN)
        var unityTransport = GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        if (unityTransport == null)
        {
            unityTransport = gameObject.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        }

        // Set Unity Transport connection data (for LAN fallback)
        unityTransport.ConnectionData.Address = "127.0.0.1";
        unityTransport.ConnectionData.Port = 7777;

        // Check if Steam is available and add Steam transport
        bool useSteam = false;

#if !DISABLESTEAMWORKS
        if (preferSteamTransport && SteamManager.Initialized)
        {
            var steamTransport = GetComponent<SteamNetworkTransport>();
            if (steamTransport == null)
            {
                steamTransport = gameObject.AddComponent<SteamNetworkTransport>();
            }

            // Use Steam transport as primary
            networkManager.NetworkConfig.NetworkTransport = steamTransport;
            useSteam = true;
            Debug.Log("[NetworkManagerSetup] Steam transport configured (internet play via Steam Relay)");
        }
        else
#endif
        {
            // Use Unity Transport
            networkManager.NetworkConfig.NetworkTransport = unityTransport;
            Debug.Log("[NetworkManagerSetup] Unity transport configured (LAN play)");
        }

        Debug.Log($"[NetworkManagerSetup] NetworkManager configured. Steam: {useSteam}");
    }

    void Start()
    {
        // Subscribe to connection events
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[Network] Client connected: {clientId}");

        if (NetworkManager.Singleton.IsServer)
        {
            Debug.Log($"[Network] Total clients: {NetworkManager.Singleton.ConnectedClientsIds.Count}");
        }
    }

    void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[Network] Client disconnected: {clientId}");
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
}
