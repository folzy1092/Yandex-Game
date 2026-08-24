using UnityEngine;

/// <summary>
/// The rotor hum, pitched and volumed by throttle so it actually tells the
/// pilot something — spooling up into a dive reads as speed, dying away as the
/// motors cut reads as trouble, before the player's eyes even confirm it.
/// </summary>
[RequireComponent(typeof(DroneController))]
public class DroneAudio : MonoBehaviour
{
    public float minPitch = 0.78f;
    public float maxPitch = 1.30f;
    public float minVolume = 0.16f;
    public float maxVolume = 0.48f;

    /// <summary>How quickly volume fades once the motors cut, in units/second.</summary>
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

        float throttle = drone.ThrottleLevel;
        source.pitch = Mathf.Lerp(minPitch, maxPitch, throttle);
        source.volume = Mathf.Lerp(minVolume, maxVolume, throttle);
    }
}
