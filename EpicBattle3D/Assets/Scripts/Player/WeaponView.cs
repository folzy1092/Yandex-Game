using UnityEngine;

/// <summary>
/// The pistol held in front of the first-person camera: recoil when it fires,
/// bobbing while walking, sway when the view turns, and a tilt during reloads.
///
/// It is rendered by a dedicated camera on its own layer, which is the standard
/// way to stop a weapon model from clipping through walls when you stand close
/// to one.
/// </summary>
public class WeaponView : MonoBehaviour
{
    public Transform muzzle;
    public CharacterController owner;

    [Header("Resting pose")]
    public Vector3 restPosition = new Vector3(0.17f, -0.16f, 0.32f);
    public Vector3 restRotation = new Vector3(0f, -4f, 0f);

    [Header("Recoil")]
    public float recoilKick = 0.05f;
    public float recoilRise = 7f;
    public float recoilRecovery = 11f;

    [Header("Bob")]
    public float bobSpeed = 9f;
    public float bobAmount = 0.022f;

    [Header("Sway")]
    public float swayAmount = 0.02f;
    public float swayMax = 0.05f;
    public float swaySmoothing = 8f;

    float recoil;
    float bobPhase;
    Vector3 swayOffset;
    float reloadTilt;
    bool reloading;

    void Reset()
    {
        transform.localPosition = restPosition;
    }

    void LateUpdate()
    {
        recoil = Mathf.Lerp(recoil, 0f, Time.deltaTime * recoilRecovery);
        reloadTilt = Mathf.Lerp(reloadTilt, reloading ? 1f : 0f, Time.deltaTime * 9f);

        Vector3 position = restPosition;
        position += ComputeBob();
        position += ComputeSway();

        // Recoil pushes the gun back toward the camera and lifts the muzzle.
        position.z -= recoil * recoilKick;
        position.y += recoil * recoilKick * 0.35f;

        transform.localPosition = position;

        Vector3 rotation = restRotation;
        rotation.x -= recoil * recoilRise;
        // Reloading drops the muzzle and rolls the gun toward the player.
        rotation.x += reloadTilt * 25f;
        rotation.z += reloadTilt * 18f;

        transform.localRotation = Quaternion.Euler(rotation);
    }

    Vector3 ComputeBob()
    {
        if (owner == null) return Vector3.zero;

        Vector3 velocity = owner.velocity;
        velocity.y = 0f;
        float speed = velocity.magnitude;

        if (speed < 0.2f || !owner.isGrounded)
        {
            bobPhase = Mathf.Lerp(bobPhase, 0f, Time.deltaTime * 6f);
            return Vector3.zero;
        }

        bobPhase += Time.deltaTime * bobSpeed * Mathf.Clamp(speed / 5f, 0.5f, 1.7f);
        float scale = Mathf.Clamp01(speed / 5f);

        // Vertical bob runs at double rate: the gun dips on each footfall, and
        // there are two footfalls per full stride.
        return new Vector3(Mathf.Cos(bobPhase) * bobAmount * scale,
                           Mathf.Sin(bobPhase * 2f) * bobAmount * 0.6f * scale,
                           0f);
    }

    Vector3 ComputeSway()
    {
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        var target = new Vector3(Mathf.Clamp(-mouseX * swayAmount, -swayMax, swayMax),
                                 Mathf.Clamp(-mouseY * swayAmount, -swayMax, swayMax),
                                 0f);

        swayOffset = Vector3.Lerp(swayOffset, target, Time.deltaTime * swaySmoothing);
        return swayOffset;
    }

    public void PlayRecoil()
    {
        recoil = 1f;
    }

    public void SetReloading(bool value)
    {
        reloading = value;
    }
}
