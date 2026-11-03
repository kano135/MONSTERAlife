using System;
using System.Linq;
using UnityEngine;

// 비닐하우스: 반투명 비닐 벽(앞쪽 문으로 걸어서 드나든다, 화면 전환 없음) + 안쪽 밭 + 장치 3개.
// 습도·온도·빛은 장치가 맞춰 주고, 장치가 고장 나면 그 값이 점점 나빠진다(꽃이 느리게 자란다).
public class Greenhouse : MonoBehaviour
{
    public static Greenhouse Instance { get; private set; }

    public const float Width = 14f, Depth = 16f, WallHeight = 3.2f, RoofHeight = 5f, DoorWidth = 2.6f;
    // 장치 하나가 평균 이 날수마다 한 번 고장 난다. 처음 GraceDays 일 동안은 고장 안 남.
    public const float DaysBetweenBreakdowns = 6f;
    public const int GraceDays = 2;
    // 값이 0→1 로 다 바뀌는 데 걸리는 실제 초
    const float SecondsToChange = 120f;

    static readonly Color Vinyl = new Color(0.92f, 0.97f, 1f, 0.22f);
    static readonly Color Frame = new Color(0.95f, 0.96f, 0.97f);
    static readonly Color FloorColor = new Color(0.72f, 0.66f, 0.56f);
    static readonly Color SoilColor = new Color(0.38f, 0.26f, 0.18f);

    public GreenhouseDevice[] Devices { get; private set; }
    public float Humidity => values[(int)GreenhouseDevice.Kind.Humidifier];
    public float Temperature => values[(int)GreenhouseDevice.Kind.Heater];
    public float Light => values[(int)GreenhouseDevice.Kind.Lamp];
    public event Action<GreenhouseDevice> Broke;

    static readonly float[] Ambient = { Progression.AmbientHumidity, Progression.AmbientTemperature, 0.7f };
    readonly float[] values = (float[])Ambient.Clone();
    Bounds area;

    public bool Contains(Vector3 p) => area.Contains(new Vector3(p.x, area.center.y, p.z));

    void Update()
    {
        var gm = GameManager.Instance;
        if (!gm || gm.IsGameOver) return;
        float dt = Time.deltaTime;
        foreach (var d in Devices)
        {
            int i = (int)d.kind;
            // 아직 안 만든 장치는 안 보이고, 그 값은 바깥 날씨를 따라간다
            bool installed = d.Installed;
            if (d.gameObject.activeSelf != installed) d.gameObject.SetActive(installed);
            values[i] = Mathf.MoveTowards(values[i], installed ? d.Target : Ambient[i], dt / SecondsToChange);
            if (installed && !d.Broken && gm.Day > GraceDays && UnityEngine.Random.value < dt / (DaysBetweenBreakdowns * Progression.RealSecondsPerDay))
            {
                d.Break();
                Broke?.Invoke(d);
            }
        }
    }

    public GreenhouseSave ToSave() => new GreenhouseSave
    {
        values = values.ToArray(),
        levels = Devices.Select(d => d.Level).ToArray(),
        broken = Devices.Select(d => d.Broken).ToArray(),
    };

    public void FromSave(GreenhouseSave s)
    {
        if (s == null || s.levels == null || s.levels.Length != Devices.Length) return;
        for (int i = 0; i < values.Length && i < s.values.Length; i++) values[i] = s.values[i];
        for (int i = 0; i < Devices.Length; i++) Devices[i].Load(s.levels[i], s.broken[i]);
    }

    // ───────── 짓기 ─────────

    public static Greenhouse Build(Vector3 center)
    {
        var gh = new GameObject("Greenhouse").AddComponent<Greenhouse>();
        Instance = gh;
        var t = gh.transform;
        t.position = center;
        gh.area = new Bounds(center + Vector3.up * 5f, new Vector3(Width, 10f, Depth));

        var vinyl = ArtLibrary.Transparent(null, Vinyl);
        var frame = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Frame };

        Box(t, "Floor", new Vector3(0, 0.05f, 0), new Vector3(Width, 0.1f, Depth), new Material(frame) { color = FloorColor }, false);

        float hx = Width / 2, hz = Depth / 2;
        // 벽: 뒤·왼·오른쪽은 통째로, 앞쪽은 가운데 문을 비우고 양옆만
        Box(t, "Wall_Back", new Vector3(0, WallHeight / 2, hz), new Vector3(Width, WallHeight, 0.1f), vinyl, true);
        Box(t, "Wall_Left", new Vector3(-hx, WallHeight / 2, 0), new Vector3(0.1f, WallHeight, Depth), vinyl, true);
        Box(t, "Wall_Right", new Vector3(hx, WallHeight / 2, 0), new Vector3(0.1f, WallHeight, Depth), vinyl, true);
        float side = (Width - DoorWidth) / 2;
        Box(t, "Wall_FrontL", new Vector3(-hx + side / 2, WallHeight / 2, -hz), new Vector3(side, WallHeight, 0.1f), vinyl, true);
        Box(t, "Wall_FrontR", new Vector3(hx - side / 2, WallHeight / 2, -hz), new Vector3(side, WallHeight, 0.1f), vinyl, true);
        Box(t, "Door_Top", new Vector3(0, WallHeight - 0.4f, -hz), new Vector3(DoorWidth, 0.8f, 0.1f), vinyl, false);

