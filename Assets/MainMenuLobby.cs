using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Standalone BGMI-Style 3D Character Lobby & Main Menu.
/// - Builds a 3D showcase stage high in the sky at (0, 600, 0) with a cloned player character model (no floating gun).
/// - Automatically hides all in-game HUD canvases (crosshair, weapon slots on the right, minimap) while in the lobby,
///   and restores them the moment you click START.
/// - Lets you customize the top Game Title Banner directly in the Inspector.
/// </summary>
public class MainMenuLobby : MonoBehaviour
{
    [Header("Master Toggle")]
    [Tooltip("Ticked = game boots into the 3D Main Menu Lobby first. Unticked = skips straight to the match.")]
    public bool enableLobbyMenu = true;

    [Header("Title & Profile Customization")]
    public string gameTitleBanner = "BATTLEGROUNDS MOBILE  |  NITK EDITION";
    public string playerName = "Commando@NITK";
    public int playerLevel = 67;
    public string rankTitle = "Silver III";
    public int battlePoints = 4500;
    public int unknownCash = 600;

    [Header("Scene References (Auto-found if left empty)")]
    public Transform playerTransform;
    public MatchStartSequence matchStartSequence;
    public GameObject enemySpawnerObject;

    private bool isLobbyActive = false;

    // 3D Stage Objects
    private GameObject stageRoot;
    private Camera lobbyCamera;
    private Transform mannequinPivot;
    private Transform platformRing;
    private float targetRotationY = 165f;
    private float currentRotationY = 165f;

    // UI Objects (Sorting Order 20)
    private GameObject lobbyCanvasObj;
    private Canvas lobbyCanvas;
    private GameObject profileModalPanel;
    private GameObject controlsModalPanel;
    private Text modeButtonLabel;
    private int selectedModeIndex = 0;
    private readonly string[] modeNames =
    {
        "CLASSIC - COUNTRYLAND (300x300)  |  SOLO TPP",
        "CLASSIC - COUNTRYLAND (300x300)  |  SQUAD (1vBots)",
        "ARCADE - WAR MODE (HIGH LOOT)  |  SOLO"
    };

    // Track hidden in-game HUD canvases/objects so we restore them cleanly when START is clicked
    private List<Canvas> hiddenHudCanvases = new List<Canvas>();
    private List<GameObject> hiddenHudObjects = new List<GameObject>();
    private List<Material> createdMaterials = new List<Material>();

    void Start()
    {
        FindReferences();

        if (!enableLobbyMenu)
        {
            enabled = false;
            return;
        }

        OpenLobby();
    }

    void FindReferences()
    {
        if (playerTransform == null)
        {
            GameObject pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj == null) pObj = GameObject.Find("Player");
            if (pObj != null) playerTransform = pObj.transform;
        }

        if (matchStartSequence == null)
        {
            matchStartSequence = FindFirstObjectByType<MatchStartSequence>();
        }

