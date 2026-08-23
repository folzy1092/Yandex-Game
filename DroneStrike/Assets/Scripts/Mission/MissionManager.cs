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
    public GameObject dronePrefabRoot;
    public int droneCount = 3;

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

    readonly List<Target> targets = new List<Target>();

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

    void LaunchDrone()
    {
        if (ActiveDrone != null) Destroy(ActiveDrone.gameObject);

        Vector3 position = launchPoint != null ? launchPoint.position : Vector3.up * 2f;
        Quaternion rotation = launchPoint != null ? launchPoint.rotation : Quaternion.identity;

        ActiveDrone = DroneFactory.Create(position, rotation);
        ActiveDrone.SignalLink.SetLaunchPoint(position);

        // Every way of losing a drone funnels through the same handler, so the
        // rules stay in one place no matter how it happened.
        ActiveDrone.Warhead.OnDetonated += HandleDroneLost;
        ActiveDrone.Battery.OnDepleted += HandleDroneLost;
        ActiveDrone.SignalLink.OnLost += HandleDroneLost;
        ActiveDrone.Impact.OnCrashed += HandleDroneLost;

        Notify();
    }

    void HandleDroneLost()
    {
        if (!IsRunning) return;

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

    /// <summary>Adds a drone to the rack. Used by the rewarded ad on later stages.</summary>
    public void GrantExtraDrone()
    {
        DronesRemaining++;
        Notify();

        if (IsRunning && ActiveDrone == null) LaunchDrone();
    }

    // ---------- mission end ----------

    void EndMission(bool won)
    {
        if (!IsRunning) return;

        IsRunning = false;

        // The results screen has buttons, so the mouse has to come back.
        LockCursor(false);

        if (OnMissionEnded != null) OnMissionEnded(won);
    }

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void Notify()
    {
        if (OnStateChanged != null) OnStateChanged();
    }
}
