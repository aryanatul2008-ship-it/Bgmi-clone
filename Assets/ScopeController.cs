using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

/// Put on the Player. Hold the RIGHT mouse button to aim down the sights of the weapon you're holding:
///  - zooms the camera (field of view) and lowers the mouse sensitivity,
///  - red-dot weapons show a red dot, sniper weapons show a black scope overlay,
///  - hides the normal crosshair while aiming; the Gun uses a tighter spread while aiming.
/// Aiming is cancelled straight away when you switch weapon, reload, die or free the cursor.
/// The overlays are built from code, so there is nothing to set up except GunScope on each weapon.
public class ScopeController : MonoBehaviour
{
    [Header("Leave empty to find automatically")]
    public WeaponSwitcher switcher;
    public Health playerHealth;
    public CinemachineCamera vcam;
    public GameObject crosshair;      // the old screen-centre crosshair (found by name if empty)

    float baseFov;
    float aim;                        // 0..1
    Gun lastGun;
    GunScope activeScope;
    bool applied;                     // true while the camera is away from its normal settings
    bool crosshairHidden;

    GameObject redDot, sniperOverlay;

    // The Rotation Composer keeps the player at a fixed FRACTION of the screen. When the field of view narrows, that fraction
    // turns into a smaller angle, so the camera would swing sideways and the crosshair would land on a different spot.
    // We scale the composer's screen position with the zoom so the camera keeps pointing at exactly the same place.
    CinemachineRotationComposer composer;
    Vector2 baseScreenPos;

    class GainEntry { public object reader; public FieldInfo gain, legacyGain; public float baseGain, baseLegacy; }
    readonly List<GainEntry> gains = new List<GainEntry>();

    void Start()
    {
        if (switcher == null) switcher = GetComponent<WeaponSwitcher>();
        if (playerHealth == null) playerHealth = GetComponent<Health>();
        if (vcam == null) vcam = FindAnyObjectByType<CinemachineCamera>();
        if (vcam == null || switcher == null)
        {
            Debug.LogError("ScopeController: needs a WeaponSwitcher on the Player and a CinemachineCamera in the scene.");
            enabled = false;
            return;
        }

        baseFov = vcam.Lens.FieldOfView;
        composer = vcam.GetComponent<CinemachineRotationComposer>();
        if (composer != null) baseScreenPos = composer.Composition.ScreenPosition;
        FindCrosshair();
        BuildOverlays();
        CacheSensitivity();
    }

    void Update()
    {
        Gun gun = switcher.CurrentGun;
        if (gun != lastGun)                       // weapon switched: drop the aim immediately
        {
            lastGun = gun;
            if (activeScope != null) activeScope.AimAmount = 0f;
            activeScope = gun != null ? gun.GetComponent<GunScope>() : null;
            aim = 0f;
            Apply(0f);
        }

        bool canAim = activeScope != null && activeScope.type != ScopeType.None && gun != null && !gun.IsReloading
                      && Cursor.lockState == CursorLockMode.Locked
                      && (playerHealth == null || !playerHealth.IsDead);
        bool wantsAim = canAim && Input.GetMouseButton(1);

        float speed = activeScope != null ? activeScope.zoomSpeed : 8f;
        aim = Mathf.MoveTowards(aim, wantsAim ? 1f : 0f, Time.deltaTime * speed);

        if (aim > 0f || applied) Apply(Mathf.SmoothStep(0f, 1f, aim));
    }

    void Apply(float t)
    {
        if (activeScope == null)
        {
            if (applied) Restore();
            return;
        }

        activeScope.AimAmount = t;

        LensSettings lens = vcam.Lens;
        lens.FieldOfView = Mathf.Lerp(baseFov, activeScope.zoomFOV, t);
        vcam.Lens = lens;
        KeepAimPoint(lens.FieldOfView);
        SetSensitivity(Mathf.Lerp(1f, activeScope.sensitivityMultiplier, t));
        applied = t > 0f;
        if (!applied) Restore();

        bool showDot = activeScope.type == ScopeType.RedDot && t > 0.8f;
        bool showSniper = activeScope.type == ScopeType.Sniper && t > 0.9f;
        if (redDot.activeSelf != showDot) redDot.SetActive(showDot);
        if (sniperOverlay.activeSelf != showSniper) sniperOverlay.SetActive(showSniper);

        bool hide = t > 0.3f;
        if (crosshair != null && hide != crosshairHidden)
        {
            crosshairHidden = hide;
            crosshair.SetActive(!hide);
        }
    }

    void Restore()
    {
        applied = false;
        LensSettings lens = vcam.Lens;
        lens.FieldOfView = baseFov;
        vcam.Lens = lens;
        KeepAimPoint(baseFov);
        SetSensitivity(1f);
        if (redDot != null) redDot.SetActive(false);
        if (sniperOverlay != null) sniperOverlay.SetActive(false);
        if (crosshair != null && crosshairHidden) { crosshair.SetActive(true); crosshairHidden = false; }
    }

    void OnDisable()
    {
        if (vcam != null && applied) Restore();
        if (activeScope != null) activeScope.AimAmount = 0f;
    }

