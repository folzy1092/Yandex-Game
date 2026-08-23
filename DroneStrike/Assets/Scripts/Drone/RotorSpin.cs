using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spins the propellers, faster under throttle, and stops them when the motors
/// cut out.
///
/// Adjacent rotors turn opposite ways, as they do on a real quadcopter — it is
/// what stops the airframe spinning itself, and it is visible enough that
/// getting it wrong looks off.
/// </summary>
public class RotorSpin : MonoBehaviour
{
    public float idleSpeed = 900f;      // degrees per second
    public float fullSpeed = 3200f;
    public float spinDownRate = 3f;

    readonly List<Transform> rotors = new List<Transform>();
    DroneController drone;
    float currentSpeed;

    void Start()
    {
        drone = GetComponent<DroneController>();

        // Found by name: the factory builds them as Prop0..Prop3.
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("Prop")) rotors.Add(child);
        }
    }

    void Update()
    {
        float target = 0f;

        if (drone != null && drone.IsPowered)
            target = Mathf.Lerp(idleSpeed, fullSpeed, drone.ThrottleLevel);

        currentSpeed = Mathf.Lerp(currentSpeed, target, Time.deltaTime * spinDownRate);

        for (int i = 0; i < rotors.Count; i++)
        {
            // Alternate direction around the frame.
            float direction = (i % 2 == 0) ? 1f : -1f;
            rotors[i].Rotate(Vector3.up, currentSpeed * direction * Time.deltaTime, Space.Self);
        }
    }
}
