using UnityEngine;

/// <summary>
/// An electronic-warfare station. While it stands, the video link degrades
/// hard inside its radius and the armour it covers cannot be finished off.
///
/// The old version only shaved up to 45% off the link, which kept the feed
/// above the point where the HUD starts glitching — the mechanic existed only
/// in the objective text. Now the link drops to a fifth right next to the
/// station, so flying towards it the player watches the picture tear, sees
/// the "ПОМЕХИ" warning and hears it, long before reading anything. It never
/// takes the link to zero by itself: the station is a problem to solve, not
/// an invisible wall.
/// </summary>
public class SignalJammer : MonoBehaviour
{
    public static SignalJammer Active { get; private set; }
    public Target target;
    public float radius = 70f;

    /// <summary>Link multiplier right at the station, 0..1.</summary>
    public float minimumMultiplier = 0.2f;

    void OnEnable() { Active = this; }
    void OnDisable() { if (Active == this) Active = null; }

    public bool IsLive { get { return target != null && !target.IsDestroyed && isActiveAndEnabled; } }

    /// <summary>0 outside the radius or once destroyed, 1 at the station.</summary>
    public float Intensity(Vector3 dronePosition)
    {
        if (!IsLive) return 0f;
        float distance = Vector3.Distance(transform.position, dronePosition);
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - distance / radius));
    }

    public float SignalMultiplier(Vector3 dronePosition)
    {
        return Mathf.Lerp(1f, minimumMultiplier, Intensity(dronePosition));
    }
}
