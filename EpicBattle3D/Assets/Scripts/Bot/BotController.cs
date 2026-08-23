using UnityEngine;

/// <summary>
/// A free-for-all opponent driven by a small behaviour state machine.
///
///     Wander ──sees someone──► Engage ──target too far──► Chase
///        ▲                        │                         │
///        │                        ├──health low───► Retreat │
///        │                        │                    │    │
///        └──lost target / calmed──┴────────────────────┴────┘
///                     ▲
///     Investigate ────┘   (shot from somewhere you cannot see)
///
/// Movement is raycast-driven rather than NavMesh-driven: the arena is small,
/// and a NavMesh would have to be baked through the editor GUI, which cannot be
/// done from code.
///
/// Each bot gets its own heading, its own re-steer timer and its own personality,
/// which is what keeps a group of them spreading across the map and behaving
/// differently from one another.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class BotController : MonoBehaviour
{
    enum State
    {
        Wander,
        Chase,
        Engage,
        Retreat,
        Investigate
    }

    [Header("Movement")]
    public float gravity = -20f;
    public float obstacleCheckDistance = 2.5f;
    public float minSteerInterval = 2f;
    public float maxSteerInterval = 5f;

    [Header("Senses")]
    public float scanInterval = 0.2f;
    public float eyeHeight = 1.5f;

    [Header("Weapon")]
    public float range = 60f;
    public float aimTolerance = 12f;

    /// <summary>Distance a bot tries to hold while shooting.</summary>
    public float preferredCombatRange = 14f;

    /// <summary>Where shots come from. Set by the factory to the pistol's muzzle.</summary>
    public Transform muzzle;

    BotProfile profile;
    float aggression;

    CharacterController controller;
    Health health;

    Vector3 velocity;
    State state = State.Wander;

    float steerTimer;
    float targetYaw;

    Health target;
    float nextScanTime;
    float nextFireTime;
    float targetAcquiredAt;
    Vector3 lastKnownTargetPosition;
    Vector3 previousTargetPosition;

    float strafeDirection = 1f;
    float nextStrafeFlip;

    Vector3 investigatePoint;
    float investigateUntil;

    float calmDownAt;

    Vector3 EyePosition { get { return transform.position + Vector3.up * eyeHeight; } }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        health = GetComponent<Health>();

        // Personality is rolled once per bot and never changes, so a bot the
        // player learns to read stays readable.
        aggression = Random.value;
        profile = BotProfile.For(MatchSettings.Difficulty).WithPersonality(aggression);

        if (health != null) health.OnDamaged += HandleDamaged;
    }

    void OnDestroy()
    {
        if (health != null) health.OnDamaged -= HandleDamaged;
    }

    void OnEnable()
    {
        // Fresh heading whenever the bot comes back to life, so respawned bots do
        // not all march off in the direction they died facing.
        PickNewHeading();
        target = null;
        state = State.Wander;
    }

    void Update()
    {
        if (!MatchManager.IsMatchRunning) return;

        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + scanInterval;
            UpdateTargeting();
        }

        switch (state)
        {
            case State.Engage: EngageBehaviour(); break;
            case State.Chase: ChaseBehaviour(); break;
            case State.Retreat: RetreatBehaviour(); break;
            case State.Investigate: InvestigateBehaviour(); break;
            default: WanderBehaviour(); break;
        }

        ApplyGravity();
    }

    // ---------- perception ----------

    void UpdateTargeting()
    {
        Health found = FindTarget();

        if (found != null)
        {
            if (target != found)
            {
                // New target: start the reaction clock. Firing before it elapses
                // is what makes a bot feel like an aimbot.
                target = found;
                targetAcquiredAt = Time.time;
                previousTargetPosition = found.transform.position;
            }

            lastKnownTargetPosition = found.transform.position;
            state = ChooseCombatState(found);
            return;
        }

        target = null;

        if (state == State.Engage || state == State.Chase)
        {
            // Lost sight of someone: go and look where they were.
            investigatePoint = lastKnownTargetPosition;
            investigateUntil = Time.time + 4f;
            state = State.Investigate;
            return;
        }

        if (state == State.Retreat && Time.time >= calmDownAt)
            state = State.Wander;
    }

    State ChooseCombatState(Health found)
    {
        if (ShouldRetreat())
        {
            calmDownAt = Time.time + 3.5f;
            return State.Retreat;
        }

        float distance = Vector3.Distance(transform.position, found.transform.position);

        // Too far to shoot accurately: close the gap first.
        if (distance > preferredCombatRange * 1.6f) return State.Chase;

        return State.Engage;
    }

    bool ShouldRetreat()
    {
        if (health == null) return false;

        float fraction = (float)health.CurrentHealth / health.maxHealth;
        if (fraction > profile.retreatHealthFraction) return false;

        // Already retreating: keep going until calm, rather than flip-flopping
        // between backing off and charging every scan.
        return state == State.Retreat || Time.time >= calmDownAt;
    }

    Health FindTarget()
    {
        if (MatchManager.Instance == null) return null;
        if (health != null && !health.IsAlive) return null;

        Health best = null;
        float bestScore = float.MaxValue;

        var combatants = MatchManager.Instance.Combatants;
        for (int i = 0; i < combatants.Count; i++)
        {
            Health candidate = combatants[i];
            if (candidate == null || candidate.gameObject == gameObject || !candidate.IsAlive) continue;

            Vector3 toCandidate = (candidate.transform.position + Vector3.up * eyeHeight) - EyePosition;
            float distance = toCandidate.magnitude;
            if (distance > profile.viewDistance) continue;

            if (Vector3.Angle(transform.forward, toCandidate) > profile.fieldOfView * 0.5f) continue;
            if (!HasLineOfSight(candidate, toCandidate, distance)) continue;

            // Prefer wounded enemies: finishing someone off is both smarter and
            // what a human player would do.
            float woundedBonus = 1f - (float)candidate.CurrentHealth / candidate.maxHealth;
            float score = distance * (1f - woundedBonus * 0.45f);

            if (score >= bestScore) continue;

            best = candidate;
            bestScore = score;
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

    /// <summary>
    /// Shot from somewhere unseen: turn toward it and go look. Without this a bot
    /// can be shot in the back repeatedly and never react, which looks broken.
    /// </summary>
    void HandleDamaged(GameObject attacker, int amount)
    {
        if (attacker == null || target != null) return;

        investigatePoint = attacker.transform.position;
        investigateUntil = Time.time + 5f;
        targetYaw = YawTowards(investigatePoint);

        state = ShouldRetreat() ? State.Retreat : State.Investigate;
        if (state == State.Retreat) calmDownAt = Time.time + 3.5f;
    }

    // ---------- behaviours ----------

    void WanderBehaviour()
    {
        steerTimer -= Time.deltaTime;

        if (steerTimer <= 0f || IsBlockedAhead())
            PickNewHeading();

        RotateTowardYaw(targetYaw);
        Move(transform.forward, profile.moveSpeed * 0.8f);
    }

    void ChaseBehaviour()
    {
        if (target == null) { state = State.Wander; return; }

        targetYaw = YawTowards(target.transform.position);
        RotateTowardYaw(targetYaw);

        // Sidestep obstacles rather than grinding into them on the way over.
        Vector3 direction = IsBlockedAhead()
            ? Quaternion.Euler(0f, 55f * strafeDirection, 0f) * transform.forward
            : transform.forward;

        Move(direction, profile.moveSpeed);
        TryShoot();
    }

    void EngageBehaviour()
    {
        if (target == null) { state = State.Wander; return; }

        FaceTarget();

        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        if (Time.time >= nextStrafeFlip)
        {
            nextStrafeFlip = Time.time + Random.Range(0.8f, 2f);
            strafeDirection = -strafeDirection;
        }

        // Circle the target while holding roughly the preferred range: standing
        // still in a firefight is what makes a bot trivial to hit.
        Vector3 forward = toTarget.normalized;
        Vector3 sideways = Vector3.Cross(Vector3.up, forward) * strafeDirection;

        float approach = Mathf.Clamp((distance - preferredCombatRange) / preferredCombatRange, -1f, 1f);
        Vector3 direction = (sideways + forward * approach).normalized;

        if (IsBlocked(direction, obstacleCheckDistance))
        {
            strafeDirection = -strafeDirection;
            direction = (Vector3.Cross(Vector3.up, forward) * strafeDirection).normalized;
        }

        Move(direction, profile.moveSpeed * 0.85f);
        TryShoot();
    }

    void RetreatBehaviour()
    {
        Vector3 away = target != null
            ? transform.position - target.transform.position
            : transform.position - investigatePoint;

        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = transform.forward;

        Vector3 direction = away.normalized;
        if (IsBlocked(direction, obstacleCheckDistance))
        {
            // Cornered: peel off sideways instead of pressing into the wall.
            direction = (Quaternion.Euler(0f, 70f * strafeDirection, 0f) * direction).normalized;
            strafeDirection = -strafeDirection;
        }

        targetYaw = Quaternion.LookRotation(direction).eulerAngles.y;
        RotateTowardYaw(targetYaw);
        Move(direction, profile.moveSpeed * 1.05f);

        // Still shoots while backing away, just without pressing the attack.
        if (target != null) TryShoot();

        if (Time.time >= calmDownAt && !ShouldRetreat())
            state = State.Wander;
    }

    void InvestigateBehaviour()
    {
        if (Time.time >= investigateUntil)
        {
            state = State.Wander;
            PickNewHeading();
            return;
        }

        Vector3 toPoint = investigatePoint - transform.position;
        toPoint.y = 0f;

        if (toPoint.magnitude < 1.5f)
        {
            state = State.Wander;
            PickNewHeading();
            return;
        }

        targetYaw = Quaternion.LookRotation(toPoint.normalized).eulerAngles.y;
        RotateTowardYaw(targetYaw);

        Vector3 direction = IsBlockedAhead()
            ? Quaternion.Euler(0f, 60f * strafeDirection, 0f) * transform.forward
            : transform.forward;

        Move(direction, profile.moveSpeed * 0.9f);
    }

    // ---------- shooting ----------

    void TryShoot()
    {
        if (target == null || !target.IsAlive) return;

        // Reaction delay: never fire the instant a target appears.
        if (Time.time - targetAcquiredAt < profile.reactionTime) return;
        if (Time.time < nextFireTime) return;

        Vector3 aimPoint = PredictAimPoint();
        Vector3 toTarget = aimPoint - EyePosition;

        Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
        if (Vector3.Angle(transform.forward, flat) > aimTolerance) return;

        nextFireTime = Time.time + profile.fireCooldown;

        ShotResult shot = Hitscan.Fire(gameObject, EyePosition, toTarget.normalized,
                                       range, profile.damage, profile.spreadDegrees);
        PlayShotFeedback(shot);
    }

    /// <summary>
    /// Leads a moving target. Only the harder profiles do this meaningfully,
    /// which is a large part of why they feel sharper.
    /// </summary>
    Vector3 PredictAimPoint()
    {
        Vector3 aimPoint = target.transform.position + Vector3.up * eyeHeight;

        if (profile.aimPrediction > 0f)
        {
            Vector3 targetVelocity = (target.transform.position - previousTargetPosition) / scanInterval;
            aimPoint += targetVelocity * profile.aimPrediction;
        }

        previousTargetPosition = target.transform.position;
        return aimPoint;
    }

    void PlayShotFeedback(ShotResult shot)
    {
        Vector3 muzzlePosition = muzzle != null ? muzzle.position : EyePosition;

        if (GameAudio.Instance != null) GameAudio.Instance.PlayBotShot(muzzlePosition);

        if (GameEffects.Instance != null)
        {
            GameEffects.Instance.MuzzleFlash(muzzlePosition, transform.forward);
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

    // ---------- movement helpers ----------

    void Move(Vector3 direction, float speed)
    {
        controller.Move(direction.normalized * speed * Time.deltaTime);
    }

    void FaceTarget()
    {
        if (target == null) return;

        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.001f) return;

        Quaternion desired = Quaternion.LookRotation(toTarget);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, desired,
                                                      profile.turnSpeed * Time.deltaTime);
    }

    float YawTowards(Vector3 point)
    {
        Vector3 direction = point - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return transform.eulerAngles.y;

        return Quaternion.LookRotation(direction).eulerAngles.y;
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
        transform.rotation = Quaternion.RotateTowards(transform.rotation, desired,
                                                      profile.turnSpeed * Time.deltaTime);
    }

    void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(Vector3.up * velocity.y * Time.deltaTime);
    }
}
