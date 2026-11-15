using System.Collections.Generic;
using UnityEngine;

// 지형 꾸미기: 층층이 쌓인 단(段) 언덕 + 집(원주민 오두막) 마을 몇 곳.
// 땅 전체(평평한 들판 · 언덕 · 폭포 산 · 파인 강바닥)를 지형 메시로 만든다. 평평한 판(GroundView)은 그림·색만 빌려 쓰고 숨긴다.
// 언덕은 1m 높이 단이 등고선처럼 구불구불 쌓인 모양. 가파른 턱은 점프(Space, 1.2m)로, 완만한 곳은 걸어서 오른다.
// 단 윗면은 땅과 같은 그림(황무지 + 풀밭), 턱(가파른 면)은 셰이더가 바위 색으로 칠한다.
// 기지·호수(우물)·절벽·원주민 마을 둘레는 평평하게 비워 둔다.
// ★ 아래 Hills / Hamlets 표만 고치면 언덕·집 자리가 바뀐다.
public class Landscape : MonoBehaviour
{
    public static Landscape Instance { get; private set; }

    // 언덕: 가운데(x, z), 반지름, 높이(m)
    static readonly (float x, float z, float radius, float height)[] Hills =
    {
        // 북서쪽 덩어리(여러 개가 붙어 있음, 하나는 높이 솟은 기둥)
        (-60f, 62f, 30f, 18f), (-84f, 40f, 18f, 10f), (-38f, 82f, 16f, 12f), (-72f, 88f, 12f, 24f),
        // 북쪽 덩어리
        (28f, 102f, 30f, 20f), (55f, 120f, 18f, 14f), (4f, 122f, 15f, 9f),
        // 북동쪽 외톨이
        (112f, 104f, 24f, 16f),
        // 동쪽 덩어리
        (124f, -42f, 26f, 18f), (142f, -16f, 14f, 11f), (100f, -66f, 13f, 8f),
        // 동쪽 끝 외톨이
        (144f, 42f, 15f, 12f),
        // 남쪽: 넓은 언덕 + 혼자 우뚝 솟은 기둥
        (42f, -102f, 26f, 14f), (-22f, -128f, 9f, 17f),
        // 남서쪽 덩어리
        (-90f, -72f, 26f, 15f), (-114f, -102f, 16f, 21f),
        // 기지 둘레 작은 둔덕(낮게)
        (-52f, 8f, 12f, 4f), (62f, -22f, 10f, 3f), (22f, 52f, 11f, 5f),
    };

    // 집 마을: 가운데(x, z), 집 수, 둘레 반지름
    static readonly (float x, float z, int count, float ring)[] Hamlets =
    {
        // 큰 마을 세 곳(원주민 마을 포함)은 Town 이 빽빽하게 짓는다. 여기는 둥근 오두막 몇 채짜리 작은 마을.
        (2f, -62f, 4, 9f),
        (92f, -4f, 3, 8f),
        (30f, 96f, 3, 8f),
    };

    const float HutRadius = 2.3f, HutWall = 2.4f, HutRoof = 2.4f;

    static readonly Color[] WallColors =
    {
        new Color(1f, 0.9f, 0.82f), new Color(0.98f, 0.82f, 0.78f), new Color(0.86f, 0.82f, 0.98f), new Color(0.84f, 0.95f, 0.9f),
    };
    static readonly Color[] RoofColors =
    {
        new Color(0.45f, 0.76f, 0.74f), new Color(0.96f, 0.6f, 0.58f), new Color(0.66f, 0.56f, 0.86f), new Color(0.98f, 0.76f, 0.5f),
    };

    readonly List<(Vector3 pos, float radius)> blockers = new List<(Vector3, float)>();
    GroundView ground;
    Material hillDead, hillGrass;

    // 언덕 하나 = 크기가 제각각인 평평한 단(판) 여러 장을 비뚤게 쌓은 것.
    //   맨 아래 단이 제일 넓고, 위로 갈수록 좁아지되 크기·위치가 들쭉날쭉(한쪽으로 쏠리거나 옆에 혹이 붙는다).
    //   단 사이 높이는 대부분 1m(점프로 오름), 가끔 2m(그 옆에 1m 디딤돌을 붙여서 돌아 올라감).
    //   단 가장자리는 구불구불한 바위 턱.
    struct Plateau { public float x, z, r, top, seed; }
    static List<Plateau> plateaus;
    const float EdgeWidth = 0.6f; // 턱의 가로 폭(m). 작을수록 가파르다

