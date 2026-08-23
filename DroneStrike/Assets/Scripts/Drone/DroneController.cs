using UnityEngine;

/// <summary>
/// Quadcopter flight, modelled the way a real multirotor actually works: the
/// rotors only ever push along the drone's own up axis, so the only way to move
/// sideways is to tilt and let part of that thrust point where you want to go.
///
/// That single rule is what gives the flight its character — you cannot stop
/// instantly, you have to tilt back to brake, and hard turns cost you altitude
/// unless you add throttle.
///
///        thrust
///          ↑              tilted forward:
///          │  ╱             the same thrust now has a
///          │ ╱              forward component, and less
///          │╱               of it is holding you up
///        ──┴──
///
/// Two modes, as in the design:
///   Casual — the drone levels itself and holds its height when you let go.
///   Sport  — the angle stays where you put it and you fly it out yourself.
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

    [Header("Attitude")]
    /// <summary>Maximum tilt from level, in degrees. More tilt means more speed and less lift.</summary>
    public float maxTilt = 35f;

    /// <summary>How fast the drone reaches the commanded angle.</summary>
    public float tiltResponse = 6f;

    /// <summary>Degrees per second of yaw at full stick.</summary>
    public float yawRate = 110f;

    public float mouseSensitivity = 2.5f;

    [Header("Power")]
    /// <summary>Extra thrust available above what it takes to hover, as a multiplier.</summary>
    public float thrustHeadroom = 0.9f;

    /// <summary>How fast the throttle follows the keys.</summary>
    public float throttleResponse = 4f;

    [Header("Limits")]
    public float maxSpeed = 28f;          // metres per second
    public float maxAltitude = 120f;
    public float horizontalDrag = 0.6f;
    public float verticalDrag = 0.9f;

    /// <summary>Speed in km/h, for the telemetry readout.</summary>
    public float SpeedKmh { get { return body.linearVelocity.magnitude * 3.6f; } }

    /// <summary>Height above the ground directly below, in metres.</summary>
    public float AltitudeMetres { get; private set; }

    /// <summary>Compass heading in degrees, 0 = north.</summary>
    public float Heading { get { return transform.eulerAngles.y; } }

    /// <summary>Throttle demand in 0..1, used by the battery drain and rotor sound.</summary>
    public float ThrottleLevel { get; private set; }

    /// <summary>False once the drone is destroyed or out of power; it then just falls.</summary>
    public bool IsPowered { get; private set; }

    Rigidbody body;
    float pitchAngle;
    float rollAngle;
    float throttle;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = true;
        body.linearDamping = 0f;    // handled manually, per axis
        body.angularDamping = 8f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        IsPowered = true;
        throttle = 0.5f;
    }

    void Update()
    {
        if (!IsPowered) return;

        ReadAttitudeInput();
        ReadThrottleInput();
        ReadYawInput();
    }

    void FixedUpdate()
    {
        MeasureAltitude();
        if (!IsPowered) return;

        ApplyAttitude();
        ApplyThrust();
        ApplyDrag();
        ClampSpeed();
    }

    // ---------- input ----------

    void ReadAttitudeInput()
    {
        float pitchInput = Input.GetAxisRaw("Vertical");     // W / S
        float rollInput = Input.GetAxisRaw("Horizontal");    // A / D

        // Nose down for forward: tilting the thrust vector forward is what moves
        // a quadcopter. In Unity a positive X rotation pitches the nose down and
        // a positive Z rotation rolls left, so W is positive pitch and D is
        // negative roll. Both signs were inverted before, which had W flying the
        // drone backwards and D sliding it left.
        float targetPitch = pitchInput * maxTilt;
        float targetRoll = -rollInput * maxTilt;

        if (mode == FlightMode.Casual)
        {
            // Commanded angle, and zero when the sticks are centred — so letting
            // go brings the drone back to level on its own.
            pitchAngle = Mathf.Lerp(pitchAngle, targetPitch, Time.deltaTime * tiltResponse);
            rollAngle = Mathf.Lerp(rollAngle, targetRoll, Time.deltaTime * tiltResponse);
            return;
        }

        // Sport: the sticks change the angle and it stays where it is left.
        pitchAngle += pitchInput * maxTilt * Time.deltaTime * 2f;
        rollAngle += -rollInput * maxTilt * Time.deltaTime * 2f;

        pitchAngle = Mathf.Clamp(pitchAngle, -maxTilt * 2f, maxTilt * 2f);
        rollAngle = Mathf.Clamp(rollAngle, -maxTilt * 2f, maxTilt * 2f);
    }

    void ReadThrottleInput()
    {
        float target = 0.5f;   // hover

        bool up = Input.GetKey(KeyCode.Space);
        bool down = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        if (up) target = 1f;
        else if (down) target = 0f;
        else if (mode == FlightMode.Sport) target = throttle;   // sport holds the last setting

        throttle = Mathf.Lerp(throttle, target, Time.deltaTime * throttleResponse);
        ThrottleLevel = throttle;
    }

    void ReadYawInput()
    {
        float yawInput = Input.GetAxis("Mouse X") * mouseSensitivity;
        transform.Rotate(Vector3.up, yawInput * yawRate * Time.deltaTime, Space.World);
    }

    // ---------- physics ----------

    void ApplyAttitude()
    {
        // Yaw is owned by the mouse; this only sets pitch and roll around it.
        Quaternion target = Quaternion.Euler(pitchAngle, transform.eulerAngles.y, rollAngle);
        body.MoveRotation(Quaternion.Slerp(body.rotation, target, Time.fixedDeltaTime * tiltResponse));
    }

    void ApplyThrust()
    {
        float gravity = Physics.gravity.magnitude;

        // What it takes to hold altitude at the current tilt. As the drone leans
        // over, less of its thrust points up, so it needs more of it just to stay
        // level — which is why hard manoeuvres make you sink.
        float upwardFraction = Mathf.Max(0.25f, Vector3.Dot(transform.up, Vector3.up));
        float hoverThrust = body.mass * gravity / upwardFraction;

        // Throttle runs 0..1 with hover at the midpoint.
        float demand = (throttle - 0.5f) * 2f;
        float thrust = hoverThrust * (1f + demand * thrustHeadroom);

        if (AltitudeMetres > maxAltitude && demand > 0f)
            thrust = hoverThrust;   // ceiling: climb stops, hovering still works

        body.AddForce(transform.up * thrust);
    }

    void ApplyDrag()
    {
        Vector3 velocity = body.linearVelocity;

        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        body.AddForce(-horizontal * horizontalDrag * body.mass);
        body.AddForce(Vector3.up * -velocity.y * verticalDrag * body.mass);
    }

    void ClampSpeed()
    {
        if (body.linearVelocity.magnitude <= maxSpeed) return;
        body.linearVelocity = body.linearVelocity.normalized * maxSpeed;
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
        body.angularDamping = 0.5f;
    }
}
