using UnityEngine;

/// Put on the Player (next to Health). Handles what happens when the player dies.
/// (The on-screen HP / ammo / "YOU DIED" text now lives in GameHUD.)
[RequireComponent(typeof(Health))]
public class PlayerDeath : MonoBehaviour
{
    Health health;
    bool dead;

    void Awake() { health = GetComponent<Health>(); }

    void Update()
    {
        if (!dead && health.IsDead) Die();
    }

    void Die()
    {
        dead = true;
        PlayerMovement move = GetComponent<PlayerMovement>();
        if (move) move.enabled = false;

        // Guns are children of the Player, so disable every one (including inactive ones)
        foreach (Gun g in GetComponentsInChildren<Gun>(true)) g.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}