    static List<Plateau> Plateaus()
    {
        if (plateaus != null) return plateaus;
        plateaus = new List<Plateau>();
        for (int hi = 0; hi < Hills.Length; hi++)
        {
            var hill = Hills[hi];
            var rng = new System.Random(100 + hi);
            float R() => (float)rng.NextDouble();
            float cx = hill.x, cz = hill.z, r = hill.radius * 0.85f, h = 0;
            int levels = Mathf.Max(2, Mathf.RoundToInt(hill.height / 1.25f));
            // 꼭대기 반지름이 3m 쯤 되게 한 층마다 줄어드는 비율(층마다 들쭉날쭉)
            float baseRatio = Mathf.Pow(3f / r, 1f / levels);
            for (int l = 0; l < levels && r > 2.2f; l++)
            {
                float step = l > 0 && R() < 0.25f ? 2f : 1f;
                float prevH = h;
                h += step;
                plateaus.Add(new Plateau { x = cx, z = cz, r = r, top = h, seed = R() * 50f });
                // 옆에 붙은 혹(같은 높이) → 모양이 동그랗지 않게
                if (R() < 0.6f)
                {
                    float a = R() * Mathf.PI * 2;
                    plateaus.Add(new Plateau { x = cx + Mathf.Cos(a) * r * 0.65f, z = cz + Mathf.Sin(a) * r * 0.65f, r = r * (0.35f + R() * 0.25f), top = h, seed = R() * 50f });
                }
                // 2m 턱이면 아래 단 위에 1m 디딤돌
                if (step > 1.5f)
                {
                    float a = R() * Mathf.PI * 2;
                    plateaus.Add(new Plateau { x = cx + Mathf.Cos(a) * (r + 1.2f), z = cz + Mathf.Sin(a) * (r + 1.2f), r = 2.6f, top = prevH + 1f, seed = R() * 50f });
                }
                // 다음 단: 조금씩(층마다 들쭉날쭉) 좁아지고, 가운데는 아무 쪽으로 쏠린다(조금 삐져나가면 그쪽은 높은 절벽)
                float ratio = Mathf.Clamp(baseRatio + (R() - 0.5f) * 0.3f, 0.5f, 0.97f);
                float nr = r * ratio;
                float off = (r - nr) * R() * 1.05f, oa = R() * Mathf.PI * 2;
                cx += Mathf.Cos(oa) * off; cz += Mathf.Sin(oa) * off;
                r = nr;
            }
        }
        return plateaus;
    }

    public const float SeaDepth = 2.4f; // 맵 가장자리에서의 바다 깊이(m)

    // 맵 가장자리까지 거리(m). 맵 밖이면 음수.
    public static float ShoreDistance(Vector3 p)
        => Mathf.Min(Mathf.Min(p.x - GroundView.MinX, GroundView.MaxX - p.x), Mathf.Min(p.z - GroundView.MinZ, GroundView.MaxZ - p.z));

