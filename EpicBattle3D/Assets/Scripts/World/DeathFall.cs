using UnityEngine;

/// <summary>
/// Drops a killed fighter as a physics body.
///
/// The model is detached from the character, handed a Rigidbody and a collider,
/// and left to Unity's physics: it falls under gravity, hits the floor, tumbles
/// and settles wherever it lands. That is why a corpse never ends up in the same
/// pose twice and never floats — the previous scripted version rotated the model
/// by a fixed angle, which left bodies bent at odd angles instead of lying down.
///
/// It is one rigid body rather than a jointed ragdoll per limb. A full ragdoll
/// needs a Rigidbody and CharacterJoint on all six body parts, which costs
/// noticeably more in WebGL and — with blocky primitive limbs — tends to jitter
/// and spasm rather than look convincing.
///
/// The corpse goes onto its own layer, set up to collide with the level but not
/// with living fighters, so bodies never shove players around or block bullets.
/// </summary>
public class DeathFall : MonoBehaviour
{
    public Transform model;

    /// <summary>Optional: the first-person camera, so dying drops your view too.</summary>
    public Transform cameraTransform;

    [Header("Impulse")]
    public float launchSpeed = 2.6f;
    public float spinSpeed = 5.5f;

    Health health;

    Transform modelParent;
    Vector3 modelRestPosition;
    Quaternion modelRestRotation;

    Vector3 cameraRestPosition;
    Quaternion cameraRestRotation;

    Rigidbody body;
    BoxCollider corpseCollider;

    bool cameraFalling;
    float cameraElapsed;
    Vector3 cameraFallenPosition;
    Quaternion cameraFallenRotation;

    const float CameraFallDuration = 0.5f;

    void Awake()
    {
        health = GetComponent<Health>();

        if (model != null)
        {
            modelParent = model.parent;
            modelRestPosition = model.localPosition;
            modelRestRotation = model.localRotation;
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
        DropBody(killer);
        DropCamera();

        if (GameAudio.Instance != null) GameAudio.Instance.PlayBodyFall(transform.position);
    }

    void DropBody(GameObject killer)
    {
        if (model == null) return;

        // Detach first: while the model is a child of the character, the
        // CharacterController would keep dragging it to the spawn point.
        model.SetParent(null, true);

        int ragdollLayer = GameLayers.Ragdoll;
        if (ragdollLayer >= 0) GameLayers.ApplyRecursively(model.gameObject, ragdollLayer);

        if (corpseCollider == null)
        {
            corpseCollider = model.gameObject.AddComponent<BoxCollider>();
            // One box around the whole body rather than per-limb colliders.
            corpseCollider.center = new Vector3(0f, 0.9f, 0f);
            corpseCollider.size = new Vector3(0.55f, 1.75f, 0.4f);
        }
        corpseCollider.enabled = true;

        if (body == null)
        {
            body = model.gameObject.AddComponent<Rigidbody>();
            body.mass = 70f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        body.isKinematic = false;
        body.useGravity = true;

        // Shoved away from whoever killed them, so a body falls in a direction
        // that matches the shot rather than an arbitrary one.
        Vector3 push = Vector3.up * 0.35f;
        if (killer != null)
        {
            Vector3 away = model.position - killer.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.01f) push += away.normalized;
        }
        else
        {
            push += Random.insideUnitSphere;
            push.y = Mathf.Abs(push.y);
        }

        body.linearVelocity = push.normalized * launchSpeed;
        body.angularVelocity = Random.onUnitSphere * spinSpeed;
    }

    void DropCamera()
    {
        if (cameraTransform == null) return;

        cameraFallenPosition = new Vector3(cameraRestPosition.x, 0.35f, cameraRestPosition.z);
        cameraFallenRotation = Quaternion.Euler(14f, 0f, Random.Range(-70f, 70f));

        cameraElapsed = 0f;
        cameraFalling = true;
    }

    void HandleRespawned()
    {
        cameraFalling = false;

        if (body != null)
        {
            // Freeze the physics before reattaching, or leftover velocity would
            // carry into the next life.
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        if (corpseCollider != null) corpseCollider.enabled = false;

        if (model != null)
        {
            model.SetParent(modelParent, false);
            model.localPosition = modelRestPosition;
            model.localRotation = modelRestRotation;

            int hitboxLayer = GameLayers.Hitbox;
            if (hitboxLayer >= 0) GameLayers.ApplyRecursively(model.gameObject, hitboxLayer);
        }

        if (cameraTransform != null)
        {
            cameraTransform.localPosition = cameraRestPosition;
            cameraTransform.localRotation = cameraRestRotation;
        }
    }

    void LateUpdate()
    {
        if (!cameraFalling) return;

        cameraElapsed += Time.deltaTime;
        float linear = Mathf.Clamp01(cameraElapsed / CameraFallDuration);

        // Accelerating, like something actually falling.
        float t = linear * linear;

        cameraTransform.localPosition = Vector3.Lerp(cameraRestPosition, cameraFallenPosition, t);
        cameraTransform.localRotation = Quaternion.Slerp(cameraRestRotation, cameraFallenRotation, t);

        if (linear >= 1f) cameraFalling = false;
    }
}
