using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Displays network connection status in the corner of the screen.
/// Shows connection state, host indicator, and player count.
/// </summary>
public class NetworkStatusUI : MonoBehaviour
{
    public static NetworkStatusUI Instance { get; private set; }

    [Header("Settings")]
    public bool showInGame = true;
    public float updateInterval = 1f;

    // State
    private float lastUpdateTime;
    private int connectedPlayers;
    private bool isHost;
    private bool isConnected;
    private string connectionStatus = "OFFLINE";

    // UI textures
    private Texture2D bgTexture;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        CreateTextures();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void CreateTextures()
    {
        bgTexture = new Texture2D(1, 1);
        bgTexture.SetPixel(0, 0, new Color(0, 0, 0, 0.6f));
        bgTexture.Apply();
    }

    void Update()
    {
        if (Time.time - lastUpdateTime < updateInterval) return;
        lastUpdateTime = Time.time;

        UpdateNetworkStatus();
    }

    void UpdateNetworkStatus()
    {
        if (NetworkManager.Singleton == null)
        {
            isConnected = false;
            isHost = false;
            connectedPlayers = 0;
            connectionStatus = "OFFLINE";
            return;
        }

        isConnected = NetworkManager.Singleton.IsConnectedClient || NetworkManager.Singleton.IsServer;
        isHost = NetworkManager.Singleton.IsHost;

        if (isConnected)
        {
            connectedPlayers = NetworkManager.Singleton.ConnectedClientsIds.Count;

            if (isHost)
            {
                connectionStatus = "HOST";
            }
            else if (NetworkManager.Singleton.IsClient)
            {
                connectionStatus = "CLIENT";
            }
        }
        else if (NetworkManager.Singleton.IsListening)
        {
            connectionStatus = "CONNEXION...";
        }
        else
        {
            connectionStatus = "OFFLINE";
        }
    }

    void OnGUI()
    {
        if (!showInGame) return;
        if (!isConnected && connectionStatus == "OFFLINE") return;

        // Position in bottom-left corner
        float boxWidth = 140;
        float boxHeight = 45;
        float x = 10;
        float y = Screen.height - boxHeight - 10;

        // Background
        GUI.color = new Color(0, 0, 0, 0.6f);
        GUI.DrawTexture(new Rect(x, y, boxWidth, boxHeight), bgTexture);
        GUI.color = Color.white;

        // Status indicator (colored dot)
        Color statusColor = GetStatusColor();
        GUIStyle dotStyle = new GUIStyle(GUI.skin.label);
        dotStyle.fontSize = 16;
        dotStyle.normal.textColor = statusColor;
        GUI.Label(new Rect(x + 8, y + 5, 20, 20), "●", dotStyle);

        // Status text
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 12;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x + 25, y + 6, boxWidth - 30, 20), connectionStatus, labelStyle);

        // Player count
        if (isConnected)
        {
            labelStyle.fontSize = 11;
            labelStyle.fontStyle = FontStyle.Normal;
            labelStyle.normal.textColor = Color.gray;
            GUI.Label(new Rect(x + 10, y + 24, boxWidth - 20, 20), $"Joueurs: {connectedPlayers}", labelStyle);
        }
    }

    Color GetStatusColor()
    {
        switch (connectionStatus)
        {
            case "HOST":
                return new Color(0.3f, 1f, 0.3f); // Green
            case "CLIENT":
                return new Color(0.3f, 0.7f, 1f); // Blue
            case "CONNEXION...":
                return new Color(1f, 0.8f, 0.3f); // Yellow
            default:
                return Color.gray;
        }
    }

    /// <summary>
    /// Show a temporary connection status message
    /// </summary>
    public void ShowTemporaryStatus(string message, float duration = 3f)
    {
        StartCoroutine(ShowTemporaryStatusCoroutine(message, duration));
    }

    System.Collections.IEnumerator ShowTemporaryStatusCoroutine(string message, float duration)
    {
        string originalStatus = connectionStatus;
        connectionStatus = message;
        yield return new WaitForSeconds(duration);
        connectionStatus = originalStatus;
    }
}
