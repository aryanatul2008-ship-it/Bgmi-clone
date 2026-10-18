using UnityEngine;

/// Put on the Player. Each weapon is a CHILD object of the Player with its own Gun component
/// (Rifle, SMG, Sniper, Knife...). Add them to "guns". With 'Only Knife At Start' ticked you begin with just the melee weapon.
/// Keys 1-9, Q/E or the scroll wheel switch between the weapons you own.
public class WeaponSwitcher : MonoBehaviour
{
    public Gun[] guns;
    public bool[] owned;   // same length as guns

    int current = -1;

    public Gun CurrentGun => (current >= 0 && current < guns.Length) ? guns[current] : null;
    public int CurrentIndex => current;
    public bool IsOwned(int i) => i >= 0 && i < guns.Length && owned != null && i < owned.Length && owned[i];

    [Tooltip("Ticked: you start with only the melee weapon (the Knife) and the 'Owned' list below is ignored.")]
    public bool onlyKnifeAtStart = true;

    void Awake()
    {
        // Build the ownership list in code, so it always matches 'Guns' and you never have to edit it by hand
        bool[] o = new bool[guns.Length];
        for (int i = 0; i < guns.Length; i++)
        {
            if (onlyKnifeAtStart) o[i] = guns[i] != null && guns[i].melee;
            else if (owned != null && i < owned.Length) o[i] = owned[i];
        }
        owned = o;
    }

    void Start() { SelectFallback(); }

    void Update()
    {
        if (PlayerInventory.IsOpen) return;

        for (int i = 0; i < guns.Length && i < 9; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) Select(i);

        if (Input.GetKeyDown(KeyCode.E)) Cycle(1);
        if (Input.GetKeyDown(KeyCode.Q)) Cycle(-1);

        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0.1f) Cycle(1);
        else if (scroll < -0.1f) Cycle(-1);
    }

    void Cycle(int dir)
    {
        for (int i = 1; i <= guns.Length; i++)
        {
            int idx = (current + dir * i + guns.Length * 2) % guns.Length;
            if (IsOwned(idx) && guns[idx] != null) { Select(idx); return; }
        }
    }

    public void Select(int index)
    {
        if (index < 0 || index >= guns.Length) return;
        if (guns[index] == null) { Debug.LogWarning("WeaponSwitcher: slot " + index + " in 'Guns' is empty."); return; }
        if (!IsOwned(index)) { PlayerInventory.Notify("You don't have the " + guns[index].name + " yet"); return; }

        current = index;
        for (int i = 0; i < guns.Length; i++)
            if (guns[i] != null) guns[i].gameObject.SetActive(i == index);
    }

    // Knife first (you always have it), otherwise any weapon you own, otherwise nothing in hand.
    void SelectFallback()
    {
        for (int i = 0; i < guns.Length; i++)
            if (IsOwned(i) && guns[i] != null && guns[i].melee) { Select(i); return; }
        for (int i = 0; i < guns.Length; i++)
            if (IsOwned(i) && guns[i] != null) { Select(i); return; }

        current = -1;
        foreach (Gun g in guns) if (g != null) g.gameObject.SetActive(false);
    }

    /// Called by WeaponPickup. mag/reserve are -1 for a fresh weapon from the map (full magazine, no reserve),
    /// or the saved ammo of a weapon someone dropped.
    public bool Pickup(int index, int bonusAmmo, int mag, int reserve)
    {
        if (index < 0 || index >= guns.Length || guns[index] == null) return false;
        Gun g = guns[index];

        if (IsOwned(index))
        {
            // You already carry this weapon: take its ammo instead
            int extra = bonusAmmo + Mathf.Max(0, mag) + Mathf.Max(0, reserve);
            g.AddAmmo(extra);
            PlayerInventory.Notify("+" + extra + " ammo for " + g.name);
            return true;
        }

        owned[index] = true;
        if (mag >= 0) g.SetAmmo(mag, Mathf.Max(0, reserve));
        else g.AddAmmo(bonusAmmo);

        Select(index);
        PlayerInventory.Notify("Picked up " + g.name);
        return true;
    }

    /// Called when a weapon is dropped.
    public void Remove(int index)
    {
        if (index < 0 || index >= guns.Length) return;
        owned[index] = false;
        if (guns[index] != null) guns[index].gameObject.SetActive(false);
        if (current == index) SelectFallback();
    }
}