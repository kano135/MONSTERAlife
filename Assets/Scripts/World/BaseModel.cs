using UnityEngine;

// 기지 건물(코드로 빚은 3D, 그림 참고): 둥근 받침 위에 큰 흰 돔(앞에 아치 입구 · 유리창 너머 잎), 왼쪽에 주황 띠 탑과
// 동그란 창 · 꼭대기 위성 안테나(주황 끝), 앞 왼쪽 작은 돔, 오른쪽 주황 지붕 별채와 파란 유리 돔, 뒤쪽 은색 저장탑,
// 둘레에 나무 상자와 덤불. 문은 로컬 +Z 쪽이고, 문 앞 발 자리에 "DoorSpot" 을 둔다(BaseInterior 가 문으로 쓴다).
// ★ 크기·색은 아래 숫자만 고치면 된다(단위: 미터).
public static class BaseModel
{
    static readonly Color White = new Color(0.95f, 0.95f, 0.96f), Gray = new Color(0.78f, 0.79f, 0.82f), Dark = new Color(0.36f, 0.37f, 0.42f);
    static readonly Color Orange = new Color(0.97f, 0.55f, 0.18f), Green = new Color(0.62f, 0.8f, 0.36f), Glass = new Color(0.55f, 0.75f, 0.98f);
    static readonly Color Deck = new Color(0.9f, 0.86f, 0.8f), DeckEdge = new Color(0.66f, 0.64f, 0.7f), Wood = new Color(0.75f, 0.56f, 0.36f), Leaf = new Color(0.36f, 0.62f, 0.3f);

    const float DeckTop = 0.25f; // 받침 높이(로봇이 그냥 걸어 올라설 수 있게 낮게)

    public static void Build(Transform root)
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        Material M(Color c, float smooth = 0.25f) { var m = new Material(lit) { color = c }; m.SetFloat("_Smoothness", smooth); return m; }
        var white = M(White, 0.35f); var gray = M(Gray, 0.4f); var dark = M(Dark); var orange = M(Orange); var green = M(Green);
        var glass = M(Glass, 0.8f); var deck = M(Deck, 0.1f); var deckEdge = M(DeckEdge); var wood = M(Wood, 0.05f); var leaf = M(Leaf, 0.05f);
        var glow = M(new Color(0.75f, 0.9f, 1f), 0.8f);
        glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor", new Color(0.5f, 0.75f, 1f) * 0.8f);

        // ── 받침(둥근 모서리 판) ──
        const float W = 12f, D = 9.4f, R = 1.2f;
        Box(root, new Vector3(0, DeckTop / 2f, 0), new Vector3(W - 2 * R, DeckTop, D), deck, true);
        Box(root, new Vector3(0, DeckTop / 2f, 0), new Vector3(W, DeckTop, D - 2 * R), deck, true);
        foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                Part(root, PrimitiveType.Cylinder, new Vector3(sx * (W / 2f - R), DeckTop / 2f, sz * (D / 2f - R)), new Vector3(2 * R, DeckTop / 2f, 2 * R), deck);
        Box(root, new Vector3(0, 0.04f, 0), new Vector3(W + 0.3f, 0.08f, D + 0.3f), deckEdge, false); // 테두리 그늘
        // 판 줄눈
        for (int i = -3; i <= 3; i++) Box(root, new Vector3(i * 1.6f, DeckTop + 0.005f, 0), new Vector3(0.04f, 0.01f, D - 0.6f), deckEdge, false);

