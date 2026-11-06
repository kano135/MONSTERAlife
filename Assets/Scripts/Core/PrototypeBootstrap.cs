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
        GroundView.Build();
        var home = BuildBase();
        var player = BuildRobot(home);
        var spots = BuildGreenhouse();
        BuildSendStation();
        Lake.Build(LakeCenter, LakeRadius);
        var nodes = BuildResources();

        var cam = Camera.main ? Camera.main.gameObject : new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
        cam.AddComponent<CameraRig>().target = player;

        var systems = new GameObject("Game");
        systems.AddComponent<GameManager>().Init(plants, spots, nodes, player);
        systems.AddComponent<SeedSpawner>().Init(plants);
        systems.AddComponent<Hud>().player = player.GetComponent<PlayerController>();
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

    // 채집 자원: 폐허=고철(절단기), 보라 바위=광석(드릴), 하늘색 결정=얼음 결정(채수기). 자리는 매번 같다(저장 순서가 맞아야 해서).
    static ResourceNode[] BuildResources()
    {
        var rng = new System.Random(3);
        var nodes = new System.Collections.Generic.List<ResourceNode>();
        var building = ArtLibrary.World("Props/ruin_building");
        var car = ArtLibrary.World("Props/ruin_car");

        Vector3 RandomPos()
        {
            var angle = (float)rng.NextDouble() * Mathf.PI * 2;
            var dist = 20f + (float)rng.NextDouble() * 18f; // 기지 울타리 밖
            var pos = new Vector3(Mathf.Cos(angle) * dist, 0, Mathf.Sin(angle) * dist);
            // 호수에 겹치면 호수 밖으로 밀어낸다(뽑는 횟수는 그대로라 자리·저장 순서가 안 바뀐다)
            if (Lake.Overlaps(pos, 2.5f))
            {
                var away = pos - LakeCenter;
                away.y = 0;
                pos = LakeCenter + away.normalized * (LakeRadius + 3f);
            }
            return pos;
        }
        GameObject Add(string name, string material, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.AddComponent<ResourceNode>().materialId = material;
            nodes.Add(go.GetComponent<ResourceNode>());
            return go;
        }

        for (int i = 0; i < 14; i++)
        {
            var go = Add("Ruin_Scrap", "scrap", RandomPos());
            var art = i % 3 == 2 ? car : building;
            float h = art == car ? 1.6f : 3f + (float)rng.NextDouble() * 2f;
            var block = Part(go.transform, PrimitiveType.Cube, Vector3.up * h / 2, new Vector3(2.4f, h, 2.4f), new Color(0.6f, 0.6f, 0.62f));
            block.AddComponent<BoxCollider>();
            if (art)
            {
                block.GetComponent<Renderer>().enabled = false;
                Billboard.CreateByHeight(go.transform, art, art == car ? 1.8f : h * 1.1f, Color.white);
            }
        }
        for (int i = 0; i < 10; i++)
        {
            var go = Add("Rock_Ore", "ore", RandomPos());
            Part(go.transform, PrimitiveType.Sphere, Vector3.up * 0.4f, new Vector3(1.6f, 1f, 1.4f), new Color(0.55f, 0.42f, 0.72f)).AddComponent<SphereCollider>();
        }
        for (int i = 0; i < 8; i++)
        {
            var go = Add("Crystal_Ice", "ice", RandomPos());
            Part(go.transform, PrimitiveType.Capsule, Vector3.up * 0.8f, new Vector3(0.6f, 0.8f, 0.6f), new Color(0.72f, 0.92f, 1f)).AddComponent<CapsuleCollider>();
        }
        return nodes.ToArray();
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
