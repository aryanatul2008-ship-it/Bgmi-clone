using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Standalone Battle Royale Map Designer & Enterable Shack Generator.
/// - Builds enterable walk-in tactical shacks (hollow walls, doorway, window, roof) with guaranteed loot inside.
/// - Scatters multi-part pine/oak trees, boulder clusters, and wooden tactical barricades across the 300x300 map.
/// - Every tree trunk, rock, barricade, and shack wall has solid non-trigger Colliders (blocks players AND bullets)
///   plus NavMeshObstacle (carving = true) so AI bots navigate around them without needing to re-bake NavMesh.
/// - Build-safe material cloning from Unity primitives (never turns pink or invisible in itch.io builds).
/// </summary>
public class MapPropSpawner : MonoBehaviour
{
    [Header("Master Toggle")]
    [Tooltip("Ticked = generates enterable shacks, cover rocks, trees, and barricades at match start.")]
    public bool generateMapDesign = true;

    [Header("1. Enterable Shacks (With Interior Loot)")]
    [Tooltip("Number of walk-in shacks to place around the map. Each shack spawns 2-3 loot items inside!")]
    public int enterableShackCount = 10;
    public bool spawnLootInsideShacks = true;

    [Header("2. Natural Cover (Solid Bullet-Blocking)")]
    public int treeCount = 55;
    public int rockClusterCount = 40;
    public int tacticalBarricadeCount = 18;

    [Header("3. Map Layout & Safe Spacing (300x300 Map)")]
    [Tooltip("Keeps spawned structures inside [-132, +132] on your 300x300 terrain.")]
    public float mapHalfExtent = 132f;
    [Tooltip("Minimum clear distance around existing Kenney buildings.")]
    public float buildingClearanceRadius = 11f;
    [Tooltip("Minimum distance between enterable shacks.")]
    public float minShackSpacing = 28f;
    [Tooltip("Minimum distance between trees and rocks so open grounds remain playable.")]
    public float minCoverSpacing = 7.5f;
    [Tooltip("Max terrain slope angle so shacks and rocks don't spawn on steep cliffs.")]
    public float maxSlopeAngle = 22f;

    private LayerMask groundMask;
    private Transform mapDesignContainer;

    private struct OccupiedCircle
    {
        public Vector3 position;
        public float radius;
        public OccupiedCircle(Vector3 pos, float r) { position = pos; radius = r; }
    }

    private List<OccupiedCircle> occupiedAreas = new List<OccupiedCircle>();
    private Material cachedTemplateMaterial;
    private List<Material> createdMaterials = new List<Material>();

    private int rifleIndex = 0;
    private int smgIndex = 1;
    private int sniperIndex = 2;

    // Shared Materials
    private Material matWoodWall;
    private Material matWoodDark;
    private Material matRoofTin;
    private Material matFloorConcrete;
    private Material matTrunkBark;
    private Material matPineLeaves;
    private Material matOakLeaves;
    private Material matRockGrey;
    private Material matRockDark;
    private Material matSandbag;
    private Material matCrateWood;

    private int framesSinceStart = 0;
    private bool lootRescueCompleted = false;

    void Awake()
    {
        groundMask = ~(1 << 2);
    }

    void Start()
    {
        if (!generateMapDesign)
        {
            enabled = false;
            return;
        }

        DetectWeaponIndices();
        InitializePaletteMaterials();
        RegisterExistingSceneObstacles();
        GenerateBalancedMap();
    }

    void LateUpdate()
    {
        if (!generateMapDesign || lootRescueCompleted) return;

        framesSinceStart++;
        if (framesSinceStart >= 2)
        {
            lootRescueCompleted = true;
            RescueAnyBuriedLootPickups();
        }
    }

    // =========================================================================
    // INITIALIZATION & EXISTING SCENE SCAN
    // =========================================================================

