using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Standalone converter that replaces the starter Knife with a 9mm Tactical Pistol:
/// - Converts the Player's "Knife" child Gun into a ranged "Pistol" (melee = false) with GunEffects & audio.
/// - Attaches a 3D Tactical Pistol skin (or optional custom FBX model) using your existing WeaponModel component
///   so it sits in the player's right hand and aims with the camera.
/// - Attaches GunScope (ScopeType.RedDot) so Right-Click aiming shows the Red Dot sight just like SMG/Rifle.
/// - Links Pistol and SMG reserveAmmo into one shared 9mm ammo pool.
/// - Build-safe material cloning (no Shader.Find stripping issues on itch.io builds).
/// </summary>
[DefaultExecutionOrder(-50)]
public class StarterPistolSetup : MonoBehaviour
{
    [Header("Master Toggle")]
    public bool enableStarterPistol = true;

    [Header("Pistol Combat Stats")]
    public string pistolDisplayName = "Pistol";
    public float pistolDamage = 18f;
    public float pistolRange = 85f;
    public float pistolFireRate = 6.5f;
    public bool pistolAutomatic = false;
    public int pistolMagazineSize = 15;
    public int startingShared9mmReserve = 75;
    public float pistolReloadTime = 1.4f;

    [Header("Red Dot Scope Settings")]
    public bool enableRedDotScope = true;
    public float redDotZoomFOV = 42f;
    public float redDotZoomSpeed = 10f;
    public float redDotSensitivityMultiplier = 0.65f;

    [Header("3D Pistol Skin (Optional Custom Model)")]
    [Tooltip("Leave empty to build a 3D Tactical Pistol skin automatically, or drag a pistol FBX/Prefab here.")]
    public GameObject customPistolModelPrefab;
    public Vector3 handOffset = new Vector3(0.02f, 0.02f, 0.22f);
    public BarrelAxis customModelBarrelAxis = BarrelAxis.PlusZ;
    public float customModelRoll = 0f;

    [Header("Shared 9mm Ammo Pool (Pistol <-> SMG)")]
    public bool shareReserveAmmoWithSMG = true;

    private WeaponSwitcher switcher;
    private Gun pistolGun;
    private Gun smgGun;
    private int pistolSlotIndex = -1;
    private int sharedReserveAmmo = 75;
    private Material cachedTemplateMaterial;
    private List<Material> createdMaterials = new List<Material>();

    void Awake()
    {
        if (!enableStarterPistol)
        {
            enabled = false;
            return;
        }

        SetupPistolAndSMGLink();
    }

    void Start()
    {
        if (!enableStarterPistol || pistolGun == null) return;

        // Ensure the player starts with the Pistol equipped in hand if no other gun is selected
        if (switcher != null && switcher.CurrentGun == null)
        {
            switcher.Select(pistolSlotIndex);
        }
    }

    void SetupPistolAndSMGLink()
    {
        switcher = GetComponent<WeaponSwitcher>();
        if (switcher == null) switcher = FindFirstObjectByType<WeaponSwitcher>();
        if (switcher == null || switcher.guns == null)
        {
            Debug.LogWarning("StarterPistolSetup: Could not find WeaponSwitcher on Player.");
            return;
        }

        // 1. Find the Knife (or Slot 3) and the SMG inside WeaponSwitcher.guns
        for (int i = 0; i < switcher.guns.Length; i++)
        {
            Gun g = switcher.guns[i];
            if (g == null) continue;

            string lower = g.name.ToLowerInvariant();
            if (g.melee || lower.Contains("knife") || lower.Contains("pistol"))
            {
                pistolGun = g;
                pistolSlotIndex = i;
            }
            else if (lower.Contains("smg") || lower.Contains("ump") || lower.Contains("uzi"))
            {
                smgGun = g;
            }
        }

        if (pistolGun == null)
        {
            Debug.LogWarning("StarterPistolSetup: No Knife slot found in WeaponSwitcher to convert into a Pistol.");
            return;
        }

        // 2. Make sure the Pistol slot is owned from the very start of the match
        if (switcher.owned != null && pistolSlotIndex >= 0 && pistolSlotIndex < switcher.owned.Length)
        {
            switcher.owned[pistolSlotIndex] = true;
        }

        // 3. Convert the Knife GameObject & Gun component into a ranged 9mm Pistol
        pistolGun.gameObject.name = pistolDisplayName;
        pistolGun.melee = false; // False so it shoots raycast bullets AND can pick up Ammo Boxes!
        pistolGun.damage = pistolDamage;
        pistolGun.range = pistolRange;
        pistolGun.fireRate = pistolFireRate;
        pistolGun.automatic = pistolAutomatic;
        pistolGun.maxAmmo = pistolMagazineSize;
        pistolGun.reloadTime = pistolReloadTime;
        pistolGun.baseSpread = 0.004f;
        pistolGun.spreadPerShot = 0.005f;
        pistolGun.maxSpread = 0.035f;
        pistolGun.recoilPitch = 0.85f;
        pistolGun.recoilYaw = 0.25f;

        // Ensure GunEffects is present so bullets show tracer lines & hit sparks
        GunEffects fx = pistolGun.GetComponent<GunEffects>();
        if (fx == null) fx = pistolGun.gameObject.AddComponent<GunEffects>();
        pistolGun.effects = fx;

        // Copy gunshot / reload audio clips from SMG if the Knife didn't have gun sounds assigned
        if (smgGun != null)
        {
            if (pistolGun.shotClip == null && smgGun.shotClip != null)
                pistolGun.shotClip = smgGun.shotClip;
            if (pistolGun.reloadClip == null && smgGun.reloadClip != null)
                pistolGun.reloadClip = smgGun.reloadClip;
        }

        // Initialize full magazine + shared 9mm reserve ammo
        sharedReserveAmmo = startingShared9mmReserve;
        pistolGun.SetAmmo(pistolMagazineSize, sharedReserveAmmo);
        if (smgGun != null && shareReserveAmmoWithSMG)
        {
            smgGun.reserveAmmo = sharedReserveAmmo;
        }

        // 4. Attach GunScope (Red Dot) so Right-Click zooms in with the Red Dot sight
        if (enableRedDotScope)
        {
            GunScope scope = pistolGun.GetComponent<GunScope>();
            if (scope == null) scope = pistolGun.gameObject.AddComponent<GunScope>();
            scope.type = ScopeType.RedDot;
            scope.zoomFOV = redDotZoomFOV;
            scope.zoomSpeed = redDotZoomSpeed;
            scope.sensitivityMultiplier = redDotSensitivityMultiplier;
        }

        // 5. Attach the 3D Pistol Model + WeaponModel component so it renders in the Player's right hand
        BuildOrAttachPistol3DSkin(pistolGun.transform);
    }

