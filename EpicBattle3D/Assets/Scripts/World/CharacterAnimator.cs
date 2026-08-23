using UnityEngine;

/// <summary>
/// Swings arms and legs while a character moves, and plays footsteps in time
/// with the stride. Driven straight from the CharacterController's velocity —
/// no animation clips, no rigging.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class CharacterAnimator : MonoBehaviour
{
    public Transform leftLegPivot;
    public Transform rightLegPivot;
    public Transform leftArmPivot;
    public Transform rightArmPivot;

    public float swingAngle = 45f;
    public float strideSpeed = 4.2f;
    public float settleSpeed = 8f;

    /// <summary>Metres travelled per footstep. One stride is two steps.</summary>
    public float stepDistance = 1.9f;

    CharacterController controller;
    float phase;
    float distanceSinceStep;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        Vector3 velocity = controller.velocity;
        velocity.y = 0f;
        float speed = velocity.magnitude;

        if (speed > 0.2f)
        {
            phase += Time.deltaTime * strideSpeed * Mathf.Clamp(speed / 5f, 0.6f, 1.8f);

            distanceSinceStep += speed * Time.deltaTime;
            if (distanceSinceStep >= stepDistance)
            {
                distanceSinceStep = 0f;
                if (GameAudio.Instance != null && controller.isGrounded)
                    GameAudio.Instance.PlayFootstep(transform.position);
            }

            float swing = Mathf.Sin(phase) * swingAngle;
            SetPitch(leftLegPivot, swing);
            SetPitch(rightLegPivot, -swing);
            // Arms counter-swing against the legs, as they do when walking.
            SetPitch(leftArmPivot, -swing * 0.7f);
            SetPitch(rightArmPivot, swing * 0.7f);
            return;
        }

        distanceSinceStep = 0f;
        Settle(leftLegPivot);
        Settle(rightLegPivot);
        Settle(leftArmPivot);
        Settle(rightArmPivot);
    }

    void SetPitch(Transform pivot, float angle)
    {
        if (pivot != null) pivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
    }

    void Settle(Transform pivot)
    {
        if (pivot == null) return;

        pivot.localRotation = Quaternion.Slerp(pivot.localRotation, Quaternion.identity,
                                               Time.deltaTime * settleSpeed);
    }
}
