using System;
using UnityEngine;

public struct AttackReport
{
    public Target target;
    public float damage;
    public float healthRemaining;
    public float speed;
    public float speedMultiplier;
    public int targetsHit;
    public int targetsDestroyed;
    public bool weakSpot;
}

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
    public event Action<AttackReport> OnImpactReport;

    public bool HasDetonated { get; private set; }
    public bool WasSubmerged { get; private set; }

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
            GameEffects.Instance.Explosion(origin, Profile.blastRadius);
        }

        if (GameAudio.Instance != null) GameAudio.Instance.PlayExplosion(origin, type);

        DroneCameraGimbal gimbal = GetComponent<DroneCameraGimbal>();
        if (gimbal != null)
            gimbal.Shake(Mathf.Lerp(1.8f, 2.8f, Mathf.InverseLerp(3f, 9f, Profile.blastRadius)));

        AttackReport report = ApplyBlast(origin, impactSpeed);
        if (OnImpactReport != null) OnImpactReport(report);

        if (drone != null) drone.CutPower();
        if (OnDetonated != null) OnDetonated();

        RetireDrone();
    }

    /// <summary>Water consumes the drone without a dry-land blast.</summary>
    public void Submerge()
    {
        if (HasDetonated) return;
        HasDetonated = true;
        WasSubmerged = true;
        if (drone != null) drone.CutPower();
        if (OnDetonated != null) OnDetonated();
        RetireDrone();
    }

    void RetireDrone()
    {
        // The drone is gone; hide it rather than destroying it this frame, so
        // anything still reading its transform this frame stays valid.
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            renderer.enabled = false;

        // Clear any optional trails or local particles immediately. Without
        // this, a detached trail can remain visible until its lifetime expires
        // while the next drone is already launching.
        foreach (TrailRenderer trail in GetComponentsInChildren<TrailRenderer>(true))
        {
            trail.emitting = false;
            trail.Clear();
            trail.enabled = false;
        }
        foreach (ParticleSystem particles in GetComponentsInChildren<ParticleSystem>(true))
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        body.detectCollisions = false;
        body.isKinematic = true;
    }

    AttackReport ApplyBlast(Vector3 origin, float impactSpeed)
    {
        AttackReport report = new AttackReport { speed = Mathf.Max(0f, impactSpeed) };
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
        report.speedMultiplier = speedBonus;

        Collider[] caught = Physics.OverlapSphere(origin, Profile.blastRadius);
        var nearestHits = new System.Collections.Generic.Dictionary<Target, float>();

        foreach (Collider collider in caught)
        {
            Target target = collider.GetComponentInParent<Target>();
            if (target == null || target.IsDestroyed) continue;

            // Collider iteration order is undefined. Use the nearest surface
            // across ALL colliders before applying damage once per target.
            float distance = Vector3.Distance(origin, collider.ClosestPoint(origin));
            if (!nearestHits.TryGetValue(target, out float nearest) || distance < nearest)
                nearestHits[target] = distance;
        }
        foreach (var hit in nearestHits)
        {
            Target target = hit.Key;
            float distance = hit.Value;
            float falloff = Mathf.Clamp01(1f - distance / Profile.blastRadius);
            float damage = Profile.damage * damageMultiplier * falloff * speedBonus;

            // A solid wall takes most of the blast. Netting remains permeable;
            // its thin mesh is visual cover, not a concrete blast shield.
            RaycastHit cover;
            Vector3 destination = target.transform.position + Vector3.up;
            if (Physics.Linecast(origin, destination, out cover, ~0, QueryTriggerInteraction.Ignore)
                && cover.collider.GetComponentInParent<Target>() != target
                && !cover.collider.name.Contains("Net")
                && !cover.collider.name.Contains("Drape"))
                damage *= 0.3f;

            float before = target.Health;
            bool weak = target.IsWeakHit(origin);
            target.TakeDamage(damage, origin);
            float applied = before - target.Health;
            report.targetsHit++;
            if (target.IsDestroyed) report.targetsDestroyed++;
            if (applied > report.damage)
            {
                report.target = target;
                report.damage = applied;
                report.healthRemaining = target.Health;
                report.weakSpot = weak;
            }
        }
        return report;
    }
}
