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
                return english ? "Destroy the jammer before the protected target." : "Сначала уничтожь источник помех.";
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
                GameObject mast = GameObject.Find("Antenna");
                if (mast != null)
                {
                    jammer = mast.AddComponent<Target>();
                    jammer.SetKind(Target.Kind.SignalJammer);
                    mast.AddComponent<SignalJammer>().target = jammer;
                    AddMarker(mast.transform, "JammerMarker", new Vector3(0f, 8.9f, 0f),
                        new Vector3(1.7f, 0.35f, 0.35f));
                    Require(jammer);
                }
                protectedTarget = tank;
                if (tank != null) tank.ProtectedByJammer = jammer != null;
                Require(tank);
                break;
            case ChallengeKind.GroupStrike:
                Target secondTruck = FindAnotherTruck(truck);
                if (truck != null && secondTruck != null)
                {
                    Vector3 centre = truck.transform.position;
                    truck.transform.position = centre + Vector3.left * 3.7f;
                    secondTruck.transform.position = centre + Vector3.right * 3.7f;
                    GameObject fuel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    fuel.name = "FuelDepot";
                    fuel.transform.position = centre + Vector3.up * 1.5f;
                    fuel.transform.localScale = new Vector3(1.6f, 1.5f, 1.6f);
                    Material material = Resources.Load<Material>("Materials/Mat_RustMetal");
                    if (material != null) fuel.GetComponent<Renderer>().sharedMaterial = material;
                    Target fuelTarget = fuel.AddComponent<Target>();
                    fuelTarget.SetKind(Target.Kind.FuelDepot);
                    Require(truck);
                    Require(secondTruck);
                    Require(fuelTarget);
                }
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
    }

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
        UnityEngine.Object.Destroy(marker.GetComponent<Collider>());
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

        Vector3 centre = target.transform.position;
        foreach (Target other in required)
            if (other != target && !other.IsDestroyed &&
                Vector3.Distance(other.transform.position, centre) <= 9f)
                other.TakeDamage(130f, centre);
    }
}
