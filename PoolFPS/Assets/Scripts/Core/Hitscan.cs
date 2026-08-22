using UnityEngine;

/// <summary>
/// Instant-hit shooting shared by the player weapon and the bots: cast a ray,
/// damage the first <see cref="Health"/> it lands on. Walls block the ray for
/// free because the raycast stops at whatever it hits first.
/// </summary>
public static class Hitscan
{
    /// <param name="attacker">Used for kill attribution and to avoid self-damage.</param>
    /// <param name="spreadDegrees">Random cone applied to the shot. 0 = perfectly accurate.</param>
    /// <returns>The point the shot landed on, for effects. Vector3.zero if nothing was hit.</returns>
    public static Vector3 Fire(GameObject attacker, Vector3 origin, Vector3 direction,
                               float range, int damage, float spreadDegrees)
    {
        if (spreadDegrees > 0f)
        {
            direction = Quaternion.Euler(
                Random.Range(-spreadDegrees, spreadDegrees),
                Random.Range(-spreadDegrees, spreadDegrees),
                0f) * direction;
        }

        RaycastHit hit;
        if (!Physics.Raycast(origin, direction, out hit, range))
            return Vector3.zero;

        Health health = hit.collider.GetComponentInParent<Health>();
        if (health != null && health.gameObject != attacker)
            health.TakeDamage(damage, attacker);

        return hit.point;
    }
}
