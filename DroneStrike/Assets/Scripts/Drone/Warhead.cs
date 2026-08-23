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
    public float damage = 160f;
    public float blastRadius = 7f;

    /// <summary>Impact speed, in m/s, needed to set the warhead off.</summary>
    public float armingSpeed = 4f;

    /// <summary>Fired once, when the warhead goes off.</summary>
    public event Action OnDetonated;

    public bool HasDetonated { get; private set; }

    Rigidbody body;
    DroneController drone;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        drone = GetComponent<DroneController>();
    }

    void Update()
    {
        // Manual detonation, for finishing a target the drone is sitting next to.
        if (!HasDetonated && Input.GetMouseButtonDown(0))
            Detonate();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (HasDetonated) return;

        // A gentle bump — clipping a branch on the way in — should not set it off.
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
        Collider[] caught = Physics.OverlapSphere(origin, blastRadius);
        var alreadyHit = new System.Collections.Generic.HashSet<Target>();

        foreach (Collider collider in caught)
        {
            Target target = collider.GetComponentInParent<Target>();
            if (target == null || target.IsDestroyed) continue;

            // A target with several colliders must not be damaged once per collider.
            if (!alreadyHit.Add(target)) continue;

            float distance = Vector3.Distance(origin, collider.ClosestPoint(origin));
            float falloff = Mathf.Clamp01(1f - distance / blastRadius);

            target.TakeDamage(damage * falloff);
        }
    }
}
