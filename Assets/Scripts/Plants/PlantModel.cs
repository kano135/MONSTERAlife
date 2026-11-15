using System.Collections.Generic;
using UnityEngine;

// 식물 3D 모델을 코드로 빚는다(로우폴리, 면마다 각진 음영). 10종 × 성장 5단계 + 변종 색 + 썩은 모습 + 씨앗.
//   0 씨앗(흙에 박힌 알) · 1 발아(떡잎 두 장) · 2 성장(잎만) · 3 성숙(꽃봉오리) · 4 개화(꽃)
// 모양은 예전 GPT 그림(Resources/Art/Plants)을 본떴다. 색만 다른 조각은 한 메시의 서브메시로 묶어 가볍다.
// ★ 식물마다 아래 함수 하나(Dandelion, Sunflower …)라서 모양을 고치려면 그 함수의 숫자만 바꾸면 된다(단위: 미터).
public static class PlantModel
{
    static Color C(int r, int g, int b) => new Color32((byte)r, (byte)g, (byte)b, 255);

    static readonly Color Leaf = C(142, 196, 84), LeafDark = C(104, 166, 76), LeafLight = C(180, 216, 108), Stalk = C(156, 188, 84);
    static readonly Color SeedBrown = C(146, 100, 62), Yellow = C(255, 212, 72), Cream = C(255, 240, 208), White = C(250, 250, 244);
    static readonly Color Brown = C(126, 78, 44), Rotten = C(92, 66, 42), Sparkle = C(255, 255, 236);

    // 잎·꽃잎 윤곽(밑동→끝, 폭 비율)
    static readonly float[] LeafP = { 0f, 0.8f, 1f, 0.65f, 0f };
    static readonly float[] SmallLeafP = { 0f, 1f, 0.8f, 0f };
    static readonly float[] PetalP = { 0.3f, 0.85f, 1f, 0.7f, 0f };
    static readonly float[] PointedP = { 0.35f, 1f, 0.75f, 0.4f, 0f };
    static readonly float[] ThinP = { 0.7f, 1f, 1f, 0.7f, 0f };
    static readonly float[] StrapP = { 0.55f, 1f, 1f, 0.75f, 0f };
    static readonly float[] PaddleP = { 0.3f, 0.35f, 0.7f, 1f, 0.55f };
    static readonly float[] HeartP = { 0.25f, 1f, 0.95f, 0.55f, 0f };
    static readonly float[] LobedP = { 0.2f, 0.85f, 0.4f, 1f, 0.5f, 0.8f, 0f };

    const float Scale = 1.25f; // 식물 전체 크기 배율

    static readonly Dictionary<string, (Mesh mesh, Material[] mats)> cache = new Dictionary<string, (Mesh, Material[])>();
    static readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();

    // 밭에 심은 식물 한 포기
    public static GameObject Build(PlantData p, int stage, bool variant, bool rotten)
    {
        string key = $"{p.id}|{stage}|{variant}|{rotten}";
        if (!cache.TryGetValue(key, out var built) || !built.mesh)
        {
            var s = new Sculpt();
            if (rotten) s.Recolor = c => Color.Lerp(c, Rotten, 0.78f);
            Shape(s, p, stage, variant);
            // 자랄수록 커진다. 전체 배율 Scale 로 밭 한 칸(1.9m)에 알맞게.
            float g = Scale * (stage <= 0 ? 1f : stage == 1 ? 1.5f : stage == 2 ? 0.62f : stage == 3 ? 0.85f : 1f);
            // 썩으면 갈색으로 변하고 옆으로 축 처진다
            var m = Matrix4x4.TRS(Vector3.zero, rotten ? Quaternion.Euler(0, 0, 38f) : Quaternion.identity,
                rotten ? new Vector3(g, g * 0.75f, g) : Vector3.one * g);
            cache[key] = built = s.ToMesh(m);
        }
        return Spawn("Visual", built);
    }

    // 씨앗(들판에서 줍는 것·손에 든 것): 빛나는 꼬투리 + 받침 잎 + 새싹. 높이 약 0.6m, 식물 색이 살짝 돈다.
    public static GameObject BuildSeed(Color tint)
    {
        Color32 k = tint;
        string key = $"seed|{k.r},{k.g},{k.b}";
        if (!cache.TryGetValue(key, out var built) || !built.mesh)
        {
            var s = new Sculpt();
            var pod = Color.Lerp(C(255, 216, 128), tint, 0.6f);
            s.Glow(pod);
            s.Lathe(pod, Vector3.zero, Quaternion.identity, new[] { 0f, 0f, 0.16f, 0.05f, 0.22f, 0.16f, 0.2f, 0.28f, 0.12f, 0.38f, 0.03f, 0.44f }, 8);
            s.Ring(C(216, 226, 152), Vector3.up * 0.02f, Quaternion.identity, 6, 0.08f, 0.2f, 0.15f, 35f, PetalP, -0.3f);
            s.Ball(Leaf, new Vector3(0, 0.43f, 0), new Vector3(0.1f, 0.04f, 0.1f), null, 6, 3);
            s.Tube(Leaf, new Vector3(0, 0.44f, 0), new Vector3(0.03f, 0.56f, 0), 0.016f, 0.01f, 5);
            s.Blade(LeafLight, new Vector3(0.03f, 0.56f, 0), new Vector3(1, 0.5f, 0), Vector3.up, 0.17f, 0.1f, LeafP, 0.2f);
            cache[key] = built = s.ToMesh(Matrix4x4.identity);
        }
        return Spawn("SeedModel", built);
    }

