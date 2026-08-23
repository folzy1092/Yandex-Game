using System;
using UnityEngine;

/// <summary>
/// Detects the drone hitting something hard enough to wreck it without setting
/// the warhead off — flying into a wall, or dropping out of the sky once the
/// battery is flat.
///
/// The warhead handles fast impacts itself; this covers everything that kills
/// the airframe without a detonation, so the mission always finds out the drone
/// is gone whichever way it went.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DroneImpact : MonoBehaviour
{
    /// <summary>Impact speed, m/s, the airframe cannot survive.</summary>
    public float fatalSpeed = 6f;

    public bool IsWrecked { get; private set; }

    /// <summary>Fired once, when the drone is wrecked by an impact.</summary>
    public event Action OnCrashed;

    DroneController drone;
    Warhead warhead;

    void Awake()
    {
        drone = GetComponent<DroneController>();
        warhead = GetComponent<Warhead>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (IsWrecked) return;

        // If the warhead went off, the mission has already been told.
        if (warhead != null && warhead.HasDetonated) return;

        if (collision.relativeVelocity.magnitude < fatalSpeed) return;

        IsWrecked = true;

        if (GameEffects.Instance != null)
            GameEffects.Instance.HardImpact(transform.position, collision.contacts[0].normal);

        if (GameAudio.Instance != null)
            GameAudio.Instance.PlayHardImpact(transform.position);

        if (drone != null) drone.CutPower();
        if (OnCrashed != null) OnCrashed();
    }
}
