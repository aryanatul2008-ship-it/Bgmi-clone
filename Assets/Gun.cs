using UnityEngine;
using System.Collections;

public class Gun : MonoBehaviour
{
    // Gun Stats
    public float damage = 10f;
    public float range = 100f;
    public float fireRate = 10f; // Bullets fired per second

    // Ammo System
    public int maxAmmo = 30;
    private int currentAmmo;
    public float reloadTime = 2f; // Takes 2 seconds to reload
    private bool isReloading = false;

    public Camera mainCamera;
    private float nextTimeToFire = 0f;

    void Start()
    {
        // Fill the magazine when the game starts
        currentAmmo = maxAmmo;
    }

    void Update()
    {
        if (isReloading)
            return;

        // Press 'R' to reload, or auto-reload if the magazine is empty
        if (currentAmmo <= 0 || Input.GetKeyDown(KeyCode.R))
        {
            StartCoroutine(Reload());
            return;
        }

        // GetButton (instead of GetButtonDown) allows automatic fire by holding the mouse
        if (Input.GetButton("Fire1") && Time.time >= nextTimeToFire)
        {
            nextTimeToFire = Time.time + 1f / fireRate;
            Shoot();
        }
    }

    // A Coroutine allows us to pause code execution (simulate reload time)
    IEnumerator Reload()
    {
        isReloading = true;
        Debug.Log("Reloading...");

        // Wait for exactly 'reloadTime' seconds before moving to the next line
        yield return new WaitForSeconds(reloadTime);

        currentAmmo = maxAmmo;
        isReloading = false;
        Debug.Log("Reload Complete! Ammo is back to " + currentAmmo);
    }

    void Shoot()
    {
        currentAmmo--; // Subtract 1 bullet
        Debug.Log("BANG! Ammo left: " + currentAmmo);

        RaycastHit hit;
        if (Physics.Raycast(mainCamera.transform.position, mainCamera.transform.forward, out hit, range))
        {
            Target target = hit.transform.GetComponent<Target>();
            if (target != null)
            {
                target.TakeDamage(damage);
            }
        }
    }
}