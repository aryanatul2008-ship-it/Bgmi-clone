using UnityEngine;

/// Walk into it to collect it. Put on an object with a Collider (it is switched to Trigger automatically).
/// Heal   = a MEDKIT goes into your backpack (use it with H or from the backpack screen)
/// Bandage = a BANDAGE goes into your backpack
/// Ammo   = adds "amount" reserve ammo to the weapon you are holding
public class ItemPickup : MonoBehaviour
{
    // Keep this order: Heal = 0 and Ammo = 1 are what your existing pickups in the scene use.
    public enum ItemType { Heal, Ammo, Bandage }

    public ItemType type = ItemType.Heal;
    public float amount = 30f;     // only used for Ammo

    void Reset() { MakeTrigger(); }
    void Awake() { MakeTrigger(); }

    void MakeTrigger()
    {
        Collider c = GetComponent<Collider>();
        if (c != null && !c.isTrigger) c.isTrigger = true;
    }

    void Update() { transform.Rotate(0, 90f * Time.deltaTime, 0); } // spin so it's easy to spot

    void OnTriggerEnter(Collider other) { TryUse(other, true); }
    void OnTriggerStay(Collider other) { TryUse(other, false); } // lets you collect it later, once your backpack has room

    void TryUse(Collider other, bool announce)
    {
        if (type == ItemType.Ammo)
        {
            WeaponSwitcher sw = other.GetComponentInParent<WeaponSwitcher>();
            if (sw == null) return;
            Gun g = sw.CurrentGun;
            if (g == null || g.melee)
            {
                if (announce) PlayerInventory.Notify("Equip a gun to take ammo");
                return;
            }
            int n = Mathf.RoundToInt(amount);
            g.AddAmmo(n);
            PlayerInventory.Notify("+" + n + " ammo for " + g.name);
            Destroy(gameObject);
            return;
        }

        PlayerInventory inv = other.GetComponentInParent<PlayerInventory>();
        if (inv == null) return;

        bool added = type == ItemType.Heal ? inv.AddMedkit(1) : inv.AddBandage(1);
        if (added) Destroy(gameObject);
        else if (announce) PlayerInventory.Notify("Backpack full");
    }
}