    void DetectWeaponIndices()
    {
        WeaponSwitcher switcher = FindFirstObjectByType<WeaponSwitcher>(FindObjectsInactive.Include);
        if (switcher == null || switcher.guns == null) return;

        for (int i = 0; i < switcher.guns.Length; i++)
        {
            Gun g = switcher.guns[i];
            if (g == null || g.melee) continue;

            string gName = g.name.ToLowerInvariant();
            if (gName.Contains("rifle") || gName.Contains("ak")) rifleIndex = i;
            else if (gName.Contains("smg") || gName.Contains("ump") || gName.Contains("uzi")) smgIndex = i;
            else if (gName.Contains("sniper") || gName.Contains("kar")) sniperIndex = i;
        }
    }

    void InitializePaletteMaterials()
    {
        matWoodWall = CreateSafeMaterial(new Color(0.56f, 0.43f, 0.30f)); // Warm cabin timber
        matWoodDark = CreateSafeMaterial(new Color(0.36f, 0.25f, 0.16f)); // Dark support beams
        matRoofTin = CreateSafeMaterial(new Color(0.28f, 0.32f, 0.36f)); // Corrugated metal roof
        matFloorConcrete = CreateSafeMaterial(new Color(0.42f, 0.43f, 0.45f)); // Stone/concrete floor
        matTrunkBark = CreateSafeMaterial(new Color(0.38f, 0.24f, 0.14f)); // Tree trunk bark
        matPineLeaves = CreateSafeMaterial(new Color(0.14f, 0.42f, 0.20f)); // Deep forest pine green
        matOakLeaves = CreateSafeMaterial(new Color(0.24f, 0.52f, 0.22f)); // Vibrant canopy green
        matRockGrey = CreateSafeMaterial(new Color(0.46f, 0.48f, 0.50f)); // Granite boulder grey
        matRockDark = CreateSafeMaterial(new Color(0.32f, 0.34f, 0.37f)); // Slate rock
        matSandbag = CreateSafeMaterial(new Color(0.68f, 0.60f, 0.42f)); // Khaki sandbag cover
        matCrateWood = CreateSafeMaterial(new Color(0.62f, 0.46f, 0.26f)); // Military supply crate
    }

    void RegisterExistingSceneObstacles()
    {
        occupiedAreas.Clear();

        occupiedAreas.Add(new OccupiedCircle(new Vector3(-70f, 0f, -70f), 8f));

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            occupiedAreas.Add(new OccupiedCircle(playerObj.transform.position, 7f));
        }

