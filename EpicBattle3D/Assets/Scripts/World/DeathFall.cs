using UnityEngine;

/// <summary>
/// Makes a killed fighter collapse instead of vanishing mid-stride.
///
/// This is a scripted fall rather than a physics ragdoll: a real ragdoll needs a
/// Rigidbody and joint on every body part, which costs far more than it is worth
/// in a WebGL build and would need the whole character rebuilt. Instead the model
/// topples about its feet with the limbs going slack, which reads as a body
/// dropping without any physics running at all.
///
/// The fall accelerates rather than moving linearly, because a body falling under
/// gravity starts slow and finishes fast — a constant-speed topple looks wrong
/// immediately even if you cannot say why.
/// </summary>
public class DeathFall : MonoBehaviour
{
    public Transform model;
    public Transform leftLegPivot;
    public Transform rightLegPivot;
    public Transform leftArmPivot;
    public Transform rightArmPivot;

    /// <summary>Optional: the first-person camera, so dying drops your view too.</summary>
    public Transform cameraTransform;

    public float fallDuration = 0.55f;

    Health health;

    Quaternion modelRestRotation;
    Vector3 modelRestPosition;
    Vector3 cameraRestPosition;
    Quaternion cameraRestRotation;

    Quaternion modelFallenRotation;
    Vector3 modelFallenPosition;
    Vector3 cameraFallenPosition;
    Quaternion cameraFallenRotation;

    Quaternion leftLegSlack, rightLegSlack, leftArmSlack, rightArmSlack;

    bool falling;
    float elapsed;

    void Awake()
    {
        health = GetComponent<Health>();

        if (model != null)
        {
            modelRestRotation = model.localRotation;
            modelRestPosition = model.localPosition;
        }

        if (cameraTransform != null)
        {
            cameraRestPosition = cameraTransform.localPosition;
            cameraRestRotation = cameraTransform.localRotation;
        }

        if (health != null)
        {
            health.OnDied += HandleDied;
            health.OnRespawned += HandleRespawned;
        }
    }

    void OnDestroy()
    {
        if (health == null) return;
        health.OnDied -= HandleDied;
        health.OnRespawned -= HandleRespawned;
    }

    void HandleDied(GameObject killer)
    {
        // Topple in a random direction so a row of kills does not look identical.
        float tipDirection = Random.Range(0f, 360f);
        Vector3 axis = Quaternion.Euler(0f, tipDirection, 0f) * Vector3.right;

        modelFallenRotation = Quaternion.AngleAxis(Random.Range(82f, 95f), axis) * modelRestRotation;
        // Dropping the model as it rotates keeps the body on the floor rather
        // than leaving it pivoting around its waist in the air.
        modelFallenPosition = modelRestPosition + Vector3.down * 0.15f;

        // Limbs go slack at slightly different angles, which is what sells it as
        // a body rather than a rotating statue.
        leftLegSlack = Quaternion.Euler(Random.Range(-25f, 10f), 0f, Random.Range(-12f, 12f));
        rightLegSlack = Quaternion.Euler(Random.Range(-25f, 10f), 0f, Random.Range(-12f, 12f));
        leftArmSlack = Quaternion.Euler(Random.Range(-40f, 25f), 0f, Random.Range(-30f, 5f));
        rightArmSlack = Quaternion.Euler(Random.Range(-40f, 25f), 0f, Random.Range(-5f, 30f));

        if (cameraTransform != null)
        {
            // First person: the view sinks to about knee height and rolls over.
            cameraFallenPosition = new Vector3(cameraRestPosition.x, 0.35f, cameraRestPosition.z);
            cameraFallenRotation = Quaternion.Euler(12f, 0f, Random.Range(-70f, 70f));
        }

        elapsed = 0f;
        falling = true;

        if (GameAudio.Instance != null) GameAudio.Instance.PlayBodyFall(transform.position);
    }

    void HandleRespawned()
    {
        falling = false;

        if (model != null)
        {
            model.localRotation = modelRestRotation;
            model.localPosition = modelRestPosition;
        }

        ResetPivot(leftLegPivot);
        ResetPivot(rightLegPivot);
        ResetPivot(leftArmPivot);
        ResetPivot(rightArmPivot);

        if (cameraTransform != null)
        {
            cameraTransform.localPosition = cameraRestPosition;
            cameraTransform.localRotation = cameraRestRotation;
        }
    }

    void LateUpdate()
    {
        if (!falling) return;

        elapsed += Time.deltaTime;
        float linear = Mathf.Clamp01(elapsed / fallDuration);

        // Accelerating curve: slow tip at first, then the body drops.
        float t = linear * linear;

        if (model != null)
        {
            model.localRotation = Quaternion.Slerp(modelRestRotation, modelFallenRotation, t);
            model.localPosition = Vector3.Lerp(modelRestPosition, modelFallenPosition, t);
        }

        SlackenPivot(leftLegPivot, leftLegSlack, t);
        SlackenPivot(rightLegPivot, rightLegSlack, t);
        SlackenPivot(leftArmPivot, leftArmSlack, t);
        SlackenPivot(rightArmPivot, rightArmSlack, t);

        if (cameraTransform != null)
        {
            cameraTransform.localPosition = Vector3.Lerp(cameraRestPosition, cameraFallenPosition, t);
            cameraTransform.localRotation = Quaternion.Slerp(cameraRestRotation, cameraFallenRotation, t);
        }

        if (linear >= 1f) falling = false;
    }

    void SlackenPivot(Transform pivot, Quaternion slack, float t)
    {
        if (pivot != null) pivot.localRotation = Quaternion.Slerp(Quaternion.identity, slack, t);
    }

    void ResetPivot(Transform pivot)
    {
        if (pivot != null) pivot.localRotation = Quaternion.identity;
    }
}
