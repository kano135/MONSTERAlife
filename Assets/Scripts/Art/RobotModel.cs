using System.Collections.Generic;
using UnityEngine;

// 3D 로봇(M-01, 바구니 멘 로봇) + 걷기·점프 애니메이션.
// 모델은 6조각(Resources/Art/Models/robot_body·head·arm_l·arm_r·leg_l·leg_r.obj)이고,
// 머리·팔·다리는 관절(피벗)에 매달려 돈다. 동작은 robot_anim.json(원본 GLB 에서 뽑은 키프레임):
//   Walk_Loop(1초 반복) — 걸을 때, Jump_Arms_Open_Close(1.25초) — 점프할 때.
// 1인칭이면 내 몸은 숨긴다.
public class RobotModel : MonoBehaviour
{
    [System.Serializable] class Pivot { public string part; public float[] pos; }
    [System.Serializable] class Channel { public string part, path; public float[] times, values; }
    [System.Serializable] class Clip { public string name; public Channel[] channels; public float length; }
    [System.Serializable] class AnimData { public Pivot[] pivots; public Clip[] clips; }

    static readonly string[] Parts = { "head", "arm_l", "arm_r", "leg_l", "leg_r" };

    Transform rig;
    readonly Dictionary<string, Transform> joints = new Dictionary<string, Transform>();
    Clip walk, jump;
    Renderer[] renderers;
    bool shown = true;
    CharacterController body;
    Vector3 lastPos;
    float walkTime, walkWeight, jumpTime = -1f;
    bool wasGrounded = true;

    // 로봇(캐릭터 컨트롤러가 붙은 오브젝트) 아래에 모델을 만들고 키·발 높이를 맞춘다. 모델이 없으면 false.
    public static bool Build(Transform robot, float height)
    {
        var json = Resources.Load<TextAsset>("Art/Models/robot_anim");
        var root = new GameObject("RobotModel");
        root.transform.SetParent(robot, false);
        var rig = new GameObject("Rig").transform;
        rig.SetParent(root.transform, false);
        if (!ArtLibrary.SpawnModel("robot_body", rig)) { Destroy(root); return false; }

        var view = root.AddComponent<RobotModel>();
        view.rig = rig;
        var data = json ? JsonUtility.FromJson<AnimData>(json.text) : null;
        foreach (var part in Parts)
        {
            var pos = Vector3.zero;
            if (data != null)
                foreach (var p in data.pivots)
                    if (p.part == part) pos = new Vector3(p.pos[0], p.pos[1], p.pos[2]);
            var joint = new GameObject(part).transform;
            joint.SetParent(rig, false);
            joint.localPosition = pos;
            var mesh = ArtLibrary.SpawnModel("robot_" + part, joint);
            if (mesh) mesh.transform.localPosition = -pos; // 조각은 원래 자리 좌표라 관절 위치만큼 되돌린다
            view.joints[part] = joint;
        }
        if (data != null)
            foreach (var c in data.clips)
            {
                c.length = 0;
                foreach (var ch in c.channels) c.length = Mathf.Max(c.length, ch.times[ch.times.Length - 1]);
                if (c.name.StartsWith("Walk")) view.walk = c;
                else if (c.name.StartsWith("Jump")) view.jump = c;
            }

        // 키 맞추기, 발을 컨트롤러 바닥에
        var cc = robot.GetComponent<CharacterController>();
        var b = Bounds(root);
        if (b.size.y > 0.001f) root.transform.localScale *= height / b.size.y;
        b = Bounds(root);
        var feet = robot.position + Vector3.up * (cc.center.y - cc.height / 2f);
        root.transform.position += new Vector3(feet.x - b.center.x, feet.y - b.min.y, feet.z - b.center.z);
        return true;
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        var b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.zero);
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    void Start()
    {
        renderers = GetComponentsInChildren<Renderer>();
        body = GetComponentInParent<CharacterController>();
        lastPos = transform.position;
    }

    void Update()
    {
        bool show = !CameraRig.FirstPerson;
        if (show != shown)
        {
            shown = show;
            foreach (var r in renderers) r.enabled = show;
        }

        var move = transform.position - lastPos;
        float vy = Time.deltaTime > 0 ? move.y / Time.deltaTime : 0;
        lastPos = transform.position;
        move.y = 0;
        float speed = Time.deltaTime > 0 ? move.magnitude / Time.deltaTime : 0;

        // 점프: 땅에서 떠오르는 순간 시작, 한 번만 재생
        bool grounded = body ? body.isGrounded : true;
        if (wasGrounded && !grounded && vy > 1f) jumpTime = 0;
        wasGrounded = grounded;

        // 걷기: 움직이는 빠르기에 맞춰 재생(걷기 ≈ 1배, 달리기 더 빨리), 멈추면 서서히 제자리 자세로
        walkWeight = Mathf.MoveTowards(walkWeight, speed > 0.3f ? 1f : 0f, Time.deltaTime * 6f);
        if (walk != null) walkTime = (walkTime + Time.deltaTime * Mathf.Clamp(speed / 3.5f, 0.6f, 2f)) % walk.length;

        // 기본 자세(가만히)
        rig.localPosition = Vector3.zero;
        foreach (var j in joints.Values) j.localRotation = Quaternion.identity;
        if (walk != null && walkWeight > 0) Apply(walk, walkTime, walkWeight, false);
        if (jump != null && jumpTime >= 0)
        {
            jumpTime += Time.deltaTime;
            if (jumpTime >= jump.length) jumpTime = -1f;
            else Apply(jump, jumpTime, 1f, true);
        }
    }

    // 클립의 time 순간 자세를 weight 만큼 섞어 넣는다. 점프는 몸 띄우기(위로 이동)는 빼고 웅크리기만(실제로 뛰는 건 캐릭터가 한다).
    void Apply(Clip clip, float time, float weight, bool crouchOnly)
    {
        foreach (var ch in clip.channels)
        {
            int n = ch.times.Length, k = 0;
            while (k < n - 2 && ch.times[k + 1] < time) k++;
            float t0 = ch.times[k], t1 = ch.times[Mathf.Min(k + 1, n - 1)];
            float f = t1 > t0 ? Mathf.Clamp01((time - t0) / (t1 - t0)) : 0;
            int k1 = Mathf.Min(k + 1, n - 1);
            if (ch.path == "rotation")
            {
                if (!joints.TryGetValue(ch.part, out var j)) continue;
                var q = Quaternion.Slerp(Q(ch.values, k), Q(ch.values, k1), f);
                j.localRotation = Quaternion.Slerp(j.localRotation, q, weight);
            }
            else if (ch.part == "root")
            {
                var p = Vector3.Lerp(V(ch.values, k), V(ch.values, k1), f);
                if (crouchOnly) p.y = Mathf.Min(p.y, 0);
                rig.localPosition = Vector3.Lerp(rig.localPosition, p, weight);
            }
        }
    }

    static Quaternion Q(float[] v, int i) => new Quaternion(v[i * 4], v[i * 4 + 1], v[i * 4 + 2], v[i * 4 + 3]);
    static Vector3 V(float[] v, int i) => new Vector3(v[i * 3], v[i * 3 + 1], v[i * 3 + 2]);
}
