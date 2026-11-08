using UnityEngine;

// 맵 왼쪽 끝(-X)의 절벽과 큰 폭포. 단순한 도형으로 형태만 잡는다(나중에 모델로 바꾸기 쉽게).
//   절벽: x = -80 ~ -66, 높이 Height. 앞면(x = -66)에 울퉁불퉁한 바위 조각.
//   폭포: 절벽 가운데(z = 0)에 폭 FallWidth 로 위에서 떨어진다(물이 흐르는 무늬가 움직임). 위에는 강, 아래엔 웅덩이·물보라.
//   동굴: 폭포 바로 뒤 입구(폭 6m, 높이 6m) → 안쪽 방. 폭포는 통과할 수 있어서 걸어 들어간다.
//   오르는 길: 폭포 오른쪽(+z)에서 절벽 앞면을 따라 꼭대기까지. 돌 징검다리(점프) · 가파른 돌계단(한 칸씩 점프) ·
//             경사로(걸어서)를 섞었다. 로봇 점프 높이 1.2m 기준으로 한 번에 1m 이하만 올라가게 짰다.
public class Cliff : MonoBehaviour
{
    public static Cliff Instance { get; private set; }

    public const float Depth = 14f;                         // 절벽 두께
    public static readonly float Edge = -GroundView.Size / 2f; // 맵 끝(절벽 뒤)
    public static readonly float Face = Edge + Depth;          // 절벽 앞면
    public const float Height = 24f;
    public const float Length = 180f;    // z 방향 길이
    // 오르는 길이 놓이는 곳(z 범위, 앞면에서 바깥으로 폭)
    public const float ClimbStartZ = 10f, ClimbEndZ = 90f, ClimbWidth = 4.6f;
    public const float FallWidth = 10f;
    const float CaveDepth = 10f, CaveHalfWidth = 6f, CaveHeight = 7f, DoorHalfWidth = 3f, DoorHeight = 6f;
    static Vector3 PoolCenter => new Vector3(Face + 6f, 0, 0);
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
        if (p.x < Face + ClimbWidth + margin && p.z > ClimbStartZ - margin && p.z < ClimbEndZ + margin) return true; // 오르는 길 밑
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
        for (int i = 0; i < 140; i++)
        {
            float z = (float)(rng.NextDouble() * 2 - 1) * (halfL - 2f);
            float y = (float)rng.NextDouble() * (Height - 2f) + 1f;
            if (Mathf.Abs(z) < FallWidth / 2f + 1.5f && y < Height) continue; // 폭포 뒤는 매끈하게
            if (z > ClimbStartZ - 2f && z < ClimbEndZ + 2f) continue;          // 오르는 길 자리는 비운다
            var size = new Vector3(2f + (float)rng.NextDouble() * 3f, 2f + (float)rng.NextDouble() * 4f, 3f + (float)rng.NextDouble() * 5f);
            var chunk = PrototypeBootstrap.Part(t, PrimitiveType.Cube, new Vector3(Face + (float)rng.NextDouble() * 0.8f, y, z), size, Stone[rng.Next(Stone.Length)]);
            chunk.transform.localRotation = Quaternion.Euler((float)rng.NextDouble() * 20 - 10, (float)rng.NextDouble() * 30 - 15, (float)rng.NextDouble() * 20 - 10);
        }
        // 절벽 위 가장자리 울퉁불퉁 + 위에 나무 몇 그루
        for (int i = 0; i < 36; i++)
        {
            float z = -halfL + 3f + i * (Length - 6f) / 35f;
            if (Mathf.Abs(z) < FallWidth / 2f + 1f) continue;
            if (z > ClimbStartZ && z < ClimbEndZ + 4f) continue; // 길 끝에서 꼭대기로 올라서는 자리
            var cap = PrototypeBootstrap.Part(t, PrimitiveType.Cube, new Vector3(Face - 2f, Height + 0.6f, z), new Vector3(5f, 1.6f + (float)rng.NextDouble() * 2f, 4.5f), Stone[rng.Next(Stone.Length)]);
            cap.transform.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 40 - 20, (float)rng.NextDouble() * 10 - 5);
            if (i % 3 == 0) TopTree(t, new Vector3(Face - 6f, Height, z + 1.5f), 1f + (float)rng.NextDouble() * 0.4f);
        }

        BuildClimb(t, rng);

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

    // ── 절벽 오르는 길 ──
    // 폭포 오른쪽 땅에서 시작해 +z 로 가며 올라간다. 순서: 징검다리 → 경사로 → 가파른 계단 → 징검다리 → 경사로 → 가파른 계단 → 꼭대기.
    //   징검다리: 돌 사이 1m 틈, 한 돌에 1m씩 높아짐 → 점프로 건넌다
    //   가파른 계단: 틈 없이 한 칸 0.9m씩 높음 → 걸어서는 못 오르고 한 칸씩 점프
    //   경사로: 38도(로봇이 걸어 오를 수 있는 한계 45도 아래) → 걸어서
    const float StoneRise = 1f, StoneGap = 1f, StoneLen = 2.2f;
    const float StepRise = 0.9f, StepRun = 0.9f;
    const float RampAngle = 38f, RampRise = 4f;
    const float PadLen = 3f;

    static void BuildClimb(Transform t, System.Random rng)
    {
        float laneX = Face + ClimbWidth / 2f;
        float z = ClimbStartZ, y = 0f;
        var route = t.gameObject.AddComponent<ClimbRoute>();

        void Ledge(string name, float z0, float z1, float top, float width, float jitterX, Color color)
        {
            float thick = Mathf.Clamp(top, 0.15f, 1.2f); // 땅에 닿게(바닥 발판은 얇게)
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(t, false);
            go.transform.position = new Vector3(laneX + jitterX, top - thick / 2f, (z0 + z1) / 2f);
            go.transform.localScale = new Vector3(width, thick, z1 - z0);
            go.GetComponent<Renderer>().material.color = color;
            route.points.Add(new Vector3(laneX + jitterX, top, (z0 + z1) / 2f));
        }
        void Pad() { Ledge("Climb_Pad", z, z + PadLen, y, ClimbWidth, 0, Stone[0] * 0.95f); z += PadLen; }
        void Stones(int n)
        {
            for (int i = 0; i < n; i++)
            {
                z += StoneGap;
                y += StoneRise;
                float jitter = (float)(rng.NextDouble() * 2 - 1) * 0.5f;
                Ledge("Climb_Stone", z, z + StoneLen, y, 2.6f, jitter, Stone[rng.Next(Stone.Length)]);
                z += StoneLen;
            }
            z += StoneGap; y += StoneRise; // 다음 발판도 한 칸 위
        }
        void Steps(int n)
        {
            for (int i = 0; i < n; i++)
            {
                y += StepRise;
                Ledge("Climb_Step", z, z + StepRun, y, 3.6f, 0, Stone[2]);
                z += StepRun;
            }
        }
        void Ramp()
        {
            float run = RampRise / Mathf.Tan(RampAngle * Mathf.Deg2Rad);
            float len = Mathf.Sqrt(run * run + RampRise * RampRise);
            const float thick = 0.6f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Climb_Ramp";
            go.transform.SetParent(t, false);
            // 윗면이 (z, y) → (z+run, y+rise) 를 지나게: 가운데를 법선 방향으로 thick/2 내린다
            var normal = Quaternion.Euler(-RampAngle, 0, 0) * Vector3.up;
            go.transform.position = new Vector3(laneX, y + RampRise / 2f, z + run / 2f) - normal * thick / 2f;
            go.transform.rotation = Quaternion.Euler(-RampAngle, 0, 0);
            go.transform.localScale = new Vector3(ClimbWidth, thick, len);
            go.GetComponent<Renderer>().material.color = Stone[3];
            route.points.Add(new Vector3(laneX, y + RampRise / 2f, z + run / 2f));
            z += run;
            y += RampRise;
        }

        Pad();            // 0m 땅
        Stones(4);        // → 5m (점프)
        Pad();
        Ramp();           // → 9m (걷기)
        Pad();
        Steps(4);         // → 12.6m (가파름, 점프)
        Pad();
        Stones(3);        // → 16.6m (점프)
        Pad();
        Ramp();           // → 20.6m (걷기)
        Pad();
        int last = Mathf.CeilToInt((Height - y) / StepRise);
        float rise = (Height - y) / last;
        for (int i = 0; i < last; i++) { y += rise; Ledge("Climb_Step", z, z + StepRun, y, 3.6f, 0, Stone[2]); z += StepRun; } // → 꼭대기(가파름)
        // 꼭대기 발판: 절벽 위와 같은 높이, 절벽 안쪽으로 이어진다
        var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
        top.name = "Climb_Top";
        top.transform.SetParent(t, false);
        top.transform.position = new Vector3(Face + ClimbWidth / 2f - 1f, Height - 0.6f, z + PadLen / 2f);
        top.transform.localScale = new Vector3(ClimbWidth + 2f, 1.2f, PadLen);
        top.GetComponent<Renderer>().material.color = Stone[0];
        route.points.Add(new Vector3(Face - 3f, Height, z + PadLen / 2f));
        route.endZ = z + PadLen;
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

// 오르는 길의 발판 가운데 점들(아래에서 위 순서). 자가 시험·나중의 길 안내에 쓴다.
public class ClimbRoute : MonoBehaviour
{
    public readonly System.Collections.Generic.List<Vector3> points = new System.Collections.Generic.List<Vector3>();
    public float endZ;
}
