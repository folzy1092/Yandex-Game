using UnityEngine;

/// <summary>
/// An electronic-warfare station. While it stands, the video link degrades
/// inside its radius and the armour it covers cannot be finished off.
///
/// The field has three phases, so closing in is a decision rather than a
/// uniform nuisance:
///   1. ПОМЕХИ       outer ring — light snow, occasional tears
///   2. СИЛЬНЫЕ      middle — heavy snow, the feed jumps, the drone drifts
///   3. ПОДАВЛЕНИЕ   close in — near white-out, strong drift; the link
///                   hangs by a thread (only range loss can finish it)
/// The station never drops the link on its own: it is a problem to fly
/// through and destroy, not an invisible wall.
/// </summary>
public class SignalJammer : MonoBehaviour
{
    public static SignalJammer Active { get; private set; }
    public Target target;
    public float radius = 95f;

    /// <summary>Link multiplier right at the station, 0..1.</summary>
    public float minimumMultiplier = 0.06f;

    /// <summary>Intensity thresholds where phase 2 and phase 3 begin.</summary>
    public const float HeavyFrom = 0.4f;
    public const float SuppressionFrom = 0.72f;

    void OnEnable() { Active = this; }
    void OnDisable() { if (Active == this) Active = null; }

    public bool IsLive { get { return target != null && !target.IsDestroyed && isActiveAndEnabled; } }

    /// <summary>0 outside the radius or once destroyed, 1 at the station. Rises faster the closer you get.</summary>
    public float Intensity(Vector3 dronePosition)
    {
        if (!IsLive) return 0f;
        float distance = Vector3.Distance(transform.position, dronePosition);
        float t = Mathf.Clamp01(1f - distance / radius);
        return Mathf.Pow(t, 0.75f);
    }

    /// <summary>0 = clear, 1 = jamming, 2 = heavy, 3 = suppression.</summary>
    public static int PhaseOf(float intensity)
    {
        if (intensity <= 0.04f) return 0;
        if (intensity < HeavyFrom) return 1;
        return intensity < SuppressionFrom ? 2 : 3;
    }

    public float SignalMultiplier(Vector3 dronePosition)
    {
        return Mathf.Lerp(1f, minimumMultiplier, Intensity(dronePosition));
    }
}
