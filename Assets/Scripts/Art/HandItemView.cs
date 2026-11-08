using UnityEngine;

// 로봇 손에 든 것: 물뿌리개(임시 상자 모양), 씨앗, 재료·비료(색 상자). 그림·모델이 생기면 Build...() 만 바꾸면 된다.
//   무엇을 드나: 고른 아래 칸(1~9)의 물건 그대로. 물뿌리개도 칸에 든 물건이다. 빈 칸이면 빈 손.
//   3인칭: 로봇 그림이 늘 카메라를 보니까 손에 든 것도 화면 기준 로봇 오른손 옆에 딱 붙여 둔다(걸을 때 통통 튀는 것도 같이).
//   1인칭: 화면 오른쪽 아래에 손에 든 것처럼 보인다.
// 물을 주면 물뿌리개를 앞으로 기울였다 돌아오고, 물이 없으면 회색이 된다.
// 카메라(CameraRig)·로봇 그림이 이번 프레임 자리를 잡은 뒤에 따라가야 흔들리지 않는다 → 늦게 돈다
[DefaultExecutionOrder(1000)]
public class HandItemView : MonoBehaviour
{
    // Hud 가 물뿌리개 남은 횟수를 띄울지 정할 때 쓴다
    public static bool HoldingCan { get; private set; }

    static readonly Color Full = new Color(0.43f, 0.78f, 0.94f);
    static readonly Color Empty = new Color(0.62f, 0.62f, 0.62f);
    const float PourSeconds = 0.6f;
    const float CanScale3rd = 0.85f, CanScale1st = 0.5f;
    // 물뿌리개 방향(위에서 본 각도). 180 근처 = 왼쪽 옆면이 카메라 쪽. 숫자만 바꿔서 보이는 면을 조절한다.
    const float CanYaw1st = 200f, CanYaw3rd = 160f;

    Transform can, seed, block;
    Renderer body;
    Renderer[] trims;
    SpriteRenderer seedSprite;
    Renderer seedBall;
    Color shown = Full;
    PlantData seedShown;
    Renderer blockRend;
    string blockShown;
    float pourTimer;
    RobotSprite sprite;
    bool subscribed;
    PlayerController player;

    void Start()
    {
        player = GetComponent<PlayerController>();
        can = BuildCan().transform;
        seed = BuildSeed().transform;
        block = BuildBlock().transform;
        sprite = GetComponent<RobotSprite>();
    }

