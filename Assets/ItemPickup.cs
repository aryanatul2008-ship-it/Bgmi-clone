using UnityEngine;

/// Walk into it to use it. Put on an object with a Collider (it is switched to Trigger automatically).
/// Heal  = restores "amount" health (only used if you're hurt).
/// Ammo  = adds "amount" reserve ammo to the weapon you are holding.
public class ItemPickup : MonoBehaviour
{
    public enum ItemType { Heal, Ammo }

    public ItemType type = ItemType.Heal;
    public float amount = 25f;

    void Reset() { MakeTrigger(); }
    void Awake() { MakeTrigger(); }

    void MakeTrigger()
    {
        Collider c = GetComponent<Collider>();
        if (c != null && !c.isTrigger) c.isTrigger = true; // a solid collider would block you and never fire
    }

    void Update() { transform.Rotate(0, 90f * Time.deltaTime, 0); } // spin so it's easy to spot

    void OnTriggerEnter(Collider other) { TryUse(other, true); }
    void OnTriggerStay(Collider other) { TryUse(other, false); } // lets you pick it up later if you were at full health

    void TryUse(Collider other, bool logReason)
    {
        if (type == ItemType.Heal)
        {
            Health h = other.GetComponentInParent<Health>();
            if (h == null || !h.isPlayer) return;
            if (h.Current >= h.maxHealth)
            {
                if (logReason) Debug.Log("ItemPickup: health is already full, heal not used.");
                return;
            }
            h.Heal(amount);
            Destroy(gameObject);
        }
        else
        {
            WeaponSwitcher sw = other.GetComponentInParent<WeaponSwitcher>();
            if (sw == null) return;
            if (sw.CurrentGun == null)
            {
                if (logReason) Debug.Log("ItemPickup: no weapon equipped, ammo not used.");
                return;
            }
            sw.CurrentGun.AddAmmo(Mathf.RoundToInt(amount));
            Destroy(gameObject);
        }
    }
}
