using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// Put this on the Player, every Enemy and the target dummy.
/// Counts a kill for the player when a non-player dies from a shot fired with fromPlayer = true.
public class Health : MonoBehaviour
{
    public static readonly List<Health> All = new List<Health>(); // used by SafeZone
    public static int PlayerKills;                                 // shown by the HUD

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { PlayerKills = 0; All.Clear(); }   // fresh numbers every time you press Play

    public float maxHealth = 100f;
    public bool isPlayer;
    public UnityEvent onDeath;

    public float Current { get; private set; }
    public bool IsDead => Current <= 0f;

    void Awake() { Current = maxHealth; }
    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public void TakeDamage(float amount, bool fromPlayer = false)
    {
        if (IsDead) return;
        Current = Mathf.Max(0f, Current - amount);
        if (IsDead)
        {
            if (fromPlayer && !isPlayer) PlayerKills++;
            onDeath?.Invoke();
            if (!isPlayer) Destroy(gameObject, 0.1f);
        }
    }

    public void Heal(float amount)
    {
        if (!IsDead) Current = Mathf.Min(maxHealth, Current + amount);
    }
}