using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// Raycast gun for the custom PlayerMovement + Cinemachine (Orbital Follow) setup.
/// Attach to the Player ONLY (not Enemy / Canvas).
public class Gun : MonoBehaviour
{
    [Header("Gun Stats")]
    public float damage = 10f;
    public float range = 100f;
    public float fireRate = 10f;       // bullets per second
    public bool automatic = true;

    [Header("Ammo")]
    public int maxAmmo = 30;           // magazine size
    public int reserveAmmo = 90;
    public float reloadTime = 2f;

    [Header("References")]
    public Camera mainCamera;                       // auto-filled if empty
    public CinemachineOrbitalFollow orbitalCamera;  // auto-found, used for recoil
    public ParticleSystem muzzleFlash;
    public AudioSource audioSource;
    public AudioClip shotClip, reloadClip;
    public LayerMask hitMask = Physics.DefaultRaycastLayers; // everything except Ignore Raycast

    [Header("Spread (0.01 is about a 0.6 degree cone)")]
    public float baseSpread = 0.005f;
    public float spreadPerShot = 0.004f;
    public float maxSpread = 0.05f;
    public float spreadRecovery = 0.1f;   // per second

    [Header("Recoil (degrees per shot)")]
    public float recoilPitch = 1.0f;      // vertical kick
    public float recoilYaw = 0.4f;        // random +/- sideways kick
    public float recoilSmoothing = 20f;   // higher = snappier kick

    public int CurrentAmmo => currentAmmo; // for the HUD later
    public int ReserveAmmo => reserveAmmo;
    public bool IsReloading => isReloading;

    int currentAmmo;
    bool isReloading;
    float nextTimeToFire;
    float currentSpread;
    float pendingPitch, pendingYaw;

    void Awake()
    {
        // These two lines remove the UnassignedReferenceException for good
        if (mainCamera == null) mainCamera = Camera.main;
        if (orbitalCamera == null) orbitalCamera = FindAnyObjectByType<CinemachineOrbitalFollow>();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        currentAmmo = maxAmmo;
        currentSpread = baseSpread;
    }

    void OnDisable() { isReloading = false; } // weapon switched mid-reload

    void Update()
    {
        HandleCursor();
        ApplyRecoil();
        currentSpread = Mathf.MoveTowards(currentSpread, baseSpread, spreadRecovery * Time.deltaTime);

        if (Cursor.lockState != CursorLockMode.Locked || isReloading) return;

        if (Input.GetKeyDown(KeyCode.R)) { StartReload(); return; }

        bool wantsFire = automatic ? Input.GetButton("Fire1") : Input.GetButtonDown("Fire1");
        if (wantsFire && Time.time >= nextTimeToFire)
        {
            if (currentAmmo <= 0) { StartReload(); return; }
            nextTimeToFire = Time.time + 1f / fireRate;
            Shoot();
        }
    }

    void HandleCursor()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            nextTimeToFire = Time.time + 0.2f; // the click that re-locks the cursor shouldn't also fire
        }
    }

    void Shoot()
    {
        currentAmmo--;

        // Random offset on the camera's right/up axes gives a cone of fire
        Transform cam = mainCamera.transform;
        Vector3 dir = cam.forward
                    + cam.right * Random.Range(-currentSpread, currentSpread)
                    + cam.up * Random.Range(-currentSpread, currentSpread);
        dir.Normalize();

        if (Physics.Raycast(cam.position, dir, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            Health h = hit.collider.GetComponentInParent<Health>();
            if (h != null) h.TakeDamage(damage);
            Debug.DrawLine(cam.position, hit.point, Color.red, 0.5f);
        } 
        if (muzzleFlash) muzzleFlash.Play();
        if (audioSource && shotClip) audioSource.PlayOneShot(shotClip);

        // Spread grows while firing, recoil gets queued and applied smoothly
        currentSpread = Mathf.Min(maxSpread, currentSpread + spreadPerShot);
        pendingPitch += recoilPitch;
        pendingYaw += Random.Range(-recoilYaw, recoilYaw);
    }

    void ApplyRecoil()
    {
        if (orbitalCamera == null) return;
        if (Mathf.Approximately(pendingPitch, 0f) && Mathf.Approximately(pendingYaw, 0f)) return;

        float k = 1f - Mathf.Exp(-recoilSmoothing * Time.deltaTime);
        float p = pendingPitch * k;
        float y = pendingYaw * k;
        pendingPitch -= p;
        pendingYaw -= y;

        // Lower vertical value = camera looks higher
        var v = orbitalCamera.VerticalAxis;
        v.Value = Mathf.Clamp(v.Value - p, v.Range.x, v.Range.y);
        orbitalCamera.VerticalAxis = v;

        var h = orbitalCamera.HorizontalAxis;
        h.Value += y;
        orbitalCamera.HorizontalAxis = h;
    }

    void StartReload()
    {
        if (isReloading || currentAmmo >= maxAmmo || reserveAmmo <= 0) return;
        StartCoroutine(Reload());
    }

    IEnumerator Reload()
    {
        isReloading = true;
        if (audioSource && reloadClip) audioSource.PlayOneShot(reloadClip);
        yield return new WaitForSeconds(reloadTime);
        int needed = Mathf.Min(maxAmmo - currentAmmo, reserveAmmo);
        currentAmmo += needed;
        reserveAmmo -= needed;
        isReloading = false;
    }

    public void AddAmmo(int amount) { reserveAmmo += amount; }
}