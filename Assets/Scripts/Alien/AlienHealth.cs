using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Health system for the alien player.
/// Takes damage from astronaut shots.
/// When killed, reduces astronaut stress.
/// </summary>
public class AlienHealth : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    [Header("Stress Reduction on Kill")]
    public float stressReductionOnKill = 10f;

    [Header("Events")]
    public UnityEvent OnDeath;
    public UnityEvent<float> OnHealthChanged;
    public UnityEvent OnDamageTaken;

    private bool isDead = false;

    public float HealthPercent => currentHealth / maxHealth;
    public bool IsDead => isDead;

    void Start()
    {
        currentHealth = maxHealth;
        UpdateUI();

        Debug.Log("[AlienHealth] Initialized");
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(0, currentHealth);

        Debug.Log($"[AlienHealth] Took {amount} damage. Health: {currentHealth}/{maxHealth}");

        OnHealthChanged?.Invoke(currentHealth);
        OnDamageTaken?.Invoke();

        // Visual feedback
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBulletImpact("Flesh", transform.position);
        }

        // Blood effect
        CreateBloodEffect();

        UpdateUI();

        // Check death
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void CreateBloodEffect()
    {
        GameObject bloodObj = new GameObject("BloodEffect");
        bloodObj.transform.position = transform.position + Vector3.up;

        ParticleSystem ps = bloodObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.startLifetime = 0.5f;
        main.startSpeed = 3f;
        main.startSize = 0.15f;
        main.startColor = new Color(0.5f, 0f, 0f, 1f);
        main.maxParticles = 20;
        main.gravityModifier = 1f;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 15) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 30f;

        var renderer = bloodObj.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));

        ps.Play();
        Destroy(bloodObj, 2f);
    }

    void Die()
    {
        if (isDead) return;

        isDead = true;
        Debug.Log("[AlienHealth] Alien KILLED!");

        OnDeath?.Invoke();

        // Reduce astronaut stress
        if (StressSystem.Instance != null)
        {
            StressSystem.Instance.ReduceStress(stressReductionOnKill);
            Debug.Log($"[AlienHealth] Astronaut stress reduced by {stressReductionOnKill}");
        }

        // Notify game manager
        if (GameManager.Instance != null)
        {
            // Create a fake NPCBehavior-like notification
            // The alien counts as an alien kill
            GameManager.Instance.OnAlienKilled?.Invoke(GameManager.Instance.AliensRemaining - 1);
        }

        // Play death sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayNPCDeath(transform.position);
        }

        // Disable alien controls
        var controller = GetComponent<AlienController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        // Death visual - fall down
        StartCoroutine(DeathSequence());
    }

    System.Collections.IEnumerator DeathSequence()
    {
        // Simple death animation - shrink and fall
        float elapsed = 0f;
        float duration = 1f;
        Vector3 startScale = transform.localScale;
        Vector3 startPos = transform.position;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            transform.localScale = Vector3.Lerp(startScale, startScale * 0.5f, t);
            transform.position = startPos + Vector3.down * t * 0.5f;

            yield return null;
        }

        // Destroy after delay
        Destroy(gameObject, 3f);
    }

    void UpdateUI()
    {
        // Always update alien health UI (even when not controlled, for when we switch)
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.UpdateAlienHealthBar(currentHealth, maxHealth);
        }
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isDead = false;
        UpdateUI();
    }
}
