using System.Collections;
using System.Linq;
using UnityEngine;

// 기지(비닐하우스 옆 네모난 3D 건물) 드나들기.
//   계단: 모델 계단(Stair_tread_*)에는 충돌이 없어서, 그 위에 안 보이는 경사로를 깔아 걸어 오르게 한다.
//   문: 계단 꼭대기 문 앞에서 E → 눈 깜빡 → 기지 안. 안은 겉보다 훨씬 넓은 빈 방(상자). 안쪽 문에서 E → 밖(계단 꼭대기).
//   안쪽 방은 땅 밑(InteriorOrigin)에 따로 지어 둔다. 나중에 침대·잠자는 곳을 여기에 꾸미면 된다.
public class BaseInterior : MonoBehaviour
{
    public static BaseInterior Instance { get; private set; }

    // 안쪽 방(겉 기지 7.7×5.3m 보다 훨씬 넓게): 가로 24 · 세로 18 · 높이 8m
    public static readonly Vector3 InteriorOrigin = new Vector3(0, -80f, 0);
    public const float RoomX = 24f, RoomZ = 18f, RoomY = 8f;
    const float DoorReach = 1.6f;

    Vector3 outsideDoor;   // 바깥 문 앞(계단 꼭대기) 발 위치
    Vector3 outward;       // 문에서 계단 아래쪽으로 향하는 방향
    Vector3 insideDoor;    // 안쪽 문 앞 발 위치
    bool busy;

