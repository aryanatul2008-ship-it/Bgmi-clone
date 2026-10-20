using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Standalone Battle Royale Loot Spawner & Visual Upgrader.
/// - Spawns configurable counts of Medkits, Bandages, Ammo Crates, Rifles, SMGs, and Snipers across the 300x300 map.
/// - Uses downward raycasts so supplies rest accurately on the terrain.
/// - Replaces plain cube meshes on both newly spawned pickups AND existing scene pickups with recognizable
///   multi-part 3D primitive models (Medkit with red cross, Bandage roll, Military Ammo Box, Weapon Cases).
/// - Requires ZERO edits to ItemPickup.cs, WeaponPickup.cs, or PlayerInventory.cs.
/// </summary>
public class LootSpawner : MonoBehaviour
{
    [Header("Master Toggles")]
    [Tooltip("Ticked = spawns extra supplies across the map at the start of the match.")]
    public bool spawnExtraLoot = true;

    [Tooltip("Ticked = upgrades plain cube pickups already in the scene into 3D Medkit/Ammo/Gun models.")]
    public bool upgradeExistingScenePickups = true;

    [Header("Medical & Ammo Counts to Spawn")]
    public int medkitCount = 18;
    public int bandageCount = 28;
    public int ammoBoxCount = 25;
    public float ammoPerBox = 30f;

    [Header("Weapon Counts to Spawn")]
    public int rifleCount = 10;
    public int smgCount = 12;
    public int sniperCount = 6;
    public int weaponBonusAmmo = 30;

    [Header("Map Placement Settings (300x300 Map)")]
    [Tooltip("Half-size of the loot spawn area around (0,0). 135 keeps items well inside the -150..150 terrain.")]
    public float mapHalfExtent = 135f;
    [Tooltip("Minimum distance between spawned pickups so they don't overlap.")]
    public float minSpacing = 4.5f;
    [Tooltip("Avoids spawning loot on steep mountain cliffs.")]
    public float maxGroundSlopeAngle = 32f;

    private LayerMask groundMask;
    private Transform lootContainer;
    private List<Vector3> spawnedPositions = new List<Vector3>();
    private List<Material> createdMaterials = new List<Material>();

    // Cached weapon indices from Player's WeaponSwitcher (Rifle=0, SMG=1, Sniper=2 by default)
    private int rifleIndex = 0;
    private int smgIndex = 1;
    private int sniperIndex = 2;

    void Awake()
    {
        // Ignore Layer 2 ("Ignore Raycast", used by Player)
        groundMask = ~(1 << 2);
    }

    void Start()
    {
        DetectWeaponIndicesFromPlayer();

        if (upgradeExistingScenePickups)
        {
            UpgradeExistingPickupsInScene();
        }

        if (spawnExtraLoot)
        {
            SpawnAllMapLoot();
        }
    }

    /// <summary>
    /// Reads the Player's WeaponSwitcher array automatically so Rifle, SMG, and Sniper indices always match your setup.
    /// </summary>
    void DetectWeaponIndicesFromPlayer()
    {
        WeaponSwitcher switcher = FindFirstObjectByType<WeaponSwitcher>(FindObjectsInactive.Include);
        if (switcher == null || switcher.guns == null) return;

        for (int i = 0; i < switcher.guns.Length; i++)
        {
            Gun g = switcher.guns[i];
            if (g == null || g.melee) continue;

            string gunName = g.name.ToLowerInvariant();
            if (gunName.Contains("rifle") || gunName.Contains("ak")) rifleIndex = i;
            else if (gunName.Contains("smg") || gunName.Contains("ump") || gunName.Contains("uzi")) smgIndex = i;
            else if (gunName.Contains("sniper") || gunName.Contains("kar") || gunName.Contains("awm")) sniperIndex = i;
        }
    }

