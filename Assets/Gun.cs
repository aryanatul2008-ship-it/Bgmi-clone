using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// Raycast gun for the custom PlayerMovement + Cinemachine (Orbital Follow) setup.
/// Each weapon is its own child object of the Player (Rifle, SMG, Sniper...).
public class Gun : MonoBehaviour
{
    [Header("Gun Stats")]
    public float damage = 10f;
    public float range = 100f;
    public float fireRate = 10f;       // bullets per second
    public bool automatic = true;

    [Header("Melee (tick for the Knife)")]
    public bool melee = false;         // true = no ammo, short-range stab (Fire Rate = stabs per second)
    public float meleeRange = 2.5f;
    public float meleeRadius = 0.45f;
    public bool logStabs = true;       // prints what each stab hit in the Console (untick when you're done testing)

    [Header("Ammo")]
    public int maxAmmo = 30;           // magazine size
    public int reserveAmmo = 90;
    public float reloadTime = 2f;

    [Header("References")]
    public Camera mainCamera;                       // auto-filled if empty
    public CinemachineOrbitalFollow orbitalCamera;  // auto-found, used for recoil
    public GunEffects effects;                      // auto-found on the same object (tracer, flash, hit puffs)
    public ParticleSystem muzzleFlash;              // optional extra, if you make your own particle effect
    public AudioSource audioSource;                 // auto-created if empty
    public AudioClip shotClip, reloadClip;
    [Range(0f, 1f)] public float shotVolume = 0.8f;
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

    // A weapon that has never been equipped hasn't run Start() yet, so report a full magazine for the HUD.
    public int CurrentAmmo => started ? currentAmmo : maxAmmo;
    public int ReserveAmmo => reserveAmmo;
    public bool IsReloading => isReloading;

    int currentAmmo;
    bool started;
    bool isReloading;
    float nextTimeToFire;
    float currentSpread;
    float pendingPitch, pendingYaw;
    GunScope scope;                    // optional: aiming (right mouse button) tightens the spread

    void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (orbitalCamera == null) orbitalCamera = FindAnyObjectByType<CinemachineOrbitalFollow>();
        if (effects == null) effects = GetComponent<GunEffects>();
        scope = GetComponent<GunScope>();
        if (effects == null && melee) effects = gameObject.AddComponent<GunEffects>(); // gives the knife its red hit puff
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D sound: always heard at full strength by the player
        }
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (!started) currentAmmo = maxAmmo; // a weapon picked up with saved ammo (SetAmmo) keeps it
        currentSpread = baseSpread;
        started = true;
    }

    void OnDisable() { isReloading = false; } // weapon switched mid-reload

    void Update()
    {
        HandleCursor();
        ApplyRecoil();
        currentSpread = Mathf.MoveTowards(currentSpread, baseSpread, spreadRecovery * Time.deltaTime);

        if (Cursor.lockState != CursorLockMode.Locked || isReloading) return;

        if (melee)
        {
            if (Input.GetButtonDown("Fire1") && Time.time >= nextTimeToFire) Stab();
            return;
        }

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
        else if (!PlayerInventory.IsOpen && Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0))
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
        float spread = currentSpread * (scope != null ? scope.SpreadMultiplier : 1f); // aiming tightens the spread
        Vector3 dir = cam.forward
                    + cam.right * Random.Range(-spread, spread)
                    + cam.up * Random.Range(-spread, spread);
        dir.Normalize();

        Vector3 endPoint = cam.position + dir * range;
        Vector3 normal = -dir;
        bool hitSomething = false;
        bool hitCharacter = false;

        if (Physics.Raycast(cam.position, dir, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            hitSomething = true;
            endPoint = hit.point;
            normal = hit.normal;

            Health h = hit.collider.GetComponentInParent<Health>();
            if (h != null)
            {
                h.TakeDamage(damage, true); // true = fired by the player, so kills get counted
                hitCharacter = true;
            }
            Debug.DrawLine(cam.position, hit.point, Color.red, 0.5f);
        }

        if (effects != null) effects.Fire(endPoint, normal, hitSomething, hitCharacter);
        if (muzzleFlash) muzzleFlash.Play();
        if (audioSource && shotClip)
        {
            audioSource.pitch = Random.Range(0.95f, 1.05f); // tiny variation so automatic fire doesn't sound robotic
            audioSource.PlayOneShot(shotClip, shotVolume);
        }

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
        if (audioSource && reloadClip)
        {
            audioSource.pitch = 1f;
            audioSource.PlayOneShot(reloadClip);
        }
        yield return new WaitForSeconds(reloadTime);
        int needed = Mathf.Min(maxAmmo - currentAmmo, reserveAmmo);
        currentAmmo += needed;
        reserveAmmo -= needed;
        isReloading = false;
    }

    void Stab()
    {
        nextTimeToFire = Time.time + 1f / fireRate;

        Transform body = transform.root;                       // the Player
        Vector3 forward = body.forward;                        // the player always faces where the camera looks
        Vector3 centre = body.position + forward * (meleeRange * 0.5f);
        float radius = meleeRange * 0.5f + meleeRadius;

        // Find the nearest living non-player in front of you inside the reach
        Collider[] found = Physics.OverlapSphere(centre, radius, hitMask, QueryTriggerInteraction.Ignore);
        Health best = null;
        float bestDist = float.MaxValue;
        Vector3 bestPoint = Vector3.zero;

        foreach (Collider c in found)
        {
            Health h = c.GetComponentInParent<Health>();
            if (h == null || h.isPlayer || h.IsDead) continue;

            Vector3 point = c.ClosestPoint(body.position);
            Vector3 flat = point - body.position;
            flat.y = 0f;
            float d = flat.magnitude;
            if (d > meleeRange) continue;
            if (d > 0.05f && Vector3.Dot(flat.normalized, forward) < 0.3f) continue; // must be roughly in front of you

            if (d < bestDist) { best = h; bestDist = d; bestPoint = point; }
        }

        if (best != null)
        {
            best.TakeDamage(damage, true);
            if (effects != null) effects.HitPuff(bestPoint, (body.position - bestPoint).normalized, true);
        }

        if (logStabs)
            Debug.Log(best != null ? "Knife hit " + best.name + " for " + damage
                                   : "Knife stab: nothing within " + meleeRange + " m in front of you");

        if (audioSource && shotClip)
        {
            audioSource.pitch = Random.Range(0.95f, 1.05f);
            audioSource.PlayOneShot(shotClip, shotVolume);
        }
    }

    // Used when a dropped weapon is picked up again, so it keeps the ammo it had.
    public void SetAmmo(int mag, int reserve)
    {
        currentAmmo = Mathf.Clamp(mag, 0, maxAmmo);
        reserveAmmo = Mathf.Max(0, reserve);
        started = true;
    }

    public void AddAmmo(int amount) { reserveAmmo += amount; }
}