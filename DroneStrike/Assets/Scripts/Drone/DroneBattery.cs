using System;
using UnityEngine;

/// <summary>
/// The drone's charge, which doubles as the mission clock.
///
/// Drain scales with throttle, so hovering carefully lasts far longer than
/// flying flat out. That turns the battery into a real decision rather than a
/// countdown you can ignore: rush the target and risk running dry, or take it
/// slow and arrive with margin.
/// </summary>
public class DroneBattery : MonoBehaviour
{
    /// <summary>Seconds of flight at a steady hover.</summary>
    public float hoverEndurance = 150f;

    /// <summary>How much faster full throttle empties the pack.</summary>
    public float throttleDrainFactor = 1.8f;

    /// <summary>Charge left, 0..1.</summary>
    public float Charge { get; private set; }

    public int ChargePercent { get { return Mathf.CeilToInt(Charge * 100f); } }

    public bool IsFlat { get { return Charge <= 0f; } }

    /// <summary>Fired once when the pack runs flat.</summary>
    public event Action OnDepleted;

    DroneController drone;
    bool reported;

    void Awake()
    {
        drone = GetComponent<DroneController>();
        Charge = 1f;
    }

    void Update()
    {
        if (reported) return;

        // Hovering sits at half throttle, so that is the baseline the endurance
        // figure is quoted against.
        float throttle = drone != null ? drone.ThrottleLevel : 0.5f;
        float effort = 1f + Mathf.Abs(throttle - 0.5f) * 2f * throttleDrainFactor;

        Charge -= Time.deltaTime / hoverEndurance * effort;

        if (Charge > 0f) return;

        Charge = 0f;
        reported = true;

        if (drone != null) drone.CutPower();
        if (OnDepleted != null) OnDepleted();
    }
}
