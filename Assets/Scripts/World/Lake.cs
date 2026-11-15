using UnityEngine;

// 기지 울타리 바로 밖(남쪽 출입구 왼쪽)의 연못(모델 pond.obj). 모델이 없으면 우물(깊게 파인 구멍 + 돌 테두리)로 만든다. 둘 다 못 들어간다. 물가에서 E를 누르면 물뿌리개를 가득 채운다(GameManager.RefillCan).
// 물 그림은 Resources/Art/Textures/water.png 로 넣으면 단색 대신 나온다. 땅은 평면이라 실제로 파지 않고, 구멍 판(DepthHole 셰이더)으로 그 자리 땅만 안 그려서 깊어 보이게 한다.
public class Lake : MonoBehaviour
{
    public static Lake Instance { get; private set; }

    public float radius = 4f;
    // 물가에서 이만큼 떨어진 곳까지 E가 닿는다
    public float reach = 1.8f;


    static readonly Color ShoreColor = new Color(0.78f, 0.7f, 0.52f);

    public bool InReach(Vector3 p)
    {
        var d = p - transform.position;
        d.y = 0;
        return d.magnitude < radius + reach;
    }

    // 이 점이 호수(+여유)에 겹치면 true. 씨앗·채집 자원이 물 위에 생기지 않게 쓴다.
    public static bool Overlaps(Vector3 p, float margin)
    {
        if (!Instance) return false;
        var d = p - Instance.transform.position;
        d.y = 0;
        return d.magnitude < Instance.radius + margin;
    }

    // 우물 모양: 깊이·돌 테두리 높이·두께
    const float Depth = 4f, RimHeight = 0.8f, RimWidth = 0.5f, WaterDepth = 3.2f;
    static readonly Color StoneColor = new Color(0.66f, 0.58f, 0.82f); // 라벤더 돌(절벽과 같은 색)

