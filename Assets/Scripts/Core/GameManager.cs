using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

/// <summary>
/// Central game manager handling game state, phase transitions, and win conditions.
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum GamePhase
    {
        Lobby,      // Waiting for players
        Starting,   // Countdown before game starts
        Playing,    // Main gameplay
        Chaos,      // Stress maxed, aliens transformed
        Ended       // Game over
    }

    public enum WinCondition
    {
        None,
        AstronautWins,  // All aliens eliminated
        AliensWin       // Astronaut killed or timer expired
    }

    [Header("Game Settings")]
    [SerializeField] private float gameDuration = 600f; // 10 minutes
    [SerializeField] private int maxAliens = 5;
    [SerializeField] private int totalNPCs = 30;

    [Header("References")]
    [SerializeField] private Transform astronautSpawn;
    [SerializeField] private Transform[] alienSpawns;
    [SerializeField] private Transform[] npcSpawns;
    [SerializeField] private StressSystem astronautStress;

    [Header("State")]
    [SerializeField] private GamePhase currentPhase = GamePhase.Lobby;
    [SerializeField] private float gameTimer;
    [SerializeField] private int aliensRemaining;
    [SerializeField] private int innocentsKilled;

    [Header("Events")]
    public UnityEvent OnGameStart;
    public UnityEvent OnChaosPhase;
    public UnityEvent<WinCondition> OnGameEnd;
    public UnityEvent<int> OnAlienKilled;
    public UnityEvent<int> OnInnocentKilled;

    // Tracking
    private List<AlienController> activeAliens = new List<AlienController>();
    private List<NPCBehavior> activeNPCs = new List<NPCBehavior>();
    private bool isAstronautAlive = true;

    public GamePhase CurrentPhase => currentPhase;
    public float TimeRemaining => gameDuration - gameTimer;
    public int AliensRemaining => aliensRemaining;
    public int InnocentsKilled => innocentsKilled;

    public static GameManager Instance { get; private set; }

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        InitializeUIManager();
    }

    void InitializeUIManager()
    {
        if (GameUIManager.Instance == null)
        {
            GameObject uiManagerObj = new GameObject("GameUIManager");
            uiManagerObj.AddComponent<GameUIManager>();
            DontDestroyOnLoad(uiManagerObj);
            Debug.Log("[GameManager] GameUIManager created");
        }
    }

    void Start()
    {
        // Auto-start for testing (remove in multiplayer)
        StartGame();

        // Subscribe to stress events AFTER StartGame finds the stress system
        SubscribeToStressSystem();
    }

    void SubscribeToStressSystem()
    {
        if (astronautStress == null)
        {
            astronautStress = FindObjectOfType<StressSystem>();
        }

        if (astronautStress != null)
        {
            astronautStress.OnStressMaxed.AddListener(TriggerChaosPhase);
            Debug.Log("[GameManager] Subscribed to StressSystem.OnStressMaxed");
        }
        else
        {
            Debug.LogWarning("[GameManager] Could not find StressSystem to subscribe!");
        }
    }

    void Update()
    {
        if (currentPhase == GamePhase.Playing || currentPhase == GamePhase.Chaos)
        {
            UpdateGameTimer();
        }
    }

    /// <summary>
    /// Update game timer and check time-based win condition
    /// </summary>
    private void UpdateGameTimer()
    {
        gameTimer += Time.deltaTime;

        if (gameTimer >= gameDuration)
        {
            EndGame(WinCondition.AliensWin);
        }
    }

    /// <summary>
    /// Initialize and start the game
    /// </summary>
    public void StartGame()
    {
        currentPhase = GamePhase.Starting;
        gameTimer = 0f;
        innocentsKilled = 0;
        aliensRemaining = 0;

        // Use SpawnManager to set up entities (aliens, defense zones, interactables)
        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.SpawnAllEntities();
        }

        // Find all NPCs and aliens in scene
        activeNPCs.AddRange(FindObjectsOfType<NPCBehavior>());
        activeAliens.AddRange(FindObjectsOfType<AlienController>());

        // Count aliens assigned to NPCs
        int alienNPCCount = 0;
        foreach (var npc in activeNPCs)
        {
            if (npc != null && npc.IsAlien)
            {
                alienNPCCount++;
            }
        }
        aliensRemaining = activeAliens.Count + alienNPCCount;

        // Find astronaut stress system if not assigned
        if (astronautStress == null)
        {
            astronautStress = FindObjectOfType<StressSystem>();
        }

        Debug.Log($"[GameManager] Game starting! NPCs: {activeNPCs.Count}, Aliens: {aliensRemaining}");

        currentPhase = GamePhase.Playing;
        OnGameStart?.Invoke();
    }

    /// <summary>
    /// Called when an NPC is killed
    /// </summary>
    public void OnNPCKilled(NPCBehavior npc)
    {
        if (npc.IsAlien)
        {
            // Alien killed
            aliensRemaining--;
            OnAlienKilled?.Invoke(aliensRemaining);

            // Reduce astronaut stress
            if (astronautStress != null)
            {
                astronautStress.OnAlienKilled();
            }

            Debug.Log($"[GameManager] Alien eliminated! {aliensRemaining} remaining.");

            // Check win condition
            if (aliensRemaining <= 0)
            {
                EndGame(WinCondition.AstronautWins);
            }
        }
        else
        {
            // Innocent killed
            innocentsKilled++;
            OnInnocentKilled?.Invoke(innocentsKilled);

            // Increase astronaut stress
            if (astronautStress != null)
            {
                astronautStress.OnInnocentKilled();
            }

            Debug.Log($"[GameManager] Innocent killed! Total: {innocentsKilled}");
        }

        activeNPCs.Remove(npc);
    }

    /// <summary>
    /// Called when astronaut is killed
    /// </summary>
    public void OnAstronautKilled()
    {
        isAstronautAlive = false;
        Debug.Log("[GameManager] Astronaut killed!");
        EndGame(WinCondition.AliensWin);
    }

    /// <summary>
    /// Called when a chaos event is triggered by an alien
    /// </summary>
    public void OnChaosEventTriggered(Vector3 position)
    {
        Debug.Log($"[GameManager] Chaos event at {position}");

        // Stress nearby astronaut
        if (astronautStress != null)
        {
            float distance = Vector3.Distance(astronautStress.transform.position, position);
            if (distance < 20f) // Within hearing range
            {
                astronautStress.OnChaosEvent();
            }
        }

        // Panic nearby NPCs
        foreach (var npc in activeNPCs)
        {
            if (npc != null)
            {
                float dist = Vector3.Distance(npc.transform.position, position);
                if (dist < 10f)
                {
                    npc.Panic();
                }
            }
        }
    }

    /// <summary>
    /// Trigger chaos phase when stress maxes out
    /// </summary>
    public void TriggerChaosPhase()
    {
        if (currentPhase != GamePhase.Playing)
        {
            Debug.Log($"[GameManager] TriggerChaosPhase called but phase is {currentPhase}, not Playing");
            return;
        }

        currentPhase = GamePhase.Chaos;
        Debug.Log("[GameManager] ========== CHAOS PHASE ACTIVATED! ==========");

        // Transform all aliens
        Debug.Log($"[GameManager] Transforming {activeAliens.Count} aliens...");
        foreach (var alien in activeAliens)
        {
            if (alien != null)
            {
                alien.Transform();
            }
        }

        // Trigger environmental effects
        TriggerAlarm();
        TurnOffLights();

        // Invoke chaos event for all subscribers
        Debug.Log($"[GameManager] Invoking OnChaosPhase event (listeners count: {OnChaosPhase?.GetPersistentEventCount()})");
        OnChaosPhase?.Invoke();
    }

    /// <summary>
    /// Trigger station alarm
    /// </summary>
    private void TriggerAlarm()
    {
        // TODO: Play alarm sound
        // TODO: Flash warning lights
        Debug.Log("[GameManager] ALARM TRIGGERED!");
    }

    /// <summary>
    /// Turn off station lights
    /// </summary>
    private void TurnOffLights()
    {
        // TODO: Disable main lights, enable emergency lighting
        Debug.Log("[GameManager] Lights out!");
    }

    /// <summary>
    /// End the game with specified win condition
    /// </summary>
    public void EndGame(WinCondition winner)
    {
        if (currentPhase == GamePhase.Ended) return;

        currentPhase = GamePhase.Ended;
        Debug.Log($"[GameManager] Game Over! Winner: {winner}");

        OnGameEnd?.Invoke(winner);

        // Show game over screen
        if (MenuManager.Instance != null)
        {
            bool victory = winner == WinCondition.AstronautWins;
            int aliensKilled = maxAliens - aliensRemaining;
            MenuManager.Instance.ShowGameOver(victory, aliensKilled, innocentsKilled, gameTimer);
        }
        else
        {
            // Fallback if no menu manager
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    /// <summary>
    /// Reset game for new round
    /// </summary>
    public void ResetGame()
    {
        currentPhase = GamePhase.Lobby;
        gameTimer = 0f;
        innocentsKilled = 0;
        aliensRemaining = 0;
        isAstronautAlive = true;
        activeAliens.Clear();
        activeNPCs.Clear();

        if (astronautStress != null)
        {
            astronautStress.ResetStress();
        }
    }
}
