using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Netcode;
using Steamworks;

/// <summary>
/// Custom Steam Networking Sockets transport for Unity Netcode for GameObjects.
/// Uses Steam's P2P relay system - no port forwarding required!
/// Players connect via SteamID, Steam handles NAT traversal.
/// </summary>
public class SteamNetworkTransport : NetworkTransport
{
    [Header("Steam Transport Settings")]
    [Tooltip("Virtual port for Steam connections (can be any number, default 0)")]
    public int virtualPort = 0;

    [Tooltip("Maximum packet size in bytes")]
    public int maxPacketSize = 1200;

    [Tooltip("Connection timeout in seconds")]
    public float connectionTimeout = 30f;

    // Connection state
    private HSteamListenSocket listenSocket = HSteamListenSocket.Invalid;
    private HSteamNetConnection hostConnection = HSteamNetConnection.Invalid;
    private Dictionary<ulong, HSteamNetConnection> clientConnections = new Dictionary<ulong, HSteamNetConnection>();
    private Dictionary<HSteamNetConnection, ulong> connectionToClientId = new Dictionary<HSteamNetConnection, ulong>();

    private bool isServer = false;
    private bool isClient = false;
    private ulong nextClientId = 1;

    // The host's Steam ID (set before connecting as client)
    public CSteamID HostSteamID { get; set; } = CSteamID.Nil;

    // Steam callbacks
    private Callback<SteamNetConnectionStatusChangedCallback_t> connectionStatusCallback;

    // Message queue for incoming data
    private struct PendingMessage
    {
        public ulong clientId;
        public ArraySegment<byte> data;
        public NetworkEvent eventType;
    }
    private Queue<PendingMessage> pendingMessages = new Queue<PendingMessage>();

    // Server client ID is always 0 in NGO
    public override ulong ServerClientId => 0;

    void Awake()
    {
        Debug.Log("[SteamTransport] Initialized");
    }

