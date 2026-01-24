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

    [Header("Alien Eating Balance")]
    [SerializeField] private float alienEatWitnessRange = 20f; // Full stress if astronaut is within this range
    [SerializeField] private float alienEatStressNearby = 3f; // Stress when astronaut is nearby (within witnessRange)
    [SerializeField] private float alienEatStressFar = 1f; // Small stress even when astronaut is far (always added)
    [SerializeField] private float alienEatStressCooldown = 1f; // Short cooldown to prevent rapid spam
    private float lastAlienEatStressTime = -999f;

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
    public bool IsAstronautAlive => isAstronautAlive;
    public int TotalNPCs => totalNPCs;

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
        // GameLoader handles initialization and calls StartGame() when ready
        // In single player: GameLoader.BeginLoading() must be called (or use a Start Game button)
        // In multiplayer: NetworkSpawnManager handles game start after connection
        Debug.Log("[GameManager] Waiting for GameLoader or NetworkSpawnManager to start game...");
    }

    void OnDestroy()
    {
        // CRITICAL: Clear static instance to prevent stale references after scene reload
        if (Instance == this)
        {
            Instance = null;
            Debug.Log("[GameManager] Instance cleared on destroy");
        }

        // Unsubscribe from stress system events
        if (astronautStress != null)
        {
            astronautStress.OnStressMaxed.RemoveListener(TriggerChaosPhase);
        }
    }

    void SubscribeToStressSystem()
    {
        if (astronautStress == null)
        {
            astronautStress = FindFirstObjectByType<StressSystem>();
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
        Debug.Log("[GameManager] === STARTING NEW GAME ===");

        // Reset all state from previous game
        ResetGameState();

        currentPhase = GamePhase.Starting;
        gameTimer = 0f;
        innocentsKilled = 0;
        aliensRemaining = 0;

        // Clear blood decals from previous game
        if (BloodDecalManager.Instance != null)
        {
            BloodDecalManager.Instance.ClearAllDecals();
            Debug.Log("[GameManager] Cleared blood decals");
        }

        // Use SpawnManager to set up entities (aliens, defense zones, interactables)
        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.SpawnAllEntities();

            // Clear any NPCs that spawned too close to players
            SpawnManager.Instance.ClearNPCsNearAllPlayers(3f);
        }

        // Find all NPCs and aliens in scene
        activeNPCs.AddRange(FindObjectsByType<NPCBehavior>(FindObjectsSortMode.None));
        activeAliens.AddRange(FindObjectsByType<AlienController>(FindObjectsSortMode.None));

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
            astronautStress = FindFirstObjectByType<StressSystem>();
        }

        Debug.Log($"[GameManager] Game starting! NPCs: {activeNPCs.Count}, Aliens: {aliensRemaining}");

        currentPhase = GamePhase.Playing;
        OnGameStart?.Invoke();

        // Subscribe to stress events
        SubscribeToStressSystem();
    }

    /// <summary>
    /// Called when an NPC is killed
    /// </summary>
    public void OnNPCKilled(NPCBehavior npc)
    {
        // Use StressSystem.Instance as fallback (works when player spawns after game start)
        StressSystem stress = astronautStress ?? StressSystem.Instance;

        if (npc.IsAlien)
        {
            // Alien killed
            aliensRemaining--;
            OnAlienKilled?.Invoke(aliensRemaining);

            // Reduce astronaut stress
            if (stress != null)
            {
                stress.OnAlienKilled();
                Debug.Log($"[GameManager] Alien killed - stress reduced");
            }
            else
            {
                Debug.LogWarning("[GameManager] StressSystem not found - cannot reduce stress");
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
            if (stress != null)
            {
                stress.OnInnocentKilled();
                Debug.Log($"[GameManager] Innocent killed - stress increased");
            }
            else
            {
                Debug.LogWarning("[GameManager] StressSystem not found - cannot add stress");
            }

            Debug.Log($"[GameManager] Innocent killed! Total: {innocentsKilled}");
        }

        activeNPCs.Remove(npc);
    }

    /// <summary>
    /// Called when an alien EATS an NPC (not when astronaut kills one).
    /// Stress is only added if astronaut is nearby (can witness), and with a cooldown.
    /// </summary>
    public void OnNPCEatenByAlien(NPCBehavior npc, Vector3 eatPosition)
    {
        if (npc == null) return;

        // Count the kill but DON'T add stress like innocent kill
        if (npc.IsAlien)
        {
            // Alien ate another alien (rare case)
            aliensRemaining--;
            OnAlienKilled?.Invoke(aliensRemaining);
            Debug.Log($"[GameManager] Alien eaten by alien! {aliensRemaining} remaining.");

            if (aliensRemaining <= 0)
            {
                EndGame(WinCondition.AstronautWins);
            }
        }
        else
        {
            // Alien ate innocent - always add some stress, more if astronaut is nearby
            innocentsKilled++;
            OnInnocentKilled?.Invoke(innocentsKilled);

            StressSystem stress = astronautStress ?? StressSystem.Instance;
            if (stress != null && Time.time - lastAlienEatStressTime >= alienEatStressCooldown)
            {
                float distance = Vector3.Distance(stress.transform.position, eatPosition);
                float stressToAdd;

                if (distance <= alienEatWitnessRange)
                {
                    // Nearby - add more stress
                    stressToAdd = alienEatStressNearby;
                    Debug.Log($"[GameManager] Astronaut nearby ({distance:F1}m) - adding {stressToAdd} stress");
                }
                else
                {
                    // Far away - still add small amount (alien is killing people, tension rises)
                    stressToAdd = alienEatStressFar;
                    Debug.Log($"[GameManager] Astronaut far ({distance:F1}m) - adding {stressToAdd} stress");
                }

                stress.AddStress(stressToAdd);
                lastAlienEatStressTime = Time.time;
            }
            else if (stress == null)
            {
                Debug.LogWarning("[GameManager] StressSystem not found when alien ate NPC");
            }
            else
            {
                Debug.Log("[GameManager] Alien ate NPC but stress on cooldown");
            }

            Debug.Log($"[GameManager] NPC eaten by alien. Total innocents killed: {innocentsKilled}");
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
        Debug.Log("[GameManager] ALARM TRIGGERED!");

        // Play alarm sound (networked)
        if (NetworkAudioManager.Instance != null)
        {
            NetworkAudioManager.Instance.PlayAlarm();
        }
        else if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayAlarm();
        }

        // Make all NPCs panic!
        foreach (var npc in activeNPCs)
        {
            if (npc != null && !npc.IsDead)
            {
                npc.Panic();
            }
        }
    }

    /// <summary>
    /// Turn off station lights
    /// </summary>
    private void TurnOffLights()
    {
        Debug.Log("[GameManager] Lights out!");

        // Use ChaosLightingController if available
        if (ChaosLightingController.Instance != null)
        {
            ChaosLightingController.Instance.StartChaosLighting();
        }
        else
        {
            // Fallback - manually turn off lights
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var light in lights)
            {
                if (light.type != LightType.Directional) // Keep directional for minimal visibility
                {
                    light.intensity *= 0.1f;
                }
            }
            Debug.Log($"[GameManager] Dimmed {lights.Length} lights");
        }
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

        // Hide all gameplay UI first (stress bar, hunger bar, etc.)
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.OnGameEnded();
        }

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
    /// End game - networked version (call from server to sync to all clients)
    /// </summary>
    public void EndGameNetworked(WinCondition winner)
    {
        if (currentPhase == GamePhase.Ended) return;

        // Use networked end game if available
        if (NetworkAudioManager.Instance != null)
        {
            bool alienWins = winner == WinCondition.AliensWin;
            NetworkAudioManager.Instance.EndGame(alienWins);
        }
        else
        {
            EndGame(winner);
        }
    }

    /// <summary>
    /// Reset game for new round
    /// </summary>
    public void ResetGame()
    {
        ResetGameState();
    }

    /// <summary>
    /// Reset all game state for a new game. Called at the start of each game.
    /// </summary>
    private void ResetGameState()
    {
        Debug.Log("[GameManager] Resetting all game state...");

        currentPhase = GamePhase.Lobby;
        gameTimer = 0f;
        innocentsKilled = 0;
        aliensRemaining = 0;
        isAstronautAlive = true;
        lastAlienEatStressTime = -999f;

        activeAliens.Clear();
        activeNPCs.Clear();

        // Reset stress system
        if (astronautStress != null)
        {
            astronautStress.ResetStress();
        }
        else
        {
            // Try to find it
            var stress = FindFirstObjectByType<StressSystem>();
            if (stress != null)
            {
                stress.ResetStress();
            }
        }

        // Clear blood decals
        if (BloodDecalManager.Instance != null)
        {
            BloodDecalManager.Instance.ClearAllDecals();
        }

        // Reset chaos lighting if active
        if (ChaosLightingController.Instance != null)
        {
            ChaosLightingController.Instance.ResetLighting();
        }

        Debug.Log("[GameManager] Game state reset complete");
    }
}
