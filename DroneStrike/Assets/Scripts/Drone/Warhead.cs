using System;
using UnityEngine;

/// <summary>
/// The drone's payload. Detonates on impact above a threshold speed, or on
/// command from the pilot.
///
/// Damage falls off with distance from the blast, so a hit dead on the target
/// destroys it while a near miss only scorches it. That is what makes lining up
/// the run worth doing rather than flying vaguely at the area.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Warhead : MonoBehaviour
{
    [Header("Blast")]
    public WarheadType type = WarheadType.Compact;

    /// <summary>Impact speed, in m/s, needed to set the warhead off.</summary>
    public float armingSpeed = 4f;

    /// <summary>
    /// Scales blast damage for the airframe carrying it. A heavier drone lifts
    /// a bigger charge, which is what the "Молот" unlock actually buys — set by
    /// DroneFactory from the selected loadout.
    /// </summary>
    public float damageMultiplier = 1f;

    /// <summary>Fired once, when the warhead goes off.</summary>
    public event Action OnDetonated;

    public bool HasDetonated { get; private set; }

    public WarheadProfile Profile { get; private set; }

    Rigidbody body;
    DroneController drone;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        drone = GetComponent<DroneController>();

        Profile = WarheadProfile.For(type);

        // A lighter charge means a livelier drone. Applied here rather than in
        // the factory so the handling always matches whatever is actually fitted.
        if (drone != null)
        {
            drone.thrust *= Profile.thrustFactor;
            drone.maxSpeed *= Profile.speedFactor;
        }
    }

    // No manual trigger: the mouse aims the camera, and losing the drone every
    // time the pilot clicked was worse than useless. The warhead goes off on
    // impact and nothing else.

    void OnCollisionEnter(Collision collision)
    {
        if (HasDetonated) return;

        // Hitting an actual target always sets it off, however gently it was
        // clipped. The arming speed is measured along the collision normal, so
        // a hit into the back or the flank of a vehicle can report a low
        // relative velocity even when the drone was doing forty — which read as
        // "flew straight into the tank and nothing happened". A pilot who
        // touches the target has earned the detonation.
        if (collision.collider.GetComponentInParent<Target>() != null)
        {
            Detonate();
            return;
        }

        // Scenery still needs the gate, or clipping a branch on the way in
        // ends the run.
        if (collision.relativeVelocity.magnitude < armingSpeed) return;

        Detonate();
    }

    public void Detonate()
    {
        if (HasDetonated) return;
        HasDetonated = true;

        Vector3 origin = transform.position;

        if (GameEffects.Instance != null)
        {
            GameEffects.Instance.MuzzleFlash(origin, Vector3.up);
            GameEffects.Instance.HardImpact(origin, Vector3.up);
        }

        if (GameAudio.Instance != null) GameAudio.Instance.PlayPlayerShot(origin);

        ApplyBlast(origin);

        if (drone != null) drone.CutPower();
        if (OnDetonated != null) OnDetonated();

        // The drone is gone; hide it rather than destroying it this frame, so
        // anything still reading its transform this frame stays valid.
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            renderer.enabled = false;

        body.detectCollisions = false;
        body.isKinematic = true;
    }

    void ApplyBlast(Vector3 origin)
    {
        Collider[] caught = Physics.OverlapSphere(origin, Profile.blastRadius);
        var alreadyHit = new System.Collections.Generic.HashSet<Target>();

        foreach (Collider collider in caught)
        {
            Target target = collider.GetComponentInParent<Target>();
            if (target == null || target.IsDestroyed) continue;

            // A target with several colliders must not be damaged once per collider.
            if (!alreadyHit.Add(target)) continue;

            float distance = Vector3.Distance(origin, collider.ClosestPoint(origin));
            float falloff = Mathf.Clamp01(1f - distance / Profile.blastRadius);

            target.TakeDamage(Profile.damage * damageMultiplier * falloff);
        }
    }
}
