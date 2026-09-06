using System;
using UnityEngine;

/// <summary>
/// Central playback for DroneStrike. Imported clips are preferred; the small
/// procedural sounds below only preserve harmless legacy feedback if an asset
/// was accidentally left out of an overlay install.
/// </summary>
public class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }

    const int WorldVoiceCount = 12;
    const int UiVoiceCount = 4;
    const string MasterVolumeKey = "DroneStrike.Audio.Master";

    static float savedMasterVolume = -1f;
    static bool platformMuted;

    sealed class Voice
    {
        public AudioSource source;
        public int priority;
        public float startedAt;
    }

    AudioClip[] metalImpacts;
    AudioClip[] groundImpacts;
    AudioClip[] hardImpacts;
    AudioClip[] compactExplosions;
    AudioClip[] standardExplosions;
    AudioClip[] heavyExplosions;

    AudioClip uiFocus;
    AudioClip uiConfirm;
    AudioClip uiBack;
    AudioClip uiUnavailable;
    AudioClip signalLost;
    AudioClip targetDestroyed;

    // Graceful fallbacks. They should never be selected in a complete install.
    AudioClip fallbackImpact;
    AudioClip fallbackExplosion;
    AudioClip rotorLoop;

    Voice[] worldVoices;
    Voice[] uiVoices;
    System.Random random = new System.Random(60227);
    int lastMetal = -1;
    int lastGround = -1;
    int lastHard = -1;
    int lastCompact = -1;
    int lastStandard = -1;
    int lastHeavy = -1;

    public static float MasterVolume
    {
        get
        {
            if (savedMasterVolume < 0f)
                savedMasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
            return savedMasterVolume;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("GameAudio");
        Instance = go.AddComponent<GameAudio>();
        DontDestroyOnLoad(go);
    }

    /// <summary>Used by settings UI when it is added. Value is always 0..1.</summary>
    public static void SetMasterVolume(float value)
    {
        savedMasterVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MasterVolumeKey, savedMasterVolume);
        PlayerPrefs.Save();
        ApplyListenerGain();
    }

    /// <summary>
    /// Ads and background tabs silence the game without overwriting the user's
    /// saved volume. This is deliberately separate from pause state.
    /// </summary>
    public static void SetPlatformMuted(bool muted)
    {
        platformMuted = muted;
        ApplyListenerGain();
    }

    static void ApplyListenerGain()
    {
        AudioListener.volume = platformMuted ? 0f : MasterVolume;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadClips();
        CreateVoices();
        ApplyListenerGain();
    }

    void LoadClips()
    {
        metalImpacts = Resources.LoadAll<AudioClip>("Audio/Impacts/Metal");
        groundImpacts = Resources.LoadAll<AudioClip>("Audio/Impacts/Ground");
        hardImpacts = Resources.LoadAll<AudioClip>("Audio/Impacts/Hard");

        // Resources.LoadAll cannot filter by prefix. Keep each charge in a
        // stable three-item array so a heavy charge never selects compact audio.
        compactExplosions = LoadNamed("Audio/Explosions/explosion_compact_01",
            "Audio/Explosions/explosion_compact_02", "Audio/Explosions/explosion_compact_03");
        standardExplosions = LoadNamed("Audio/Explosions/explosion_standard_01",
            "Audio/Explosions/explosion_standard_02", "Audio/Explosions/explosion_standard_03");
        heavyExplosions = LoadNamed("Audio/Explosions/explosion_heavy_01",
            "Audio/Explosions/explosion_heavy_02", "Audio/Explosions/explosion_heavy_03");

        uiFocus = Resources.Load<AudioClip>("Audio/UI/ui_focus");
        uiConfirm = Resources.Load<AudioClip>("Audio/UI/ui_confirm");
        uiBack = Resources.Load<AudioClip>("Audio/UI/ui_back");
        uiUnavailable = Resources.Load<AudioClip>("Audio/UI/ui_unavailable");
        signalLost = Resources.Load<AudioClip>("Audio/UI/signal_lost");
        targetDestroyed = Resources.Load<AudioClip>("Audio/UI/target_destroyed");

        fallbackImpact = ProceduralAudio.CreateImpact("FallbackImpact", 0.16f, 1750f, 1006);
        fallbackExplosion = ProceduralAudio.CreateExplosion("FallbackExplosion", 1.1f, 1012);
        // Keep the softer original rotor character. The imported five-layer
        // version sounded sharp and tiring during a full mission.
        rotorLoop = ProceduralAudio.CreateRotorLoop("RotorLoop", 0.25f,
            new[] { 280f, 292f, 304f }, 1011);
    }

    AudioClip[] LoadNamed(params string[] paths)
    {
        var clips = new AudioClip[paths.Length];
        bool hasAny = false;
        for (int i = 0; i < paths.Length; i++)
        {
            clips[i] = Resources.Load<AudioClip>(paths[i]);
            hasAny |= clips[i] != null;
        }
        return hasAny ? clips : Array.Empty<AudioClip>();
    }

    void CreateVoices()
    {
        worldVoices = CreateVoicePool("WorldVoice", WorldVoiceCount, true);
        uiVoices = CreateVoicePool("UiVoice", UiVoiceCount, false);
    }

    Voice[] CreateVoicePool(string name, int count, bool spatial)
    {
        var pool = new Voice[count];
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject(name + i);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatial ? 1f : 0f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = spatial ? 4f : 1f;
            source.maxDistance = spatial ? 90f : 1f;
            pool[i] = new Voice { source = source, priority = 99 };
        }
        return pool;
    }

    // ---------- public gameplay API ----------

    public void PlayHardImpact(Vector3 position)
    {
        PlayWorld(metalImpacts, ref lastMetal, position, 0.54f, 36, 3f, 62f, fallbackImpact);
    }

    public void PlayGroundImpact(Vector3 position)
    {
        PlayWorld(groundImpacts, ref lastGround, position, 0.50f, 38, 4f, 55f, fallbackImpact);
    }

    public void PlayStructureImpact(Vector3 position)
    {
        PlayWorld(hardImpacts, ref lastHard, position, 0.50f, 38, 4f, 65f, fallbackImpact);
    }

    public void PlayExplosion(Vector3 position, WarheadType type)
    {
        switch (type)
        {
            case WarheadType.Heavy:
                PlayWorld(heavyExplosions, ref lastHeavy, position, 0.90f, 4, 8f, 160f, fallbackExplosion);
                break;
            case WarheadType.Standard:
                PlayWorld(standardExplosions, ref lastStandard, position, 0.82f, 7, 7f, 135f, fallbackExplosion);
                break;
            default:
                PlayWorld(compactExplosions, ref lastCompact, position, 0.72f, 11, 6f, 105f, fallbackExplosion);
                break;
        }
    }

    // Compatibility for older call sites. New gameplay must pass the charge.
    public void PlayExplosion(Vector3 position) { PlayExplosion(position, WarheadType.Standard); }

    public void PlayUiFocus() { PlayUi(uiFocus, 0.22f, 50); }
    public void PlayUiConfirm() { PlayUi(uiConfirm, 0.32f, 18); }
    public void PlayUiBack() { PlayUi(uiBack, 0.24f, 40); }
    public void PlayUiUnavailable() { PlayUi(uiUnavailable, 0.27f, 28); }
    public void PlaySignalLost() { PlayUi(signalLost, 0.34f, 10); }
    public void PlayTargetDestroyed() { PlayUi(targetDestroyed, 0.20f, 25); }

    public AudioSource AttachDroneLoop(Transform parent)
    {
        var go = new GameObject("RotorLoop");
        go.transform.SetParent(parent, false);

        var source = go.AddComponent<AudioSource>();
        source.clip = rotorLoop;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.volume = 0f;
        source.Play();
        return source;
    }

    // Legacy calls remain available to avoid silently breaking prototype code.
    public void PlayPlayerShot(Vector3 position) { PlayHardImpact(position); }
    public void PlayBotShot(Vector3 position) { PlayHardImpact(position); }
    public void PlayEmptyClick(Vector3 position) { PlayUiUnavailable(); }
    public void PlayMagOut(Vector3 position) { PlayHardImpact(position); }
    public void PlayMagIn(Vector3 position) { PlayHardImpact(position); }
    public void PlayFleshImpact(Vector3 position) { PlayHardImpact(position); }
    public void PlayFootstep(Vector3 position) { PlayGroundImpact(position); }
    public void PlayRespawn(Vector3 position) { PlayUiConfirm(); }
    public void PlayBodyFall(Vector3 position) { PlayStructureImpact(position); }
    public void PlayHitmarker(bool headshot)
    {
        PlayUi(headshot ? uiConfirm : uiFocus, headshot ? 0.30f : 0.22f, 24);
    }

    void PlayWorld(AudioClip[] clips, ref int lastIndex, Vector3 position, float volume,
                   int priority, float minDistance, float maxDistance, AudioClip fallback)
    {
        AudioClip clip = Select(clips, ref lastIndex) ?? fallback;
        if (clip == null) return;
        Voice voice = TakeVoice(worldVoices, priority);
        if (voice == null) return;

        var source = voice.source;
        source.Stop();
        source.transform.position = position;
        source.clip = clip;
        source.volume = volume;
        source.pitch = 1f + (float)(random.NextDouble() * 0.06 - 0.03);
        source.spatialBlend = 1f;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.priority = priority;
        source.Play();
        voice.priority = priority;
        voice.startedAt = Time.unscaledTime;
    }

    void PlayUi(AudioClip clip, float volume, int priority)
    {
        if (clip == null) return;
        Voice voice = TakeVoice(uiVoices, priority);
        if (voice == null) return;

        var source = voice.source;
        source.Stop();
        source.clip = clip;
        source.volume = volume;
        source.pitch = 1f;
        source.spatialBlend = 0f;
        source.priority = priority;
        source.Play();
        voice.priority = priority;
        voice.startedAt = Time.unscaledTime;
    }

    AudioClip Select(AudioClip[] clips, ref int lastIndex)
    {
        if (clips == null || clips.Length == 0) return null;
        int usable = 0;
        for (int i = 0; i < clips.Length; i++) if (clips[i] != null) usable++;
        if (usable == 0) return null;

        int index;
        if (usable == 1)
        {
            index = Array.FindIndex(clips, clip => clip != null);
        }
        else
        {
            do { index = random.Next(clips.Length); }
            while (clips[index] == null || index == lastIndex);
        }
        lastIndex = index;
        return clips[index];
    }

    Voice TakeVoice(Voice[] pool, int priority)
    {
        foreach (Voice voice in pool)
            if (!voice.source.isPlaying) return voice;

        Voice candidate = null;
        foreach (Voice voice in pool)
        {
            if (voice.priority < priority) continue;
            if (candidate == null || voice.priority > candidate.priority ||
                (voice.priority == candidate.priority && voice.startedAt < candidate.startedAt))
                candidate = voice;
        }
        return candidate;
    }
}