    public static Lake Build(Vector3 center, float radius)
    {
        var lake = new GameObject("Lake").AddComponent<Lake>();
        Instance = lake;
        lake.radius = radius;
        lake.transform.position = center;
        var t = lake.transform;
        if (BuildPond(lake)) return lake; // 연못 모델이 있으면 그걸로(없으면 아래 우물)
        var lit = Shader.Find("Universal Render Pipeline/Lit");

        // ① 우물 안쪽 벽(깊을수록 어둡게) — 땅보다 먼저 그린다
        var shaftMat = new Material(lit) { mainTexture = Gradient(), color = StoneColor, renderQueue = 1998 };
        Mesh(t, "Shaft", Ring(radius, radius, -Depth, RimHeight, true, Depth + RimHeight), shaftMat);

        // ② 깊은 곳의 물(불투명, 역시 땅보다 먼저)
        var waterMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.32f, 0.36f, 0.62f), renderQueue = 1998 };
        var tex = ArtLibrary.Texture("Textures/water");
        if (tex) { waterMat.mainTexture = tex; waterMat.color = new Color(0.55f, 0.55f, 0.7f); }
        Mesh(t, "Water", Disc(radius, -WaterDepth), waterMat);

        // ③ 구멍 판: 이 자리만 땅이 안 그려진다
        Mesh(t, "Hole", Disc(radius, 0.05f), new Material(Shader.Find("Monstera/DepthHole")));

        // ④ 돌 테두리(바깥벽 + 윗면)
        var stoneMat = new Material(lit) { color = StoneColor };
        Mesh(t, "RimOuter", Ring(radius + RimWidth, radius + RimWidth, 0f, RimHeight, false, 1f), stoneMat);
        Mesh(t, "RimTop", Annulus(radius, radius + RimWidth, RimHeight), stoneMat);

        // 물가(모래) 고리는 그대로(테두리 바깥으로 살짝)
        PrototypeBootstrap.Part(t, PrimitiveType.Cylinder, Vector3.up * 0.03f, new Vector3((radius + RimWidth) * 2 + 1.6f, 0.01f, (radius + RimWidth) * 2 + 1.6f), ShoreColor).name = "Shore";

        // ⑤ 못 들어가게: 우물 전체를 덮는 높은 원기둥 충돌체(보이지 않음, 카메라 광선에는 안 걸리게)
        var block = new GameObject("WellBlock") { layer = 2 }; // Ignore Raycast
        block.transform.SetParent(t, false);
        var col = block.AddComponent<CapsuleCollider>();
        col.radius = radius + RimWidth;
        col.height = 30f;
        col.center = Vector3.zero;

        // 물뿌리개는 테두리 밖에서 E로 채운다
        lake.reach = RimWidth + 1.8f;
        return lake;
    }

    // 연못 모델(Resources/Art/Models/pond.obj, 원본 FBX 를 OBJ 로 바꾼 것): 물 + 테두리 돌 + 큰 바위 + 연잎·꽃 + 흐르는 물결(애니메이션).
    // 모델 크기와 상관없이 폭이 PondWidth 가 되게 맞추고, 물 위로는 못 들어가게 막는다(물가에서 E로 물 채우기).
    const float PondWidth = 10f;
    // 모델 단위(원본 파일 기준): 작은 돌의 가운데 높이, 물 타원의 반지름(가로·세로)
    const float SmallStoneMid = 0.22f, WaterHalfX = 1.85f, WaterHalfZ = 1.3f;

    static bool BuildPond(Lake lake)
    {
        var t = lake.transform;
        var go = ArtLibrary.SpawnModel("pond", t); // 재질 이름 col_RRGGBB → 그 색의 URP 재질
        if (!go) return false;
        go.name = "Pond";

        // 크기 맞추기: 전체 폭(가로·세로 중 큰 쪽)이 PondWidth
        var b = Bounds(go);
        float width = Mathf.Max(b.size.x, b.size.z);
        float k = width > 0.001f ? PondWidth / width : 1f; // 모델 단위 → 미터
        go.transform.localScale *= k;
        // 가운데를 호수 자리에. 높이는 땅에 박힌 것처럼: 작은 돌이 반쯤 묻히는 만큼 전체를 내린다(큰 돌도 같은 깊이만큼만 묻힌다).
        b = Bounds(go);
        go.transform.position += new Vector3(t.position.x - b.center.x, 0, t.position.z - b.center.z);
        var origin = go.transform.position;
        origin.y = t.position.y - SmallStoneMid * k;
        go.transform.position = origin;

        // 물결: 물 위의 가는 띠 3개(pond_flow.obj, 연못과 같은 좌표). 움직이지 않는다.
        var flow = ArtLibrary.SpawnModel("pond_flow", go.transform);
        if (flow)
        {
            flow.transform.localPosition = Vector3.zero;
            flow.transform.localRotation = Quaternion.identity;
            flow.transform.localScale = Vector3.one;
        }

        // 물이 땅보다 낮아지므로 물 자리만 땅을 안 그린다(구멍 판). 연못은 땅보다 먼저 그려야 구멍 속에 보인다.
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) if (mats[i]) mats[i] = new Material(mats[i]) { renderQueue = 1998 };
            r.sharedMaterials = mats;
        }
        var hole = new GameObject("Hole", typeof(MeshFilter), typeof(MeshRenderer));
        hole.transform.SetParent(t, false);
        hole.transform.position = new Vector3(origin.x, t.position.y + 0.045f, origin.z);
        hole.transform.localScale = new Vector3(WaterHalfX * k * 1.04f, 1f, WaterHalfZ * k * 1.04f);
        hole.GetComponent<MeshFilter>().sharedMesh = Disc(1f, 0f);
        hole.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Monstera/DepthHole"));

        Renderer water = null;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (r.name.StartsWith("Pond_Water")) water = r;
            if (r.name.StartsWith("Tall_Rock")) r.gameObject.AddComponent<MeshCollider>(); // 큰 바위는 부딪힌다
        }

        // 물 위로 못 들어가게: 물 모양(타원)대로 높은 원기둥 충돌체(보이지 않음, 카메라 광선에는 안 걸림)
        var wb = water ? water.bounds : Bounds(go);
        var block = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(block.GetComponent<Collider>());
        Object.Destroy(block.GetComponent<Renderer>());
        block.name = "PondBlock";
        block.layer = 2; // Ignore Raycast
        block.transform.SetParent(t, false);
        block.transform.position = new Vector3(wb.center.x, t.position.y + 2f, wb.center.z);
        block.transform.localScale = new Vector3(wb.size.x * 0.92f, 4f, wb.size.z * 0.92f);
        block.AddComponent<MeshCollider>().convex = true;

        lake.radius = Mathf.Max(wb.extents.x, wb.extents.z);
        lake.reach = 2f;
        return true;
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static void Mesh(Transform parent, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    const int Segments = 40;

    // 원통 벽. inward=true 면 안쪽을 본다. v(세로 그림 좌표)는 아래 0 → 위 1.
    static Mesh Ring(float r0, float r1, float y0, float y1, bool inward, float vScale)
    {
        var v = new Vector3[(Segments + 1) * 2];
        var n = new Vector3[v.Length];
        var uv = new Vector2[v.Length];
        var tri = new int[Segments * 6];
        for (int i = 0; i <= Segments; i++)
        {
            float a = i / (float)Segments * Mathf.PI * 2;
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            v[i * 2] = dir * r0 + Vector3.up * y0;
            v[i * 2 + 1] = dir * r1 + Vector3.up * y1;
            n[i * 2] = n[i * 2 + 1] = inward ? -dir : dir;
            uv[i * 2] = new Vector2(i / (float)Segments * 6f, 0);
            uv[i * 2 + 1] = new Vector2(i / (float)Segments * 6f, 1);
        }
        for (int i = 0; i < Segments; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            if (!inward) { tri[i * 6] = a; tri[i * 6 + 1] = b; tri[i * 6 + 2] = c; tri[i * 6 + 3] = c; tri[i * 6 + 4] = b; tri[i * 6 + 5] = d; }
            else { tri[i * 6] = a; tri[i * 6 + 1] = c; tri[i * 6 + 2] = b; tri[i * 6 + 3] = c; tri[i * 6 + 4] = d; tri[i * 6 + 5] = b; }
        }
        return new Mesh { vertices = v, normals = n, uv = uv, triangles = tri };
    }

    // 위를 보는 원판
    static Mesh Disc(float r, float y)
    {
        var v = new Vector3[Segments + 1];
        var uv = new Vector2[v.Length];
        var tri = new int[Segments * 3];
        v[0] = Vector3.up * y; uv[0] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < Segments; i++)
        {
            float a = i / (float)Segments * Mathf.PI * 2;
            v[i + 1] = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            uv[i + 1] = new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f);
            tri[i * 3] = 0; tri[i * 3 + 1] = (i + 1) % Segments + 1; tri[i * 3 + 2] = i + 1;
        }
        var m = new Mesh { vertices = v, uv = uv, triangles = tri };
        m.RecalculateNormals();
        return m;
    }

    // 위를 보는 도넛(테두리 윗면)
    static Mesh Annulus(float r0, float r1, float y)
    {
        var v = new Vector3[(Segments + 1) * 2];
        var tri = new int[Segments * 6];
        for (int i = 0; i <= Segments; i++)
        {
            float a = i / (float)Segments * Mathf.PI * 2;
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            v[i * 2] = dir * r0 + Vector3.up * y;
            v[i * 2 + 1] = dir * r1 + Vector3.up * y;
        }
        for (int i = 0; i < Segments; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            tri[i * 6] = a; tri[i * 6 + 1] = c; tri[i * 6 + 2] = b; tri[i * 6 + 3] = c; tri[i * 6 + 4] = d; tri[i * 6 + 5] = b;
        }
        var m = new Mesh { vertices = v, triangles = tri };
        m.RecalculateNormals();
        return m;
    }

    // 세로 그라데이션: 아래(깊은 곳) 어둡게 → 위 밝게
    static Texture2D Gradient()
    {
        var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
        {
            float k = Mathf.Pow(y / 63f, 1.6f);
            tex.SetPixel(0, y, Color.Lerp(new Color(0.08f, 0.07f, 0.14f), Color.white, k));
        }
        tex.Apply();
        return tex;
    }
}
