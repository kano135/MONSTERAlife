using System.Collections.Generic;
using UnityEngine;

// 폭포 산: 맵 서쪽의 큰 바위산. 위에서 보면 찌그러진 오각형.
//   동쪽 면(기지 쪽)  = 계단식 폭포 — 꼭대기 샘 → 개울 → 폭포 3단(단마다 웅덩이) → 아래 큰 웅덩이 → 강 → 연못
//   북쪽·남쪽 면      = 깎아지른 절벽(못 오른다)
//   서쪽 두 면        = 오르는 쪽 — 비탈(걸어서)과 1m 돌턱(점프)이 섞여 꼭대기까지 이어진다
// 모양은 높이 함수(HeightAt) 하나로 정하고, 실제 땅(메시·충돌)은 Landscape 가 언덕과 함께 만든다.
// 여기서는 물·물보라·바위·나무·다리 같은 꾸밈만 올린다.
// ★ 자리·크기는 아래 Corners / Height / 폭포 숫자만 고치면 된다(단위: 미터).
public class Cliff : MonoBehaviour
{
    public static Cliff Instance { get; private set; }

    public const float Height = 22f;

    // 오각형 꼭짓점(위에서 봤을 때 시계 반대 방향): 동남 → 동북 → 북서 → 서 → 남서
    static readonly Vector2[] Corners =
    {
        new Vector2(-150f, -25f), new Vector2(-158f, 48f), new Vector2(-225f, 85f), new Vector2(-285f, 12f), new Vector2(-245f, -70f),
    };
    enum Side { Fall, Wall, Climb }
    // 변마다 성격: 동(폭포) · 북(절벽) · 북서(오르는 쪽) · 남서(오르는 쪽) · 남(절벽)
    static readonly Side[] Sides = { Side.Fall, Side.Wall, Side.Climb, Side.Climb, Side.Wall };

    const float WallRun = 1.4f; // 절벽이 바닥에서 꼭대기까지 올라가는 가로 폭(작을수록 수직)

    // 절벽 중간의 쉬는 턱(벽을 타다가 올라서서 숨을 고르는 작은 돌 선반): 변 번호, 변을 따라 위치(m), 높이(m).
    // 일부러 몇 개만 둔다 — 원래 길은 아니지만 이 길도 있다는 느낌. ★ 여기 줄을 늘리면 쉬워진다.
    static readonly (int side, float s, float y)[] Shelves =
    {
        (0, 9f, 12f), (0, 63f, 12f),        // 동쪽 면(폭포 양옆)
        (1, 22f, 12f), (1, 52f, 12.5f),     // 북쪽 절벽
        (4, 30f, 11.5f), (4, 72f, 12f),     // 남쪽 절벽
    };
    const float ShelfHalf = 1.8f, ShelfDepth = 1.9f;

    // 오르는 쪽: 한 구간(가로 ClimbPeriod)마다 ClimbRise 만큼 높아진다
    const float ClimbPeriod = 5.2f, ClimbRise = 2.8f;

    // ── 폭포 골짜기(동쪽 면에서 안쪽으로 파인 계단) ── 거리는 동쪽 면에서 안쪽으로 잰다
    const float FallAt = 0.55f;                    // 동쪽 변의 어디(0 = 남쪽 끝, 1 = 북쪽 끝)에 폭포가 있나
    const float Tier1 = 14f, Tier2 = 26f, Tier3 = 38f; // 폭포 세 줄기가 떨어지는 자리
    const float TierH1 = 7f, TierH2 = 14.5f;       // 중간 웅덩이 두 단의 높이
    const float StreamDepth = 0.6f;                // 꼭대기 개울이 파인 깊이
    const float SpringAt = 66f, SpringRadius = 6.5f; // 꼭대기 샘
    const float CoveHalf = 14f, TierHalf = 10f, StreamHalf = 4f; // 골짜기 반폭(아래 웅덩이 · 중간 단 · 개울)

    // 강: 아래 웅덩이에서 남동쪽 연못까지
    static readonly Vector2[] River =
    {
        new Vector2(-151f, 15f), new Vector2(-142f, 9f), new Vector2(-135f, -6f), new Vector2(-131f, -26f), new Vector2(-132f, -42f),
    };
    static readonly Vector2 PondCenter = new Vector2(-132f, -50f);
    const float RiverHalf = 3.2f, PondRadius = 9.5f, PoolRadius = 11f;

    static readonly Color WaterColor = new Color(0.62f, 0.72f, 1f, 0.82f); // 보랏빛 도는 하늘색

    Material fallMat, riverMat;
    readonly List<Transform> foam = new List<Transform>();

