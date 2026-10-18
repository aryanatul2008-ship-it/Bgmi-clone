using UnityEngine;
using UnityEngine.UI;

/// Builds the whole HUD from code, so there is nothing to drag around:
///  - health bar (bottom-left)
///  - BGMI-style weapon slot panels with ammo (bottom-right), active weapon highlighted
///  - kill counter (top-right)
///  - "YOU DIED" message
/// Just put this on an empty GameObject called "HUD". It finds the Player by itself.
public class GameHUD : MonoBehaviour
{
    [Header("Leave empty to find automatically")]
    public WeaponSwitcher switcher;
    public Health playerHealth;

    Font font;
    RectTransform healthFill;
    Image healthFillImg;
    Text healthText, killsText;
    GameObject deadPanel;

    class Slot { public Image bg; public Text key, name, ammo; }
    Slot[] slots;

    // cached values, so UI is only touched when something changes
    float lastRatio = -1f;
    int lastHp = -1, lastKills = -1;
    bool[] lastOwned, lastActive, lastReloading;
    int[] lastAmmo, lastReserve;

    static readonly Color activeBg = new Color(1f, 0.72f, 0.1f, 0.92f);
    static readonly Color ownedBg = new Color(0.08f, 0.08f, 0.08f, 0.7f);
    static readonly Color emptyBg = new Color(0.04f, 0.04f, 0.04f, 0.35f);

    void Start()
    {
        if (switcher == null) switcher = FindAnyObjectByType<WeaponSwitcher>();
        if (playerHealth == null && switcher != null) playerHealth = switcher.GetComponent<Health>();
        if (switcher == null || playerHealth == null)
        {
            Debug.LogError("GameHUD: couldn't find the Player. It needs WeaponSwitcher and Health on the same object.");
            enabled = false;
            return;
        }

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);

        BuildUI();
    }

    // ---------------------------------------------------------------- building

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("HUD_Canvas");
        canvasGO.transform.SetParent(transform, false);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        Transform root = canvasGO.transform;

        // Health bar (bottom-left)
        RectTransform hb = NewRect("HealthBar", root, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(40, 40), new Vector2(460, 36));
        NewImage(hb, new Color(0, 0, 0, 0.55f));
        healthFill = NewRect("Fill", hb, Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
        healthFill.offsetMin = new Vector2(4, 4);
        healthFill.offsetMax = new Vector2(-4, -4);
        healthFillImg = NewImage(healthFill, Color.green);
        healthText = NewText(hb, "HPText", 22, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.zero);

        // Kill counter (top-right)
        RectTransform kr = NewRect("Kills", root, Vector2.one, Vector2.one, Vector2.one, new Vector2(-40, -40), new Vector2(320, 50));
        killsText = NewText(kr, "KillsText", 34, TextAnchor.MiddleRight, Color.white, Vector2.zero, Vector2.zero);

        // Weapon slot panels (bottom-right), slot 1 on top
        int n = switcher.guns.Length;
        slots = new Slot[n];
        lastOwned = new bool[n]; lastActive = new bool[n]; lastReloading = new bool[n];
        lastAmmo = new int[n]; lastReserve = new int[n];
        for (int i = 0; i < n; i++)
        {
            float y = 40 + (n - 1 - i) * 78;
            RectTransform panel = NewRect("Slot" + (i + 1), root, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, y), new Vector2(320, 70));
            Slot s = new Slot();
            s.bg = NewImage(panel, emptyBg);
            s.key = NewText(panel, "Key", 30, TextAnchor.MiddleLeft, Color.white, new Vector2(14, 0), Vector2.zero);
            s.name = NewText(panel, "Name", 24, TextAnchor.MiddleLeft, Color.white, new Vector2(60, 0), Vector2.zero);
            s.ammo = NewText(panel, "Ammo", 26, TextAnchor.MiddleRight, Color.white, Vector2.zero, new Vector2(-14, 0));
            s.key.text = (i + 1).ToString();
            slots[i] = s;
            lastAmmo[i] = -99; lastReserve[i] = -99;
        }

        // Death message
        RectTransform dr = NewRect("Dead", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 200));
        Text deadText = NewText(dr, "DeadText", 90, TextAnchor.MiddleCenter, new Color(0.9f, 0.1f, 0.1f), Vector2.zero, Vector2.zero);
        deadText.text = "YOU DIED";
        deadPanel = dr.gameObject;
        deadPanel.SetActive(false);
    }

    RectTransform NewRect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    Image NewImage(RectTransform rt, Color c)
    {
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    Text NewText(Transform parent, string name, int size, TextAnchor anchor, Color color, Vector2 offMin, Vector2 offMax)
    {
        RectTransform rt = NewRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
        Text t = rt.gameObject.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = anchor;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        rt.gameObject.AddComponent<Shadow>(); // keeps text readable on any background
        return t;
    }

    // ---------------------------------------------------------------- updating

    void Update()
    {
        UpdateHealth();
        UpdateKills();
        for (int i = 0; i < slots.Length; i++) UpdateSlot(i);

        bool dead = playerHealth.IsDead;
        if (deadPanel.activeSelf != dead) deadPanel.SetActive(dead);
    }

    void UpdateHealth()
    {
        float ratio = Mathf.Clamp01(playerHealth.Current / playerHealth.maxHealth);
        if (Mathf.Abs(ratio - lastRatio) > 0.001f)
        {
            lastRatio = ratio;
            healthFill.anchorMax = new Vector2(ratio, 1f);
            healthFill.offsetMin = new Vector2(4, 4);
            healthFill.offsetMax = new Vector2(-4, -4);
            healthFillImg.enabled = ratio > 0.01f;
            healthFillImg.color = Color.Lerp(new Color(0.85f, 0.1f, 0.1f), new Color(0.2f, 0.85f, 0.2f), ratio);
        }

        int hp = Mathf.CeilToInt(playerHealth.Current);
        if (hp != lastHp)
        {
            lastHp = hp;
            healthText.text = hp + " / " + Mathf.CeilToInt(playerHealth.maxHealth);
        }
    }

    void UpdateKills()
    {
        if (Health.PlayerKills == lastKills) return;
        lastKills = Health.PlayerKills;
        killsText.text = "KILLS  " + lastKills;
    }

    void UpdateSlot(int i)
    {
        Gun gun = switcher.guns[i];
        bool owned = gun != null && i < switcher.owned.Length && switcher.owned[i];
        bool active = owned && switcher.CurrentGun == gun;
        int ammo = owned ? gun.CurrentAmmo : -1;
        int reserve = owned ? gun.ReserveAmmo : -1;
        bool reloading = owned && gun.IsReloading;

        if (owned == lastOwned[i] && active == lastActive[i] && ammo == lastAmmo[i]
            && reserve == lastReserve[i] && reloading == lastReloading[i]) return;

        lastOwned[i] = owned; lastActive[i] = active; lastAmmo[i] = ammo;
        lastReserve[i] = reserve; lastReloading[i] = reloading;

        Slot s = slots[i];
        s.bg.color = active ? activeBg : (owned ? ownedBg : emptyBg);
        Color tc = active ? Color.black : (owned ? Color.white : new Color(1, 1, 1, 0.35f));
        s.key.color = tc; s.name.color = tc; s.ammo.color = tc;
        s.name.text = owned ? gun.name : "EMPTY";
        s.ammo.text = !owned || gun.melee ? "" : (reloading ? "RELOADING" : ammo + " / " + reserve);
    }
}