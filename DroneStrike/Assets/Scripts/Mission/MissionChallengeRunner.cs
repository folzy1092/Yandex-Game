using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Maps one authored challenge onto the already built Outpost scene.</summary>
public class MissionChallengeRunner
{
    readonly List<Target> required = new List<Target>();
    readonly List<Target> available = new List<Target>();
    Target jammer;
    Target protectedTarget;
    PatrolMover escapeTruck;
    int tutorialStage;
    int variant;

    public MissionDefinition Definition { get; private set; }
    public bool Failed { get; private set; }
    public string FailureHint
    {
        get
        {
            if (!Failed) return string.Empty;
            bool english = Localization.Current == Localization.Language.English;
            if (Definition.kind == ChallengeKind.Intercept)
                return english ? "The patrol escaped the sector." : "Патруль вышел из сектора.";
            if (Definition.kind == ChallengeKind.WeakSpot)
                return english ? "Finish the armour through its marked rear." : "Добей бронецель через отмеченную корму.";
            if (Definition.kind == ChallengeKind.Jammer)
                return english ? "Destroy the jammer station first." : "Сначала уничтожь станцию помех.";
            return english ? "Objective failed." : "Задание провалено.";
        }
    }
    public int RequiredCount { get { return required.Count; } }
    public Target PrimaryTarget { get { return required.Count > 0 ? required[0] : null; } }
    public bool TutorialComplete { get { return tutorialStage >= 3; } }

    public string TutorialPrompt
    {
        get
        {
            if (Definition.kind != ChallengeKind.FirstFlight || TutorialComplete) return Definition.Brief;
            bool english = Localization.Current == Localization.Language.English;
            if (tutorialStage == 0) return english ? "Press SPACE to climb" : "Нажми SPACE, чтобы подняться";
            if (tutorialStage == 1) return english ? "Press SHIFT to descend" : "Нажми SHIFT, чтобы снизиться";
            return english ? "Move the mouse to look around" : "Поверни камеру мышью";
        }
    }

    public void Configure(MissionDefinition definition, int layoutVariant)
    {
        Definition = definition;
        variant = layoutVariant;
        available.AddRange(UnityEngine.Object.FindObjectsByType<Target>(FindObjectsSortMode.None));
        available.Sort((a, b) =>
        {
            int kind = a.kind.CompareTo(b.kind);
            if (kind != 0) return kind;
            int x = a.transform.position.x.CompareTo(b.transform.position.x);
            return x != 0 ? x : a.transform.position.z.CompareTo(b.transform.position.z);
        });

        Target truck = Find(Target.Kind.LightVehicle, false);
        Target patrol = Find(Target.Kind.LightVehicle, true);
        Target tank = Find(Target.Kind.ArmouredVehicle, false);
        Target depot = Find(Target.Kind.SupplyDepot, false);

        switch (definition.kind)
        {
            case ChallengeKind.FirstFlight:
                Require(truck);
                break;
            case ChallengeKind.Intercept:
                Require(patrol);
                escapeTruck = patrol != null ? patrol.GetComponent<PatrolMover>() : null;
                if (escapeTruck != null)
                {
                    escapeTruck.maxWaypointsPassed = escapeTruck.waypoints.Length;
                    escapeTruck.OnEscaped += () => Failed = true;
                }
                break;
            case ChallengeKind.WeakSpot:
                Require(tank);
                if (tank != null)
                {
                    tank.WeakRear = true;
                    AddMarker(tank.transform, "WeakRear", new Vector3(0f, 1.3f, -2.9f),
                        new Vector3(2.2f, 0.28f, 0.15f));
                }
                break;
            case ChallengeKind.Jammer:
                protectedTarget = tank;
                Require(tank);
                break;
            case ChallengeKind.GroupStrike:
                Require(truck);
                Require(FindAnotherTruck(truck));
                break;
            case ChallengeKind.FreePlan:
                Require(truck);
                Require(tank);
                Require(depot);
                break;
        }

        // Free Plan leaves the surrounding targets as optional choices.
        if (definition.kind != ChallengeKind.FreePlan)
            foreach (Target target in available)
                if (!required.Contains(target)) target.gameObject.SetActive(false);

        // Props are laid out after the unused targets are gone, so the free
        // ground search sees the scene exactly as the player will.
        Physics.SyncTransforms();
        if (definition.kind == ChallengeKind.Jammer) BuildJammer(tank);
        if (definition.kind == ChallengeKind.GroupStrike && required.Count >= 2)
            BuildFuelGroup(required[0], required[1]);
    }

