using UnityEngine;

/// <summary>
/// The original single-layer rotor hum. It follows throttle without stacking
/// bright motor and wind layers into a constant high-pitched whine.
/// </summary>
[RequireComponent(typeof(DroneController))]
public class DroneAudio : MonoBehaviour
{
    public float minPitch = 0.82f;
    public float maxPitch = 1.12f;
    public float minVolume = 0.06f;
    public float maxVolume = 0.18f;
    public float fadeOutRate = 1.4f;

    DroneController drone;
    AudioSource source;

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
            source.volume = Mathf.MoveTowards(source.volume, 0f, Time.deltaTime * fadeOutRate);
            if (source.volume <= 0.001f && source.isPlaying) source.Stop();
            return;
        }

        if (!source.isPlaying) source.Play();
        float throttle = drone.ThrottleLevel;
        source.pitch = Mathf.Lerp(minPitch, maxPitch, throttle);
        source.volume = Mathf.Lerp(minVolume, maxVolume, throttle);
    }
}
