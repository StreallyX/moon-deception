using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Health system for the astronaut.
/// Takes damage from alien attacks.
/// </summary>
public class AstronautHealth : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    [Header("Regeneration")]
    public bool canRegenerate = false;
    public float regenRate = 5f;
    public float regenDelay = 5f;

    [Header("Events")]
    public UnityEvent OnDeath;
    public UnityEvent<float> OnHealthChanged;
    public UnityEvent OnDamageTaken;

    private float lastDamageTime;
    private bool isDead = false;

    public static AstronautHealth Instance { get; private set; }

    public float HealthPercent => currentHealth / maxHealth;
    public bool IsDead => isDead;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        currentHealth = maxHealth;
        UpdateUI();

        Debug.Log("[AstronautHealth] Initialized");
    }

    void Update()
    {
        if (isDead) return;

        // Regeneration
        if (canRegenerate && currentHealth < maxHealth)
        {
            if (Time.time - lastDamageTime > regenDelay)
            {
                Heal(regenRate * Time.deltaTime);
            }
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(0, currentHealth);
        lastDamageTime = Time.time;

        Debug.Log($"[AstronautHealth] Took {amount} damage. Health: {currentHealth}/{maxHealth}");

        OnHealthChanged?.Invoke(currentHealth);
        OnDamageTaken?.Invoke();

        // Visual/audio feedback
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBulletImpact("Flesh", transform.position);
        }

        if (CameraShake.Instance != null)
        {
            CameraShake.Instance.ShakeImpact();
        }

        if (PostProcessController.Instance != null)
        {
            PostProcessController.Instance.TriggerDamageEffect();
        }

        UpdateUI();

        // Check death
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (isDead) return;

        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);

        OnHealthChanged?.Invoke(currentHealth);
        UpdateUI();
    }

    void Die()
    {
        if (isDead) return;

        isDead = true;
        Debug.Log("[AstronautHealth] Astronaut DIED!");

        OnDeath?.Invoke();

        // Notify game manager (networked)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.EndGameNetworked(GameManager.WinCondition.AliensWin);
        }

        // Disable player controls
        var movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.enabled = false;
        }

        var shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
        {
            shooting.enabled = false;
        }
    }

    void UpdateUI()
    {
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.UpdateAstronautHealthBar(currentHealth, maxHealth);
        }
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isDead = false;
        UpdateUI();

        // Re-enable controls
        var movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.enabled = true;
        }

        var shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
        {
            shooting.enabled = true;
        }
    }

    // ==================== UI DISPLAY ====================

    void OnGUI()
    {
        // Health warning when low
        if (currentHealth < maxHealth * 0.3f && !isDead)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 20;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = new Color(1f, 0.2f, 0.2f, Mathf.PingPong(Time.time * 3f, 1f));

            GUI.Label(new Rect(Screen.width / 2 - 80, Screen.height - 80, 160, 30), "LOW HEALTH!", style);
        }
    }
}