    void UpgradeExistingPickupsInScene()
    {
        ItemPickup[] existingItems = FindObjectsByType<ItemPickup>(FindObjectsSortMode.None);
        foreach (ItemPickup item in existingItems)
        {
            if (item == null) continue;
            spawnedPositions.Add(item.transform.position);
            DecorateExistingItemPickup(item);
        }

        WeaponPickup[] existingWeapons = FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None);
        foreach (WeaponPickup wp in existingWeapons)
        {
            if (wp == null) continue;
            spawnedPositions.Add(wp.transform.position);
            DecorateExistingWeaponPickup(wp);
        }
    }

    void SpawnAllMapLoot()
    {
        lootContainer = new GameObject("SpawnedMapLoot").transform;

        // 1. Spawn Medkits
        for (int i = 0; i < medkitCount; i++)
        {
            if (TryFindLootSpot(out Vector3 pos))
                CreateItemPickupObject("Pickup_Medkit_" + i, pos, ItemPickup.ItemType.Heal, 0f);
        }

        // 2. Spawn Bandages
        for (int i = 0; i < bandageCount; i++)
        {
            if (TryFindLootSpot(out Vector3 pos))
                CreateItemPickupObject("Pickup_Bandage_" + i, pos, ItemPickup.ItemType.Bandage, 0f);
        }

        // 3. Spawn Ammo Boxes
        for (int i = 0; i < ammoBoxCount; i++)
        {
            if (TryFindLootSpot(out Vector3 pos))
                CreateItemPickupObject("Pickup_Ammo_" + i, pos, ItemPickup.ItemType.Ammo, ammoPerBox);
        }

        // 4. Spawn Rifles
        for (int i = 0; i < rifleCount; i++)
        {
            if (TryFindLootSpot(out Vector3 pos))
                CreateWeaponPickupObject("Pickup_Rifle_" + i, pos, rifleIndex, weaponBonusAmmo);
        }

        // 5. Spawn SMGs
        for (int i = 0; i < smgCount; i++)
        {
            if (TryFindLootSpot(out Vector3 pos))
                CreateWeaponPickupObject("Pickup_SMG_" + i, pos, smgIndex, weaponBonusAmmo);
        }

        // 6. Spawn Snipers
        for (int i = 0; i < sniperCount; i++)
        {
            if (TryFindLootSpot(out Vector3 pos))
                CreateWeaponPickupObject("Pickup_Sniper_" + i, pos, sniperIndex, weaponBonusAmmo);
        }
    }

    bool TryFindLootSpot(out Vector3 validPos)
    {
        float minSqDist = minSpacing * minSpacing;

        for (int attempt = 0; attempt < 30; attempt++)
        {
            float rx = Random.Range(-mapHalfExtent, mapHalfExtent);
            float rz = Random.Range(-mapHalfExtent, mapHalfExtent);
            Vector3 rayStart = new Vector3(rx, 220f, rz);

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 260f, groundMask, QueryTriggerInteraction.Ignore))
            {
                // Skip steep mountain slopes
                if (Vector3.Angle(hit.normal, Vector3.up) > maxGroundSlopeAngle) continue;

                // Skip rooftops or treetops that are way up in the air (keep loot on ground level)
                if (hit.point.y > 4.5f) continue;

                Vector3 candidate = hit.point + Vector3.up * 0.45f;

                bool tooClose = false;
                for (int i = 0; i < spawnedPositions.Count; i++)
                {
                    if ((spawnedPositions[i] - candidate).sqrMagnitude < minSqDist)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    spawnedPositions.Add(candidate);
                    validPos = candidate;
                    return true;
                }
            }
        }

        validPos = Vector3.zero;
        return false;
    }

    // =========================================================================
    // SPAWN & DECORATE PICKUPS
    // =========================================================================

    void CreateItemPickupObject(string objName, Vector3 pos, ItemPickup.ItemType itemType, float ammoAmount)
    {
        GameObject root = new GameObject(objName);
        root.transform.SetParent(lootContainer, false);
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        // Generous trigger collider so walking over it picks it up smoothly
        BoxCollider col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1.1f, 1.1f, 1.1f);

        ItemPickup pickup = root.AddComponent<ItemPickup>();
        pickup.type = itemType;
        pickup.amount = ammoAmount;

        BuildItemVisuals(root.transform, itemType);
    }

    void CreateWeaponPickupObject(string objName, Vector3 pos, int weaponIdx, int bonusAmmo)
    {
        GameObject root = new GameObject(objName);
        root.transform.SetParent(lootContainer, false);
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        BoxCollider col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1.3f, 1.1f, 1.3f);

        WeaponPickup pickup = root.AddComponent<WeaponPickup>();
        pickup.weaponIndex = weaponIdx;
        pickup.bonusAmmo = bonusAmmo;
        pickup.magAmmo = -1;
        pickup.reserveAmmo = -1;

        BuildWeaponVisuals(root.transform, weaponIdx);
    }

    void DecorateExistingItemPickup(ItemPickup item)
    {
        // Hide the default cube mesh renderer on the root object while keeping its trigger collider intact
        MeshRenderer rootRenderer = item.GetComponent<MeshRenderer>();
        if (rootRenderer != null) rootRenderer.enabled = false;

        // Reset scale to (1,1,1) if it was a squashed cube so our child model looks right
        BoxCollider box = item.GetComponent<BoxCollider>();
        Vector3 oldScale = item.transform.localScale;
        item.transform.localScale = Vector3.one;
        if (box != null)
        {
            box.size = new Vector3(
                Mathf.Max(1.0f, oldScale.x),
                Mathf.Max(1.0f, oldScale.y),
                Mathf.Max(1.0f, oldScale.z)
            );
        }

        BuildItemVisuals(item.transform, item.type);
    }

    void DecorateExistingWeaponPickup(WeaponPickup wp)
    {
        MeshRenderer rootRenderer = wp.GetComponent<MeshRenderer>();
        if (rootRenderer != null) rootRenderer.enabled = false;

        BoxCollider box = wp.GetComponent<BoxCollider>();
        Vector3 oldScale = wp.transform.localScale;
        wp.transform.localScale = Vector3.one;
        if (box != null)
        {
            box.size = new Vector3(
                Mathf.Max(1.2f, oldScale.x),
                Mathf.Max(1.0f, oldScale.y),
                Mathf.Max(1.2f, oldScale.z)
            );
        }

        BuildWeaponVisuals(wp.transform, wp.weaponIndex);
    }

    // =========================================================================
    // 3D PROCEDURAL MODELS FOR ITEMS & WEAPONS
    // =========================================================================

    void BuildItemVisuals(Transform parent, ItemPickup.ItemType type)
    {
        GameObject modelRoot = new GameObject("VisualModel");
        modelRoot.transform.SetParent(parent, false);

        if (type == ItemPickup.ItemType.Heal)
        {
            // MEDKIT: White medical case + Red Cross on top & front + dark handle
            Material whiteMat = CreateSafeMaterial(new Color(0.95f, 0.96f, 0.98f));
            Material redMat = CreateSafeMaterial(new Color(0.88f, 0.14f, 0.14f));
            Material handleMat = CreateSafeMaterial(new Color(0.18f, 0.20f, 0.22f));
            Material glowMat = CreateSafeMaterial(new Color(0.2f, 0.95f, 0.35f));

            // Main Case Body
            MakePart(PrimitiveType.Cube, "MedkitCase", modelRoot.transform,
                Vector3.zero, Vector3.zero, new Vector3(0.62f, 0.42f, 0.46f), whiteMat);

            // Red Cross on Top
            MakePart(PrimitiveType.Cube, "CrossTopA", modelRoot.transform,
                new Vector3(0f, 0.215f, 0f), Vector3.zero, new Vector3(0.32f, 0.02f, 0.10f), redMat);
            MakePart(PrimitiveType.Cube, "CrossTopB", modelRoot.transform,
                new Vector3(0f, 0.215f, 0f), Vector3.zero, new Vector3(0.10f, 0.02f, 0.32f), redMat);

            // Red Cross on Front & Back
            MakePart(PrimitiveType.Cube, "CrossFrontA", modelRoot.transform,
                new Vector3(0f, 0f, 0.235f), Vector3.zero, new Vector3(0.26f, 0.08f, 0.02f), redMat);
            MakePart(PrimitiveType.Cube, "CrossFrontB", modelRoot.transform,
                new Vector3(0f, 0f, 0.235f), Vector3.zero, new Vector3(0.08f, 0.26f, 0.02f), redMat);
            MakePart(PrimitiveType.Cube, "CrossBackA", modelRoot.transform,
                new Vector3(0f, 0f, -0.235f), Vector3.zero, new Vector3(0.26f, 0.08f, 0.02f), redMat);
            MakePart(PrimitiveType.Cube, "CrossBackB", modelRoot.transform,
                new Vector3(0f, 0f, -0.235f), Vector3.zero, new Vector3(0.08f, 0.26f, 0.02f), redMat);

            // Carry Handle
            MakePart(PrimitiveType.Cube, "Handle", modelRoot.transform,
                new Vector3(0f, 0.25f, 0f), Vector3.zero, new Vector3(0.22f, 0.05f, 0.05f), handleMat);

            // Subtle Base Ring so it's easy to spot in grass
            MakePart(PrimitiveType.Cylinder, "BaseRing", modelRoot.transform,
                new Vector3(0f, -0.24f, 0f), Vector3.zero, new Vector3(0.75f, 0.015f, 0.75f), glowMat);
        }
        else if (type == ItemPickup.ItemType.Bandage)
        {
            // BANDAGE: Rolled white gauze cylinder + red medical tape band
            Material gauzeMat = CreateSafeMaterial(new Color(0.94f, 0.92f, 0.86f));
            Material stripeMat = CreateSafeMaterial(new Color(0.85f, 0.18f, 0.18f));
            Material innerMat = CreateSafeMaterial(new Color(0.35f, 0.32f, 0.28f));

            // Main Gauze Roll
            MakePart(PrimitiveType.Cylinder, "GauzeRoll", modelRoot.transform,
                Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(0.36f, 0.22f, 0.36f), gauzeMat);

            // Red Medical Band around the middle
            MakePart(PrimitiveType.Cylinder, "RedBand", modelRoot.transform,
                Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(0.375f, 0.06f, 0.375f), stripeMat);

            // Hollow core ends
            MakePart(PrimitiveType.Cylinder, "InnerCore", modelRoot.transform,
                Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(0.14f, 0.23f, 0.14f), innerMat);
        }
        else // Ammo
        {
            // AMMO BOX: Military olive drab crate + lid + yellow brass ammo stripe
            Material oliveMat = CreateSafeMaterial(new Color(0.24f, 0.33f, 0.18f));
            Material lidMat = CreateSafeMaterial(new Color(0.18f, 0.25f, 0.13f));
            Material brassMat = CreateSafeMaterial(new Color(0.95f, 0.76f, 0.15f));

            // Main Crate
            MakePart(PrimitiveType.Cube, "AmmoCrate", modelRoot.transform,
                Vector3.zero, Vector3.zero, new Vector3(0.65f, 0.38f, 0.36f), oliveMat);

            // Top Lid Overhang
            MakePart(PrimitiveType.Cube, "CrateLid", modelRoot.transform,
                new Vector3(0f, 0.19f, 0f), Vector3.zero, new Vector3(0.68f, 0.07f, 0.39f), lidMat);

            // Yellow Stencil Stripe
            MakePart(PrimitiveType.Cube, "AmmoStripe", modelRoot.transform,
                new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(0.66f, 0.10f, 0.37f), brassMat);
        }
    }

    void BuildWeaponVisuals(Transform parent, int weaponIdx)
    {
        GameObject modelRoot = new GameObject("VisualWeaponModel");
        modelRoot.transform.SetParent(parent, false);

        Material gunMetal = CreateSafeMaterial(new Color(0.16f, 0.18f, 0.20f));
        Material woodOrStock = CreateSafeMaterial(new Color(0.48f, 0.28f, 0.14f));
        Material tacPolymer = CreateSafeMaterial(new Color(0.26f, 0.28f, 0.32f));
        Material accentColor;

        if (weaponIdx == rifleIndex)
        {
            // RIFLE (AKM style): Receiver + Barrel + Curved Mag + Wooden Stock + Orange Beacon
            accentColor = CreateSafeMaterial(new Color(1f, 0.55f, 0.1f));

            MakePart(PrimitiveType.Cube, "Receiver", modelRoot.transform,
                new Vector3(0f, 0.08f, 0f), Vector3.zero, new Vector3(0.12f, 0.16f, 0.52f), gunMetal);
            MakePart(PrimitiveType.Cylinder, "Barrel", modelRoot.transform,
                new Vector3(0f, 0.11f, 0.45f), new Vector3(90f, 0f, 0f), new Vector3(0.05f, 0.26f, 0.05f), gunMetal);
            MakePart(PrimitiveType.Cube, "Stock", modelRoot.transform,
                new Vector3(0f, 0.04f, -0.38f), new Vector3(8f, 0f, 0f), new Vector3(0.10f, 0.14f, 0.30f), woodOrStock);
            MakePart(PrimitiveType.Cube, "Magazine", modelRoot.transform,
                new Vector3(0f, -0.08f, 0.10f), new Vector3(-20f, 0f, 0f), new Vector3(0.08f, 0.24f, 0.11f), accentColor);
        }
        else if (weaponIdx == smgIndex)
        {
            // SMG (UMP/Uzi style): Compact body + Vertical Mag + Suppressor barrel + Cyan Beacon
            accentColor = CreateSafeMaterial(new Color(0.15f, 0.85f, 1f));

            MakePart(PrimitiveType.Cube, "SMGBody", modelRoot.transform,
                new Vector3(0f, 0.08f, 0f), Vector3.zero, new Vector3(0.12f, 0.18f, 0.38f), tacPolymer);
            MakePart(PrimitiveType.Cylinder, "Barrel", modelRoot.transform,
                new Vector3(0f, 0.09f, 0.30f), new Vector3(90f, 0f, 0f), new Vector3(0.06f, 0.15f, 0.06f), gunMetal);
            MakePart(PrimitiveType.Cube, "StraightMag", modelRoot.transform,
                new Vector3(0f, -0.10f, 0.05f), Vector3.zero, new Vector3(0.07f, 0.24f, 0.08f), accentColor);
        }
        else
        {
            // SNIPER (Kar98k/AWM style): Long barrel + Rifle body + Top Scope + Gold Beacon
            accentColor = CreateSafeMaterial(new Color(0.95f, 0.22f, 0.85f));

            MakePart(PrimitiveType.Cube, "SniperChassis", modelRoot.transform,
                new Vector3(0f, 0.06f, -0.05f), Vector3.zero, new Vector3(0.11f, 0.14f, 0.68f), woodOrStock);
            MakePart(PrimitiveType.Cylinder, "LongBarrel", modelRoot.transform,
                new Vector3(0f, 0.10f, 0.52f), new Vector3(90f, 0f, 0f), new Vector3(0.045f, 0.36f, 0.045f), gunMetal);
            MakePart(PrimitiveType.Cylinder, "SniperScope", modelRoot.transform,
                new Vector3(0f, 0.20f, 0.02f), new Vector3(90f, 0f, 0f), new Vector3(0.07f, 0.18f, 0.07f), accentColor);
        }

        // Glowing loot pad underneath the weapon so you can spot guns easily from a distance
        MakePart(PrimitiveType.Cylinder, "LootPad", modelRoot.transform,
            new Vector3(0f, -0.24f, 0f), Vector3.zero, new Vector3(0.95f, 0.015f, 0.95f), accentColor);
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

        Collider c = go.GetComponent<Collider>();
        if (c != null) Destroy(c);

        Renderer r = go.GetComponent<Renderer>();
        if (r != null && mat != null) r.sharedMaterial = mat;

        return go;
    }

    Material CreateSafeMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        Material mat = new Material(shader);
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
