using UnityEngine;
using UnityEngine.InputSystem;

// AI 로봇 조작: WASD 이동, Shift 달리기(스태미나), Space 점프, 가파른 벽에 대고 앞으로 = 벽 타기(스태미나), E 심기·물주기·수확·채집·지구로 보내기·호수에서 물 채우기,
// Q(기지 충전기 옆) 충전하고 하루 끝내기, Tab 다음 칸(손에 든 것) 바꾸기
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float walkSpeed = 4f;
    public float sprintSpeed = 7f;
    public float jumpHeight = 1.2f;
    public float gravity = -20f;
    public float interactRange = 2.2f;

    public PlantSpot NearestSpot { get; private set; }
    public ResourceNode NearestNode { get; private set; }
    public bool NearStation { get; private set; }
    public bool NearPad { get; private set; }
    public bool NearLake { get; private set; }
    public bool NearBaseDoor { get; private set; }
    public bool NearExitDoor { get; private set; }
    public SeedPickup NearestSeed { get; private set; }
    public GreenhouseDevice NearestDevice { get; private set; }
    public bool InGreenhouse { get; private set; }
    public string CurrentMessage => Time.time < messageUntil ? message : null;

    CharacterController controller;
    SendStation station;
    float verticalSpeed;
    string message;
    float messageUntil;

    void Awake() => controller = GetComponent<CharacterController>();

    // ── 스태미나: 달리기와 벽 타기에 쓴다. 평평한 데 서 있거나 걸으면 찬다. 바닥나면 지쳐서 잠깐 못 달리고 못 탄다. ──
    public const float MaxStamina = 150f; // 100 → 150
    public float Stamina { get; private set; } = MaxStamina;
    public bool Exhausted { get; private set; }   // 바닥났다 → RecoverAt 까지 찰 때까지
    public bool Climbing { get; private set; }
    public float Wet { get; private set; }         // 물에 잠긴 정도 0~1
    const float SprintDrain = 9f, ClimbDrain = 14f, Regen = 28f, RecoverAt = 30f; // 초당. 달리기 약 17초, 벽 타기 약 17m(22m 절벽은 여전히 중간 턱에서 쉬어야 한다)
    public float climbSpeed = 1.6f;                // 벽을 타고 오르는 빠르기(m/s)
    const float FallsPush = 0.7f;                  // 폭포 물이 내리누르는 빠르기(오르는 힘보다 조금 약하다 → 느리게나마 오른다)
    const float ClimbAngle = 46f;                  // 이보다 가파른 지형은 탄다(걸어 오를 수 있는 한계 45도 바로 위 — 못 걷는 곳은 전부 탈 수 있게)
    float mantleUntil;                             // 이 시각까지는 턱 위로 올라서는 중
    bool steepContact;                             // 방금 가파른 면을 밟았나(미끄러진다)
    Vector3 steepNormal;

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.normal.y > 0.02f && Vector3.Angle(hit.normal, Vector3.up) > controller.slopeLimit + 2f) { steepContact = true; steepNormal = hit.normal; }
    }

    // 탈 수 있는 것: 지형(언덕·절벽)과 바위. 나무·집·울타리는 못 탄다.
    static bool Climbable(Collider c)
    {
        if (c.name == "Hill") return true;
        string n = c.name.ToLowerInvariant(), parent = c.transform.parent ? c.transform.parent.name.ToLowerInvariant() : "";
        return n.Contains("rock") || parent.Contains("rock");
    }

    void Locomotion(Keyboard kb, GameManager gm, Vector3 move)
    {
        float dt = Time.deltaTime;
        bool steep = steepContact;
        steepContact = false;
        bool grounded = controller.isGrounded && !steep;

        // 물속: 잠길수록 몸이 가벼워지고(중력 약하게) 느려진다. 점프하면 붕 떴다가 천천히 내려온다.
        float feetY = transform.position.y + controller.center.y - controller.height / 2f;
        Wet = Mathf.Clamp01(Cliff.Submerged(new Vector3(transform.position.x, feetY, transform.position.z)) / 1.6f);
        float g = gravity * Mathf.Lerp(1f, 0.2f, Wet);

        // 벽 타기: 가파른 지형에 대고 앞으로 가면 붙어서 오른다(스태미나가 닳는다)
        Climbing = false;
        Vector3 wallNormal = Vector3.zero;
        if (move.sqrMagnitude > 0.04f && !Exhausted)
        {
            // 발끝과 무릎 높이 두 곳에서 앞을 본다(낮은 돌턱도, 높은 벽도 잡는다)
            foreach (float above in new[] { 0.08f, 0.5f })
            {
                var origin = transform.position + Vector3.down * (controller.height / 2f - above);
                if (!Physics.Raycast(origin, move.normalized, out var wall, controller.radius + 0.55f, ~(1 << 2), QueryTriggerInteraction.Ignore)
                    || !Climbable(wall.collider) || Vector3.Angle(wall.normal, Vector3.up) <= ClimbAngle) continue;
                Climbing = true;
                wallNormal = new Vector3(wall.normal.x, 0, wall.normal.z).normalized;
                mantleUntil = Time.time + 0.4f;
                break;
            }
        }

        Vector3 velocity;
        bool sprinting = false;
        if (Climbing)
        {
            float up = climbSpeed - (Cliff.InWaterfall(transform.position) ? FallsPush : 0f);
            velocity = Vector3.up * up + move * 1.2f - wallNormal * 1.5f; // 벽에 붙어서
            verticalSpeed = 0;
            Stamina -= ClimbDrain * dt;
        }
        else if (Time.time < mantleUntil && move.sqrMagnitude > 0.04f && !Exhausted)
        {
            // 턱 끝에 닿았다: 벽이 더는 앞에 없어도 잠깐 더 밀어 올려 턱 위로 올라선다
            velocity = Vector3.up * climbSpeed + move * 3f;
            verticalSpeed = 0;
        }
        else
        {
            if (grounded && verticalSpeed < 0) verticalSpeed = -2f;
            if (grounded && kb.spaceKey.wasPressedThisFrame) verticalSpeed = Mathf.Sqrt(jumpHeight * Mathf.Lerp(1f, 1.5f, Wet) * -2f * g);
            verticalSpeed += g * dt;
            verticalSpeed = Mathf.Max(verticalSpeed, Mathf.Lerp(-60f, -2.2f, Wet)); // 물속에서는 천천히 가라앉는다
            sprinting = kb.leftShiftKey.isPressed && move.sqrMagnitude > 0.04f && !Exhausted && Wet < 0.5f;
            float speed = (sprinting ? sprintSpeed : walkSpeed) * (gm.HasUpgrade(Progression.Booster) ? 1.4f : 1f) * Mathf.Lerp(1f, 0.6f, Wet);
            velocity = move * speed + Vector3.up * verticalSpeed;
            // 가파른 면에서는 못 버티고 미끄러진다(점프로 벽을 오르지 못하게)
            if (steep && verticalSpeed <= 0) velocity += new Vector3(steepNormal.x, 0, steepNormal.z).normalized * 3.5f;
            if (sprinting) Stamina -= SprintDrain * dt;
            else if (controller.isGrounded) Stamina += Regen * (Wet > 0.5f ? 0.5f : 1f) * dt; // 어디든 발을 디디고 쉬면 찬다
        }
        Stamina = Mathf.Clamp(Stamina, 0, MaxStamina);
        if (Stamina <= 0 && !Exhausted) { Exhausted = true; Show("지쳤어요… 잠깐 쉬면 다시 움직일 수 있어요", 2.5f); }
        else if (Exhausted && Stamina >= RecoverAt) Exhausted = false;

        controller.Move(velocity * dt);

        // 강제 미션 중(첫날 원주민): 정해진 둘레 밖으로 못 나간다
        if (Leash is (Vector3 center, float radius))
        {
            var off = transform.position - center; off.y = 0;
            if (off.magnitude > radius)
            {
                var back = center + off.normalized * radius;
                controller.enabled = false;
                transform.position = new Vector3(back.x, transform.position.y, back.z);
                controller.enabled = true;
                if (Time.time > leashWarnAt) { Show("먼저 원주민에게 말을 걸어야 해요 (E)", 2.5f); leashWarnAt = Time.time + 3f; }
            }
        }

        // 바다 속 벽에 닿으면 알려 준다
        if (Landscape.ShoreDistance(transform.position) < GroundView.SeaWall + 1.1f && Time.time > seaWarnAt)
        {
            Show("파도가 세서 더 깊이는 못 들어가요", 2.5f);
            seaWarnAt = Time.time + 6f;
        }
    }
    float seaWarnAt, leashWarnAt;
    // 강제 미션: 이 둘레(가운데, 반지름) 밖으로 못 나간다. null 이면 자유
    public static (Vector3 center, float radius)? Leash;

    // 언덕 속에 끼면(예전 저장 자리에 언덕이 생겼거나 턱을 뚫고 들어간 경우) 언덕 위로 올려 준다.
    // 기지 안 방(땅 밑 -80m)은 건드리지 않는다.
    void KeepAboveGround()
    {
        var p = transform.position;
        if (p.y < -20f) return;
        float feet = p.y + controller.center.y - controller.height / 2f;
        float ground = Landscape.HeightAt(p);
        if (feet >= ground - 0.3f) return;
        controller.enabled = false;
        transform.position = p + Vector3.up * (ground - feet + 0.05f);
        controller.enabled = true;
        verticalSpeed = 0;
    }

    ChargingPad pad;

    void Start()
    {
        station = FindFirstObjectByType<SendStation>();
        pad = FindFirstObjectByType<ChargingPad>();
    }

    public void Show(string text, float seconds = 2.5f)
    {
        message = text;
        messageUntil = Time.time + seconds;
    }

    void Update()
    {
        // shortcut: 키보드를 직접 읽는다. 게임패드·키 변경이 필요해지면 InputSystem_Actions 로 옮긴다.
        var kb = Keyboard.current;
        var gm = GameManager.Instance;
        // gm 이 없으면: 플레이 중에 코드를 고쳐 다시 컴파일된 경우(정적 값이 지워짐). Play 를 다시 누르면 된다.
        if (kb == null || !gm || gm.IsGameOver || Hud.AnyPanelOpen) return;

        var input = new Vector2(
            (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0),
            (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0));
        var cam = Camera.main.transform;
        var forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        var right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
        var move = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);

        // 1인칭이면 몸이 카메라가 보는 쪽을 향하고, 3인칭이면 걷는 쪽을 향한다
        if (CameraRig.FirstPerson) transform.rotation = Quaternion.LookRotation(forward);
        else if (move.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(move), 12f * Time.deltaTime);

        Locomotion(kb, gm, move);
        KeepAboveGround();

        NearestSpot = FindNearest(gm.Spots, interactRange);
        NearestNode = FindNearest(gm.Nodes, interactRange);
        var greenhouse = Greenhouse.Instance;
        InGreenhouse = greenhouse && greenhouse.Contains(transform.position);
        NearestDevice = greenhouse ? FindNearest(greenhouse.Devices, interactRange) : null;
        // 밭과 장치가 둘 다 가까우면 더 가까운 쪽만
        if (NearestDevice && NearestSpot)
        {
            if (Dist(NearestDevice) < Dist(NearestSpot)) NearestSpot = null;
            else NearestDevice = null;
        }
        NearStation = station && (station.transform.position - transform.position).sqrMagnitude < station.range * station.range;
        NearPad = pad && (pad.transform.position - transform.position).sqrMagnitude < pad.range * pad.range;
        NearLake = Lake.Instance && Lake.Instance.InReach(transform.position);
        NearestSeed = FindNearest(SeedPickup.All, interactRange);
        var bi = BaseInterior.Instance;
        NearBaseDoor = bi && bi.NearOutsideDoor(transform.position);
        NearExitDoor = bi && bi.NearInsideDoor(transform.position);
        var greeter = NativeGreeter.Instance;
        if (kb.eKey.wasPressedThisFrame && greeter && greeter.CanTalk(transform.position)) greeter.Talk(); // 첫날 원주민에게 말 걸기(가장 먼저)
        else if (kb.eKey.wasPressedThisFrame)
        {
            if (gm.Inv.HeldKey == Inventory.FuelKey) Show(gm.UseFuelCan(), 3f); // 테스트용 연료통은 어디서든
            else if (NearStation) Show(gm.SendToEarth(), 4f);
            else if (NearestSpot) Show(gm.Interact(NearestSpot), 3f);
            else if (NearestDevice) Show(NearestDevice.Use(), 3f);
            else if (NearBaseDoor && gm.HasReturnFuel && !gm.ReturnFuelLoaded) Show(gm.LoadReturnFuel(), 4f); // 귀환 연료통 넣기
            else if (NearBaseDoor && gm.ReturnFuelLoaded) gm.LaunchHome();                                    // 연료를 넣은 기지 → 지구로(엔딩)
            else if (NearBaseDoor) bi.Enter(transform);
            else if (NearExitDoor) bi.Exit(transform);
            else if (NearLake) Show(gm.RefillCan(), 3f);
            else if (NearestSeed) Show(NearestSeed.Pick());
            else if (NearestNode) Show(gm.Gather(NearestNode), 3.5f);
        }
        if (kb.fKey.wasPressedThisFrame && NearestSpot) Show(gm.Fertilize(NearestSpot), 3f);
        if (kb.qKey.wasPressedThisFrame && NearPad) gm.Sleep(passedOut: false);
        if (kb.tabKey.wasPressedThisFrame) gm.Inv.SelectNextFilled();
    }

    float Dist(Component c) => (c.transform.position - transform.position).sqrMagnitude;

    // 가까운 것 고르기. 1인칭에서는 화면 가운데(조준점) 쪽에 있는 것만 고른다.
    T FindNearest<T>(System.Collections.Generic.IReadOnlyList<T> items, float range) where T : Component
    {
        T best = null;
        float bestDist = range * range;
        var look = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up);
        foreach (var item in items)
        {
            if (!item.gameObject.activeInHierarchy) continue; // 아직 안 연 밭, 다 주운 작은 돌
            var to = item.transform.position - transform.position;
            float d = to.sqrMagnitude;
            if (CameraRig.FirstPerson && Vector3.Angle(look, Vector3.ProjectOnPlane(to, Vector3.up)) > 35f) continue;
            if (d < bestDist) { bestDist = d; best = item; }
        }
        return best;
    }
}
