using UnityEngine;

public enum BarrelAxis { PlusZ, MinusZ, PlusX, MinusX, PlusY, MinusY }

/// Put on a gun MODEL (the 3D mesh) that is a child of a weapon object such as Rifle, SMG or Sniper.
/// The gun is held at the player's right hand and points where the crosshair points, so every model only needs
/// two settings: which way its barrel points in the model ("Barrel") and, if needed, a "Roll".
/// Tune them in Play mode, then Copy Component / Paste Component Values after you stop.
/// Colliders on the model are switched off automatically (they would block your own bullets).
public class WeaponModel : MonoBehaviour
{
    public HumanBodyBones bone = HumanBodyBones.RightHand;

    [Header("Which way does the barrel point INSIDE the model? Try each until the gun points forward")]
    public BarrelAxis barrel = BarrelAxis.PlusZ;
    [Tooltip("Turns the gun around its barrel. Use 90, 180 or 270 if it is sideways or upside down.")]
    public float roll = 0f;

    [Header("Position from the hand (x = right, y = up, z = forward in the direction you aim)")]
    public Vector3 handOffset = new Vector3(0f, 0f, 0.25f);

    [Header("Aiming")]
    public bool aimWithCamera = true;   // true: the gun follows the camera direction (up and down too)

    Transform hand;
    Camera cam;

    void Awake()
    {
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
    }

    void OnEnable() { hand = null; } // look the hand up again after a skin swap or when the weapon is re-equipped

    void LateUpdate()
    {
        if (hand == null && !FindHand()) return;
        if (cam == null) cam = Camera.main;

        Vector3 forward = (aimWithCamera && cam != null) ? cam.transform.forward : transform.root.forward;
        Quaternion aim = Quaternion.LookRotation(forward, Vector3.up);

        // fix = turns the model so that its barrel points along +Z; then roll around the barrel; then aim
        Quaternion rotation = aim * Quaternion.Euler(0f, 0f, roll) * BarrelToForward(barrel);
        transform.SetPositionAndRotation(hand.position + aim * handOffset, rotation);
    }

    bool FindHand()
    {
        // The character model is a child of the Player. Its humanoid skeleton tells us where the hand is.
        Animator anim = transform.root.GetComponentInChildren<Animator>();
        if (anim == null || !anim.isHuman) return false;
        hand = anim.GetBoneTransform(bone);
        return hand != null;
    }

    static Quaternion BarrelToForward(BarrelAxis a)
    {
        switch (a)
        {
            case BarrelAxis.MinusZ: return Quaternion.Euler(0f, 180f, 0f);
            case BarrelAxis.PlusX: return Quaternion.Euler(0f, -90f, 0f);
            case BarrelAxis.MinusX: return Quaternion.Euler(0f, 90f, 0f);
            case BarrelAxis.PlusY: return Quaternion.Euler(90f, 0f, 0f);
            case BarrelAxis.MinusY: return Quaternion.Euler(-90f, 0f, 0f);
            default: return Quaternion.identity;
        }
    }
}