    // 골짜기 좌표: G0 = 동쪽 면 위 폭포 자리, In = 안쪽(서쪽) 방향, Along = 변을 따라가는 방향
    static Vector2 G0 => Vector2.Lerp(Corners[0], Corners[1], FallAt);
    static Vector2 Along => (Corners[1] - Corners[0]).normalized;
    static Vector2 In => new Vector2(-Along.y, Along.x);
    static Vector3 W(float inward, float lateral, float y)
    {
        var p = G0 + In * inward + Along * lateral;
        return new Vector3(p.x, y, p.y);
    }

    static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    // 이 자리의 산 높이(산 밖은 0)
    public static float HeightAt(float x, float z)
    {
        if (x > -138f || x < -295f || z < -82f || z > 96f) return 0;
        var p = new Vector2(x, z);
        float h = Height + 1.2f * Mathf.PerlinNoise(x * 0.06f + 50f, z * 0.06f); // 꼭대기도 살짝 울퉁불퉁
        float dFall = 0;
        for (int i = 0; i < Corners.Length; i++)
        {
            Vector2 a = Corners[i], b = Corners[(i + 1) % Corners.Length];
            var e = (b - a).normalized;
            var n = new Vector2(-e.y, e.x);
            float d = Vector2.Dot(p - a, n);
            float s = Vector2.Dot(p - a, e);
            if (i == 0) dFall = d;
            // 가장자리가 반듯하지 않게: 큰 굴곡 + 잔 굴곡(바위가 들쭉날쭉 튀어나온 것처럼)
            d += (Mathf.PerlinNoise(s * 0.07f, i * 7.3f) - 0.5f) * 3.5f + (Mathf.PerlinNoise(s * 0.33f, i * 3.1f + 20f) - 0.5f) * 2.6f;
            if (d <= 0) return 0;
            // 절벽 기울기도 자리마다 다르다(대부분 수직에 가깝고, 군데군데 밑동이 불룩)
            float bulge = Mathf.PerlinNoise(s * 0.11f, i * 5.3f + 40f);
            float run = WallRun + 5f * bulge * bulge * bulge;
            float wallH = (Height + 3f) * Smooth(d / run);
            if (Sides[i] != Side.Climb)
                foreach (var shelf in Shelves)
                {
                    if (shelf.side != i || Mathf.Abs(s - shelf.s) > ShelfHalf) continue;
                    float t01 = shelf.y / (Height + 3f);
                    float at = run * (0.5f - Mathf.Sin(Mathf.Asin(1f - 2f * t01) / 3f)); // 벽이 턱 높이에 닿는 자리
                    if (wallH > shelf.y && d < at + ShelfDepth) wallH = shelf.y;
                }
            h = Mathf.Min(h, Sides[i] == Side.Climb ? ClimbProfile(d, s, i) : wallH);
        }

        // 폭포 골짜기: 바닥 높이가 안쪽으로 갈수록 계단처럼 올라가고, 옆은 벽
        float lat = Vector2.Dot(p - G0, Along);
        if (dFall < SpringAt + SpringRadius + 3f)
        {
            // 단의 턱은 물이 떨어지는 가운데만 반듯하고, 양옆은 들쭉날쭉
            float jag = (Mathf.PerlinNoise(lat * 0.22f, 5.7f) - 0.5f) * 6f * Smooth((Mathf.Abs(lat) - 4.5f) / 4f);
            float dT = dFall + jag;
            float floor = TierH1 * Smooth(dT - Tier1 + 0.5f) + (TierH2 - TierH1) * Smooth(dT - Tier2 + 0.5f)
                        + (Height - StreamDepth - TierH2) * Smooth(dFall - Tier3 + 0.5f);
            float half = dFall < Tier1 ? CoveHalf : dFall < Tier3 ? TierHalf : StreamHalf;
            half += (Mathf.PerlinNoise(dFall * 0.18f, lat > 0 ? 3.1f : 9.7f) - 0.5f) * (dFall < Tier3 ? 6f : 1.5f); // 골짜기 벽도 양쪽이 다르게 굽는다
            float wall = (Height + 4f) * Smooth((Mathf.Abs(lat) - half) / 2.2f);
            if (dFall > SpringAt) wall = Height + 4f; // 개울은 샘에서 끝난다
            h = Mathf.Min(h, floor + wall);
            // 샘(동그란 웅덩이)
            float spring = (p - (G0 + In * SpringAt)).magnitude;
            h = Mathf.Min(h, Height - StreamDepth - 0.3f + (Height + 4f) * Smooth((spring - SpringRadius) / 1.5f));
        }
        return h;
    }

