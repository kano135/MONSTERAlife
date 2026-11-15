using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 비밀 동굴: 폭포 산의 폭포 반대쪽(남서쪽) 기슭에 작은 동굴 입구. 옆에서 원주민이 지킨다.
//   OpenDay 전에는 못 들어간다(입구가 막혀 있고, 다가가면 지킴이가 "신비한 곳이라 들어갈 수 없어요"를 되풀이).
//   OpenDay 부터는 지킴이가 비켜 주고, 입구로 걸어 들어가면 안쪽 큰 동굴(땅 밑에 따로 지은 공간)로 옮겨진다.
//   안쪽: 천장이 뚫려 빛이 쏟아지고(위로는 절대 못 나간다), 폭포가 웅덩이로 떨어지고, 가운데 빛나는 큰 나무.
//   나무 밑에 전설의 씨앗(별빛 꽃) 하나. 비닐하우스에 심어 피우고 수확하면 지구로 보내져 마지막 이야기가 시작된다(GameManager.HarvestLegend).
// ★ 자리·열리는 날은 아래 숫자만 고치면 된다.
public class SecretCave : MonoBehaviour
{
    public static SecretCave Instance { get; private set; }

    public const int OpenDay = 90; // 이날부터 들어갈 수 있다
    public static readonly Vector3 Mouth = new Vector3(-262.5f, 0, -39f);       // 바깥 입구(땅 높이는 자동)
    static readonly Vector3 Outward = new Vector3(-0.9f, 0, -0.44f);     // 입구가 바라보는 쪽(산 바깥)
    public static readonly Vector3 Origin = new Vector3(-215f, -160f, -20f); // 안쪽 동굴 바닥 가운데
    const float Radius = 24f, Height = 22f, HoleRadius = 7f;
    const float GuardTalkRange = 6f, EnterRange = 1.3f;

    Vector3 mouthGround, insideMouth, spawnInside;
    GameObject block;           // 열리기 전 입구를 막는 벽
    Transform guard;
    Vector3 guardBlockPos, guardAsidePos;
    GameObject seed;
    bool busy, announced;
    float talkAt;
    Material fallMat;
    readonly List<Transform> fireflies = new List<Transform>();

    public static bool Inside(Vector3 p) => p.y < Origin.y + Height + 5f && p.y > Origin.y - 10f;

    public static SecretCave Build()
    {
        var cave = new GameObject("SecretCave").AddComponent<SecretCave>();
        Instance = cave;
        cave.BuildEntrance();
        cave.BuildCavern();
        return cave;
    }