    void OnEnable()
    {
        if (SteamManager.Initialized)
        {
            connectionStatusCallback = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatusChanged);
        }
    }

    void OnDisable()
    {
        connectionStatusCallback?.Dispose();
        connectionStatusCallback = null;
    }

    void Update()
    {
        if (!SteamManager.Initialized) return;

        // Poll for incoming messages
        if (isServer)
        {
            PollServerMessages();
        }
        else if (isClient && hostConnection != HSteamNetConnection.Invalid)
        {
            PollClientMessages();
        }
    }

    // ==================== TRANSPORT INTERFACE ====================

    public override void Initialize(NetworkManager networkManager = null)
    {
        Debug.Log("[SteamTransport] Initialize called");
    }

    public override bool StartServer()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamTransport] Steam not initialized!");
            return false;
        }

        Debug.Log($"[SteamTransport] Starting server on virtual port {virtualPort}...");

        // Create a P2P listen socket
        SteamNetworkingConfigValue_t[] options = new SteamNetworkingConfigValue_t[] { };
        listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(virtualPort, options.Length, options);

        if (listenSocket == HSteamListenSocket.Invalid)
        {
            Debug.LogError("[SteamTransport] Failed to create listen socket!");
            return false;
        }

        isServer = true;
        isClient = false;
        nextClientId = 1; // 0 is reserved for server

        Debug.Log($"[SteamTransport] Server started! SteamID: {SteamUser.GetSteamID()}");
        return true;
    }

    public override bool StartClient()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamTransport] Steam not initialized!");
            return false;
        }

        if (!HostSteamID.IsValid())
        {
            Debug.LogError("[SteamTransport] Host SteamID not set! Set HostSteamID before connecting.");
            return false;
        }

        Debug.Log($"[SteamTransport] Connecting to host {HostSteamID} on virtual port {virtualPort}...");

        // Create identity for the host
        SteamNetworkingIdentity hostIdentity = new SteamNetworkingIdentity();
        hostIdentity.SetSteamID(HostSteamID);

        // Connect to the host via P2P
        SteamNetworkingConfigValue_t[] options = new SteamNetworkingConfigValue_t[] { };
        hostConnection = SteamNetworkingSockets.ConnectP2P(ref hostIdentity, virtualPort, options.Length, options);

        if (hostConnection == HSteamNetConnection.Invalid)
        {
            Debug.LogError("[SteamTransport] Failed to create connection to host!");
            return false;
        }

        isClient = true;
        isServer = false;

        Debug.Log($"[SteamTransport] Client connecting... Connection handle: {hostConnection}");
        return true;
    }

    public override void Shutdown()
    {
        Debug.Log("[SteamTransport] Shutting down...");

        // Close all client connections
        foreach (var conn in clientConnections.Values)
        {
            if (conn != HSteamNetConnection.Invalid)
            {
                SteamNetworkingSockets.CloseConnection(conn, 0, "Shutdown", false);
            }
        }
        clientConnections.Clear();
        connectionToClientId.Clear();

        // Close host connection (if client)
        if (hostConnection != HSteamNetConnection.Invalid)
        {
            SteamNetworkingSockets.CloseConnection(hostConnection, 0, "Shutdown", false);
            hostConnection = HSteamNetConnection.Invalid;
        }

        // Close listen socket (if server)
        if (listenSocket != HSteamListenSocket.Invalid)
        {
            SteamNetworkingSockets.CloseListenSocket(listenSocket);
            listenSocket = HSteamListenSocket.Invalid;
        }

        isServer = false;
        isClient = false;
        pendingMessages.Clear();

        Debug.Log("[SteamTransport] Shutdown complete");
    }

    public override void Send(ulong clientId, ArraySegment<byte> data, NetworkDelivery delivery)
    {
        if (!SteamManager.Initialized) return;

        HSteamNetConnection connection;

        if (isServer)
        {
            // Server sending to client
            if (!clientConnections.TryGetValue(clientId, out connection))
            {
                Debug.LogWarning($"[SteamTransport] No connection for client {clientId}");
                return;
            }
        }
        else
        {
            // Client sending to server
            connection = hostConnection;
        }

        if (connection == HSteamNetConnection.Invalid)
        {
            Debug.LogWarning("[SteamTransport] Cannot send - invalid connection");
            return;
        }

        // Convert delivery type to Steam flags
        int sendFlags = GetSteamSendFlags(delivery);

        // Send the message
        IntPtr dataPtr = Marshal.AllocHGlobal(data.Count);
        try
        {
            Marshal.Copy(data.Array, data.Offset, dataPtr, data.Count);

            EResult result = SteamNetworkingSockets.SendMessageToConnection(
                connection,
                dataPtr,
                (uint)data.Count,
                sendFlags,
                out long _
            );

            if (result != EResult.k_EResultOK)
            {
                Debug.LogWarning($"[SteamTransport] Send failed: {result}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(dataPtr);
        }
    }

    public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
    {
        clientId = 0;
        payload = default;
        receiveTime = Time.realtimeSinceStartup;

        if (pendingMessages.Count > 0)
        {
            var msg = pendingMessages.Dequeue();
            clientId = msg.clientId;
            payload = msg.data;
            return msg.eventType;
        }

        return NetworkEvent.Nothing;
    }

    public override ulong GetCurrentRtt(ulong clientId)
    {
        HSteamNetConnection connection;

        if (isServer)
        {
            if (!clientConnections.TryGetValue(clientId, out connection))
                return 0;
        }
        else
        {
            connection = hostConnection;
        }

        if (connection == HSteamNetConnection.Invalid)
            return 0;

        // Get connection info which contains ping
        SteamNetConnectionInfo_t info = new SteamNetConnectionInfo_t();
        if (SteamNetworkingSockets.GetConnectionInfo(connection, out info))
        {
            // m_nPing is in the quick status, use connection info instead
            return 50; // Default RTT estimate if not available
        }

        return 0;
    }

    public override void DisconnectLocalClient()
    {
        if (hostConnection != HSteamNetConnection.Invalid)
        {
            SteamNetworkingSockets.CloseConnection(hostConnection, 0, "Disconnect", false);
            hostConnection = HSteamNetConnection.Invalid;
        }
        isClient = false;
    }

    public override void DisconnectRemoteClient(ulong clientId)
    {
        if (clientConnections.TryGetValue(clientId, out HSteamNetConnection connection))
        {
            SteamNetworkingSockets.CloseConnection(connection, 0, "Kicked", false);
            connectionToClientId.Remove(connection);
            clientConnections.Remove(clientId);
        }
    }

    // ==================== MESSAGE POLLING ====================

    void PollServerMessages()
    {
        // Poll messages from each connected client
        IntPtr[] messagePointers = new IntPtr[32];

        // We need to poll each connection individually in Steamworks.NET
        foreach (var kvp in clientConnections)
        {
            HSteamNetConnection conn = kvp.Value;
            if (conn == HSteamNetConnection.Invalid) continue;

            int numMessages = SteamNetworkingSockets.ReceiveMessagesOnConnection(
                conn,
                messagePointers,
                messagePointers.Length
            );

            for (int i = 0; i < numMessages; i++)
            {
                ProcessMessage(messagePointers[i], true, kvp.Key);
            }
        }
    }

    void PollClientMessages()
    {
        IntPtr[] messagePointers = new IntPtr[32];

        int numMessages = SteamNetworkingSockets.ReceiveMessagesOnConnection(
            hostConnection,
            messagePointers,
            messagePointers.Length
        );

        for (int i = 0; i < numMessages; i++)
        {
            ProcessMessage(messagePointers[i], false, ServerClientId);
        }
    }

    void ProcessMessage(IntPtr messagePtr, bool isServerReceiving, ulong knownClientId)
    {
        SteamNetworkingMessage_t message = Marshal.PtrToStructure<SteamNetworkingMessage_t>(messagePtr);

        // Use the known client ID passed in
        ulong clientId = knownClientId;

        // Copy message data
        byte[] data = new byte[message.m_cbSize];
        Marshal.Copy(message.m_pData, data, 0, message.m_cbSize);

        pendingMessages.Enqueue(new PendingMessage
        {
            clientId = clientId,
            data = new ArraySegment<byte>(data),
            eventType = NetworkEvent.Data
        });

        // Release the message
        SteamNetworkingMessage_t.Release(messagePtr);
    }

    // ==================== CONNECTION STATUS ====================

    void OnConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t callback)
    {
        Debug.Log($"[SteamTransport] Connection status changed: {callback.m_info.m_eState} for connection {callback.m_hConn}");

        switch (callback.m_info.m_eState)
        {
            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                // Someone is trying to connect to us (server only)
                if (isServer)
                {
                    AcceptConnection(callback.m_hConn);
                }
                break;

            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                if (isServer)
                {
                    // New client connected
                    HandleClientConnected(callback.m_hConn);
                }
                else if (isClient)
                {
                    // We connected to the server
                    Debug.Log("[SteamTransport] Connected to server!");
                    pendingMessages.Enqueue(new PendingMessage
                    {
                        clientId = ServerClientId,
                        data = default,
                        eventType = NetworkEvent.Connect
                    });
                }
                break;

            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                if (isServer)
                {
                    HandleClientDisconnected(callback.m_hConn);
                }
                else if (isClient)
                {
                    Debug.Log("[SteamTransport] Disconnected from server");
                    pendingMessages.Enqueue(new PendingMessage
                    {
                        clientId = ServerClientId,
                        data = default,
                        eventType = NetworkEvent.Disconnect
                    });
                    hostConnection = HSteamNetConnection.Invalid;
                }
                break;
        }
    }

    void AcceptConnection(HSteamNetConnection connection)
    {
        EResult result = SteamNetworkingSockets.AcceptConnection(connection);
        if (result != EResult.k_EResultOK)
        {
            Debug.LogWarning($"[SteamTransport] Failed to accept connection: {result}");
            SteamNetworkingSockets.CloseConnection(connection, 0, "Accept failed", false);
        }
        else
        {
            Debug.Log($"[SteamTransport] Accepted connection {connection}");
        }
    }

    void HandleClientConnected(HSteamNetConnection connection)
    {
        // Assign a client ID
        ulong clientId = nextClientId++;

        clientConnections[clientId] = connection;
        connectionToClientId[connection] = clientId;

        Debug.Log($"[SteamTransport] Client {clientId} connected (connection {connection})");

        pendingMessages.Enqueue(new PendingMessage
        {
            clientId = clientId,
            data = default,
            eventType = NetworkEvent.Connect
        });
    }

    void HandleClientDisconnected(HSteamNetConnection connection)
    {
        if (connectionToClientId.TryGetValue(connection, out ulong clientId))
        {
            Debug.Log($"[SteamTransport] Client {clientId} disconnected");

            pendingMessages.Enqueue(new PendingMessage
            {
                clientId = clientId,
                data = default,
                eventType = NetworkEvent.Disconnect
            });

            connectionToClientId.Remove(connection);
            clientConnections.Remove(clientId);
        }

        SteamNetworkingSockets.CloseConnection(connection, 0, "Disconnected", false);
    }

    // ==================== HELPERS ====================

    // Steam send flags (from steam_api.h)
    private const int k_nSteamNetworkingSend_Unreliable = 0;
    private const int k_nSteamNetworkingSend_NoNagle = 1;
    private const int k_nSteamNetworkingSend_Reliable = 8;

    int GetSteamSendFlags(NetworkDelivery delivery)
    {
        switch (delivery)
        {
            case NetworkDelivery.Unreliable:
                return k_nSteamNetworkingSend_Unreliable | k_nSteamNetworkingSend_NoNagle;

            case NetworkDelivery.UnreliableSequenced:
                return k_nSteamNetworkingSend_Unreliable;

            case NetworkDelivery.Reliable:
                return k_nSteamNetworkingSend_Reliable;

            case NetworkDelivery.ReliableSequenced:
                return k_nSteamNetworkingSend_Reliable;

            case NetworkDelivery.ReliableFragmentedSequenced:
                return k_nSteamNetworkingSend_Reliable;

            default:
                return k_nSteamNetworkingSend_Reliable;
        }
    }

    /// <summary>
    /// Get the local Steam ID (useful for debugging)
    /// </summary>
    public CSteamID GetLocalSteamID()
    {
        if (SteamManager.Initialized)
        {
            return SteamUser.GetSteamID();
        }
        return CSteamID.Nil;
    }
}
