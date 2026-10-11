using UnityEngine;

/// Put on the Player (next to Health). Shows a temporary HP readout and handles death.
/// The real HUD comes in Phase 5, so this is just for testing the bots.
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
        Gun gun = GetComponent<Gun>();
        if (gun) gun.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnGUI()
    {
        GUI.skin.label.fontSize = 24;
        GUI.Label(new Rect(20, 20, 300, 40), "HP: " + Mathf.CeilToInt(health.Current));
        if (dead)
            GUI.Label(new Rect(Screen.width / 2f - 80, Screen.height / 2f - 20, 300, 40), "YOU DIED");
    }
}