    void LateUpdate()
    {
        if (!enableStarterPistol || pistolGun == null) return;

        // Keep the starter Pistol permanently owned even if the player accidentally presses G (Drop Weapon)
        if (switcher != null && switcher.owned != null &&
            pistolSlotIndex >= 0 && pistolSlotIndex < switcher.owned.Length &&
            !switcher.owned[pistolSlotIndex])
        {
            switcher.owned[pistolSlotIndex] = true;
            if (switcher.CurrentGun == null) switcher.Select(pistolSlotIndex);
        }

        // Synchronize the 9mm Reserve Ammo pool between Pistol and SMG
        if (shareReserveAmmoWithSMG && smgGun != null)
        {
            if (pistolGun.reserveAmmo != sharedReserveAmmo)
            {
                sharedReserveAmmo = Mathf.Max(0, pistolGun.reserveAmmo);
                smgGun.reserveAmmo = sharedReserveAmmo;
            }
            else if (smgGun.reserveAmmo != sharedReserveAmmo)
            {
                sharedReserveAmmo = Mathf.Max(0, smgGun.reserveAmmo);
                pistolGun.reserveAmmo = sharedReserveAmmo;
            }
        }
    }

    // =========================================================================
    // 3D PISTOL SKIN BUILDER (USES YOUR EXISTING WeaponModel.cs)
    // =========================================================================

