using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// The radio link back to the launch point.
///
/// This is the map boundary — in every direction, including up. Strength falls
/// off with distance: the picture degrades first, warning the pilot they are
/// pushing it, and past the hard limit the link drops. A player who wanders off
/// gets a reason they can see and feel, instead of bouncing off something
/// invisible.
///
/// It is the altitude limit too. The range is measured in full 3D from the pad,
/// so climbing far enough breaks the link exactly the way flying too far out
/// does. That replaced a hard ceiling the drone used to bump into, which is the
/// worst kind of boundary: it stops the player without telling them anything.
///
/// Losing the link cuts the motors immediately, so the drone starts falling,
/// and the payload self-destructs a couple of seconds later — which is what a
/// real one does rather than leaving live ordnance lying in a field.
/// </summary>
public class SignalLink : MonoBehaviour
{
    /// <summary>Distance at which the picture is still perfectly clean.</summary>
    public float cleanRange = 230f;

    /// <summary>Distance at which the link drops entirely.</summary>
    public float maximumRange = 350f;

    /// <summary>Seconds between losing the link and the payload going off.</summary>
    public float selfDestructDelay = 2f;

    /// <summary>Link quality, 1 = clean, 0 = lost.</summary>
    public float Strength { get; private set; }

    /// <summary>Bars for the telemetry readout, 0 to 4.</summary>
    public int Bars { get { return Mathf.CeilToInt(Strength * 4f); } }

    public bool IsLost { get; private set; }

    /// <summary>
    /// Fired the moment the link drops, for the warning on screen. The drone is
    /// not counted as lost here — that happens when the payload detonates, so
    /// the mission only ever counts it once.
    /// </summary>
    public event Action OnLost;

    Vector3 launchPoint;
    DroneController drone;
    Warhead warhead;

    void Awake()
    {
        drone = GetComponent<DroneController>();
        warhead = GetComponent<Warhead>();
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

        StartCoroutine(SelfDestruct());
    }

    IEnumerator SelfDestruct()
    {
        yield return new WaitForSeconds(selfDestructDelay);

        // Detonating rather than just vanishing also routes the loss through the
        // warhead, which is the single place the mission counts a drone as gone.
        if (warhead != null && !warhead.HasDetonated) warhead.Detonate();
    }
}
