using UnityEngine;

/// <summary>
/// The motor sound, from real on-board FPV recordings (CC0, see
/// Resources/Audio/SOURCE_MANIFEST.md): a low-revs loop and a high-revs loop
/// crossfaded by throttle, with the pitch rising smoothly with revs and speed.
///
/// The old single synthesized 0.25 s loop sounded like a muddy, stepped buzz:
/// keyboard throttle is either 0 or 1, and the pitch snapped between the two
/// the instant a key went down, while the short loop audibly repeated. Now
/// the throttle driving the sound is smoothed (motors spool up and down, they
/// do not jump), the loops are 9-11 s of real recording, and the two layers
/// blend with an equal-power crossfade.
///
/// When the drone dies — impact, blast, water, flat battery, lost link — the
/// sound fades out over <see cref="fadeOutSeconds"/> and the sources are then
/// stopped and destroyed. The fade runs on unscaled time: a mission that ends
/// on the same frame pauses the clock (timeScale 0), and a fade on scaled time
/// froze half-way, leaving the dead drone humming under the results screen.
/// </summary>
[RequireComponent(typeof(DroneController))]
public class DroneAudio : MonoBehaviour
{
    public float idleVolume = 0.30f;
    public float fullVolume = 0.48f;
    public float minPitch = 0.9f;
    public float maxPitch = 1.14f;
    public float spoolUp = 3.2f;
    public float spoolDown = 1.8f;
    public float fadeOutSeconds = 0.2f;

    DroneController drone;
    Rigidbody body;
    AudioSource low;
    AudioSource high;
    bool recorded;
    float revs;
    float fadeFromLow = -1f, fadeFromHigh;
    float fadeStartedAt;

    void Start()
    {
        drone = GetComponent<DroneController>();
        body = GetComponent<Rigidbody>();
        if (GameAudio.Instance == null) return;

        low = GameAudio.Instance.AttachDroneLayer(transform, "fpv_real_low");
        high = GameAudio.Instance.AttachDroneLayer(transform, "fpv_real_high");
        recorded = low != null && high != null;
        if (!recorded)
        {
            if (low != null) Destroy(low.gameObject);
            if (high != null) Destroy(high.gameObject);
            high = null;
            low = GameAudio.Instance.AttachDroneLoop(transform);
        }
    }

    void Update()
    {
        if (low == null || drone == null) return;

        if (!drone.IsPowered)
        {
            if (fadeFromLow < 0f)
            {
                fadeFromLow = low.volume;
                fadeFromHigh = high != null ? high.volume : 0f;
                fadeStartedAt = Time.unscaledTime;
            }
            float t = Mathf.Clamp01((Time.unscaledTime - fadeStartedAt) / Mathf.Max(0.01f, fadeOutSeconds));
            low.volume = Mathf.Lerp(fadeFromLow, 0f, t);
            if (high != null) high.volume = Mathf.Lerp(fadeFromHigh, 0f, t);
            if (t >= 1f) Silence();
            return;
        }

        // Paused (menu, lost focus, ad): the motors are not what the player
        // should be hearing over a pause screen.
        MissionManager mission = MissionManager.Instance;
        bool paused = mission != null && mission.PauseReasons != MissionManager.PauseReason.None;
        if (paused)
        {
            low.volume = Mathf.MoveTowards(low.volume, 0f, Time.unscaledDeltaTime * 1.5f);
            if (high != null) high.volume = Mathf.MoveTowards(high.volume, 0f, Time.unscaledDeltaTime * 1.5f);
            return;
        }

        if (!low.isPlaying) low.Play();
        if (high != null && !high.isPlaying) high.Play();

        // Motors spool: the sound follows a smoothed throttle, never the key.
        float target = drone.ThrottleLevel;
        float rate = target > revs ? spoolUp : spoolDown;
        revs = Mathf.MoveTowards(revs, target, Time.deltaTime * rate);
        float speed = body != null ? Mathf.Clamp01(body.linearVelocity.magnitude / 30f) : 0f;

        float pitch = Mathf.Lerp(minPitch, maxPitch, revs) + speed * 0.05f;
        float loudness = Mathf.Lerp(idleVolume, fullVolume, Mathf.Max(revs, speed * 0.6f));

        if (recorded)
        {
            float blend = Mathf.Clamp01(revs * 0.8f + speed * 0.3f);
            low.volume = loudness * Mathf.Cos(blend * Mathf.PI * 0.5f);
            high.volume = loudness * Mathf.Sin(blend * Mathf.PI * 0.5f);
            low.pitch = pitch;
            // The high-revs recording sits ~40% above the low one already.
            high.pitch = pitch * 0.94f;
        }
        else
        {
            low.pitch = pitch;
            low.volume = loudness * 0.4f;
        }
    }

    /// <summary>Stops and removes the loops immediately.</summary>
    public void Silence()
    {
        foreach (AudioSource source in new[] { low, high })
        {
            if (source == null) continue;
            source.Stop();
            Destroy(source.gameObject);
        }
        low = null;
        high = null;
    }

    void OnDisable() { Silence(); }
}
