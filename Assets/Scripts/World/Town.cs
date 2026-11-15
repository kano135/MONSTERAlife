using System.Collections.Generic;
using UnityEngine;

// 빽빽한 마을: 광장을 둘러싸고 집이 두 겹으로 촘촘히 서 있고, 사이사이에 좌판·벤치·등불·상자·통·장작더미·화분·울타리가 놓인다.
// 집은 파스텔 벽에 짙은 나무 뼈대, 박공지붕(세모 지붕), 불 켜진 창. 일부는 2층, 일부는 굴뚝이 있다.
// 마을 둘레에는 PrototypeBootstrap 이 숲 띠(BeltTrees)를 빽빽하게 심는다(기지 쪽만 터놓는다).
// ★ 마을 자리·크기는 아래 Towns 표만 고치면 된다.
public static class Town
{
    // 가운데(x, z), 반지름(m), 둘레 숲의 나무 수, 가운데에 제단(true) 또는 우물(false)
    public static readonly (float x, float z, float radius, int beltTrees, bool altar)[] Towns =
    {
        (Village.Center.x, Village.Center.z, 26f, 55, true), // 원주민 마을(가운데에 원주민들과 꽃 제단)
        (-40f, -50f, 20f, 42, false),
        (-116f, 14f, 20f, 42, false),                         // 폭포 가는 길목, 강가
    };

    static readonly Color[] Walls =
    {
        new Color(1f, 0.93f, 0.84f), new Color(0.99f, 0.86f, 0.82f), new Color(0.88f, 0.85f, 0.98f), new Color(0.86f, 0.95f, 0.91f), new Color(0.99f, 0.95f, 0.78f),
    };
    static readonly Color[] Roofs =
    {
        new Color(0.94f, 0.5f, 0.48f), new Color(0.42f, 0.72f, 0.72f), new Color(0.62f, 0.52f, 0.84f), new Color(0.97f, 0.7f, 0.42f), new Color(0.9f, 0.56f, 0.68f),
    };
    static readonly Color[] Goods =
    {
        new Color(1f, 0.55f, 0.5f), new Color(1f, 0.82f, 0.4f), new Color(0.7f, 0.6f, 0.95f), new Color(0.55f, 0.85f, 0.7f), new Color(1f, 0.7f, 0.85f),
    };

    static Material[] wallMats, roofMats, goodsMats;
    static Material timber, wood, woodLight, stone, door, glow, dirt, cloth, leaf;
    static Mesh gable;

    public static void Build(Transform parent, Landscape land)
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        Material M(Color c) { var m = new Material(lit) { color = c }; m.SetFloat("_Smoothness", 0.1f); return m; }
        wallMats = System.Array.ConvertAll(Walls, M);
        roofMats = System.Array.ConvertAll(Roofs, M);
        goodsMats = System.Array.ConvertAll(Goods, M);
        timber = M(new Color(0.4f, 0.28f, 0.34f));
        wood = M(new Color(0.66f, 0.47f, 0.33f));
        woodLight = M(new Color(0.84f, 0.67f, 0.48f));
        stone = M(new Color(0.74f, 0.7f, 0.76f));
        door = M(new Color(0.5f, 0.33f, 0.3f));
        dirt = M(new Color(0.9f, 0.82f, 0.66f));
        cloth = M(new Color(1f, 0.97f, 0.9f));
        leaf = M(new Color(0.45f, 0.75f, 0.55f));
        glow = M(new Color(1f, 0.85f, 0.5f));
        glow.EnableKeyword("_EMISSION");
        glow.SetColor("_EmissionColor", new Color(1f, 0.75f, 0.35f) * 1.6f);
        gable = GableMesh();

