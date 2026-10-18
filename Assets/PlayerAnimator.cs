using UnityEngine;

/// Put on the Player (the object that has CharacterController + PlayerMovement).
/// Feeds the character model's Animator: local movement direction (walk / run / strafe),
/// overall speed and crouching (for the crouch blend), whether you are on the ground (jump / fall) and whether you are dead.
public class PlayerAnimator : MonoBehaviour
{
    public Animator animator;        // on the character model (a child of the Player). Found automatically.
    public float damping = 0.1f;     // smoothing so animations blend instead of snapping
    public float airGrace = 0.12f;   // seconds in the air before the jump pose starts (avoids flicker on slopes)

    CharacterController cc;
    PlayerMovement movement;
    Health health;
    float airTime;
    bool dead;

    static readonly int MoveX = Animator.StringToHash("MoveX");
    static readonly int MoveZ = Animator.StringToHash("MoveZ");
    static readonly int MoveSpeed = Animator.StringToHash("MoveSpeed");
    static readonly int Grounded = Animator.StringToHash("Grounded");
    static readonly int Crouching = Animator.StringToHash("Crouching");
    static readonly int DeadParam = Animator.StringToHash("Dead");

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        movement = GetComponent<PlayerMovement>();
        health = GetComponent<Health>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogError("PlayerAnimator: no Animator found. Add the character model as a child of the Player.");
            enabled = false;
            return;
        }
        animator.applyRootMotion = false; // PlayerMovement moves the character, not the animation
    }

    void Update()
    {
        // Velocity relative to the way the player faces (the player always faces the camera)
        Vector3 v = cc.velocity;
        v.y = 0f;
        Vector3 local = transform.InverseTransformDirection(v);

        // 1.0 = full sprint speed, so a normal walk lands around 0.6 and blends between the walk and run clips
        float top = movement != null ? movement.moveSpeed * movement.sprintMultiplier : 8f;
        float x = local.x / top;
        float z = local.z / top;
        animator.SetFloat(MoveX, x, damping, Time.deltaTime);
        animator.SetFloat(MoveZ, z, damping, Time.deltaTime);
        animator.SetFloat(MoveSpeed, new Vector2(x, z).magnitude, damping, Time.deltaTime);

        animator.SetBool(Crouching, movement != null && movement.IsCrouching);

        if (cc.isGrounded) airTime = 0f; else airTime += Time.deltaTime;
        animator.SetBool(Grounded, airTime < airGrace);

        if (!dead && health != null && health.IsDead)
        {
            dead = true;
            animator.SetBool(DeadParam, true);
        }
    }
}