        if (enemySpawnerObject == null)
        {
            EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include);
            if (spawner != null) enemySpawnerObject = spawner.gameObject;
            else
            {
                GameObject found = GameObject.Find("EnemySpawner");
                if (found != null) enemySpawnerObject = found;
            }
        }
    }

    void OpenLobby()
    {
        isLobbyActive = true;

        // 1. Ensure an EventSystem exists so UI buttons are clickable
        EnsureEventSystem();

        // 2. Keep EnemySpawner and Player gameplay components switched OFF while in the lobby
        if (enemySpawnerObject != null) enemySpawnerObject.SetActive(false);
        SetPlayerGameplayEnabled(false);

        // 3. Free the mouse cursor so the user can click buttons and drag the character
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 4. Build the 3D Sky Stage + Character Showcase + Camera (Floating gun removed!)
        Build3DLobbyStage();

        // 5. Build the BGMI Main Menu Canvas (Sorting Order 20)
        BuildLobbyUI();

        // 6. Hide in-game HUD (Crosshair, Right-hand Weapon Slots, Minimap) while in Lobby
        SuppressInGameHUDDuringLobby();
    }

    void Update()
    {
        if (!isLobbyActive) return;

        // Keep cursor unlocked while in lobby
        if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Catch any HUD canvases created after our Start() (like GameHUD / Minimap) and hide them in Lobby
        SuppressInGameHUDDuringLobby();

        // Spin decorative platform ring slowly
        if (platformRing != null)
        {
            platformRing.Rotate(Vector3.up, 20f * Time.deltaTime, Space.Self);
        }

        // Click and drag anywhere to rotate the 3D character
        if (Input.GetMouseButton(0))
        {
            float mouseX = Input.GetAxis("Mouse X");
            targetRotationY -= mouseX * 6.5f;
        }
        else
        {
            // Subtle idle turntable sway when not dragging
            targetRotationY += Mathf.Sin(Time.time * 0.7f) * 4f * Time.deltaTime;
        }

        currentRotationY = Mathf.Lerp(currentRotationY, targetRotationY, 10f * Time.deltaTime);
        if (mannequinPivot != null)
        {
            mannequinPivot.localRotation = Quaternion.Euler(0f, currentRotationY, 0f);
        }

        // Keyboard shortcut: pressing Return/Enter also starts the match
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            OnClickStartMatch();
        }
    }

    // =========================================================================
    // HIDE / RESTORE IN-GAME HUD (CROSSHAIR, WEAPON SLOTS, MINIMAP)
    // =========================================================================

    void SuppressInGameHUDDuringLobby()
    {
        // Disable every Canvas in the scene except our own Lobby Canvas
        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas c in allCanvases)
        {
            if (c == null || c == lobbyCanvas) continue;
            if (c.enabled)
            {
                c.enabled = false;
                if (!hiddenHudCanvases.Contains(c))
                {
                    hiddenHudCanvases.Add(c);
                }
            }
        }

        // Also hide any standalone HUD / Crosshair GameObjects if they use OnGUI or SpriteRenderers
        string[] hudNames = { "HUD", "Canvas", "Crosshair", "crosshair" };
        foreach (string hName in hudNames)
        {
            GameObject go = GameObject.Find(hName);
            if (go != null && go != lobbyCanvasObj && go.activeSelf)
            {
                go.SetActive(false);
                if (!hiddenHudObjects.Contains(go))
                {
                    hiddenHudObjects.Add(go);
                }
            }
        }
    }

    void RestoreInGameHUDAfterLobby()
    {
        for (int i = 0; i < hiddenHudObjects.Count; i++)
        {
            if (hiddenHudObjects[i] != null)
            {
                hiddenHudObjects[i].SetActive(true);
            }
        }
        hiddenHudObjects.Clear();

        for (int i = 0; i < hiddenHudCanvases.Count; i++)
        {
            if (hiddenHudCanvases[i] != null)
            {
                hiddenHudCanvases[i].enabled = true;
            }
        }
        hiddenHudCanvases.Clear();
    }

    // =========================================================================
    // BUTTON ACTIONS
    // =========================================================================

    public void OnClickStartMatch()
    {
        if (!isLobbyActive) return;
        isLobbyActive = false;

        // 1. Restore all in-game HUD canvases (Crosshair, Weapon Slots, Minimap) for matchmaking & gameplay
        RestoreInGameHUDAfterLobby();

        // 2. Destroy 3D Lobby Stage and Lobby UI Canvas
        if (stageRoot != null) Destroy(stageRoot);
        if (lobbyCanvasObj != null) Destroy(lobbyCanvasObj);

        // 3. Lock the cursor for 3D shooter gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 4. Hand over control to MatchStartSequence (or normal ground gameplay if missing)
        if (matchStartSequence != null)
        {
            matchStartSequence.enabled = true;
            matchStartSequence.TriggerSequenceFromLobby();
        }
        else
        {
            if (enemySpawnerObject != null) enemySpawnerObject.SetActive(true);
            SetPlayerGameplayEnabled(true);
        }

        enabled = false;
    }

    void OnClickCycleMode()
    {
        selectedModeIndex = (selectedModeIndex + 1) % modeNames.Length;
        if (modeButtonLabel != null)
        {
            modeButtonLabel.text = modeNames[selectedModeIndex];
        }
    }

    void ToggleProfileModal()
    {
        if (profileModalPanel != null)
        {
            bool nextState = !profileModalPanel.activeSelf;
            profileModalPanel.SetActive(nextState);
            if (nextState && controlsModalPanel != null) controlsModalPanel.SetActive(false);
        }
    }

    void ToggleControlsModal()
    {
        if (controlsModalPanel != null)
        {
            bool nextState = !controlsModalPanel.activeSelf;
            controlsModalPanel.SetActive(nextState);
            if (nextState && profileModalPanel != null) profileModalPanel.SetActive(false);
        }
    }

    void OnClickQuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // =========================================================================
    // 3D LOBBY STAGE & CHARACTER CLONER (NO FLOATING GUN)
    // =========================================================================

    void Build3DLobbyStage()
    {
        if (stageRoot != null) Destroy(stageRoot);

        // Place stage high in the sky so terrain/trees don't clip into it
        stageRoot = new GameObject("BGMI_3D_LobbyStage");
        stageRoot.transform.position = new Vector3(0f, 600f, 0f);

        Material darkFloorMat = CreateSafeMaterial(new Color(0.10f, 0.12f, 0.16f));
        Material pedestalMat = CreateSafeMaterial(new Color(0.16f, 0.20f, 0.26f));
        Material goldAccentMat = CreateSafeMaterial(new Color(0.95f, 0.72f, 0.15f));
        Material cyanGlowMat = CreateSafeMaterial(new Color(0.18f, 0.82f, 0.98f));
        Material pillarMat = CreateSafeMaterial(new Color(0.14f, 0.16f, 0.20f));

        // 1. Tactical Hangar Backdrop & Floor
        MakePart(PrimitiveType.Cylinder, "StageFloor", stageRoot.transform,
            new Vector3(0f, -0.35f, 0f), Vector3.zero, new Vector3(14f, 0.25f, 14f), darkFloorMat);

        MakePart(PrimitiveType.Cube, "BackWall", stageRoot.transform,
            new Vector3(0f, 3.2f, 5.8f), Vector3.zero, new Vector3(18f, 9f, 0.4f), darkFloorMat);

        // Decorative Angled Hangar Pillars behind the character
        MakePart(PrimitiveType.Cube, "LeftPillar", stageRoot.transform,
            new Vector3(-3.4f, 2.8f, 4.2f), new Vector3(0f, 0f, -10f), new Vector3(0.7f, 7f, 0.7f), pillarMat);
        MakePart(PrimitiveType.Cube, "RightPillar", stageRoot.transform,
            new Vector3(3.4f, 2.8f, 4.2f), new Vector3(0f, 0f, 10f), new Vector3(0.7f, 7f, 0.7f), pillarMat);

        MakePart(PrimitiveType.Cube, "LeftNeonStrip", stageRoot.transform,
            new Vector3(-2.6f, 2.8f, 4.5f), new Vector3(0f, 0f, -10f), new Vector3(0.12f, 6.5f, 0.12f), goldAccentMat);
        MakePart(PrimitiveType.Cube, "RightNeonStrip", stageRoot.transform,
            new Vector3(2.6f, 2.8f, 4.5f), new Vector3(0f, 0f, 10f), new Vector3(0.12f, 6.5f, 0.12f), goldAccentMat);

        // 2. Center Character Pedestal
        MakePart(PrimitiveType.Cylinder, "PedestalBase", stageRoot.transform,
            new Vector3(0f, -0.08f, 0f), Vector3.zero, new Vector3(2.8f, 0.08f, 2.8f), pedestalMat);

        MakePart(PrimitiveType.Cylinder, "PedestalInnerRing", stageRoot.transform,
            new Vector3(0f, 0.01f, 0f), Vector3.zero, new Vector3(2.25f, 0.02f, 2.25f), cyanGlowMat);

        MakePart(PrimitiveType.Cylinder, "PedestalTop", stageRoot.transform,
            new Vector3(0f, 0.03f, 0f), Vector3.zero, new Vector3(2.10f, 0.025f, 2.10f), pedestalMat);

        // Rotating accent blocks around pedestal
        GameObject ringObj = new GameObject("RotatingRing");
        ringObj.transform.SetParent(stageRoot.transform, false);
        ringObj.transform.localPosition = Vector3.zero;
        platformRing = ringObj.transform;

        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(Mathf.Cos(angle) * 1.45f, 0.02f, Mathf.Sin(angle) * 1.45f);
            MakePart(PrimitiveType.Cube, "RingNode_" + i, platformRing,
                pos, new Vector3(0f, -i * 45f, 0f), new Vector3(0.16f, 0.05f, 0.35f), goldAccentMat);
        }

        // Side Supply Crates for Battle Royale Atmosphere
        MakePart(PrimitiveType.Cube, "SupplyCrateLeft", stageRoot.transform,
            new Vector3(-2.1f, 0.35f, 1.3f), new Vector3(0f, 28f, 0f), new Vector3(0.95f, 0.7f, 0.95f), goldAccentMat);
        MakePart(PrimitiveType.Cube, "SupplyCrateTop", stageRoot.transform,
            new Vector3(-2.1f, 0.72f, 1.3f), new Vector3(0f, 28f, 0f), new Vector3(1.0f, 0.08f, 1.0f), pedestalMat);

        // 3. Character Mannequin Pivot (Spins when dragging mouse)
        GameObject pivotObj = new GameObject("CharacterMannequinPivot");
        pivotObj.transform.SetParent(stageRoot.transform, false);
        pivotObj.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        mannequinPivot = pivotObj.transform;
        mannequinPivot.localRotation = Quaternion.Euler(0f, currentRotationY, 0f);

        ClonePlayerVisualIntoLobby(mannequinPivot);

        // 4. Dedicated Lobby Lighting
        GameObject keyLightObj = new GameObject("LobbyKeyLight");
        keyLightObj.transform.SetParent(stageRoot.transform, false);
        keyLightObj.transform.localPosition = new Vector3(1.8f, 3.2f, -2.8f);
        keyLightObj.transform.localRotation = Quaternion.Euler(28f, -28f, 0f);
        Light keyLight = keyLightObj.AddComponent<Light>();
        keyLight.type = LightType.Point;
        keyLight.range = 18f;
        keyLight.intensity = 3.5f;
        keyLight.color = new Color(1f, 0.96f, 0.90f);

        GameObject rimLightObj = new GameObject("LobbyRimLight");
        rimLightObj.transform.SetParent(stageRoot.transform, false);
        rimLightObj.transform.localPosition = new Vector3(-2.2f, 2.6f, 2.5f);
        Light rimLight = rimLightObj.AddComponent<Light>();
        rimLight.type = LightType.Point;
        rimLight.range = 14f;
        rimLight.intensity = 2.8f;
        rimLight.color = new Color(0.25f, 0.75f, 1f);

        // 5. Dedicated Lobby Camera
        GameObject camObj = new GameObject("LobbyShowcaseCamera");
        camObj.transform.SetParent(stageRoot.transform, false);
        camObj.transform.localPosition = new Vector3(0f, 1.32f, -3.55f);
        camObj.transform.localRotation = Quaternion.Euler(4.5f, 0f, 0f);

        lobbyCamera = camObj.AddComponent<Camera>();
        lobbyCamera.clearFlags = CameraClearFlags.SolidColor;
        lobbyCamera.backgroundColor = new Color(0.06f, 0.08f, 0.12f);
        lobbyCamera.fieldOfView = 42f;
        lobbyCamera.depth = 50f;
    }

    void ClonePlayerVisualIntoLobby(Transform parentPivot)
    {
        GameObject clonedVisual = null;

        if (playerTransform != null)
        {
            Animator[] anims = playerTransform.GetComponentsInChildren<Animator>(true);
            foreach (Animator anim in anims)
            {
                if (anim != null && anim.transform != playerTransform)
                {
                    clonedVisual = Instantiate(anim.gameObject, parentPivot);
                    break;
                }
            }
        }

        if (clonedVisual != null)
        {
            clonedVisual.name = "Lobby_PlayerSkin";
            clonedVisual.SetActive(true);
            clonedVisual.transform.localPosition = Vector3.zero;
            clonedVisual.transform.localRotation = Quaternion.identity;

            Renderer[] rends = clonedVisual.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in rends) r.enabled = true;

            MonoBehaviour[] scripts = clonedVisual.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour mb in scripts)
            {
                if (mb != null) Destroy(mb);
            }

            Collider[] cols = clonedVisual.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in cols)
            {
                if (c != null) Destroy(c);
            }

            Animator cloneAnim = clonedVisual.GetComponent<Animator>();
            if (cloneAnim != null)
            {
                cloneAnim.applyRootMotion = false;
                cloneAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }
        else
        {
            Material armorMat = CreateSafeMaterial(new Color(0.22f, 0.28f, 0.24f));
            Material visorMat = CreateSafeMaterial(new Color(0.95f, 0.72f, 0.15f));
            MakePart(PrimitiveType.Capsule, "Body", parentPivot, new Vector3(0f, 1.0f, 0f), Vector3.zero, new Vector3(0.55f, 0.95f, 0.45f), armorMat);
            MakePart(PrimitiveType.Sphere, "Helmet", parentPivot, new Vector3(0f, 1.85f, 0f), Vector3.zero, new Vector3(0.42f, 0.42f, 0.42f), armorMat);
            MakePart(PrimitiveType.Cube, "Visor", parentPivot, new Vector3(0f, 1.86f, 0.16f), Vector3.zero, new Vector3(0.28f, 0.10f, 0.15f), visorMat);
        }
    }

    // =========================================================================
    // BGMI MAIN MENU UI BUILDER (SORTING ORDER 20)
    // =========================================================================

    void BuildLobbyUI()
    {
        if (lobbyCanvasObj != null) Destroy(lobbyCanvasObj);

        lobbyCanvasObj = new GameObject("BGMI_LobbyCanvas_Order20");
        lobbyCanvas = lobbyCanvasObj.AddComponent<Canvas>();
        lobbyCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        lobbyCanvas.sortingOrder = 20;

        CanvasScaler scaler = lobbyCanvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        lobbyCanvasObj.AddComponent<GraphicRaycaster>();
        Font font = GetBuiltInFont();
        Transform root = lobbyCanvasObj.transform;

        // 1. TOP-LEFT: PROFILE CARD
        RectTransform profileCard = CreatePanel("ProfileCard", root,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(36f, -32f), new Vector2(450f, 108f),
            new Color(0.08f, 0.11f, 0.15f, 0.92f));

        Button profileBtn = profileCard.gameObject.AddComponent<Button>();
        profileBtn.targetGraphic = profileCard.GetComponent<Image>();
        profileBtn.onClick.AddListener(ToggleProfileModal);

        RectTransform avatarFrame = CreatePanel("AvatarFrame", profileCard,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(14f, 0f), new Vector2(80f, 80f),
            new Color(0.95f, 0.72f, 0.14f, 1f));

        RectTransform avatarInner = CreatePanel("AvatarInner", avatarFrame,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(70f, 70f),
            new Color(0.16f, 0.22f, 0.30f, 1f));

        CreateText("AvatarIcon", avatarInner, font, 28, FontStyle.Bold,
            new Color(0.95f, 0.78f, 0.20f), TextAnchor.MiddleCenter, "LVL\n" + playerLevel);

        Text nameTxt = CreateText("PlayerName", profileCard, font, 26, FontStyle.Bold,
            Color.white, TextAnchor.MiddleLeft, playerName);
        SetRect(nameTxt.rectTransform, new Vector2(0f, 0.52f), new Vector2(1f, 0.92f), new Vector2(110f, 0f), new Vector2(-16f, 0f));

        Text rankTxt = CreateText("RankTitle", profileCard, font, 18, FontStyle.Bold,
            new Color(1f, 0.78f, 0.18f), TextAnchor.MiddleLeft, "RANK: " + rankTitle + "   [CLICK PROFILE]");
        SetRect(rankTxt.rectTransform, new Vector2(0f, 0.24f), new Vector2(1f, 0.54f), new Vector2(110f, 0f), new Vector2(-16f, 0f));

        RectTransform xpBarBg = CreatePanel("XPBarBg", profileCard,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(46f, 12f), new Vector2(-126f, 8f),
            new Color(1f, 1f, 1f, 0.18f));

        CreatePanel("XPBarFill", xpBarBg,
            new Vector2(0f, 0f), new Vector2(0.76f, 1f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero,
            new Color(0.25f, 0.85f, 1f, 0.95f));

        // 2. TOP-CENTER: GAME TITLE BANNER & DRAG HINT
        Text titleBanner = CreateText("GameTitle", root, font, 34, FontStyle.Bold,
            new Color(1f, 0.84f, 0.28f), TextAnchor.MiddleCenter, gameTitleBanner);
        titleBanner.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        titleBanner.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        titleBanner.rectTransform.pivot = new Vector2(0.5f, 1f);
        titleBanner.rectTransform.anchoredPosition = new Vector2(0f, -34f);
        titleBanner.rectTransform.sizeDelta = new Vector2(760f, 48f);

        Text dragHint = CreateText("DragHint", root, font, 18, FontStyle.Italic,
            new Color(0.80f, 0.86f, 0.92f, 0.75f), TextAnchor.MiddleCenter,
            "< Drag Left/Right to Rotate Character >");
        dragHint.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        dragHint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        dragHint.rectTransform.pivot = new Vector2(0.5f, 0f);
        dragHint.rectTransform.anchoredPosition = new Vector2(0f, 42f);
        dragHint.rectTransform.sizeDelta = new Vector2(500f, 36f);

        // 3. TOP-RIGHT: CURRENCY BAR (BP & UC) + CONTROLS & QUIT BUTTONS
        RectTransform bpBadge = CreatePanel("BPBadge", root,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-430f, -36f), new Vector2(180f, 48f),
            new Color(0.08f, 0.11f, 0.15f, 0.90f));
        CreateText("BPText", bpBadge, font, 20, FontStyle.Bold,
            new Color(1f, 0.82f, 0.22f), TextAnchor.MiddleCenter, $"BP: {battlePoints:N0}");

        RectTransform ucBadge = CreatePanel("UCBadge", root,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-235f, -36f), new Vector2(165f, 48f),
            new Color(0.08f, 0.11f, 0.15f, 0.90f));
        CreateText("UCText", ucBadge, font, 20, FontStyle.Bold,
            new Color(0.35f, 0.88f, 1f), TextAnchor.MiddleCenter, $"UC: {unknownCash}");

        CreateMenuButton("ControlsBtn", root, font, "CONTROLS", 20,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-105f, -36f), new Vector2(115f, 48f),
            new Color(0.18f, 0.24f, 0.32f, 0.95f), Color.white, ToggleControlsModal);

        CreateMenuButton("QuitBtn", root, font, "X", 22,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -36f), new Vector2(54f, 48f),
            new Color(0.75f, 0.18f, 0.18f, 0.95f), Color.white, OnClickQuitGame);

        // 4. LEFT SIDEBAR: QUICK TABS (PROFILE / CONTROLS)
        CreateMenuButton("SidebarProfileBtn", root, font, "CAREER STATS", 20,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(36f, 60f), new Vector2(210f, 52f),
            new Color(0.12f, 0.16f, 0.22f, 0.90f), new Color(0.95f, 0.80f, 0.25f), ToggleProfileModal);

        CreateMenuButton("SidebarControlsBtn", root, font, "HOW TO PLAY", 20,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(36f, -6f), new Vector2(210f, 52f),
            new Color(0.12f, 0.16f, 0.22f, 0.90f), Color.white, ToggleControlsModal);

        // 5. BOTTOM-LEFT: MAP SELECTOR & BIG YELLOW "START" BUTTON
        RectTransform modeCard = CreatePanel("ModeSelectorCard", root,
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(36f, 134f), new Vector2(440f, 64f),
            new Color(0.08f, 0.11f, 0.15f, 0.92f));

        Button modeBtn = modeCard.gameObject.AddComponent<Button>();
        modeBtn.targetGraphic = modeCard.GetComponent<Image>();
        modeBtn.onClick.AddListener(OnClickCycleMode);

        modeButtonLabel = CreateText("ModeLabel", modeCard, font, 19, FontStyle.Bold,
            new Color(0.85f, 0.92f, 1f), TextAnchor.MiddleCenter, modeNames[selectedModeIndex]);

        Button startBtn = CreateMenuButton("StartMatchButton", root, font, "START", 40,
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(36f, 36f), new Vector2(440f, 86f),
            new Color(0.98f, 0.73f, 0.08f, 1f), new Color(0.08f, 0.08f, 0.08f, 1f), OnClickStartMatch);

        Text startSub = CreateText("StartSubText", startBtn.transform, font, 15, FontStyle.Bold,
            new Color(0.15f, 0.15f, 0.15f, 0.85f), TextAnchor.LowerCenter, "CLICK OR PRESS [ENTER] TO DEPLOY");
        SetRect(startSub.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 6f), new Vector2(0f, -6f));

        // 6. POPUP MODALS
        BuildProfileModal(root, font);
        BuildControlsModal(root, font);
    }

    void BuildProfileModal(Transform root, Font font)
    {
        profileModalPanel = CreatePanel("ProfileModal", root,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(720f, 460f),
            new Color(0.06f, 0.09f, 0.13f, 0.96f)).gameObject;

        CreateText("ModalHeader", profileModalPanel.transform, font, 30, FontStyle.Bold,
            new Color(1f, 0.78f, 0.18f), TextAnchor.UpperCenter,
            $"\n{playerName}  —  SEASON CAREER PROFILE");

        string statsBody =
            $"CURRENT TIER:   {rankTitle} (3,840 Rating)\n" +
            $"ACCOUNT LEVEL:   Level {playerLevel}\n\n" +
            "MATCHES PLAYED:   48\n" +
            "CHICKEN DINNERS (WINS):   14   (29.1% Win Rate)\n" +
            "TOP 10 FINISHES:   39   (81.2%)\n" +
            "TOTAL ELIMINATIONS:   164\n" +
            "K/D RATIO:   4.82\n" +
            "HEADSHOT ACCURACY:   31.4%\n" +
            "LONGEST SNIPER KNOCK:   246 m";

        Text bodyTxt = CreateText("StatsBody", profileModalPanel.transform, font, 21, FontStyle.Normal,
            Color.white, TextAnchor.MiddleLeft, statsBody);
        SetRect(bodyTxt.rectTransform, new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.80f), Vector2.zero, Vector2.zero);

        CreateMenuButton("CloseProfileBtn", profileModalPanel.transform, font, "CLOSE", 22,
            new Vector2(0.5f, 0.0f), new Vector2(0.5f, 0.0f), new Vector2(0f, 22f), new Vector2(180f, 46f),
            new Color(0.95f, 0.72f, 0.12f, 1f), Color.black, ToggleProfileModal);

        profileModalPanel.SetActive(false);
    }

    void BuildControlsModal(Transform root, Font font)
    {
        controlsModalPanel = CreatePanel("ControlsModal", root,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(740f, 480f),
            new Color(0.06f, 0.09f, 0.13f, 0.96f)).gameObject;

        CreateText("ControlsHeader", controlsModalPanel.transform, font, 30, FontStyle.Bold,
            new Color(0.30f, 0.88f, 1f), TextAnchor.UpperCenter,
            "\nCOMBAT & DROP CONTROLS");

        string controlsBody =
            "AIR DROP SEQUENCE:\n" +
            "  •  [F] :   Jump from C-130 Transport Plane\n" +
            "  •  [SPACE] :   Open Parachute (Auto-opens at 60m)\n" +
            "  •  [W A S D] + Mouse :   Steer in Freefall & Glide\n\n" +
            "GROUND COMBAT:\n" +
            "  •  [W A S D] / [SPACE] / [C] :   Move / Jump / Crouch\n" +
            "  •  [Left Click] / [Right Click] :   Fire / Scope Zoom\n" +
            "  •  [1 - 4] or [Q / E] or Scroll :   Switch Weapons\n" +
            "  •  [R] :   Reload Magazine\n" +
            "  •  [TAB] :   Open Backpack Inventory\n" +
            "  •  [H] :   Quick-Use Medkit / Bandage\n" +
            "  •  [G] :   Drop Current Weapon";

        Text bodyTxt = CreateText("ControlsBody", controlsModalPanel.transform, font, 20, FontStyle.Normal,
            Color.white, TextAnchor.MiddleLeft, controlsBody);
        SetRect(bodyTxt.rectTransform, new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.82f), Vector2.zero, Vector2.zero);

        CreateMenuButton("CloseControlsBtn", controlsModalPanel.transform, font, "GOT IT", 22,
            new Vector2(0.5f, 0.0f), new Vector2(0.5f, 0.0f), new Vector2(0f, 22f), new Vector2(180f, 46f),
            new Color(0.30f, 0.88f, 1f, 1f), Color.black, ToggleControlsModal);

        controlsModalPanel.SetActive(false);
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    void SetPlayerGameplayEnabled(bool state)
    {
        if (playerTransform == null) return;

        CharacterController cc = playerTransform.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = state;

        PlayerMovement pm = playerTransform.GetComponent<PlayerMovement>();
        if (pm != null) pm.enabled = state;

        WeaponSwitcher ws = playerTransform.GetComponent<WeaponSwitcher>();
        if (ws != null) ws.enabled = state;

        PlayerInventory inv = playerTransform.GetComponent<PlayerInventory>();
        if (inv != null) inv.enabled = state;

        ScopeController sc = playerTransform.GetComponent<ScopeController>();
        if (sc != null) sc.enabled = state;

        Gun[] guns = playerTransform.GetComponentsInChildren<Gun>(true);
        foreach (Gun g in guns)
        {
            if (g != null) g.enabled = state;
        }
    }

    void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
    }

    RectTransform CreatePanel(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta, Color bgColor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        Image img = go.AddComponent<Image>();
        img.color = bgColor;

        return rt;
    }

    Button CreateMenuButton(string name, Transform parent, Font font, string label, int fontSize,
        Vector2 aMin, Vector2 aMax, Vector2 anchoredPos, Vector2 sizeDelta,
        Color bgColor, Color textColor, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rt = CreatePanel(name, parent, aMin, aMax, aMin, anchoredPos, sizeDelta, bgColor);
        Button btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = rt.GetComponent<Image>();
        btn.onClick.AddListener(onClick);

        CreateText("Label", rt, font, fontSize, FontStyle.Bold, textColor, TextAnchor.MiddleCenter, label);
        return btn;
    }

    Text CreateText(string name, Transform parent, Font font, int fontSize, FontStyle style,
        Color color, TextAnchor align, string content)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Text txt = go.AddComponent<Text>();
        txt.font = font;
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.color = color;
        txt.alignment = align;
        txt.text = content;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.raycastTarget = false;

        Shadow sh = go.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.8f);
        sh.effectDistance = new Vector2(1.5f, -1.5f);

        return txt;
    }

    void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
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

    Font GetBuiltInFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 22);
        return f;
    }

    void OnDestroy()
    {
        RestoreInGameHUDAfterLobby();

        for (int i = 0; i < createdMaterials.Count; i++)
        {
            if (createdMaterials[i] != null) Destroy(createdMaterials[i]);
        }
        createdMaterials.Clear();
    }
}