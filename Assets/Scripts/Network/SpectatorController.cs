using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

/// <summary>
/// Spectator camera controller for dead players.
/// Allows free camera movement or following alive players.
/// </summary>
public class SpectatorController : MonoBehaviour
{
    public static SpectatorController Instance { get; private set; }

    [Header("Camera Settings")]
    public float moveSpeed = 10f;
    public float fastMoveSpeed = 25f;
    public float mouseSensitivity = 2f;
    public float smoothTime = 0.1f;

    [Header("Follow Settings")]
    public float followDistance = 5f;
    public float followHeight = 2f;
    public Vector3 followOffset = new Vector3(0, 2f, -5f);

    // State
    private bool isActive = false;
    public bool IsActive => isActive;
    private Camera spectatorCamera;
    private NetworkedPlayer followTarget;
    private List<NetworkedPlayer> alivePlayers = new List<NetworkedPlayer>();
    private int currentTargetIndex = -1; // -1 = free cam

    // Camera rotation
    private float rotationX = 0f;
    private float rotationY = 0f;

    // Smooth follow
    private Vector3 currentVelocity;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Activate spectator mode - creates a dedicated spectator camera
    /// </summary>
    public void Activate(Camera originalCamera)
    {
        isActive = true;

        // Disable ALL existing cameras and audio listeners first to avoid conflicts
        foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            cam.enabled = false;
            var listener = cam.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
        }
        Debug.Log("[Spectator] Disabled all existing cameras");

        // Create a NEW dedicated spectator camera instead of reusing the player's camera
        // This avoids issues with the player's camera being tied to their GameObject
        GameObject camObj = new GameObject("SpectatorCamera");
        DontDestroyOnLoad(camObj);

        spectatorCamera = camObj.AddComponent<Camera>();
        spectatorCamera.clearFlags = CameraClearFlags.Skybox;
        spectatorCamera.cullingMask = -1; // Everything
        spectatorCamera.depth = 100; // High priority
        spectatorCamera.fieldOfView = 60f;
        spectatorCamera.nearClipPlane = 0.1f;
        spectatorCamera.farClipPlane = 1000f;

        // Add AudioListener to the spectator camera
        camObj.AddComponent<AudioListener>();

        // Copy position from original camera if available
        if (originalCamera != null)
        {
            spectatorCamera.transform.position = originalCamera.transform.position;
            spectatorCamera.transform.rotation = originalCamera.transform.rotation;

            Vector3 euler = originalCamera.transform.eulerAngles;
            rotationX = euler.y;
            rotationY = euler.x;

            // Disable the original camera and its AudioListener
            originalCamera.enabled = false;
            var originalListener = originalCamera.GetComponent<AudioListener>();
            if (originalListener != null) originalListener.enabled = false;
        }

        // Unlock cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Refresh player list
        RefreshPlayerList();

        // Auto-follow first alive player instead of free cam
        if (alivePlayers.Count > 0)
        {
            currentTargetIndex = 0;
            followTarget = alivePlayers[0];
            string playerType = followTarget.IsAstronautRole.Value ? "ASTRONAUT" : "ALIEN";
            Debug.Log($"[Spectator] Activated - Auto-following {playerType}");

            // Move camera to follow position immediately
            Vector3 targetPos = followTarget.transform.position + followTarget.transform.TransformDirection(followOffset);
            spectatorCamera.transform.position = targetPos;
            spectatorCamera.transform.LookAt(followTarget.transform.position + Vector3.up * 1.5f);

            if (GameUIManager.Instance != null)
            {
                GameUIManager.Instance.ShowNotification($"Suivi: {playerType} (TAB = Changer)", 3f);
            }
        }
        else
        {
            currentTargetIndex = -1;
            followTarget = null;
            Debug.Log("[Spectator] Activated - No alive players, free cam mode");
        }

