using UnityEngine;

/// <summary>
/// Holds the camera steady while the airframe throws itself around.
///
/// A camera bolted rigidly to the frame is what a bare FPV rig gives you, and it
/// is miserable to fly: every time you push forward the whole view pitches at
/// the ground, and hard turns roll the horizon over. Real strike drones carry
/// the camera on a gimbal for exactly this reason.
///
/// So the camera keeps the drone's *position* and its heading, but ignores the
/// body's pitch and roll entirely. The pilot aims it with the mouse: horizontal
/// yaws the whole drone (handled in DroneController), vertical tilts the camera
/// alone. The result is that the view stays where you point it while the drone
/// leans about underneath.
/// </summary>
public class DroneCameraGimbal : MonoBehaviour
{
    public Transform cameraTransform;

    public float mouseSensitivity = 2.5f;

    /// <summary>Looking straight down is useful for a strike run; straight up is not.</summary>
    public float minPitch = -85f;
    public float maxPitch = 35f;

    /// <summary>Starting tilt. Slightly down, as a camera on a strike drone is set.</summary>
    public float restingPitch = 12f;

    float pitch;

    void Awake()
    {
        pitch = restingPitch;
    }

    void Update()
    {
        // Vertical only: the horizontal axis turns the drone itself, which the
        // flight controller owns.
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - mouseY, minPitch, maxPitch);
    }

    void LateUpdate()
    {
        if (cameraTransform == null) return;

        // Runs after the physics has moved the body, so the stabilisation is
        // applied to this frame's attitude rather than the previous one's.
        cameraTransform.rotation = Quaternion.Euler(pitch, transform.eulerAngles.y, 0f);
    }
}