    // ── 바깥 입구 ── 바위가 겹겹이 쌓인 작은 굴, 어두운 구멍, 흙길과 덤불, 옆에 지킴이
    void BuildEntrance()
    {
        var o = Outward.normalized;
        var side = Vector3.Cross(Vector3.up, o);
        mouthGround = Mouth; mouthGround.y = Landscape.HeightAt(Mouth);
        var root = new GameObject("CaveEntrance").transform;
        root.SetParent(transform, false);
        root.position = mouthGround;
        root.rotation = Quaternion.LookRotation(o);
        var rock = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Landscape.RockColor * 0.92f };
        var dark = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.03f, 0.03f, 0.06f) };

        StoneHut(root);
        // 덤불·나무(팩이 있으면)
        if (NaturePack.Available)
        {
            for (int i = 0; i < 6; i++)
            {
                var p = mouthGround + side * ((i % 2 == 0 ? -1 : 1) * (3.5f + i * 0.7f)) + o * (0.5f + i * 0.5f);
                p.y = Landscape.HeightAt(p);
                NaturePack.Spawn("bush_flower_01", root.parent, p, i * 61f, 1.1f + (i % 3) * 0.2f);
            }
            var t1 = mouthGround + side * 6.5f - o * 1f; t1.y = Landscape.HeightAt(t1);
            var tree = NaturePack.Spawn("tree_oak_01", root.parent, t1, 30f, 1.6f);
            if (tree) NaturePack.Recolor(tree, NaturePack.Tint.Teal);
        }

        // 막는 벽(보이지 않음): 열리기 전
        block = new GameObject("CaveBlock");
        block.transform.SetParent(root, false);
        block.transform.localPosition = new Vector3(0, 1.3f, 0.9f);
        block.AddComponent<BoxCollider>().size = new Vector3(2f, 2.6f, 0.6f);

        // 지킴이(원주민): 열리기 전에는 입구 바로 앞, 열리면 옆으로 비킨다
        guardBlockPos = mouthGround + o * 1.6f;
        guardAsidePos = mouthGround + o * 1.4f + side * 2.6f;
        guardAsidePos.y = Landscape.HeightAt(guardAsidePos);
        guard = NativeModel.Build(1.5f).transform;
        guard.SetParent(transform, false);
        guard.name = "CaveGuard";
        guard.position = guardBlockPos;
        guard.rotation = Quaternion.LookRotation(o);
    }

    // 돌무지 굴 입구(그림 참고): 둥근 돌을 고리처럼 쌓아 올린 작은 돔 + 위에 이끼 + 박힌 색 보석
    // + 앞쪽 나무 문틀과 바닥 널 + 흙 마당 + 풀포기. 문(폭 1.6m · 높이 2.3m)은 +Z(바깥) 쪽.
    static void StoneHut(Transform root)
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        Material M(Color c, float smooth = 0.1f) { var m = new Material(lit) { color = c }; m.SetFloat("_Smoothness", smooth); return m; }
        var stones = new[] { M(new Color(0.62f, 0.6f, 0.58f)), M(new Color(0.55f, 0.53f, 0.52f)), M(new Color(0.68f, 0.66f, 0.62f)), M(new Color(0.5f, 0.5f, 0.52f)) };
        var moss = new[] { M(new Color(0.45f, 0.7f, 0.22f)), M(new Color(0.55f, 0.78f, 0.28f)), M(new Color(0.36f, 0.6f, 0.2f)) };
        var wood = M(new Color(0.55f, 0.36f, 0.22f));
        var woodDark = M(new Color(0.4f, 0.25f, 0.16f));
        var dark = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.04f, 0.035f, 0.04f) };
        var dirt = M(new Color(0.84f, 0.7f, 0.52f));
        var gems = new Material[4];
        Color[] gemColors = { new Color(0.3f, 0.6f, 1f), new Color(1f, 0.55f, 0.2f), new Color(0.35f, 0.9f, 0.5f), new Color(1f, 0.35f, 0.4f) };
        for (int i = 0; i < gems.Length; i++)
        {
            gems[i] = M(gemColors[i], 0.8f);
            gems[i].EnableKeyword("_EMISSION");
            gems[i].SetColor("_EmissionColor", gemColors[i] * 0.6f);
        }

        var hut = new GameObject("StoneHut").transform;
        hut.SetParent(root, false);
        var c = new Vector3(0, 0, -2.2f);              // 돔 가운데(입구 판 뒤쪽)
        const float R = 3.1f, DoorHalfDeg = 26f;

        // 안쪽 어둠(문 너머로 보이는 굴 속)
        Part(hut, PrimitiveType.Sphere, c + new Vector3(0, 1.4f, 0.2f), new Vector3(4.6f, 3.6f, 4.6f), dark);

        // 돌 고리 4층: 아래는 크고 넓게, 위로 갈수록 작고 좁게. 아래 두 층은 문 자리를 비운다.
        int idx = 0;
        float[] ringY = { 0.55f, 1.55f, 2.5f, 3.25f };
        float[] ringR = { R, R * 0.93f, R * 0.74f, R * 0.45f };
        float[] stoneW = { 1.55f, 1.4f, 1.25f, 1.05f };
        for (int k = 0; k < ringY.Length; k++)
        {
            int n = Mathf.Max(4, Mathf.RoundToInt(2f * Mathf.PI * ringR[k] / (stoneW[k] * 0.82f)));
            for (int i = 0; i < n; i++)
            {
                float deg = (i + (k % 2) * 0.5f) / n * 360f;           // 0도 = 문(+Z)
                float rel = Mathf.DeltaAngle(deg, 0f);
                if (k < 2 && Mathf.Abs(rel) < DoorHalfDeg) continue;    // 문 자리
                var dir = new Vector3(Mathf.Sin(deg * Mathf.Deg2Rad), 0, Mathf.Cos(deg * Mathf.Deg2Rad)); // 0도 = +Z(문)
                float wob = Mathf.Sin(idx * 2.3f) * 0.12f;
                var pos = c + dir * (ringR[k] + wob) + Vector3.up * (ringY[k] + Mathf.Sin(idx * 1.7f) * 0.08f);
                var st = Part(hut, PrimitiveType.Sphere, pos, new Vector3(stoneW[k] * (0.95f + wob), 1.05f - k * 0.08f, 1.05f) , stones[idx % stones.Length]);
                st.transform.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(Mathf.Sin(idx) * 8f, 0, Mathf.Cos(idx * 1.3f) * 10f);
                st.AddComponent<SphereCollider>();
                st.name = "HutStone";
                // 군데군데 색 보석
                if (idx % 3 == 1)
                {
                    var g = Part(hut, PrimitiveType.Sphere, pos + dir * 0.5f + Vector3.up * (Mathf.Sin(idx) * 0.25f), Vector3.one * 0.13f, gems[idx % gems.Length]);
                    g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                idx++;
            }
        }
        // 꼭대기 덮개 돌
        Part(hut, PrimitiveType.Sphere, c + Vector3.up * 3.85f, new Vector3(1.9f, 0.8f, 1.9f), stones[2]).AddComponent<SphereCollider>();
        // 문 위 아치 돌(문틀 위를 덮는다)
        for (int i = -1; i <= 1; i++)
        {
            var arch = Part(hut, PrimitiveType.Sphere, c + new Vector3(i * 0.85f, 2.55f - Mathf.Abs(i) * 0.25f, R * 0.98f), new Vector3(1.05f, 0.85f, 0.95f), stones[(i + 4) % stones.Length]);
            arch.transform.localRotation = Quaternion.Euler(0, 0, i * -18f);
        }

        // 이끼: 꼭대기를 덮고 옆으로 흘러내린다
        for (int i = 0; i < 22; i++)
        {
            float deg = i * 137.5f, up = i < 8 ? 0 : 1;
            float r = i < 8 ? i * 0.22f : 1.1f + (i % 5) * 0.35f;
            var d = new Vector3(Mathf.Sin(deg * Mathf.Deg2Rad), 0, Mathf.Cos(deg * Mathf.Deg2Rad));
            float y = 4.2f - r * r * 0.22f - up * (i % 3) * 0.25f;
            var m = Part(hut, PrimitiveType.Sphere, c + d * r + Vector3.up * y, new Vector3(1.1f, 0.38f, 1.1f) * (0.8f + (i % 4) * 0.15f), moss[i % moss.Length]);
            m.transform.localRotation = Quaternion.LookRotation(d) * Quaternion.Euler(-r * 18f, 0, 0);
            m.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        // 이끼 늘어짐(가는 덩굴)
        for (int i = 0; i < 6; i++)
        {
            float deg = -60f + i * 24f;
            var d = new Vector3(Mathf.Sin(deg * Mathf.Deg2Rad), 0, Mathf.Cos(deg * Mathf.Deg2Rad));
            Part(hut, PrimitiveType.Capsule, c + d * (R * 0.82f) + Vector3.up * 2.75f, new Vector3(0.14f, 0.45f + (i % 3) * 0.15f, 0.14f), moss[i % moss.Length]);
        }

        // 나무 문틀: 양옆 기둥 · 위 가로대 · 비스듬한 버팀대 · 바닥 널
        float doorZ = c.z + R * 0.92f;
        foreach (float sx in new[] { -1f, 1f })
        {
            var post = Part(hut, PrimitiveType.Cube, new Vector3(sx * 0.85f, 1.15f, doorZ), new Vector3(0.22f, 2.3f, 0.22f), wood);
            post.transform.localRotation = Quaternion.Euler(0, 0, sx * 4f);
            var brace = Part(hut, PrimitiveType.Cube, new Vector3(sx * 0.55f, 1.95f, doorZ - 0.05f), new Vector3(0.14f, 0.7f, 0.14f), woodDark);
            brace.transform.localRotation = Quaternion.Euler(0, 0, sx * 45f);
        }
        Part(hut, PrimitiveType.Cube, new Vector3(0, 2.35f, doorZ), new Vector3(2.1f, 0.24f, 0.26f), wood);
        for (int i = 0; i < 5; i++)
        {
            var plank = Part(hut, PrimitiveType.Cube, new Vector3(-0.6f + i * 0.3f, 0.06f, doorZ + 0.6f - (i % 2) * 0.15f), new Vector3(0.27f, 0.08f, 2.2f), i % 2 == 0 ? wood : woodDark);
            plank.transform.localRotation = Quaternion.Euler(0, (i - 2) * 3f, 0);
        }

        // 흙 마당 + 작은 돌 + 풀포기
        Part(hut, PrimitiveType.Cylinder, new Vector3(0.4f, 0.025f, doorZ + 2.4f), new Vector3(3.4f, 0.01f, 2.8f), dirt);
        for (int i = 0; i < 6; i++)
            Part(hut, PrimitiveType.Sphere, new Vector3((i % 2 == 0 ? -1 : 1) * (1.4f + i * 0.25f), 0.15f, doorZ + 0.8f + i * 0.4f), new Vector3(0.7f, 0.45f, 0.6f) * (0.6f + (i % 3) * 0.25f), stones[i % stones.Length]);
        for (int i = 0; i < 26; i++)
        {
            float deg = i * 47f;
            var d = new Vector3(Mathf.Sin(deg * Mathf.Deg2Rad), 0, Mathf.Cos(deg * Mathf.Deg2Rad));
            if (Mathf.Abs(Mathf.DeltaAngle(deg, 0)) < 30f) continue; // 문 앞은 비운다
            var basePos = c + d * (R + 0.6f + (i % 3) * 0.3f);
            for (int b = 0; b < 3; b++)
            {
                var blade = Part(hut, PrimitiveType.Capsule, basePos + new Vector3((b - 1) * 0.12f, 0.25f, 0), new Vector3(0.07f, 0.28f + b * 0.06f, 0.07f), moss[(i + b) % moss.Length]);
                blade.transform.localRotation = Quaternion.Euler((b - 1) * 18f, deg, (b - 1) * -14f);
                blade.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }

    // ── 안쪽 큰 동굴 ──
    void BuildCavern()
    {
        var root = new GameObject("Cavern").transform;
        root.SetParent(transform, false);
        root.position = Origin;
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var wallMat = new Material(lit) { color = new Color(0.2f, 0.26f, 0.42f) };
        wallMat.SetFloat("_Smoothness", 0.05f);
        var floorMat = new Material(lit) { color = new Color(0.36f, 0.34f, 0.4f) };

        // 바닥(충돌) + 웅덩이
        var floor = Part(root, PrimitiveType.Cylinder, new Vector3(0, -0.5f, 0), new Vector3(Radius * 2f + 2f, 0.5f, Radius * 2f + 2f), floorMat);
        floor.AddComponent<MeshCollider>();
        floor.name = "CaveFloor";
        var water = ArtLibrary.Transparent(null, new Color(0.25f, 0.95f, 0.95f, 0.75f));
        water.renderQueue = 3000;
        var pool = Part(root, PrimitiveType.Cylinder, new Vector3(-2f, 0.04f, -3f), new Vector3(24f, 0.01f, 20f), water);
        pool.name = "CavePool";

        // 벽: 아래는 넓고 위로 갈수록 좁아지다 천장 가운데가 뚫린 돔(안쪽을 본다, 울퉁불퉁)
        var dome = new GameObject("CaveWall", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider)); // 이름이 Hill 이 아니라서 타고 오를 수 없다
        dome.transform.SetParent(root, false);
        var mesh = DomeMesh();
        dome.GetComponent<MeshFilter>().sharedMesh = mesh;
        dome.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
        dome.GetComponent<MeshCollider>().sharedMesh = mesh;
        // 천장 구멍 위 보이지 않는 뚜껑: 위로는 절대 못 나간다(밖에서도 못 들어온다 — 동굴은 땅 밑 따로 있는 공간)
        var cap = new GameObject("CaveCap").AddComponent<BoxCollider>();
        cap.transform.SetParent(root, false);
        cap.transform.localPosition = new Vector3(0, Height - 1f, 0);
        cap.size = new Vector3(HoleRadius * 3f, 1f, HoleRadius * 3f);

        // 바위 덩어리: 벽 둘레와 웅덩이 가
        var rockMat = new Material(lit) { color = new Color(0.32f, 0.36f, 0.5f) };
        var warmRock = new Material(lit) { color = new Color(0.78f, 0.6f, 0.5f) };
        for (int i = 0; i < 26; i++)
        {
            float a = i / 26f * Mathf.PI * 2f + (i % 3) * 0.1f;
            float d = Radius - 2.5f - (i % 4) * 1.4f;
            var r = Part(root, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * d, 0.4f + (i % 3) * 0.3f, Mathf.Sin(a) * d),
                new Vector3(2.4f + i % 3, 1.4f + (i % 4) * 0.6f, 2f + (i % 2)), i % 5 == 0 ? warmRock : rockMat);
            r.transform.localRotation = Quaternion.Euler(0, i * 37f, 0);
            r.AddComponent<SphereCollider>();
        }

        // 천장 구멍에서 떨어지는 폭포 + 빛줄기
        fallMat = ArtLibrary.Transparent(FlowTexture(), new Color(0.75f, 0.95f, 1f, 0.8f));
        fallMat.renderQueue = 3001;
        var fall = Part(root, PrimitiveType.Cube, new Vector3(-HoleRadius + 1.5f, Height / 2f, -3f), new Vector3(0.3f, Height, 3.4f), fallMat);
        fall.name = "CaveFall";
        var shaft = ArtLibrary.Transparent(null, new Color(1f, 0.95f, 0.7f, 0.08f));
        shaft.renderQueue = 3002;
        for (int i = 0; i < 3; i++)
        {
            var beam = Part(root, PrimitiveType.Cylinder, new Vector3(1.5f + i * 1.6f, Height / 2f, 1f - i * 1.2f), new Vector3(2.4f - i * 0.4f, Height / 2f, 2.4f - i * 0.4f), shaft);
            beam.transform.localRotation = Quaternion.Euler(8f - i * 3f, 0, -10f + i * 4f);
            beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // 가운데 큰 나무(빛나는 결) + 씨앗
        var treePos = new Vector3(4f, 0, 4f);
        BigTree(root, treePos);
        var seedLocal = treePos + new Vector3(-2.2f, 0, -2.2f);
        {
            seed = new GameObject("LegendSeed");
            seed.transform.position = Origin + seedLocal;
            var model = PlantModel.BuildSeed(PlantCatalog.Legend.color).transform;
            model.SetParent(seed.transform, false);
            model.localScale = Vector3.one * 1.1f;
            seed.AddComponent<SeedPickup>().plant = PlantCatalog.Legend;
            var glow = new GameObject("SeedGlow").AddComponent<Light>();
            glow.transform.SetParent(seed.transform, false);
            glow.transform.localPosition = Vector3.up * 0.6f;
            glow.type = LightType.Point; glow.color = new Color(0.6f, 1f, 0.95f); glow.range = 5f; glow.intensity = 2.5f;
        }

        // 빛: 웅덩이의 청록빛, 구멍에서 들어오는 햇빛, 나무의 노란빛
        Lamp(root, new Vector3(-2f, 1.5f, -3f), new Color(0.3f, 0.95f, 1f), 26f, 3f);
        Lamp(root, new Vector3(2f, Height - 3f, 0), new Color(1f, 0.92f, 0.75f), 34f, 3.5f);
        Lamp(root, treePos + Vector3.up * 5f, new Color(1f, 0.9f, 0.45f), 14f, 2f);

        // 반딧불이
        var fly = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(1f, 0.95f, 0.45f) };
        for (int i = 0; i < 40; i++)
        {
            float a = i * 2.39996f, d = 3f + (i * 7 % 17);
            var f = Part(root, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * d, 1f + (i * 5 % 9), Mathf.Sin(a) * d), Vector3.one * 0.12f, fly);
            f.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fireflies.Add(f.transform);
        }

        // 나가는 굴(벽에 어두운 구멍) — 들어오면 여기서 시작, 여기로 걸어가면 밖으로
        var dark = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.02f, 0.02f, 0.05f) };
        var exitDir = new Vector3(-1f, 0, -0.3f).normalized;
        var mouthIn = exitDir * (Radius - 0.6f);
        var tunnel = Part(root, PrimitiveType.Cube, mouthIn + Vector3.up * 1.4f, new Vector3(2.4f, 2.8f, 0.4f), dark);
        tunnel.transform.localRotation = Quaternion.LookRotation(-exitDir);
        tunnel.name = "CaveExit";
        insideMouth = Origin + mouthIn - exitDir * 0.2f;
        spawnInside = Origin + mouthIn - exitDir * 3.5f; // 들어오면 굴에서 몇 걸음 안쪽에 선다
    }

    // 씨앗을 이미 가져갔나(가방에 있거나, 밭에 심었거나, 이미 피웠으면 나무 밑 씨앗은 없다)
    static bool SeedTaken(GameManager gm)
    {
        if (gm.LegendBloomed || gm.SeedCount(PlantCatalog.Legend) > 0) return true;
        foreach (var s in gm.Spots) if (s.Plant == PlantCatalog.Legend) return true;
        return false;
    }

    void BigTree(Transform root, Vector3 at)
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var bark = new Material(lit) { color = new Color(0.12f, 0.2f, 0.22f) };
        var leaves = new Material(lit) { color = new Color(0.3f, 0.6f, 0.32f) };
        var leaves2 = new Material(lit) { color = new Color(0.42f, 0.72f, 0.36f) };
        var vein = new Material(lit) { color = new Color(1f, 0.9f, 0.3f) };
        vein.EnableKeyword("_EMISSION");
        vein.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.25f) * 2f);
        var tree = new GameObject("GlowTree").transform;
        tree.SetParent(root, false);
        tree.localPosition = at;
        // 비틀린 줄기: 굵기가 줄어드는 원통 여러 개를 조금씩 비틀어 쌓는다
        Vector3 p = Vector3.zero; float r = 1.6f;
        for (int i = 0; i < 7; i++)
        {
            var seg = Part(tree, PrimitiveType.Cylinder, p + Vector3.up * 0.9f, new Vector3(r * 2f, 0.95f, r * 2f), bark);
            seg.transform.localRotation = Quaternion.Euler(Mathf.Sin(i * 1.3f) * 9f, i * 25f, Mathf.Cos(i * 1.1f) * 9f);
            if (i == 0) seg.AddComponent<CapsuleCollider>();
            p += new Vector3(Mathf.Sin(i * 0.9f) * 0.35f, 1.8f, Mathf.Cos(i * 0.7f) * 0.3f);
            r *= 0.86f;
        }
        // 뿌리
        for (int i = 0; i < 7; i++)
        {
            float a = i / 7f * 360f;
            var root1 = Part(tree, PrimitiveType.Capsule, Quaternion.Euler(0, a, 0) * new Vector3(0, 0.3f, 1.9f), new Vector3(0.6f, 1.4f, 0.6f), bark);
            root1.transform.localRotation = Quaternion.Euler(0, a, 0) * Quaternion.Euler(70f, 0, 0);
        }
        // 빛나는 결(줄기를 따라 노란 줄)
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f * Mathf.Deg2Rad;
            var v = Part(tree, PrimitiveType.Capsule, new Vector3(Mathf.Cos(a) * 1.25f, 3f + i % 3, Mathf.Sin(a) * 1.25f), new Vector3(0.08f, 2.4f, 0.08f), vein);
            v.transform.localRotation = Quaternion.Euler(Mathf.Sin(a) * 12f, 0, Mathf.Cos(a) * 12f);
        }
        // 잎 덩어리
        for (int i = 0; i < 9; i++)
        {
            float a = i * 40f * Mathf.Deg2Rad, d = i == 0 ? 0 : 3.6f + (i % 3);
            Part(tree, PrimitiveType.Sphere, p + new Vector3(Mathf.Cos(a) * d, (i % 3) * 0.8f - 0.6f, Mathf.Sin(a) * d),
                new Vector3(5.5f, 3.6f, 5.5f) * (0.8f + (i % 4) * 0.12f), i % 2 == 0 ? leaves : leaves2);
        }
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (!gm || !gm.Player) return;
        // 반딧불이·폭포 무늬
        float t = Time.time;
        for (int i = 0; i < fireflies.Count; i++)
            fireflies[i].localPosition += new Vector3(Mathf.Sin(t * 0.7f + i), Mathf.Sin(t * 1.3f + i * 2f), Mathf.Cos(t * 0.6f + i)) * 0.004f;
        if (fallMat) fallMat.mainTextureOffset = new Vector2(0, t * 0.8f);
        if (seed && SeedTaken(gm)) Destroy(seed);

        bool open = gm.Day >= OpenDay;
        if (block.activeSelf == open) block.SetActive(!open);
        var target = open ? guardAsidePos : guardBlockPos;
        guard.position = Vector3.MoveTowards(guard.position, target, 2f * Time.deltaTime);
        if (busy || Hud.AnyPanelOpen) return;

        var player = gm.Player;
        var pc = player.GetComponent<PlayerController>();
        var toMouth = player.position - mouthGround; toMouth.y = 0;
        if (!Inside(player.position))
        {
            if (toMouth.magnitude < GuardTalkRange && Time.time > talkAt)
            {
                if (!open)
                {
                    pc.Show("원주민이 막아서요.\n\"여기는 신비한 곳이라 들어갈 수 없어요.\"", 3f);
                    talkAt = Time.time + 4f;
                }
                else if (!announced)
                {
                    announced = true;
                    pc.Show("원주민이 길을 비켜 줘요.\n\"이제 들어가도 좋아요. 큰 나무가 기다리고 있어요.\"", 4f);
                    talkAt = Time.time + 4f;
                }
            }
            if (open && toMouth.magnitude < EnterRange + 0.6f) StartCoroutine(Move(player, spawnInside + Vector3.up * 1f, Yaw(Origin - insideMouth)));
        }
        else
        {
            var toExit = player.position - insideMouth; toExit.y = 0;
            if (toExit.magnitude < EnterRange && Time.time > enteredAt + 1.5f)
                StartCoroutine(Move(player, mouthGround + Outward.normalized * 2.4f + Vector3.up * 1f, Yaw(Outward)));
        }
    }
    float enteredAt;

    static float Yaw(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

    IEnumerator Move(Transform player, Vector3 to, float yaw)
    {
        busy = true;
        var lids = Eyelids.Instance;
        if (lids) yield return lids.Close(0.35f);
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.position = to;
        player.rotation = Quaternion.Euler(0, yaw, 0);
        cc.enabled = true;
        var rig = FindFirstObjectByType<CameraRig>();
        if (rig) rig.SetYaw(yaw);
        enteredAt = Time.time;
        if (lids) yield return lids.Open(0.5f);
        busy = false;
    }

    // 돔: 바닥 둘레 Radius → 위로 갈수록 좁아져 천장 구멍 HoleRadius. 안쪽을 본다. 각도마다 반지름이 조금씩 달라 울퉁불퉁
    static Mesh DomeMesh()
    {
        const int seg = 56, rings = 14;
        var v = new List<Vector3>();
        var tri = new List<int>();
        for (int j = 0; j <= rings; j++)
        {
            float u = j / (float)rings;
            float y = u * Height;
            float baseR = Mathf.Lerp(Radius + 1.5f, HoleRadius, Mathf.Pow(u, 1.6f));
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                float n = Mathf.PerlinNoise(Mathf.Cos(a) * 2f + 10f, y * 0.12f + Mathf.Sin(a) * 2f) - 0.5f;
                float r = baseR * (1f + n * 0.25f * (1f - u * 0.6f));
                v.Add(new Vector3(Mathf.Cos(a) * r, y + (j > 0 && j < rings ? n * 1.2f : 0), Mathf.Sin(a) * r));
            }
        }
        for (int j = 0; j < rings; j++)
            for (int i = 0; i < seg; i++)
            {
                int a = j * (seg + 1) + i, b = a + 1, c = a + seg + 1, d = c + 1;
                tri.Add(a); tri.Add(b); tri.Add(c);   // 안쪽을 보게
                tri.Add(b); tri.Add(d); tri.Add(c);
            }
        var m = new Mesh();
        m.SetVertices(v);
        m.SetTriangles(tri, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    static void Lamp(Transform root, Vector3 pos, Color c, float range, float intensity)
    {
        var l = new GameObject("CaveLight").AddComponent<Light>();
        l.transform.SetParent(root, false);
        l.transform.localPosition = pos;
        l.type = LightType.Point; l.color = c; l.range = range; l.intensity = intensity;
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

    static Texture2D FlowTexture()
    {
        const int w = 32, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float s = Mathf.PerlinNoise(x * 0.35f, y * 0.12f);
                tex.SetPixel(x, y, new Color(0.85f + s * 0.15f, 0.95f, 1f, 0.5f + s * 0.5f));
            }
        tex.Apply();
        return tex;
    }
}
