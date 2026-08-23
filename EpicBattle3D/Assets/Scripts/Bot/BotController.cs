using UnityEngine;

/// <summary>
/// A simple free-for-all opponent with two behaviours: wander the arena, and
/// shoot at anyone it can actually see.
///
/// Wandering is deliberately raycast-driven rather than NavMesh-driven — the map
/// is a small box, and a NavMesh would have to be baked through the Unity editor
/// GUI, which cannot be done from code.
///
/// Each bot gets its own random heading and its own random re-steer timer, which
/// is what keeps a group of bots spreading out across the map instead of all
/// walking toward the same place.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class BotController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 3.5f;
    public float turnSpeed = 260f;
    public float gravity = -20f;
    public float obstacleCheckDistance = 2.5f;
    public float minSteerInterval = 2f;
    public float maxSteerInterval = 5f;

    [Header("Senses")]
    public float viewDistance = 45f;
    public float fieldOfView = 110f;
    public float scanInterval = 0.25f;
    public float eyeHeight = 1.5f;

    [Header("Weapon")]
    public int damage = 18;
    public float range = 60f;
    public float fireCooldown = 0.9f;
    public float spreadDegrees = 5f;
    public float aimTolerance = 12f;

    /// <summary>Where shots come from. Set by the factory to the pistol's muzzle.</summary>
    public Transform muzzle;

    CharacterController controller;
    Health health;

    Vector3 velocity;
    float steerTimer;
    float targetYaw;

    Health target;
    float nextScanTime;
    float nextFireTime;

    Vector3 EyePosition { get { return transform.position + Vector3.up * eyeHeight; } }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        health = GetComponent<Health>();
    }

    void OnEnable()
    {
        // Fresh heading whenever the bot comes back to life, so respawned bots
        // do not all march off in the direction they died facing.
        PickNewHeading();
        target = null;
    }

    void Update()
    {
        if (!MatchManager.IsMatchRunning) return;

        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + scanInterval;
            target = FindTarget();
        }

        if (target != null && target.IsAlive)
            AttackBehaviour();
        else
            WanderBehaviour();

        ApplyGravity();
    }

    // ---------- wandering ----------

    void WanderBehaviour()
    {
        steerTimer -= Time.deltaTime;

        if (steerTimer <= 0f || IsBlockedAhead())
            PickNewHeading();

        RotateTowardYaw(targetYaw);
        controller.Move(transform.forward * moveSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Picks a random heading, preferring one with clear space ahead so the bot
    /// does not immediately grind against the wall it just turned away from.
    /// </summary>
    void PickNewHeading()
    {
        steerTimer = Random.Range(minSteerInterval, maxSteerInterval);

        for (int attempt = 0; attempt < 12; attempt++)
        {
            float candidateYaw = Random.Range(0f, 360f);
            Vector3 direction = Quaternion.Euler(0f, candidateYaw, 0f) * Vector3.forward;

            if (!IsBlocked(direction, obstacleCheckDistance * 2f))
            {
                targetYaw = candidateYaw;
                return;
            }
        }

        // Boxed in on every sample — just turn around.
        targetYaw = transform.eulerAngles.y + 180f;
    }

    bool IsBlockedAhead()
    {
        return IsBlocked(transform.forward, obstacleCheckDistance);
    }

    bool IsBlocked(Vector3 direction, float distance)
    {
        // Start the ray just outside our own capsule, otherwise it can begin
        // inside our collider and report nothing at all.
        Vector3 origin = EyePosition + direction.normalized * (controller.radius + 0.1f);
        return Physics.Raycast(origin, direction, distance,
                               GameLayers.GeometryMask, QueryTriggerInteraction.Ignore);
    }

    void RotateTowardYaw(float yaw)
    {
        Quaternion desired = Quaternion.Euler(0f, yaw, 0f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, turnSpeed * Time.deltaTime);
    }

    // ---------- fighting ----------

    void AttackBehaviour()
    {
        Vector3 toTarget = TargetAimPoint() - EyePosition;
        Vector3 flatDirection = new Vector3(toTarget.x, 0f, toTarget.z);

        if (flatDirection.sqrMagnitude > 0.001f)
        {
            Quaternion desired = Quaternion.LookRotation(flatDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, turnSpeed * Time.deltaTime);
        }

        // Only shoot once roughly on target, so bots cannot snap-fire the instant
        // someone walks past behind them.
        float aimError = Vector3.Angle(transform.forward, flatDirection);
        if (aimError > aimTolerance) return;

        if (Time.time < nextFireTime) return;
        nextFireTime = Time.time + fireCooldown;

        ShotResult shot = Hitscan.Fire(gameObject, EyePosition, toTarget.normalized,
                                       range, damage, spreadDegrees);
        PlayShotFeedback(shot);
    }

    void PlayShotFeedback(ShotResult shot)
    {
        Vector3 muzzlePosition = muzzle != null ? muzzle.position : EyePosition;

        if (GameAudio.Instance != null) GameAudio.Instance.PlayBotShot(muzzlePosition);

        if (GameEffects.Instance != null)
        {
            GameEffects.Instance.MuzzleFlash(muzzlePosition, transform.forward, muzzle);
            GameEffects.Instance.Tracer(muzzlePosition, shot.point, new Color(1f, 0.6f, 0.35f));
        }

        if (!shot.hitSomething) return;

        if (shot.hitCharacter)
        {
            if (GameEffects.Instance != null) GameEffects.Instance.FleshImpact(shot.point, shot.normal);
            if (GameAudio.Instance != null) GameAudio.Instance.PlayFleshImpact(shot.point);
            return;
        }

        if (GameEffects.Instance != null) GameEffects.Instance.HardImpact(shot.point, shot.normal);
        if (GameAudio.Instance != null) GameAudio.Instance.PlayHardImpact(shot.point);
    }

    Vector3 TargetAimPoint()
    {
        return target.transform.position + Vector3.up * eyeHeight;
    }

    Health FindTarget()
    {
        if (MatchManager.Instance == null) return null;
        if (health != null && !health.IsAlive) return null;

        Health best = null;
        float bestDistance = float.MaxValue;

        var combatants = MatchManager.Instance.Combatants;
        for (int i = 0; i < combatants.Count; i++)
        {
            Health candidate = combatants[i];
            if (candidate == null || candidate.gameObject == gameObject || !candidate.IsAlive) continue;

            Vector3 toCandidate = (candidate.transform.position + Vector3.up * eyeHeight) - EyePosition;
            float distance = toCandidate.magnitude;
            if (distance > viewDistance || distance >= bestDistance) continue;

            if (Vector3.Angle(transform.forward, toCandidate) > fieldOfView * 0.5f) continue;
            if (!HasLineOfSight(candidate, toCandidate, distance)) continue;

            best = candidate;
            bestDistance = distance;
        }

        return best;
    }

    bool HasLineOfSight(Health candidate, Vector3 toCandidate, float distance)
    {
        RaycastHit hit;
        if (!Physics.Raycast(EyePosition, toCandidate.normalized, out hit, distance + 0.5f,
                             GameLayers.ShootableMask, QueryTriggerInteraction.Collide))
            return false;

        Hitbox hitbox = hit.collider.GetComponent<Hitbox>();
        Health hitHealth = hitbox != null ? hitbox.owner : hit.collider.GetComponentInParent<Health>();

        return hitHealth == candidate;
    }

    // ---------- physics ----------

    void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(Vector3.up * velocity.y * Time.deltaTime);
    }
}
