using UnityEngine;

public enum ScopeType { None, RedDot, Sniper }

/// Add to each weapon object (Rifle, SMG, Sniper) next to its Gun component.
/// ScopeController (on the Player) reads these values while the right mouse button is held.
public class GunScope : MonoBehaviour
{
    public ScopeType type = ScopeType.RedDot;
    public float zoomFOV = 45f;                                    // camera field of view while aiming (normal is about 60)
    [Range(0.05f, 1f)] public float spreadMultiplier = 0.5f;       // 0.5 = bullets scatter half as much while aiming
    [Range(0.1f, 1f)] public float sensitivityMultiplier = 0.7f;   // lower = slower mouse while aiming
    public float zoomSpeed = 8f;                                   // higher = zooms faster

    // 0 = not aiming, 1 = fully aimed. Set by ScopeController, read by Gun.
    public float AimAmount { get; set; }
    public float SpreadMultiplier => Mathf.Lerp(1f, spreadMultiplier, AimAmount);
}
