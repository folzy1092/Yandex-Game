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
    public float minPitch = -35f;
    public float maxPitch = 85f;

    /// <summary>Starting tilt. Slightly down, as a camera on a strike drone is set.</summary>
    public float restingPitch = 12f;

    float pitch;
    float shakeUntil;
    float shakeDuration;
    float shakeStrength;

    void Awake()
    {
        pitch = restingPitch;
    }

    void Update()
    {
        if (Time.timeScale <= 0f || Cursor.lockState != CursorLockMode.Locked) return;
        if (MissionManager.Instance != null && !MissionManager.Instance.IsRunning) return;
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
        float shakePitch = 0f;
        float shakeYaw = 0f;
        float shakeRoll = 0f;
        if (Time.unscaledTime < shakeUntil && shakeDuration > 0f)
        {
            float remaining = Mathf.Clamp01((shakeUntil - Time.unscaledTime) / shakeDuration);
            float phase = Time.unscaledTime * 83f;
            float amount = shakeStrength * remaining * remaining;
            shakePitch = Mathf.Sin(phase) * amount;
            shakeYaw = Mathf.Sin(phase * 1.31f + 1.7f) * amount * 0.7f;
            shakeRoll = Mathf.Sin(phase * 0.73f + 3.1f) * amount * 0.55f;
        }

        cameraTransform.rotation = Quaternion.Euler(
            pitch + shakePitch, transform.eulerAngles.y + shakeYaw, shakeRoll);
    }

    /// <summary>
    /// A short deterministic kick from the detonation. Sine waves are used
    /// instead of UnityEngine.Random so a visual effect cannot alter gameplay
    /// RNG such as the next launch pad selection.
    /// </summary>
    public void Shake(float strength = 2.4f, float duration = 0.22f)
    {
        shakeStrength = Mathf.Max(shakeStrength, strength);
        shakeDuration = Mathf.Max(0.01f, duration);
        shakeUntil = Time.unscaledTime + shakeDuration;
    }
}