        // ── 큰 돔 ──
        var dome = new Vector3(1.1f, DeckTop, -0.4f);
        const float DR = 3.1f;
        var d = Part(root, PrimitiveType.Sphere, dome, new Vector3(DR * 2f, DR * 1.7f, DR * 2f), white);
        d.AddComponent<SphereCollider>();
        Part(root, PrimitiveType.Cylinder, dome + Vector3.up * 0.45f, new Vector3(DR * 2f + 0.12f, 0.08f, DR * 2f + 0.12f), orange);   // 주황 띠
        Part(root, PrimitiveType.Cylinder, dome + Vector3.up * DR * 0.82f, new Vector3(1.9f, 0.12f, 1.9f), gray);                       // 꼭대기 고리
        Part(root, PrimitiveType.Sphere, dome + Vector3.up * DR * 0.86f, new Vector3(1.6f, 0.9f, 1.6f), white);                          // 꼭대기 작은 돔
        // 앞 아치 입구: 반원 통로 + 유리창(잎이 비친다) + 문
        var arch = dome + new Vector3(0, 0, DR - 0.2f);
        var tunnel = Part(root, PrimitiveType.Cylinder, arch + new Vector3(0, 0, 0.55f), new Vector3(3.2f, 0.75f, 3.2f), white);
        tunnel.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        tunnel.AddComponent<CapsuleCollider>().direction = 1;
        var rim = Part(root, PrimitiveType.Cylinder, arch + new Vector3(0, 0, 1.31f), new Vector3(3.35f, 0.06f, 3.35f), gray);
        rim.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        var pane = Part(root, PrimitiveType.Cylinder, arch + new Vector3(0, 0.02f, 1.33f), new Vector3(2.5f, 0.04f, 2.5f), glass);
        pane.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        for (int i = 0; i < 5; i++) // 유리 너머 잎
        {
            var lf = Part(root, PrimitiveType.Sphere, arch + new Vector3(-0.7f + i * 0.35f, 0.55f + (i % 2) * 0.25f, 1.32f), new Vector3(0.5f, 0.22f, 0.05f), leaf);
            lf.transform.localRotation = Quaternion.Euler(0, 0, -40f + i * 20f);
        }
        Box(root, arch + new Vector3(0, DeckTop + 0.05f, 1.36f), new Vector3(2.1f, 0.16f, 0.1f), gray, false); // 문지방
        // 돔 옆 동그란 창
        foreach (float a in new[] { -55f, 55f, 120f })
            Porthole(root, dome + Quaternion.Euler(0, a, 0) * new Vector3(0, 1.3f, DR * 0.93f), Quaternion.Euler(0, a, 0), 0.55f, gray, glass);

        // ── 탑(왼쪽) ──
        var tower = new Vector3(-3.9f, DeckTop, -0.9f);
        const float TR = 1.25f, TH = 4.4f;
        var t = Part(root, PrimitiveType.Cylinder, tower + Vector3.up * TH / 2f, new Vector3(TR * 2f, TH / 2f, TR * 2f), white);
        t.AddComponent<CapsuleCollider>();
        Part(root, PrimitiveType.Cylinder, tower + Vector3.up * 0.9f, new Vector3(TR * 2f + 0.1f, 0.14f, TR * 2f + 0.1f), green);
        foreach (float y in new[] { 2.3f, 3.6f })
            Part(root, PrimitiveType.Cylinder, tower + Vector3.up * y, new Vector3(TR * 2f + 0.12f, 0.2f, TR * 2f + 0.12f), orange);
        for (int i = 0; i < 4; i++)
        {
            float a = -40f + i * 40f;
            Porthole(root, tower + Quaternion.Euler(0, a, 0) * new Vector3(0, 3.6f, TR + 0.04f), Quaternion.Euler(0, a, 0), 0.42f, gray, glow);
            if (i % 2 == 0) Porthole(root, tower + Quaternion.Euler(0, a + 20f, 0) * new Vector3(0, 1.6f, TR + 0.03f), Quaternion.Euler(0, a + 20f, 0), 0.36f, gray, glass);
        }
        Part(root, PrimitiveType.Cylinder, tower + Vector3.up * (TH + 0.2f), new Vector3(TR * 1.7f, 0.2f, TR * 1.7f), gray);
        Part(root, PrimitiveType.Cylinder, tower + Vector3.up * (TH + 0.75f), new Vector3(0.35f, 0.4f, 0.35f), dark);
        // 위성 안테나: 접시 + 받침대 + 주황 끝
        var dishPos = tower + Vector3.up * (TH + 1.6f);
        var dish = Part(root, PrimitiveType.Sphere, dishPos, new Vector3(2.4f, 0.45f, 2.4f), white);
        dish.transform.localRotation = Quaternion.Euler(-35f, 25f, 0);
        var arm = Part(root, PrimitiveType.Cylinder, dishPos + dish.transform.localRotation * Vector3.up * 0.55f, new Vector3(0.07f, 0.45f, 0.07f), dark);
        arm.transform.localRotation = dish.transform.localRotation;
        Part(root, PrimitiveType.Sphere, dishPos + dish.transform.localRotation * Vector3.up * 1.05f, Vector3.one * 0.32f, orange);

        // ── 작은 돔(앞 왼쪽) ──
        var small = new Vector3(-4.6f, DeckTop, 2.1f);
        Part(root, PrimitiveType.Sphere, small, new Vector3(2.2f, 2.2f, 2.2f), white).AddComponent<SphereCollider>();
        Part(root, PrimitiveType.Cylinder, small + Vector3.up * 0.2f, new Vector3(2.26f, 0.06f, 2.26f), gray);
        Porthole(root, small + new Vector3(0.35f, 0.55f, 0.95f), Quaternion.Euler(0, 20f, 0), 0.38f, gray, glass);
        Part(root, PrimitiveType.Cylinder, small + Vector3.up * 1.12f, new Vector3(0.2f, 0.15f, 0.2f), orange);

