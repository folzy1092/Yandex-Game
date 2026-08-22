using UnityEngine;

/// <summary>
/// Classic FPS movement: WASD, Shift to sprint, Space to jump, Ctrl to crouch,
/// mouse to look. Uses the legacy Input Manager so the project needs no extra packages.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Speeds")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float crouchSpeed = 2.5f;

    [Header("Jump and gravity")]
    public float jumpHeight = 1.2f;
    public float gravity = -20f;

    [Header("Look")]
    public float mouseSensitivity = 2f;
    public float maxPitch = 85f;

    [Header("Stance")]
    public float standHeight = 1.8f;
    public float crouchHeight = 1.0f;
    public float stanceLerpSpeed = 10f;

    public Transform cameraTransform;

    CharacterController controller;
    Vector3 velocity;
    float pitch;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void OnEnable()
    {
        LockCursor(true);
    }

    void Update()
    {
        HandleLook();
        HandleStance();
        HandleMovement();
    }

    void HandleLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        pitch = Mathf.Clamp(pitch - mouseY, -maxPitch, maxPitch);
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void HandleStance()
    {
        bool crouching = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        float targetHeight = crouching ? crouchHeight : standHeight;

        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * stanceLerpSpeed);
        // Keep the capsule's feet on the ground while its height changes.
        controller.center = new Vector3(0f, controller.height * 0.5f, 0f);

        if (cameraTransform != null)
        {
            Vector3 camPos = cameraTransform.localPosition;
            camPos.y = controller.height - 0.2f;
            cameraTransform.localPosition = camPos;
        }
    }

    void HandleMovement()
    {
        bool crouching = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool sprinting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        float speed = crouching ? crouchSpeed : (sprinting ? sprintSpeed : walkSpeed);

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        Vector3 move = transform.right * x + transform.forward * z;
        if (move.sqrMagnitude > 1f) move.Normalize();

        if (controller.isGrounded)
        {
            // A small downward bias keeps the controller glued to slopes.
            if (velocity.y < 0f) velocity.y = -2f;

            if (Input.GetKeyDown(KeyCode.Space) && !crouching)
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move((move * speed + Vector3.up * velocity.y) * Time.deltaTime);
    }

    public static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
