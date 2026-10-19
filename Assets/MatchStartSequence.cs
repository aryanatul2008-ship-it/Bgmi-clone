using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Self-contained Battle Royale match start sequence:
/// Matchmaking overlay -> Cargo Plane flight -> Freefall -> Parachute -> Ground Landing.
/// Builds its own Canvas (Sorting Order 13), plane primitives, and parachute canopy in code.
/// </summary>
public class MatchStartSequence : MonoBehaviour
{
    [Header("Master Toggle")]
    [Tooltip("Ticked = sequence runs. Unticked = game starts normally on the ground.")]
    public bool enableDropSequence = true;

    [Header("1. Matchmaking & Countdown")]
    public bool showMatchmakingOverlay = true;
    public float matchmakingDuration = 5f;
    public int startPlayerCount = 12;
    public int maxPlayerCount = 20;
    public float countdownDuration = 5f;

    [Header("2. Plane Flight (300x300 Map)")]
    public float planeAltitude = 250f;
    public float flightDuration = 25f;

    [Header("3. Freefall Settings")]
    public float freefallTerminalSpeed = 50f;
    public float freefallAcceleration = 35f;
    public float freefallSteerSpeed = 24f;
    public float autoParachuteHeight = 60f;

    [Header("4. Parachute Settings")]
    public float parachuteDescentSpeed = 6f;
    public float parachuteSteerSpeed = 14f;

    [Header("Scene References (Auto-found if left empty)")]
    public Transform playerTransform;
    public GameObject enemySpawnerObject;

    private enum DropState
    {
        Inactive,
        Matchmaking,
        Countdown,
        InPlane,
        Freefall,
        Parachute,
        Landed
    }

    private DropState state = DropState.Inactive;
    private float stateTimer = 0f;

    // Flight path
    private Vector3 flightStartPos;
    private Vector3 flightEndPos;
    private float flightProgress = 0f;

    // Physics & Movement
    private float verticalSpeed = 0f;
    private float currentAltitudeAGL = 0f;
    private Camera mainCam;
    private LayerMask groundMask;

    // Cached Player Components
    private CharacterController charController;
    private PlayerMovement playerMovement;
    private Health playerHealth;
    private WeaponSwitcher weaponSwitcher;
    private PlayerInventory playerInventory;
    private ScopeController scopeController;
    private Gun[] playerGuns;
    private List<Renderer> playerRenderers = new List<Renderer>();
    private List<bool> initialRendererStates = new List<bool>();

    // Generated 3D Objects
    private GameObject planeRoot;
    private Transform propellerTransform;
    private GameObject canopyRoot;

    // Generated UI (Canvas Sorting Order 13)
    private GameObject uiCanvasObj;
    private GameObject overlayPanel;
    private Text overlayMainText;
    private Text overlaySubText;
    private GameObject hudContainer;
    private Text promptText;
    private Text altitudeText;
    private Image altBarFill;

    // Materials cache for cleanup
    private List<Material> createdMaterials = new List<Material>();

    void Awake()
    {
        // Exclude Layer 2 ("Ignore Raycast", which the Player uses) from ground detection
        groundMask = ~(1 << 2);
    }

    void Start()
    {
        FindSceneReferences();

        if (!enableDropSequence)
        {
            // Normal ground start: ensure EnemySpawner is active so bots spawn as usual
            if (enemySpawnerObject != null && !enemySpawnerObject.activeSelf)
            {
                enemySpawnerObject.SetActive(true);
            }
            state = DropState.Inactive;
            enabled = false;
            return;
        }

        if (playerTransform == null)
        {
            Debug.LogError("MatchStartSequence: Player object could not be found! Disabling drop sequence.");
            enabled = false;
            return;
        }

        // Keep EnemySpawner disabled until the player jumps out of the plane
        if (enemySpawnerObject != null)
        {
            enemySpawnerObject.SetActive(false);
        }

        BeginDropSequence();
    }

    void FindSceneReferences()
    {
        mainCam = Camera.main;

        if (playerTransform == null)
        {
            GameObject pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj == null) pObj = GameObject.Find("Player");
            if (pObj != null) playerTransform = pObj.transform;
        }

