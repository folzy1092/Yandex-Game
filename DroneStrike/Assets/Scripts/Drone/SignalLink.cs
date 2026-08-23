using System;
using UnityEngine;

/// <summary>
/// The radio link back to the launch point.
///
/// This is the map boundary, and it is deliberately not a wall. Strength falls
/// off with distance: the picture degrades first, warning the pilot they are
/// pushing it, and only past the hard limit is the drone lost. A player who
/// wanders off gets a reason they can see and feel, instead of bouncing off
/// something invisible.
/// </summary>
public class SignalLink : MonoBehaviour
{
    /// <summary>Distance at which the picture is still perfectly clean.</summary>
    public float cleanRange = 260f;

    /// <summary>Distance at which the link drops entirely.</summary>
    public float maximumRange = 400f;

    /// <summary>Link quality, 1 = clean, 0 = lost.</summary>
    public float Strength { get; private set; }

    /// <summary>Bars for the telemetry readout, 0 to 4.</summary>
    public int Bars { get { return Mathf.CeilToInt(Strength * 4f); } }

    public bool IsLost { get; private set; }

    /// <summary>Fired once when the link drops.</summary>
    public event Action OnLost;

    Vector3 launchPoint;
    DroneController drone;

    void Awake()
    {
        drone = GetComponent<DroneController>();
        launchPoint = transform.position;
        Strength = 1f;
    }

    /// <summary>Called by the mission when a fresh drone is launched.</summary>
    public void SetLaunchPoint(Vector3 point)
    {
        launchPoint = point;
        Strength = 1f;
        IsLost = false;
    }

    void Update()
    {
        if (IsLost) return;

        float distance = Vector3.Distance(transform.position, launchPoint);

        Strength = distance <= cleanRange
            ? 1f
            : Mathf.Clamp01(1f - (distance - cleanRange) / (maximumRange - cleanRange));

        if (Strength > 0f) return;

        IsLost = true;
        if (drone != null) drone.CutPower();
        if (OnLost != null) OnLost();
    }
}
