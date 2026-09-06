using UnityEngine;

/// <summary>
/// Blends dedicated idle, cruise, load and wind loops. The layers follow real
/// throttle and velocity, so a hovering drone does not sound like a fast pass.
/// </summary>
[RequireComponent(typeof(DroneController))]
public class DroneAudio : MonoBehaviour
{
    DroneController drone;
    DroneMotorRig motor;

    void Start()
    {
        drone = GetComponent<DroneController>();
        if (GameAudio.Instance != null)
            motor = GameAudio.Instance.AttachDroneMotor(transform);
    }

    void Update()
    {
        if (motor == null || drone == null) return;
        motor.Tick(drone.ThrottleLevel, drone.SpeedKmh / 3.6f, drone.IsPowered);
    }

    void OnDestroy()
    {
        if (motor != null) motor.Dispose();
    }
}
