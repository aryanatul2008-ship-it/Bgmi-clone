using UnityEngine;

/// Put on the Player. Each weapon is a CHILD object of the Player with its own Gun component.
/// Add those Gun components to "guns" and mark which ones you own.
/// Keys 1/2/3 (number row or numpad), Q/E, or the scroll wheel switch weapons.
/// Messages in the Console tell you exactly why a switch worked or didn't.
public class WeaponSwitcher : MonoBehaviour
{
    public Gun[] guns;
    public bool[] owned;   // same length as guns; element 0 should start as true

    int current = -1;

    public Gun CurrentGun => (current >= 0 && current < guns.Length) ? guns[current] : null;

    void Start()
    {
        if (owned == null || owned.Length != guns.Length)
            Debug.LogWarning("WeaponSwitcher: 'Owned' must have the same size as 'Guns'.");
        Select(0);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) Select(0);
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) Select(1);
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) Select(2);

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
            if (idx < owned.Length && owned[idx] && guns[idx] != null) { Select(idx); return; }
        }
    }

    public void Select(int index)
    {
        if (index < 0 || index >= guns.Length)
        {
            Debug.LogWarning("WeaponSwitcher: there is no weapon slot " + (index + 1));
            return;
        }
        if (guns[index] == null)
        {
            Debug.LogWarning("WeaponSwitcher: slot " + index + " in 'Guns' is empty. Drag the weapon object into it.");
            return;
        }
        if (index >= owned.Length || !owned[index])
        {
            Debug.LogWarning("WeaponSwitcher: " + guns[index].name + " is not owned yet (tick it in 'Owned' or pick it up).");
            return;
        }

        current = index;
        for (int i = 0; i < guns.Length; i++)
            if (guns[i] != null) guns[i].gameObject.SetActive(i == index);

        Debug.Log("WeaponSwitcher: switched to " + guns[index].name);
    }

    public void Pickup(int index, int bonusAmmo)
    {
        if (index < 0 || index >= guns.Length || guns[index] == null) return;
        owned[index] = true;
        guns[index].AddAmmo(bonusAmmo);
        Select(index);
    }
}