    // 오르는 쪽 높이: 가장자리에서 d 만큼 들어온 곳. 구간마다 비탈만 / 비탈+돌턱 / 돌턱 세 개 중 하나(자리에 따라 바뀜).
    static float ClimbProfile(float d, float s, int side)
    {
        d = Mathf.Max(0, d - 1.5f + (Mathf.PerlinNoise(s * 0.05f, side * 3.7f + 11f) - 0.5f) * 4f);
        float cycles = d / ClimbPeriod;
        int k = Mathf.FloorToInt(cycles);
        float u = cycles - k;
        float kind = Mathf.PerlinNoise(s * 0.035f + side * 9.1f, k * 0.61f);
        float rise;
        if (kind > 0.62f) rise = u;                                   // 비탈만(약 28도, 걸어서)
        else if (kind > 0.4f) rise = u < 0.58f ? u / 0.58f * 0.64f    // 비탈 →
            : 0.64f + 0.36f * Smooth((u - 0.58f) / 0.08f);            //   → 1m 돌턱(점프)
        else rise = 0.357f * Smooth((u - 0.25f) / 0.07f) + 0.322f * Smooth((u - 0.55f) / 0.07f) + 0.321f * Smooth((u - 0.85f) / 0.07f); // 돌턱 셋(점프 세 번)
        return (k + rise) * ClimbRise;
    }

    // 물이 고인 높이(땅보다 낮다)와 파인 깊이
    public const float WaterLevel = -0.35f;
    const float RiverBed = 1.0f, BowlDepth = 2.8f, Bank = 2.2f; // 강바닥 깊이 · 웅덩이 깊이(키를 넘는다) · 물가 비탈 폭

    // 이 자리가 땅보다 얼마나 파였나(0 또는 음수). 강 · 폭포 아래 웅덩이(골짜기 안쪽 포함) · 강 끝 연못.
    public static float CarveAt(float x, float z)
    {
        if (x > -112f || x < -182f || z < -66f || z > 40f) return 0;
        var p = new Vector2(x, z);
        float c = -RiverBed * Smooth((RiverHalf + Bank - RiverDistance(p)) / (Bank + 1f));
        c = Mathf.Min(c, -BowlDepth * Smooth((PondRadius + Bank - (p - PondCenter).magnitude) / (Bank + 3f)));
        c = Mathf.Min(c, -BowlDepth * Smooth((PoolRadius + Bank - (p - (G0 - In * 3f)).magnitude) / (Bank + 3f)));
        float dFall = Vector2.Dot(p - G0, In), lat = Vector2.Dot(p - G0, Along);
        if (dFall > -4f && dFall < Tier1 + 1f)
            c = Mathf.Min(c, -BowlDepth * Smooth((CoveHalf - Mathf.Abs(lat)) / 3f) * Smooth((Tier1 + 0.5f - dFall) / 2f));
        return c;
    }

    // 발이 물에 얼마나 잠겼나(m). 물 밖이면 0. 파인 곳(강·웅덩이·연못)과 바다의 물만 센다.
    public static float Submerged(Vector3 feet)
    {
        if (feet.y >= WaterLevel) return 0;
        if (CarveAt(feet.x, feet.z) > -0.3f && Landscape.ShoreDistance(feet) > GroundView.Beach) return 0;
        return WaterLevel - feet.y;
    }

    // 폭포 물줄기 속인가(물이 위에서 때려서 타고 오르기 힘들다)
    public static bool InWaterfall(Vector3 pos)
    {
        var p = new Vector2(pos.x, pos.z);
        float dFall = Vector2.Dot(p - G0, In), lat = Mathf.Abs(Vector2.Dot(p - G0, Along));
        float top = Height - StreamDepth;
        return Under(Tier3, StreamHalf, TierH2, top) || Under(Tier2, TierHalf * 0.62f, TierH1, TierH2) || Under(Tier1, TierHalf * 0.85f, WaterLevel, TierH1);
        bool Under(float tier, float half, float y0, float y1) => dFall > tier - 2.6f && dFall < tier + 1f && lat < half && pos.y > y0 - 1f && pos.y < y1 + 1.5f;
    }

