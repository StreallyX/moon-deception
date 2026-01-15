using UnityEngine;

/// <summary>
/// Bootstrap script that initializes all game systems.
/// Add this to an empty GameObject in your scene.
/// Creates all necessary managers if they don't exist.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    [Header("Auto-Create Managers")]
    public bool createAudioManager = true;
    public bool createMenuManager = true;
    public bool createPostProcessController = true;

    [Header("Player Setup")]
    public bool addCameraShakeToPlayer = true;

    private static bool hasInitialized = false;

    void Awake()
    {
        if (hasInitialized)
        {
            Destroy(gameObject);
            return;
        }
        hasInitialized = true;

        DontDestroyOnLoad(gameObject);
        InitializeSystems();
    }

    void InitializeSystems()
    {
        Debug.Log("[GameBootstrap] Initializing game systems...");

        // Audio Manager
        if (createAudioManager && AudioManager.Instance == null)
        {
            GameObject audioManagerObj = new GameObject("AudioManager");
            audioManagerObj.AddComponent<AudioManager>();
            DontDestroyOnLoad(audioManagerObj);
            Debug.Log("[GameBootstrap] Created AudioManager");
        }

        // Menu Manager
        if (createMenuManager && MenuManager.Instance == null)
        {
            GameObject menuManagerObj = new GameObject("MenuManager");
            menuManagerObj.AddComponent<MenuManager>();
            DontDestroyOnLoad(menuManagerObj);
            Debug.Log("[GameBootstrap] Created MenuManager");
        }

        // Post Process Controller
        if (createPostProcessController && PostProcessController.Instance == null)
        {
            GameObject postProcessObj = new GameObject("PostProcessController");
            postProcessObj.AddComponent<PostProcessController>();
            DontDestroyOnLoad(postProcessObj);
            Debug.Log("[GameBootstrap] Created PostProcessController");
        }

        // Add CameraShake to player camera
        if (addCameraShakeToPlayer)
        {
            StartCoroutine(SetupCameraShake());
        }

        Debug.Log("[GameBootstrap] All systems initialized");
    }

    System.Collections.IEnumerator SetupCameraShake()
    {
        // Wait a frame for player to be set up
        yield return null;

        // Find player camera and add CameraShake
        PlayerMovement player = FindObjectOfType<PlayerMovement>();
        if (player != null)
        {
            Camera playerCam = player.GetCamera();
            if (playerCam == null)
            {
                playerCam = player.GetComponentInChildren<Camera>();
            }

            if (playerCam != null && playerCam.GetComponent<CameraShake>() == null)
            {
                playerCam.gameObject.AddComponent<CameraShake>();
                Debug.Log("[GameBootstrap] Added CameraShake to player camera");
            }
        }

        // Also check for alien camera
        AlienController alien = FindObjectOfType<AlienController>();
        if (alien != null)
        {
            Camera alienCam = alien.GetCamera();
            if (alienCam != null && alienCam.GetComponent<CameraShake>() == null)
            {
                alienCam.gameObject.AddComponent<CameraShake>();
                Debug.Log("[GameBootstrap] Added CameraShake to alien camera");
            }
        }
    }

    void OnDestroy()
    {
        if (hasInitialized)
        {
            hasInitialized = false;
        }
    }
}