    GameObject BuildCan()
    {
        var root = new GameObject("WateringCan");
        var t = root.transform;
        // 주둥이가 +x 쪽. 크기는 미터(scale 1 기준).
        body = PrototypeBootstrap.Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0.24f, 0.26f, 0.18f), Full).GetComponent<Renderer>();
        var spout = PrototypeBootstrap.Part(t, PrimitiveType.Cube, new Vector3(0.2f, 0.05f, 0), new Vector3(0.26f, 0.045f, 0.045f), Full * 0.85f);
        spout.transform.localRotation = Quaternion.Euler(0, 0, 35f);
        var head = PrototypeBootstrap.Part(t, PrimitiveType.Cube, new Vector3(0.33f, 0.13f, 0), new Vector3(0.05f, 0.08f, 0.08f), Full * 0.85f); // 물 나오는 머리
        trims = new[] { spout.GetComponent<Renderer>(), head.GetComponent<Renderer>() };
        PrototypeBootstrap.Part(t, PrimitiveType.Cube, new Vector3(-0.02f, 0.18f, 0), new Vector3(0.2f, 0.035f, 0.04f), new Color(0.3f, 0.35f, 0.4f)); // 손잡이
        NoShadows(root);
        return root;
    }

    // 씨앗: 씨앗 그림(Props/seed)에 식물 색을 살짝 입힌다. 그림이 없으면 작은 알.
    GameObject BuildSeed()
    {
        var root = new GameObject("HeldSeed");
        var art = ArtLibrary.Ui("Props/seed");
        if (art)
        {
            var go = new GameObject("Sprite", typeof(SpriteRenderer));
            go.transform.SetParent(root.transform, false);
            seedSprite = go.GetComponent<SpriteRenderer>();
            seedSprite.sprite = art;
            go.transform.localScale = Vector3.one * (0.45f / art.bounds.size.y); // 높이 0.45m
        }
        else seedBall = PrototypeBootstrap.Part(root.transform, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.14f, 0.2f, 0.14f), Color.white).GetComponent<Renderer>();
        NoShadows(root);
        root.SetActive(false);
        return root;
    }

    // 재료·비료: 작은 색 상자(그 물건 색)
    GameObject BuildBlock()
    {
        var root = new GameObject("HeldItem");
        blockRend = PrototypeBootstrap.Part(root.transform, PrimitiveType.Cube, Vector3.zero, Vector3.one * 0.18f, Color.white).GetComponent<Renderer>();
        NoShadows(root);
        root.SetActive(false);
        return root;
    }

    static void NoShadows(GameObject root)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    public void Pour() => pourTimer = PourSeconds;

    // 물뿌리개를 들고 있나(물 주는 동안에도)
    bool WantsCan(GameManager gm) => pourTimer > 0 || gm.Inv.HeldCan;

    void LateUpdate()
    {
        var gm = GameManager.Instance;
        var cam = Camera.main;
        if (!can || !gm || !cam) return;
        if (!subscribed) { gm.Poured += Pour; subscribed = true; }

        HoldingCan = WantsCan(gm);
        bool holdSeed = !HoldingCan && gm.Inv.HeldPlant;
        bool holdBlock = !HoldingCan && !holdSeed && gm.Inv.HeldKey != ""; // 빈 손이면 아무것도 안 든다
        if (can.gameObject.activeSelf != HoldingCan) can.gameObject.SetActive(HoldingCan);
        if (seed.gameObject.activeSelf != holdSeed) seed.gameObject.SetActive(holdSeed);
        if (block.gameObject.activeSelf != holdBlock) block.gameObject.SetActive(holdBlock);

        if (HoldingCan) UpdateCanColor(gm);
        else if (holdSeed) UpdateSeedColor(gm);
        else UpdateBlockColor(gm);

        // 기울기: 0 → 앞으로 50도 → 0
        float tilt = 0;
        if (pourTimer > 0)
        {
            pourTimer -= Time.deltaTime;
            tilt = Mathf.Sin((1 - pourTimer / PourSeconds) * Mathf.PI) * 50f;
        }

        var camT = cam.transform;
        if (CameraRig.FirstPerson)
        {
            // 화면 오른쪽 아래(오른쪽 아래 안내 창에 안 가리게 가운데 쪽으로 조금), 카메라를 따라 돈다
            // 물뿌리개는 왼쪽 옆면이 보이게(주둥이는 왼쪽 앞, 화면 가운데 쪽)
            var pos = camT.position + camT.right * 0.15f - camT.up * 0.2f + camT.forward * 0.6f;
            can.SetPositionAndRotation(pos, camT.rotation * Quaternion.Euler(0, CanYaw1st, 0) * Quaternion.Euler(0, 0, -tilt));
            can.localScale = Vector3.one * CanScale1st;
            seed.SetPositionAndRotation(pos, camT.rotation);
            seed.localScale = Vector3.one * 0.6f;
            block.SetPositionAndRotation(pos, camT.rotation * Quaternion.Euler(15f, 30f, 0));
            block.localScale = Vector3.one * 0.6f;
            return;
        }

        // 3인칭: 화면 기준 로봇 오른손 옆. 로봇 그림이 걸을 때 튀는 만큼 같이 움직인다.
        var flatForward = Vector3.ProjectOnPlane(camT.forward, Vector3.up).normalized;
        var flatRight = Vector3.Cross(Vector3.up, flatForward);
        float bob = sprite ? sprite.Bob : 0f;
        // 로봇 몸 옆 손 높이, 그림보다 살짝 카메라 쪽(그림에 안 가리게)
        var hand = transform.position + flatRight * 0.45f + Vector3.up * (bob - 0.3f) - flatForward * 0.3f;
        // 왼쪽 옆면이 카메라 쪽을 보게(주둥이는 왼쪽 앞)
        can.SetPositionAndRotation(hand, Quaternion.LookRotation(flatForward) * Quaternion.Euler(0, CanYaw3rd, 0) * Quaternion.Euler(0, 0, -tilt));
        can.localScale = Vector3.one * CanScale3rd;
        seed.SetPositionAndRotation(hand, Quaternion.LookRotation(flatForward));
        seed.localScale = Vector3.one;
        block.SetPositionAndRotation(hand, Quaternion.LookRotation(flatForward) * Quaternion.Euler(15f, 30f, 0));
        block.localScale = Vector3.one;
    }

    void UpdateCanColor(GameManager gm)
    {
        var color = gm.CanWater > 0 ? Full : Empty;
        if (color == shown) return;
        shown = color;
        body.material.color = color;
        foreach (var r in trims) r.material.color = color * 0.85f;
    }

    void UpdateSeedColor(GameManager gm)
    {
        var p = gm.Inv.HeldPlant;
        if (!p || p == seedShown) return;
        seedShown = p;
        var tint = Color.Lerp(Color.white, p.color, 0.45f);
        if (seedSprite) seedSprite.color = tint;
        if (seedBall) seedBall.material.color = p.color;
    }

    void UpdateBlockColor(GameManager gm)
    {
        string key = gm.Inv.HeldKey;
        if (key == blockShown) return;
        blockShown = key;
        blockRend.material.color = gm.Inv.Tint(key);
    }

    void OnDestroy()
    {
        if (subscribed && GameManager.Instance) GameManager.Instance.Poured -= Pour;
        if (can) Destroy(can.gameObject);
        if (seed) Destroy(seed.gameObject);
        if (block) Destroy(block.gameObject);
        HoldingCan = false;
    }
}
