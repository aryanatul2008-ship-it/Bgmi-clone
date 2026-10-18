using UnityEngine;

/// Camera-relative third-person movement with sprint, jump, crouch (press C) and fall damage.
/// The player turns to face the camera's direction, so W always walks "into the screen".
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public Transform cameraTransform;      // auto-filled with Main Camera
    public float moveSpeed = 5.0f;
    public float sprintMultiplier = 1.6f;  // hold Left Shift
    public float gravity = -9.81f;
    public float jumpHeight = 1.2f;
    public float turnSpeed = 15f;

    [Header("Crouch (press C to toggle)")]
    public float crouchSpeedMultiplier = 0.5f;
    public float standHeight = 2f;         // must match the Character Controller's normal height
    public float crouchHeight = 1.2f;
    public float crouchSmoothing = 12f;

    [Header("Fall damage")]
    public float safeFallHeight = 3f;      // falling less than this hurts nothing
    public float damagePerMeter = 10f;     // damage for every metre beyond the safe height

    public bool IsCrouching { get; private set; }

    CharacterController controller;
    Health health;
    Vector3 velocity;
    float peakY;
    bool wasGrounded = true;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        health = GetComponent<Health>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        peakY = transform.position.y;
    }

    void Update()
    {
        // Face the camera's horizontal direction
        if (cameraTransform != null)
        {
            Quaternion target = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, turnSpeed * Time.deltaTime);
        }

        bool grounded = controller.isGrounded;
        HandleFall(grounded);
        if (grounded && velocity.y < 0f) velocity.y = -2f;

        HandleCrouch();

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        Vector3 move = transform.right * x + transform.forward * z;
        if (move.sqrMagnitude > 1f) move.Normalize();

        float speed = moveSpeed;
        if (IsCrouching) speed *= crouchSpeedMultiplier;
        else if (Input.GetKey(KeyCode.LeftShift)) speed *= sprintMultiplier;

        if (Input.GetButtonDown("Jump") && grounded && !IsCrouching)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        velocity.y += gravity * Time.deltaTime;

        controller.Move((move * speed + Vector3.up * velocity.y) * Time.deltaTime);
    }

    // Remembers the highest point of a fall; when you land, drops bigger than safeFallHeight hurt.
    void HandleFall(bool grounded)
    {
        float y = transform.position.y;
        if (grounded)
        {
            if (!wasGrounded)
            {
                float drop = peakY - y;
                if (drop > safeFallHeight && health != null)
                    health.TakeDamage((drop - safeFallHeight) * damagePerMeter);
            }
            peakY = y;
        }
        else if (y > peakY) peakY = y;
        wasGrounded = grounded;
    }

    void HandleCrouch()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            if (!IsCrouching) IsCrouching = true;
            else if (CanStand()) IsCrouching = false;
        }
        // Sprinting stands you up (if there is room above your head)
        if (IsCrouching && Input.GetKey(KeyCode.LeftShift) && CanStand()) IsCrouching = false;

        float targetHeight = IsCrouching ? crouchHeight : standHeight;
        float h = Mathf.Lerp(controller.height, targetHeight, crouchSmoothing * Time.deltaTime);
        controller.height = h;
        controller.center = new Vector3(0f, h * 0.5f - standHeight * 0.5f, 0f); // keeps your feet on the ground
    }

    bool CanStand()
    {
        Vector3 top = transform.position + Vector3.up * (standHeight * 0.5f - controller.radius);
        return !Physics.CheckSphere(top, controller.radius * 0.95f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }
}