using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

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

        // Visual feedback (networked audio)
        if (NetworkAudioManager.Instance != null)
        {
            NetworkAudioManager.Instance.PlayBulletImpact("Flesh", transform.position);
        }
        else if (AudioManager.Instance != null)
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
        // Use fallback shader chain for build compatibility
        Shader shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            renderer.material = new Material(shader);
        }

        ps.Play();
        Destroy(bloodObj, 2f);
    }

    void Die()
    {
        if (isDead) return;

        isDead = true;
        Debug.Log("[AlienHealth] Alien KILLED!");

        OnDeath?.Invoke();

        // Play death sound (networked)
        if (NetworkAudioManager.Instance != null)
        {
            NetworkAudioManager.Instance.PlayAlienKilled(transform.position);
        }
        else if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayAlienKilled();
        }

        // NETWORK: Notify server about death via RPC
        // Server will handle stress reduction, win condition check, and broadcast
        var networkedPlayer = GetComponent<NetworkedPlayer>();
        bool isNetworked = networkedPlayer != null &&
                           Unity.Netcode.NetworkManager.Singleton != null &&
                           Unity.Netcode.NetworkManager.Singleton.IsConnectedClient;

        if (isNetworked)
        {
            // Use the networked death RPC
            ulong clientId = networkedPlayer.OwnerClientId;
            networkedPlayer.AlienDiedServerRpc(clientId);
            Debug.Log($"[AlienHealth] Sent AlienDiedServerRpc for client {clientId}");
        }
        else
        {
            // Single player fallback - do local logic
            if (StressSystem.Instance != null)
            {
                StressSystem.Instance.ReduceStress(stressReductionOnKill);
            }

            // Count remaining alive aliens locally
            int aliveAliens = 0;
            AlienHealth[] allAliens = FindObjectsByType<AlienHealth>(FindObjectsSortMode.None);
            foreach (var alien in allAliens)
            {
                if (!alien.IsDead)
                {
                    aliveAliens++;
                }
            }

            if (aliveAliens == 0 && GameManager.Instance != null)
            {
                GameManager.Instance.EndGame(GameManager.WinCondition.AstronautWins);
            }
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

        // NETWORK: Don't destroy networked players - server handles despawn
        // Just disable visuals and let spectator mode work
        var networkObject = GetComponent<Unity.Netcode.NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned)
        {
            // Disable renderers to hide the dead player
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = false;
            }
            Debug.Log("[AlienHealth] Networked player - hiding visuals instead of destroying");
            // Server will handle cleanup when game ends
        }
        else
        {
            // Single player - safe to destroy
            Destroy(gameObject, 3f);
        }
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
