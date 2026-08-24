using UnityEngine;

/// <summary>
/// Drives a target back and forth along a fixed loop of waypoints — a supply
/// truck running its route instead of sitting parked, so the compound feels
/// occupied rather than laid out for a shooting gallery.
///
/// Stops the moment the target is destroyed: a burning wreck has no business
/// still driving, and Target.Explode() already leaves the wreck exactly where
/// it died, so this only has to get out of the way.
///
/// Movement goes through a kinematic Rigidbody rather than plain transform
/// edits. The target's BoxCollider needs a Rigidbody to move efficiently
/// without Unity rebuilding static broadphase data every frame, and marking it
/// kinematic keeps it fully controlled by this script rather than by physics —
/// it still collides properly with the drone, which is dynamic. Two kinematic
/// bodies, though, never push each other apart — Unity simply does not
/// resolve a kinematic-versus-kinematic collision — so without the obstacle
/// check below, two patrol trucks (or a truck and a wreck sitting on the road)
/// would silently interpenetrate rather than react to each other at all.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PatrolMover : MonoBehaviour
{
    public Vector3[] waypoints = new Vector3[0];
    public float speed = 3.5f;
    public float turnRate = 90f;

    /// <summary>Distance to a waypoint at which it counts as reached.</summary>
    public float arrivalDistance = 0.75f;

    /// <summary>
    /// Index into <see cref="waypoints"/> to start heading towards. Set by the
    /// scene builder so several trucks sharing one loop start at different
    /// points along it rather than all beginning at index 0.
    /// </summary>
    public int startWaypoint;

    /// <summary>How far ahead to check for something blocking the road.</summary>
    public float obstacleCheckDistance = 9f;
    public float obstacleCheckRadius = 2.6f;

    /// <summary>Seconds to keep moving away after reversing, before checking ahead again.</summary>
    public float obstacleCooldown = 2.5f;

    int nextWaypoint;

    /// <summary>+1 walks the loop forward, -1 backward — flipped when something blocks the way.</summary>
    int direction = 1;

    float cooldownRemaining;

    Rigidbody body;
    Target target;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        target = GetComponent<Target>();

        nextWaypoint = waypoints.Length > 0 ? startWaypoint % waypoints.Length : 0;
    }

    void FixedUpdate()
    {
        if (waypoints.Length < 2) return;
        if (target != null && target.IsDestroyed) return;

        if (cooldownRemaining > 0f) cooldownRemaining -= Time.fixedDeltaTime;

        Vector3 destination = waypoints[nextWaypoint];
        Vector3 toDestination = destination - body.position;
        toDestination.y = 0f;

        if (toDestination.magnitude <= arrivalDistance)
        {
            Advance();
            return;
        }

        // Checked before turning, not after: a truck that only notices a
        // wreck once it is already alongside it has already driven into it.
        if (cooldownRemaining <= 0f && ObstacleAhead())
        {
            direction = -direction;
            Advance();
            cooldownRemaining = obstacleCooldown;
            return;
        }

        Quaternion desiredRotation = Quaternion.LookRotation(toDestination.normalized);
        Quaternion rotation = Quaternion.RotateTowards(body.rotation, desiredRotation,
                                                        turnRate * Time.fixedDeltaTime);
        body.MoveRotation(rotation);

        // Only move once roughly facing the right way, or a sharp corner turns
        // into a visible slide rather than a turn.
        if (Quaternion.Angle(body.rotation, desiredRotation) < 25f)
            body.MovePosition(body.position + rotation * Vector3.forward * speed * Time.fixedDeltaTime);
    }

    void Advance()
    {
        nextWaypoint = (nextWaypoint + direction + waypoints.Length) % waypoints.Length;
    }

    /// <summary>
    /// Whether another target — a wreck sitting on the road, or another
    /// patrol truck sharing this loop — is close enough ahead to be a
    /// collision rather than open road.
    /// </summary>
    bool ObstacleAhead()
    {
        Vector3 point = body.position + body.rotation * Vector3.forward * obstacleCheckDistance;
        Collider[] hits = Physics.OverlapSphere(point, obstacleCheckRadius);

        foreach (Collider hit in hits)
        {
            Target other = hit.GetComponentInParent<Target>();
            if (other != null && other != target) return true;
        }

        return false;
    }
}