    void BuildOrAttachPistol3DSkin(Transform pistolParent)
    {
        WeaponModel existingModel = pistolParent.GetComponentInChildren<WeaponModel>(true);
        if (existingModel != null) return;

        GameObject skinRoot;

        if (customPistolModelPrefab != null)
        {
            skinRoot = Instantiate(customPistolModelPrefab, pistolParent);
            skinRoot.name = "Pistol_3DModel";
            skinRoot.transform.localPosition = Vector3.zero;
            skinRoot.transform.localRotation = Quaternion.identity;

            WeaponModel wm = skinRoot.GetComponent<WeaponModel>();
            if (wm == null) wm = skinRoot.AddComponent<WeaponModel>();
            wm.bone = HumanBodyBones.RightHand;
            wm.barrel = customModelBarrelAxis;
            wm.roll = customModelRoll;
            wm.handOffset = handOffset;
            wm.aimWithCamera = true;
            return;
        }

        skinRoot = new GameObject("TacticalPistol_9mm_Skin");
        skinRoot.transform.SetParent(pistolParent, false);
        skinRoot.transform.localPosition = Vector3.zero;
        skinRoot.transform.localRotation = Quaternion.identity;

        Material slideMat = CreateSafeMaterial(new Color(0.16f, 0.18f, 0.21f)); // Dark gunmetal slide
        Material frameMat = CreateSafeMaterial(new Color(0.25f, 0.27f, 0.24f)); // Tactical olive-grey polymer frame
        Material barrelMat = CreateSafeMaterial(new Color(0.10f, 0.11f, 0.12f)); // Black steel barrel & mag
        Material accentMat = CreateSafeMaterial(new Color(0.88f, 0.68f, 0.18f)); // Brass ejection port
        Material sightMat = CreateSafeMaterial(new Color(0.20f, 0.98f, 0.35f)); // High-vis tritium iron sights

        // 1. Top Metallic Slide (points along +Z)
        MakePart(PrimitiveType.Cube, "Slide", skinRoot.transform,
            new Vector3(0f, 0.045f, 0.04f), Vector3.zero, new Vector3(0.042f, 0.048f, 0.24f), slideMat);

        // 2. Brass Ejection Port on Slide
        MakePart(PrimitiveType.Cube, "EjectionPort", skinRoot.transform,
            new Vector3(0.014f, 0.058f, 0.03f), Vector3.zero, new Vector3(0.020f, 0.026f, 0.055f), accentMat);

        // 3. Lower Polymer Receiver / Accessory Rail
        MakePart(PrimitiveType.Cube, "LowerFrame", skinRoot.transform,
            new Vector3(0f, 0.012f, 0.035f), Vector3.zero, new Vector3(0.038f, 0.026f, 0.22f), frameMat);

        // 4. Extended Threaded Barrel (protruding from front +Z)
        MakePart(PrimitiveType.Cylinder, "Barrel", skinRoot.transform,
            new Vector3(0f, 0.045f, 0.175f), new Vector3(90f, 0f, 0f), new Vector3(0.022f, 0.035f, 0.022f), barrelMat);

        // 5. Angled Ergonomic Pistol Grip
        MakePart(PrimitiveType.Cube, "PistolGrip", skinRoot.transform,
            new Vector3(0f, -0.048f, -0.045f), new Vector3(16f, 0f, 0f), new Vector3(0.036f, 0.115f, 0.052f), frameMat);

        // 6. Magazine Baseplate at Bottom of Grip
        MakePart(PrimitiveType.Cube, "MagBase", skinRoot.transform,
            new Vector3(0f, -0.105f, -0.062f), new Vector3(16f, 0f, 0f), new Vector3(0.040f, 0.018f, 0.058f), barrelMat);

        // 7. Trigger Guard
        MakePart(PrimitiveType.Cube, "TriggerGuardBottom", skinRoot.transform,
            new Vector3(0f, -0.022f, 0.015f), Vector3.zero, new Vector3(0.018f, 0.010f, 0.055f), barrelMat);
        MakePart(PrimitiveType.Cube, "TriggerGuardFront", skinRoot.transform,
            new Vector3(0f, -0.008f, 0.040f), Vector3.zero, new Vector3(0.018f, 0.032f, 0.010f), barrelMat);

        // 8. Glowing Tritium Front & Rear Sights on top of Slide
        MakePart(PrimitiveType.Cube, "FrontSight", skinRoot.transform,
            new Vector3(0f, 0.074f, 0.145f), Vector3.zero, new Vector3(0.008f, 0.012f, 0.012f), sightMat);
        MakePart(PrimitiveType.Cube, "RearSightLeft", skinRoot.transform,
            new Vector3(-0.011f, 0.074f, -0.065f), Vector3.zero, new Vector3(0.007f, 0.012f, 0.012f), sightMat);
        MakePart(PrimitiveType.Cube, "RearSightRight", skinRoot.transform,
            new Vector3(0.011f, 0.074f, -0.065f), Vector3.zero, new Vector3(0.007f, 0.012f, 0.012f), sightMat);

        // Attach your WeaponModel component so it locks to the Player's RightHand and aims with the camera
        WeaponModel weaponModel = skinRoot.AddComponent<WeaponModel>();
        weaponModel.bone = HumanBodyBones.RightHand;
        weaponModel.barrel = BarrelAxis.PlusZ;
        weaponModel.roll = 0f;
        weaponModel.handOffset = handOffset;
        weaponModel.aimWithCamera = true;
    }

    GameObject MakePart(PrimitiveType type, string partName, Transform parent,
        Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = partName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(localEuler);
        go.transform.localScale = localScale;

        // Destroy collider immediately so it never blocks the player's own raycast bullets
        Collider c = go.GetComponent<Collider>();
        if (c != null) Destroy(c);

        Renderer r = go.GetComponent<Renderer>();
        if (r != null && mat != null) r.sharedMaterial = mat;

        return go;
    }

    Material CreateSafeMaterial(Color color)
    {
        if (cachedTemplateMaterial == null)
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Renderer r = temp.GetComponent<Renderer>();
            if (r != null && r.sharedMaterial != null)
            {
                cachedTemplateMaterial = r.sharedMaterial;
            }
            Destroy(temp);
        }

        Material mat;
        if (cachedTemplateMaterial != null)
        {
            mat = new Material(cachedTemplateMaterial);
        }
        else
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            mat = new Material(shader);
        }

        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

        createdMaterials.Add(mat);
        return mat;
    }

    void OnDestroy()
    {
        for (int i = 0; i < createdMaterials.Count; i++)
        {
            if (createdMaterials[i] != null) Destroy(createdMaterials[i]);
        }
        createdMaterials.Clear();
    }
}