    public bool NearOutsideDoor(Vector3 p) => Flat(p - outsideDoor) < DoorReach && p.y > outsideDoor.y - 0.5f && p.y < outsideDoor.y + 3f;
    public bool NearInsideDoor(Vector3 p) => Flat(p - insideDoor) < DoorReach + 0.4f && p.y < InteriorOrigin.y + 4f;
    public bool IsInside(Vector3 p) => p.y < InteriorOrigin.y + RoomY + 1f;
    static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }

    public static BaseInterior Build(Transform habitat)
    {
        var bi = new GameObject("BaseInterior").AddComponent<BaseInterior>();
        Instance = bi;
        bi.BuildStairs(habitat);
        bi.BuildRoom();
        return bi;
    }

    // ── 바깥: 계단 경사로 + 문 위치 ──
    void BuildStairs(Transform habitat)
    {
        var renderers = habitat.GetComponentsInChildren<Renderer>();
        var treads = renderers.Where(r => r.name.StartsWith("Stair_tread")).ToList();
        if (treads.Count == 0)
        {
            // 3D 모델이 없을 때(임시 돔): 돔 오른쪽(+x) 문 앞을 입구로
            outward = Vector3.right;
            outsideDoor = habitat.position + Vector3.right * 3.6f;
            return;
        }
        var top = treads.OrderByDescending(r => r.bounds.max.y).First().bounds;
        var bottom = treads.OrderBy(r => r.bounds.max.y).First().bounds;
        outward = bottom.center - top.center;
        outward.y = 0;
        outward.Normalize();
        float topY = top.max.y;
        // 맨 위 칸은 건물 충돌 상자 안쪽에 묻혀 있어서 로봇이 거기까지 못 간다.
        // → 로봇이 갈 수 있는 가장 안쪽(충돌 상자 면 + 로봇 반지름)에서 문 높이가 되게, 바깥쪽으로 35도 경사로를 깐다.
        var topXZ = new Vector3(top.center.x, 0, top.center.z);
        float face = 0;
        var body = habitat.GetComponent<BoxCollider>();
        if (body)
        {
            var bb = body.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? bb.min.x : bb.max.x, 0, (i & 2) == 0 ? bb.min.z : bb.max.z);
                face = Mathf.Max(face, Vector3.Dot(corner - topXZ, outward));
            }
        }
        const float robotRadius = 0.5f, slope = 35f;
        var high = topXZ + outward * (face + robotRadius); high.y = topY;
        var low = high + outward * (topY / Mathf.Tan(slope * Mathf.Deg2Rad)); low.y = 0;
        var ramp = new GameObject("BaseStairRamp").AddComponent<BoxCollider>();
        ramp.transform.SetParent(transform, false);
        const float thick = 0.2f;
        var along = high - low;
        var rot = Quaternion.LookRotation(along.normalized, Vector3.up);
        ramp.transform.SetPositionAndRotation((low + high) / 2f - rot * Vector3.up * thick / 2f, rot);
        float width = Mathf.Abs(Vector3.Dot(top.size, Vector3.Cross(Vector3.up, outward))) + 0.2f;
        ramp.size = new Vector3(width, thick, along.magnitude + 0.2f);
        // 꼭대기 발판(경사로 끝 ~ 건물 면): 문 앞에 평평하게 설 수 있게
        var landing = new GameObject("BaseStairLanding").AddComponent<BoxCollider>();
        landing.transform.SetParent(transform, false);
        landing.transform.SetPositionAndRotation(topXZ + outward * (face + robotRadius) / 2f + Vector3.up * (topY - thick / 2f), Quaternion.LookRotation(outward));
        landing.size = new Vector3(width, thick, face + robotRadius + 0.2f);
        // 문 앞 발 위치(로봇이 서는 곳)
        outsideDoor = high;
    }

    // ── 안쪽: 큰 빈 방 ──
    void BuildRoom()
    {
        var o = InteriorOrigin;
        var wall = new Color(0.93f, 0.89f, 0.8f);
        var floor = new Color(0.66f, 0.5f, 0.36f);
        const float t = 1f;
        Box("Room_Floor", o + new Vector3(0, -t / 2f, 0), new Vector3(RoomX + 2 * t, t, RoomZ + 2 * t), floor);
        Box("Room_Ceiling", o + new Vector3(0, RoomY + t / 2f, 0), new Vector3(RoomX + 2 * t, t, RoomZ + 2 * t), wall * 0.95f);
        Box("Room_WallW", o + new Vector3(-RoomX / 2f - t / 2f, RoomY / 2f, 0), new Vector3(t, RoomY, RoomZ), wall);
        Box("Room_WallE", o + new Vector3(RoomX / 2f + t / 2f, RoomY / 2f, 0), new Vector3(t, RoomY, RoomZ), wall);
        Box("Room_WallS", o + new Vector3(0, RoomY / 2f, -RoomZ / 2f - t / 2f), new Vector3(RoomX + 2 * t, RoomY, t), wall * 0.97f);
        Box("Room_WallN", o + new Vector3(0, RoomY / 2f, RoomZ / 2f + t / 2f), new Vector3(RoomX + 2 * t, RoomY, t), wall * 0.97f);
        // 안쪽 문(동쪽 벽 가운데) — 보이기만 하는 판
        var doorPanel = PrototypeBootstrap.Part(transform, PrimitiveType.Cube, o + new Vector3(RoomX / 2f - 0.06f, 1.5f, 0), new Vector3(0.1f, 3f, 2f), new Color(0.45f, 0.85f, 0.85f));
        doorPanel.name = "Room_ExitDoor";
        PrototypeBootstrap.Part(transform, PrimitiveType.Cube, o + new Vector3(RoomX / 2f - 0.1f, 3.1f, 0), new Vector3(0.12f, 0.2f, 2.4f), new Color(0.95f, 0.96f, 0.97f)).name = "Room_DoorFrame";
        insideDoor = o + new Vector3(RoomX / 2f - 1.2f, 0, 0);
        foreach (var x in new[] { -RoomX / 4f, RoomX / 4f })
        {
            var l = new GameObject("RoomLight").AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.transform.position = o + new Vector3(x, RoomY - 1f, 0);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.93f, 0.8f);
            l.range = 22f;
            l.intensity = 3f;
        }
    }

    void Box(string name, Vector3 pos, Vector3 size, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().material.color = color;
    }

    // ── 드나들기 ──
    public void Enter(Transform player) { if (!busy) StartCoroutine(Move(player, insideDoor + Vector3.up * 1f, -90f)); }

    // 게임을 시작하면 로봇은 기지 안 방에서 깨어난다(방 가운데쯤, 안쪽 문을 바라보며)
    public Vector3 StartPosition => InteriorOrigin + new Vector3(RoomX / 4f, 1f, 0);
    public const float StartYaw = 90f; // +x = 안쪽 문 쪽

    public void Exit(Transform player)
    {
        if (busy) return;
        float yaw = Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg; // 계단 아래쪽을 보며 나온다
        StartCoroutine(Move(player, outsideDoor + outward * 0.2f + Vector3.up * 1f, yaw)); // 문 앞 계단 꼭대기
    }

    IEnumerator Move(Transform player, Vector3 to, float yaw)
    {
        busy = true;
        var lids = Eyelids.Instance;
        if (lids) yield return lids.Close(0.25f);
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.position = to;
        cc.enabled = true;
        var rig = FindFirstObjectByType<CameraRig>();
        if (rig) rig.SetYaw(yaw);
        if (lids) yield return lids.Open(0.35f);
        busy = false;
    }
}
