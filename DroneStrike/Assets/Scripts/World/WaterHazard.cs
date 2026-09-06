using UnityEngine;

/// <summary>
/// Circular water-surface trigger. A flight controller and exposed motors do
/// not survive contact with the pond, so the mission immediately consumes the
/// current drone without playing a land explosion.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WaterHazard : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        Warhead warhead = other.GetComponentInParent<Warhead>();
        if (warhead == null || warhead.HasDetonated) return;

        warhead.Submerge();
    }
}
