using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Network audio synchronization manager.
/// Broadcasts sounds to all clients so everyone hears game events.
/// Use this instead of AudioManager directly for sounds that should be heard by all players.
///
/// This is a local singleton that finds a NetworkedPlayer to send RPCs through.
/// </summary>
public class NetworkAudioManager : MonoBehaviour
{
    public static NetworkAudioManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ==================== HELPER ====================

    private bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
    private bool IsClient => NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient;

    /// <summary>
    /// Check if we should use network sync or play locally.
    /// </summary>
    private bool ShouldSync()
    {
        return NetworkManager.Singleton != null &&
               (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient);
    }

    /// <summary>
    /// Find ANY NetworkedPlayer to send RPCs through.
    /// We use RequireOwnership=false on RPCs, so any player can send.
    /// </summary>
    private NetworkedPlayer FindAnyNetworkedPlayer()
    {
        if (NetworkManager.Singleton == null) return null;

        // Find any NetworkedPlayer that is spawned
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
            {
                var networkedPlayer = client.PlayerObject.GetComponent<NetworkedPlayer>();
                if (networkedPlayer != null && networkedPlayer.IsSpawned)
                {
                    return networkedPlayer;
                }
            }
        }

        // Fallback: find any NetworkedPlayer in scene
        return FindObjectOfType<NetworkedPlayer>();
    }

    // ==================== GUNSHOTS ====================

    /// <summary>
    /// Play gunshot - heard by all players
    /// </summary>
    public void PlayGunshot(Vector3 position, bool isMinigun = false)
    {
        if (!ShouldSync())
        {
            // Single player - play directly
            AudioManager.Instance?.PlayGunshot3D(position, isMinigun);
            return;
        }

        var player = FindAnyNetworkedPlayer();
        if (player != null)
        {
            player.PlayGunshotServerRpc(position, isMinigun);
        }
        else
        {
            // Fallback: play locally
            AudioManager.Instance?.PlayGunshot3D(position, isMinigun);
        }
    }

    // ==================== RELOAD ====================

    public void PlayReload(Vector3 position)
    {
        if (!ShouldSync())
        {
            AudioManager.Instance?.PlayReload();
            return;
        }

        var player = FindAnyNetworkedPlayer();
        if (player != null)
        {
            player.PlayReloadServerRpc(position);
        }
        else
        {
            AudioManager.Instance?.PlayReload();
        }
    }

    // ==================== BULLET IMPACTS ====================

    public void PlayBulletImpact(string surfaceType, Vector3 position)
    {
        if (!ShouldSync())
        {
            AudioManager.Instance?.PlayBulletImpact(surfaceType, position);
            return;
        }

        var player = FindAnyNetworkedPlayer();
        if (player != null)
        {
            player.PlayBulletImpactServerRpc(surfaceType, position);
        }
        else
        {
            AudioManager.Instance?.PlayBulletImpact(surfaceType, position);
        }
    }

    // ==================== NPC SOUNDS ====================

    public void PlayNPCDeath(Vector3 position)
    {
        if (!ShouldSync())
        {
            AudioManager.Instance?.PlayNPCDeath(position);
            return;
        }

        var player = FindAnyNetworkedPlayer();
        if (player != null)
        {
            player.PlayNPCDeathServerRpc(position);
        }
        else
        {
            AudioManager.Instance?.PlayNPCDeath(position);
        }
    }

    public void PlayNPCPanic(Vector3 position)
    {
        if (!ShouldSync())
        {
            AudioManager.Instance?.PlayNPCPanic(position);
            return;
        }

        var player = FindAnyNetworkedPlayer();
        if (player != null)
        {
            player.PlayNPCPanicServerRpc(position);
        }
        else
        {
            AudioManager.Instance?.PlayNPCPanic(position);
        }
    }

    // ==================== ALIEN SOUNDS ====================

    public void PlayAlienReveal()
    {
        if (!ShouldSync())
        {
            AudioManager.Instance?.PlayAlienReveal();
            return;
        }

        var player = FindAnyNetworkedPlayer();
        if (player != null)
        {
            player.PlayAlienRevealServerRpc();
        }
        else
        {
            AudioManager.Instance?.PlayAlienReveal();
        }
    }

    public void PlayAlienGrowl(Vector3 position)
    {
        if (!ShouldSync())
        {
            AudioManager.Instance?.PlayAlienGrowl(position);
            return;
        }

        var player = FindAnyNetworkedPlayer();
        if (player != null)
        {
            player.PlayAlienGrowlServerRpc(position);
        }
        else
        {
            AudioManager.Instance?.PlayAlienGrowl(position);
        }
    }

    public void PlayAlienAttack(Vector3 position)
    {
        if (!ShouldSync())
        {
            AudioManager.Instance?.PlayAlienAttack();
            return;
        }

        var player = FindAnyNetworkedPlayer();
        if (player != null)
        {
            player.PlayAlienAttackServerRpc(position);
        }
        else
        {
            AudioManager.Instance?.PlayAlienAttack();
        }
    }

    public void PlayAlienKilled(Vector3 position)
    {
        if (!ShouldSync())
        {
            AudioManager.Instance?.PlayAlienKilled();
            return;
        }

        var player = FindAnyNetworkedPlayer();
        if (player != null)
        {
            player.PlayAlienKilledServerRpc(position);
        }
        else
        {
            AudioManager.Instance?.PlayAlienKilled();
        }
    }

    // ==================== INTERACTABLE SOUNDS ====================

    public void PlayCoffeeMachine(Vector3 position)
    {
        if (!ShouldSync())
        {
            AudioManager.Instance?.PlayCoffeeMachine(position);
            return;
        }

        var player = FindAnyNetworkedPlayer();
        if (player != null)
        {
            player.PlayCoffeeMachineServerRpc(position);
        }
        else
        {
            AudioManager.Instance?.PlayCoffeeMachine(position);
        }
    }

    public void PlayAlarmTrigger(Vector3 position)
    {
        if (!ShouldSync())
        {
            AudioManager.Instance?.PlayAlarmTrigger(position);
            return;
        }

        var player = FindAnyNetworkedPlayer();
        if (player != null)
        {
            player.PlayAlarmTriggerServerRpc(position);
        }
        else
        {
            AudioManager.Instance?.PlayAlarmTrigger(position);
        }
    }

    public void PlayTerminalBeep(Vector3 position)
    {
        if (!ShouldSync())
        {
            AudioManager.Instance?.PlayTerminalBeep(position);
            return;
        }

        var player = FindAnyNetworkedPlayer();
        if (player != null)
        {
            player.PlayTerminalBeepServerRpc(position);
        }
        else
        {
            AudioManager.Instance?.PlayTerminalBeep(position);
        }
    }

    // ==================== GAME EVENTS ====================

    public void PlayPowerDown()
    {
        if (!ShouldSync())
        {
            AudioManager.Instance?.PlayPowerDown();
            return;
        }

        var player = FindAnyNetworkedPlayer();
        if (player != null)
        {
            player.PlayPowerDownServerRpc();
        }
        else
        {
            AudioManager.Instance?.PlayPowerDown();
        }
    }

    public void PlayLightsEmergency()
    {
        if (!ShouldSync())
        {
            AudioManager.Instance?.PlayLightsEmergency();
            return;
        }

        var player = FindAnyNetworkedPlayer();
        if (player != null)
        {
            player.PlayLightsEmergencyServerRpc();
        }
        else
        {
            AudioManager.Instance?.PlayLightsEmergency();
        }
    }

    /// <summary>
    /// Victory sound - server broadcasts to all
    /// </summary>
    public void PlayVictory()
    {
        if (!ShouldSync())
        {
            AudioManager.Instance?.PlayVictory();
            return;
        }

        // Victory is server-only broadcast
        if (IsServer)
        {
            var player = FindAnyNetworkedPlayer();
            if (player != null)
            {
                player.BroadcastVictory();
            }
        }
    }

    /// <summary>
    /// Defeat sound - server broadcasts to all
    /// </summary>
    public void PlayDefeat()
    {
        if (!ShouldSync())
        {
            AudioManager.Instance?.PlayDefeat();
            return;
        }

        // Defeat is server-only broadcast
        if (IsServer)
        {
            var player = FindAnyNetworkedPlayer();
            if (player != null)
            {
                player.BroadcastDefeat();
            }
        }
    }

    // ==================== CHAOS PHASE ====================

    public void StartChaosAmbient()
    {
        if (!ShouldSync())
        {
            AudioManager.Instance?.StartChaosAmbient();
            return;
        }

        // Chaos ambient is server-only broadcast
        if (IsServer)
        {
            var player = FindAnyNetworkedPlayer();
            if (player != null)
            {
                player.BroadcastStartChaosAmbient();
            }
        }
    }

    public void StartNormalAmbient()
    {
        if (!ShouldSync())
        {
            AudioManager.Instance?.StartNormalAmbient();
            return;
        }

        // Normal ambient is server-only broadcast
        if (IsServer)
        {
            var player = FindAnyNetworkedPlayer();
            if (player != null)
            {
                player.BroadcastStartNormalAmbient();
            }
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
