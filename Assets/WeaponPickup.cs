using UnityEngine;

/// Put on a world object with a Trigger collider. Walk into it to pick up weapon #weaponIndex.
public class WeaponPickup : MonoBehaviour
{
    public int weaponIndex = 1;
    public int bonusAmmo = 60;

    void OnTriggerEnter(Collider other)
    {
        WeaponSwitcher sw = other.GetComponentInParent<WeaponSwitcher>();
        if (sw == null) return;
        sw.Pickup(weaponIndex, bonusAmmo);
        Destroy(gameObject);
    }

    void Update() { transform.Rotate(0, 90f * Time.deltaTime, 0); } // spin so it's visible
}