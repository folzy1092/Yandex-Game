using UnityEngine;

/// <summary>
/// Owns the synthesised sound effects and plays them.
///
/// Creates itself on load, generating every clip once, then plays them through a
/// small pool of pooled AudioSources so overlapping gunfire does not cut itself off.
/// </summary>
public class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }

    const int VoiceCount = 12;

    AudioClip gunshot;
    AudioClip botGunshot;
    AudioClip emptyClick;
    AudioClip magOut;
    AudioClip magIn;
    AudioClip impactHard;
    AudioClip impactFlesh;
    AudioClip footstep;
    AudioClip hitmarker;
    AudioClip respawn;

    AudioSource[] voices;
    int nextVoice;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;

        var go = new GameObject("GameAudio");
        Instance = go.AddComponent<GameAudio>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        GenerateClips();
        CreateVoices();
    }

    void GenerateClips()
    {
        gunshot = ProceduralAudio.CreateGunshot("Gunshot", 0.30f, 190f, 0.42f, 1001);
        // Slightly lower and duller, so incoming fire is distinguishable from your own.
        botGunshot = ProceduralAudio.CreateGunshot("BotGunshot", 0.28f, 150f, 0.30f, 1002);

        emptyClick = ProceduralAudio.CreateEmptyClick("EmptyClick", 1003);
        magOut = ProceduralAudio.CreateClick("MagOut", 0.12f, 950f, 60f, 1004);
        magIn = ProceduralAudio.CreateClick("MagIn", 0.14f, 1450f, 45f, 1005);

        impactHard = ProceduralAudio.CreateImpact("ImpactHard", 0.16f, 1750f, 1006);
        impactFlesh = ProceduralAudio.CreateFleshHit("ImpactFlesh", 0.18f, 1007);

        footstep = ProceduralAudio.CreateFootstep("Footstep", 0.13f, 1008);
        hitmarker = ProceduralAudio.CreateClick("Hitmarker", 0.05f, 2100f, 150f, 1009);
        respawn = ProceduralAudio.CreateRespawnChime("Respawn");
    }

    void CreateVoices()
    {
        voices = new AudioSource[VoiceCount];

        for (int i = 0; i < VoiceCount; i++)
        {
            var go = new GameObject("Voice" + i);
            go.transform.SetParent(transform, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 4f;
            source.maxDistance = 60f;

            voices[i] = source;
        }
    }

    // ---------- public API ----------

    public void PlayPlayerShot(Vector3 position) { Play(gunshot, position, 0.55f, 1f, 0.05f); }
    public void PlayBotShot(Vector3 position) { Play(botGunshot, position, 0.5f, 1f, 0.12f); }
    public void PlayEmptyClick(Vector3 position) { Play(emptyClick, position, 0.4f, 1f, 0.05f); }
    public void PlayMagOut(Vector3 position) { Play(magOut, position, 0.4f, 1f, 0.08f); }
    public void PlayMagIn(Vector3 position) { Play(magIn, position, 0.4f, 1f, 0.08f); }
    public void PlayHardImpact(Vector3 position) { Play(impactHard, position, 0.45f, 1f, 0.18f); }
    public void PlayFleshImpact(Vector3 position) { Play(impactFlesh, position, 0.55f, 1f, 0.15f); }
    public void PlayFootstep(Vector3 position) { Play(footstep, position, 0.25f, 1f, 0.15f); }
    public void PlayRespawn(Vector3 position) { Play(respawn, position, 0.45f, 1f, 0f); }

    /// <summary>
    /// The hit confirmation tick. Played flat (not positioned in the world)
    /// because it is feedback for the shooter, not a sound in the arena.
    /// </summary>
    public void PlayHitmarker(bool headshot)
    {
        AudioSource source = NextVoice();
        source.transform.position = Vector3.zero;
        source.spatialBlend = 0f;
        source.clip = hitmarker;
        source.volume = headshot ? 0.5f : 0.35f;
        source.pitch = headshot ? 1.35f : 1f;
        source.Play();
    }

    void Play(AudioClip clip, Vector3 position, float volume, float basePitch, float pitchJitter)
    {
        if (clip == null) return;

        AudioSource source = NextVoice();
        source.transform.position = position;
        source.spatialBlend = 1f;
        source.clip = clip;
        source.volume = volume;
        source.pitch = basePitch + Random.Range(-pitchJitter, pitchJitter);
        source.Play();
    }

    AudioSource NextVoice()
    {
        AudioSource source = voices[nextVoice];
        nextVoice = (nextVoice + 1) % VoiceCount;
        return source;
    }
}
