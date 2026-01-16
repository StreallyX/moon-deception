using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

/// <summary>
/// Handles game initialization and loading sequence.
/// Waits for all systems to be ready before starting the game.
/// </summary>
public class GameLoader : MonoBehaviour
{
    public static GameLoader Instance { get; private set; }

    [Header("Loading Settings")]
    [SerializeField] private float minLoadingTime = 3f; // Minimum time to show loading screen (3 seconds)
    [SerializeField] private float maxWaitTime = 10f; // Maximum wait time before forcing start
    [SerializeField] private int expectedZoneCount = 4; // Expected number of zones

    [Header("Loading State")]
    [SerializeField] private bool isLoading = true;
    [SerializeField] private float loadingProgress = 0f;
    [SerializeField] private string currentLoadingStep = "Initializing...";

    [Header("Events")]
    public UnityEvent OnLoadingStart;
    public UnityEvent<float, string> OnLoadingProgress; // progress (0-1), step name
    public UnityEvent OnLoadingComplete;

    // Loading checks
    private bool mapManagerReady = false;
    private bool spawnManagerReady = false;
    private bool audioManagerReady = false;
    private bool uiManagerReady = false;

    public bool IsLoading => isLoading;
    public float LoadingProgress => loadingProgress;
    public string CurrentStep => currentLoadingStep;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Initialize events if null
            if (OnLoadingStart == null) OnLoadingStart = new UnityEvent();
            if (OnLoadingProgress == null) OnLoadingProgress = new UnityEvent<float, string>();
            if (OnLoadingComplete == null) OnLoadingComplete = new UnityEvent();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // Don't auto-start loading - wait for BeginLoading() to be called
        // BeginLoading() is triggered when player presses H (Host) or J (Join)
        isLoading = false;
        Debug.Log("[GameLoader] Ready. Press H to Host or J to Join.");
    }

    /// <summary>
    /// Call this to start the loading sequence (e.g., when hosting or joining a game)
    /// </summary>
    public void BeginLoading()
    {
        if (isLoading) return; // Already loading
        StartCoroutine(LoadGameSequence());
    }

    IEnumerator LoadGameSequence()
    {
        isLoading = true;
        float startTime = Time.time;
        OnLoadingStart?.Invoke();

        Debug.Log("[GameLoader] ========== LOADING GAME ==========");

        // Initial delay to let scene fully load
        UpdateProgress(0.05f, "Initializing scene...");
        yield return new WaitForSeconds(0.5f);

        // Step 0: Wait for network connection if we're joining
        yield return StartCoroutine(WaitForNetworkConnection());

        // Step 1: Wait for MapManager
        yield return StartCoroutine(WaitForMapManager());

        // Step 2: Wait for SpawnManager
        yield return StartCoroutine(WaitForSpawnManager());

        // Step 3: Wait for AudioManager
        yield return StartCoroutine(WaitForAudioManager());

        // Step 4: Wait for UIManager
        yield return StartCoroutine(WaitForUIManager());

        // Step 5: Final preparations
        UpdateProgress(0.9f, "Finalizing...");
        yield return null;

        // Ensure minimum loading time for smooth UX
        float elapsed = Time.time - startTime;
        if (elapsed < minLoadingTime)
        {
            yield return new WaitForSeconds(minLoadingTime - elapsed);
        }

        // Loading complete!
        UpdateProgress(1f, "Ready!");
        Debug.Log("[GameLoader] ========== LOADING COMPLETE ==========");

        yield return new WaitForSeconds(0.2f); // Brief pause to show "Ready!"

        isLoading = false;
        OnLoadingComplete?.Invoke();

        // Start the game - but only in single player mode
        // In multiplayer, NetworkSpawnManager handles game initialization
        bool isMultiplayer = NetworkManager.Singleton != null &&
                            (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer);

        if (!isMultiplayer && GameManager.Instance != null)
        {
            Debug.Log("[GameLoader] Single player mode - calling GameManager.StartGame()");
            GameManager.Instance.StartGame();
        }
        else if (isMultiplayer)
        {
            Debug.Log("[GameLoader] Multiplayer mode - NetworkSpawnManager handles initialization");
        }
    }

    IEnumerator WaitForNetworkConnection()
    {
        // Skip if no network manager
        if (NetworkManager.Singleton == null)
        {
            Debug.Log("[GameLoader] No NetworkManager - skipping network wait");
            yield break;
        }

        // Skip if we're the host/server (already connected)
        if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
        {
            Debug.Log("[GameLoader] We are host/server - skipping network wait");
            yield break;
        }

        // Wait for client to be connected
        UpdateProgress(0.07f, "Connecting to server...");
        float timeout = Time.time + maxWaitTime;

        while (!NetworkManager.Singleton.IsConnectedClient && Time.time < timeout)
        {
            Debug.Log("[GameLoader] Waiting for client connection...");
            yield return new WaitForSeconds(0.3f);
        }

        if (NetworkManager.Singleton.IsConnectedClient)
        {
            Debug.Log("[GameLoader] Client connected successfully!");
            // Give extra time for scene sync
            yield return new WaitForSeconds(1f);
        }
        else
        {
            Debug.LogWarning("[GameLoader] Client connection timeout - proceeding anyway");
        }
    }

    IEnumerator WaitForMapManager()
    {
        UpdateProgress(0.1f, "Searching for map zones...");
        Debug.Log("[GameLoader] Starting WaitForMapManager...");

        // Check if we're a client
        bool isClient = NetworkManager.Singleton != null &&
                       NetworkManager.Singleton.IsClient &&
                       !NetworkManager.Singleton.IsServer;
        Debug.Log($"[GameLoader] IsClient: {isClient}");

        // Wait for MapManager to exist
        float timeout = Time.time + maxWaitTime;
        while (MapManager.Instance == null && Time.time < timeout)
        {
            Debug.Log("[GameLoader] Waiting for MapManager.Instance...");
            yield return new WaitForSeconds(0.2f);
        }

        if (MapManager.Instance == null)
        {
            // CLIENTS should NEVER create managers - they must exist in scene or be spawned by server
            if (isClient)
            {
                Debug.LogWarning("[GameLoader] CLIENT: MapManager not found - waiting for server...");
                // Keep waiting, don't create
                while (MapManager.Instance == null && Time.time < timeout)
                {
                    yield return new WaitForSeconds(0.3f);
                }
            }
            else
            {
                // Server/Single player can create if needed
                Debug.LogWarning("[GameLoader] SERVER: MapManager not found! Creating one...");
                GameObject mapMgrObj = new GameObject("MapManager");
                mapMgrObj.AddComponent<MapManager>();
                yield return null;
            }
        }

        Debug.Log($"[GameLoader] MapManager found. Current zone count: {MapManager.Instance.ZoneCount}");
        UpdateProgress(0.15f, "Loading map zones...");

        // Wait for MapZone.Start() to run (they register themselves)
        yield return new WaitForSeconds(0.5f);

        // For clients or if no zones found, refresh zones (clears stale refs and re-finds)
        if (isClient || MapManager.Instance.ZoneCount == 0)
        {
            Debug.Log("[GameLoader] Refreshing zones...");
            MapManager.Instance.RefreshZones();
        }
        else
        {
            MapManager.Instance.FindAllZonesInScene();
        }
        yield return new WaitForSeconds(0.2f);

        Debug.Log($"[GameLoader] After zone search: {MapManager.Instance.ZoneCount} zones");

        // Keep checking until we have zones or timeout
        timeout = Time.time + maxWaitTime;
        int attempts = 0;
        while (!mapManagerReady && Time.time < timeout)
        {
            int zoneCount = MapManager.Instance.ZoneCount;
            attempts++;

            Debug.Log($"[GameLoader] Zone check attempt {attempts}: {zoneCount} zones found");

            if (zoneCount >= expectedZoneCount)
            {
                mapManagerReady = true;
                Debug.Log($"[GameLoader] MapManager ready: {zoneCount} zones found");
            }
            else if (zoneCount > 0 && attempts > 5)
            {
                // Accept partial zones after a few attempts
                mapManagerReady = true;
                Debug.Log($"[GameLoader] Accepting {zoneCount} zones after {attempts} attempts");
            }
            else
            {
                // Try refreshing zones again
                MapManager.Instance.RefreshZones();
                float zoneProgress = zoneCount > 0 ? (float)zoneCount / expectedZoneCount : 0f;
                UpdateProgress(0.15f + (zoneProgress * 0.15f), $"Loading zones ({zoneCount}/{expectedZoneCount})...");
            }

            yield return new WaitForSeconds(0.3f);
        }

        if (!mapManagerReady)
        {
            int finalCount = MapManager.Instance?.ZoneCount ?? 0;
            Debug.LogWarning($"[GameLoader] MapManager timeout after {attempts} attempts - proceeding with {finalCount} zones");
            mapManagerReady = true;
        }

        // List all found zones
        if (MapManager.Instance != null)
        {
            Debug.Log($"[GameLoader] Final zone count: {MapManager.Instance.ZoneCount}");
            foreach (var zone in MapManager.Instance.AllZones)
            {
                if (zone != null)
                {
                    Debug.Log($"[GameLoader] Zone: {zone.zoneName} ({zone.zoneType})");
                }
            }
        }

        UpdateProgress(0.3f, $"Map zones loaded ({MapManager.Instance?.ZoneCount ?? 0} zones)");
    }

    IEnumerator WaitForSpawnManager()
    {
        UpdateProgress(0.4f, "Initializing spawn system...");
        float timeout = Time.time + maxWaitTime;

        bool isClient = NetworkManager.Singleton != null &&
                       NetworkManager.Singleton.IsClient &&
                       !NetworkManager.Singleton.IsServer;

        while (!spawnManagerReady && Time.time < timeout)
        {
            if (SpawnManager.Instance != null)
            {
                spawnManagerReady = true;
                Debug.Log("[GameLoader] SpawnManager ready");
            }
            yield return new WaitForSeconds(0.1f);
        }

        if (!spawnManagerReady)
        {
            // CLIENTS should NEVER create managers
            if (isClient)
            {
                Debug.LogWarning("[GameLoader] CLIENT: SpawnManager not found - continuing without it");
                spawnManagerReady = true; // Mark as ready to continue loading
            }
            else
            {
                Debug.LogWarning("[GameLoader] SERVER: SpawnManager timeout - creating new instance");
                GameObject spawnMgrObj = new GameObject("SpawnManager");
                spawnMgrObj.AddComponent<SpawnManager>();
                spawnManagerReady = true;
            }
        }

        UpdateProgress(0.5f, "Spawn system ready");
    }

    IEnumerator WaitForAudioManager()
    {
        UpdateProgress(0.6f, "Loading audio...");
        float timeout = Time.time + maxWaitTime;

        while (!audioManagerReady && Time.time < timeout)
        {
            if (AudioManager.Instance != null)
            {
                audioManagerReady = true;
                Debug.Log("[GameLoader] AudioManager ready");
            }
            yield return new WaitForSeconds(0.1f);
        }

        if (!audioManagerReady)
        {
            Debug.LogWarning("[GameLoader] AudioManager not found - continuing without audio");
            audioManagerReady = true;
        }

        UpdateProgress(0.7f, "Audio loaded");
    }

    IEnumerator WaitForUIManager()
    {
        UpdateProgress(0.8f, "Preparing interface...");
        float timeout = Time.time + maxWaitTime;

        while (!uiManagerReady && Time.time < timeout)
        {
            if (GameUIManager.Instance != null)
            {
                uiManagerReady = true;
                Debug.Log("[GameLoader] UIManager ready");
            }
            yield return new WaitForSeconds(0.1f);
        }

        if (!uiManagerReady)
        {
            Debug.LogWarning("[GameLoader] UIManager not found - continuing without UI");
            uiManagerReady = true;
        }

        UpdateProgress(0.85f, "Interface ready");
    }

    void UpdateProgress(float progress, string step)
    {
        loadingProgress = progress;
        currentLoadingStep = step;
        OnLoadingProgress?.Invoke(progress, step);
        Debug.Log($"[GameLoader] {(int)(progress * 100)}% - {step}");
    }

    /// <summary>
    /// Check if a specific system is ready
    /// </summary>
    public bool IsSystemReady(string systemName)
    {
        switch (systemName.ToLower())
        {
            case "mapmanager": return mapManagerReady;
            case "spawnmanager": return spawnManagerReady;
            case "audiomanager": return audioManagerReady;
            case "uimanager": return uiManagerReady;
            default: return false;
        }
    }

    /// <summary>
    /// Force complete loading (for testing or skip)
    /// </summary>
    public void ForceComplete()
    {
        StopAllCoroutines();
        isLoading = false;
        loadingProgress = 1f;
        OnLoadingComplete?.Invoke();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartGame();
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
