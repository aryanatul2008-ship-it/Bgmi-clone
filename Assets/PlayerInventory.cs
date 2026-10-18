using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// Put on the Player. The backpack:
///  - holds healing items (Bandage +20 HP, Medkit +50 HP) with a capacity limit,
///  - TAB opens the backpack screen (the mouse is freed so you can click USE / DROP; the camera stops turning),
///  - H uses the best healing item, G drops the weapon you are holding,
///  - dropped weapons and items appear on the ground in front of you and can be picked up again (weapons keep their ammo),
///  - shows short messages ("Picked up AKM", "Backpack full"...) near the bottom of the screen.
/// All UI is built from code, so there is nothing to set up.
public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; IsOpen = false; }

    [Header("Backpack")]
    public int bandages = 0;
    public int medkits = 0;
    public int maxBandages = 5;       // backpack capacity
    public int maxMedkits = 2;
    public float bandageHeal = 20f;
    public float medkitHeal = 50f;

    [Header("Keys")]
    public KeyCode inventoryKey = KeyCode.Tab;
    public KeyCode healKey = KeyCode.H;
    public KeyCode dropWeaponKey = KeyCode.G;

    [Header("Leave empty to find automatically")]
    public WeaponSwitcher switcher;
    public Health health;
    public CinemachineCamera vcam;

    Font font;
    GameObject panel;
    RectTransform content;
    Text messageText;
    float messageUntil;

    void Awake() { Instance = this; }

    void Start()
    {
        if (switcher == null) switcher = GetComponent<WeaponSwitcher>();
        if (health == null) health = GetComponent<Health>();
        if (vcam == null) vcam = FindAnyObjectByType<CinemachineCamera>();
        if (switcher == null || health == null)
        {
            Debug.LogError("PlayerInventory: needs WeaponSwitcher and Health on the Player.");
            enabled = false;
            return;
        }

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);
        BuildUI();
    }

    void OnDisable() { if (IsOpen) Close(); }

    void Update()
    {
        if (health.IsDead)
        {
            if (IsOpen) Close();
            return;
        }

        if (Input.GetKeyDown(inventoryKey)) { if (IsOpen) Close(); else Open(); }
        if (Input.GetKeyDown(healKey)) UseBestHeal();
        if (Input.GetKeyDown(dropWeaponKey)) DropWeapon(switcher.CurrentIndex);

        if (messageText != null && messageText.gameObject.activeSelf)
        {
            float left = messageUntil - Time.time;
            if (left <= 0f) messageText.gameObject.SetActive(false);
            else
            {
                Color c = messageText.color;
                c.a = Mathf.Clamp01(left / 0.6f);   // fade out in the last 0.6 s
                messageText.color = c;
            }
        }
    }

    // ------------------------------------------------------------ open / close

    void Open()
    {
        IsOpen = true;
        panel.SetActive(true);
        Refresh();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetCameraInput(false);
    }

    void Close()
    {
        IsOpen = false;
        if (panel != null) panel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SetCameraInput(true);
    }

    // Stops the camera from turning while you move the mouse over the backpack
    void SetCameraInput(bool on)
    {
        if (vcam == null) return;
        foreach (MonoBehaviour mb in vcam.GetComponents<MonoBehaviour>())
            if (mb != null && mb.GetType().Name.Contains("InputAxisController")) mb.enabled = on;
    }

    // ------------------------------------------------------------ items

    public bool AddMedkit(int n)
    {
        if (medkits >= maxMedkits) return false;
        medkits = Mathf.Min(maxMedkits, medkits + n);
        Notify("Medkit +1");
        Refresh();
        return true;
    }

    public bool AddBandage(int n)
    {
        if (bandages >= maxBandages) return false;
        bandages = Mathf.Min(maxBandages, bandages + n);
        Notify("Bandage +1");
        Refresh();
        return true;
    }

    public void UseBandage()
    {
        if (bandages <= 0) { Notify("No bandages"); return; }
        if (health.Current >= health.maxHealth) { Notify("Health is already full"); return; }
        bandages--;
        health.Heal(bandageHeal);
        Notify("Bandage used  +" + bandageHeal + " HP");
        Refresh();
    }

    public void UseMedkit()
    {
        if (medkits <= 0) { Notify("No medkits"); return; }
        if (health.Current >= health.maxHealth) { Notify("Health is already full"); return; }
        medkits--;
        health.Heal(medkitHeal);
        Notify("Medkit used  +" + medkitHeal + " HP");
        Refresh();
    }

    // H key: a medkit for a big wound, otherwise a bandage
    void UseBestHeal()
    {
        float missing = health.maxHealth - health.Current;
        if (missing <= 0.5f) { Notify("Health is already full"); return; }
        if (medkits > 0 && (missing >= medkitHeal * 0.8f || bandages == 0)) UseMedkit();
        else if (bandages > 0) UseBandage();
        else Notify("No healing items");
    }

    public void DropItem(bool medkit)
    {
        if (medkit)
        {
            if (medkits <= 0) return;
            medkits--;
            ItemPickup p = MakeCube("Medkit", DropPoint(), new Vector3(0.45f, 0.45f, 0.45f), new Color(0.2f, 0.85f, 0.3f)).AddComponent<ItemPickup>();
            p.type = ItemPickup.ItemType.Heal;
            Notify("Dropped a medkit");
        }
        else
        {
            if (bandages <= 0) return;
            bandages--;
            ItemPickup p = MakeCube("Bandage", DropPoint(), new Vector3(0.4f, 0.25f, 0.4f), new Color(0.95f, 0.95f, 0.95f)).AddComponent<ItemPickup>();
            p.type = ItemPickup.ItemType.Bandage;
            Notify("Dropped a bandage");
        }
        Refresh();
    }

    public void DropWeapon(int index)
    {
        if (index < 0 || index >= switcher.guns.Length) return;
        Gun g = switcher.guns[index];
        if (g == null || !switcher.IsOwned(index)) return;
        if (g.melee) { Notify("You can't drop the knife"); return; }

        WeaponPickup p = MakeCube(g.name, DropPoint(), new Vector3(0.3f, 0.25f, 1.1f), new Color(0.25f, 0.5f, 1f)).AddComponent<WeaponPickup>();
        p.weaponIndex = index;
        p.bonusAmmo = 0;
        p.magAmmo = g.CurrentAmmo;       // the weapon keeps its ammo
        p.reserveAmmo = g.ReserveAmmo;

        switcher.Remove(index);
        Notify("Dropped " + g.name);
        Refresh();
    }

    Vector3 DropPoint()
    {
        Vector3 p = transform.position + transform.forward * 1.6f;
        if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            p.y = hit.point.y + 0.35f;
        return p;
    }

    static GameObject MakeCube(string label, Vector3 pos, Vector3 scale, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Dropped_" + label;
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().material.color = color;
        return go;
    }

    // ------------------------------------------------------------ messages

    public static void Notify(string msg)
    {
        if (Instance != null) Instance.ShowMessage(msg);
    }

    void ShowMessage(string msg)
    {
        if (messageText == null) return;
        messageText.text = msg;
        Color c = messageText.color;
        c.a = 1f;
        messageText.color = c;
        messageText.gameObject.SetActive(true);
        messageUntil = Time.time + 2.2f;
    }

    // ------------------------------------------------------------ UI (built from code)

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("Inventory_Canvas");
        canvasGO.transform.SetParent(transform, false);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 12;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();     // needed so the buttons can be clicked
        Transform root = canvasGO.transform;

        // Message line near the bottom centre
        RectTransform msg = NewRect("Message", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(900, 50));
        messageText = NewText(msg, "MessageText", 30, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.zero);
        messageText.gameObject.SetActive(false);

        // Backpack panel
        RectTransform p = NewRect("BackpackPanel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660, 640));
        NewImage(p, new Color(0.05f, 0.05f, 0.05f, 0.92f));
        RectTransform title = NewRect("Title", p, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(0, 56));
        NewText(title, "TitleText", 38, TextAnchor.MiddleCenter, new Color(1f, 0.72f, 0.1f), Vector2.zero, Vector2.zero).text = "BACKPACK";
        content = NewRect("Content", p, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -76), new Vector2(0, 540));
        panel = p.gameObject;
        panel.SetActive(false);
    }

    void Refresh()
    {
        if (panel == null || !panel.activeSelf) return;
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);

        float y = 0f;
        AddLabel("WEAPONS", ref y);
        for (int i = 0; i < switcher.guns.Length; i++)
        {
            Gun g = switcher.guns[i];
            if (g == null) continue;
            int slot = i;
            string number = (i + 1) + "   ";
            if (!switcher.IsOwned(i)) { AddRow(ref y, number + g.name + "   (empty)", true); continue; }

            string equipped = switcher.CurrentIndex == i ? "   [in hand]" : "";
            if (g.melee) AddRow(ref y, number + g.name + equipped, false);
            else AddRow(ref y, number + g.name + "   " + g.CurrentAmmo + " / " + g.ReserveAmmo + equipped, false,
                        ("DROP", () => DropWeapon(slot)));
        }

        AddLabel("ITEMS", ref y);
        AddRow(ref y, "Bandage  x" + bandages + "   (+" + bandageHeal + " HP)", bandages == 0,
               ("DROP", () => DropItem(false)), ("USE", UseBandage));
        AddRow(ref y, "Medkit  x" + medkits + "   (+" + medkitHeal + " HP)", medkits == 0,
               ("DROP", () => DropItem(true)), ("USE", UseMedkit));

        AddLabel("TAB close     H heal     G drop weapon", ref y);
    }

    void AddLabel(string text, ref float y)
    {
        RectTransform r = NewRect("Label", content, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(-40, 34));
        Text t = NewText(r, "Text", 22, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.55f), Vector2.zero, Vector2.zero);
        t.text = text;
        y += 38f;
    }

    // buttons are listed right-to-left: the first one sits at the far right
    void AddRow(ref float y, string label, bool dim, params (string text, UnityAction action)[] buttons)
    {
        RectTransform row = NewRect("Row", content, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(-40, 52));
        NewImage(row, new Color(1f, 1f, 1f, dim ? 0.05f : 0.12f));
        Text t = NewText(row, "Text", 26, TextAnchor.MiddleLeft, dim ? new Color(1f, 1f, 1f, 0.4f) : Color.white, new Vector2(14, 0), Vector2.zero);
        t.text = label;

        for (int k = 0; k < buttons.Length; k++)
            MakeButton(row, buttons[k].text, -8f - k * 108f, buttons[k].action);
        y += 58f;
    }

    void MakeButton(RectTransform parent, string label, float xFromRight, UnityAction onClick)
    {
        RectTransform rt = NewRect(label + "Button", parent, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(xFromRight, 0), new Vector2(100, 38));
        Image img = NewImage(rt, new Color(0.95f, 0.7f, 0.1f, 0.95f));
        img.raycastTarget = true;
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(onClick);
        NewText(rt, "Label", 22, TextAnchor.MiddleCenter, Color.black, Vector2.zero, Vector2.zero).text = label;
    }

    static RectTransform NewRect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
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

    static Image NewImage(RectTransform rt, Color c)
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
        rt.gameObject.AddComponent<Shadow>();
        return t;
    }
}