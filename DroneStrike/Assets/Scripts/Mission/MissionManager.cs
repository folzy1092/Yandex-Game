using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum DroneLossCause { Impact, Obstacle, Battery, Signal, Water }

public struct DroneLossReport
{
    public DroneLossCause cause;
    public AttackReport attack;
}

/// <summary>
/// Runs the mission: tracks the targets, hands out drones, and decides when it
/// is won or lost.
///
/// Losing a drone is not losing the mission — you get the next one from the
/// rack and keep going, and any targets already destroyed stay destroyed. The
/// mission is only lost when the rack is empty, which is what makes the
/// "one more drone" reward worth watching an ad for later on.
/// </summary>
public class MissionManager : MonoBehaviour
{
    [Flags]
    public enum PauseReason { None = 0, Manual = 1, Focus = 2, Advertisement = 4, Result = 8 }

    public static MissionManager Instance { get; private set; }

    [Header("Setup")]
    public Transform launchPoint;

    /// <summary>
    /// Every pad the next drone might launch from. Filled by the scene builder.
    ///
    /// A fixed launch point means every run starts with the same approach flown
    /// from the same angle, and by the fourth drone the player is repeating a
    /// memorised line rather than flying. Rotating the pad around the position
    /// makes each life a fresh problem without touching the map itself.
    /// </summary>
    public Transform[] launchPoints = new Transform[0];
    public GameObject dronePrefabRoot;
    public int droneCount = 3;

    /// <summary>
    /// Which charge the drones carry. Set from the briefing screen at startup;
    /// the value in the scene is only the fallback for pressing Play straight
    /// into the mission without going through the menu.
    /// </summary>
    public WarheadType warhead = WarheadType.Compact;

    /// <summary>
    /// How many extra drones a rewarded ad has already granted this mission.
    /// Capped, so a player cannot grind an unlimited rack out of the ad slot —
    /// which would both wreck the difficulty and get the placement flagged.
    /// </summary>
    public int ExtraDronesGranted { get; private set; }

    public const int MaxExtraDrones = 3;

    public bool CanRequestExtraDrone { get { return challenge == null && ExtraDronesGranted < MaxExtraDrones; } }

    /// <summary>Seconds between losing a drone and the next one launching.</summary>
    public float relaunchDelay = 2.5f;

    public int TargetsTotal { get; private set; }
    public int TargetsDestroyed { get; private set; }
    public int DronesRemaining { get; private set; }
    public int Score { get; private set; }
    public int DronesUsed { get; private set; }
    public float ElapsedTime { get; private set; }
    public float ActiveFlightTime { get; private set; }
    public float WaitingTime { get; private set; }
    public DroneLossReport LastDroneReport { get; private set; }
    public bool HasChallenge { get { return challenge != null; } }
    public MissionDefinition Challenge { get { return challenge != null ? challenge.Definition : default(MissionDefinition); } }
    public int ChallengeIndex { get; private set; }
    public string ObjectiveHint { get { return challenge != null ? challenge.TutorialPrompt : string.Empty; } }
    public string FailureHint { get { return challenge != null ? challenge.FailureHint : string.Empty; } }
    public bool IsRunning { get; private set; }
    public PauseReason PauseReasons { get; private set; }
    public bool CanPilot { get { return IsRunning && PauseReasons == PauseReason.None
        && Cursor.lockState == CursorLockMode.Locked && !YandexAds.IsBusy; } }

    /// <summary>The drone currently being flown, or null between launches.</summary>
    public DroneRig ActiveDrone { get; private set; }

    public event Action OnStateChanged;
    public event Action OnPauseChanged;

    /// <summary>Fired with (won) when the mission ends.</summary>
    public event Action<bool> OnMissionEnded;

    /// <summary>Fired when the active drone loses its link, for the on-screen warning.</summary>
    public event Action OnSignalLost;
    public event Action<DroneLossReport> OnDroneReported;

    readonly List<Target> targets = new List<Target>();

    /// <summary>
    /// Guards against counting the same drone twice. A drone can meet several
    /// ends at once — the link drops, it falls, and the impact and the
    /// self-destruct both fire — and without this the rack would empty several
    /// times over from a single loss.
    /// </summary>
    bool activeDroneLost;
    bool signalLostOnActiveDrone;
    bool victoryPending;
    AttackReport activeAttack;
    bool firstHitLogged;