    // ---------- jammer ----------

    /// <summary>
    /// Stands the EW station 15-21 m from the armour it covers, on the first
    /// free bearing: close enough that attacking the tank means flying
    /// through the jamming, far enough that one blast cannot take both.
    /// </summary>
    void BuildJammer(Target tank)
    {
        if (tank == null) return;
        Vector3 centre = tank.transform.position;
        Vector3 spot = centre + tank.transform.right * 17f;
        bool found = false;
        for (float distance = 15f; distance <= 21f && !found; distance += 3f)
            for (int step = 0; step < 18 && !found; step++)
            {
                float bearing = step * 20f + variant * 37f;
                Vector3 candidate = centre + Quaternion.Euler(0f, bearing, 0f) * Vector3.forward * distance;
                candidate.y = GroundHeight(candidate);
                if (!IsClear(candidate, Quaternion.identity, new Vector3(2.2f, 3f, 2.2f), tank)) continue;
                spot = candidate;
                found = true;
            }

        Vector3 toTank = centre - spot;
        float yaw = Mathf.Atan2(toTank.x, toTank.z) * Mathf.Rad2Deg;
        jammer = FieldProps.JammerStation(spot, yaw);
        var field = jammer.gameObject.AddComponent<SignalJammer>();
        field.target = jammer;
        FieldProps.JamWave(jammer.transform, field.radius);
        // The station is the first step, so it leads the objective list.
        jammer.IsPriority = true;
        required.Insert(0, jammer);
        tank.ProtectedByJammer = true;
    }

    // ---------- fuel ----------

    const float TruckGap = 6.4f;

    /// <summary>
    /// Parks the two trucks side by side either side of a small fuel cache,
    /// with a clear gap to each, inside the cache's blast radius - and draws
    /// that radius on the ground, so the chain reaction is something the
    /// player plans rather than something that happens to them.
    /// </summary>
    void BuildFuelGroup(Target first, Target second)
    {
        Vector3 centre = first.transform.position;
        float bestYaw = first.transform.eulerAngles.y;
        for (int step = 0; step < 24; step++)
        {
            float yaw = first.transform.eulerAngles.y + step * 15f;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 right = rotation * Vector3.right;
            if (!IsClear(centre, rotation, new Vector3(2f, 0.8f, 1.6f), first, second)) continue;
            if (!IsClear(centre + right * TruckGap, rotation, new Vector3(1.5f, 1.7f, 3.8f), first, second)) continue;
            if (!IsClear(centre - right * TruckGap, rotation, new Vector3(1.5f, 1.7f, 3.8f), first, second)) continue;
            bestYaw = yaw;
            break;
        }

        Quaternion parked = Quaternion.Euler(0f, bestYaw, 0f);
        Vector3 side = parked * Vector3.right;
        Vector3 leftSpot = centre - side * TruckGap;
        Vector3 rightSpot = centre + side * TruckGap;
        leftSpot.y = GroundHeight(leftSpot);
        rightSpot.y = GroundHeight(rightSpot);
        first.transform.SetPositionAndRotation(leftSpot, parked);
        second.transform.SetPositionAndRotation(rightSpot, parked * Quaternion.Euler(0f, 180f, 0f));

        centre.y = GroundHeight(centre);
        Target fuel = FieldProps.FuelCache(centre, bestYaw);
        FieldProps.DangerRing(fuel.transform, FieldProps.FuelBlastRadius);
        Require(fuel);
        Physics.SyncTransforms();
    }

    // ---------- placement ----------

    static float GroundHeight(Vector3 position)
    {
        RaycastHit hit;
        GameObject terrain = GameObject.Find("Terrain");
        Collider ground = terrain != null ? terrain.GetComponent<Collider>() : null;
        if (ground != null && ground.Raycast(new Ray(position + Vector3.up * 60f, Vector3.down), out hit, 200f))
            return hit.point.y;
        return position.y;
    }

