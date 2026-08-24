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

    /// <summary>
    /// Read from <see cref="type"/> every time rather than cached in Awake.
    ///
    /// Caching it there was a real bug: AddComponent runs Awake synchronously,
    /// so the profile was resolved before the factory had assigned the type on
    /// the next line, and every drone flew with the compact charge whatever the
    /// player picked. It cost exactly the damage that made the standard charge
    /// worth fitting — the heavy airframe with the heavy charge came out at
    /// 132 against a tank's 140 and could not kill one in a single run.
    /// </summary>
    public WarheadProfile Profile { get { return WarheadProfile.For(type); } }

    Rigidbody body;
    DroneController drone;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        drone = GetComponent<DroneController>();
    }

    /// <summary>
    /// Fits a charge and applies what it does to the airframe's handling.
    ///
    /// Called by the factory once the drone is assembled, because the handling
    /// change depends on which charge was chosen and Awake cannot know that
    /// yet. A lighter charge means a livelier drone, so the two multiply with
    /// the airframe's own figures.
    /// </summary>
    public void Fit(WarheadType charge, float damageScale)
    {
        type = charge;
        damageMultiplier = damageScale;

        if (drone == null) drone = GetComponent<DroneController>();
        if (drone == null) return;

        drone.thrust *= Profile.thrustFactor;
        drone.maxSpeed *= Profile.speedFactor;
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
            Detonate(collision.relativeVelocity.magnitude);
            return;
        }

        // Scenery still needs the gate, or clipping a branch on the way in
        // ends the run.
        if (collision.relativeVelocity.magnitude < armingSpeed) return;

        Detonate(collision.relativeVelocity.magnitude);
    }

    /// <summary>
    /// <paramref name="impactSpeed"/> is the relative velocity the collision
    /// went off at, or left at its default when there was no collision at all
    /// — the signal-loss self-destruct calls this with nothing to measure.
    /// Only a real impact can earn the speed bonus in ApplyBlast; everything
    /// else deals exactly the charge's own base damage, same as before this
    /// existed.
    /// </summary>
    public void Detonate(float impactSpeed = -1f)
    {
        if (HasDetonated) return;
        HasDetonated = true;

        Vector3 origin = transform.position;

        if (GameEffects.Instance != null)
        {
            GameEffects.Instance.MuzzleFlash(origin, Vector3.up);
            GameEffects.Instance.HardImpact(origin, Vector3.up);
        }

        if (GameAudio.Instance != null) GameAudio.Instance.PlayExplosion(origin);

        ApplyBlast(origin, impactSpeed);

        if (drone != null) drone.CutPower();
        if (OnDetonated != null) OnDetonated();

        // The drone is gone; hide it rather than destroying it this frame, so
        // anything still reading its transform this frame stays valid.
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            renderer.enabled = false;

        body.detectCollisions = false;
        body.isKinematic = true;
    }

    void ApplyBlast(Vector3 origin, float impactSpeed)
    {
        // A drone that barely bumps a target and one flown into it at full
        // speed used to deal identical damage — the charge alone decided the
        // outcome, and there was no reason to fly a fast, committed run over
        // a lazy tap except that it looked better. A fast hit now earns up to
        // 50% more damage, floored at the charge's own base figure so nothing
        // gets weaker than it already was. impactSpeed is negative for a
        // detonation with no collision behind it (the signal-loss
        // self-destruct) — that case gets no bonus and no penalty, unchanged.
        float speedBonus = impactSpeed >= 0f
            ? 1f + Mathf.Clamp01((impactSpeed - armingSpeed) / 40f) * 0.5f
            : 1f;

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

            target.TakeDamage(Profile.damage * damageMultiplier * falloff * speedBonus);
        }
    }
}