        var rng = new System.Random(29);
        foreach (var town in Towns) BuildTown(parent, land, town, rng);
    }

    static void BuildTown(Transform parent, Landscape land, (float x, float z, float radius, int beltTrees, bool altar) town, System.Random rng)
    {
        float R() => (float)rng.NextDouble();
        var center = new Vector3(town.x, 0, town.z);
        var root = new GameObject("Town").transform;
        root.SetParent(parent, false);
        root.position = center;
        float toBase = Mathf.Atan2(PrototypeBootstrap.BaseCenter.z - town.z, PrototypeBootstrap.BaseCenter.x - town.x); // 기지 쪽(들어오는 길)

        // 흙 마당
        var yard = Prim(root, PrimitiveType.Cylinder, new Vector3(0, 0.025f, 0), new Vector3((town.radius + 1.5f) * 2f, 0.01f, (town.radius + 1.5f) * 2f), dirt);
        yard.name = "Yard";

        // 가운데: 제단(꽃이 놓인 돌 단) 또는 우물
        if (town.altar) Altar(root, rng); else Well(root);
        land.AddBlocker(center, 1.6f);

        float r1 = Mathf.Max(9.5f, town.radius * 0.45f), r2 = Mathf.Max(town.radius * 0.85f, r1 + 8.5f);
        Vector3 At(float ang, float r) => center + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * r;
        bool Free(Vector3 p, float margin) => Mathf.Abs(Landscape.HeightAt(p)) < 0.02f && !Cliff.Blocks(p, margin) && !Lake.Overlaps(p, margin)
            && Landscape.ShoreDistance(p) > GroundView.Beach + margin && !Landscape.OnPath(p, margin * 0.6f);

        // 벤치: 광장 둘레에서 가운데를 보고
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI / 2f + 0.6f;
            Bench(root, At(a, 6.6f), center);
        }

        // 안쪽 고리: 집과 좌판이 번갈아
        int n1 = Mathf.Max(4, Mathf.RoundToInt(2f * Mathf.PI * r1 / 8.4f));
        for (int i = 0; i < n1; i++)
        {
            float a = (i + 0.5f) / n1 * Mathf.PI * 2f + toBase;
            var pos = At(a, r1);
            if (!Free(pos, 3f)) continue;
            if (i % 2 == 0) House(root, land, pos, center, 4.4f + R() * 0.8f, 3.8f + R() * 0.5f, R() < 0.3f, rng);
            else
            {
                Stall(root, land, pos, center, rng);
                Vector3 side = Vector3.Cross(Vector3.up, (center - pos).normalized);
                Crate(root, pos + side * 1.9f, 0.7f, R() * 40f);
                Barrel(root, pos - side * 1.8f);
            }
        }

        // 바깥 고리: 집이 촘촘히(기지 쪽 한 칸은 들어오는 길로 비운다)
        int n2 = Mathf.RoundToInt(2f * Mathf.PI * r2 / 8.3f);
        for (int i = 0; i < n2; i++)
        {
            float a = (float)i / n2 * Mathf.PI * 2f + toBase;
            if (i == 0) continue; // 입구
            var pos = At(a + (R() - 0.5f) * 0.06f, r2 + (R() - 0.5f) * 1.6f);
            if (!Free(pos, 3.5f)) continue;
            float w = 4.6f + R() * 1.8f, d = 4f + R() * 1f;
            House(root, land, pos, center, w, d, R() < 0.4f, rng);
            // 집 옆 살림살이
            Vector3 side = Vector3.Cross(Vector3.up, (center - pos).normalized);
            var beside = pos + side * (w / 2f + 1f) + (center - pos).normalized * (R() * 1.5f);
            int kind = rng.Next(4);
            if (kind == 0) WoodPile(root, land, beside, a * Mathf.Rad2Deg);
            else if (kind == 1) { Crate(root, beside, 0.8f, R() * 40f); Crate(root, beside + side * 0.75f, 0.55f, R() * 40f); }
            else if (kind == 2) { Barrel(root, beside); Barrel(root, beside + side * 0.7f); }
            // 고리 사이 등불(집 두 채마다)
            if (i % 2 == 0) Lamp(root, At(a + Mathf.PI / n2, (r1 + r2) / 2f + 0.8f));
        }

        // 울타리: 마을 둘레에 군데군데(입구와 틈은 비운다)
        float rf = r2 + 5.2f;
        int posts = Mathf.RoundToInt(2f * Mathf.PI * rf / 2.2f);
        for (int i = 0; i < posts; i++)
        {
            float a0 = (float)i / posts * Mathf.PI * 2f + toBase, a1 = (float)(i + 1) / posts * Mathf.PI * 2f + toBase;
            float rel = Mathf.Abs(Mathf.DeltaAngle(a0 * Mathf.Rad2Deg, toBase * Mathf.Rad2Deg));
            if (rel < 28f || (i / 6) % 3 == 2) continue; // 입구 · 틈
            Vector3 p0 = At(a0, rf), p1 = At(a1, rf);
            if (!Free(p0, 1f) || !Free(p1, 1f)) continue;
            Fence(root, p0, p1);
        }
        Bake(root);
    }

    // 마을 하나의 도형 수천 개를 재질별 메시 몇 장으로 합친다(그리는 횟수를 줄여 빠르게). 충돌체는 그대로 둔다.
    // 지붕 판("Roof")과 마당("Yard")은 지도 그리기가 찾아 쓰므로 합치지 않는다.
    static void Bake(Transform root)
    {
        var groups = new Dictionary<Material, List<CombineInstance>>();
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
        {
            var r = mf.GetComponent<MeshRenderer>();
            if (mf.name == "Roof" || mf.name == "Yard" || !r || !mf.sharedMesh) continue;
            if (!groups.TryGetValue(r.sharedMaterial, out var list)) groups[r.sharedMaterial] = list = new List<CombineInstance>();
            list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = root.worldToLocalMatrix * mf.transform.localToWorldMatrix });
            Object.Destroy(r);
            Object.Destroy(mf);
        }
        foreach (var kv in groups)
        {
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(kv.Value.ToArray(), true, true);
            var go = new GameObject("TownMesh", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = kv.Key;
        }
    }

    // ── 집 ── 얼굴(문)이 faceTo 쪽. w = 가로, d = 세로(깊이)
    static void House(Transform parent, Landscape land, Vector3 pos, Vector3 faceTo, float w, float d, bool twoStory, System.Random rng)
    {
        float R() => (float)rng.NextDouble();
        var h = new GameObject("Hut").transform; // 지도·다른 코드가 집을 이 이름으로 찾는다
        h.SetParent(parent, true);
        h.position = pos;
        var look = faceTo - pos; look.y = 0;
        h.rotation = Quaternion.LookRotation(look.normalized) * Quaternion.Euler(0, (R() - 0.5f) * 16f, 0);

        var wall = wallMats[rng.Next(wallMats.Length)];
        var roof = roofMats[rng.Next(roofMats.Length)];
        const float plinth = 0.3f;
        float wallH = twoStory ? 4.6f : 2.7f, roofH = d * 0.42f;
        float top = plinth + wallH;

        Prim(h, PrimitiveType.Cube, new Vector3(0, plinth / 2f, 0), new Vector3(w + 0.25f, plinth, d + 0.25f), stone);
        Prim(h, PrimitiveType.Cube, new Vector3(0, plinth + wallH / 2f, 0), new Vector3(w, wallH, d), wall);
        // 박공(지붕 밑 세모 벽)
        var g = new GameObject("Gable", typeof(MeshFilter), typeof(MeshRenderer));
        g.transform.SetParent(h, false);
        g.transform.localPosition = new Vector3(0, top, 0);
        g.transform.localScale = new Vector3(w, roofH, d);
        g.GetComponent<MeshFilter>().sharedMesh = gable;
        g.GetComponent<MeshRenderer>().sharedMaterial = wall;
        // 지붕 판 두 장(처마가 벽 밖으로 나온다)
        float slope = Mathf.Sqrt(roofH * roofH + d * d / 4f), ang = Mathf.Atan2(roofH, d / 2f) * Mathf.Rad2Deg;
        for (int s = -1; s <= 1; s += 2)
        {
            var slab = Prim(h, PrimitiveType.Cube, new Vector3(0, top + roofH / 2f + 0.06f, s * d / 4f * 1.06f), new Vector3(w + 0.9f, 0.16f, slope + 0.55f), roof);
            slab.transform.localRotation = Quaternion.Euler(s * ang, 0, 0);
            slab.name = "Roof";
        }
        Prim(h, PrimitiveType.Cube, new Vector3(0, top + roofH + 0.1f, 0), new Vector3(w + 1f, 0.16f, 0.3f), timber); // 용마루

        // 나무 뼈대: 모서리 기둥, 층 사이·처마 밑 가로대, 앞면 샛기둥
        foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                Prim(h, PrimitiveType.Cube, new Vector3(sx * (w / 2f - 0.05f), plinth + wallH / 2f, sz * (d / 2f - 0.05f)), new Vector3(0.24f, wallH, 0.24f), timber);
        var bands = twoStory ? new[] { plinth + 2.35f, top - 0.1f } : new[] { top - 0.1f };
        foreach (float y in bands)
        {
            foreach (float sz in new[] { -1f, 1f }) Prim(h, PrimitiveType.Cube, new Vector3(0, y, sz * (d / 2f + 0.02f)), new Vector3(w + 0.1f, 0.2f, 0.1f), timber);
            foreach (float sx in new[] { -1f, 1f }) Prim(h, PrimitiveType.Cube, new Vector3(sx * (w / 2f + 0.02f), y, 0), new Vector3(0.1f, 0.2f, d + 0.1f), timber);
        }
        foreach (float sx in new[] { -1f, 1f })
            Prim(h, PrimitiveType.Cube, new Vector3(sx * w * 0.2f, plinth + wallH / 2f, d / 2f + 0.02f), new Vector3(0.14f, wallH, 0.08f), timber);

        // 문·창(불빛)
        float front = d / 2f + 0.04f;
        Prim(h, PrimitiveType.Cube, new Vector3(0, plinth + 0.9f, front), new Vector3(1f, 1.8f, 0.1f), door);
        Prim(h, PrimitiveType.Sphere, new Vector3(0.32f, plinth + 0.9f, front + 0.07f), Vector3.one * 0.12f, glow); // 손잡이
        foreach (float sx in new[] { -1f, 1f })
        {
            Window(h, new Vector3(sx * w * 0.34f, plinth + 1.45f, front), 0);
            Window(h, new Vector3(sx * (w / 2f + 0.04f), plinth + 1.45f, (R() - 0.5f) * d * 0.4f), 90);
            if (!twoStory) continue;
            Window(h, new Vector3(sx * w * 0.3f, plinth + 3.55f, front), 0);
            Window(h, new Vector3(sx * (w / 2f + 0.04f), plinth + 3.55f, 0), 90);
        }
        Window(h, new Vector3(0, top + roofH * 0.38f, 0), 90, 0.5f, w + 0.12f); // 박공 둥근 창(양쪽)
        // 굴뚝
        if (R() < 0.55f)
        {
            float cx = (R() < 0.5f ? -1f : 1f) * w * 0.28f;
            Prim(h, PrimitiveType.Cube, new Vector3(cx, top + roofH * 0.75f, -d * 0.12f), new Vector3(0.5f, roofH + 0.9f, 0.5f), stone);
        }
        // 문 옆 화분
        if (R() < 0.7f) Planter(h, new Vector3((R() < 0.5f ? -1f : 1f) * 1.15f, plinth, front + 0.35f), rng);

        var col = h.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, (top + roofH) / 2f, 0);
        col.size = new Vector3(w, top + roofH, d);
        land.AddBlocker(pos, Mathf.Sqrt(w * w + d * d) / 2f + 0.4f);
    }

    static void Window(Transform h, Vector3 pos, float yaw, float size = 0.72f, float depth = 0.1f)
    {
        var frame = Prim(h, PrimitiveType.Cube, pos, new Vector3(size + 0.18f, size + 0.18f, depth * 0.8f), timber);
        var pane = Prim(h, PrimitiveType.Cube, pos, new Vector3(size, size, depth), glow);
        frame.transform.localRotation = pane.transform.localRotation = Quaternion.Euler(0, yaw, 0);
    }

    static void Planter(Transform h, Vector3 pos, System.Random rng)
    {
        Prim(h, PrimitiveType.Cube, pos + Vector3.up * 0.18f, new Vector3(0.9f, 0.36f, 0.4f), wood);
        for (int i = 0; i < 3; i++)
        {
            Prim(h, PrimitiveType.Sphere, pos + new Vector3(-0.28f + i * 0.28f, 0.42f, 0), new Vector3(0.3f, 0.22f, 0.3f), leaf);
            Prim(h, PrimitiveType.Sphere, pos + new Vector3(-0.28f + i * 0.28f, 0.58f, 0), Vector3.one * 0.17f, goodsMats[rng.Next(goodsMats.Length)]);
        }
    }

    // ── 좌판 ── 기둥 넷 + 줄무늬 차양 + 판매대 + 물건
    static void Stall(Transform parent, Landscape land, Vector3 pos, Vector3 faceTo, System.Random rng)
    {
        var s = new GameObject("Stall").transform;
        s.SetParent(parent, true);
        s.position = pos;
        var look = faceTo - pos; look.y = 0;
        s.rotation = Quaternion.LookRotation(look.normalized);
        var stripe = roofMats[rng.Next(roofMats.Length)];
        foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                Prim(s, PrimitiveType.Cube, new Vector3(sx * 1.2f, sz > 0 ? 1.05f : 1.25f, sz * 0.75f), new Vector3(0.12f, sz > 0 ? 2.1f : 2.5f, 0.12f), wood);
        for (int i = 0; i < 6; i++)
        {
            var strip = Prim(s, PrimitiveType.Cube, new Vector3(-1.25f + i * 0.5f, 2.38f, 0.05f), new Vector3(0.5f, 0.07f, 2.1f), i % 2 == 0 ? stripe : cloth);
            strip.transform.localRotation = Quaternion.Euler(14f, 0, 0);
        }
        Prim(s, PrimitiveType.Cube, new Vector3(0, 0.45f, 0.35f), new Vector3(2.5f, 0.9f, 0.9f), woodLight);
        for (int i = 0; i < 6; i++)
            Prim(s, PrimitiveType.Sphere, new Vector3(-1f + i * 0.4f, 1.02f, 0.25f + (i % 2) * 0.25f), Vector3.one * 0.27f, goodsMats[rng.Next(goodsMats.Length)]);
        var col = s.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.6f, 0.2f);
        col.size = new Vector3(2.6f, 1.2f, 1.3f);
        land.AddBlocker(pos, 2f);
    }

    static void Bench(Transform parent, Vector3 pos, Vector3 faceTo)
    {
        var b = new GameObject("Bench").transform;
        b.SetParent(parent, true);
        b.position = pos;
        var look = faceTo - pos; look.y = 0;
        b.rotation = Quaternion.LookRotation(look.normalized);
        Prim(b, PrimitiveType.Cube, new Vector3(0, 0.45f, 0), new Vector3(1.7f, 0.1f, 0.5f), woodLight);
        Prim(b, PrimitiveType.Cube, new Vector3(0, 0.78f, -0.24f), new Vector3(1.7f, 0.42f, 0.08f), woodLight);
        foreach (float sx in new[] { -1f, 1f }) Prim(b, PrimitiveType.Cube, new Vector3(sx * 0.7f, 0.22f, 0), new Vector3(0.1f, 0.45f, 0.45f), wood);
        var col = b.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.4f, 0);
        col.size = new Vector3(1.7f, 0.8f, 0.5f);
    }

    static void Crate(Transform parent, Vector3 pos, float size, float yaw)
    {
        var c = Prim(parent, PrimitiveType.Cube, Vector3.zero, Vector3.one * size, woodLight);
        c.transform.position = pos + Vector3.up * size / 2f;
        c.transform.rotation = Quaternion.Euler(0, yaw, 0);
        c.AddComponent<BoxCollider>();
        var band = Prim(c.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(1.04f, 0.14f, 1.04f), wood);
        band.transform.localPosition = new Vector3(0, 0.3f, 0);
    }

    static void Barrel(Transform parent, Vector3 pos)
    {
        var b = Prim(parent, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.62f, 0.45f, 0.62f), wood);
        b.transform.position = pos + Vector3.up * 0.45f;
        b.AddComponent<CapsuleCollider>();
        Prim(b.transform, PrimitiveType.Cylinder, new Vector3(0, 0.45f, 0), new Vector3(1.06f, 0.08f, 1.06f), timber);
        Prim(b.transform, PrimitiveType.Cylinder, new Vector3(0, -0.45f, 0), new Vector3(1.06f, 0.08f, 1.06f), timber);
    }

    static void Lamp(Transform parent, Vector3 pos)
    {
        if (Mathf.Abs(Landscape.HeightAt(pos)) > 0.02f) return;
        var l = new GameObject("Lamp").transform;
        l.SetParent(parent, true);
        l.position = pos;
        Prim(l, PrimitiveType.Cylinder, new Vector3(0, 1.4f, 0), new Vector3(0.13f, 1.4f, 0.13f), timber);
        Prim(l, PrimitiveType.Sphere, new Vector3(0, 2.95f, 0), Vector3.one * 0.42f, glow);
        Prim(l, PrimitiveType.Cylinder, new Vector3(0, 3.2f, 0), new Vector3(0.5f, 0.04f, 0.5f), timber);
    }

    static void WoodPile(Transform parent, Landscape land, Vector3 pos, float yaw)
    {
        var p = new GameObject("WoodPile").transform;
        p.SetParent(parent, true);
        p.position = pos;
        p.rotation = Quaternion.Euler(0, yaw, 0);
        int k = 0;
        for (int row = 0; row < 3; row++)
            for (int i = 0; i < 3 - row; i++)
            {
                var log = Prim(p, PrimitiveType.Cylinder, new Vector3((i - (2 - row) / 2f) * 0.44f, 0.22f + row * 0.38f, 0), new Vector3(0.42f, 0.8f, 0.42f), k++ % 2 == 0 ? wood : woodLight);
                log.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            }
        var col = p.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.5f, 0);
        col.size = new Vector3(1.4f, 1f, 1.6f);
        land.AddBlocker(pos, 1.2f);
    }

    static void Fence(Transform parent, Vector3 a, Vector3 b)
    {
        var f = new GameObject("TownFence").transform;
        f.SetParent(parent, true);
        f.position = (a + b) / 2f;
        f.rotation = Quaternion.LookRotation((b - a).normalized);
        float len = (b - a).magnitude;
        Prim(f, PrimitiveType.Cube, new Vector3(0, 0.5f, -len / 2f), new Vector3(0.16f, 1f, 0.16f), wood);
        Prim(f, PrimitiveType.Cube, new Vector3(0, 0.75f, 0), new Vector3(0.07f, 0.1f, len), woodLight);
        Prim(f, PrimitiveType.Cube, new Vector3(0, 0.4f, 0), new Vector3(0.07f, 0.1f, len), woodLight);
        var col = f.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.5f, 0);
        col.size = new Vector3(0.2f, 1f, len);
    }

    // 꽃 제단: 낮은 돌 단 위에 꽃들(이야기 25일차의 제단)
    static void Altar(Transform root, System.Random rng)
    {
        Prim(root, PrimitiveType.Cylinder, new Vector3(0, 0.15f, 0), new Vector3(2.6f, 0.15f, 2.6f), stone).AddComponent<CapsuleCollider>();
        Prim(root, PrimitiveType.Cylinder, new Vector3(0, 0.45f, 0), new Vector3(1.6f, 0.15f, 1.6f), stone);
        for (int i = 0; i < 7; i++)
        {
            float a = i / 7f * Mathf.PI * 2f, r = i == 0 ? 0 : 0.5f;
            var p = new Vector3(Mathf.Cos(a) * r, 0.6f, Mathf.Sin(a) * r);
            Prim(root, PrimitiveType.Cylinder, p + Vector3.up * 0.18f, new Vector3(0.05f, 0.18f, 0.05f), leaf);
            Prim(root, PrimitiveType.Sphere, p + Vector3.up * 0.42f, new Vector3(0.3f, 0.2f, 0.3f), goodsMats[rng.Next(goodsMats.Length)]);
        }
    }

    static void Well(Transform root)
    {
        Prim(root, PrimitiveType.Cylinder, new Vector3(0, 0.4f, 0), new Vector3(2f, 0.4f, 2f), stone).AddComponent<CapsuleCollider>();
        Prim(root, PrimitiveType.Cylinder, new Vector3(0, 0.81f, 0), new Vector3(1.5f, 0.01f, 1.5f), roofMats[1]); // 물
        foreach (float sx in new[] { -1f, 1f }) Prim(root, PrimitiveType.Cube, new Vector3(sx * 0.95f, 1.4f, 0), new Vector3(0.14f, 2.8f, 0.14f), wood);
        for (int s = -1; s <= 1; s += 2)
        {
            var slab = Prim(root, PrimitiveType.Cube, new Vector3(0, 2.95f, s * 0.42f), new Vector3(2.6f, 0.1f, 1.1f), roofMats[0]);
            slab.transform.localRotation = Quaternion.Euler(s * 32f, 0, 0);
        }
        Prim(root, PrimitiveType.Cylinder, new Vector3(0, 2.45f, 0), new Vector3(0.14f, 0.95f, 0.14f), timber).transform.localRotation = Quaternion.Euler(0, 0, 90f);
    }

    // 충돌 없는 도형 하나(재질은 나눠 쓴다). pos·scale 은 parent 기준
    static GameObject Prim(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    // 박공: 가로 1 · 높이 1 · 세로 1 인 세모 기둥(마루가 가로 방향). 밑면 가운데가 원점
    static Mesh GableMesh()
    {
        var v = new[]
        {
            new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(-0.5f, 1, 0),   // 왼쪽 세모
            new Vector3(0.5f, 0, -0.5f), new Vector3(0.5f, 1, 0), new Vector3(0.5f, 0, 0.5f),      // 오른쪽 세모
            new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 1, 0), new Vector3(-0.5f, 1, 0),    // 앞 비탈
            new Vector3(0.5f, 0, -0.5f), new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 1, 0), new Vector3(0.5f, 1, 0),  // 뒤 비탈
        };
        var tri = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 6, 8, 9, 10, 11, 12, 10, 12, 13 };
        var m = new Mesh { vertices = v, triangles = tri };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}