    /// <summary>
    /// True if a box standing on the ground at <paramref name="foot"/> touches
    /// nothing solid except the terrain and the given targets, and keeps clear
    /// of the patrol road.
    /// </summary>
    bool IsClear(Vector3 foot, Quaternion rotation, Vector3 halfExtents, params Target[] ignore)
    {
        Vector3 centre = foot + Vector3.up * (halfExtents.y + 0.15f);
        foreach (Collider hit in Physics.OverlapBox(centre, halfExtents + new Vector3(0.6f, 0f, 0.6f),
                                                     rotation, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.gameObject.name == "Terrain") continue;
            Target owner = hit.GetComponentInParent<Target>();
            if (owner != null && System.Array.IndexOf(ignore, owner) >= 0) continue;
            return false;
        }
        return RoadDistance(foot) >= 8.5f + Mathf.Max(halfExtents.x, halfExtents.z);
    }

    float RoadDistance(Vector3 point)
    {
        if (road == null)
        {
            foreach (Target target in available)
            {
                PatrolMover mover = target != null ? target.GetComponent<PatrolMover>() : null;
                if (mover != null && mover.waypoints != null && mover.waypoints.Length > 1)
                {
                    road = mover.waypoints;
                    break;
                }
            }
            if (road == null) road = new Vector3[0];
        }

        float best = float.PositiveInfinity;
        var p = new Vector2(point.x, point.z);
        for (int i = 0; i < road.Length; i++)
        {
            var a = new Vector2(road[i].x, road[i].z);
            Vector3 next = road[(i + 1) % road.Length];
            var b = new Vector2(next.x, next.z);
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
        }
        return best;
    }

    Vector3[] road;

    Target Find(Target.Kind kind, bool moving)
    {
        var matches = new List<Target>();
        foreach (Target target in available)
            if (target.kind == kind && (target.GetComponent<PatrolMover>() != null) == moving)
                matches.Add(target);
        return matches.Count > 0 ? matches[variant % matches.Count] : null;
    }

    Target FindAnotherTruck(Target first)
    {
        foreach (Target target in available)
            if (target != first && target.kind == Target.Kind.LightVehicle &&
                target.GetComponent<PatrolMover>() == null) return target;
        return null;
    }

    void Require(Target target)
    {
        if (target == null || required.Contains(target)) return;
        target.IsPriority = true;
        required.Add(target);
    }

    static void AddMarker(Transform parent, string name, Vector3 localPosition, Vector3 scale)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = name;
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = localPosition;
        marker.transform.localScale = scale;
        FieldProps.Discard(marker.GetComponent<Collider>());
        Material material = Resources.Load<Material>("Materials/Mat_Highlight");
        if (material != null) marker.GetComponent<Renderer>().sharedMaterial = material;
    }

    public void UpdateTutorial(bool canPilot)
    {
        if (!canPilot || Definition.kind != ChallengeKind.FirstFlight || TutorialComplete) return;
        if (tutorialStage == 0 && Input.GetKey(KeyCode.Space)) tutorialStage++;
        else if (tutorialStage == 1 && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) tutorialStage++;
        else if (tutorialStage == 2 &&
                 Mathf.Abs(Input.GetAxis("Mouse X")) + Mathf.Abs(Input.GetAxis("Mouse Y")) > 0.2f)
            tutorialStage++;
    }

    public bool IsRequired(Target target) { return required.Contains(target); }

    public void OnTargetDestroyed(Target target)
    {
        if (Definition.kind == ChallengeKind.WeakSpot && target == PrimaryTarget &&
            !target.DestroyedViaWeakSpot)
            Failed = true;

        if (Definition.kind == ChallengeKind.Jammer && target == protectedTarget &&
            jammer != null && !jammer.IsDestroyed)
            Failed = true;

        if (target == jammer && protectedTarget != null)
            protectedTarget.ProtectedByJammer = false;

        if (Definition.kind != ChallengeKind.GroupStrike || target.kind != Target.Kind.FuelDepot)
            return;

        // The cache goes up in a fireball of its own and takes every vehicle
        // inside the ring on the ground with it.
        Vector3 centre = target.transform.position;
        if (GameEffects.Instance != null)
            GameEffects.Instance.Explosion(centre + Vector3.up, FieldProps.FuelBlastRadius * 0.8f);
        if (GameAudio.Instance != null) GameAudio.Instance.PlayExplosion(centre, WarheadType.Heavy);
        foreach (Target other in required.ToArray())
            if (other != target && !other.IsDestroyed &&
                Vector3.Distance(other.transform.position, centre) <= FieldProps.FuelBlastRadius)
                other.TakeDamage(130f, centre);
    }
}