    // 이 자리의 땅 높이(언덕이 없으면 0)
    public static float HeightAt(Vector3 p)
    {
        float h = Cliff.HeightAt(p.x, p.z); // 서쪽 폭포 산
        foreach (var pl in Plateaus())
        {
            if (pl.top <= h) continue;
            float dx = p.x - pl.x, dz = p.z - pl.z;
            float d2 = dx * dx + dz * dz, maxR = pl.r * 1.35f;
            if (d2 > maxR * maxR) continue;
            float d = Mathf.Sqrt(d2), a = Mathf.Atan2(dz, dx);
            // 가장자리 구불구불(각도 따라 둘레를 한 바퀴 도는 노이즈라 이음매가 없다)
            float n = Mathf.PerlinNoise(pl.seed + Mathf.Cos(a) * 1.6f, pl.seed + Mathf.Sin(a) * 1.6f) - 0.5f;
            float n2 = Mathf.PerlinNoise(pl.seed + 20f + Mathf.Cos(a) * 4f, pl.seed + Mathf.Sin(a) * 4f) - 0.5f;
            float edge = pl.r * (1f + n * 0.55f) + n2 * 1.2f;
            float k = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(edge, edge - EdgeWidth, d));
            h = Mathf.Max(h, pl.top * k);
        }
        // 바닷가: 맵 가장자리로 갈수록 모래사장으로 낮아져 바다 밑(-SeaDepth)까지 내려간다. 가장자리에 걸친 언덕은 바다 쪽으로 낮아진다.
        float shore = ShoreDistance(p);
        if (shore < GroundView.Beach)
        {
            // 안쪽 40%는 물 밖 모래사장(아주 완만하게 0.3m 내려감), 나머지는 바다 속으로 깊어진다
            float u = Mathf.Clamp01(1f - shore / GroundView.Beach);
            float depth = u < 0.4f ? 0.3f * Mathf.SmoothStep(0, 1, u / 0.4f) : 0.3f + (SeaDepth - 0.3f) * Mathf.SmoothStep(0, 1, (u - 0.4f) / 0.6f);
            return h * Mathf.SmoothStep(0, 1, Mathf.Clamp01(shore / 10f)) - depth;
        }
        // 파인 땅(강·웅덩이·연못): 땅보다 낮아진다(음수). 산·언덕이 솟는 자리에서는 서서히 사라진다.
        if (h < 1.5f)
        {
            float carve = Cliff.CarveAt(p.x, p.z);
            if (carve < 0) h += carve * (1f - Mathf.Clamp01(h / 1.5f));
        }
        return h;
    }

    // 집·좌판처럼 자리를 차지하는 것을 알려 둔다(나무·돌·씨앗이 겹쳐 생기지 않게)
    public void AddBlocker(Vector3 pos, float radius) => blockers.Add((pos, radius));

    // 집 자리(+여유)에 겹치면 true. 나무·돌·씨앗이 집 안에 안 생기게.
    public static bool Blocks(Vector3 p, float margin)
    {
        if (!Instance) return false;
        foreach (var b in Instance.blockers)
        {
            var d = p - b.pos; d.y = 0;
            if (d.magnitude < b.radius + margin) return true;
        }
        return false;
    }

    // 숲: 가운데(x, z), 반지름, 나무 수, 주로 자라는 나무(NaturePack.Trees 의 번호: 0 참나무 · 1 자작 · 2 벚나무 · 3 단풍)
    // 숲마다 오솔길이 한 줄 지나간다(기지 쪽에서 들어와 반대쪽으로 나간다). ★ 이 표만 고치면 숲 자리·크기가 바뀐다.
    public static readonly (float x, float z, float radius, int trees, int kind)[] Forests =
    {
        (-100f, -22f, 20f, 46, 2),   // 서쪽 벚나무 숲
        (38f, -48f, 17f, 32, 0),     // 남동쪽 참나무 숲
        (95f, 62f, 18f, 36, 1),      // 원주민 마을 북동쪽 자작나무 숲
        (-32f, -92f, 20f, 46, 3),    // 남쪽 단풍 숲
        (-200f, -122f, 26f, 70, 0),  // 폭포 산 남쪽 큰 숲
        (-186f, 126f, 24f, 60, 2),   // 폭포 산 북쪽 숲
    };
    const float PathHalf = 1.3f; // 오솔길 반폭(m)
    static List<Vector3>[] forestPaths;

    static List<Vector3>[] ForestPaths()
    {
        if (forestPaths != null) return forestPaths;
        forestPaths = new List<Vector3>[Forests.Length];
        for (int f = 0; f < Forests.Length; f++)
        {
            var fo = Forests[f];
            var c = new Vector3(fo.x, 0.035f, fo.z);
            var d = new Vector3(-fo.x, 0, 1f - fo.z).normalized; // 기지 쪽
            var side = new Vector3(-d.z, 0, d.x);
            float r = fo.radius, bend = (f % 2 == 0 ? 1f : -1f) * r;
            var ctrl = new[] { c + d * (r + 8f), c + d * r * 0.45f + side * bend * 0.3f, c - d * r * 0.3f - side * bend * 0.25f, c - d * (r + 8f) };
            var pts = new List<Vector3>();
            for (int i = 0; i < ctrl.Length - 1; i++)
                for (int k = 0; k < 8; k++)
                {
                    float u = k / 8f;
                    Vector3 p0 = ctrl[Mathf.Max(i - 1, 0)], p1 = ctrl[i], p2 = ctrl[i + 1], p3 = ctrl[Mathf.Min(i + 2, ctrl.Length - 1)];
                    pts.Add(0.5f * (2f * p1 + (p2 - p0) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u + (3f * p1 - p0 - 3f * p2 + p3) * u * u * u));
                }
            pts.Add(ctrl[ctrl.Length - 1]);
            forestPaths[f] = pts;
        }
        return forestPaths;
    }

    // 오솔길 위(+여유)면 true. 나무·돌이 길을 막지 않게.
    public static bool OnPath(Vector3 p, float margin)
    {
        float limit = PathHalf + margin;
        foreach (var path in ForestPaths())
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 a = path[i], b = path[i + 1];
                float t = Mathf.Clamp01(Vector3.Dot(new Vector3(p.x - a.x, 0, p.z - a.z), b - a) / (b - a).sqrMagnitude);
                var q = a + (b - a) * t;
                if ((p.x - q.x) * (p.x - q.x) + (p.z - q.z) * (p.z - q.z) < limit * limit) return true;
            }
        return false;
    }

    void BuildForestPaths()
    {
        var dirt = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.86f, 0.76f, 0.6f) };
        dirt.SetFloat("_Smoothness", 0f);
        foreach (var path in ForestPaths()) Cliff.Ribbon(transform, "ForestPath", path, PathHalf, dirt, false);
    }

    // 나무 숲 자리로 쓰기 좋은 곳: 언덕 비탈 + 마을 둘레
    public static IEnumerable<Vector3> GroveCenters()
    {
        for (int i = 0; i < Hills.Length; i++)
        {
            var h = Hills[i];
            if (h.radius < 20f) continue;
            float a = i * 2.4f;
            yield return new Vector3(h.x + Mathf.Cos(a) * h.radius * 0.45f, 0, h.z + Mathf.Sin(a) * h.radius * 0.45f);
        }
        for (int i = 0; i < Hamlets.Length; i++)
            yield return new Vector3(Hamlets[i].x + Hamlets[i].ring + 9f, 0, Hamlets[i].z - 6f);
    }

    public static Landscape Build(GroundView ground)
    {
        var land = new GameObject("Landscape").AddComponent<Landscape>();
        Instance = land;
        land.ground = ground;
        land.BuildHills();
        land.BuildHamlets();
        Town.Build(land.transform, land); // 빽빽한 큰 마을들
        land.BuildForestPaths();
        return land;
    }

    void Update()
    {
        if (!ground || hillDead == null) return;
        hillDead.color = ground.DeadMaterial.color;
        if (hillGrass && ground.GrassMaterial) hillGrass.color = ground.GrassMaterial.color;
    }

    // ── 단 언덕 ──
    // 바위(가파른 면) 색. ★ 절벽·돌턱 색은 여기만 바꾸면 된다.
    public static readonly Color RockColor = new Color(0.78f, 0.72f, 0.74f, 1f); // 따뜻한 연회색 돌
    // 모래 색(땅보다 낮은 곳: 바닷가·강바닥)
    public static readonly Color SandColor = new Color(0.96f, 0.88f, 0.7f, 1f);

    const float ChunkSize = 48f, Res = 0.55f; // 메시 한 덩어리 크기(m), 꼭짓점 간격(m)

    void BuildHills()
    {
        var shader = Shader.Find("Monstera/GroundShaded");
        var rock = RockColor; // 턱·절벽 바위
        var dead = ground.DeadMaterial;
        hillDead = new Material(shader) { mainTexture = dead.mainTexture, mainTextureScale = dead.mainTextureScale, color = dead.color };
        hillDead.SetColor("_RockColor", rock);
        hillDead.SetColor("_SandColor", SandColor);
        if (ground.GrassMaterial)
        {
            var g = ground.GrassMaterial;
            hillGrass = new Material(shader) { mainTexture = g.mainTexture, mainTextureScale = g.mainTextureScale, color = g.color, renderQueue = 3000 };
            hillGrass.SetFloat("_Src", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            hillGrass.SetFloat("_Dst", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            hillGrass.SetFloat("_ZW", 0);
            hillGrass.SetColor("_RockColor", new Color(rock.r, rock.g, rock.b, 0f)); // 턱에는 풀이 안 덮인다
            hillGrass.SetColor("_SandColor", SandColor);
        }

        // 땅 판의 그림 좌표(uv)를 그대로 이어 쓰려고 판의 모서리 두 점에서 uv 를 읽는다
        var plane = ground.DeadPlane;
        var pm = plane.GetComponent<MeshFilter>().sharedMesh;
        var pv = pm.vertices; var puv = pm.uv;
        int lo = 0, hi = 0;
        for (int i = 1; i < pv.Length; i++)
        {
            if (pv[i].x + pv[i].z < pv[lo].x + pv[lo].z) lo = i;
            if (pv[i].x + pv[i].z > pv[hi].x + pv[hi].z) hi = i;
        }
        Vector2 UV(float x, float z)
        {
            var l = plane.InverseTransformPoint(new Vector3(x, 0, z));
            return new Vector2(
                Mathf.LerpUnclamped(puv[lo].x, puv[hi].x, (l.x - pv[lo].x) / (pv[hi].x - pv[lo].x)),
                Mathf.LerpUnclamped(puv[lo].y, puv[hi].y, (l.z - pv[lo].z) / (pv[hi].z - pv[lo].z)));
        }

        // 덩어리끼리 꼭 맞물리게: 꼭짓점 간격을 덩어리 크기에 딱 떨어지게 잡는다(안 그러면 덩어리 사이가 벌어져 틈이 보인다).
        int n = Mathf.CeilToInt(ChunkSize / Res) + 1;
        float step = ChunkSize / (n - 1);
        int m = n + 2; // 음영(법선)을 이웃 덩어리와 똑같이 내려고 한 칸 바깥까지 높이를 잰다
        var hgt = new float[m * m];
        for (float cz = GroundView.MinZ; cz < GroundView.MaxZ; cz += ChunkSize)
            for (float cx = GroundView.MinX; cx < GroundView.MaxX; cx += ChunkSize)
            {
                // 먼저 테두리는 촘촘히, 안쪽은 듬성듬성 재 본다. 전부 평평하면(높이 0) 네모 한 장으로 끝낸다.
                bool any = false;
                for (int i = 0; i < n && !any; i++)
                    any = HeightAt(new Vector3(cx + i * step, 0, cz)) != 0 || HeightAt(new Vector3(cx + i * step, 0, cz + ChunkSize)) != 0
                       || HeightAt(new Vector3(cx, 0, cz + i * step)) != 0 || HeightAt(new Vector3(cx + ChunkSize, 0, cz + i * step)) != 0;
                for (int j = 3; j < n && !any; j += 6)
                    for (int i = 3; i < n && !any; i += 6)
                        any = HeightAt(new Vector3(cx + i * step, 0, cz + j * step)) != 0;
                Mesh mesh, grassMesh;
                if (!any)
                {
                    // 평평한 덩어리: 아래 층(황무지)은 이웃과 살짝 겹치게 넉넉히(틈 방지), 위 층(풀)은 딱 맞게
                    mesh = Flat(cx - 0.1f, cz - 0.1f, cx + ChunkSize + 0.1f, cz + ChunkSize + 0.1f, UV);
                    grassMesh = Flat(cx, cz, cx + ChunkSize, cz + ChunkSize, UV);
                }
                else
                {
                    for (int j = 0; j < m; j++)
                        for (int i = 0; i < m; i++)
                            hgt[j * m + i] = HeightAt(new Vector3(cx + (i - 1) * step, 0, cz + (j - 1) * step));

                    var v = new Vector3[n * n];
                    var uv = new Vector2[v.Length];
                    var nrm = new Vector3[v.Length];
                    for (int j = 0; j < n; j++)
                        for (int i = 0; i < n; i++)
                        {
                            float x = cx + i * step, z = cz + j * step;
                            int k = (j + 1) * m + i + 1;
                            v[j * n + i] = new Vector3(x, hgt[k], z);
                            uv[j * n + i] = UV(x, z);
                            nrm[j * n + i] = new Vector3(hgt[k - 1] - hgt[k + 1], 2f * step, hgt[k - m] - hgt[k + m]).normalized;
                        }
                    var tri = new int[(n - 1) * (n - 1) * 6];
                    int ti = 0;
                    for (int j = 0; j < n - 1; j++)
                        for (int i = 0; i < n - 1; i++)
                        {
                            int a = j * n + i, b = a + 1, c = a + n, d = c + 1;
                            tri[ti++] = a; tri[ti++] = c; tri[ti++] = b;
                            tri[ti++] = b; tri[ti++] = c; tri[ti++] = d;
                        }
                    mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, vertices = v, uv = uv, normals = nrm, triangles = tri };
                    mesh.RecalculateBounds();
                    grassMesh = mesh;
                }

                var go = new GameObject("Hill", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = hillDead;
                go.GetComponent<MeshCollider>().sharedMesh = mesh;
                if (hillGrass)
                {
                    var grass = new GameObject("HillGrass", typeof(MeshFilter), typeof(MeshRenderer));
                    grass.transform.SetParent(go.transform, false);
                    grass.transform.localPosition = Vector3.up * 0.02f;
                    grass.GetComponent<MeshFilter>().sharedMesh = grassMesh;
                    grass.GetComponent<MeshRenderer>().sharedMaterial = hillGrass;
                }
            }
        ground.HidePlanes(); // 이제 땅은 전부 이 지형 메시다(평평한 판은 숨긴다)
    }

    // 평평한 네모 한 장(높이 0)
    static Mesh Flat(float x0, float z0, float x1, float z1, System.Func<float, float, Vector2> uv)
    {
        var mesh = new Mesh
        {
            vertices = new[] { new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), new Vector3(x0, 0, z1), new Vector3(x1, 0, z1) },
            uv = new[] { uv(x0, z0), uv(x1, z0), uv(x0, z1), uv(x1, z1) },
            normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up },
            triangles = new[] { 0, 2, 1, 1, 2, 3 },
        };
        mesh.RecalculateBounds();
        return mesh;
    }

    // ── 집 ──
    void BuildHamlets()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var walls = new Material[WallColors.Length];
        var roofs = new Material[RoofColors.Length];
        for (int i = 0; i < walls.Length; i++) walls[i] = new Material(lit) { color = WallColors[i] };
        for (int i = 0; i < roofs.Length; i++) roofs[i] = new Material(lit) { color = RoofColors[i] };
        var door = new Material(lit) { color = new Color(0.36f, 0.28f, 0.42f) };
        // 창문: 따뜻한 노란 불빛(밤에도 빛나 보이게 발광)
        window = new Material(lit) { color = new Color(1f, 0.85f, 0.5f) };
        window.EnableKeyword("_EMISSION");
        window.SetColor("_EmissionColor", new Color(1f, 0.75f, 0.35f) * 1.6f);
        var plaza = new Material(lit) { color = new Color(0.9f, 0.82f, 0.66f) };
        var roofMesh = Cone(Segments);
        var rng = new System.Random(11);

        for (int h = 0; h < Hamlets.Length; h++)
        {
            var ham = Hamlets[h];
            var center = new Vector3(ham.x, 0, ham.z);
            var root = new GameObject("Hamlet").transform;
            root.SetParent(transform, false);
            root.position = center;
            // 마당(흙 원판). 원주민 마을은 원주민이 서 있는 곳까지 덮는다.
            var yard = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(yard.GetComponent<Collider>());
            yard.name = "Yard";
            yard.transform.SetParent(root, false);
            yard.transform.localPosition = Vector3.up * 0.025f;
            yard.transform.localScale = new Vector3(ham.ring * 2f, 0.01f, ham.ring * 2f);
            yard.GetComponent<Renderer>().sharedMaterial = plaza;

            for (int k = 0; k < ham.count; k++)
            {
                float a = (k + 0.5f) / ham.count * Mathf.PI * 2 + h * 0.7f + (float)rng.NextDouble() * 0.3f;
                var pos = center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * ham.ring;
                if (HeightAt(pos) > 0.01f || Lake.Overlaps(pos, HutRadius + 2f) || Cliff.Blocks(pos, HutRadius + 2f)) continue;
                float size = 0.85f + (float)rng.NextDouble() * 0.35f;
                Hut(root, pos, center, size, walls[rng.Next(walls.Length)], roofs[rng.Next(roofs.Length)], door, roofMesh);
                blockers.Add((pos, HutRadius * size + 0.5f));
            }
        }
    }

    // 동그란 오두막: 원통 벽 + 원뿔 지붕 + 마당 쪽을 보는 문 + 동그란 창 두 개. 벽에 부딪힌다.
    static void Hut(Transform parent, Vector3 pos, Vector3 faceTo, float size, Material wall, Material roof, Material door, Mesh roofMesh)
    {
        var hut = new GameObject("Hut").transform;
        hut.SetParent(parent, true);
        hut.position = pos;
        var look = faceTo - pos; look.y = 0;
        hut.rotation = Quaternion.LookRotation(look.normalized);
        hut.localScale = Vector3.one * size;

        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(hut, false);
        body.transform.localPosition = Vector3.up * HutWall / 2f;
        body.transform.localScale = new Vector3(HutRadius * 2, HutWall / 2f, HutRadius * 2);
        body.GetComponent<Renderer>().sharedMaterial = wall;

        var r = new GameObject("Roof", typeof(MeshFilter), typeof(MeshRenderer));
        r.transform.SetParent(hut, false);
        r.transform.localPosition = Vector3.up * (HutWall - 0.1f);
        r.transform.localScale = new Vector3(HutRadius * 1.3f, HutRoof, HutRadius * 1.3f);
        r.GetComponent<MeshFilter>().sharedMesh = roofMesh;
        r.GetComponent<MeshRenderer>().sharedMaterial = roof;

        var d = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(d.GetComponent<Collider>());
        d.transform.SetParent(hut, false);
        d.transform.localPosition = new Vector3(0, 0.75f, HutRadius - 0.02f);
        d.transform.localScale = new Vector3(0.9f, 1.5f, 0.12f);
        d.GetComponent<Renderer>().sharedMaterial = door;
        foreach (float side in new[] { -1f, 1f })
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(w.GetComponent<Collider>());
            w.transform.SetParent(hut, false);
            float a = side * 0.75f;
            w.transform.localPosition = new Vector3(Mathf.Sin(a) * HutRadius, 1.5f, Mathf.Cos(a) * HutRadius);
            w.transform.localScale = new Vector3(0.5f, 0.5f, 0.15f);
            w.transform.localRotation = Quaternion.Euler(0, a * Mathf.Rad2Deg, 0);
            w.GetComponent<Renderer>().sharedMaterial = window;
        }

        var col = hut.gameObject.AddComponent<CapsuleCollider>();
        col.radius = HutRadius;
        col.height = HutWall + HutRoof;
        col.center = Vector3.up * (HutWall + HutRoof) / 2f;
    }

    static Material window;

    const int Segments = 24;

    // 밑면 반지름 1, 높이 1 원뿔(끝이 위)
    static Mesh Cone(int seg)
    {
        var v = new List<Vector3>();
        var tri = new List<int>();
        for (int i = 0; i < seg; i++)
        {
            float a0 = i / (float)seg * Mathf.PI * 2, a1 = (i + 1) / (float)seg * Mathf.PI * 2;
            int b = v.Count;
            v.Add(new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)));
            v.Add(new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)));
            v.Add(Vector3.up);
            tri.Add(b); tri.Add(b + 2); tri.Add(b + 1);
            // 밑면(처마 아래에서 보일 때)
            int c = v.Count;
            v.Add(new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)));
            v.Add(new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)));
            v.Add(Vector3.zero);
            tri.Add(c); tri.Add(c + 1); tri.Add(c + 2);
        }
        var m = new Mesh { vertices = v.ToArray(), triangles = tri.ToArray() };
        m.RecalculateNormals();
        return m;
    }
}
