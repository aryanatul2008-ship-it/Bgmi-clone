using UnityEngine;

/// Muzzle flash, bullet tracer and hit puffs for one weapon. Add it to each weapon object (Rifle, SMG, Sniper).
/// Everything is built from code with built-in shapes and the "Sprites/Default" shader (always included in builds),
/// so there is nothing to import. Gun.cs calls Fire() every time a shot goes off.
public class GunEffects : MonoBehaviour
{
    [Header("Where shots visually start (optional)")]
    public Transform muzzle;                                          // later: the tip of the gun model
    public Vector3 fallbackOffset = new Vector3(0.4f, 0.4f, 0.8f);    // right, up, forward from the Player until a gun model exists

    [Header("Tracer")]
    public Color tracerColor = new Color(1f, 0.85f, 0.4f, 1f);
    public float tracerWidth = 0.04f;
    public float tracerTime = 0.06f;

    [Header("Muzzle flash")]
    public Color flashColor = new Color(1f, 0.75f, 0.2f, 1f);
    public float flashSize = 0.25f;
    public float flashTime = 0.05f;
    public bool useFlashLight = false;       // a real light flickering 15x per second can cost FPS on fast guns; the blob alone looks fine
    public float flashLightIntensity = 3f;

    [Header("Hit puffs")]
    public Color enemyHitColor = new Color(0.8f, 0.05f, 0.05f, 1f);   // red when you hit a character
    public Color surfaceHitColor = new Color(0.9f, 0.8f, 0.5f, 1f);   // sparks / dust on walls, trees, ground
    public float enemyHitSize = 0.45f;
    public float surfaceHitSize = 0.3f;
    public float hitTime = 0.2f;

    class Puff
    {
        public Transform t;
        public Renderer r;
        public Material m;
        public float start, life, size;
        public Color color;
        public bool active;
    }

    const int TracerCount = 6;
    const int PuffCount = 12;

    static Transform holder;          // scene object that keeps all effect objects tidy
    static Shader shader;

    LineRenderer[] tracers;
    float[] tracerEnd;
    int tracerIndex;

    Puff[] puffs;
    int puffIndex;

    Puff flash;
    Light flashLight;
    float flashEnd;

    void Awake()
    {
        if (holder == null) holder = new GameObject("Effects").transform;
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        tracers = new LineRenderer[TracerCount];
        tracerEnd = new float[TracerCount];
        for (int i = 0; i < TracerCount; i++) tracers[i] = MakeTracer();

        puffs = new Puff[PuffCount];
        for (int i = 0; i < PuffCount; i++) puffs[i] = MakePuff("HitPuff");
        flash = MakePuff("MuzzleFlash");

        if (useFlashLight)
        {
            GameObject lg = new GameObject("MuzzleLight");
            lg.transform.SetParent(holder, false);
            flashLight = lg.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.range = 8f;
            flashLight.shadows = LightShadows.None;
            flashLight.enabled = false;
        }
    }

    // ------------------------------------------------------------ called by Gun.cs

    public void Fire(Vector3 endPoint, Vector3 normal, bool hitSomething, bool hitCharacter)
    {
        Vector3 start = MuzzlePosition();

        // Tracer
        int ti = tracerIndex;
        tracerIndex = (tracerIndex + 1) % TracerCount;
        LineRenderer lr = tracers[ti];
        lr.startColor = tracerColor;
        lr.endColor = new Color(tracerColor.r, tracerColor.g, tracerColor.b, 0.2f);
        lr.startWidth = tracerWidth;
        lr.endWidth = tracerWidth * 0.5f;
        lr.SetPosition(0, start);
        lr.SetPosition(1, endPoint);
        lr.enabled = true;
        tracerEnd[ti] = Time.time + tracerTime;

        // Muzzle flash (blob + short light)
        Show(flash, start, flashColor, flashSize, flashTime);
        if (flashLight != null)
        {
            flashLight.transform.position = start;
            flashLight.color = flashColor;
            flashLight.intensity = flashLightIntensity;
            flashLight.enabled = true;
            flashEnd = Time.time + flashTime;
        }

        // Hit puff
        if (hitSomething)
        {
            Puff p = puffs[puffIndex];
            puffIndex = (puffIndex + 1) % PuffCount;
            Show(p, endPoint + normal * 0.05f,
                 hitCharacter ? enemyHitColor : surfaceHitColor,
                 hitCharacter ? enemyHitSize : surfaceHitSize, hitTime);
        }
    }

    // ------------------------------------------------------------ internals

    Vector3 MuzzlePosition()
    {
        if (muzzle != null) return muzzle.position;
        Transform r = transform.root;
        return r.position + r.right * fallbackOffset.x + Vector3.up * fallbackOffset.y + r.forward * fallbackOffset.z;
    }

    void Update()
    {
        float now = Time.time;

        for (int i = 0; i < TracerCount; i++)
            if (tracers[i].enabled && now >= tracerEnd[i]) tracers[i].enabled = false;

        if (flashLight != null && flashLight.enabled && now >= flashEnd) flashLight.enabled = false;

        Animate(flash, now);
        for (int i = 0; i < PuffCount; i++) Animate(puffs[i], now);
    }

    void OnDisable() // weapon switched away: hide everything straight away
    {
        if (tracers == null) return;
        for (int i = 0; i < TracerCount; i++)
            if (tracers[i] != null) tracers[i].enabled = false;
        if (flashLight != null) flashLight.enabled = false;
        if (flash != null && flash.r != null) { flash.active = false; flash.r.enabled = false; }
        for (int i = 0; i < PuffCount; i++)
            if (puffs[i] != null && puffs[i].r != null) { puffs[i].active = false; puffs[i].r.enabled = false; }
    }

    void Show(Puff p, Vector3 pos, Color c, float size, float life)
    {
        p.t.position = pos;
        p.start = Time.time;
        p.life = life;
        p.size = size;
        p.color = c;
        p.active = true;
        p.r.enabled = true;
        p.t.localScale = Vector3.one * size * 0.4f;
        p.m.color = c;
    }

    void Animate(Puff p, float now)
    {
        if (!p.active) return;
        float t = (now - p.start) / p.life;
        if (t >= 1f) { p.active = false; p.r.enabled = false; return; }
        p.t.localScale = Vector3.one * Mathf.Lerp(p.size * 0.4f, p.size, t); // grows...
        Color c = p.color; c.a = 1f - t;                                      // ...while fading out
        p.m.color = c;
    }

    LineRenderer MakeTracer()
    {
        GameObject go = new GameObject("Tracer");
        go.transform.SetParent(holder, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.material = new Material(shader);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.enabled = false;
        return lr;
    }

    Puff MakePuff(string objName)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = objName;
        Destroy(go.GetComponent<Collider>()); // effects must never block bullets or the player
        go.transform.SetParent(holder, false);

        Puff p = new Puff();
        p.t = go.transform;
        p.r = go.GetComponent<Renderer>();
        p.m = new Material(shader);
        p.r.material = p.m;
        p.r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        p.r.receiveShadows = false;
        p.r.enabled = false;
        return p;
    }
}