using UnityEngine;
using UnityEngine.InputSystem;

// AI 로봇 조작: WASD 이동, Shift 달리기, Space 점프, E 심기·물주기·수확·채집·지구로 보내기·호수에서 물 채우기,
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

        if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2f;
        if (controller.isGrounded && kb.spaceKey.wasPressedThisFrame) verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);
        verticalSpeed += gravity * Time.deltaTime;

        float speed = (kb.leftShiftKey.isPressed ? sprintSpeed : walkSpeed) * (gm.HasUpgrade(Progression.Booster) ? 1.4f : 1f);
        controller.Move((move * speed + Vector3.up * verticalSpeed) * Time.deltaTime);

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
        if (kb.eKey.wasPressedThisFrame)
        {
            if (gm.Inv.HeldKey == Inventory.FuelKey) Show(gm.UseFuelCan(), 3f); // 테스트용 연료통은 어디서든
            else if (NearStation) Show(gm.SendToEarth(), 4f);
            else if (NearestSpot) Show(gm.Interact(NearestSpot), 3f);
            else if (NearestDevice) Show(NearestDevice.Use(), 3f);
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
