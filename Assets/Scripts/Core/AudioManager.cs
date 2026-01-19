using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Central audio management system.
/// Handles all game sounds: SFX, ambient, UI.
/// Uses object pooling for efficient sound playback.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource ambientSource;
    [SerializeField] private AudioSource uiSource;

    [Header("SFX Pool")]
    [SerializeField] private int sfxPoolSize = 10;
    private List<AudioSource> sfxPool = new List<AudioSource>();
    private int currentSfxIndex = 0;

    [Header("Volume Settings")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    [Range(0f, 1f)] public float ambientVolume = 0.7f;
    [Range(0f, 1f)] public float uiVolume = 1f;

    [Header("Gun Sounds")]
    public AudioClip gunShot;
    public AudioClip gunShotMinigun;
    public AudioClip gunEmpty;
    public AudioClip gunReload;

    [Header("Impact Sounds")]
    public AudioClip bulletImpactFlesh;
    public AudioClip bulletImpactMetal;
    public AudioClip bulletImpactConcrete;

    [Header("Footstep Sounds")]
    public AudioClip[] footstepsMetal;
    public AudioClip[] footstepsConcrete;

    [Header("Ambient Sounds")]
    public AudioClip ambientLoop;
    public AudioClip ambientChaos;
    public AudioClip alarmSound;
    public AudioClip heartbeatLoop;

    [Header("UI Sounds")]
    public AudioClip uiHover;
    public AudioClip uiClick;
    public AudioClip uiBack;

    [Header("NPC Sounds")]
    public AudioClip npcDeath;
    public AudioClip npcPanic;

    [Header("Alien Sounds")]
    public AudioClip alienReveal;
    public AudioClip alienGrowl;
    public AudioClip alienAttack;
    public AudioClip alienKilled;

    [Header("Game Event Sounds")]
    public AudioClip victoryStinger;
    public AudioClip defeatStinger;
    public AudioClip powerDown;
    public AudioClip lightsEmergency;

    [Header("Interactable Sounds")]
    public AudioClip coffeeMachineSound;
    public AudioClip alarmTrigger;
    public AudioClip terminalBeep;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeAudioSources();
        CreateSFXPool();
    }

    void Start()
    {
        LoadVolumeSettings();
        LoadAudioFromResources();

        // Preload all audio clips to avoid decompression stutter during gameplay
        PreloadAllAudioClips();

        if (ambientLoop != null)
        {
            PlayAmbient(ambientLoop);
        }

        Debug.Log("[AudioManager] Initialized");
    }

    /// <summary>
    /// Force Unity to decompress all audio clips now, avoiding stutters later
    /// </summary>
    void PreloadAllAudioClips()
    {
        AudioClip[] allClips = new AudioClip[]
        {
            gunShot, gunShotMinigun, gunEmpty, gunReload,
            bulletImpactFlesh, bulletImpactMetal, bulletImpactConcrete,
            ambientLoop, ambientChaos, alarmSound, heartbeatLoop,
            uiHover, uiClick, uiBack,
            npcDeath, npcPanic,
            alienReveal, alienGrowl, alienAttack, alienKilled,
            victoryStinger, defeatStinger, powerDown, lightsEmergency,
            coffeeMachineSound, alarmTrigger, terminalBeep
        };

        int preloadedCount = 0;
        foreach (var clip in allClips)
        {
            if (clip != null)
            {
                // Force load the audio data into memory
                clip.LoadAudioData();
                preloadedCount++;
            }
        }

        // Also preload footstep arrays
        if (footstepsMetal != null)
        {
            foreach (var clip in footstepsMetal)
            {
                if (clip != null) clip.LoadAudioData();
            }
        }
        if (footstepsConcrete != null)
        {
            foreach (var clip in footstepsConcrete)
            {
                if (clip != null) clip.LoadAudioData();
            }
        }

        Debug.Log($"[AudioManager] Preloaded {preloadedCount} audio clips to prevent runtime stutter");

        // Force-play critical chaos sounds silently to fully initialize them
        StartCoroutine(WarmUpCriticalSounds());
    }

    /// <summary>
    /// Play critical sounds at zero volume to force Unity to fully initialize them
    /// This prevents the first-play stutter
    /// </summary>
    System.Collections.IEnumerator WarmUpCriticalSounds()
    {
        yield return new WaitForSeconds(0.5f); // Wait for audio system to be ready

        float originalVolume = masterVolume;
        masterVolume = 0f; // Mute

        // Play each critical sound briefly
        AudioClip[] criticalClips = { powerDown, ambientChaos, alarmSound, lightsEmergency, alienReveal };

        foreach (var clip in criticalClips)
        {
            if (clip != null)
            {
                AudioSource source = GetNextSFXSource();
                source.clip = clip;
                source.volume = 0f;
                source.Play();
                yield return new WaitForSeconds(0.05f);
                source.Stop();
            }
        }

        masterVolume = originalVolume; // Restore volume
        Debug.Log("[AudioManager] Critical sounds warmed up - no stutter on first play");
    }

    /// <summary>
    /// Try to load audio clips from Resources/Audio folder if not assigned
    /// </summary>
    void LoadAudioFromResources()
    {
        int loadedCount = 0;

        // Gun sounds
        if (gunShot == null)
            gunShot = Resources.Load<AudioClip>("Audio/gunshot");
        if (gunShot != null) loadedCount++;

        if (gunShotMinigun == null)
            gunShotMinigun = Resources.Load<AudioClip>("Audio/minigun");
        if (gunShotMinigun != null) loadedCount++;

        if (gunReload == null)
            gunReload = Resources.Load<AudioClip>("Audio/reload");
        if (gunReload != null) loadedCount++;

        // Impact sounds
        if (bulletImpactMetal == null)
            bulletImpactMetal = Resources.Load<AudioClip>("Audio/impact_metal");
        if (bulletImpactMetal != null) loadedCount++;

        if (bulletImpactConcrete == null)
            bulletImpactConcrete = Resources.Load<AudioClip>("Audio/impact_concrete");
        if (bulletImpactConcrete != null) loadedCount++;

        // Use impact_metal as fallback for flesh if not available
        if (bulletImpactFlesh == null)
            bulletImpactFlesh = Resources.Load<AudioClip>("Audio/impact_flesh") ?? bulletImpactMetal;

        // Ambient sounds
        if (ambientLoop == null)
            ambientLoop = Resources.Load<AudioClip>("Audio/ambient_station");
        if (ambientLoop != null) loadedCount++;

        if (ambientChaos == null)
            ambientChaos = Resources.Load<AudioClip>("Audio/ambient_chaos");
        if (ambientChaos != null) loadedCount++;

        if (alarmSound == null)
            alarmSound = Resources.Load<AudioClip>("Audio/alarm");
        if (alarmSound != null) loadedCount++;

        if (heartbeatLoop == null)
            heartbeatLoop = Resources.Load<AudioClip>("Audio/heartbeat");
        if (heartbeatLoop != null) loadedCount++;

        // NPC sounds
        if (npcDeath == null)
            npcDeath = Resources.Load<AudioClip>("Audio/npc_death");
        if (npcDeath != null) loadedCount++;

        if (npcPanic == null)
            npcPanic = Resources.Load<AudioClip>("Audio/npc_panic");
        if (npcPanic != null) loadedCount++;

        // Alien sounds
        if (alienReveal == null)
            alienReveal = Resources.Load<AudioClip>("Audio/alien_reveal");
        if (alienReveal != null) loadedCount++;

        if (alienGrowl == null)
            alienGrowl = Resources.Load<AudioClip>("Audio/alien_growl");
        if (alienGrowl != null) loadedCount++;

        if (alienAttack == null)
            alienAttack = Resources.Load<AudioClip>("Audio/alien_attack");
        if (alienAttack != null) loadedCount++;

        if (alienKilled == null)
            alienKilled = Resources.Load<AudioClip>("Audio/alien_killed");
        if (alienKilled != null) loadedCount++;

        // Game event sounds
        if (victoryStinger == null)
            victoryStinger = Resources.Load<AudioClip>("Audio/victory_stinger");
        if (victoryStinger != null) loadedCount++;

        if (defeatStinger == null)
            defeatStinger = Resources.Load<AudioClip>("Audio/defeat_stinger");
        if (defeatStinger != null) loadedCount++;

        if (powerDown == null)
            powerDown = Resources.Load<AudioClip>("Audio/power_down");
        if (powerDown != null) loadedCount++;

        if (lightsEmergency == null)
            lightsEmergency = Resources.Load<AudioClip>("Audio/lights_emergency");
        if (lightsEmergency != null) loadedCount++;

        // Interactable sounds
        if (coffeeMachineSound == null)
            coffeeMachineSound = Resources.Load<AudioClip>("Audio/coffee_machine");
        if (coffeeMachineSound != null) loadedCount++;

        if (alarmTrigger == null)
            alarmTrigger = Resources.Load<AudioClip>("Audio/alarm_trigger");
        if (alarmTrigger != null) loadedCount++;

        if (terminalBeep == null)
            terminalBeep = Resources.Load<AudioClip>("Audio/terminal_beep");
        if (terminalBeep != null) loadedCount++;

        Debug.Log($"[AudioManager] Loaded {loadedCount}/22 audio clips from Resources/Audio/");
    }

    void InitializeAudioSources()
    {
        if (musicSource == null)
        {
            GameObject musicObj = new GameObject("MusicSource");
            musicObj.transform.SetParent(transform);
            musicSource = musicObj.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }

        if (ambientSource == null)
        {
            GameObject ambientObj = new GameObject("AmbientSource");
            ambientObj.transform.SetParent(transform);
            ambientSource = ambientObj.AddComponent<AudioSource>();
            ambientSource.loop = true;
            ambientSource.playOnAwake = false;
        }

        if (uiSource == null)
        {
            GameObject uiObj = new GameObject("UISource");
            uiObj.transform.SetParent(transform);
            uiSource = uiObj.AddComponent<AudioSource>();
            uiSource.playOnAwake = false;
        }
    }

    void CreateSFXPool()
    {
        GameObject poolContainer = new GameObject("SFXPool");
        poolContainer.transform.SetParent(transform);

        for (int i = 0; i < sfxPoolSize; i++)
        {
            GameObject sfxObj = new GameObject($"SFX_{i}");
            sfxObj.transform.SetParent(poolContainer.transform);
            AudioSource source = sfxObj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D by default
            sfxPool.Add(source);
        }
    }

    AudioSource GetNextSFXSource()
    {
        AudioSource source = sfxPool[currentSfxIndex];
        currentSfxIndex = (currentSfxIndex + 1) % sfxPool.Count;
        return source;
    }

    // ==================== PUBLIC METHODS ====================

    /// <summary>
    /// Play a 2D sound effect (UI, HUD sounds)
    /// </summary>
    public void PlaySFX(AudioClip clip, float volumeMultiplier = 1f)
    {
        if (clip == null) return;

        AudioSource source = GetNextSFXSource();
        source.spatialBlend = 0f;
        source.clip = clip;
        source.volume = sfxVolume * masterVolume * volumeMultiplier;
        source.pitch = 1f;
        source.Play();
    }

    /// <summary>
    /// Play a 2D sound with random pitch variation
    /// </summary>
    public void PlaySFXWithPitch(AudioClip clip, float minPitch = 0.95f, float maxPitch = 1.05f, float volumeMultiplier = 1f)
    {
        if (clip == null) return;

        AudioSource source = GetNextSFXSource();
        source.spatialBlend = 0f;
        source.clip = clip;
        source.volume = sfxVolume * masterVolume * volumeMultiplier;
        source.pitch = Random.Range(minPitch, maxPitch);
        source.Play();
    }

    /// <summary>
    /// Play a 3D positional sound effect
    /// </summary>
    public void PlaySFX3D(AudioClip clip, Vector3 position, float volumeMultiplier = 1f, float minDistance = 1f, float maxDistance = 30f)
    {
        if (clip == null) return;

        AudioSource source = GetNextSFXSource();
        source.transform.position = position;
        source.spatialBlend = 1f;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.clip = clip;
        source.volume = sfxVolume * masterVolume * volumeMultiplier;
        source.pitch = Random.Range(0.95f, 1.05f);
        source.Play();
    }

    /// <summary>
    /// Play a random clip from an array
    /// </summary>
    public void PlayRandomSFX(AudioClip[] clips, float volumeMultiplier = 1f)
    {
        if (clips == null || clips.Length == 0) return;
        PlaySFXWithPitch(clips[Random.Range(0, clips.Length)], 0.9f, 1.1f, volumeMultiplier);
    }

    /// <summary>
    /// Play a random 3D sound from array
    /// </summary>
    public void PlayRandomSFX3D(AudioClip[] clips, Vector3 position, float volumeMultiplier = 1f)
    {
        if (clips == null || clips.Length == 0) return;
        PlaySFX3D(clips[Random.Range(0, clips.Length)], position, volumeMultiplier);
    }

    // ==================== SPECIFIC SOUND METHODS ====================

    public void PlayGunshot(bool isMinigun = false)
    {
        AudioClip clip = isMinigun && gunShotMinigun != null ? gunShotMinigun : gunShot;
        if (clip == null)
        {
            Debug.LogWarning("[AudioManager] gunShot clip is NOT assigned!");
            return;
        }
        PlaySFXWithPitch(clip, 0.95f, 1.05f, 1f);
    }

    /// <summary>
    /// Play gunshot with 3D spatial audio - so aliens can hear direction
    /// </summary>
    public void PlayGunshot3D(Vector3 position, bool isMinigun = false)
    {
        AudioClip clip = isMinigun && gunShotMinigun != null ? gunShotMinigun : gunShot;
        if (clip == null)
        {
            Debug.LogWarning("[AudioManager] gunShot clip is NOT assigned!");
            return;
        }
        // Gunshots are loud - heard from far away (100m)
        PlaySFX3D(clip, position, 1f, 5f, 100f);
    }

    public void PlayGunEmpty()
    {
        PlaySFX(gunEmpty);
    }

    public void PlayReload()
    {
        PlaySFX(gunReload);
    }

    public void PlayBulletImpact(string surfaceType, Vector3 position)
    {
        AudioClip clip = surfaceType switch
        {
            "Flesh" => bulletImpactFlesh,
            "Metal" => bulletImpactMetal,
            _ => bulletImpactConcrete
        };
        PlaySFX3D(clip, position, 0.8f);
    }

    public void PlayFootstep(string surfaceType = "Metal")
    {
        AudioClip[] clips = surfaceType switch
        {
            "Concrete" => footstepsConcrete,
            _ => footstepsMetal
        };
        PlayRandomSFX(clips, 0.5f);
    }

    public void PlayUIHover()
    {
        if (uiSource != null && uiHover != null)
        {
            uiSource.PlayOneShot(uiHover, uiVolume * masterVolume * 0.5f);
        }
    }

    public void PlayUIClick()
    {
        if (uiSource != null && uiClick != null)
        {
            uiSource.PlayOneShot(uiClick, uiVolume * masterVolume);
        }
    }

    public void PlayUIBack()
    {
        if (uiSource != null && uiBack != null)
        {
            uiSource.PlayOneShot(uiBack, uiVolume * masterVolume);
        }
    }

    // ==================== AMBIENT & MUSIC ====================

    public void PlayAmbient(AudioClip clip)
    {
        if (ambientSource == null || clip == null) return;
        ambientSource.clip = clip;
        ambientSource.volume = ambientVolume * masterVolume;
        ambientSource.Play();
    }

    public void PlayMusic(AudioClip clip)
    {
        if (musicSource == null || clip == null) return;
        musicSource.clip = clip;
        musicSource.volume = musicVolume * masterVolume;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    public void PlayAlarm()
    {
        if (alarmSound != null)
        {
            PlaySFX(alarmSound);
        }
    }

    public void StartHeartbeat()
    {
        if (heartbeatLoop != null && ambientSource != null)
        {
            // Layer heartbeat with ambient
            PlaySFX(heartbeatLoop, 0.6f);
        }
    }

    // ==================== NPC SOUNDS ====================

    public void PlayNPCDeath(Vector3 position)
    {
        PlaySFX3D(npcDeath, position);
    }

    public void PlayNPCPanic(Vector3 position)
    {
        PlaySFX3D(npcPanic, position, 0.8f);
    }

    // ==================== ALIEN SOUNDS ====================

    public void PlayAlienReveal()
    {
        PlaySFX(alienReveal, 1.2f);
    }

    public void PlayAlienGrowl(Vector3 position)
    {
        PlaySFX3D(alienGrowl, position, 0.7f);
    }

    public void PlayAlienAttack()
    {
        PlaySFX(alienAttack, 1f);
    }

    public void PlayAlienKilled()
    {
        PlaySFX(alienKilled, 1f);
    }

    // ==================== INTERACTABLE SOUNDS ====================

    public void PlayCoffeeMachine(Vector3 position)
    {
        PlaySFX3D(coffeeMachineSound, position, 0.8f);
    }

    public void PlayAlarmTrigger(Vector3 position)
    {
        PlaySFX3D(alarmTrigger, position, 1f, 5f, 50f);
    }

    public void PlayTerminalBeep(Vector3 position)
    {
        PlaySFX3D(terminalBeep, position, 0.6f);
    }

    // ==================== GAME EVENTS ====================

    public void PlayVictory()
    {
        StopMusic();
        PlaySFX(victoryStinger);
    }

    public void PlayDefeat()
    {
        StopMusic();
        PlaySFX(defeatStinger);
    }

    public void PlayPowerDown()
    {
        PlaySFX(powerDown, 1f);
    }

    public void PlayLightsEmergency()
    {
        PlaySFX(lightsEmergency, 0.8f);
    }

    // ==================== CHAOS PHASE ====================

    public void StartChaosAmbient()
    {
        if (ambientChaos != null)
        {
            PlayAmbient(ambientChaos);
        }
    }

    public void StartNormalAmbient()
    {
        if (ambientLoop != null)
        {
            PlayAmbient(ambientLoop);
        }
    }

    // ==================== VOLUME SETTINGS ====================

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        UpdateAllVolumes();
        SaveVolumeSettings();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        SaveVolumeSettings();
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (musicSource != null)
        {
            musicSource.volume = musicVolume * masterVolume;
        }
        SaveVolumeSettings();
    }

    public void SetAmbientVolume(float volume)
    {
        ambientVolume = Mathf.Clamp01(volume);
        if (ambientSource != null)
        {
            ambientSource.volume = ambientVolume * masterVolume;
        }
        SaveVolumeSettings();
    }

    void UpdateAllVolumes()
    {
        if (musicSource != null)
            musicSource.volume = musicVolume * masterVolume;
        if (ambientSource != null)
            ambientSource.volume = ambientVolume * masterVolume;
    }

    void SaveVolumeSettings()
    {
        PlayerPrefs.SetFloat("MasterVolume", masterVolume);
        PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
        PlayerPrefs.SetFloat("MusicVolume", musicVolume);
        PlayerPrefs.SetFloat("AmbientVolume", ambientVolume);
        PlayerPrefs.Save();
    }

    void LoadVolumeSettings()
    {
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
        ambientVolume = PlayerPrefs.GetFloat("AmbientVolume", 0.7f);
        UpdateAllVolumes();
    }
}