        // ── 별채(오른쪽 앞) + 파란 유리 돔 ──
        var annex = new Vector3(5f, DeckTop, 1.3f);
        Box(root, annex + Vector3.up * 0.85f, new Vector3(2f, 1.7f, 1.9f), white, true);
        Box(root, annex + Vector3.up * 1.78f, new Vector3(2.3f, 0.18f, 2.2f), orange, false);
        Box(root, annex + new Vector3(0, 0.95f, 0.96f), new Vector3(1.3f, 0.75f, 0.05f), glass, false);
        Box(root, annex + new Vector3(0, 0.95f, 0.97f), new Vector3(1.42f, 0.08f, 0.04f), dark, false);
        var bubble = Part(root, PrimitiveType.Sphere, new Vector3(3.6f, DeckTop, 0.2f), new Vector3(2.2f, 2.6f, 2.2f), glass);
        bubble.AddComponent<SphereCollider>();
        // 별채 앞 벤치 겸 화단
        Box(root, annex + new Vector3(0, 0.25f, 1.5f), new Vector3(1.8f, 0.12f, 0.45f), wood, false);
        foreach (float sx in new[] { -0.75f, 0.75f }) Box(root, annex + new Vector3(sx, 0.12f, 1.5f), new Vector3(0.1f, 0.25f, 0.4f), dark, false);

        // ── 저장탑(뒤 오른쪽) ──
        var silo = new Vector3(4.4f, DeckTop, -2.4f);
        Part(root, PrimitiveType.Cylinder, silo + Vector3.up * 0.9f, new Vector3(1.4f, 0.9f, 1.4f), gray).AddComponent<CapsuleCollider>();
        Part(root, PrimitiveType.Sphere, silo + Vector3.up * 1.8f, new Vector3(1.4f, 0.8f, 1.4f), gray);
        for (int i = 0; i < 3; i++) Part(root, PrimitiveType.Cylinder, silo + Vector3.up * (0.4f + i * 0.5f), new Vector3(1.44f, 0.02f, 1.44f), dark);

        // ── 상자·덤불 ──
        var crates = new[] { new Vector3(-5.4f, 0, -2.6f), new Vector3(-5f, 0, -3.3f), new Vector3(-2.3f, 0, 3.4f), new Vector3(5.5f, 0, -0.6f), new Vector3(2.9f, 0, 3.5f) };
        for (int i = 0; i < crates.Length; i++)
        {
            float s = 0.55f + (i % 3) * 0.15f;
            var c = Box(root, crates[i] + Vector3.up * (DeckTop + s / 2f), Vector3.one * s, wood, true);
            c.transform.localRotation = Quaternion.Euler(0, i * 23f, 0);
            Box(c.transform, Vector3.zero, new Vector3(1.04f, 0.15f, 1.04f), M(Wood * 0.8f), false).transform.localPosition = new Vector3(0, 0.25f, 0);
        }
        var bushes = new[] { new Vector3(-3f, 0, 2.6f), new Vector3(-1.8f, 0, 3.1f), new Vector3(0.9f, 0, -3.6f), new Vector3(5.4f, 0, 3.2f), new Vector3(-5.5f, 0, 0.6f) };
        for (int i = 0; i < bushes.Length; i++)
            Part(root, PrimitiveType.Sphere, bushes[i] + Vector3.up * (DeckTop + 0.25f), new Vector3(1f, 0.75f, 0.9f) * (0.8f + (i % 3) * 0.15f), leaf);

        // 문 앞 발 자리(BaseInterior 가 바깥 문으로 쓴다): 아치 바로 앞, 받침 위
        var spot = new GameObject("DoorSpot").transform;
        spot.SetParent(root, false);
        spot.localPosition = arch + new Vector3(0, 0, 2.1f);
        spot.localRotation = Quaternion.identity; // 앞(+Z)이 바깥
    }

    // 동그란 창: 회색 테 + 안쪽 유리(벽 바깥쪽을 본다)
    static void Porthole(Transform root, Vector3 pos, Quaternion facing, float size, Material frame, Material pane)
    {
        var f = Part(root, PrimitiveType.Cylinder, pos, new Vector3(size, 0.05f, size), frame);
        f.transform.localRotation = facing * Quaternion.Euler(90f, 0, 0);
        var p = Part(root, PrimitiveType.Cylinder, pos + facing * Vector3.forward * 0.03f, new Vector3(size * 0.72f, 0.05f, size * 0.72f), pane);
        p.transform.localRotation = facing * Quaternion.Euler(90f, 0, 0);
    }

    static GameObject Box(Transform parent, Vector3 pos, Vector3 size, Material mat, bool solid)
    {
        var go = Part(parent, PrimitiveType.Cube, pos, size, mat);
        if (solid) go.AddComponent<BoxCollider>();
        return go;
    }

    static GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }
}
