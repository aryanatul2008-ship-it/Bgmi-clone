using UnityEngine;
using UnityEngine.UI;

/// Top-down minimap in the top-left corner. Put this on the same "HUD" object as GameHUD (or any empty object).
/// It creates its own camera, render texture and UI, and follows the Player (tag "Player").
/// North (+Z in the world) is always up; the arrow shows where the player is facing.
public class MiniMap : MonoBehaviour
{
    public Transform player;          // leave empty: finds the object tagged Player
    public float viewSize = 40f;      // how much world the map shows (half-width in metres). Bigger = more zoomed out
    public float cameraHeight = 120f; // must be above the tallest tree / building
    public int textureSize = 256;     // lower (128) if you lose FPS
    public float uiSize = 260f;       // size on screen in canvas units

    Camera cam;
    RenderTexture rt;
    RectTransform arrow;

    void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player == null)
        {
            Debug.LogError("MiniMap: no object tagged 'Player' found.");
            enabled = false;
            return;
        }

        // Camera that looks straight down and draws into a texture
        rt = new RenderTexture(textureSize, textureSize, 16);
        GameObject camGO = new GameObject("MiniMapCamera");
        camGO.transform.SetParent(transform, false);
        camGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = viewSize;
        cam.targetTexture = rt;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.2f, 0.12f);
        cam.nearClipPlane = 1f;
        cam.farClipPlane = cameraHeight * 2f;
        cam.depth = -10f;

        BuildUI();
    }

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("MiniMap_Canvas");
        canvasGO.transform.SetParent(transform, false);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Border
        RectTransform border = NewRect("Border", canvasGO.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                                       new Vector2(40, -40), new Vector2(uiSize + 12, uiSize + 12));
        NewImage(border, new Color(0, 0, 0, 0.75f));

        // Map picture
        RectTransform map = NewRect("Map", border, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                    Vector2.zero, new Vector2(uiSize, uiSize));
        RawImage raw = map.gameObject.AddComponent<RawImage>();
        raw.texture = rt;
        raw.raycastTarget = false;

        // Player marker: a dot with a pointer that rotates with the player
        arrow = NewRect("Arrow", map, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        Vector2.zero, new Vector2(24, 24));
        RectTransform tip = NewRect("Tip", arrow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f),
                                    Vector2.zero, new Vector2(8, 16));
        NewImage(tip, new Color(1f, 0.25f, 0.2f));
        RectTransform dot = NewRect("Dot", arrow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                    Vector2.zero, new Vector2(12, 12));
        NewImage(dot, Color.white);
    }

    RectTransform NewRect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt2 = go.GetComponent<RectTransform>();
        rt2.SetParent(parent, false);
        rt2.anchorMin = aMin;
        rt2.anchorMax = aMax;
        rt2.pivot = pivot;
        rt2.anchoredPosition = pos;
        rt2.sizeDelta = size;
        return rt2;
    }

    void NewImage(RectTransform r, Color c)
    {
        Image img = r.gameObject.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
    }

    void LateUpdate()
    {
        if (player == null || cam == null) return;
        cam.transform.position = new Vector3(player.position.x, player.position.y + cameraHeight, player.position.z);
        arrow.localEulerAngles = new Vector3(0f, 0f, -player.eulerAngles.y); // pointer follows the player's facing
    }

    void OnDestroy()
    {
        if (rt != null) rt.Release();
    }
}