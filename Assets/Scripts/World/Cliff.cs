using UnityEngine;

// 맵 왼쪽 끝(-X)의 절벽과 큰 폭포. 단순한 도형으로 형태만 잡는다(나중에 모델로 바꾸기 쉽게).
//   절벽: x = -80 ~ -66, 높이 Height. 앞면(x = -66)에 울퉁불퉁한 바위 조각.
//   폭포: 절벽 가운데(z = 0)에 폭 FallWidth 로 위에서 떨어진다(물이 흐르는 무늬가 움직임). 위에는 강, 아래엔 웅덩이·물보라.
//   동굴: 폭포 바로 뒤 입구(폭 6m, 높이 6m) → 안쪽 방. 폭포는 통과할 수 있어서 걸어 들어간다.
public class Cliff : MonoBehaviour
{
    public static Cliff Instance { get; private set; }

    public const float Edge = -80f;      // 맵 끝(절벽 뒤)
    public const float Face = -66f;      // 절벽 앞면
    public const float Height = 24f;
    public const float Length = 90f;     // z 방향 길이
    public const float FallWidth = 10f;
    const float CaveDepth = 10f, CaveHalfWidth = 6f, CaveHeight = 7f, DoorHalfWidth = 3f, DoorHeight = 6f;
    static readonly Vector3 PoolCenter = new Vector3(Face + 6f, 0, 0);
    const float PoolRadius = 7f;

    static readonly Color[] Stone =
    {
        new Color(0.74f, 0.68f, 0.62f), new Color(0.66f, 0.61f, 0.57f), new Color(0.8f, 0.74f, 0.66f), new Color(0.6f, 0.57f, 0.55f),
    };
    static readonly Color WaterColor = new Color(0.62f, 0.86f, 0.98f, 0.8f);

    Material fallMat, riverMat;
    Transform[] foam;

    // 이 점(+여유)이 절벽·웅덩이에 걸리면 true. 씨앗·자원이 여기 생기지 않게 쓴다.
    public static bool Blocks(Vector3 p, float margin)
    {
        if (p.x < Face + margin) return true;
        var d = p - PoolCenter;
        d.y = 0;
        return d.magnitude < PoolRadius + margin;
    }

    public static Cliff Build()
    {
        var cliff = new GameObject("Cliff").AddComponent<Cliff>();
        Instance = cliff;
        var t = cliff.transform;
        var rng = new System.Random(21);
        float halfL = Length / 2f;
        float depth = Face - Edge;

        // ── 돌 덩어리(부딪힘) ── 동굴 자리만 비우고 큰 상자로 채운다
        Block(t, "Cliff_Left",  new Vector3(Edge, 0, -halfL), new Vector3(Face, Height, -CaveHalfWidth), Stone[0]);
        Block(t, "Cliff_Right", new Vector3(Edge, 0, CaveHalfWidth), new Vector3(Face, Height, halfL), Stone[0]);
        Block(t, "Cliff_AboveCave", new Vector3(Edge, CaveHeight, -CaveHalfWidth), new Vector3(Face, Height, CaveHalfWidth), Stone[0]);
        Block(t, "Cave_Back", new Vector3(Edge, 0, -CaveHalfWidth), new Vector3(Face - CaveDepth, CaveHeight, CaveHalfWidth), Stone[3]);
        // 입구 양옆(동굴 방보다 입구가 좁다)과 입구 위
        Block(t, "Cave_LipL", new Vector3(Face - 2f, 0, -CaveHalfWidth), new Vector3(Face, CaveHeight, -DoorHalfWidth), Stone[1]);
        Block(t, "Cave_LipR", new Vector3(Face - 2f, 0, DoorHalfWidth), new Vector3(Face, CaveHeight, CaveHalfWidth), Stone[1]);
        Block(t, "Cave_LipTop", new Vector3(Face - 2f, DoorHeight, -DoorHalfWidth), new Vector3(Face, CaveHeight, DoorHalfWidth), Stone[1]);

        // ── 앞면 바위 조각(보이는 것만, 부딪힘 없음) ── 동굴 입구·폭포 자리는 피한다
        for (int i = 0; i < 70; i++)
        {
            float z = (float)(rng.NextDouble() * 2 - 1) * (halfL - 2f);
            float y = (float)rng.NextDouble() * (Height - 2f) + 1f;
            if (Mathf.Abs(z) < FallWidth / 2f + 1.5f && y < Height) continue; // 폭포 뒤는 매끈하게
            var size = new Vector3(2f + (float)rng.NextDouble() * 3f, 2f + (float)rng.NextDouble() * 4f, 3f + (float)rng.NextDouble() * 5f);
            var chunk = PrototypeBootstrap.Part(t, PrimitiveType.Cube, new Vector3(Face + (float)rng.NextDouble() * 0.8f, y, z), size, Stone[rng.Next(Stone.Length)]);
            chunk.transform.localRotation = Quaternion.Euler((float)rng.NextDouble() * 20 - 10, (float)rng.NextDouble() * 30 - 15, (float)rng.NextDouble() * 20 - 10);
        }
        // 절벽 위 가장자리 울퉁불퉁 + 위에 나무 몇 그루
        for (int i = 0; i < 18; i++)
        {
            float z = -halfL + 3f + i * (Length - 6f) / 17f;
            if (Mathf.Abs(z) < FallWidth / 2f + 1f) continue;
            var cap = PrototypeBootstrap.Part(t, PrimitiveType.Cube, new Vector3(Face - 2f, Height + 0.6f, z), new Vector3(5f, 1.6f + (float)rng.NextDouble() * 2f, 4.5f), Stone[rng.Next(Stone.Length)]);
            cap.transform.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 40 - 20, (float)rng.NextDouble() * 10 - 5);
            if (i % 3 == 0) TopTree(t, new Vector3(Face - 6f, Height, z + 1.5f), 1f + (float)rng.NextDouble() * 0.4f);
        }