        // 지붕: 박공(ㅅ자) 비닐 두 장
        float rise = RoofHeight - WallHeight;
        float slope = Mathf.Sqrt(hx * hx + rise * rise);
        float angle = Mathf.Atan2(rise, hx) * Mathf.Rad2Deg;
        Box(t, "Roof_L", new Vector3(-hx / 2, WallHeight + rise / 2, 0), new Vector3(slope, 0.08f, Depth), vinyl, false).localRotation = Quaternion.Euler(0, 0, angle);
        Box(t, "Roof_R", new Vector3(hx / 2, WallHeight + rise / 2, 0), new Vector3(slope, 0.08f, Depth), vinyl, false).localRotation = Quaternion.Euler(0, 0, -angle);

        // 흰 뼈대: 기둥, 아치(지붕선), 용마루, 문틀
        for (float z = -hz; z <= hz + 0.01f; z += Depth / 4)
        {
            foreach (float x in new[] { -hx, hx })
                Box(t, "Post", new Vector3(x, WallHeight / 2, z), new Vector3(0.15f, WallHeight, 0.15f), frame, false);
            Box(t, "Rafter_L", new Vector3(-hx / 2, WallHeight + rise / 2, z), new Vector3(slope, 0.15f, 0.15f), frame, false).localRotation = Quaternion.Euler(0, 0, angle);
            Box(t, "Rafter_R", new Vector3(hx / 2, WallHeight + rise / 2, z), new Vector3(slope, 0.15f, 0.15f), frame, false).localRotation = Quaternion.Euler(0, 0, -angle);
        }
        Box(t, "Ridge", new Vector3(0, RoofHeight, 0), new Vector3(0.15f, 0.15f, Depth), frame, false);
        foreach (float x in new[] { -DoorWidth / 2, DoorWidth / 2 })
            Box(t, "DoorFrame", new Vector3(x, WallHeight / 2, -hz), new Vector3(0.15f, WallHeight, 0.15f), frame, false);

        // 장치: 뒤쪽 벽을 따라 가습기·난방기·조명. 가습기·난방기는 설계키트로 만들어야 생긴다.
        gh.Devices = new[]
        {
            Device(t, GreenhouseDevice.Kind.Humidifier, new Vector3(-4f, 0, hz - 1.5f), "Props/sprinkler", 1.7f, new Color(0.5f, 0.8f, 0.95f), Progression.Humidifier),
            Device(t, GreenhouseDevice.Kind.Heater, new Vector3(0, 0, hz - 1.5f), "Props/thermostat", 1.3f, new Color(1f, 0.55f, 0.35f), Progression.Heater),
            Device(t, GreenhouseDevice.Kind.Lamp, new Vector3(4f, 0, hz - 1.5f), "Props/lamp", 2.4f, new Color(1f, 0.9f, 0.5f), null),
        };
        return gh;
    }

    // 밭 한 칸: 흙 두둑(보이는 것) + 밟고 지나가지 못하는 충돌
    public PlantSpot AddBed(string name, Vector3 localPos, bool locked)
    {
        var root = new GameObject(name);
        root.transform.SetParent(transform, false);
        root.transform.localPosition = localPos;
        var soilMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = SoilColor };
        Box(root.transform, "Soil", Vector3.up * PlantSpot.SoilTop / 2, new Vector3(1.9f, PlantSpot.SoilTop, 1.9f), soilMat, false);
        var block = root.AddComponent<CapsuleCollider>();
        block.radius = 0.6f;
        block.height = 1.2f;
        block.center = Vector3.up * 0.6f;
        var spot = root.AddComponent<PlantSpot>();
        spot.locked = locked;
        return spot;
    }

    static GreenhouseDevice Device(Transform parent, GreenhouseDevice.Kind kind, Vector3 pos, string art, float height, Color fallback, string requiredUpgrade)
    {
        var go = new GameObject(kind.ToString());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var d = go.AddComponent<GreenhouseDevice>();
        d.kind = kind;
        d.requiredUpgrade = requiredUpgrade;
        var box = go.AddComponent<BoxCollider>();
        box.center = Vector3.up * 0.6f;
        box.size = new Vector3(1f, 1.2f, 1f);

        SpriteRenderer sr = null;
        var sprite = ArtLibrary.World(art);
        if (sprite) sr = Billboard.CreateByHeight(go.transform, sprite, height, Color.white).Renderer;
        else PrototypeBootstrap.Part(go.transform, PrimitiveType.Cube, Vector3.up * 0.6f, new Vector3(0.9f, 1.2f, 0.9f), fallback);

        var alarm = PrototypeBootstrap.Part(go.transform, PrimitiveType.Sphere, Vector3.up * (height + 0.25f), Vector3.one * 0.35f, new Color(1f, 0.15f, 0.1f));
        alarm.name = "Alarm";
        d.SetVisual(sr, alarm);
        return d;
    }

    static Transform Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool solid)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!solid) Destroy(go.GetComponent<Collider>());
        return go.transform;
    }
}

[Serializable]
public class GreenhouseSave
{
    public float[] values;
    public int[] levels;
    public bool[] broken;
}