    // What the current drone's strike destroyed, including chain reactions
    // that happen inside the blast callback. Reset at every launch.
    int strikePoints;
    int strikeVehicles;
    bool strikeFuel;
    MissionChallengeRunner challenge;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            Time.timeScale = 1f;
        }
    }

    void Start()
    {
        // What the player picked in the briefing wins over whatever the scene
        // was saved with.
        warhead = DroneLoadout.SelectedWarhead;

        if (SceneManager.GetActiveScene().name == "Mission1")
        {
            ChallengeIndex = MissionChallenges.SelectedIndex;
            challenge = new MissionChallengeRunner();
            challenge.Configure(MissionChallenges.Definitions[ChallengeIndex], MissionChallenges.Variant);
            relaunchDelay = 1.25f;
        }

        CollectTargets();
        DronesRemaining = challenge != null ? challenge.Definition.droneBudget : droneCount;
        IsRunning = true;

        // The mouse aims the camera, so it has to be captured — otherwise it
        // leaves the window mid-flight and aiming simply stops working.
        LockCursor(true);

        LaunchDrone();
        Notify();
        LogEvent("mission_start", "");
    }

    void Update()
    {
        if (IsRunning && PauseReasons == PauseReason.None)
        {
            ElapsedTime += Time.deltaTime;
            if (CanPilot && ActiveDrone != null && !activeDroneLost)
                ActiveFlightTime += Time.deltaTime;
            else WaitingTime += Time.deltaTime;
        }
        if (challenge != null && IsRunning)
            challenge.UpdateTutorial(CanPilot);
        // Browsers can release pointer lock without sending Esc to Unity.
        // Freeze the mission until the player explicitly presses Continue.
        if (IsRunning && PauseReasons == PauseReason.None &&
            Cursor.lockState != CursorLockMode.Locked)
            SetPause(PauseReason.Focus, true);
    }

    void LateUpdate()
    {
        // A target can fail an order-sensitive objective inside the blast
        // callback. Wait until the blast report and drone-loss event finish,
        // then let failure take precedence over the same-frame target count.
        if (challenge != null && challenge.Failed && IsRunning)
        {
            victoryPending = false;
            EndMission(false);
            return;
        }
        // Damage callbacks can finish the last target inside Warhead.ApplyBlast.
        // Resolve victory after the warhead has emitted its complete report.
        if (victoryPending && IsRunning)
        {
            victoryPending = false;
            EndMission(true);
        }
    }

    public void SetPause(PauseReason reason, bool active)
    {
        PauseReason next = active ? PauseReasons | reason : PauseReasons & ~reason;
        if (next == PauseReasons) return;
        PauseReasons = next;
        Time.timeScale = PauseReasons == PauseReason.None ? 1f : 0f;
        if (PauseReasons != PauseReason.None) LockCursor(false);
        if (OnPauseChanged != null) OnPauseChanged();
    }

    public void ResumeFromPlayer()
    {
        if (!IsRunning || (PauseReasons & (PauseReason.Advertisement | PauseReason.Result)) != 0)
            return;
        if (!YandexAds.HasFocus) return;
        LockCursor(true);
        if (Cursor.lockState != CursorLockMode.Locked) return;
        SetPause(PauseReason.Manual | PauseReason.Focus, false);
    }

    static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void CollectTargets()
    {
        targets.Clear();
        targets.AddRange(FindObjectsByType<Target>(FindObjectsSortMode.None));

        foreach (Target target in targets)
            target.OnDestroyed += HandleTargetDestroyed;

        TargetsTotal = challenge != null ? challenge.RequiredCount : targets.Count;
    }

    public Target ClosestRemainingTarget(Vector3 position)
    {
        Target closest = null;
        float best = float.PositiveInfinity;
        foreach (Target target in targets)
        {
            if (target == null || target.IsDestroyed ||
                (challenge != null && !challenge.IsRequired(target))) continue;
            float distance = (target.transform.position - position).sqrMagnitude;
            if (distance >= best) continue;
            best = distance;
            closest = target;
        }
        return closest;
    }

    void HandleTargetDestroyed(Target target, int points)
    {
        if (challenge == null || challenge.IsRequired(target)) TargetsDestroyed++;
        Score += points;
        strikePoints += points;
        if (target.kind == Target.Kind.FuelDepot) strikeFuel = true;
        else if (target.kind == Target.Kind.LightVehicle || target.kind == Target.Kind.ArmouredVehicle)
            strikeVehicles++;
        if (challenge != null) challenge.OnTargetDestroyed(target);
        Notify();

        if (TargetsTotal > 0 && TargetsDestroyed >= TargetsTotal) victoryPending = true;
    }

    // ---------- drones ----------

    /// <summary>Index of the pad used last, so the next one is never the same.</summary>
    int lastLaunchPad = -1;

    Transform PickLaunchPad()
    {
        if (launchPoints == null || launchPoints.Length == 0) return launchPoint;

        if (lastLaunchPad < 0 && challenge != null &&
            challenge.Definition.kind == ChallengeKind.FirstFlight &&
            challenge.PrimaryTarget != null)
        {
            int nearest = 0;
            float best = float.PositiveInfinity;
            for (int i = 0; i < launchPoints.Length; i++)
            {
                if (launchPoints[i] == null) continue;
                float distance = (launchPoints[i].position -
                    challenge.PrimaryTarget.transform.position).sqrMagnitude;
                if (distance < best) { best = distance; nearest = i; }
            }
            lastLaunchPad = nearest;
            return launchPoints[nearest] != null ? launchPoints[nearest] : launchPoint;
        }

        // One pad is not a choice; two or more must not repeat.
        if (launchPoints.Length == 1) return launchPoints[0];

        int index = UnityEngine.Random.Range(0, launchPoints.Length - (lastLaunchPad >= 0 ? 1 : 0));
        if (lastLaunchPad >= 0 && index >= lastLaunchPad) index++;

        lastLaunchPad = index;
        return launchPoints[index] != null ? launchPoints[index] : launchPoint;
    }

    void LaunchDrone()
    {
        if (ActiveDrone != null) Destroy(ActiveDrone.gameObject);

        Transform pad = PickLaunchPad();

        Vector3 position = pad != null ? pad.position : Vector3.up * 2f;
        Quaternion rotation = pad != null ? pad.rotation : Quaternion.identity;

        ActiveDrone = DroneFactory.Create(position, rotation, warhead);
        ActiveDrone.SignalLink.SetLaunchPoint(position);

        activeDroneLost = false;
        signalLostOnActiveDrone = false;
        activeAttack = new AttackReport();
        strikePoints = 0;
        strikeVehicles = 0;
        strikeFuel = false;

        // Every way of losing a drone funnels through the same handler.
        ActiveDrone.Warhead.OnImpactReport += HandleImpactReport;
        ActiveDrone.Warhead.OnDetonated += HandleDetonation;
        ActiveDrone.Battery.OnDepleted += HandleBatteryLoss;
        ActiveDrone.Impact.OnCrashed += HandleCrash;

        // Losing the link is not a loss by itself: the drone falls and its
        // payload self-destructs, and that detonation is what counts. This only
        // raises the warning on screen.
        ActiveDrone.SignalLink.OnLost += HandleSignalLost;

        Notify();
    }

    void HandleSignalLost()
    {
        signalLostOnActiveDrone = true;
        if (OnSignalLost != null) OnSignalLost();
    }

    void HandleImpactReport(AttackReport report)
    {
        report.pointsEarned = strikePoints;
        report.vehiclesDestroyed = strikeVehicles;
        report.fuelDetonated = strikeFuel;
        activeAttack = report;
        if (!firstHitLogged && report.targetsHit > 0)
        {
            firstHitLogged = true;
            LogEvent("first_hit", "damage=" + Mathf.RoundToInt(report.damage));
        }
    }
    void HandleDetonation() { HandleDroneLost(ActiveDrone != null && ActiveDrone.Warhead.WasSubmerged
        ? DroneLossCause.Water : signalLostOnActiveDrone ? DroneLossCause.Signal
        : activeAttack.targetsHit > 0 ? DroneLossCause.Impact : DroneLossCause.Obstacle); }
    void HandleBatteryLoss() { HandleDroneLost(DroneLossCause.Battery); }
    void HandleCrash() { HandleDroneLost(DroneLossCause.Obstacle); }

    void HandleDroneLost(DroneLossCause cause)
    {
        if (!IsRunning || activeDroneLost) return;
        activeDroneLost = true;

        LastDroneReport = new DroneLossReport { cause = cause, attack = activeAttack };
        LogEvent("drone_lost", "cause=" + cause);
        if (OnDroneReported != null) OnDroneReported(LastDroneReport);

        if (challenge == null || challenge.TutorialComplete) DronesRemaining--;
        DronesUsed++;
        Notify();

        if (DronesRemaining <= 0)
        {
            StartCoroutine(EndAfterDelay(false));
            return;
        }

        StartCoroutine(RelaunchAfterDelay());
    }

    IEnumerator RelaunchAfterDelay()
    {
        yield return new WaitForSeconds(relaunchDelay);
        if (IsRunning) LaunchDrone();
    }

    IEnumerator EndAfterDelay(bool won)
    {
        // A beat before the results screen, so the last explosion is actually seen.
        yield return new WaitForSeconds(relaunchDelay);
        EndMission(won);
    }

    /// <summary>
    /// Shows a rewarded ad and, if it was watched through, puts another drone in
    /// the rack and resumes the mission.
    ///
    /// This is the second half of the monetisation and the half that matters:
    /// it is offered exactly when the player has just lost and wants one more
    /// go, which is the only moment an ad is genuinely worth something to them.
    /// The mission is revived rather than restarted, so every target already
    /// destroyed stays destroyed.
    /// </summary>
    public void RequestExtraDrone(Action<bool> onResolved = null)
    {
        if (!CanRequestExtraDrone)
        {
            if (onResolved != null) onResolved(false);
            return;
        }

        YandexAds.ShowRewarded(watched =>
        {
            if (watched) GrantExtraDrone();
            if (onResolved != null) onResolved(watched);
        });
    }

    /// <summary>Adds a drone to the rack and puts the mission back in play.</summary>
    public void GrantExtraDrone()
    {
        ExtraDronesGranted++;
        DronesRemaining++;

        // The mission has usually already ended by the time this is called —
        // that is the whole point of the offer — so it has to be revived, not
        // merely topped up.
        if (!IsRunning)
        {
            IsRunning = true;
            SetPause(PauseReason.Result, false);
        }

        Notify();

        if (ActiveDrone == null || activeDroneLost) LaunchDrone();
    }

    /// <summary>Back to the briefing screen.</summary>
    public void ReturnToMenu()
    {
        PauseReasons = PauseReason.None;
        Time.timeScale = 1f;
        LockCursor(false);
        SceneManager.LoadScene("MainMenu");
    }

    // ---------- mission end ----------

    void EndMission(bool won)
    {
        if (!IsRunning) return;

        IsRunning = false;

        // The results screen has buttons, so the mouse has to come back.
        LockCursor(false);
        SetPause(PauseReason.Result, true);

        // Clearing a map is what opens the next one without an ad.
        if (won && challenge != null)
        {
            MissionChallenges.RecordWin(ChallengeIndex, ElapsedTime, DronesUsed);
            if (ChallengeIndex == MissionChallenges.Definitions.Length - 1)
                MissionCatalog.MarkCleared(SceneManager.GetActiveScene().name);
        }
        else if (won) MissionCatalog.MarkCleared(SceneManager.GetActiveScene().name);

        if (OnMissionEnded != null) OnMissionEnded(won);
        LogEvent("mission_end", "won=" + won + " active=" + ActiveFlightTime.ToString("0.0")
            + " waiting=" + WaitingTime.ToString("0.0"));
    }

    public void Restart()
    {
        if (challenge != null) MissionChallenges.AdvanceVariant();
        // An interstitial on the transition between attempts: the one break in
        // play where an ad does not interrupt anything.
        YandexAds.ShowIntermission(() =>
            SceneManager.LoadScene(SceneManager.GetActiveScene().name));
    }

    public void NextChallenge()
    {
        if (challenge == null || ChallengeIndex + 1 >= MissionChallenges.Definitions.Length) return;
        MissionChallenges.SelectedIndex = ChallengeIndex + 1;
        LogEvent("next_mission", "next=" + MissionChallenges.Selected.id);
        YandexAds.ShowIntermission(() => SceneManager.LoadScene(SceneManager.GetActiveScene().name));
    }

    void LogEvent(string eventName, string detail)
    {
        string missionId = challenge != null ? challenge.Definition.id
            : SceneManager.GetActiveScene().name;
        Debug.Log("DroneStrikeAnalytics " + eventName + " mission=" + missionId +
            " kit=" + DroneLoadout.Selected.id + "/" + warhead +
            " elapsed=" + ElapsedTime.ToString("0.0") + " " + detail);
    }

    void Notify()
    {
        if (OnStateChanged != null) OnStateChanged();
    }
}