        Debug.Log($"[Spectator] Spectator camera created at {spectatorCamera.transform.position}");
    }

    /// <summary>
    /// Deactivate spectator mode
    /// </summary>
    public void Deactivate()
    {
        isActive = false;

        // Destroy the detached spectator camera if it exists
        if (spectatorCamera != null)
        {
            Destroy(spectatorCamera.gameObject);
            Debug.Log("[Spectator] Destroyed spectator camera");
        }

        spectatorCamera = null;
        followTarget = null;
        alivePlayers.Clear();
    }

    void Update()
    {
        if (!isActive || spectatorCamera == null) return;

        // Handle input
        HandleInput();

        // Update camera position
        if (followTarget != null)
        {
            UpdateFollowCamera();
        }
        else
        {
            UpdateFreeCamera();
        }
    }

    void HandleInput()
    {
        // Don't process input if game ended or menu is showing
        if (GameManager.Instance != null && GameManager.Instance.CurrentPhase == GameManager.GamePhase.Ended)
        {
            return;
        }

        // Don't process input if cursor is visible (menu is open)
        if (Cursor.visible && Cursor.lockState == CursorLockMode.None)
        {
            // Only allow TAB to switch players when menu/game over is showing
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                CycleTarget();
            }
            return;
        }

        // TAB - cycle through players
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            CycleTarget();
        }

        // SPACE - free cam mode
        if (Input.GetKeyDown(KeyCode.Space))
        {
            currentTargetIndex = -1;
            followTarget = null;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Debug.Log("[Spectator] Free camera mode");
        }

        // ESC - unlock cursor in free cam
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Left click - lock cursor in free cam (for mouse look)
        if (Input.GetMouseButtonDown(0) && followTarget == null)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void CycleTarget()
    {
        RefreshPlayerList();

        if (alivePlayers.Count == 0)
        {
            currentTargetIndex = -1;
            followTarget = null;
            Debug.Log("[Spectator] No alive players to follow");
            return;
        }

        // Cycle to next player
        currentTargetIndex++;
        if (currentTargetIndex >= alivePlayers.Count)
        {
            currentTargetIndex = 0;
        }

        followTarget = alivePlayers[currentTargetIndex];

        // Unlock cursor when following
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        string playerType = followTarget.IsAstronautRole.Value ? "ASTRONAUT" : "ALIEN";
        Debug.Log($"[Spectator] Following {playerType} (Client {followTarget.OwnerClientId})");

        // Show notification
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.ShowNotification($"Suivi: {playerType}", 2f);
        }
    }

    void RefreshPlayerList()
    {
        alivePlayers.Clear();

        var allPlayers = FindObjectsByType<NetworkedPlayer>(FindObjectsSortMode.None);
        foreach (var player in allPlayers)
        {
            if (player != null && player.IsSpawned && !player.IsDead.Value)
            {
                alivePlayers.Add(player);
            }
        }

        Debug.Log($"[Spectator] Found {alivePlayers.Count} alive players");
    }

    void UpdateFreeCamera()
    {
        // Mouse look (only when cursor locked)
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            rotationX += Input.GetAxis("Mouse X") * mouseSensitivity;
            rotationY -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            rotationY = Mathf.Clamp(rotationY, -89f, 89f);

            spectatorCamera.transform.rotation = Quaternion.Euler(rotationY, rotationX, 0f);
        }

        // WASD movement
        float speed = Input.GetKey(KeyCode.LeftShift) ? fastMoveSpeed : moveSpeed;
        Vector3 move = Vector3.zero;

        if (Input.GetKey(KeyCode.W)) move += spectatorCamera.transform.forward;
        if (Input.GetKey(KeyCode.S)) move -= spectatorCamera.transform.forward;
        if (Input.GetKey(KeyCode.A)) move -= spectatorCamera.transform.right;
        if (Input.GetKey(KeyCode.D)) move += spectatorCamera.transform.right;
        if (Input.GetKey(KeyCode.E)) move += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) move -= Vector3.up;

        spectatorCamera.transform.position += move.normalized * speed * Time.deltaTime;
    }

    void UpdateFollowCamera()
    {
        if (followTarget == null || !followTarget.gameObject.activeInHierarchy)
        {
            // Target was destroyed or inactive - switch to next
            Debug.Log("[Spectator] Follow target lost, switching to next");
            CycleTarget();
            return;
        }

        if (spectatorCamera == null)
        {
            Debug.LogWarning("[Spectator] Camera is null!");
            return;
        }

        // Calculate target position behind and above the player
        Vector3 targetPos = followTarget.transform.position +
                           followTarget.transform.TransformDirection(followOffset);

        // Smooth follow
        spectatorCamera.transform.position = Vector3.SmoothDamp(
            spectatorCamera.transform.position,
            targetPos,
            ref currentVelocity,
            smoothTime
        );

        // Look at player
        Vector3 lookAt = followTarget.transform.position + Vector3.up * 1.5f;
        spectatorCamera.transform.LookAt(lookAt);
    }

    // ==================== UI ====================

    void OnGUI()
    {
        if (!isActive) return;

        // Don't show UI if game ended or in lobby/main menu
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentPhase == GameManager.GamePhase.Ended ||
            GameManager.Instance.CurrentPhase == GameManager.GamePhase.Lobby) return;

        // Spectator mode indicator
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 18;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.MiddleCenter;

        // Background
        GUI.color = new Color(0, 0, 0, 0.7f);
        GUI.DrawTexture(new Rect(Screen.width / 2 - 150, 10, 300, 35), Texture2D.whiteTexture);
        GUI.color = Color.white;

        string modeText = followTarget != null
            ? $"SPECTATEUR - Suivi: {(followTarget.IsAstronautRole.Value ? "ASTRONAUT" : "ALIEN")}"
            : "SPECTATEUR - Caméra Libre";
        GUI.Label(new Rect(Screen.width / 2 - 150, 15, 300, 25), modeText, style);

        // Controls hint
        style.fontSize = 12;
        style.normal.textColor = Color.gray;
        GUI.Label(new Rect(Screen.width / 2 - 150, 50, 300, 20), "TAB = Joueur suivant | ESPACE = Caméra libre", style);

        // Player list
        if (alivePlayers.Count > 0)
        {
            DrawPlayerList();
        }
    }

    void DrawPlayerList()
    {
        float boxWidth = 180;
        float boxHeight = 30 + alivePlayers.Count * 25;
        float x = Screen.width - boxWidth - 10;
        float y = 10;

        // Background
        GUI.color = new Color(0, 0, 0, 0.8f);
        GUI.DrawTexture(new Rect(x, y, boxWidth, boxHeight), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Title
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 14;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x + 10, y + 5, boxWidth - 20, 20), "JOUEURS EN VIE", titleStyle);

        // Players
        GUIStyle playerStyle = new GUIStyle(GUI.skin.label);
        playerStyle.fontSize = 12;

        float lineY = y + 30;
        for (int i = 0; i < alivePlayers.Count; i++)
        {
            var player = alivePlayers[i];
            if (player == null) continue;

            string playerType = player.IsAstronautRole.Value ? "Astronaute" : "Alien";
            bool isFollowing = (followTarget == player);

            playerStyle.normal.textColor = isFollowing ? Color.yellow : Color.white;
            string prefix = isFollowing ? "> " : "  ";

            GUI.Label(new Rect(x + 10, lineY, boxWidth - 20, 20), $"{prefix}{playerType}", playerStyle);
            lineY += 22;
        }
    }
}