    static GameObject Spawn(string name, (Mesh mesh, Material[] mats) built)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.GetComponent<MeshFilter>().sharedMesh = built.mesh;
        go.GetComponent<MeshRenderer>().sharedMaterials = built.mats;
        return go;
    }

    static Material MaterialFor(Color32 c, bool glow)
    {
        int key = c.r | c.g << 8 | c.b << 16 | (glow ? 1 << 24 : 0);
        if (materials.TryGetValue(key, out var m) && m) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = c };
        m.SetFloat("_Smoothness", 0.15f);
        if (glow)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", (Color)c * 0.3f);
        }
        return materials[key] = m;
    }

    // ── 식물별 모양 ──
    static void Shape(Sculpt s, PlantData p, int stage, bool variant)
    {
        if (stage <= 0)
        {
            // 씨앗: 흙에 반쯤 박힌 알 + 작은 싹
            s.Ball(SeedBrown, new Vector3(0, 0.03f, 0), new Vector3(0.07f, 0.05f, 0.09f), null, 6, 4);
            s.Tube(LeafLight, new Vector3(0, 0.06f, 0), new Vector3(0.012f, 0.12f, 0), 0.012f, 0.004f, 4);
            return;
        }
        if (stage == 1)
        {
            // 발아: 줄기 + 떡잎 두 장
            var top = new Vector3(0, 0.17f, 0);
            s.Tube(Stalk, Vector3.zero, top, 0.016f, 0.012f, 5);
            s.Blade(LeafLight, top, new Vector3(1, 0.55f, 0), Vector3.up, 0.14f, 0.1f, LeafP, 0.2f);
            s.Blade(LeafLight, top, new Vector3(-1, 0.55f, 0), Vector3.up, 0.14f, 0.1f, LeafP, 0.2f);
            return;
        }
        var main = variant ? p.variantColor : p.color;
        int.TryParse(p.id.Length >= 2 ? p.id.Substring(0, 2) : "", out int kind);
        switch (kind)
        {
            case 1: Dandelion(s, stage, main); break;
            case 2: Sunflower(s, stage, main); break;
            case 3: Mimosa(s, stage, main); break;
            case 4: Flytrap(s, stage, main, variant); break;
            case 5: Sundew(s, stage, main); break;
            case 6: Pitcher(s, stage, main, variant); break;
            case 7: Lithops(s, stage, main); break;
            case 8: Resurrection(s, stage, main); break;
            case 9: Cactus(s, stage, main, variant); break;
            case 10: Waterlily(s, stage, main); break;
            case 99: Legend(s, stage, main); break;
            default: Generic(s, stage, main); break;
        }
        // 변종은 꽃 둘레에 작은 반짝이
        if (variant && stage >= PlantSpot.BloomStage)
        {
            var b = s.Top();
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Cos(a) * 0.3f, b - 0.05f - (i % 2) * 0.18f, Mathf.Sin(a) * 0.3f);
                s.Glow(Sparkle);
                s.Ball(Sparkle, pos, Vector3.one * 0.022f, null, 4, 2);
            }
        }
    }

    static Vector3 H(float deg) => new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), 0, Mathf.Sin(deg * Mathf.Deg2Rad));

    // 휘어진 줄기 점들(a → b, bend 만큼 옆으로 부푼다)
    static Vector3[] Curve(Vector3 a, Vector3 b, Vector3 bend, int n = 5)
    {
        var pts = new Vector3[n];
        var mid = (a + b) / 2 + bend;
        for (int i = 0; i < n; i++)
        {
            float t = i / (n - 1f);
            pts[i] = Vector3.Lerp(Vector3.Lerp(a, mid, t), Vector3.Lerp(mid, b, t), t);
        }
        return pts;
    }

    static Color Lighter(Color c, float k = 0.35f) => Color.Lerp(c, Color.white, k);
    static Color Darker(Color c, float k = 0.2f) => Color.Lerp(c, Color.black, k);

    // 01 민들레: 톱니 잎이 둥글게 퍼진 로제트 + 노란 꽃 + 흰 솜털 씨앗
    static void Dandelion(Sculpt s, int stage, Color flower)
    {
        var id = Quaternion.identity;
        s.Ring(Leaf, Vector3.zero, id, 7, 0.02f, 0.42f, 0.24f, 22f, LobedP, 0.25f, 0.06f);
        s.Ring(LeafDark, Vector3.zero, id, 5, 0.02f, 0.3f, 0.2f, 42f, LobedP, 0.2f, 0.06f, 25f);
        if (stage < 3) return;
        var top = new Vector3(-0.05f, 0.78f, 0.03f);
        s.Stem(Stalk, Curve(Vector3.zero, top, new Vector3(0.06f, 0, 0)), 0.022f, 0.016f);
        if (stage == 3)
        {
            s.Ball(LeafDark, top, new Vector3(0.06f, 0.09f, 0.06f), null, 6, 4);
            s.Ball(flower, top + Vector3.up * 0.07f, new Vector3(0.035f, 0.04f, 0.035f), null, 6, 3);
            return;
        }
        s.Ball(Darker(flower, 0.06f), top, new Vector3(0.12f, 0.06f, 0.12f), null, 8, 3);
        s.Ring(flower, top, id, 14, 0.06f, 0.15f, 0.06f, 8f, PetalP, 0.1f);
        s.Ring(Lighter(flower, 0.15f), top + Vector3.up * 0.03f, id, 11, 0.04f, 0.12f, 0.055f, 35f, PetalP, 0.1f, 0.05f, 13f);
        s.Ring(flower, top + Vector3.up * 0.05f, id, 7, 0.02f, 0.08f, 0.05f, 62f, PetalP, 0f, 0.05f, 5f);
        // 솜털 씨앗 공
        var puff = new Vector3(0.24f, 0.56f, -0.08f);
        s.Stem(Stalk, Curve(Vector3.zero, puff, new Vector3(0.02f, 0.05f, 0)), 0.018f, 0.012f);
        s.Ball(Cream, puff, Vector3.one * 0.05f, null, 6, 4);
        foreach (var d in Sphere(22))
            s.Tube(White, puff + d * 0.04f, puff + d * 0.14f, 0.004f, 0.012f, 3);
    }

    // 02 해바라기: 굵은 줄기 + 큰 하트 잎 + 갈색 원반과 노란 꽃잎(남쪽을 본다)
    static void Sunflower(Sculpt s, int stage, Color flower)
    {
        var top = new Vector3(0.02f, 0.95f, -0.03f);
        var stem = Curve(Vector3.zero, top, new Vector3(0.04f, 0, 0.03f), 6);
        s.Stem(Stalk, stem, 0.04f, 0.03f, 6);
        for (int i = 0; i < 5; i++)
        {
            var at = Vector3.Lerp(stem[i], stem[i + 1], 0.3f);
            var h = H(i * 137f + 30f);
            float e = 18f * Mathf.Deg2Rad;
            s.Blade(i % 2 == 0 ? Leaf : LeafDark, at, h * Mathf.Cos(e) + Vector3.up * Mathf.Sin(e), Vector3.up * Mathf.Cos(e) - h * Mathf.Sin(e),
                0.4f - i * 0.03f, 0.32f - i * 0.025f, HeartP, 0.3f, 0.05f);
        }
        if (stage < 3) return;
        if (stage == 3)
        {
            s.Ball(LeafDark, top, new Vector3(0.11f, 0.1f, 0.11f), null, 7, 4);
            s.Ring(Leaf, top, Quaternion.identity, 8, 0.05f, 0.15f, 0.09f, 58f, PetalP, -0.2f);
            return;
        }
        var frame = Quaternion.AngleAxis(-58f, Vector3.right); // 꽃 얼굴이 남쪽(-z) 위를 본다
        var axis = frame * Vector3.up;
        var c = top + axis * 0.04f;
        s.Ball(Brown, c, new Vector3(0.19f, 0.06f, 0.19f), frame, 10, 3);
        s.Ball(Darker(Brown, 0.25f), c + axis * 0.035f, new Vector3(0.1f, 0.035f, 0.1f), frame, 8, 3);
        s.Ring(flower, c - axis * 0.01f, frame, 15, 0.15f, 0.21f, 0.11f, 6f, PetalP, 0.12f);
        s.Ring(Lighter(flower, 0.2f), c - axis * 0.03f, frame, 15, 0.13f, 0.18f, 0.11f, -6f, PetalP, 0.1f, 0.05f, 12f);
        s.Ring(LeafDark, c - axis * 0.06f, frame, 8, 0.04f, 0.2f, 0.13f, -14f, PetalP, 0f);
    }

    // 03 미모사: 깃털 같은 겹잎 + 분홍 솜방울 꽃
    static void Mimosa(Sculpt s, int stage, Color flower)
    {
        var top = new Vector3(0.03f, 0.74f, 0);
        var stem = Curve(Vector3.zero, top, new Vector3(0.05f, 0, 0), 6);
        s.Stem(Stalk, stem, 0.024f, 0.012f);
        for (int i = 0; i < 6; i++)
            Compound(s, i % 2 == 0 ? Leaf : LeafDark, stem[1 + i * 4 / 6], i * 131f + 20f, 28f - i * 2f, 0.4f - i * 0.02f, 6, 0.14f, 0.075f, 0.3f);
        if (stage < 3) return;
        var a = new Vector3(0.07f, 0.98f, 0.02f);
        var b = new Vector3(0.24f, 0.8f, -0.06f);
        s.Stem(Stalk, Curve(top, a, new Vector3(0.03f, 0, 0), 4), 0.012f, 0.008f, 4);
        s.Stem(Stalk, Curve(stem[4], b, new Vector3(0.02f, 0.05f, 0), 4), 0.01f, 0.007f, 4);
        if (stage == 3)
        {
            s.Ball(Darker(flower, 0.15f), a, Vector3.one * 0.04f, null, 6, 4);
            s.Ball(Darker(flower, 0.15f), b, Vector3.one * 0.035f, null, 6, 4);
            return;
        }
        Pompom(s, flower, a, 0.07f, 0.1f);
        s.Ball(Darker(flower, 0.12f), b, new Vector3(0.04f, 0.05f, 0.04f), null, 6, 4);
    }

    static void Pompom(Sculpt s, Color c, Vector3 at, float core, float rays)
    {
        s.Ball(c, at, Vector3.one * core, null, 7, 5);
        foreach (var d in Sphere(26))
            s.Tube(Lighter(c, 0.3f), at + d * core * 0.8f, at + d * (core + rays), 0.007f, 0.002f, 3);
    }

    // 겹잎 한 장: 휘어진 잎줄기 양옆으로 작은 잎이 쌍쌍이
    static void Compound(Sculpt s, Color c, Vector3 basePos, float angDeg, float elevDeg, float len, int pairs, float leafLen, float leafW, float droop)
    {
        var h = H(angDeg);
        float e = elevDeg * Mathf.Deg2Rad;
        var dir = h * Mathf.Cos(e) + Vector3.up * Mathf.Sin(e);
        var nrm = Vector3.up * Mathf.Cos(e) - h * Mathf.Sin(e);
        var side = Vector3.Cross(nrm, dir);
        Vector3 P(float t) => basePos + dir * (len * t) - nrm * (droop * len * t * t);
        s.Stem(Stalk, new[] { P(0), P(0.33f), P(0.66f), P(1) }, 0.012f, 0.006f, 4);
        for (int i = 1; i <= pairs; i++)
        {
            float t = i / (pairs + 0.5f);
            float ll = leafLen * (1f - 0.45f * t);
            for (int sg = -1; sg <= 1; sg += 2)
                s.Blade(c, P(t), (side * sg + dir * 0.45f).normalized, nrm, ll, leafW, SmallLeafP, 0.1f, 0.04f);
        }
        s.Blade(c, P(1), dir, nrm, leafLen * 0.6f, leafW, SmallLeafP, 0.1f, 0.04f);
    }

    // 04 파리지옥: 납작한 잎자루 끝에 이빨 달린 덫(겉은 초록, 속은 빨강) + 흰 꽃
    static void Flytrap(Sculpt s, int stage, Color shell, bool variant)
    {
        var inside = variant ? Color.Lerp(shell, Yellow, 0.45f) : C(228, 86, 72);
        int traps = stage == 2 ? 3 : 5;
        for (int k = 0; k < traps; k++)
        {
            var h = H(k * 360f / traps + 10f);
            float e = (k % 2 == 0 ? 32f : 55f) * Mathf.Deg2Rad;
            float len = k % 2 == 0 ? 0.3f : 0.36f;
            var dir = h * Mathf.Cos(e) + Vector3.up * Mathf.Sin(e);
            var nrm = Vector3.up * Mathf.Cos(e) - h * Mathf.Sin(e);
            s.Blade(Leaf, Vector3.zero, dir, nrm, len, 0.1f, StrapP, 0f, 0.05f);
            Trap(s, shell, inside, dir * len, h, 0.9f + (k % 3) * 0.12f);
        }
        if (stage < 3) return;
        var top = new Vector3(0.02f, stage == 3 ? 0.55f : 0.86f, 0.02f);
        s.Stem(Stalk, Curve(Vector3.zero, top, new Vector3(-0.04f, 0, 0.02f)), 0.018f, 0.012f);
        if (stage == 3) { s.Ball(LeafLight, top, new Vector3(0.04f, 0.06f, 0.04f), null, 6, 4); return; }
        s.Ring(White, top, Quaternion.identity, 5, 0.02f, 0.1f, 0.08f, 22f, PetalP, 0.1f);
        s.Ball(Yellow, top + Vector3.up * 0.015f, Vector3.one * 0.032f, null, 6, 4);
    }

    static void Trap(Sculpt s, Color shell, Color inside, Vector3 at, Vector3 h, float size)
    {
        var side = Vector3.Cross(Vector3.up, h);
        float e = 58f * Mathf.Deg2Rad;
        var axis = h * Mathf.Cos(e) + Vector3.up * Mathf.Sin(e);
        var n = Vector3.Cross(side, axis);
        for (int sg = -1; sg <= 1; sg += 2)
        {
            var rot = Quaternion.AngleAxis(sg * 30f, side);
            var up = rot * axis;          // 덫 한 쪽이 뻗는 방향
            var outer = rot * (n * sg);   // 그 쪽의 바깥 면
            var c = at + up * 0.12f * size;
            var q = Quaternion.LookRotation(outer, up);
            s.Ball(shell, c + outer * 0.012f * size, new Vector3(0.105f, 0.125f, 0.03f) * size, q, 8, 4);
            s.Ball(inside, c - outer * 0.008f * size, new Vector3(0.088f, 0.108f, 0.016f) * size, q, 8, 3);
            // 이빨
            for (int i = 0; i < 7; i++)
            {
                float a = (15f + i * 25f) * Mathf.Deg2Rad;
                var rim = q * new Vector3(Mathf.Cos(a) * 0.1f, Mathf.Sin(a) * 0.12f, 0) * size;
                var tooth = (rim.normalized - outer * 0.9f).normalized;
                s.Tube(LeafLight, c + rim, c + rim + tooth * 0.05f * size, 0.008f * size, 0.001f, 3, false);
            }
        }
    }

    // 05 끈끈이주걱: 붉은 주걱 잎 로제트 + 끈끈한 이슬방울 + 휘어진 꽃대의 연분홍 꽃
    static void Sundew(Sculpt s, int stage, Color leaf)
    {
        var id = Quaternion.identity;
        s.Ring(leaf, Vector3.zero, id, 9, 0.02f, 0.36f, 0.11f, 15f, PaddleP, 0.15f, 0.03f);
        s.Ring(Darker(leaf, 0.12f), Vector3.zero, id, 6, 0.02f, 0.26f, 0.1f, 38f, PaddleP, 0.1f, 0.03f, 20f);
        var dew = Lighter(leaf, 0.7f);
        for (int k = 0; k < 9; k++)
            for (int i = 0; i < 4; i++)
            {
                var h = H(k * 40f + (i % 2 == 0 ? 6f : -6f));
                s.Ball(dew, h * (0.2f + i * 0.045f) + Vector3.up * (0.085f + i * 0.004f), Vector3.one * 0.014f, null, 4, 2);
            }
        if (stage < 3) return;
        var top = new Vector3(0.12f, stage == 3 ? 0.5f : 0.8f, 0);
        var stalk = Curve(Vector3.zero, top, new Vector3(-0.14f, 0.05f, 0), 6);
        s.Stem(C(198, 166, 78), stalk, 0.016f, 0.01f);
        var pink = C(255, 208, 222);
        if (stage == 3)
        {
            s.Ball(pink, top, new Vector3(0.035f, 0.05f, 0.035f), null, 6, 4);
            s.Ball(Darker(pink, 0.1f), stalk[4] + new Vector3(0.03f, 0, 0), Vector3.one * 0.028f, null, 5, 3);
            return;
        }
        s.Ring(pink, top, id, 5, 0.02f, 0.1f, 0.09f, 25f, PetalP, 0.1f);
        s.Ball(Yellow, top + Vector3.up * 0.02f, Vector3.one * 0.03f, null, 6, 4);
        s.Ball(Darker(pink, 0.12f), stalk[4] + new Vector3(0.05f, 0.01f, 0.02f), new Vector3(0.03f, 0.04f, 0.03f), null, 5, 3);
        s.Ball(Darker(pink, 0.12f), stalk[3] + new Vector3(0.05f, 0, -0.02f), new Vector3(0.026f, 0.035f, 0.026f), null, 5, 3);
    }

    // 06 벌레잡이통풀: 길쭉한 잎 다발 + 발치의 항아리(포충낭) + 별 모양 분홍 꽃
    static void Pitcher(Sculpt s, int stage, Color main, bool variant)
    {
        var id = Quaternion.identity;
        var jar = variant ? main : C(216, 116, 96);
        s.Ring(Leaf, Vector3.zero, id, 8, 0.03f, 0.55f, 0.13f, 50f, StrapP, 0.55f, 0.05f);
        s.Ring(LeafLight, Vector3.zero, id, 5, 0.02f, 0.38f, 0.11f, 68f, StrapP, 0.3f, 0.05f, 20f);
        int jars = stage == 2 ? 1 : 3;
        for (int k = 0; k < jars; k++)
        {
            var h = H(30f + k * 120f);
            var pos = h * 0.36f;
            var tilt = Quaternion.AngleAxis(12f, Vector3.Cross(Vector3.up, h));
            s.Lathe(jar, pos, tilt, new[] { 0.02f, 0f, 0.075f, 0.03f, 0.09f, 0.1f, 0.07f, 0.19f, 0.06f, 0.23f, 0.088f, 0.262f, 0.06f, 0.27f }, 8);
            s.Ball(Darker(jar, 0.45f), pos + tilt * new Vector3(0, 0.262f, 0), new Vector3(0.058f, 0.008f, 0.058f), tilt, 8, 2);
            s.Ball(Lighter(jar, 0.15f), pos + tilt * new Vector3(0, 0.315f, 0) + h * 0.05f, new Vector3(0.075f, 0.012f, 0.065f),
                Quaternion.AngleAxis(-35f, Vector3.Cross(Vector3.up, h)), 7, 2);
        }
        if (stage < 3) return;
        var top = new Vector3(0.03f, stage == 3 ? 0.6f : 0.95f, 0.02f);
        s.Stem(Stalk, Curve(Vector3.zero, top, new Vector3(0.03f, 0, 0.02f)), 0.018f, 0.012f);
        if (stage == 3) { s.Ball(C(255, 200, 204), top, new Vector3(0.04f, 0.07f, 0.04f), null, 6, 4); return; }
        s.Ring(C(255, 190, 198), top, id, 6, 0.02f, 0.26f, 0.09f, 15f, PointedP, 0.25f);
        s.Ring(Cream, top + Vector3.up * 0.01f, id, 6, 0.02f, 0.18f, 0.07f, 45f, PointedP, 0.1f, 0.05f, 30f);
        s.Ball(Yellow, top + Vector3.up * 0.02f, Vector3.one * 0.03f, null, 6, 4);
        for (int i = 0; i < 5; i++)
            s.Tube(Yellow, top, top + (H(i * 72f) * 0.4f + Vector3.up).normalized * 0.09f, 0.006f, 0.008f, 3);
    }

    // 07 리톱스: 가운데가 갈라진 조약돌 몸통 + 틈에서 나온 가는 꽃잎의 흰 꽃
    static void Lithops(Sculpt s, int stage, Color body)
    {
        const float k = 1.35f;
        var spots = new[] { (new Vector3(0, 0, 0), 1f, 20f), (new Vector3(0.27f, 0, 0.1f), 0.75f, 80f), (new Vector3(-0.22f, 0, 0.19f), 0.7f, 140f) };
        int count = stage == 2 ? 1 : 3;
        for (int i = 0; i < count; i++)
        {
            var (pos, size, ang) = spots[i];
            float z = size * k;
            var side = H(ang);
            var q = Quaternion.LookRotation(Vector3.Cross(side, Vector3.up), Vector3.up);
            for (int sg = -1; sg <= 1; sg += 2)
            {
                var c = pos * k + side * (0.062f * z * sg) + Vector3.up * 0.06f * z;
                s.Ball(sg < 0 ? body : Darker(body, 0.06f), c, new Vector3(0.085f, 0.11f, 0.12f) * z, q, 7, 5);
                s.Ball(Darker(body, 0.28f), c + Vector3.up * 0.1f * z, new Vector3(0.045f, 0.012f, 0.075f) * z, q, 6, 2);
            }
        }
        if (stage < 3) return;
        var top = new Vector3(0, stage == 3 ? 0.24f : 0.36f, 0) * k;
        s.Tube(Stalk, Vector3.up * 0.12f * k, top, 0.016f, 0.013f, 5);
        if (stage == 3) { s.Ball(Cream, top, new Vector3(0.04f, 0.07f, 0.04f), null, 6, 4); return; }
        var id = Quaternion.identity;
        s.Ring(Cream, top, id, 18, 0.03f, 0.22f, 0.04f, 20f, ThinP, 0.2f, 0.03f);
        s.Ring(White, top + Vector3.up * 0.01f, id, 14, 0.03f, 0.17f, 0.04f, 45f, ThinP, 0.1f, 0.03f, 10f);
        s.Ring(Yellow, top + Vector3.up * 0.01f, id, 10, 0.01f, 0.09f, 0.02f, 75f, ThinP, 0f, 0.02f);
        s.Ball(Yellow, top + Vector3.up * 0.03f, new Vector3(0.04f, 0.06f, 0.04f), null, 6, 4);
    }

    // 08 부활초: 고사리 같은 잎이 사방으로 펼쳐진 로제트 + 가운데 꽃대의 연살구 꽃
    static void Resurrection(Sculpt s, int stage, Color frond)
    {
        for (int i = 0; i < 9; i++)
            Compound(s, i % 2 == 0 ? frond : Lighter(frond, 0.22f), Vector3.up * 0.02f, i * 40f, i % 2 == 0 ? 32f : 55f, i % 2 == 0 ? 0.5f : 0.38f, 6, 0.12f, 0.055f, 0.3f);
        if (stage < 3) return;
        var top = new Vector3(0.02f, stage == 3 ? 0.5f : 0.82f, 0);
        s.Stem(Stalk, Curve(Vector3.zero, top, new Vector3(0.03f, 0, 0.02f)), 0.018f, 0.012f);
        var petal = C(255, 224, 206);
        if (stage == 3) { s.Ball(petal, top, new Vector3(0.04f, 0.07f, 0.04f), null, 6, 4); return; }
        var id = Quaternion.identity;
        s.Ring(petal, top, id, 6, 0.02f, 0.16f, 0.09f, 52f, PetalP, -0.1f);
        s.Ring(White, top + Vector3.up * 0.01f, id, 5, 0.01f, 0.12f, 0.08f, 72f, PetalP, -0.1f, 0.05f, 30f);
        for (int i = 0; i < 6; i++)
        {
            var tip = top + (H(i * 60f) * 0.35f + Vector3.up).normalized * 0.11f;
            s.Tube(Yellow, top, tip, 0.005f, 0.005f, 3, false);
            s.Ball(Yellow, tip, Vector3.one * 0.014f, null, 4, 2);
        }
    }

    // 09 선인장: 골이 진 통통한 몸 + 가시 + 꼭대기의 분홍 꽃
    static void Cactus(Sculpt s, int stage, Color body, bool variant)
    {
        var id = Quaternion.identity;
        s.Lathe(body, Vector3.zero, id, new[] { 0.2f, 0f, 0.29f, 0.12f, 0.31f, 0.3f, 0.27f, 0.45f, 0.17f, 0.54f, 0f, 0.58f }, 16, 0.16f);
        // 가시: 등성이(짝수 칸)마다 세 줄
        var heights = new[] { (0.14f, 0.295f), (0.3f, 0.31f), (0.44f, 0.275f) };
        for (int i = 0; i < 16; i += 2)
            foreach (var (y, r) in heights)
            {
                var h = H(i * 22.5f);
                var from = h * r + Vector3.up * y;
                s.Tube(Cream, from, from + (h + Vector3.up * 0.3f).normalized * 0.055f, 0.012f, 0.001f, 3, false);
            }
        for (int i = 0; i < 5; i++)
            s.Ball(Lighter(body, 0.2f), H(i * 72f) * 0.09f + Vector3.up * 0.555f, new Vector3(0.05f, 0.04f, 0.05f), null, 5, 3);
        if (stage < 3) return;
        var pink = variant ? Cream : C(246, 142, 172);
        var c = new Vector3(0, 0.6f, 0);
        if (stage == 3) { s.Ball(pink, c + Vector3.up * 0.03f, new Vector3(0.06f, 0.085f, 0.06f), null, 6, 4); return; }
        s.Ring(pink, c, id, 8, 0.03f, 0.2f, 0.13f, 30f, PetalP, -0.12f);
        s.Ring(Lighter(pink, 0.25f), c + Vector3.up * 0.02f, id, 7, 0.02f, 0.16f, 0.12f, 52f, PetalP, -0.1f, 0.05f, 22f);
        s.Ring(Lighter(pink, 0.45f), c + Vector3.up * 0.03f, id, 5, 0.01f, 0.11f, 0.09f, 72f, PetalP, 0f, 0.05f, 10f);
        for (int i = 0; i < 7; i++)
            s.Tube(Yellow, c + Vector3.up * 0.03f, c + Vector3.up * 0.03f + (H(i * 51f) * 0.3f + Vector3.up).normalized * 0.07f, 0.007f, 0.009f, 3);
    }

    // 10 수련: 작은 물웅덩이 + 한쪽이 갈라진 둥근 잎 + 겹겹이 핀 연꽃
    static void Waterlily(Sculpt s, int stage, Color flower)
    {
        s.Ball(C(152, 202, 236), new Vector3(0, 0.012f, 0), new Vector3(0.58f, 0.012f, 0.58f), null, 14, 2);
        int pads = stage == 2 ? 2 : 3;
        for (int i = 0; i < pads; i++)
            s.Pad(i % 2 == 0 ? Leaf : LeafDark, H(i * 125f + 40f) * 0.3f + Vector3.up * (0.035f + i * 0.004f), 0.22f - i * 0.02f, i * 97f, 10);
        if (stage < 3) return;
        var c = new Vector3(0, stage == 3 ? 0.34f : 0.3f, 0);
        s.Tube(Stalk, Vector3.zero, c, 0.022f, 0.018f, 5, false);
        var id = Quaternion.identity;
        if (stage == 3)
        {
            s.Ball(flower, c + Vector3.up * 0.09f, new Vector3(0.07f, 0.12f, 0.07f), null, 6, 4);
            s.Ring(LeafDark, c, id, 4, 0.02f, 0.13f, 0.09f, 68f, PetalP, -0.15f);
            return;
        }
        s.Ring(Darker(flower, 0.08f), c, id, 8, 0.03f, 0.25f, 0.13f, 25f, PetalP, -0.15f);
        s.Ring(flower, c + Vector3.up * 0.02f, id, 7, 0.03f, 0.23f, 0.12f, 48f, PetalP, -0.12f, 0.05f, 22f);
        s.Ring(Lighter(flower, 0.25f), c + Vector3.up * 0.04f, id, 6, 0.02f, 0.2f, 0.11f, 66f, PetalP, -0.1f, 0.05f, 8f);
        s.Ring(Lighter(flower, 0.45f), c + Vector3.up * 0.05f, id, 4, 0.01f, 0.15f, 0.09f, 80f, PetalP, 0f, 0.05f, 30f);
        s.Ball(Yellow, c + Vector3.up * 0.09f, Vector3.one * 0.045f, null, 6, 4);
    }

    // 99 별빛 꽃(전설): 은은히 빛나는 긴 줄기 + 별 모양 꽃잎 두 겹 + 빛 알갱이
    static void Legend(Sculpt s, int stage, Color glow)
    {
        var id = Quaternion.identity;
        s.Ring(LeafDark, Vector3.zero, id, 6, 0.02f, 0.36f, 0.14f, 28f, PointedP, 0.25f, 0.05f);
        var top = new Vector3(0, stage == 3 ? 0.7f : 1.05f, 0);
        s.Glow(Lighter(glow, 0.3f));
        s.Stem(Lighter(glow, 0.3f), Curve(Vector3.zero, top, new Vector3(0.05f, 0, 0.03f), 6), 0.025f, 0.015f);
        if (stage < 3) return;
        s.Glow(glow);
        if (stage == 3) { s.Ball(glow, top, new Vector3(0.07f, 0.11f, 0.07f), null, 7, 5); return; }
        s.Glow(White);
        s.Ring(glow, top, id, 5, 0.03f, 0.3f, 0.12f, 18f, PointedP, 0.1f);
        s.Ring(White, top + Vector3.up * 0.02f, id, 5, 0.02f, 0.2f, 0.09f, 40f, PointedP, 0.05f, 0.05f, 36f);
        s.Glow(Yellow);
        s.Ball(Yellow, top + Vector3.up * 0.04f, Vector3.one * 0.06f, null, 7, 5);
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45f * Mathf.Deg2Rad;
            s.Ball(Yellow, top + new Vector3(Mathf.Cos(a) * 0.45f, -0.1f + (i % 3) * 0.12f, Mathf.Sin(a) * 0.45f), Vector3.one * 0.025f, null, 4, 2);
        }
    }

    // 목록에 없는 식물: 줄기 + 잎 + 꽃잎 한 바퀴
    static void Generic(Sculpt s, int stage, Color flower)
    {
        var top = new Vector3(0, 0.8f, 0);
        s.Tube(Stalk, Vector3.zero, top, 0.025f, 0.018f, 5);
        s.Ring(Leaf, Vector3.up * 0.1f, Quaternion.identity, 4, 0.02f, 0.3f, 0.16f, 30f, LeafP, 0.3f);
        if (stage == 3) s.Ball(Darker(flower), top, new Vector3(0.06f, 0.09f, 0.06f), null, 6, 4);
        if (stage >= 4)
        {
            s.Ring(flower, top, Quaternion.identity, 8, 0.04f, 0.18f, 0.1f, 20f, PetalP, 0.1f);
            s.Ball(Yellow, top + Vector3.up * 0.02f, Vector3.one * 0.06f, null, 6, 4);
        }
    }

    // 공 겉면에 고르게 퍼진 방향 n개(솜털·술)
    static IEnumerable<Vector3> Sphere(int n)
    {
        for (int i = 0; i < n; i++)
        {
            float y = 1f - (i + 0.5f) / n * 2f, r = Mathf.Sqrt(1f - y * y), a = i * 2.39996f;
            yield return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
        }
    }

    // ── 조각 모으기 ──
    // 삼각형을 색별로 모았다가 메시 하나(색마다 서브메시)로 만든다. 꼭짓점을 면마다 따로 둬서 각진 로우폴리로 보인다.
    class Sculpt
    {
        readonly Dictionary<int, List<Vector3>> tris = new Dictionary<int, List<Vector3>>();
        readonly HashSet<int> glow = new HashSet<int>();
        public System.Func<Color, Color> Recolor;

        static int Key(Color32 c) => c.r | c.g << 8 | c.b << 16;
        Color Final(Color c) => Recolor != null ? Recolor(c) : c;

        // 이 색 조각은 스스로 빛난다(씨앗 꼬투리·변종 반짝이)
        public void Glow(Color c) => glow.Add(Key(Final(c)));

        List<Vector3> List(Color c)
        {
            int k = Key(Final(c));
            if (!tris.TryGetValue(k, out var l)) tris[k] = l = new List<Vector3>();
            return l;
        }

        void Add(List<Vector3> l, Vector3 a, Vector3 b, Vector3 d)
        {
            if (Vector3.Cross(b - a, d - a).sqrMagnitude < 1e-14f) return; // 넓이 없는 조각
            l.Add(a); l.Add(b); l.Add(d);
        }

        // 겉면이 inside 반대쪽을 보게
        void TriOut(List<Vector3> l, Vector3 a, Vector3 b, Vector3 d, Vector3 inside)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, d - a), (a + b + d) / 3f - inside) < 0) Add(l, a, d, b);
            else Add(l, a, b, d);
        }

        // 양면(잎·꽃잎)
        void Quad2(List<Vector3> l, Vector3 a, Vector3 b, Vector3 d, Vector3 e)
        {
            Add(l, a, b, d); Add(l, a, d, b);
            Add(l, a, d, e); Add(l, a, e, d);
        }

        // 지금까지 만든 것 중 가장 높은 곳
        public float Top()
        {
            float y = 0;
            foreach (var l in tris.Values) foreach (var v in l) y = Mathf.Max(y, v.y);
            return y;
        }

        // 잎·꽃잎 한 장: base 에서 dir 쪽으로 length, 윗면이 normal. profile = 밑동→끝 폭 비율.
        // droop > 0 이면 끝이 처지고(< 0 이면 오므라들고), fold 만큼 가운데가 접힌다(V자).
        public void Blade(Color c, Vector3 basePos, Vector3 dir, Vector3 normal, float length, float width, float[] profile, float droop = 0f, float fold = 0.05f)
        {
            var l = List(c);
            dir.Normalize();
            normal = (normal - dir * Vector3.Dot(normal, dir)).normalized;
            var side = Vector3.Cross(normal, dir);
            Vector3 pm = default, pl = default, pr = default;
            for (int i = 0; i < profile.Length; i++)
            {
                float t = i / (profile.Length - 1f);
                var mid = basePos + dir * (length * t) - normal * (droop * length * t * t);
                float w = profile[i] * width * 0.5f;
                var left = mid - side * w + normal * (fold * w * 2f);
                var right = mid + side * w + normal * (fold * w * 2f);
                if (i > 0)
                {
                    Quad2(l, pm, pl, left, mid);
                    Quad2(l, pm, mid, right, pr);
                }
                pm = mid; pl = left; pr = right;
            }
        }

        // 잎·꽃잎을 둥글게 한 바퀴. frame = 꽃이 보는 방향(위 = 꽃의 축), elev = 수평에서 들린 각도.
        public void Ring(Color c, Vector3 center, Quaternion frame, int count, float inner, float length, float width, float elevDeg,
            float[] profile, float droop = 0f, float fold = 0.05f, float offsetDeg = 0f)
        {
            float e = elevDeg * Mathf.Deg2Rad;
            for (int k = 0; k < count; k++)
            {
                var h = H(offsetDeg + 360f * k / count);
                var dir = h * Mathf.Cos(e) + Vector3.up * Mathf.Sin(e);
                var nrm = Vector3.up * Mathf.Cos(e) - h * Mathf.Sin(e);
                Blade(c, center + frame * (h * inner), frame * dir, frame * nrm, length, width, profile, droop, fold);
            }
        }

        // 각진 공(타원체)
        public void Ball(Color c, Vector3 center, Vector3 radii, Quaternion? rot = null, int seg = 7, int rings = 5)
        {
            var l = List(c);
            var q = rot ?? Quaternion.identity;
            Vector3 P(int i, int j)
            {
                float t = Mathf.PI * j / rings, p = 2f * Mathf.PI * i / seg;
                return center + q * new Vector3(Mathf.Sin(t) * Mathf.Cos(p) * radii.x, Mathf.Cos(t) * radii.y, Mathf.Sin(t) * Mathf.Sin(p) * radii.z);
            }
            for (int j = 0; j < rings; j++)
                for (int i = 0; i < seg; i++)
                {
                    Vector3 a = P(i, j), b = P(i + 1, j), d = P(i + 1, j + 1), e = P(i, j + 1);
                    TriOut(l, a, b, d, center);
                    TriOut(l, a, d, e, center);
                }
        }

        // 굵기가 변하는 대롱(줄기·가시)
        public void Tube(Color c, Vector3 a, Vector3 b, float ra, float rb, int sides = 5, bool cap = true)
        {
            var l = List(c);
            var axis = (b - a).normalized;
            var u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var v = Vector3.Cross(axis, u);
            var mid = (a + b) / 2f;
            for (int i = 0; i < sides; i++)
            {
                float a0 = 2f * Mathf.PI * i / sides, a1 = 2f * Mathf.PI * (i + 1) / sides;
                var o0 = u * Mathf.Cos(a0) + v * Mathf.Sin(a0);
                var o1 = u * Mathf.Cos(a1) + v * Mathf.Sin(a1);
                Vector3 p0 = a + o0 * ra, p1 = a + o1 * ra, q0 = b + o0 * rb, q1 = b + o1 * rb;
                TriOut(l, p0, p1, q1, mid);
                TriOut(l, p0, q1, q0, mid);
                if (cap) TriOut(l, b, q0, q1, a);
            }
        }

        public void Stem(Color c, Vector3[] pts, float r0, float r1, int sides = 5)
        {
            for (int i = 0; i < pts.Length - 1; i++)
            {
                float t0 = i / (pts.Length - 1f), t1 = (i + 1) / (pts.Length - 1f);
                Tube(c, pts[i], pts[i + 1], Mathf.Lerp(r0, r1, t0), Mathf.Lerp(r0, r1, t1), sides, i == pts.Length - 2);
            }
        }

        // 돌려 깎은 모양(항아리·선인장·꼬투리). profile = (반지름, 높이) 쌍들. rib > 0 이면 한 칸 걸러 들어가 골이 진다.
        public void Lathe(Color c, Vector3 basePos, Quaternion rot, float[] profile, int sides, float rib = 0f)
        {
            var l = List(c);
            int n = profile.Length / 2;
            Vector3 P(int k, int i)
            {
                float a = 2f * Mathf.PI * i / sides;
                float r = profile[k * 2] * (i % 2 == 1 ? 1f - rib : 1f);
                return basePos + rot * new Vector3(Mathf.Cos(a) * r, profile[k * 2 + 1], Mathf.Sin(a) * r);
            }
            for (int k = 0; k < n - 1; k++)
            {
                var inside = basePos + rot * new Vector3(0, (profile[k * 2 + 1] + profile[k * 2 + 3]) / 2f, 0);
                for (int i = 0; i < sides; i++)
                {
                    Vector3 a = P(k, i), b = P(k, i + 1), d = P(k + 1, i + 1), e = P(k + 1, i);
                    TriOut(l, a, b, d, inside);
                    TriOut(l, a, d, e, inside);
                }
            }
        }

        // 연잎: 한쪽이 쐐기 모양으로 갈라진 둥근 판(양면)
        public void Pad(Color c, Vector3 center, float radius, float notchDeg, int seg)
        {
            var l = List(c);
            const float gap = 36f;
            for (int i = 0; i < seg; i++)
            {
                float a0 = notchDeg + gap / 2f + (360f - gap) * i / seg, a1 = notchDeg + gap / 2f + (360f - gap) * (i + 1) / seg;
                Vector3 p0 = center + H(a0) * radius, p1 = center + H(a1) * radius;
                Add(l, center, p0, p1);
                Add(l, center, p1, p0);
            }
        }

        public (Mesh, Material[]) ToMesh(Matrix4x4 m)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var subs = new List<int[]>();
            var mats = new List<Material>();
            foreach (var kv in tris)
            {
                var l = kv.Value;
                var idx = new int[l.Count];
                for (int i = 0; i < l.Count; i += 3)
                {
                    Vector3 a = m.MultiplyPoint3x4(l[i]), b = m.MultiplyPoint3x4(l[i + 1]), d = m.MultiplyPoint3x4(l[i + 2]);
                    var n = Vector3.Cross(b - a, d - a).normalized;
                    for (int k = 0; k < 3; k++) { idx[i + k] = verts.Count + k; normals.Add(n); }
                    verts.Add(a); verts.Add(b); verts.Add(d);
                }
                subs.Add(idx);
                var col = new Color32((byte)(kv.Key & 255), (byte)(kv.Key >> 8 & 255), (byte)(kv.Key >> 16 & 255), 255);
                mats.Add(MaterialFor(col, glow.Contains(kv.Key)));
            }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, subMeshCount = subs.Count };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            for (int i = 0; i < subs.Count; i++) mesh.SetTriangles(subs[i], i);
            mesh.RecalculateBounds();
            return (mesh, mats.ToArray());
        }
    }
}
