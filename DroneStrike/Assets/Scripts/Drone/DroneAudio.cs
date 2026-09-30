using UnityEngine;

/// <summary>
/// The original single-layer rotor hum. It follows throttle without stacking
/// bright motor and wind layers into a constant high-pitched whine.
///
/// When the drone dies — impact, blast, water, flat battery, lost link — the
/// hum fades out over <see cref="fadeOutSeconds"/> and the source is then
/// stopped and destroyed, so nothing is left playing on a hidden airframe.
/// The fade runs on unscaled time: a mission that ends on the same frame
/// pauses the clock (timeScale 0), and a fade on scaled time froze half-way,
/// leaving the dead drone humming under the results screen.
/// </summary>
[RequireComponent(typeof(DroneController))]
public class DroneAudio : MonoBehaviour
{
    public float minPitch = 0.82f;
    public float maxPitch = 1.12f;
    public float minVolume = 0.06f;
    public float maxVolume = 0.18f;
    public float fadeOutSeconds = 0.2f;

    DroneController drone;
    AudioSource source;
    float fadeFrom = -1f;
    float fadeStartedAt;

    void Start()
    {
        drone = GetComponent<DroneController>();
        if (GameAudio.Instance != null) source = GameAudio.Instance.AttachDroneLoop(transform);
    }

    void Update()
    {
        if (source == null || drone == null) return;

        if (!drone.IsPowered)
        {
            if (fadeFrom < 0f)
            {
                fadeFrom = source.volume;
                fadeStartedAt = Time.unscaledTime;
            }
            float t = Mathf.Clamp01((Time.unscaledTime - fadeStartedAt) / Mathf.Max(0.01f, fadeOutSeconds));
            source.volume = Mathf.Lerp(fadeFrom, 0f, t);
            if (t >= 1f) Silence();
            return;
        }

        // Paused (menu, lost focus, ad): the motors are not what the player
        // should be hearing over a pause screen.
        MissionManager mission = MissionManager.Instance;
        bool paused = mission != null && mission.PauseReasons != MissionManager.PauseReason.None;
        if (paused)
        {
            source.volume = Mathf.MoveTowards(source.volume, 0f, Time.unscaledDeltaTime * 1.5f);
            return;
        }

        if (!source.isPlaying) source.Play();
        float throttle = drone.ThrottleLevel;
        source.pitch = Mathf.Lerp(minPitch, maxPitch, throttle);
        source.volume = Mathf.Lerp(minVolume, maxVolume, throttle);
    }

    /// <summary>Stops and removes the loop immediately.</summary>
    public void Silence()
    {
        if (source == null) return;
        source.Stop();
        Destroy(source.gameObject);
        source = null;
    }

    void OnDisable() { Silence(); }
}
