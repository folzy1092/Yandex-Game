using UnityEngine;

/// <summary>
/// One assembled drone: the components that make it up, gathered behind a
/// single reference so the mission can wire itself to a new drone in one line
/// instead of hunting for each part.
/// </summary>
public class DroneRig : MonoBehaviour
{
    public DroneController Controller { get; private set; }
    public Warhead Warhead { get; private set; }
    public DroneBattery Battery { get; private set; }
    public SignalLink SignalLink { get; private set; }
    public DroneImpact Impact { get; private set; }
    public Camera View { get; private set; }

    void Awake()
    {
        Controller = GetComponent<DroneController>();
        Warhead = GetComponent<Warhead>();
        Battery = GetComponent<DroneBattery>();
        SignalLink = GetComponent<SignalLink>();
        Impact = GetComponent<DroneImpact>();
        View = GetComponentInChildren<Camera>();
    }
}