        if (playerTransform != null)
        {
            charController = playerTransform.GetComponent<CharacterController>();
            playerMovement = playerTransform.GetComponent<PlayerMovement>();
            playerHealth = playerTransform.GetComponent<Health>();
            weaponSwitcher = playerTransform.GetComponent<WeaponSwitcher>();
            playerInventory = playerTransform.GetComponent<PlayerInventory>();
            scopeController = playerTransform.GetComponent<ScopeController>();
            playerGuns = playerTransform.GetComponentsInChildren<Gun>(true);
        }

        if (enemySpawnerObject == null)
        {
            EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include);
            if (spawner != null)
            {
                enemySpawnerObject = spawner.gameObject;
            }
            else
            {
                GameObject found = GameObject.Find("EnemySpawner");
                if (found != null) enemySpawnerObject = found;
            }
        }
    }

    void BeginDropSequence()
    {
        // 1. Switch OFF player movement, character controller, inventory, scope, and all guns
        SetPlayerComponentsEnabled(false);

        // 2. Pick a straight flight line across the 300x300 map (x: -140..140, z: -140..140)
        PickRandomFlightPath();

        // 3. Build the plane from primitives and hide the player inside it
        BuildPlanePrimitive();
        planeRoot.transform.position = flightStartPos;
        planeRoot.transform.rotation = Quaternion.LookRotation((flightEndPos - flightStartPos).normalized, Vector3.up);

        // Move player while CharacterController is disabled
        playerTransform.position = flightStartPos;
        Vector3 flatForward = (flightEndPos - flightStartPos);
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude > 0.01f)
        {
            playerTransform.rotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
        }

        // Hide player model while seated inside the plane
        CacheAndHidePlayerModel();

        // 4. Build the Sorting Order 13 UI Canvas
        BuildSequenceUI();

        // 5. Enter initial state
        if (showMatchmakingOverlay)
        {
            state = DropState.Matchmaking;
            stateTimer = 0f;
            overlayPanel.SetActive(true);
            hudContainer.SetActive(false);
        }
        else
        {
            EnterPlaneState();
        }
    }

    void PickRandomFlightPath()
    {
        // Map spans (-150, 0, -150) to (+150, 0, +150). Keep flight within [-135, +135] so auto-eject stays inside bounds.
        float halfSpan = 135f;
        int edge = Random.Range(0, 4);
        float startOffset = Random.Range(-70f, 70f);
        float endOffset = Random.Range(-70f, 70f);

        switch (edge)
        {
            case 0: // South to North
                flightStartPos = new Vector3(startOffset, planeAltitude, -halfSpan);
                flightEndPos = new Vector3(endOffset, planeAltitude, halfSpan);
                break;
            case 1: // North to South
                flightStartPos = new Vector3(startOffset, planeAltitude, halfSpan);
                flightEndPos = new Vector3(endOffset, planeAltitude, -halfSpan);
                break;
            case 2: // West to East
                flightStartPos = new Vector3(-halfSpan, planeAltitude, startOffset);
                flightEndPos = new Vector3(halfSpan, planeAltitude, endOffset);
                break;
            default: // East to West
                flightStartPos = new Vector3(halfSpan, planeAltitude, startOffset);
                flightEndPos = new Vector3(-halfSpan, planeAltitude, endOffset);
                break;
        }
    }

    void Update()
    {
        if (state == DropState.Inactive || state == DropState.Landed) return;

        // Rule check: Everything must be restored if the player is killed during the flight/fall
        if (playerHealth != null && playerHealth.IsDead)
        {
            AbortAndRestoreOnDeath();
            return;
        }

        // Spin plane propeller while plane exists
        if (propellerTransform != null)
        {
            propellerTransform.Rotate(Vector3.forward, 900f * Time.deltaTime, Space.Self);
        }

        switch (state)
        {
            case DropState.Matchmaking:
                UpdateMatchmaking();
                break;
            case DropState.Countdown:
                UpdateCountdown();
                break;
            case DropState.InPlane:
                UpdateInPlane();
                break;
            case DropState.Freefall:
                UpdateFreefall();
                break;
            case DropState.Parachute:
                UpdateParachute();
                break;
        }
    }

    // =========================================================================
    // STATE UPDATES
    // =========================================================================

    void UpdateMatchmaking()
    {
        stateTimer += Time.deltaTime;
        float t = Mathf.Clamp01(stateTimer / Mathf.Max(0.1f, matchmakingDuration));
        int currentPlayers = Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Lerp(startPlayerCount, maxPlayerCount, t)),
            startPlayerCount,
            maxPlayerCount
        );

        overlayMainText.text = $"Finding players... {currentPlayers}/{maxPlayerCount}";
        overlaySubText.text = "PREPARING DROP ZONE - ERANGELTRAINING GROUNDS";

        // Keep player synced with waiting plane
        playerTransform.position = planeRoot.transform.position;

        if (stateTimer >= matchmakingDuration)
        {
            state = DropState.Countdown;
            stateTimer = countdownDuration;
        }
    }

    void UpdateCountdown()
    {
        stateTimer -= Time.deltaTime;
        int secondsLeft = Mathf.Max(1, Mathf.CeilToInt(stateTimer));

        overlayMainText.text = $"Match starting in {secondsLeft}..";
        overlaySubText.text = "BOARDING C-130 TRANSPORT AIRCRAFT";

        playerTransform.position = planeRoot.transform.position;

        if (stateTimer <= 0f)
        {
            EnterPlaneState();
        }
    }

    void EnterPlaneState()
    {
        state = DropState.InPlane;
        flightProgress = 0f;

        if (overlayPanel != null) overlayPanel.SetActive(false);
        if (hudContainer != null) hudContainer.SetActive(true);

        promptText.text = "Press [F] to Jump";
        UpdateAltitudeHUD();
    }

    void UpdateInPlane()
    {
        flightProgress += Time.deltaTime / Mathf.Max(1f, flightDuration);
        Vector3 currentPlanePos = Vector3.Lerp(flightStartPos, flightEndPos, Mathf.Clamp01(flightProgress));

        if (planeRoot != null)
        {
            planeRoot.transform.position = currentPlanePos;
        }

        // Plane carries the player
        playerTransform.position = currentPlanePos;

        UpdateAltitudeHUD();

        // Jump when pressing F or when plane reaches the opposite edge of the map
        if (Input.GetKeyDown(KeyCode.F) || flightProgress >= 1f)
        {
            JumpFromPlane();
        }
    }

    void JumpFromPlane()
    {
        state = DropState.Freefall;
        verticalSpeed = 5f;

        // Show the player model again and place slightly below the plane belly
        RestorePlayerModelVisibility();
        playerTransform.position = planeRoot != null
            ? planeRoot.transform.position + Vector3.down * 3.0f
            : playerTransform.position + Vector3.down * 3.0f;

        // Remove the plane once jumped
        if (planeRoot != null)
        {
            Destroy(planeRoot);
            planeRoot = null;
        }

        // Rule: Enable EnemySpawner at the moment the player jumps so bots appear on the map
        if (enemySpawnerObject != null && !enemySpawnerObject.activeSelf)
        {
            enemySpawnerObject.SetActive(true);
        }

        promptText.text = "Press [SPACE] to Open Parachute";
        UpdateAltitudeHUD();
    }

    void UpdateFreefall()
    {
        // Accelerate towards terminal velocity (~50 m/s)
        verticalSpeed = Mathf.MoveTowards(verticalSpeed, freefallTerminalSpeed, freefallAcceleration * Time.deltaTime);

        Vector3 horizontalVel = GetAirSteeringVelocity(freefallSteerSpeed);
        Vector3 step = (horizontalVel + Vector3.down * verticalSpeed) * Time.deltaTime;

        // Check if this frame's step would hit the ground
        if (CheckGroundLanding(step, out float groundY))
        {
            CompleteLanding(groundY);
            return;
        }

        playerTransform.position += step;
        ClampPlayerInsideMapBounds();
        UpdateAltitudeHUD();

        // Open parachute manually with SPACE or automatically at ~60 m above the ground
        if (Input.GetKeyDown(KeyCode.Space) || currentAltitudeAGL <= autoParachuteHeight)
        {
            OpenParachute();
        }
    }

    void OpenParachute()
    {
        state = DropState.Parachute;
        verticalSpeed = parachuteDescentSpeed;

        BuildParachuteCanopy();
        promptText.text = "GLIDING - Steer with WASD";
        UpdateAltitudeHUD();
    }

    void UpdateParachute()
    {
        // Smoothly settle vertical speed to parachute descent speed (~6 m/s)
        verticalSpeed = Mathf.MoveTowards(verticalSpeed, parachuteDescentSpeed, 60f * Time.deltaTime);

        Vector3 horizontalVel = GetAirSteeringVelocity(parachuteSteerSpeed);
        Vector3 step = (horizontalVel + Vector3.down * verticalSpeed) * Time.deltaTime;

        // Tilt canopy slightly into the turn for visual feedback
        if (canopyRoot != null)
        {
            float h = GetHorizontalAxisRaw();
            float v = GetVerticalAxisRaw();
            Quaternion targetLocalRot = Quaternion.Euler(v * 10f, 0f, -h * 12f);
            canopyRoot.transform.localRotation = Quaternion.Slerp(
                canopyRoot.transform.localRotation,
                targetLocalRot,
                8f * Time.deltaTime
            );
        }

        // Land when downward raycast finds ground (terrain, houses, rocks, any collider)
        if (CheckGroundLanding(step, out float groundY))
        {
            CompleteLanding(groundY);
            return;
        }

        playerTransform.position += step;
        ClampPlayerInsideMapBounds();
        UpdateAltitudeHUD();
    }

    // =========================================================================
    // STEERING, GROUND DETECTION & LANDING RESTORATION
    // =========================================================================

    Vector3 GetAirSteeringVelocity(float speed)
    {
        float h = GetHorizontalAxisRaw();
        float v = GetVerticalAxisRaw();
        Vector2 input = new Vector2(h, v);
        if (input.sqrMagnitude > 1f) input.Normalize();

        if (mainCam == null) mainCam = Camera.main;

        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;

        if (mainCam != null)
        {
            forward = mainCam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            right = mainCam.transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            right.Normalize();

            // Rotate the player smoothly to face the camera's horizontal look direction
            playerTransform.rotation = Quaternion.Slerp(
                playerTransform.rotation,
                Quaternion.LookRotation(forward, Vector3.up),
                10f * Time.deltaTime
            );
        }

        return (forward * input.y + right * input.x) * speed;
    }

    float GetHorizontalAxisRaw()
    {
        float val = 0f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) val += 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) val -= 1f;
        return val;
    }

    float GetVerticalAxisRaw()
    {
        float val = 0f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) val += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) val -= 1f;
        return val;
    }

    void ClampPlayerInsideMapBounds()
    {
        // Keep player within the 300x300 terrain (-148 to +148)
        Vector3 pos = playerTransform.position;
        pos.x = Mathf.Clamp(pos.x, -148f, 148f);
        pos.z = Mathf.Clamp(pos.z, -148f, 148f);
        playerTransform.position = pos;
    }

    bool CheckGroundLanding(Vector3 step, out float hitGroundY)
    {
        hitGroundY = 0f;
        Vector3 nextPos = playerTransform.position + step;

        // Player pivot is capsule CENTRE (ground is 1.0 m below pivot).
        // Cast from slightly above the current pivot down past the next frame's feet position.
        Vector3 rayOrigin = playerTransform.position + Vector3.up * 0.5f;
        float castDistance = Mathf.Abs(step.y) + 1.55f;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, castDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            // Feet are 1.0 m below pivot
            float nextFeetY = nextPos.y - 1.0f;
            if (nextFeetY <= hit.point.y + 0.08f)
            {
                hitGroundY = hit.point.y;
                return true;
            }
        }

        // Safety fallback if raycast ever misses terrain at y = 0
        if (nextPos.y <= 1.0f)
        {
            hitGroundY = 0f;
            return true;
        }

        return false;
    }

    float MeasureAltitudeAboveGround()
    {
        Vector3 rayOrigin = playerTransform.position + Vector3.up * 0.5f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 1000f, groundMask, QueryTriggerInteraction.Ignore))
        {
            // Subtract 1.0 m because pivot is at capsule centre
            return Mathf.Max(0f, (playerTransform.position.y - 1.0f) - hit.point.y);
        }
        return Mathf.Max(0f, playerTransform.position.y - 1.0f);
    }

    void CompleteLanding(float groundY)
    {
        state = DropState.Landed;

        // 1. Remove canopy and plane if any remain
        if (canopyRoot != null) Destroy(canopyRoot);
        if (planeRoot != null) Destroy(planeRoot);
        if (uiCanvasObj != null) Destroy(uiCanvasObj);

        // 2. Ensure player model is visible
        RestorePlayerModelVisibility();

        // 3. Set position on the ground WHILE CharacterController is still disabled (pivot is 1m above ground)
        Vector3 landedPos = playerTransform.position;
        landedPos.y = groundY + 1.0f;
        playerTransform.position = landedPos;

        // 4. Call PlayerMovement.ResetFall() BEFORE re-enabling so there is zero fall damage
        if (playerMovement != null)
        {
            playerMovement.ResetFall();
        }

        // 5. Switch ALL player components back ON
        SetPlayerComponentsEnabled(true);
    }

    void AbortAndRestoreOnDeath()
    {
        state = DropState.Landed;

        if (canopyRoot != null) Destroy(canopyRoot);
        if (planeRoot != null) Destroy(planeRoot);
        if (uiCanvasObj != null) Destroy(uiCanvasObj);

        RestorePlayerModelVisibility();

        if (enemySpawnerObject != null && !enemySpawnerObject.activeSelf)
        {
            enemySpawnerObject.SetActive(true);
        }

        if (playerMovement != null)
        {
            playerMovement.ResetFall();
        }

        SetPlayerComponentsEnabled(true);
    }

    void SetPlayerComponentsEnabled(bool isEnabled)
    {
        if (playerMovement != null) playerMovement.enabled = isEnabled;
        if (charController != null) charController.enabled = isEnabled;

        if (playerGuns != null)
        {
            for (int i = 0; i < playerGuns.Length; i++)
            {
                if (playerGuns[i] != null) playerGuns[i].enabled = isEnabled;
            }
        }

        if (weaponSwitcher != null) weaponSwitcher.enabled = isEnabled;
        if (playerInventory != null) playerInventory.enabled = isEnabled;
        if (scopeController != null) scopeController.enabled = isEnabled;
    }

    void CacheAndHidePlayerModel()
    {
        playerRenderers.Clear();
        initialRendererStates.Clear();

        Renderer[] renderers = playerTransform.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            playerRenderers.Add(r);
            initialRendererStates.Add(r.enabled);
            r.enabled = false;
        }
    }

    void RestorePlayerModelVisibility()
    {
        for (int i = 0; i < playerRenderers.Count; i++)
        {
            if (playerRenderers[i] != null)
            {
                playerRenderers[i].enabled = initialRendererStates[i];
            }
        }
        playerRenderers.Clear();
        initialRendererStates.Clear();
    }

    // =========================================================================
    // 3D PRIMITIVE BUILDERS (PLANE & PARACHUTE CANOPY)
    // =========================================================================

    void BuildPlanePrimitive()
    {
        if (planeRoot != null) Destroy(planeRoot);

        planeRoot = new GameObject("DropPlane_C130");

        Material bodyMat = CreateSafeMaterial(new Color(0.28f, 0.31f, 0.34f)); // Tactical slate grey
        Material wingMat = CreateSafeMaterial(new Color(0.20f, 0.22f, 0.25f)); // Darker grey
        Material accentMat = CreateSafeMaterial(new Color(0.85f, 0.38f, 0.12f)); // Rescue orange stripes
        Material glassMat = CreateSafeMaterial(new Color(0.12f, 0.18f, 0.24f)); // Cockpit glass

        // Main Fuselage
        CreatePrimitivePart(PrimitiveType.Cube, "Fuselage", planeRoot.transform,
            Vector3.zero, Vector3.zero, new Vector3(3.4f, 3.0f, 16f), bodyMat);

        // Nose Cone
        CreatePrimitivePart(PrimitiveType.Cube, "Nose", planeRoot.transform,
            new Vector3(0f, -0.2f, 8.8f), new Vector3(12f, 0f, 0f), new Vector3(2.8f, 2.2f, 2.6f), bodyMat);

        // Cockpit Windshield
        CreatePrimitivePart(PrimitiveType.Cube, "CockpitGlass", planeRoot.transform,
            new Vector3(0f, 0.95f, 7.6f), new Vector3(20f, 0f, 0f), new Vector3(2.4f, 1.0f, 2.0f), glassMat);

        // Main High Wing
        CreatePrimitivePart(PrimitiveType.Cube, "MainWing", planeRoot.transform,
            new Vector3(0f, 1.3f, 0.8f), Vector3.zero, new Vector3(24f, 0.45f, 3.6f), wingMat);

        // Wingtip Stripes
        CreatePrimitivePart(PrimitiveType.Cube, "LeftWingStripe", planeRoot.transform,
            new Vector3(-11.2f, 1.32f, 0.8f), Vector3.zero, new Vector3(1.2f, 0.48f, 3.62f), accentMat);
        CreatePrimitivePart(PrimitiveType.Cube, "RightWingStripe", planeRoot.transform,
            new Vector3(11.2f, 1.32f, 0.8f), Vector3.zero, new Vector3(1.2f, 0.48f, 3.62f), accentMat);

        // Tail Fin (Vertical Stabilizer)
        CreatePrimitivePart(PrimitiveType.Cube, "TailFin", planeRoot.transform,
            new Vector3(0f, 3.2f, -6.8f), new Vector3(-12f, 0f, 0f), new Vector3(0.5f, 4.2f, 2.8f), wingMat);

        // Tail Stripe
        CreatePrimitivePart(PrimitiveType.Cube, "TailStripe", planeRoot.transform,
            new Vector3(0f, 4.6f, -7.1f), new Vector3(-12f, 0f, 0f), new Vector3(0.54f, 0.9f, 2.7f), accentMat);

        // Horizontal Stabilizer
        CreatePrimitivePart(PrimitiveType.Cube, "TailWing", planeRoot.transform,
            new Vector3(0f, 1.6f, -7.0f), Vector3.zero, new Vector3(8.5f, 0.35f, 2.0f), wingMat);

        // 4 Under-wing Engines
        float[] engineX = { -7.5f, -3.8f, 3.8f, 7.5f };
        foreach (float ex in engineX)
        {
            CreatePrimitivePart(PrimitiveType.Cylinder, "EnginePod", planeRoot.transform,
                new Vector3(ex, 0.6f, 2.2f), new Vector3(90f, 0f, 0f), new Vector3(1.0f, 1.4f, 1.0f), bodyMat);
        }

        // Spinning Nose Propeller Hub
        GameObject propHub = new GameObject("PropellerHub");
        propHub.transform.SetParent(planeRoot.transform, false);
        propHub.transform.localPosition = new Vector3(0f, -0.2f, 10.2f);
        propellerTransform = propHub.transform;

        CreatePrimitivePart(PrimitiveType.Cube, "Blade1", propHub.transform,
            Vector3.zero, Vector3.zero, new Vector3(3.4f, 0.25f, 0.1f), accentMat);
        CreatePrimitivePart(PrimitiveType.Cube, "Blade2", propHub.transform,
            Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(3.4f, 0.25f, 0.1f), accentMat);
    }

    void BuildParachuteCanopy()
    {
        if (canopyRoot != null) Destroy(canopyRoot);

        canopyRoot = new GameObject("ParachuteCanopy");
        canopyRoot.transform.SetParent(playerTransform, false);
        canopyRoot.transform.localPosition = Vector3.zero;
        canopyRoot.transform.localRotation = Quaternion.identity;

        Material canopyMainMat = CreateSafeMaterial(new Color(0.90f, 0.36f, 0.10f)); // High-vis orange
        Material canopyStripeMat = CreateSafeMaterial(new Color(0.15f, 0.20f, 0.25f)); // Dark navy accent
        Material cordMat = CreateSafeMaterial(new Color(0.18f, 0.18f, 0.18f));

        // Squashed sphere canopy above the player
        CreatePrimitivePart(PrimitiveType.Sphere, "CanopyDome", canopyRoot.transform,
            new Vector3(0f, 4.2f, 0f), Vector3.zero, new Vector3(5.2f, 1.35f, 3.6f), canopyMainMat);

        // Center stripe on the canopy
        CreatePrimitivePart(PrimitiveType.Sphere, "CanopyCenterStripe", canopyRoot.transform,
            new Vector3(0f, 4.26f, 0f), Vector3.zero, new Vector3(1.6f, 1.38f, 3.66f), canopyStripeMat);

        // 4 thin riser cords connecting player shoulders to the canopy
        CreatePrimitivePart(PrimitiveType.Cube, "CordFL", canopyRoot.transform,
            new Vector3(-0.9f, 2.4f, 0.6f), new Vector3(10f, 0f, 18f), new Vector3(0.04f, 3.4f, 0.04f), cordMat);
        CreatePrimitivePart(PrimitiveType.Cube, "CordFR", canopyRoot.transform,
            new Vector3(0.9f, 2.4f, 0.6f), new Vector3(10f, 0f, -18f), new Vector3(0.04f, 3.4f, 0.04f), cordMat);
        CreatePrimitivePart(PrimitiveType.Cube, "CordBL", canopyRoot.transform,
            new Vector3(-0.9f, 2.4f, -0.6f), new Vector3(-10f, 0f, 18f), new Vector3(0.04f, 3.4f, 0.04f), cordMat);
        CreatePrimitivePart(PrimitiveType.Cube, "CordBR", canopyRoot.transform,
            new Vector3(0.9f, 2.4f, -0.6f), new Vector3(-10f, 0f, -18f), new Vector3(0.04f, 3.4f, 0.04f), cordMat);
    }

    GameObject CreatePrimitivePart(PrimitiveType type, string partName, Transform parent,
        Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = partName;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPos;
        obj.transform.localRotation = Quaternion.Euler(localEuler);
        obj.transform.localScale = localScale;

        // Remove collider so plane/canopy primitives never interfere with physics or raycasts
        Collider col = obj.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = obj.GetComponent<Renderer>();
        if (rend != null && mat != null)
        {
            rend.sharedMaterial = mat;
        }

        return obj;
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

    // =========================================================================
    // UI BUILDER (SORTING ORDER 13)
    // =========================================================================

    void BuildSequenceUI()
    {
        if (uiCanvasObj != null) Destroy(uiCanvasObj);

        uiCanvasObj = new GameObject("MatchStartCanvas_Order13");
        Canvas canvas = uiCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 13; // Rule: sorting order 13

        CanvasScaler scaler = uiCanvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        uiCanvasObj.AddComponent<GraphicRaycaster>();

        Font uiFont = GetBuiltInFont();

        // 1. Fake Matchmaking / Countdown Overlay Panel
        overlayPanel = CreateUIObject("MatchmakingOverlay", uiCanvasObj.transform);
        RectTransform overlayRect = overlayPanel.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayBg = overlayPanel.AddComponent<Image>();
        overlayBg.color = new Color(0.05f, 0.07f, 0.10f, 0.88f);

        // Center Banner Box
        GameObject bannerBox = CreateUIObject("BannerBox", overlayPanel.transform);
        RectTransform bannerRect = bannerBox.GetComponent<RectTransform>();
        bannerRect.anchorMin = new Vector2(0.5f, 0.5f);
        bannerRect.anchorMax = new Vector2(0.5f, 0.5f);
        bannerRect.sizeDelta = new Vector2(760f, 190f);
        bannerRect.anchoredPosition = Vector2.zero;

        Image bannerBg = bannerBox.AddComponent<Image>();
        bannerBg.color = new Color(0.12f, 0.15f, 0.20f, 0.95f);

        overlayMainText = CreateUIText("OverlayMainText", bannerBox.transform, uiFont, 42, FontStyle.Bold,
            new Color(1f, 0.78f, 0.18f), TextAnchor.MiddleCenter);
        overlayMainText.rectTransform.anchorMin = new Vector2(0f, 0.35f);
        overlayMainText.rectTransform.anchorMax = new Vector2(1f, 0.95f);
        overlayMainText.rectTransform.offsetMin = Vector2.zero;
        overlayMainText.rectTransform.offsetMax = Vector2.zero;

        overlaySubText = CreateUIText("OverlaySubText", bannerBox.transform, uiFont, 20, FontStyle.Normal,
            new Color(0.80f, 0.85f, 0.90f), TextAnchor.MiddleCenter);
        overlaySubText.rectTransform.anchorMin = new Vector2(0f, 0.08f);
        overlaySubText.rectTransform.anchorMax = new Vector2(1f, 0.38f);
        overlaySubText.rectTransform.offsetMin = Vector2.zero;
        overlaySubText.rectTransform.offsetMax = Vector2.zero;

        // 2. Flight / Freefall / Parachute HUD Container
        hudContainer = CreateUIObject("DropHUDContainer", uiCanvasObj.transform);
        RectTransform hudRect = hudContainer.GetComponent<RectTransform>();
        hudRect.anchorMin = Vector2.zero;
        hudRect.anchorMax = Vector2.one;
        hudRect.offsetMin = Vector2.zero;
        hudRect.offsetMax = Vector2.zero;

        // Action Prompt Box (Bottom-Center)
        GameObject promptBgObj = CreateUIObject("PromptBox", hudContainer.transform);
        RectTransform promptBgRect = promptBgObj.GetComponent<RectTransform>();
        promptBgRect.anchorMin = new Vector2(0.5f, 0.16f);
        promptBgRect.anchorMax = new Vector2(0.5f, 0.16f);
        promptBgRect.sizeDelta = new Vector2(540f, 64f);
        promptBgRect.anchoredPosition = Vector2.zero;

        Image promptBg = promptBgObj.AddComponent<Image>();
        promptBg.color = new Color(0f, 0f, 0f, 0.65f);

        promptText = CreateUIText("PromptText", promptBgObj.transform, uiFont, 28, FontStyle.Bold,
            new Color(1f, 0.86f, 0.25f), TextAnchor.MiddleCenter);
        promptText.rectTransform.anchorMin = Vector2.zero;
        promptText.rectTransform.anchorMax = Vector2.one;
        promptText.rectTransform.offsetMin = Vector2.zero;
        promptText.rectTransform.offsetMax = Vector2.zero;

        // Flat Altitude HUD Panel (Right Side, BGMI style)
        GameObject altPanel = CreateUIObject("AltitudePanel", hudContainer.transform);
        RectTransform altRect = altPanel.GetComponent<RectTransform>();
        altRect.anchorMin = new Vector2(0.92f, 0.5f);
        altRect.anchorMax = new Vector2(0.92f, 0.5f);
        altRect.sizeDelta = new Vector2(170f, 280f);
        altRect.anchoredPosition = Vector2.zero;

        Image altBg = altPanel.AddComponent<Image>();
        altBg.color = new Color(0f, 0f, 0f, 0.58f);

        // Vertical Altitude Gauge Bar Background
        GameObject barTrack = CreateUIObject("AltBarTrack", altPanel.transform);
        RectTransform trackRect = barTrack.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0.22f, 0.12f);
        trackRect.anchorMax = new Vector2(0.32f, 0.72f);
        trackRect.offsetMin = Vector2.zero;
        trackRect.offsetMax = Vector2.zero;

        Image trackImg = barTrack.AddComponent<Image>();
        trackImg.color = new Color(1f, 1f, 1f, 0.22f);

        // Vertical Altitude Fill
        GameObject barFillObj = CreateUIObject("AltBarFill", barTrack.transform);
        RectTransform fillRect = barFillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        altBarFill = barFillObj.AddComponent<Image>();
        altBarFill.color = new Color(0.25f, 0.85f, 1f, 0.9f);

        // Altitude Readout Text
        altitudeText = CreateUIText("AltitudeText", altPanel.transform, uiFont, 24, FontStyle.Bold,
            Color.white, TextAnchor.UpperCenter);
        altitudeText.rectTransform.anchorMin = new Vector2(0.05f, 0.75f);
        altitudeText.rectTransform.anchorMax = new Vector2(0.95f, 0.96f);
        altitudeText.rectTransform.offsetMin = Vector2.zero;
        altitudeText.rectTransform.offsetMax = Vector2.zero;
    }

    void UpdateAltitudeHUD()
    {
        currentAltitudeAGL = MeasureAltitudeAboveGround();

        if (altitudeText != null)
        {
            int altMeters = Mathf.RoundToInt(currentAltitudeAGL);
            int spd = Mathf.RoundToInt(verticalSpeed);
            string phaseLabel = state == DropState.InPlane ? "PLANE"
                : (state == DropState.Parachute ? "CHUTE" : "FALL");

            altitudeText.text = $"{altMeters} m\n<size=16>{spd} m/s ({phaseLabel})</size>";
        }

        if (altBarFill != null)
        {
            float normalizedAlt = Mathf.Clamp01(currentAltitudeAGL / Mathf.Max(1f, planeAltitude));
            RectTransform fillRect = altBarFill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(1f, normalizedAlt);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            altBarFill.color = (state == DropState.Parachute)
                ? new Color(0.3f, 0.95f, 0.45f, 0.9f)
                : new Color(0.25f, 0.85f, 1f, 0.9f);
        }
    }

    GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    Text CreateUIText(string name, Transform parent, Font font, int fontSize, FontStyle style, Color color, TextAnchor anchor)
    {
        GameObject go = CreateUIObject(name, parent);
        Text txt = go.AddComponent<Text>();
        txt.font = font;
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.color = color;
        txt.alignment = anchor;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;

        Shadow shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);

        return txt;
    }

    Font GetBuiltInFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 24);
        return f;
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