        MeshRenderer[] allRenderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        foreach (MeshRenderer mr in allRenderers)
        {
            if (mr == null || !mr.enabled) continue;
            string n = mr.gameObject.name.ToLowerInvariant();
            string parentName = mr.transform.parent != null ? mr.transform.parent.name.ToLowerInvariant() : "";

            if (n.Contains("building") || parentName.Contains("building"))
            {
                occupiedAreas.Add(new OccupiedCircle(mr.bounds.center, buildingClearanceRadius));
            }
            else if (n.Contains("birch") || parentName.Contains("birch") || n.Contains("rock"))
            {
                occupiedAreas.Add(new OccupiedCircle(mr.bounds.center, 4.0f));
            }
        }
    }

    // =========================================================================
    // MAIN MAP GENERATION PIPELINE
    // =========================================================================

    void GenerateBalancedMap()
    {
        mapDesignContainer = new GameObject("RedesignedMap_Environment").transform;

        for (int i = 0; i < enterableShackCount; i++)
        {
            if (TryFindGroundSpot(minShackSpacing, 12f, out Vector3 pos, out Vector3 normal))
            {
                BuildEnterableShack(pos, i);
                occupiedAreas.Add(new OccupiedCircle(pos, minShackSpacing * 0.55f));
            }
        }

        for (int i = 0; i < tacticalBarricadeCount; i++)
        {
            if (TryFindGroundSpot(minCoverSpacing, 16f, out Vector3 pos, out Vector3 normal))
            {
                BuildTacticalBarricade(pos, i);
                occupiedAreas.Add(new OccupiedCircle(pos, 5.0f));
            }
        }

        for (int i = 0; i < rockClusterCount; i++)
        {
            if (TryFindGroundSpot(minCoverSpacing, maxSlopeAngle, out Vector3 pos, out Vector3 normal))
            {
                BuildRockCluster(pos, i);
                occupiedAreas.Add(new OccupiedCircle(pos, 4.2f));
            }
        }

        for (int i = 0; i < treeCount; i++)
        {
            if (TryFindGroundSpot(minCoverSpacing * 0.85f, maxSlopeAngle, out Vector3 pos, out Vector3 normal))
            {
                BuildSolidTree(pos, i);
                occupiedAreas.Add(new OccupiedCircle(pos, 3.5f));
            }
        }
    }

    bool TryFindGroundSpot(float requiredRadius, float allowedSlope, out Vector3 groundPos, out Vector3 groundNormal)
    {
        for (int attempt = 0; attempt < 45; attempt++)
        {
            float rx = Random.Range(-mapHalfExtent, mapHalfExtent);
            float rz = Random.Range(-mapHalfExtent, mapHalfExtent);
            Vector3 rayOrigin = new Vector3(rx, 240f, rz);

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 280f, groundMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.point.y > 3.8f) continue;

                float slope = Vector3.Angle(hit.normal, Vector3.up);
                if (slope > allowedSlope) continue;

                Vector3 candidate = hit.point;

                bool overlaps = false;
                for (int i = 0; i < occupiedAreas.Count; i++)
                {
                    float minDist = Mathf.Max(requiredRadius, occupiedAreas[i].radius);
                    Vector2 a = new Vector2(candidate.x, candidate.z);
                    Vector2 b = new Vector2(occupiedAreas[i].position.x, occupiedAreas[i].position.z);
                    if ((a - b).sqrMagnitude < minDist * minDist)
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                {
                    groundPos = candidate;
                    groundNormal = hit.normal;
                    return true;
                }
            }
        }

        groundPos = Vector3.zero;
        groundNormal = Vector3.up;
        return false;
    }

    // =========================================================================
    // 1. ENTERABLE TACTICAL SHACK (WALK-IN + BULLETPROOF WALLS + INTERIOR LOOT)
    // =========================================================================

    void BuildEnterableShack(Vector3 pos, int index)
    {
        GameObject shackRoot = new GameObject("EnterableShack_" + index);
        shackRoot.transform.SetParent(mapDesignContainer, false);
        shackRoot.transform.position = pos;
        shackRoot.transform.rotation = Quaternion.Euler(0f, Random.Range(0, 4) * 90f + Random.Range(-15f, 15f), 0f);

        float width = 5.6f;
        float depth = 6.4f;
        float wallH = 3.2f;
        float wallThick = 0.24f;
        float doorWidth = 2.1f;
        float doorHeight = 2.45f;

        CreateSolidPart(PrimitiveType.Cube, "FloorSlab", shackRoot.transform,
            new Vector3(0f, 0.04f, 0f), Vector3.zero, new Vector3(width, 0.12f, depth),
            matFloorConcrete, addNavObstacle: false);

        CreateSolidPart(PrimitiveType.Cube, "BackWall_Bottom", shackRoot.transform,
            new Vector3(0f, 0.65f, -depth * 0.5f), Vector3.zero, new Vector3(width, 1.3f, wallThick),
            matWoodWall, addNavObstacle: true);
        CreateSolidPart(PrimitiveType.Cube, "BackWall_Top", shackRoot.transform,
            new Vector3(0f, 2.75f, -depth * 0.5f), Vector3.zero, new Vector3(width, 0.9f, wallThick),
            matWoodWall, addNavObstacle: false);
        CreateSolidPart(PrimitiveType.Cube, "BackWall_Left", shackRoot.transform,
            new Vector3(-1.9f, 1.8f, -depth * 0.5f), Vector3.zero, new Vector3(1.8f, 1.0f, wallThick),
            matWoodWall, addNavObstacle: true);
        CreateSolidPart(PrimitiveType.Cube, "BackWall_Right", shackRoot.transform,
            new Vector3(1.9f, 1.8f, -depth * 0.5f), Vector3.zero, new Vector3(1.8f, 1.0f, wallThick),
            matWoodWall, addNavObstacle: true);

        CreateSolidPart(PrimitiveType.Cube, "LeftWall", shackRoot.transform,
            new Vector3(-width * 0.5f, wallH * 0.5f, 0f), Vector3.zero, new Vector3(wallThick, wallH, depth),
            matWoodWall, addNavObstacle: true);

        CreateSolidPart(PrimitiveType.Cube, "RightWall", shackRoot.transform,
            new Vector3(width * 0.5f, wallH * 0.5f, 0f), Vector3.zero, new Vector3(wallThick, wallH, depth),
            matWoodWall, addNavObstacle: true);

        float sidePanelWidth = (width - doorWidth) * 0.5f;
        float sidePanelCenterX = (doorWidth * 0.5f) + (sidePanelWidth * 0.5f);

        CreateSolidPart(PrimitiveType.Cube, "FrontWall_Left", shackRoot.transform,
            new Vector3(-sidePanelCenterX, wallH * 0.5f, depth * 0.5f), Vector3.zero,
            new Vector3(sidePanelWidth, wallH, wallThick), matWoodWall, addNavObstacle: true);

        CreateSolidPart(PrimitiveType.Cube, "FrontWall_Right", shackRoot.transform,
            new Vector3(sidePanelCenterX, wallH * 0.5f, depth * 0.5f), Vector3.zero,
            new Vector3(sidePanelWidth, wallH, wallThick), matWoodWall, addNavObstacle: true);

        float lintelH = wallH - doorHeight;
        CreateSolidPart(PrimitiveType.Cube, "FrontDoor_Lintel", shackRoot.transform,
            new Vector3(0f, doorHeight + lintelH * 0.5f, depth * 0.5f), Vector3.zero,
            new Vector3(doorWidth, lintelH, wallThick), matWoodDark, addNavObstacle: false);

        float[] cx = { -width * 0.5f, width * 0.5f };
        float[] cz = { -depth * 0.5f, depth * 0.5f };
        foreach (float x in cx)
        {
            foreach (float z in cz)
            {
                CreateSolidPart(PrimitiveType.Cube, "CornerPost", shackRoot.transform,
                    new Vector3(x, wallH * 0.5f, z), Vector3.zero, new Vector3(0.34f, wallH + 0.1f, 0.34f),
                    matWoodDark, addNavObstacle: false);
            }
        }

        CreateSolidPart(PrimitiveType.Cube, "RoofSlope_Left", shackRoot.transform,
            new Vector3(-width * 0.27f, wallH + 0.45f, 0f), new Vector3(0f, 0f, 16f),
            new Vector3(width * 0.62f, 0.16f, depth + 0.7f), matRoofTin, addNavObstacle: false);

        CreateSolidPart(PrimitiveType.Cube, "RoofSlope_Right", shackRoot.transform,
            new Vector3(width * 0.27f, wallH + 0.45f, 0f), new Vector3(0f, 0f, -16f),
            new Vector3(width * 0.62f, 0.16f, depth + 0.7f), matRoofTin, addNavObstacle: false);

        if (spawnLootInsideShacks)
        {
            SpawnInteriorShackLoot(shackRoot.transform, index);
        }
    }

    void SpawnInteriorShackLoot(Transform shackTransform, int shackIndex)
    {
        Vector3 weaponWorldPos = shackTransform.TransformPoint(new Vector3(-1.4f, 0.52f, -1.6f));
        int chosenWeapon = (shackIndex % 3 == 0) ? rifleIndex : ((shackIndex % 3 == 1) ? smgIndex : sniperIndex);
        SpawnWeaponPickupAt("ShackWeapon_" + shackIndex, weaponWorldPos, chosenWeapon, 30);

        Vector3 healWorldPos = shackTransform.TransformPoint(new Vector3(1.4f, 0.52f, -1.6f));
        ItemPickup.ItemType healType = (shackIndex % 2 == 0) ? ItemPickup.ItemType.Heal : ItemPickup.ItemType.Bandage;
        SpawnItemPickupAt("ShackHeal_" + shackIndex, healWorldPos, healType, 0f);

        Vector3 ammoWorldPos = shackTransform.TransformPoint(new Vector3(1.3f, 0.52f, 0.4f));
        SpawnItemPickupAt("ShackAmmo_" + shackIndex, ammoWorldPos, ItemPickup.ItemType.Ammo, 30f);
    }

    // =========================================================================
    // 2. SOLID TREES, ROCK CLUSTERS & TACTICAL BARRICADES (BLOCKS BULLETS & PLAYERS)
    // =========================================================================

    void BuildSolidTree(Vector3 pos, int index)
    {
        GameObject treeRoot = new GameObject("SolidTree_" + index);
        treeRoot.transform.SetParent(mapDesignContainer, false);
        treeRoot.transform.position = pos;
        treeRoot.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        float scale = Random.Range(0.95f, 1.45f);
        treeRoot.transform.localScale = new Vector3(scale, scale, scale);

        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "TreeTrunk_SolidCover";
        trunk.transform.SetParent(treeRoot.transform, false);
        trunk.transform.localPosition = new Vector3(0f, 1.8f, 0f);
        trunk.transform.localScale = new Vector3(0.58f, 1.8f, 0.58f);
        trunk.GetComponent<Renderer>().sharedMaterial = matTrunkBark;

        Collider oldCol = trunk.GetComponent<Collider>();
        if (oldCol != null) Destroy(oldCol);
        CapsuleCollider trunkCol = trunk.AddComponent<CapsuleCollider>();
        trunkCol.isTrigger = false;
        trunkCol.radius = 0.52f;
        trunkCol.height = 3.6f;

        AddCarvingNavObstacle(trunk, new Vector3(1.1f, 3.6f, 1.1f));

        bool isPine = (index % 2 == 0);
        if (isPine)
        {
            CreateVisualOnlyPart(PrimitiveType.Sphere, "PineTier1", treeRoot.transform,
                new Vector3(0f, 3.2f, 0f), Vector3.zero, new Vector3(2.8f, 1.8f, 2.8f), matPineLeaves);
            CreateVisualOnlyPart(PrimitiveType.Sphere, "PineTier2", treeRoot.transform,
                new Vector3(0f, 4.5f, 0f), Vector3.zero, new Vector3(2.1f, 1.6f, 2.1f), matPineLeaves);
            CreateVisualOnlyPart(PrimitiveType.Sphere, "PineTop", treeRoot.transform,
                new Vector3(0f, 5.5f, 0f), Vector3.zero, new Vector3(1.2f, 1.3f, 1.2f), matPineLeaves);
        }
        else
        {
            CreateVisualOnlyPart(PrimitiveType.Sphere, "OakCrownMain", treeRoot.transform,
                new Vector3(0f, 3.8f, 0f), Vector3.zero, new Vector3(3.4f, 2.4f, 3.4f), matOakLeaves);
            CreateVisualOnlyPart(PrimitiveType.Sphere, "OakCrownSideA", treeRoot.transform,
                new Vector3(0.9f, 3.4f, 0.6f), Vector3.zero, new Vector3(2.2f, 1.8f, 2.2f), matOakLeaves);
            CreateVisualOnlyPart(PrimitiveType.Sphere, "OakCrownSideB", treeRoot.transform,
                new Vector3(-0.8f, 3.5f, -0.7f), Vector3.zero, new Vector3(2.3f, 1.9f, 2.3f), matOakLeaves);
        }
    }

    void BuildRockCluster(Vector3 pos, int index)
    {
        GameObject clusterRoot = new GameObject("RockCoverCluster_" + index);
        clusterRoot.transform.SetParent(mapDesignContainer, false);
        clusterRoot.transform.position = pos;
        clusterRoot.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        Vector3 mainScale = new Vector3(Random.Range(1.8f, 2.7f), Random.Range(1.3f, 1.9f), Random.Range(1.6f, 2.4f));
        CreateSolidPart(PrimitiveType.Cube, "MainBoulder", clusterRoot.transform,
            new Vector3(0f, mainScale.y * 0.42f, 0f),
            new Vector3(Random.Range(-8f, 8f), Random.Range(0f, 45f), Random.Range(-8f, 8f)),
            mainScale, matRockGrey, addNavObstacle: true);

        Vector3 sideScale = new Vector3(Random.Range(1.2f, 1.8f), Random.Range(0.9f, 1.3f), Random.Range(1.2f, 1.7f));
        CreateSolidPart(PrimitiveType.Cube, "SideBoulder", clusterRoot.transform,
            new Vector3(mainScale.x * 0.55f, sideScale.y * 0.40f, 0.3f),
            new Vector3(Random.Range(-10f, 10f), Random.Range(15f, 75f), Random.Range(-10f, 10f)),
            sideScale, matRockDark, addNavObstacle: true);
    }

    void BuildTacticalBarricade(Vector3 pos, int index)
    {
        GameObject barricadeRoot = new GameObject("TacticalCover_" + index);
        barricadeRoot.transform.SetParent(mapDesignContainer, false);
        barricadeRoot.transform.position = pos;
        barricadeRoot.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        CreateSolidPart(PrimitiveType.Cube, "SandbagWall", barricadeRoot.transform,
            new Vector3(0f, 0.65f, 0f), Vector3.zero, new Vector3(2.8f, 1.3f, 0.65f),
            matSandbag, addNavObstacle: true);

        CreateSolidPart(PrimitiveType.Cube, "MilitaryCrateA", barricadeRoot.transform,
            new Vector3(-1.85f, 0.55f, 0.35f), new Vector3(0f, 18f, 0f), new Vector3(1.1f, 1.1f, 1.1f),
            matCrateWood, addNavObstacle: true);

        CreateSolidPart(PrimitiveType.Cube, "MilitaryCrateB", barricadeRoot.transform,
            new Vector3(-1.85f, 1.45f, 0.35f), new Vector3(0f, -12f, 0f), new Vector3(0.85f, 0.75f, 0.85f),
            matWoodDark, addNavObstacle: false);
    }

    // =========================================================================
    // 3. AUTOMATIC LOOT RESCUE (ENSURES ZERO BURIED ITEMS)
    // =========================================================================

    void RescueAnyBuriedLootPickups()
    {
        Collider[] allColliders = mapDesignContainer != null
            ? mapDesignContainer.GetComponentsInChildren<Collider>()
            : new Collider[0];

        List<Collider> solidWorldColliders = new List<Collider>(allColliders);
        GameObject[] allObjs = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject go in allObjs)
        {
            if (go == null) continue;
            string n = go.name.ToLowerInvariant();
            if (n.Contains("building") || n.Contains("rock"))
            {
                Collider c = go.GetComponent<Collider>();
                if (c != null && !c.isTrigger && !solidWorldColliders.Contains(c))
                {
                    solidWorldColliders.Add(c);
                }
            }
        }

        ItemPickup[] items = FindObjectsByType<ItemPickup>(FindObjectsSortMode.None);
        foreach (ItemPickup item in items)
        {
            if (item != null) EnsurePickupIsNotBuried(item.transform, solidWorldColliders);
        }

        WeaponPickup[] weapons = FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None);
        foreach (WeaponPickup wp in weapons)
        {
            if (wp != null) EnsurePickupIsNotBuried(wp.transform, solidWorldColliders);
        }
    }

    void EnsurePickupIsNotBuried(Transform pickupTransform, List<Collider> solidObstacles)
    {
        if (pickupTransform.name.StartsWith("Shack")) return;

        Vector3 pos = pickupTransform.position;

        foreach (Collider col in solidObstacles)
        {
            if (col == null || col.isTrigger) continue;
            if (col.name == "FloorSlab") continue;

            Bounds b = col.bounds;
            b.Expand(0.35f);
            if (b.Contains(pos))
            {
                Vector3 dir = (pos - b.center);
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
                dir.Normalize();

                float pushDist = Mathf.Max(b.extents.x, b.extents.z) + 1.4f;
                Vector3 safeXZ = new Vector3(b.center.x, 0f, b.center.z) + dir * pushDist;

                if (Physics.Raycast(safeXZ + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 100f, groundMask, QueryTriggerInteraction.Ignore))
                {
                    pickupTransform.position = hit.point + Vector3.up * 0.45f;
                }
                break;
            }
        }
    }

    // =========================================================================
    // HELPERS FOR SOLID PARTS, NAVMESH CARVING & BUILD-SAFE MATERIALS
    // =========================================================================

    GameObject CreateSolidPart(PrimitiveType type, string partName, Transform parent,
        Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat, bool addNavObstacle)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = partName;
        go.layer = 0; // Default layer -> blocks bullets, raycasts, and CharacterController
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(localEuler);
        go.transform.localScale = localScale;

        Collider c = go.GetComponent<Collider>();
        if (c != null) c.isTrigger = false;

        Renderer r = go.GetComponent<Renderer>();
        if (r != null && mat != null) r.sharedMaterial = mat;

        if (addNavObstacle)
        {
            AddCarvingNavObstacle(go, Vector3.one);
        }

        return go;
    }

    GameObject CreateVisualOnlyPart(PrimitiveType type, string partName, Transform parent,
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

    void AddCarvingNavObstacle(GameObject target, Vector3 size)
    {
        NavMeshObstacle navObs = target.AddComponent<NavMeshObstacle>();
        navObs.shape = NavMeshObstacleShape.Box;
        navObs.size = size;
        navObs.carving = true;
        navObs.carveOnlyStationary = true;
    }

    void SpawnItemPickupAt(string objName, Vector3 pos, ItemPickup.ItemType type, float ammoAmount)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = objName;
        go.transform.SetParent(mapDesignContainer, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(0.48f, 0.38f, 0.48f);

        Collider col = go.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        Color c = type == ItemPickup.ItemType.Heal ? new Color(0.90f, 0.18f, 0.18f)
                : (type == ItemPickup.ItemType.Bandage ? new Color(0.95f, 0.95f, 0.92f)
                : new Color(0.28f, 0.42f, 0.18f));
        go.GetComponent<Renderer>().sharedMaterial = CreateSafeMaterial(c);

        ItemPickup ip = go.AddComponent<ItemPickup>();
        ip.type = type;
        ip.amount = ammoAmount;
    }

    void SpawnWeaponPickupAt(string objName, Vector3 pos, int weaponIdx, int bonusAmmo)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = objName;
        go.transform.SetParent(mapDesignContainer, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(0.32f, 0.25f, 0.95f);

        Collider col = go.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        go.GetComponent<Renderer>().sharedMaterial = CreateSafeMaterial(new Color(1f, 0.58f, 0.12f));

        WeaponPickup wp = go.AddComponent<WeaponPickup>();
        wp.weaponIndex = weaponIdx;
        wp.bonusAmmo = bonusAmmo;
        wp.magAmmo = -1;
        wp.reserveAmmo = -1;
    }

    Material CreateSafeMaterial(Color color)
    {
        // Unity ka default built-in URP material use karo jo build mein kabhi pink nahi hota
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat.shader == null || !mat.shader.isSupported)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
        }
        if (mat.shader == null || !mat.shader.isSupported)
        {
            mat = new Material(Shader.Find("Standard"));
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