    void KeepAimPoint(float currentFov)
    {
        if (composer == null) return;
        float k = Mathf.Tan(baseFov * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(currentFov * 0.5f * Mathf.Deg2Rad);
        ScreenComposerSettings comp = composer.Composition;
        comp.ScreenPosition = baseScreenPos * k;
        composer.Composition = comp;
    }

    // ------------------------------------------------------------ mouse sensitivity (through the Cinemachine input component)

    void CacheSensitivity()
    {
        foreach (MonoBehaviour mb in vcam.GetComponents<MonoBehaviour>())
        {
            if (mb == null || !mb.GetType().Name.Contains("InputAxisController")) continue;
            IEnumerable controllers = GetMember(mb, "Controllers") as IEnumerable;
            if (controllers == null) continue;

            foreach (object ctrl in controllers)
            {
                object reader = GetMember(ctrl, "Input");
                if (reader == null) continue;
                Type rt = reader.GetType();
                FieldInfo g = rt.GetField("Gain");
                FieldInfo lg = rt.GetField("LegacyGain");
                if (g == null || lg == null || g.FieldType != typeof(float) || lg.FieldType != typeof(float)) continue;
                gains.Add(new GainEntry
                {
                    reader = reader,
                    gain = g,
                    legacyGain = lg,
                    baseGain = (float)g.GetValue(reader),
                    baseLegacy = (float)lg.GetValue(reader)
                });
            }
        }
        if (gains.Count == 0)
            Debug.LogWarning("ScopeController: couldn't find the mouse sensitivity settings. Zoom still works, but the mouse speed won't change while aiming.");
    }

    void SetSensitivity(float factor)
    {
        foreach (GainEntry e in gains)
        {
            e.gain.SetValue(e.reader, e.baseGain * factor);
            e.legacyGain.SetValue(e.reader, e.baseLegacy * factor);
        }
    }

    static object GetMember(object o, string name)
    {
        Type t = o.GetType();
        FieldInfo f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (f != null) return f.GetValue(o);
        PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        return p != null ? p.GetValue(o) : null;
    }

    // ------------------------------------------------------------ overlays

    void FindCrosshair()
    {
        if (crosshair != null) return;
        foreach (Image img in FindObjectsByType<Image>(FindObjectsSortMode.None))
            if (img.name.ToLower().Contains("crosshair")) { crosshair = img.gameObject; return; }
        Debug.LogWarning("ScopeController: couldn't find the crosshair by name. Drag it into the 'Crosshair' slot to hide it while aiming.");
    }

    void BuildOverlays()
    {
        GameObject canvasGO = new GameObject("Scope_Canvas");
        canvasGO.transform.SetParent(transform, false);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 11;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;           // canvas height is always 1080 units, whatever the screen shape
        Transform root = canvasGO.transform;

        // Red dot
        RectTransform dot = NewRect("RedDot", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 16));
        Image dotImg = dot.gameObject.AddComponent<Image>();
        dotImg.sprite = MakeCircleSprite(64);
        dotImg.color = new Color(1f, 0.1f, 0.1f, 0.95f);
        dotImg.raycastTarget = false;
        redDot = dot.gameObject;
        redDot.SetActive(false);

        // Sniper scope: black outside a circle, thin crosshair lines inside
        RectTransform full = NewRect("SniperScope", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        full.offsetMin = Vector2.zero;
        full.offsetMax = Vector2.zero;

        RectTransform centre = NewRect("Circle", full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080, 1080));
        Image circleImg = centre.gameObject.AddComponent<Image>();
        circleImg.sprite = MakeHoleSprite(512, 0.92f);
        circleImg.raycastTarget = false;

        RectTransform left = NewRect("BarLeft", full, new Vector2(0, 0), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
        left.offsetMin = Vector2.zero;
        left.offsetMax = new Vector2(-540, 0);
        SolidImage(left, Color.black);

        RectTransform right = NewRect("BarRight", full, new Vector2(0.5f, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
        right.offsetMin = new Vector2(540, 0);
        right.offsetMax = Vector2.zero;
        SolidImage(right, Color.black);

        SolidImage(NewRect("LineH", full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(990, 2)), new Color(0, 0, 0, 0.9f));
        SolidImage(NewRect("LineV", full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2, 990)), new Color(0, 0, 0, 0.9f));
        SolidImage(NewRect("CentreDot", full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5, 5)), new Color(1f, 0.1f, 0.1f, 1f));

        sniperOverlay = full.gameObject;
        sniperOverlay.SetActive(false);
    }

    static RectTransform NewRect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static void SolidImage(RectTransform rt, Color c)
    {
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
    }

    static Sprite MakeCircleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Color32[] px = new Color32[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float a = Mathf.Clamp01((r - d) / 2f);                 // soft 2-pixel edge
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // Black everywhere except a transparent circle in the middle (radius is a fraction of half the square)
    static Sprite MakeHoleSprite(int size, float radius01)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Color32[] px = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half; // 0 centre, 1 edge
                float a = Mathf.Clamp01((d - radius01) * half / 3f + 0.5f);                                    // soft 3-pixel edge
                px[y * size + x] = new Color32(0, 0, 0, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}