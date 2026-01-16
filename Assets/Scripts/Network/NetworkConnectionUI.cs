using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Simple UI for hosting or joining a game.
/// Temporary UI using OnGUI - will be replaced with proper UI later.
/// </summary>
public class NetworkConnectionUI : MonoBehaviour
{
    [Header("Connection Settings")]
    public string ipAddress = "127.0.0.1";
    public ushort port = 7777;

    private string ipInput = "127.0.0.1";
    private bool showUI = true;
    private string statusMessage = "";

    void Update()
    {
        // Toggle UI with F1
        if (Input.GetKeyDown(KeyCode.F1))
        {
            showUI = !showUI;
        }

        // Quick host with H key (debug)
        if (Input.GetKeyDown(KeyCode.H) && !IsConnected())
        {
            StartHost();
        }

        // Quick join with J key (debug)
        if (Input.GetKeyDown(KeyCode.J) && !IsConnected())
        {
            StartClient();
        }
    }

    bool IsConnected()
    {
        return NetworkManager.Singleton != null &&
               (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer);
    }

    void OnGUI()
    {
        if (!showUI) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 200));

        // Title
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 18;
        titleStyle.fontStyle = FontStyle.Bold;
        GUILayout.Label("Network Connection", titleStyle);

        GUILayout.Space(10);

        if (!IsConnected())
        {
            // Not connected - show Host/Join options
            DrawConnectionUI();
        }
        else
        {
            // Connected - show status
            DrawConnectedUI();
        }

        // Status message
        if (!string.IsNullOrEmpty(statusMessage))
        {
            GUILayout.Space(10);
            GUIStyle statusStyle = new GUIStyle(GUI.skin.label);
            statusStyle.normal.textColor = Color.yellow;
            GUILayout.Label(statusMessage, statusStyle);
        }

        GUILayout.EndArea();
    }

    void DrawConnectionUI()
    {
        // IP Address input
        GUILayout.BeginHorizontal();
        GUILayout.Label("IP:", GUILayout.Width(30));
        ipInput = GUILayout.TextField(ipInput, GUILayout.Width(150));
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Host button
        if (GUILayout.Button("Host Game (H)", GUILayout.Height(35)))
        {
            StartHost();
        }

        // Join button
        if (GUILayout.Button("Join Game (J)", GUILayout.Height(35)))
        {
            StartClient();
        }

        GUILayout.Space(5);
        GUILayout.Label("Press F1 to hide this UI", GUI.skin.box);
    }

    void DrawConnectedUI()
    {
        string role = NetworkManager.Singleton.IsHost ? "HOST" :
                      NetworkManager.Singleton.IsServer ? "SERVER" : "CLIENT";

        GUIStyle connectedStyle = new GUIStyle(GUI.skin.label);
        connectedStyle.normal.textColor = Color.green;
        GUILayout.Label($"Connected as: {role}", connectedStyle);

        if (NetworkManager.Singleton.IsServer)
        {
            GUILayout.Label($"Players: {NetworkManager.Singleton.ConnectedClientsIds.Count}");
        }

        GUILayout.Label($"Client ID: {NetworkManager.Singleton.LocalClientId}");

        GUILayout.Space(10);

        if (GUILayout.Button("Disconnect", GUILayout.Height(30)))
        {
            Disconnect();
        }
    }

    void StartHost()
    {
        if (NetworkManager.Singleton == null)
        {
            statusMessage = "ERROR: NetworkManager not found!";
            return;
        }

        // Set port
        var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        if (transport != null)
        {
            transport.ConnectionData.Port = port;
        }

        NetworkManager.Singleton.StartHost();
        statusMessage = $"Hosting on port {port}...";
        Debug.Log($"[Network] Started Host on port {port}");
    }

    void StartClient()
    {
        if (NetworkManager.Singleton == null)
        {
            statusMessage = "ERROR: NetworkManager not found!";
            return;
        }

        // Set connection data
        var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        if (transport != null)
        {
            transport.ConnectionData.Address = ipInput;
            transport.ConnectionData.Port = port;
        }

        NetworkManager.Singleton.StartClient();
        statusMessage = $"Connecting to {ipInput}:{port}...";
        Debug.Log($"[Network] Connecting to {ipInput}:{port}");
    }

    void Disconnect()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.Shutdown();
        statusMessage = "Disconnected";
        Debug.Log("[Network] Disconnected");
    }
}
