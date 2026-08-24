using UnityEngine;

/// <summary>
/// Drone flight, flown relative to where the camera is looking.
///
/// The camera is the stick. Forward means "along the line of sight", so looking
/// down and pushing forward puts the drone into a dive that gains speed, and
/// levelling out flies level. That is what makes a strike run feel like aiming
/// rather than like solving a physics puzzle mid-air.
///
///     camera looking down 40°
///              ╲
///               ╲  W ─────► accelerates along this line: forward and down
///                ▼
///
/// This is not a strict multirotor model — a real quadcopter can only push along
/// its own up axis, and flying one that way through a target under time pressure
/// is miserable. Thrust here follows the aim, and the airframe leans into its
/// own acceleration purely so it looks right.
///
/// Momentum is still real: the drone carries speed, has to be flown out of a
/// dive, and cannot stop dead.
///
/// There is deliberately no altitude ceiling. An invisible roof the drone
/// bounces off is the worst kind of boundary — it stops the player without
/// telling them anything. Height is bounded by the same thing distance is:
/// SignalLink measures the full 3D range back to the launch point, so climbing
/// far enough degrades the picture and eventually drops the link, exactly as
/// flying too far out does.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DroneController : MonoBehaviour
{
    public enum FlightMode
    {
        Casual,
        Sport
    }

    public FlightMode mode = FlightMode.Casual;

    /// <summary>Where "forward" points. Set by the factory to the camera.</summary>
    public Transform aimReference;

    [Header("Power")]
    /// <summary>Acceleration along the aim, in m/s².</summary>
    public float thrust = 26f;

    /// <summary>Sideways acceleration, as a fraction of the forward figure.</summary>
    public float strafeFactor = 0.7f;

    /// <summary>Vertical acceleration from the throttle keys, in m/s².</summary>
    public float climbThrust = 14f;

    [Header("Speed")]
    public float maxSpeed = 34f;

    /// <summary>
    /// How quickly unwanted motion bleeds off. Higher means tighter, more arcade
    /// handling; lower means the drone floats on and has to be flown out.
    /// </summary>
    public float drag = 1.15f;

    /// <summary>Extra braking when nothing is commanded, so it settles to a hover.</summary>
    public float hoverBrake = 1.9f;

    [Header("Altitude Hold")]
    /// <summary>
    /// Corrective acceleration per metre of drift from the held altitude, once
    /// the throttle keys are released.
    ///
    /// Cancelling gravity exactly (below, in ApplyThrust) zeroes the vertical
    /// acceleration, but it is open-loop: it does nothing about an altitude the
    /// drone has already drifted to, whether from a gust of momentum out of a
    /// climb or ordinary floating-point creep over a long flight. This closes
    /// the loop — the drone actively corrects back to the altitude it was at
    /// when the throttle was released, rather than merely not falling further
    /// from wherever it happens to be.
    /// </summary>
    public float altitudeHoldStrength = 6f;
    public float altitudeHoldDamping = 4f;

    [Header("Look")]
    public float yawRate = 130f;
    public float mouseSensitivity = 2.5f;

    [Header("Airframe")]
    /// <summary>How far the body leans into its own acceleration. Cosmetic only.</summary>
    public float leanAngle = 28f;
    public float leanResponse = 5f;

    public float SpeedKmh { get { return body.linearVelocity.magnitude * 3.6f; } }
    public float AltitudeMetres { get; private set; }
    public float Heading { get { return transform.eulerAngles.y; } }

    /// <summary>0..1, drives battery drain and rotor speed.</summary>
    public float ThrottleLevel { get; private set; }

    public bool IsPowered { get; private set; }

    Rigidbody body;
    float yaw;
    Vector3 leanVelocity;
    Quaternion leanRotation = Quaternion.identity;

    /// <summary>The altitude the hold loop is correcting back to.</summary>
    float heldAltitude;
    bool holdingAltitude;

    void Awake()
    {
        body = GetComponent<Rigidbody>();

        // Gravity is handled explicitly: the drone holds its own altitude, and a
        // separate fall is applied only once the motors are cut.
        body.useGravity = false;
        body.linearDamping = 0f;
        body.angularDamping = 0f;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        yaw = transform.eulerAngles.y;
        IsPowered = true;
    }

    void Update()
    {
        if (!IsPowered) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        yaw += mouseX * yawRate * Time.deltaTime;
    }

    void FixedUpdate()
    {
        MeasureAltitude();

        if (!IsPowered)
        {
            // Motors are out: it is just falling now.
            body.AddForce(Physics.gravity, ForceMode.Acceleration);
            return;
        }

        Vector3 command = ReadCommand();
        ApplyThrust(command);
        ApplyDrag(command);
        ClampSpeed();
        ApplyOrientation(command);
    }

    // ---------- input ----------

    /// <summary>
    /// The commanded acceleration, in world space, built from the aim direction.
    /// </summary>
    Vector3 ReadCommand()
    {
        float forwardInput = Input.GetAxisRaw("Vertical");     // W / S
        float strafeInput = Input.GetAxisRaw("Horizontal");    // A / D

        // Shift climbs too, alongside Space — some players reach for one,
        // some the other, and there is no sprint mechanic here to conflict with.
        bool up = Input.GetKey(KeyCode.Space)
                 || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool down = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        float climbInput = (up ? 1f : 0f) - (down ? 1f : 0f);

        // Releasing the throttle keys re-arms the hold at whatever altitude the
        // drone is at that instant, rather than snapping back to an old one.
        if (climbInput != 0f) holdingAltitude = false;
        else if (!holdingAltitude)
        {
            heldAltitude = AltitudeMetres;
            holdingAltitude = true;
        }

        // Aim including its vertical component: this is the whole point — looking
        // down and pushing forward has to dive, not fly level.
        Vector3 forward = aimReference != null ? aimReference.forward : transform.forward;

        // Strafing stays horizontal. Rolling the sideways axis with the camera
        // would make a dive slide the drone sideways into the ground.
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.001f) right = transform.right;
        right.Normalize();

        Vector3 command = forward * (forwardInput * thrust)
                          + right * (strafeInput * thrust * strafeFactor)
                          + Vector3.up * (climbInput * climbThrust);

        ThrottleLevel = Mathf.Clamp01(command.magnitude / thrust);
        return command;
    }

    // ---------- physics ----------

    void ApplyThrust(Vector3 command)
    {
        // Hold altitude: the drone hovers on its own, so the throttle keys are
        // for climbing and descending rather than for not falling.
        body.AddForce(-Physics.gravity, ForceMode.Acceleration);
        body.AddForce(command, ForceMode.Acceleration);

        if (holdingAltitude)
        {
            float error = heldAltitude - AltitudeMetres;
            float correction = error * altitudeHoldStrength - body.linearVelocity.y * altitudeHoldDamping;
            body.AddForce(Vector3.up * correction, ForceMode.Acceleration);
        }
    }

    void ApplyDrag(Vector3 command)
    {
        Vector3 velocity = body.linearVelocity;

        // Braking is stronger when nothing is being asked for, which is what
        // makes the drone settle into a hover instead of drifting forever.
        float braking = command.sqrMagnitude < 0.01f ? drag * hoverBrake : drag;

        // Sport mode keeps its momentum: less help, more to fly.
        if (mode == FlightMode.Sport) braking *= 0.55f;

        body.AddForce(-velocity * braking, ForceMode.Acceleration);
    }

    void ClampSpeed()
    {
        if (body.linearVelocity.magnitude <= maxSpeed) return;
        body.linearVelocity = body.linearVelocity.normalized * maxSpeed;
    }

    /// <summary>
    /// Points the airframe along its heading and leans it into its acceleration.
    /// Purely visual — the forces above do not care which way the body faces.
    /// </summary>
    void ApplyOrientation(Vector3 command)
    {
        Quaternion heading = Quaternion.Euler(0f, yaw, 0f);

        Vector3 local = Quaternion.Inverse(heading) * command;
        var targetLean = new Vector3(
            Mathf.Clamp(local.z / thrust, -1f, 1f) * leanAngle,
            0f,
            Mathf.Clamp(-local.x / thrust, -1f, 1f) * leanAngle);

        leanVelocity = Vector3.Lerp(leanVelocity, targetLean, Time.fixedDeltaTime * leanResponse);
        leanRotation = Quaternion.Euler(leanVelocity.x, 0f, leanVelocity.z);

        body.MoveRotation(heading * leanRotation);
    }

    void MeasureAltitude()
    {
        RaycastHit hit;
        AltitudeMetres = Physics.Raycast(transform.position, Vector3.down, out hit, 500f)
            ? hit.distance
            : transform.position.y;
    }

    /// <summary>
    /// Cuts the motors. The drone keeps its momentum and falls — used when the
    /// battery runs flat or the signal is lost, so failure is something you watch
    /// happen rather than an instant cut to a menu.
    /// </summary>
    public void CutPower()
    {
        IsPowered = false;
        ThrottleLevel = 0f;

        // Let it tumble as it goes down.
        body.constraints = RigidbodyConstraints.None;
        body.angularDamping = 0.4f;
    }
}
