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
    public AudioClip alarmSound;
    public AudioClip heartbeatLoop;

    [Header("UI Sounds")]
    public AudioClip uiHover;
    public AudioClip uiClick;
    public AudioClip uiBack;

    [Header("Game Event Sounds")]
    public AudioClip npcDeath;
    public AudioClip alienReveal;
    public AudioClip victoryStinger;
    public AudioClip defeatStinger;

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

        if (ambientLoop != null)
        {
            PlayAmbient(ambientLoop);
        }

        Debug.Log("[AudioManager] Initialized");
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

    public void PlayGunshot()
    {
        PlaySFXWithPitch(gunShot, 0.95f, 1.05f, 1f);
    }

    public void PlayGunEmpty()
    {
        PlaySFX(gunEmpty);
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

    // ==================== GAME EVENTS ====================

    public void PlayNPCDeath(Vector3 position)
    {
        PlaySFX3D(npcDeath, position);
    }

    public void PlayAlienReveal()
    {
        PlaySFX(alienReveal, 1.2f);
    }

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
