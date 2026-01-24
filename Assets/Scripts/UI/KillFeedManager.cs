using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

/// <summary>
/// Manages and displays kill feed notifications.
/// Shows who killed who, synced across all clients.
/// </summary>
public class KillFeedManager : MonoBehaviour
{
    public static KillFeedManager Instance { get; private set; }

    [Header("Settings")]
    public float displayDuration = 5f;
    public int maxEntries = 5;
    public float entryHeight = 25f;

    // Kill feed entries
    private List<KillFeedEntry> entries = new List<KillFeedEntry>();

    // Deduplication - prevent same entry type within short time
    private float lastAlienKillTime = -10f;
    private float lastAstronautKillTime = -10f;
    private const float DEDUPE_WINDOW = 0.5f; // 500ms window

    // UI textures
    private Texture2D bgTexture;
    private Texture2D killIconTexture;

    public struct KillFeedEntry
    {
        public string killerName;
        public string victimName;
        public KillType killType;
        public float timeAdded;

        public enum KillType
        {
            PlayerKilledAlien,
            AlienKilledPlayer,
            AlienKilledNPC,
            PlayerKilledNPC
        }
    }

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
        bgTexture.SetPixel(0, 0, new Color(0, 0, 0, 0.7f));
        bgTexture.Apply();

