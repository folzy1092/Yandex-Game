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
/// it still collides properly with the drone, which is dynamic.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PatrolMover : MonoBehaviour
{
    public Vector3[] waypoints = new Vector3[0];
    public float speed = 3.5f;
    public float turnRate = 90f;

    /// <summary>Distance to a waypoint at which it counts as reached.</summary>
    public float arrivalDistance = 0.75f;

    int nextWaypoint;
    Rigidbody body;
    Target target;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        target = GetComponent<Target>();
    }

    void FixedUpdate()
    {
        if (waypoints.Length < 2) return;
        if (target != null && target.IsDestroyed) return;

        Vector3 destination = waypoints[nextWaypoint];
        Vector3 toDestination = destination - body.position;
        toDestination.y = 0f;

        if (toDestination.magnitude <= arrivalDistance)
        {
            nextWaypoint = (nextWaypoint + 1) % waypoints.Length;
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
}
