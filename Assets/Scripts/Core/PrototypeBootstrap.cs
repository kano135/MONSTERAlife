using UnityEngine;
using UnityEngine.SceneManagement;

// 아무 씬에서 Play 를 누르면 시험용 세계(땅·하늘·기지·비닐하우스·로봇·송신기·채집 자원·씨앗·UI)를 코드로 만든다.
// GPT 그림(Assets/Resources/Art)이 있으면 그림을 세워 쓰고, 없으면 기본 도형으로 만든다.
// 씬에 GameManager 가 이미 있으면(진짜 맵을 만든 뒤) 아무것도 안 한다.
public static class PrototypeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        // 게임 오버 뒤 다시 시작(씬 다시 불러오기)할 때도 세계를 새로 만든다
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Build();

    static void Build()
    {
        if (Object.FindFirstObjectByType<GameManager>()) return;

        var plants = PlantCatalog.Load();
        DayNight.Build(GroundView.Build()); // 시간대에 따라 하늘·땅·빛 색이 바뀐다
        var home = BuildBase();
        var player = BuildRobot(home);
        // 시작은 기지 안 방에서(문으로 나가면 계단 꼭대기). 충전기(home)는 그대로 밖에 있다.
        player.position = BaseInterior.Instance.StartPosition;
        player.rotation = Quaternion.Euler(0, BaseInterior.StartYaw, 0);
        var spots = BuildGreenhouse();
        BuildSendStation();
        Lake.Build(LakeCenter, LakeRadius);
        Cliff.Build(); // 맵 왼쪽 끝 절벽·폭포·동굴
        Village.Build(); // 동쪽 원주민 마을(임시: 타원형)
        var nodes = BuildResources();

        var cam = Camera.main ? Camera.main.gameObject : new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
        cam.AddComponent<CameraRig>().target = player;

        var systems = new GameObject("Game");
        systems.AddComponent<GameManager>().Init(plants, spots, nodes, player);
        systems.AddComponent<SeedSpawner>().Init(plants);
        systems.AddComponent<Hud>().player = player.GetComponent<PlayerController>();
        systems.AddComponent<TitleScreen>();
        Eyelids.Build();
        StoryUI.Build();
        systems.AddComponent<StoryDirector>(); // 날짜별 이야기·선택·결말
#if UNITY_EDITOR
        systems.AddComponent<DevScreenshots>();
#endif
    }

    // 기지: 바닥 + 기지 건물 + 충전기(Q로 하루 끝내기, 아침에 여기서 시작) + 둥근 울타리(남쪽이 출입구).
    // 기지 건물은 3D 모델 Resources/Art/Models/base_colony(STELLUX Colony 07) → 그림 Props/base.png → 임시 돔 순서.
    // 충전기 그림은 Props/charger.png 로 넣으면 도형 대신 나온다.
    static readonly Vector3 BaseCenter = new Vector3(0, 0, 1);
    const float FenceRadius = 16f;
    // 호수: 남쪽 출입구로 나가서 왼쪽(남서쪽) 울타리 바로 밖
    static readonly Vector3 LakeCenter = new Vector3(-11f, 0, -21f);
    const float LakeRadius = 4f;

    static Vector3 BuildBase()
    {
        var root = new GameObject("Base").transform;

        var floor = Part(root, PrimitiveType.Cylinder, BaseCenter + Vector3.up * 0.03f, new Vector3(FenceRadius * 2, 0.01f, FenceRadius * 2), new Color(0.82f, 0.8f, 0.76f));
        floor.name = "Floor";

        var dome = new GameObject("Habitat").transform;
        dome.SetParent(root, false);
        dome.position = new Vector3(-10f, 0, -5f);
        var model = ArtLibrary.SpawnModel("base_colony", dome);
        var domeArt = ArtLibrary.World("Props/base");
        if (model)
        {
            // 모델 입구가 +Z 라서 90도 돌려 충전기·비닐하우스 쪽(+X)을 보게 한다
            dome.rotation = Quaternion.Euler(0, 90, 0);
            AddBodyCollider(dome, model);
        }
        else if (domeArt)
        {
            dome.gameObject.AddComponent<SphereCollider>().radius = 3.2f;
            Billboard.CreateByHeight(dome, domeArt, 5.5f, Color.white);
        }
        else
        {
            dome.gameObject.AddComponent<SphereCollider>().radius = 3.2f;
            Part(dome, PrimitiveType.Sphere, Vector3.zero, new Vector3(6.4f, 6f, 6.4f), new Color(0.95f, 0.96f, 0.97f));
            Part(dome, PrimitiveType.Cube, new Vector3(3.1f, 1.1f, 0), new Vector3(0.3f, 2.2f, 1.6f), new Color(0.45f, 0.85f, 0.85f)).name = "Door";
            for (int i = 0; i < 3; i++)
                Part(dome, PrimitiveType.Sphere, new Vector3(2.2f, 2.2f, -1.6f + i * 1.6f), Vector3.one * 0.6f, new Color(0.3f, 0.6f, 0.7f));
        }

        var pad = new GameObject("ChargingPad").transform;
        pad.SetParent(root, false);
        pad.position = new Vector3(-3f, 0, -5f); // 기지 계단 바로 앞
        pad.gameObject.AddComponent<ChargingPad>();
        var padArt = ArtLibrary.World("Props/charger");
        if (padArt) Billboard.CreateByHeight(pad, padArt, 1.6f, Color.white);
        else
        {
            Part(pad, PrimitiveType.Cylinder, Vector3.up * 0.05f, new Vector3(1.8f, 0.05f, 1.8f), new Color(0.45f, 0.85f, 0.85f));
            Part(pad, PrimitiveType.Cylinder, Vector3.up * 0.1f, new Vector3(1.2f, 0.05f, 1.2f), new Color(1f, 0.83f, 0.3f));
        }

        // 울타리: 원을 40조각으로 나눠 낮은 벽을 세우고, 남쪽(-z) 두 조각은 비워 출입구로
        const int segments = 40;
        float length = 2 * Mathf.PI * FenceRadius / segments * 1.05f;
        for (int i = 0; i < segments; i++)
        {
            float angle = (i + 0.5f) / segments * Mathf.PI * 2;
            var dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            if (Vector3.Angle(dir, Vector3.back) < 12f) continue;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Fence";
            wall.transform.SetParent(root, false);
            wall.transform.position = BaseCenter + dir * FenceRadius + Vector3.up * 0.5f;
            wall.transform.rotation = Quaternion.LookRotation(dir);
            wall.transform.localScale = new Vector3(length, 1f, 0.25f);
            wall.GetComponent<Renderer>().material.color = new Color(0.78f, 0.6f, 0.42f);
        }
        BaseInterior.Build(dome); // 계단 오르기 + 문으로 들어가는 넓은 안쪽 방
        return pad.position;
    }

    // 모델 몸통(계단 빼고)을 감싸는 상자 충돌. 계단은 밟고 다가갈 수 있게 비워 둔다.
    static void AddBodyCollider(Transform owner, GameObject model)
    {
        Bounds? body = null;
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            if (r.name.Contains("Stair") || r.name.Contains("stair") || r.name.Contains("Tread")) continue;
            var b = r.bounds;
            if (body == null) body = b;
            else { var e = body.Value; e.Encapsulate(b); body = e; }
        }
        if (body == null) return;
        var box = owner.gameObject.AddComponent<BoxCollider>();
        box.center = owner.InverseTransformPoint(body.Value.center);
        var size = owner.InverseTransformVector(body.Value.size);
        box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
    }

    static Transform BuildRobot(Vector3 home)
    {
        var robot = new GameObject("Robot");
        robot.transform.position = home + Vector3.up;
        var cc = robot.AddComponent<CharacterController>();
        cc.height = 1.6f;
        robot.AddComponent<PlayerController>();
        robot.AddComponent<HandItemView>();

        if (RobotSprite.HasArt)
        {
            robot.AddComponent<RobotSprite>();
            return robot.transform;
        }
        Part(robot.transform, PrimitiveType.Capsule, new Vector3(0, -0.2f, 0), new Vector3(0.8f, 0.6f, 0.8f), new Color(0.92f, 0.94f, 0.96f));
        Part(robot.transform, PrimitiveType.Sphere, new Vector3(0, 0.55f, 0), Vector3.one * 0.7f, new Color(0.45f, 0.85f, 0.85f));
        Part(robot.transform, PrimitiveType.Cube, new Vector3(0, 0.6f, 0.3f), new Vector3(0.4f, 0.12f, 0.1f), new Color(0.15f, 0.2f, 0.25f));
        return robot.transform;
    }

    // 비닐하우스(앞쪽 문으로 걸어 들어간다) 안에 4×4 밭. 맨 뒷줄 4칸은 밭 확장 업그레이드로 열린다.
    // 저장 순서가 바뀌지 않게 밭은 항상 16칸, 같은 순서로 만든다.
    static PlantSpot[] BuildGreenhouse()
    {
        var greenhouse = Greenhouse.Build(new Vector3(2f, 0, 5f));
        var spots = new PlantSpot[16];
        for (int row = 0; row < 4; row++)
        for (int col = 0; col < 4; col++)
            spots[row * 4 + col] = greenhouse.AddBed($"Bed_{row}_{col}", new Vector3(-4.5f + col * 3f, 0, -5f + row * 3f), row == 3);
        return spots;
    }

    // 지구 송신기: 기둥 + 위에 빛나는 공
    static void BuildSendStation()
    {
        var station = new GameObject("SendStation");
        station.transform.position = new Vector3(8f, 0, -4.5f);
        station.AddComponent<SendStation>();
        Part(station.transform, PrimitiveType.Cylinder, Vector3.up, new Vector3(0.8f, 1f, 0.8f), new Color(0.9f, 0.92f, 0.95f));
        Part(station.transform, PrimitiveType.Sphere, Vector3.up * 2.3f, new Vector3(1.1f, 0.7f, 1.1f), new Color(0.45f, 0.85f, 0.85f));
    }

    // 채집 자원: 나무=나무(도끼), 회색 돌=돌멩이(중간 돌은 드릴, 작은 돌은 그냥 줍기), 하늘색 결정=얼음 결정(채수기).
    // 자리는 정해진 난수로 뽑아서 매번 같다(저장 순서가 맞아야 해서). 나중에 지도를 받으면 자리만 그걸로 바꾸면 된다.
    const int TreeCount = 90; // 맵 320m 기준 듬성듬성
    const int IceCount = 16;

    static ResourceNode[] BuildResources()
    {
        var rng = new System.Random(3);
        var nodes = new System.Collections.Generic.List<ResourceNode>();
        var taken = new System.Collections.Generic.List<(Vector3 pos, float radius)>();

        GameObject Add(string name, string material, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.AddComponent<ResourceNode>().materialId = material;
            nodes.Add(go.GetComponent<ResourceNode>());
            return go;
        }

        // 나무: 맵에 듬성듬성. Stylized Nature Pack 나무 모델(6종), 팩이 없으면 줄기 + 둥근 잎 덩어리(임시 도형).
        // 어떤 모델을 쓸지는 따로 뽑아서(look), 팩이 있든 없든 나무 자리는 같다.
        var look = new System.Random(77);
        var bark = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.45f, 0.3f, 0.18f) };
        var leaves = new Material[3];
        for (int k = 0; k < leaves.Length; k++)
            leaves[k] = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.32f + k * 0.05f, 0.58f + k * 0.04f, 0.28f) };
        for (int i = 0; i < TreeCount; i++)
        {
            if (!FreeSpot(rng, 1.6f, 3f, taken, out var pos)) continue;
            var go = Add("Tree", "scrap", pos);
            float size = 0.8f + (float)rng.NextDouble() * 0.5f;
            go.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
            go.transform.localScale = Vector3.one * size;
            var leaf = leaves[rng.Next(leaves.Length)];
            var model = NaturePack.Spawn(NaturePack.Trees[look.Next(NaturePack.Trees.Length)], go.transform, Vector3.zero, 0, 1f); // 실제 크기(나무 4~6m) × 자리마다 0.8~1.3배
            if (!model)
            {
                Shape(go.transform, PrimitiveType.Cylinder, new Vector3(0, 1.4f, 0), new Vector3(0.5f, 1.4f, 0.5f), bark);
                Shape(go.transform, PrimitiveType.Sphere, new Vector3(0, 3.4f, 0), new Vector3(3f, 2.6f, 3f), leaf);
                Shape(go.transform, PrimitiveType.Sphere, new Vector3(0.9f, 2.9f, 0.3f), new Vector3(1.8f, 1.6f, 1.8f), leaf);
                Shape(go.transform, PrimitiveType.Sphere, new Vector3(-0.7f, 3.0f, -0.6f), new Vector3(1.6f, 1.5f, 1.6f), leaf);
            }
            if (!model || !NaturePack.HasCollider(model))
            {
                var trunk = go.AddComponent<CapsuleCollider>(); // 줄기만 막는다
                trunk.radius = 0.35f;
                trunk.height = 3f;
                trunk.center = Vector3.up * 1.5f;
            }
            taken.Add((pos, 1.6f * size));
        }

        for (int i = 0; i < IceCount; i++)
        {
            if (!FreeSpot(rng, 0.8f, 1.2f, taken, out var pos)) continue;
            var go = Add("Crystal_Ice", "ice", pos);
            Part(go.transform, PrimitiveType.Capsule, Vector3.up * 0.8f, new Vector3(0.6f, 0.8f, 0.6f), new Color(0.72f, 0.92f, 1f)).AddComponent<CapsuleCollider>();
            taken.Add((pos, 0.8f));
        }
        BuildStones(rng, taken, Add);
        BuildDecor(taken);
        return nodes.ToArray();
    }

    // 맵(울타리 밖)에서 빈자리 찾기: 호수·다른 자원과 gap 이상 떨어지게. 40번 해 봐서 못 찾으면 false.
    static bool FreeSpot(System.Random rng, float radius, float gap,
        System.Collections.Generic.List<(Vector3 pos, float radius)> taken, out Vector3 pos)
    {
        float lim = GroundView.Size / 2f - radius - 1f;
        for (int tries = 0; tries < 40; tries++)
        {
            pos = new Vector3((float)(rng.NextDouble() * 2 - 1) * lim, 0, (float)(rng.NextDouble() * 2 - 1) * lim);
            var fromBase = pos - BaseCenter;
            fromBase.y = 0;
            if (fromBase.magnitude < FenceRadius + radius + 2f || Lake.Overlaps(pos, radius + 1.5f) || Cliff.Blocks(pos, radius + 1.5f) || Village.Blocks(pos, radius + 1.5f)) continue;
            bool clear = true;
            foreach (var t in taken)
                if ((t.pos - pos).sqrMagnitude < Sq(t.radius + radius + gap)) { clear = false; break; }
            if (clear) return true;
        }
        pos = default;
        return false;
    }

    // 충돌 없는 도형 하나(재질은 나눠 쓴다)
    static void Shape(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // 돌멩이: 종류마다 count 개를 맵(울타리 밖)에 흩어 놓는다.
    //   중간 돌 = 길을 막는 바위(예전 보라 바위 크기). 드릴로 캔다.
    //   작은 돌 = 땅에 박혀 머리만 살짝 보인다(높이 약 0.07m). 밟고 지나가고, 도구 없이 E로 줍는다.
    //             주우면 사라졌다가 다음 날 다시 생긴다.
    // scale = 공 크기, y = 공 가운데 높이(음수면 땅에 묻힘), radius = 자리 잡을 때 쓰는 크기, amount = 하루에 얻는 개수.
    static readonly (string name, int count, Vector3 scale, float y, float radius, int amount, bool pickup, Color color)[] Stones =
    {
        ("Stone_Medium", 10,   new Vector3(1.6f, 1f, 1.4f),    0.4f,   0.8f,  3, false, new Color(0.6f, 0.58f, 0.55f)),
        ("Stone_Small",  100, new Vector3(0.5f, 0.3f, 0.45f), -0.08f, 0.25f, 1, true,  new Color(0.66f, 0.64f, 0.6f)),
    };

    static void BuildStones(System.Random rng,
        System.Collections.Generic.List<(Vector3 pos, float radius)> taken,
        System.Func<string, string, Vector3, GameObject> add)
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        foreach (var s in Stones)
        {
            // 돌이 많아서 색은 몇 가지 재질을 나눠 쓴다(돌마다 재질을 만들면 느려진다)
            var shades = new Material[4];
            for (int k = 0; k < shades.Length; k++) shades[k] = new Material(lit) { color = s.color * (0.88f + k * 0.08f) };

            for (int i = 0; i < s.count; i++)
            {
                // 바위 사이는 로봇이 지나갈 틈을 남긴다. 못 찾으면 그 돌은 건너뛴다.
                if (!FreeSpot(rng, s.radius, s.pickup ? 0.2f : 1.2f, taken, out var pos)) continue;
                var go = add(s.name, "ore", pos);
                go.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                var node = go.GetComponent<ResourceNode>();
                node.maxAmount = node.Amount = s.amount;
                node.needsTool = !s.pickup;
                node.hideWhenEmpty = s.pickup;

                var shade = shades[rng.Next(shades.Length)];
                // 중간 돌은 팩의 바위 모델(있으면)
                var rockModel = s.pickup ? null : NaturePack.Spawn(NaturePack.Rocks[(int)(pos.x * 7 + pos.z * 13) & 1], go.transform, Vector3.zero, 0, 1.1f);
                if (rockModel)
                {
                    if (!NaturePack.HasCollider(rockModel)) rockModel.AddComponent<SphereCollider>().center = Vector3.up * 0.5f;
                    taken.Add((pos, s.radius));
                    continue;
                }
                var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = "Rock";
                if (s.pickup) Object.Destroy(rock.GetComponent<Collider>()); // 밟고 지나간다
                rock.transform.SetParent(go.transform, false);
                rock.transform.localPosition = Vector3.up * s.y;
                rock.transform.localScale = s.scale;
                rock.GetComponent<Renderer>().sharedMaterial = shade;
                taken.Add((pos, s.radius));
            }
        }
    }

    static float Sq(float x) => x * x;

    // 꾸미기(자원 아님): 팩이 있을 때만. 꽃덤불·해바라기·빨간 버섯을 들판에, 선인장을 여기저기, 폭포 둘레에 야자수·현무암 기둥.
    static readonly (string model, int count, float minScale, float maxScale, float radius)[] Decor =
    {
        ("bush_flower_01",      45, 1.0f, 1.5f, 0.8f),
        ("flower_sunflower_01", 25, 1.0f, 1.4f, 0.6f),
        ("mushroom_red_01",     35, 1.6f, 2.6f, 0.4f),
        ("cactus_saguaro_01",   10, 1.0f, 1.4f, 1.2f),
    };

    static void BuildDecor(System.Collections.Generic.List<(Vector3 pos, float radius)> taken)
    {
        if (!NaturePack.Available) return;
        var rng = new System.Random(31);
        var root = new GameObject("Decor").transform;
        foreach (var d in Decor)
            for (int i = 0; i < d.count; i++)
            {
                if (!FreeSpot(rng, d.radius, 0.6f, taken, out var pos)) continue;
                float scale = d.minScale + (float)rng.NextDouble() * (d.maxScale - d.minScale);
                NaturePack.Spawn(d.model, root, pos, (float)rng.NextDouble() * 360f, scale);
                taken.Add((pos, d.radius * scale));
            }
    }

    public static GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        if (parent) Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().material.color = color;
        return go;
    }
}