        // ── 물 ──
        cliff.riverMat = ArtLibrary.Transparent(FlowTexture(), WaterColor);
        cliff.fallMat = ArtLibrary.Transparent(FlowTexture(), new Color(0.85f, 0.95f, 1f, 0.85f));
        cliff.fallMat.renderQueue = 3001; // 웅덩이 위에 그려지게
        // 절벽 위 강(폭포로 흘러 들어감)
        var river = PrototypeBootstrap.Part(t, PrimitiveType.Cube, new Vector3((Edge + Face) / 2f, Height + 0.05f, 0), new Vector3(depth, 0.1f, FallWidth), Color.white);
        river.GetComponent<Renderer>().sharedMaterial = cliff.riverMat;
        river.name = "River";
        // 폭포: 절벽 앞면에서 살짝 떨어진 물줄기 한 장(통과 가능)
        var fall = PrototypeBootstrap.Part(t, PrimitiveType.Cube, new Vector3(Face + 0.6f, Height / 2f, 0), new Vector3(0.4f, Height + 0.2f, FallWidth), Color.white);
        fall.GetComponent<Renderer>().sharedMaterial = cliff.fallMat;
        fall.name = "Waterfall";
        // 웅덩이 + 물가
        PrototypeBootstrap.Part(t, PrimitiveType.Cylinder, PoolCenter + Vector3.up * 0.03f, new Vector3(PoolRadius * 2 + 2f, 0.01f, PoolRadius * 2 + 2f), new Color(0.78f, 0.72f, 0.6f)).name = "PoolShore";
        var pool = PrototypeBootstrap.Part(t, PrimitiveType.Cylinder, PoolCenter + Vector3.up * 0.07f, new Vector3(PoolRadius * 2, 0.01f, PoolRadius * 2), Color.white);
        pool.GetComponent<Renderer>().sharedMaterial = ArtLibrary.Transparent(null, new Color(0.45f, 0.78f, 0.95f, 0.85f));
        pool.name = "Pool";
        // 물보라: 폭포 아래 하얀 공 몇 개가 부풀었다 줄었다
        var foamMat = ArtLibrary.Transparent(null, new Color(1f, 1f, 1f, 0.75f));
        cliff.foam = new Transform[9];
        for (int i = 0; i < cliff.foam.Length; i++)
        {
            float z = -FallWidth / 2f + 0.6f + i * (FallWidth - 1.2f) / (cliff.foam.Length - 1);
            var f = PrototypeBootstrap.Part(t, PrimitiveType.Sphere, new Vector3(Face + 1.6f, 0.3f, z), Vector3.one * 2f, Color.white);
            f.GetComponent<Renderer>().sharedMaterial = foamMat;
            f.name = "Foam";
            cliff.foam[i] = f.transform;
        }

