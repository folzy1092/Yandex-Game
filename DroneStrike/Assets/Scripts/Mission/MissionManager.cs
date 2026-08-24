using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    public bool CanRequestExtraDrone { get { return ExtraDronesGranted < MaxExtraDrones; } }

    /// <summary>Seconds between losing a drone and the next one launching.</summary>
    public float relaunchDelay = 2.5f;

    public int TargetsTotal { get; private set; }
    public int TargetsDestroyed { get; private set; }
    public int DronesRemaining { get; private set; }
    public int Score { get; private set; }
    public bool IsRunning { get; private set; }

    /// <summary>The drone currently being flown, or null between launches.</summary>
    public DroneRig ActiveDrone { get; private set; }

    public event Action OnStateChanged;

    /// <summary>Fired with (won) when the mission ends.</summary>
    public event Action<bool> OnMissionEnded;

    /// <summary>Fired when the active drone loses its link, for the on-screen warning.</summary>
    public event Action OnSignalLost;

    readonly List<Target> targets = new List<Target>();

    /// <summary>
    /// Guards against counting the same drone twice. A drone can meet several
    /// ends at once — the link drops, it falls, and the impact and the
    /// self-destruct both fire — and without this the rack would empty several
    /// times over from a single loss.
    /// </summary>
    bool activeDroneLost;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // What the player picked in the briefing wins over whatever the scene
        // was saved with.
        warhead = DroneLoadout.SelectedWarhead;

        CollectTargets();
        DronesRemaining = droneCount;
        IsRunning = true;

        // The mouse aims the camera, so it has to be captured — otherwise it
        // leaves the window mid-flight and aiming simply stops working.
        LockCursor(true);

        LaunchDrone();
        Notify();
    }

    void Update()
    {
        // Esc releases the mouse; clicking back in recaptures it. Browsers drop
        // pointer lock on their own, so the click path matters in a WebGL build.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            LockCursor(false);
            return;
        }

        if (IsRunning && Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
            LockCursor(true);
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

        TargetsTotal = targets.Count;
    }

    void HandleTargetDestroyed(Target target, int points)
    {
        TargetsDestroyed++;
        Score += points;
        Notify();

        if (TargetsDestroyed >= TargetsTotal) EndMission(true);
    }

    // ---------- drones ----------

    /// <summary>Index of the pad used last, so the next one is never the same.</summary>
    int lastLaunchPad = -1;

    Transform PickLaunchPad()
    {
        if (launchPoints == null || launchPoints.Length == 0) return launchPoint;

        // One pad is not a choice; two or more must not repeat.
        if (launchPoints.Length == 1) return launchPoints[0];

        int index = lastLaunchPad;
        for (int attempt = 0; attempt < 8 && index == lastLaunchPad; attempt++)
            index = UnityEngine.Random.Range(0, launchPoints.Length);

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

        // Every way of losing a drone funnels through the same handler.
        ActiveDrone.Warhead.OnDetonated += HandleDroneLost;
        ActiveDrone.Battery.OnDepleted += HandleDroneLost;
        ActiveDrone.Impact.OnCrashed += HandleDroneLost;

        // Losing the link is not a loss by itself: the drone falls and its
        // payload self-destructs, and that detonation is what counts. This only
        // raises the warning on screen.
        ActiveDrone.SignalLink.OnLost += HandleSignalLost;

        Notify();
    }

    void HandleSignalLost()
    {
        if (OnSignalLost != null) OnSignalLost();
    }

    void HandleDroneLost()
    {
        if (!IsRunning || activeDroneLost) return;
        activeDroneLost = true;

        DronesRemaining--;
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
#if UNITY_EDITOR
            // No ad network in the editor, so the revive could never be tested
            // before a build. Editor only — a shipped build grants nothing
            // without a completed view.
            watched = true;
#endif
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
            LockCursor(true);
        }

        Notify();

        if (ActiveDrone == null || activeDroneLost) LaunchDrone();
    }

    /// <summary>Back to the briefing screen.</summary>
    public void ReturnToMenu()
    {
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

        // Clearing a map is what opens the next one without an ad.
        if (won) MissionCatalog.MarkCleared(SceneManager.GetActiveScene().name);

        if (OnMissionEnded != null) OnMissionEnded(won);
    }

    public void Restart()
    {
        // An interstitial on the transition between attempts: the one break in
        // play where an ad does not interrupt anything.
        YandexAds.ShowFullscreen(_ =>
            SceneManager.LoadScene(SceneManager.GetActiveScene().name));
    }

    void Notify()
    {
        if (OnStateChanged != null) OnStateChanged();
    }
}
