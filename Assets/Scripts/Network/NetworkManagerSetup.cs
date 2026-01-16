using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Sets up the NetworkManager with proper configuration.
/// Attach to a GameObject with NetworkManager component.
/// </summary>
public class NetworkManagerSetup : MonoBehaviour
{
    [Header("Network Settings")]
    public int maxPlayers = 6; // 1 Astronaut + 5 Aliens

    void Awake()
    {
        // Ensure NetworkManager exists
        var networkManager = GetComponent<NetworkManager>();
        if (networkManager == null)
        {
            networkManager = gameObject.AddComponent<NetworkManager>();
        }

        // Configure transport (Unity Transport)
        var transport = GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        if (transport == null)
        {
            transport = gameObject.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            networkManager.NetworkConfig.NetworkTransport = transport;
        }

        // Set connection data
        transport.ConnectionData.Address = "127.0.0.1";
        transport.ConnectionData.Port = 7777;

        Debug.Log("[NetworkManagerSetup] NetworkManager configured");
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