        // ── 동굴 안 ── 어두운 바닥, 은은한 파란 빛, 빛나는 결정
        PrototypeBootstrap.Part(t, PrimitiveType.Cube, new Vector3(Face - CaveDepth / 2f - 1f, 0.02f, 0), new Vector3(CaveDepth + 2f, 0.04f, CaveHalfWidth * 2), new Color(0.35f, 0.33f, 0.35f)).name = "CaveFloor";
        var glow = new GameObject("CaveLight").AddComponent<Light>();
        glow.transform.SetParent(t, false);
        glow.transform.position = new Vector3(Face - CaveDepth / 2f, CaveHeight - 2f, 0);
        glow.type = LightType.Point;
        glow.color = new Color(0.55f, 0.8f, 1f);
        glow.range = 12f;
        glow.intensity = 3f;
        var crystalMat = ArtLibrary.Transparent(null, new Color(0.6f, 0.95f, 1f, 0.9f));
        for (int i = 0; i < 7; i++)
        {
            var pos = new Vector3(Face - CaveDepth + 1f + (float)rng.NextDouble() * 2f, 0.6f, -CaveHalfWidth + 1f + (float)rng.NextDouble() * (CaveHalfWidth * 2 - 2f));
            if (i >= 4) pos = new Vector3(Face - 2.6f - (float)rng.NextDouble() * (CaveDepth - 4f), 0.6f, (i % 2 == 0 ? -1 : 1) * (CaveHalfWidth - 0.8f));
            var c = PrototypeBootstrap.Part(t, PrimitiveType.Capsule, pos, new Vector3(0.5f, 0.8f + (float)rng.NextDouble() * 0.6f, 0.5f), Color.white);
            c.GetComponent<Renderer>().sharedMaterial = crystalMat;
            c.transform.localRotation = Quaternion.Euler((float)rng.NextDouble() * 30 - 15, 0, (float)rng.NextDouble() * 30 - 15);
            c.name = "CaveCrystal";
        }
        return cliff;
    }

    void Update()
    {
        // 물이 흐르는 것처럼 무늬를 움직인다
        float t = Time.time;
        if (fallMat) fallMat.mainTextureOffset = new Vector2(0, t * 0.9f);
        if (riverMat) riverMat.mainTextureOffset = new Vector2(t * 0.35f, 0);
        if (foam == null) return;
        for (int i = 0; i < foam.Length; i++)
            foam[i].localScale = Vector3.one * (1.8f + Mathf.Sin(t * 3f + i * 1.3f) * 0.35f);
    }

    // 두 모서리(min, max)로 만드는 돌 상자(부딪힘 있음)
    static void Block(Transform parent, string name, Vector3 min, Vector3 max, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = (min + max) / 2f;
        go.transform.localScale = max - min;
        go.GetComponent<Renderer>().material.color = color;
    }

    static void TopTree(Transform parent, Vector3 pos, float size)
    {
        var root = new GameObject("CliffTree").transform;
        root.SetParent(parent, false);
        root.position = pos;
        root.localScale = Vector3.one * size;
        PrototypeBootstrap.Part(root, PrimitiveType.Cylinder, new Vector3(0, 1.4f, 0), new Vector3(0.5f, 1.4f, 0.5f), new Color(0.45f, 0.3f, 0.18f));
        PrototypeBootstrap.Part(root, PrimitiveType.Sphere, new Vector3(0, 3.4f, 0), new Vector3(3f, 2.6f, 3f), new Color(0.36f, 0.6f, 0.3f));
        PrototypeBootstrap.Part(root, PrimitiveType.Sphere, new Vector3(0.9f, 2.9f, 0.3f), new Vector3(1.8f, 1.6f, 1.8f), new Color(0.4f, 0.64f, 0.32f));
    }

    // 흐르는 물 무늬: 세로 줄무늬(밝고 어두운 물줄기)를 코드로 그린다
    static Texture2D FlowTexture()
    {
        const int w = 32, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        var rng = new System.Random(5);
        var col = new float[w];
        for (int x = 0; x < w; x++) col[x] = 0.75f + (float)rng.NextDouble() * 0.25f;
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float streak = Mathf.PerlinNoise(x * 0.35f, y * 0.12f);
                float v = Mathf.Lerp(col[x], 1f, streak * 0.6f);
                px[y * w + x] = new Color(v, v, 1f, 0.55f + streak * 0.45f);
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