        killIconTexture = new Texture2D(1, 1);
        killIconTexture.SetPixel(0, 0, Color.red);
        killIconTexture.Apply();
    }

    /// <summary>
    /// Add a kill to the feed (call locally - each client manages their own display)
    /// </summary>
    public void AddKill(string killer, string victim, KillFeedEntry.KillType killType)
    {
        var entry = new KillFeedEntry
        {
            killerName = killer,
            victimName = victim,
            killType = killType,
            timeAdded = Time.time
        };

        entries.Insert(0, entry);

        // Limit entries
        while (entries.Count > maxEntries)
        {
            entries.RemoveAt(entries.Count - 1);
        }

        Debug.Log($"[KillFeed] {killer} killed {victim} ({killType})");
    }

    /// <summary>
    /// Add astronaut killed alien (with deduplication)
    /// </summary>
    public void AddAstronautKilledAlien()
    {
        // Dedupe - prevent duplicate entries within short window
        if (Time.time - lastAlienKillTime < DEDUPE_WINDOW)
        {
            Debug.Log("[KillFeed] Duplicate alien kill ignored (within dedupe window)");
            return;
        }
        lastAlienKillTime = Time.time;
        AddKill("ASTRONAUTE", "ALIEN", KillFeedEntry.KillType.PlayerKilledAlien);
    }

    /// <summary>
    /// Add alien killed astronaut (with deduplication)
    /// </summary>
    public void AddAlienKilledAstronaut()
    {
        // Dedupe - prevent duplicate entries within short window
        if (Time.time - lastAstronautKillTime < DEDUPE_WINDOW)
        {
            Debug.Log("[KillFeed] Duplicate astronaut kill ignored (within dedupe window)");
            return;
        }
        lastAstronautKillTime = Time.time;
        AddKill("ALIEN", "ASTRONAUTE", KillFeedEntry.KillType.AlienKilledPlayer);
    }

    /// <summary>
    /// Add astronaut killed NPC
    /// </summary>
    public void AddAstronautKilledNPC()
    {
        AddKill("ASTRONAUTE", "CIVIL", KillFeedEntry.KillType.PlayerKilledNPC);
    }

    /// <summary>
    /// Add alien ate NPC
    /// </summary>
    public void AddAlienAteNPC()
    {
        AddKill("ALIEN", "CIVIL", KillFeedEntry.KillType.AlienKilledNPC);
    }

    void Update()
    {
        // Remove old entries
        entries.RemoveAll(e => Time.time - e.timeAdded > displayDuration);
    }

    void OnGUI()
    {
        if (entries.Count == 0) return;

        // Don't show in main menu or when game ended
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentPhase == GameManager.GamePhase.Lobby ||
            GameManager.Instance.CurrentPhase == GameManager.GamePhase.Ended) return;

        // Position in top-right corner
        float boxWidth = 250;
        float x = Screen.width - boxWidth - 15;
        float y = 80; // Below any other UI

        // Draw each entry
        for (int i = 0; i < entries.Count; i++)
        {
            DrawEntry(entries[i], x, y + i * (entryHeight + 3));
        }
    }

    void DrawEntry(KillFeedEntry entry, float x, float y)
    {
        float width = 250;
        float height = entryHeight;

        // Calculate alpha based on age (fade out)
        float age = Time.time - entry.timeAdded;
        float alpha = 1f - Mathf.Clamp01((age - (displayDuration - 1f)) / 1f);

        // Background
        GUI.color = new Color(0, 0, 0, 0.7f * alpha);
        GUI.DrawTexture(new Rect(x, y, width, height), bgTexture);
        GUI.color = Color.white;

        // Get colors based on kill type
        Color killerColor = GetKillerColor(entry.killType);
        Color victimColor = GetVictimColor(entry.killType);

        // Draw killer name
        GUIStyle killerStyle = new GUIStyle(GUI.skin.label);
        killerStyle.fontSize = 14;
        killerStyle.fontStyle = FontStyle.Bold;
        killerStyle.normal.textColor = new Color(killerColor.r, killerColor.g, killerColor.b, alpha);

        float killerWidth = GUI.skin.label.CalcSize(new GUIContent(entry.killerName)).x + 10;
        GUI.Label(new Rect(x + 10, y + 3, killerWidth, height), entry.killerName, killerStyle);

        // Draw kill icon (simple X)
        GUIStyle iconStyle = new GUIStyle(GUI.skin.label);
        iconStyle.fontSize = 16;
        iconStyle.fontStyle = FontStyle.Bold;
        iconStyle.normal.textColor = new Color(1f, 0.2f, 0.2f, alpha);

        float iconX = x + 10 + killerWidth;
        GUI.Label(new Rect(iconX, y + 2, 20, height), " X ", iconStyle);

        // Draw victim name
        GUIStyle victimStyle = new GUIStyle(GUI.skin.label);
        victimStyle.fontSize = 14;
        victimStyle.fontStyle = FontStyle.Bold;
        victimStyle.normal.textColor = new Color(victimColor.r, victimColor.g, victimColor.b, alpha);

        GUI.Label(new Rect(iconX + 25, y + 3, width - killerWidth - 40, height), entry.victimName, victimStyle);
    }

    Color GetKillerColor(KillFeedEntry.KillType killType)
    {
        switch (killType)
        {
            case KillFeedEntry.KillType.PlayerKilledAlien:
            case KillFeedEntry.KillType.PlayerKilledNPC:
                return new Color(0.3f, 0.7f, 1f); // Blue for astronaut

            case KillFeedEntry.KillType.AlienKilledPlayer:
            case KillFeedEntry.KillType.AlienKilledNPC:
                return new Color(1f, 0.3f, 0.3f); // Red for alien

            default:
                return Color.white;
        }
    }

    Color GetVictimColor(KillFeedEntry.KillType killType)
    {
        switch (killType)
        {
            case KillFeedEntry.KillType.PlayerKilledAlien:
                return new Color(1f, 0.3f, 0.3f); // Red - alien died

            case KillFeedEntry.KillType.AlienKilledPlayer:
                return new Color(0.3f, 0.7f, 1f); // Blue - astronaut died

            case KillFeedEntry.KillType.PlayerKilledNPC:
            case KillFeedEntry.KillType.AlienKilledNPC:
                return Color.gray; // Gray for NPCs

            default:
                return Color.white;
        }
    }

    /// <summary>
    /// Clear all entries and reset deduplication timers
    /// </summary>
    public void ClearAll()
    {
        entries.Clear();
        lastAlienKillTime = -10f;
        lastAstronautKillTime = -10f;
        Debug.Log("[KillFeed] Cleared all entries");
    }
}
