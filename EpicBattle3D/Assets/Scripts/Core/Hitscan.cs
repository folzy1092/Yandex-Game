using UnityEngine;

/// <summary>
/// What a shot ran into, so the caller can play the right effect and sound.
/// </summary>
public struct ShotResult
{
    public bool hitSomething;
    public Vector3 point;
    public Vector3 normal;

    public bool hitCharacter;
    public bool wasHeadshot;
    public bool wasKill;
    public int damageDealt;
}

/// <summary>
/// Instant-hit shooting shared by the player weapon and the bots: cast a ray,
/// damage whatever hitbox it lands on. Walls block the ray for free because the
/// raycast stops at whatever it meets first.
/// </summary>
public static class Hitscan
{
    /// <param name="attacker">Used for kill attribution and to avoid self-damage.</param>
    /// <param name="spreadDegrees">Random cone applied to the shot. 0 = perfectly accurate.</param>
    public static ShotResult Fire(GameObject attacker, Vector3 origin, Vector3 direction,
                                  float range, int damage, float spreadDegrees)
    {
        var result = new ShotResult();

        if (spreadDegrees > 0f)
        {
            direction = Quaternion.Euler(
                Random.Range(-spreadDegrees, spreadDegrees),
                Random.Range(-spreadDegrees, spreadDegrees),
                0f) * direction;
        }

        RaycastHit hit;
        bool struck = Physics.Raycast(origin, direction, out hit, range,
                                      GameLayers.ShootableMask, QueryTriggerInteraction.Collide);

        if (!struck)
        {
            result.point = origin + direction.normalized * range;
            result.normal = -direction.normalized;
            return result;
        }

        result.hitSomething = true;
        result.point = hit.point;
        result.normal = hit.normal;

        Hitbox hitbox = hit.collider.GetComponent<Hitbox>();
        Health health = hitbox != null ? hitbox.owner : hit.collider.GetComponentInParent<Health>();

        if (health == null || health.gameObject == attacker) return result;

        float multiplier = hitbox != null ? hitbox.DamageMultiplier : 1f;
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(damage * multiplier));

        result.hitCharacter = true;
        result.wasHeadshot = hitbox != null && hitbox.IsHeadshot;
        result.damageDealt = finalDamage;

        health.TakeDamage(finalDamage, attacker);
        result.wasKill = !health.IsAlive;

        return result;
    }
}
