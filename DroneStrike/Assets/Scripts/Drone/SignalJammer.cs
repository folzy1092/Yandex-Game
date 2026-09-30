using UnityEngine;

/// <summary>Shortens a drone's usable radio range until its mast is destroyed.</summary>
public class SignalJammer : MonoBehaviour
{
    public static SignalJammer Active { get; private set; }
    public Target target;
    public float radius = 85f;
    public float signalPenalty = 0.45f;

    void OnEnable() { Active = this; }
    void OnDisable() { if (Active == this) Active = null; }

    public float SignalMultiplier(Vector3 dronePosition)
    {
        if (target == null || target.IsDestroyed) return 1f;
        float distance = Vector3.Distance(transform.position, dronePosition);
        return 1f - Mathf.Clamp01(1f - distance / radius) * signalPenalty;
    }
}
