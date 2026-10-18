using UnityEngine;

/// Walk into it to pick up weapon #weaponIndex (matches the order in WeaponSwitcher > Guns).
/// A fresh weapon from the map gives a full magazine (Gun > Max Ammo) plus "Bonus Ammo" in reserve.
/// A weapon dropped by the player keeps the ammo it had (magAmmo / reserveAmmo are filled in automatically).
public class WeaponPickup : MonoBehaviour
{
    public int weaponIndex = 1;
    public int bonusAmmo = 0;      // extra reserve ammo on top of the magazine (0 = magazine only)
    public int magAmmo = -1;       // -1 = a fresh weapon; 0 or more = saved ammo of a dropped weapon
    public int reserveAmmo = -1;

    void Reset() { MakeTrigger(); }
    void Awake() { MakeTrigger(); }

    void MakeTrigger()
    {
        Collider c = GetComponent<Collider>();
        if (c != null && !c.isTrigger) c.isTrigger = true;
    }

    void Update() { transform.Rotate(0, 90f * Time.deltaTime, 0); } // spin so it's visible

    void OnTriggerEnter(Collider other)
    {
        WeaponSwitcher sw = other.GetComponentInParent<WeaponSwitcher>();
        if (sw == null) return;
        if (sw.Pickup(weaponIndex, bonusAmmo, magAmmo, reserveAmmo)) Destroy(gameObject);
    }
}