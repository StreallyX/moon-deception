using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple loading screen UI that shows progress during game initialization.
/// Uses OnGUI for quick prototyping - can be replaced with proper UI later.
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool useOnGUI = true; // Quick prototype mode
    [SerializeField] private Color backgroundColor = new Color(0.05f, 0.05f, 0.1f, 1f);
    [SerializeField] private Color progressBarColor = new Color(0.2f, 0.6f, 1f, 1f);
    [SerializeField] private Color textColor = Color.white;

    [Header("Optional UI References")]
    [SerializeField] private Canvas loadingCanvas;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Text progressText;
    [SerializeField] private Text stepText;

    private bool isVisible = false; // Start hidden
    private float displayProgress = 0f;
    private string displayStep = "Initializing...";
    private float fadeOutTimer = 0f;
    private bool isFadingOut = false;

    void Start()
    {
        // Subscribe to GameLoader events
        StartCoroutine(WaitForGameLoader());
    }

    System.Collections.IEnumerator WaitForGameLoader()
    {
        // Wait for GameLoader to exist
        while (GameLoader.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }

        // Wait a frame to ensure Awake has run
        yield return null;

        // Subscribe to events (with null checks)
        if (GameLoader.Instance.OnLoadingStart != null)
            GameLoader.Instance.OnLoadingStart.AddListener(OnLoadingStart);
        if (GameLoader.Instance.OnLoadingProgress != null)
            GameLoader.Instance.OnLoadingProgress.AddListener(OnProgressUpdate);
        if (GameLoader.Instance.OnLoadingComplete != null)
            GameLoader.Instance.OnLoadingComplete.AddListener(OnLoadingComplete);
    }

    void OnLoadingStart()
    {
        isVisible = true;
        isFadingOut = false;
        displayProgress = 0f;
        displayStep = "Initializing...";
    }

    void OnProgressUpdate(float progress, string step)
    {
        displayProgress = progress;
        displayStep = step;

        // Update UI elements if assigned
        if (progressSlider != null)
        {
            progressSlider.value = progress;
        }
        if (progressText != null)
        {
            progressText.text = $"{(int)(progress * 100)}%";
        }
        if (stepText != null)
        {
            stepText.text = step;
        }
    }

    void OnLoadingComplete()
    {
        isFadingOut = true;
        fadeOutTimer = 0.5f; // Fade out over 0.5 seconds
    }

    void Update()
    {
        // Smooth progress interpolation
        if (GameLoader.Instance != null && GameLoader.Instance.IsLoading)
        {
            displayProgress = Mathf.Lerp(displayProgress, GameLoader.Instance.LoadingProgress, Time.deltaTime * 5f);
        }

        // Handle fade out
        if (isFadingOut)
        {
            fadeOutTimer -= Time.deltaTime;
            if (fadeOutTimer <= 0f)
            {
                isVisible = false;
                if (loadingCanvas != null)
                {
                    loadingCanvas.gameObject.SetActive(false);
                }
            }
        }
    }

    void OnGUI()
    {
        if (!useOnGUI || !isVisible) return;

        // Calculate alpha for fade
        float alpha = isFadingOut ? Mathf.Clamp01(fadeOutTimer / 0.5f) : 1f;

        // Background
        Color bgColor = backgroundColor;
        bgColor.a *= alpha;
        GUI.color = bgColor;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        // Reset color
        GUI.color = Color.white;

        // Title
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 48,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        titleStyle.normal.textColor = new Color(textColor.r, textColor.g, textColor.b, alpha);

        float centerY = Screen.height * 0.4f;
        GUI.Label(new Rect(0, centerY - 60, Screen.width, 60), "MOON DECEPTION", titleStyle);

        // Progress bar background
        float barWidth = Screen.width * 0.5f;
        float barHeight = 20f;
        float barX = (Screen.width - barWidth) / 2f;
        float barY = centerY + 20f;

        Color barBgColor = new Color(0.2f, 0.2f, 0.2f, alpha);
        GUI.color = barBgColor;
        GUI.DrawTexture(new Rect(barX, barY, barWidth, barHeight), Texture2D.whiteTexture);

        // Progress bar fill
        Color barFillColor = progressBarColor;
        barFillColor.a = alpha;
        GUI.color = barFillColor;
        GUI.DrawTexture(new Rect(barX, barY, barWidth * displayProgress, barHeight), Texture2D.whiteTexture);

        // Progress percentage
        GUIStyle percentStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            alignment = TextAnchor.MiddleCenter
        };
        percentStyle.normal.textColor = new Color(textColor.r, textColor.g, textColor.b, alpha);

        GUI.Label(new Rect(0, barY + 30, Screen.width, 30), $"{(int)(displayProgress * 100)}%", percentStyle);

        // Current step
        GUIStyle stepStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleCenter
        };
        stepStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f, alpha);

        GUI.Label(new Rect(0, barY + 65, Screen.width, 25), displayStep, stepStyle);

        // Loading dots animation
        string dots = new string('.', (int)(Time.time * 2) % 4);
        if (!isFadingOut)
        {
            GUI.Label(new Rect(0, barY + 95, Screen.width, 25), $"Loading{dots}", stepStyle);
        }

        GUI.color = Color.white;
    }

    void OnDestroy()
    {
        if (GameLoader.Instance != null)
        {
            GameLoader.Instance.OnLoadingStart.RemoveListener(OnLoadingStart);
            GameLoader.Instance.OnLoadingProgress.RemoveListener(OnProgressUpdate);
            GameLoader.Instance.OnLoadingComplete.RemoveListener(OnLoadingComplete);
        }
    }
}
