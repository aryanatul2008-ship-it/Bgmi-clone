using UnityEngine;

/// Put on the Player (next to Health). Temporary on-screen readout until the real HUD (Phase 5):
/// shows HP, the weapon you're holding and its ammo, plus a "YOU DIED" message.
[RequireComponent(typeof(Health))]
public class PlayerDeath : MonoBehaviour
{
    Health health;
    WeaponSwitcher switcher;
    bool dead;

    void Awake()
    {
        health = GetComponent<Health>();
        switcher = GetComponent<WeaponSwitcher>();
    }

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

    void OnGUI()
    {
        GUI.skin.label.fontSize = 24;
        GUI.Label(new Rect(20, 20, 300, 40), "HP: " + Mathf.CeilToInt(health.Current));

        Gun gun = switcher != null ? switcher.CurrentGun : null;
        if (gun != null)
        {
            string status = gun.IsReloading ? "   (reloading...)" : "";
            GUI.Label(new Rect(20, 60, 600, 40), gun.name + "   " + gun.CurrentAmmo + " / " + gun.ReserveAmmo + status);
        }

        if (dead)
            GUI.Label(new Rect(Screen.width / 2f - 80, Screen.height / 2f - 20, 300, 40), "YOU DIED");
    }
}