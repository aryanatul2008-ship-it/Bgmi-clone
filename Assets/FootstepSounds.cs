using UnityEngine;

/// Put on the Player. Plays footsteps while you walk or sprint on the ground (faster when sprinting),
/// plus optional jump and landing sounds. Add 2-3 footstep clips to "clips" so it doesn't sound repetitive.
[RequireComponent(typeof(CharacterController))]
public class FootstepSounds : MonoBehaviour
{
    public AudioClip[] clips;
    public AudioClip jumpClip;      // optional
    public AudioClip landClip;      // optional
    public float walkInterval = 0.5f;    // seconds between steps
    public float sprintInterval = 0.32f;
    [Range(0f, 1f)] public float volume = 0.6f;

    CharacterController cc;
    AudioSource src;
    float timer;
    bool wasGrounded = true;
    int lastIndex = -1;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f; // 2D: your own steps sound the same from any camera angle
    }

    void Update()
    {
        bool grounded = cc.isGrounded;

        Vector3 flat = cc.velocity;
        flat.y = 0f;

        if (grounded && flat.magnitude > 0.5f)
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                PlayStep();
                timer = Input.GetKey(KeyCode.LeftShift) ? sprintInterval : walkInterval;
            }
        }
        else timer = 0f; // first step plays immediately when you start moving

        if (grounded && !wasGrounded && landClip != null) src.PlayOneShot(landClip, volume);
        if (!grounded && wasGrounded && jumpClip != null && cc.velocity.y > 0.5f) src.PlayOneShot(jumpClip, volume);
        wasGrounded = grounded;
    }

    void PlayStep()
    {
        if (clips == null || clips.Length == 0) return;
        int i = Random.Range(0, clips.Length);
        if (clips.Length > 1 && i == lastIndex) i = (i + 1) % clips.Length; // never the same clip twice in a row
        lastIndex = i;
        if (clips[i] == null) return;
        src.pitch = Random.Range(0.9f, 1.1f);
        src.PlayOneShot(clips[i], volume);
    }
}