    static float RiverDistance(Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i < River.Length - 1; i++)
        {
            Vector2 a = River[i], b = River[i + 1];
            float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
            best = Mathf.Min(best, (p - (a + (b - a) * t)).magnitude);
        }
        return best;
    }

    // 이 점(+여유)이 물(웅덩이·강·연못·폭포 골짜기·샘)이나 가파른 바위면 true. 씨앗·나무·돌이 여기 생기지 않게 쓴다.
    public static bool Blocks(Vector3 pos, float margin)
    {
        var p = new Vector2(pos.x, pos.z);
        if ((p - new Vector2(SecretCave.Mouth.x, SecretCave.Mouth.z)).magnitude < 9f + margin) return true; // 비밀 동굴 입구 앞
        if (RiverDistance(p) < RiverHalf + Bank + 0.5f + margin) return true;
        if ((p - PondCenter).magnitude < PondRadius + Bank + 0.5f + margin) return true;
        if ((p - (G0 - In * 3f)).magnitude < PoolRadius + Bank + 0.5f + margin) return true;
        float h = HeightAt(pos.x, pos.z);
        if (h <= 0)
        {
            // 산 바로 밑(벽에 붙은 자리)
            return HeightAt(pos.x - margin, pos.z) > 0 || HeightAt(pos.x + margin, pos.z) > 0 || HeightAt(pos.x, pos.z - margin) > 0 || HeightAt(pos.x, pos.z + margin) > 0;
        }
        float dFall = Vector2.Dot(p - G0, In), lat = Vector2.Dot(p - G0, Along);
        if (dFall < SpringAt + SpringRadius + margin && Mathf.Abs(lat) < CoveHalf + 2f + margin) return true;
        // 가파른 곳(절벽 면·돌턱)
        float r = Mathf.Max(1.2f, margin);
        return Mathf.Abs(HeightAt(pos.x + r, pos.z) - HeightAt(pos.x - r, pos.z)) > 1.3f || Mathf.Abs(HeightAt(pos.x, pos.z + r) - HeightAt(pos.x, pos.z - r)) > 1.3f;
    }

    // 이 자리가 물인가(웅덩이·강·연못·폭포 단·개울·샘). 지도 그리기 등에 쓴다.
    public static bool IsWater(Vector3 pos)
    {
        var p = new Vector2(pos.x, pos.z);
        if (RiverDistance(p) < RiverHalf || (p - PondCenter).magnitude < PondRadius || (p - (G0 - In * 3f)).magnitude < PoolRadius) return true;
        float dFall = Vector2.Dot(p - G0, In), lat = Vector2.Dot(p - G0, Along);
        if ((p - (G0 + In * SpringAt)).magnitude < SpringRadius) return true;
        if (dFall < -2f || dFall > SpringAt) return false;
        return Mathf.Abs(lat) < (dFall < Tier1 ? CoveHalf : dFall < Tier3 ? TierHalf : StreamHalf);
    }

    public static Cliff Build()
    {
        var cliff = new GameObject("Cliff").AddComponent<Cliff>();
        Instance = cliff;
        var t = cliff.transform;
        var rng = new System.Random(21);
        float R() => (float)rng.NextDouble();

        cliff.riverMat = ArtLibrary.Transparent(FlowTexture(), WaterColor);
        cliff.fallMat = ArtLibrary.Transparent(FlowTexture(), new Color(0.92f, 0.94f, 1f, 0.86f));
        cliff.fallMat.renderQueue = 3001; // 웅덩이 위에 그려지게
        var still = ArtLibrary.Transparent(null, new Color(0.56f, 0.66f, 0.98f, 0.85f));
        var foamMat = ArtLibrary.Transparent(null, new Color(1f, 1f, 1f, 0.75f));
        float top = Height - StreamDepth;

        // 꼭대기: 샘 → 개울
        Disc(t, "Spring", W(SpringAt, 0, top - 0.12f), SpringRadius + 0.6f, still);
        Sheet(t, "Stream", W(Tier3 - 0.5f, -StreamHalf, top + 0.1f), W(Tier3 - 0.5f, StreamHalf, top + 0.1f), W(SpringAt, StreamHalf, top + 0.1f), W(SpringAt, -StreamHalf, top + 0.1f), cliff.riverMat, true);
        // 중간 웅덩이 두 단
        Sheet(t, "Pool_Upper", W(Tier2 - 0.3f, -TierHalf - 1f, TierH2 + 0.18f), W(Tier2 - 0.3f, TierHalf + 1f, TierH2 + 0.18f), W(Tier3 + 0.6f, TierHalf + 1f, TierH2 + 0.18f), W(Tier3 + 0.6f, -TierHalf - 1f, TierH2 + 0.18f), still, false);
        Sheet(t, "Pool_Middle", W(Tier1 - 0.3f, -TierHalf - 1f, TierH1 + 0.18f), W(Tier1 - 0.3f, TierHalf + 1f, TierH1 + 0.18f), W(Tier2 + 0.6f, TierHalf + 1f, TierH1 + 0.18f), W(Tier2 + 0.6f, -TierHalf - 1f, TierH1 + 0.18f), still, false);
        // 아래 큰 웅덩이: 골짜기 안쪽 + 바깥으로 둥글게
        // 아래 큰 웅덩이: 땅을 파서 물이 땅보다 낮다(골짜기 안쪽 + 바깥으로 둥글게)
        Sheet(t, "Pool_Cove", W(-2f, -CoveHalf - 1f, WaterLevel + 0.01f), W(-2f, CoveHalf + 1f, WaterLevel + 0.01f), W(Tier1 + 0.6f, CoveHalf + 1f, WaterLevel + 0.01f), W(Tier1 + 0.6f, -CoveHalf - 1f, WaterLevel + 0.01f), still, false);
        Disc(t, "Pool", W(-3f, 0, WaterLevel), PoolRadius + Bank, still);
        // 폭포 세 줄기(위는 좁고 아래로 갈수록 넓다)
        cliff.Fall(t, Tier3, StreamHalf, top + 0.15f, TierH2, foamMat);
        cliff.Fall(t, Tier2, TierHalf * 0.62f, TierH2 + 0.2f, TierH1, foamMat);
        cliff.Fall(t, Tier1, TierHalf * 0.85f, TierH1 + 0.2f, WaterLevel, foamMat);

        // 강 + 연못
        var pts = new List<Vector3>();
        foreach (var r in River) pts.Add(new Vector3(r.x, WaterLevel + 0.02f, r.y));
        Ribbon(t, "River", pts, RiverHalf + Bank, cliff.riverMat, true);
        Disc(t, "Pond", new Vector3(PondCenter.x, WaterLevel, PondCenter.y), PondRadius + Bank, still);

        // 통나무 다리(강 가운데쯤)
        var mid = (River[2] + River[3]) / 2f;
        var flow = (River[3] - River[2]).normalized;
        var bridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bridge.name = "Bridge";
        bridge.transform.SetParent(t, false);
        bridge.transform.position = new Vector3(mid.x, 0.22f, mid.y);
        bridge.transform.rotation = Quaternion.LookRotation(new Vector3(-flow.y, 0, flow.x));
        bridge.transform.localScale = new Vector3(2f, 0.3f, (RiverHalf + Bank) * 2f + 3f);
        bridge.GetComponent<Renderer>().material.color = new Color(0.62f, 0.44f, 0.3f);

        // 바위: 웅덩이 둘레·강가·강 한가운데·중간 단
        for (int i = 0; i < 12; i++)
        {
            float a = i / 12f * Mathf.PI * 2f + R() * 0.3f;
            var c = W(-3f, 0, 0);
            var pos = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (PoolRadius + 0.5f + R() * 1.5f);
            if (HeightAt(pos.x, pos.z) > 0.5f) continue;
            Rock(t, pos, 1.2f + R() * 1.4f, i);
            if (i % 3 == 0) Rock(t, pos + new Vector3(R() * 3f - 1.5f, 0, R() * 3f - 1.5f), 0.7f + R() * 0.6f, i + 90);
        }
        for (int i = 0; i < River.Length - 1; i++)
            for (int k = 0; k < 4; k++)
            {
                var p = Vector2.Lerp(River[i], River[i + 1], (k + R() * 0.6f) / 4f);
                var dir = (River[i + 1] - River[i]).normalized;
                var side = new Vector2(-dir.y, dir.x) * (k % 3 == 1 ? (R() - 0.5f) * 3f : (k % 2 == 0 ? 1 : -1) * (RiverHalf + 0.4f + R()));
                Rock(t, new Vector3(p.x + side.x, 0, p.y + side.y), k % 3 == 1 ? 0.7f + R() * 0.5f : 1f + R() * 1.2f, i * 4 + k);
            }
        for (int i = 0; i < 6; i++)
        {
            float tier = i < 3 ? TierH1 : TierH2;
            float d = (i < 3 ? Tier1 : Tier2) + 2f + R() * 8f;
            Rock(t, W(d, (R() - 0.5f) * TierHalf * 1.6f, tier), 0.8f + R() * 0.8f, i + 40);
        }

        Boulders(t, rng);
        Dress(t, rng);
        return cliff;
    }

    // 폭포 한 줄기: 턱(inward)에서 살짝 앞에 세운 물 판 + 아래 물보라
    void Fall(Transform t, float inward, float half, float yTop, float yBottom, Material foamMat)
    {
        float d = inward - 0.9f;
        Sheet(t, "Waterfall", W(d, -half, yBottom), W(d, half, yBottom), W(d + 0.5f, half, yTop), W(d + 0.5f, -half, yTop), fallMat, false);
        int count = Mathf.Max(3, Mathf.RoundToInt(half * 1.1f));
        for (int i = 0; i < count; i++)
        {
            var f = PrototypeBootstrap.Part(t, PrimitiveType.Sphere, W(d - 0.6f, Mathf.Lerp(-half + 0.5f, half - 0.5f, i / (count - 1f)), yBottom + 0.35f), Vector3.one * 2f, Color.white);
            f.GetComponent<Renderer>().sharedMaterial = foamMat;
            f.name = "Foam";
            foam.Add(f.transform);
        }
    }

    // 변 i 의 s 지점에서 벽이 실제로 시작하는 자리(가장자리가 구불구불해서 찾아야 한다). n = 안쪽 방향
    static bool WallFoot(int i, float s, out Vector2 foot, out Vector2 n, out Vector2 e)
    {
        Vector2 a = Corners[i], b = Corners[(i + 1) % Corners.Length];
        e = (b - a).normalized;
        n = new Vector2(-e.y, e.x);
        var q = a + e * s;
        for (float inward = -7f; inward < 10f; inward += 0.5f)
            if (HeightAt(q.x + n.x * inward, q.y + n.y * inward) >= 1f) { foot = q + n * inward; return true; }
        foot = q;
        return false;
    }

    static bool InFrontOfFalls(Vector2 p) => Mathf.Abs(Vector2.Dot(p - G0, Along)) < CoveHalf + 5f && Vector2.Dot(p - G0, In) < Tier3;

    // 절벽 바위: 성벽처럼 반듯해 보이지 않게 벽 밑동에 굴러떨어진 돌, 벽 중간에 박혀 튀어나온 돌, 꼭대기 가장자리의 돌을 붙인다.
    static void Boulders(Transform t, System.Random rng)
    {
        float R() => (float)rng.NextDouble();
        int index = 200;
        for (int i = 0; i < Corners.Length; i++)
        {
            float len = (Corners[(i + 1) % Corners.Length] - Corners[i]).magnitude;
            bool climb = Sides[i] == Side.Climb;
            for (float s = 4f; s < len - 4f; s += (climb ? 13f : 6.5f) * (0.7f + R() * 0.6f))
            {
                if (!WallFoot(i, s, out var foot, out var n, out var e) || InFrontOfFalls(foot)) continue;
                var p1 = foot - n * (0.5f + R() * 2.5f);
                if (CarveAt(p1.x, p1.y) > -0.05f) Rock(t, new Vector3(p1.x, 0, p1.y), 1.4f + R() * 2.4f, index++, 0.2f);
                if (climb) continue;
                // 쉬는 턱이 있는 길(턱 좌우 10m)은 튀어나온 바위로 막지 않는다 — 그 길은 끝까지 오를 수 있어야 한다
                bool route = false;
                foreach (var shelf in Shelves) route |= shelf.side == i && Mathf.Abs(s - shelf.s) < 10f;
                if (route) continue;
                int count = 1 + rng.Next(2);
                for (int k = 0; k < count; k++)
                {
                    var p2 = foot + n * (0.5f + R() * 0.9f) + e * (R() * 4f - 2f);
                    Rock(t, new Vector3(p2.x, 2.5f + R() * (Height - 5f), p2.y), 1.8f + R() * 1.6f, index++, 0.35f, false);
                }
                if (R() < 0.5f)
                {
                    var p3 = foot + n * (3f + R() * 2f);
                    Rock(t, new Vector3(p3.x, 0, p3.y), 1.2f + R() * 1.6f, index++, 0.25f);
                }
            }
        }
        // 폭포 골짜기의 벽과 단 모서리
        for (int k = 0; k < 16; k++)
        {
            float d = 2f + R() * (Tier3 - 2f);
            float half = d < Tier1 ? CoveHalf : TierHalf;
            var pos = W(d, (k % 2 == 0 ? 1f : -1f) * (half + R() * 1.5f), 0);
            pos.y = Mathf.Max(1f, HeightAt(pos.x, pos.z) * (0.25f + R() * 0.6f));
            Rock(t, pos, 1.6f + R() * 2.2f, index++, 0.3f, false);
        }
    }

    static readonly string[] DecorTrees = { "tree_cherry_02", "tree_oak_01", "tree_birch_01", "tree_autumn_maple_01" };

    static void Tree(Transform t, System.Random rng, Vector3 pos, float scale)
    {
        pos.y = Landscape.HeightAt(pos);
        string name = DecorTrees[rng.Next(DecorTrees.Length)];
        var tree = NaturePack.Spawn(name, t, pos, (float)rng.NextDouble() * 360f, scale);
        if (tree && (name.Contains("oak") || name.Contains("birch"))) NaturePack.Recolor(tree, NaturePack.Tint.Teal);
    }

    static void Bush(Transform t, System.Random rng, Vector3 pos, float scale)
    {
        pos.y = Landscape.HeightAt(pos);
        NaturePack.Spawn("bush_flower_01", t, pos, (float)rng.NextDouble() * 360f, scale);
    }

    // 산 위·둘레 꾸미기(팩이 있을 때): 꼭대기·오르는 길의 나무, 절벽 밑과 가장자리의 나무·덤불, 폭포 단의 덤불, 웅덩이 둘레 야자수
    static void Dress(Transform t, System.Random rng)
    {
        if (!NaturePack.Available) return;
        float R() => (float)rng.NextDouble();
        // 꼭대기와 오르는 비탈 곳곳
        int placed = 0;
        for (int tries = 0; tries < 500 && placed < 40; tries++)
        {
            var pos = new Vector3(-290f + R() * 150f, 0, -75f + R() * 165f);
            if (HeightAt(pos.x, pos.z) < 2f || Blocks(pos, 2f)) continue;
            Tree(t, rng, pos, 1.3f + R() * 0.7f);
            placed++;
        }
        // 절벽을 따라: 밑에 나무와 덤불, 위 가장자리에 나무
        for (int i = 0; i < Corners.Length; i++)
        {
            if (Sides[i] == Side.Climb) continue;
            float len = (Corners[(i + 1) % Corners.Length] - Corners[i]).magnitude;
            for (float s = 5f; s < len - 5f; s += 7f + R() * 6f)
            {
                if (!WallFoot(i, s, out var foot, out var n, out var e) || InFrontOfFalls(foot)) continue;
                var below = foot - n * (4f + R() * 4f);
                var bp = new Vector3(below.x, 0, below.y);
                if (CarveAt(below.x, below.y) > -0.05f && HeightAt(below.x, below.y) < 0.5f) Tree(t, rng, bp, 1.2f + R() * 0.8f);
                var bush = foot - n * (1.5f + R() * 2f) + e * (R() * 6f - 3f);
                if (CarveAt(bush.x, bush.y) > -0.05f) Bush(t, rng, new Vector3(bush.x, 0, bush.y), 1f + R() * 0.6f);
                var above = foot + n * (4.5f + R() * 3f);
                var ap = new Vector3(above.x, 0, above.y);
                if (HeightAt(above.x, above.y) > Height - 1f && !Blocks(ap, 1.5f)) Tree(t, rng, ap, 1.1f + R() * 0.6f);
            }
        }
        // 폭포 단 양옆(물가)의 덤불과 작은 나무
        foreach (var (d0, y) in new[] { (Tier1, TierH1), (Tier2, TierH2) })
            for (int k = 0; k < 6; k++)
            {
                var pos = W(d0 + 2.5f + R() * 7f, (k % 2 == 0 ? 1f : -1f) * (TierHalf - 1.2f - R() * 1.5f), y);
                if (Mathf.Abs(HeightAt(pos.x, pos.z) - y) > 0.8f) continue;
                if (k < 2) Tree(t, rng, pos, 0.7f + R() * 0.3f); else Bush(t, rng, pos, 0.9f + R() * 0.5f);
            }
        // 아래 웅덩이·강 끝 연못 둘레
        var pool = W(-3f, 0, 0);
        for (int i = 0; i < 12; i++)
        {
            float a = i / 12f * Mathf.PI * 2f;
            var pos = pool + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (PoolRadius + Bank + 1.5f + R() * 3f);
            if (HeightAt(pos.x, pos.z) > 0.5f || CarveAt(pos.x, pos.z) < -0.05f) continue;
            if (i % 4 == 0) NaturePack.Spawn("tree_palm_02", t, pos, R() * 360f, 1f + R() * 0.3f);
            else if (i % 4 == 2) NaturePack.Spawn("basalt_columns_02", t, pos, R() * 360f, 1.1f + R() * 0.4f);
            else Bush(t, rng, pos, 1f + R() * 0.5f);
        }
        var pond = new Vector3(PondCenter.x, 0, PondCenter.y);
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f + 0.4f;
            var pos = pond + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (PondRadius + Bank + 1.5f + R() * 2f);
            if (CarveAt(pos.x, pos.z) < -0.05f) continue;
            if (i % 3 == 0) NaturePack.Spawn("tree_palm_02", t, pos, R() * 360f, 1f + R() * 0.4f); else Bush(t, rng, pos, 1f + R() * 0.4f);
        }
    }

    // 바위 하나(절벽과 같은 돌 색). 땅(파인 곳이면 그 바닥)에 살짝 묻어 놓는다. sink = 바위 크기 대비 묻는 비율.
    static GameObject Rock(Transform t, Vector3 pos, float scale, int index, float sink = 0.12f, bool onGround = true)
    {
        if (onGround) pos.y = Landscape.HeightAt(pos);
        pos.y -= sink * scale;
        var rock = NaturePack.Spawn(NaturePack.Rocks[index & 1], t, pos, index * 67f, scale);
        if (rock)
        {
            rock.transform.localRotation = Quaternion.Euler((index * 37) % 30 - 15, index * 67f, (index * 53) % 30 - 15) * rock.transform.localRotation;
            NaturePack.Recolor(rock, NaturePack.Tint.Stone);
            if (!NaturePack.HasCollider(rock)) rock.AddComponent<SphereCollider>().center = Vector3.up * 0.5f;
            return rock;
        }
        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Rock";
        ball.transform.SetParent(t, false);
        ball.transform.position = pos + Vector3.up * 0.3f * scale;
        ball.transform.localScale = new Vector3(1.4f, 0.9f, 1.2f) * scale;
        ball.GetComponent<Renderer>().material.color = Landscape.RockColor * (0.9f + (index % 3) * 0.06f);
        return ball;
    }

    void Update()
    {
        // 물이 흐르는 것처럼 무늬를 움직인다
        float time = Time.time;
        if (fallMat) fallMat.mainTextureOffset = new Vector2(0, time * 0.9f);
        if (riverMat) riverMat.mainTextureOffset = new Vector2(-time * 0.35f, 0);
        for (int i = 0; i < foam.Count; i++)
            foam[i].localScale = Vector3.one * (1.8f + Mathf.Sin(time * 3f + i * 1.3f) * 0.35f);
    }

    // ── 물 판 만들기(양면, 충돌 없음) ──
    // 네 점을 잇는 판. alongU = 무늬가 a→d 방향으로 흐르게(개울), 아니면 세로로(폭포·고인 물)
    static void Sheet(Transform parent, string name, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Material mat, bool alongU)
    {
        float w = (b - a).magnitude / 4f, l = (d - a).magnitude / 4f;
        var uv = alongU ? new[] { new Vector2(0, 0), new Vector2(0, w), new Vector2(l, w), new Vector2(l, 0) }
                        : new[] { new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, l), new Vector2(0, l) };
        var mesh = new Mesh { vertices = new[] { a, b, c, d }, uv = uv, triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 } };
        mesh.RecalculateBounds();
        Spawn(parent, name, mesh, mat);
    }

    static void Disc(Transform parent, string name, Vector3 center, float radius, Material mat)
    {
        const int seg = 32;
        var v = new Vector3[seg + 1];
        var uv = new Vector2[seg + 1];
        var tri = new int[seg * 3];
        v[0] = center; uv[0] = Vector2.one * 0.5f;
        for (int i = 0; i < seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            v[i + 1] = center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
            uv[i + 1] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.5f + Vector2.one * 0.5f;
            int n = (i + 1) % seg + 1;
            tri[i * 3] = 0; tri[i * 3 + 1] = n; tri[i * 3 + 2] = i + 1; // 윗면만(위를 본다)
        }
        var mesh = new Mesh { vertices = v, uv = uv, triangles = tri };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        Spawn(parent, name, mesh, mat);
    }

    // 점들을 따라가는 띠(강). 무늬는 흐르는 방향으로
    public static void Ribbon(Transform parent, string name, List<Vector3> pts, float half, Material mat, bool smoothen)
    {
        // 꺾이는 곳이 부드럽게 중간 점을 넣는다(smoothen 이 꺼져 있으면 받은 점 그대로)
        var smooth = new List<Vector3>();
        if (!smoothen) smooth.AddRange(pts);
        for (int i = 0; smoothen && i < pts.Count - 1; i++)
            for (int k = 0; k < 6; k++)
            {
                float u = k / 6f;
                Vector3 p0 = pts[Mathf.Max(i - 1, 0)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(i + 2, pts.Count - 1)];
                smooth.Add(0.5f * (2f * p1 + (p2 - p0) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u + (3f * p1 - p0 - 3f * p2 + p3) * u * u * u));
            }
        if (smoothen) smooth.Add(pts[pts.Count - 1]);
        var v = new Vector3[smooth.Count * 2];
        var uv = new Vector2[v.Length];
        var tri = new List<int>();
        float len = 0;
        for (int i = 0; i < smooth.Count; i++)
        {
            var dir = (smooth[Mathf.Min(i + 1, smooth.Count - 1)] - smooth[Mathf.Max(i - 1, 0)]).normalized;
            var side = new Vector3(-dir.z, 0, dir.x);
            if (i > 0) len += (smooth[i] - smooth[i - 1]).magnitude;
            v[i * 2] = smooth[i] - side * half; v[i * 2 + 1] = smooth[i] + side * half;
            uv[i * 2] = new Vector2(len / 4f, 0); uv[i * 2 + 1] = new Vector2(len / 4f, half / 2f);
            if (i == 0) continue;
            int a = (i - 1) * 2;
            tri.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 }); // 윗면만(위를 본다)
        }
        var mesh = new Mesh { vertices = v, uv = uv, triangles = tri.ToArray() };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        Spawn(parent, name, mesh, mat);
    }

    static void Spawn(Transform parent, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // 흐르는 물 무늬: 세로 줄무늬(밝고 어두운 줄이 섞여 물줄기처럼 보인다). 세로로 이어 붙는다.
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
