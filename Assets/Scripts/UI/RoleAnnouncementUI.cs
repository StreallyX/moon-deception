using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Shows "YOU ARE THE ASTRONAUT" or "YOU ARE AN ALIEN" at game start.
/// Auto-creates UI if not set up.
/// </summary>
public class RoleAnnouncementUI : MonoBehaviour
{
    public static RoleAnnouncementUI Instance { get; private set; }

    [Header("UI References (auto-created if null)")]
    public GameObject announcementPanel;
    public TextMeshProUGUI roleText;
    public TextMeshProUGUI subtitleText;
    public Image backgroundImage;

    [Header("Colors")]
    public Color astronautColor = new Color(0.2f, 0.5f, 1f); // Blue
    public Color alienColor = new Color(0.6f, 0.1f, 0.6f);   // Purple

    [Header("Settings")]
    public float displayDuration = 4f;
    public float fadeInDuration = 0.5f;
    public float fadeOutDuration = 0.5f;

    private Canvas canvas;
    private CanvasGroup canvasGroup;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        SetupUI();
    }

    void Start()
    {
        // Subscribe to NetworkGameManager for role announcements ONLY
        // Game end UI is handled by MenuManager to avoid duplicate screens
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.OnLocalRoleAssigned += ShowRole;
        }
        else
        {
            StartCoroutine(LateSubscribe());
        }

        // Hide initially
        if (announcementPanel != null)
            announcementPanel.SetActive(false);
    }

    IEnumerator LateSubscribe()
    {
        yield return new WaitForSeconds(0.5f);
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.OnLocalRoleAssigned += ShowRole;
        }
    }

    void OnDestroy()
    {
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.OnLocalRoleAssigned -= ShowRole;
        }
    }

    void SetupUI()
    {
        // Find or create canvas
        canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("RoleAnnouncementCanvas");
            canvasObj.transform.SetParent(transform);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // On top of everything
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Create announcement panel if needed
        if (announcementPanel == null)
        {
            announcementPanel = new GameObject("AnnouncementPanel");
            announcementPanel.transform.SetParent(canvas.transform);

            RectTransform rect = announcementPanel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            backgroundImage = announcementPanel.AddComponent<Image>();
            backgroundImage.color = new Color(0, 0, 0, 0.85f);

            canvasGroup = announcementPanel.AddComponent<CanvasGroup>();
        }
        else
        {
            canvasGroup = announcementPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = announcementPanel.AddComponent<CanvasGroup>();
        }

        // Create role text
        if (roleText == null)
        {
            GameObject roleObj = new GameObject("RoleText");
            roleObj.transform.SetParent(announcementPanel.transform);

            RectTransform roleRect = roleObj.AddComponent<RectTransform>();
            roleRect.anchorMin = new Vector2(0.5f, 0.55f);
            roleRect.anchorMax = new Vector2(0.5f, 0.55f);
            roleRect.sizeDelta = new Vector2(800, 120);
            roleRect.anchoredPosition = Vector2.zero;

            roleText = roleObj.AddComponent<TextMeshProUGUI>();
            roleText.fontSize = 72;
            roleText.fontStyle = FontStyles.Bold;
            roleText.alignment = TextAlignmentOptions.Center;
            roleText.color = Color.white;
        }

        // Create subtitle text
        if (subtitleText == null)
        {
            GameObject subObj = new GameObject("SubtitleText");
            subObj.transform.SetParent(announcementPanel.transform);

            RectTransform subRect = subObj.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 0.42f);
            subRect.anchorMax = new Vector2(0.5f, 0.42f);
            subRect.sizeDelta = new Vector2(600, 60);
            subRect.anchoredPosition = Vector2.zero;

            subtitleText = subObj.AddComponent<TextMeshProUGUI>();
            subtitleText.fontSize = 28;
            subtitleText.alignment = TextAlignmentOptions.Center;
            subtitleText.color = new Color(0.8f, 0.8f, 0.8f);
        }
    }

    // ==================== PUBLIC METHODS ====================

    public void ShowRole(NetworkGameManager.PlayerRole role)
    {
        if (role == NetworkGameManager.PlayerRole.None) return;

        bool isAstronaut = (role == NetworkGameManager.PlayerRole.Astronaut);
        ShowRole(isAstronaut);
    }

    public void ShowRole(bool isAstronaut)
    {
        string roleTitle = isAstronaut ? "ASTRONAUT" : "ALIEN";
        string subtitle = isAstronaut
            ? "Find and eliminate the aliens among the crew"
            : "Blend in with the NPCs. Survive until chaos.";
        Color color = isAstronaut ? astronautColor : alienColor;

        StartCoroutine(ShowAnnouncement($"YOU ARE THE\n{roleTitle}", subtitle, color));
    }

    public void ShowGameEnd(bool astronautWins)
    {
        string title = astronautWins ? "ASTRONAUT WINS!" : "ALIENS WIN!";
        string subtitle = astronautWins
            ? "All aliens have been eliminated"
            : "The aliens have survived";
        Color color = astronautWins ? astronautColor : alienColor;

        StartCoroutine(ShowAnnouncement(title, subtitle + "\n\nReturning to lobby...", color));
    }

    public void ShowCustomMessage(string title, string subtitle, Color color)
    {
        StartCoroutine(ShowAnnouncement(title, subtitle, color));
    }

    // ==================== ANIMATION ====================

    IEnumerator ShowAnnouncement(string title, string subtitle, Color accentColor)
    {
        if (announcementPanel == null) yield break;

        // Set content
        if (roleText != null)
        {
            roleText.text = title;
            roleText.color = accentColor;
        }

        if (subtitleText != null)
        {
            subtitleText.text = subtitle;
        }

        // Show panel
        announcementPanel.SetActive(true);

        // Fade in
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = elapsed / fadeInDuration;
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        // Play sound
        if (AudioManager.Instance != null)
        {
            // Could play a dramatic sound here
        }

        // Wait
        yield return new WaitForSeconds(displayDuration);

        // Fade out
        if (canvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = 1f - (elapsed / fadeOutDuration);
                yield return null;
            }
            canvasGroup.alpha = 0f;
        }

        // Hide panel
        announcementPanel.SetActive(false);
    }
}
