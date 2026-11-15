using UnityEngine;

// 원주민 3D 모델: Resources/Art/Models/native.obj (라벤더 꽃 캐릭터 — 꽃 모양 머리, 감은 눈, 작은 몸·팔·발).
// 얼굴은 +Z 를 본다. Build 의 height(m)로 키를 맞추고 발바닥을 땅(0)에 놓는다.
// 모델 파일이 없는 컴퓨터에서는 연보라 타원으로 대신한다.
public static class NativeModel
{
    public static GameObject Build(float height = 1.4f)
    {
        var root = new GameObject("Native");
        var body = new GameObject("Body").transform; // 숨쉬듯 움직이는 부분(NativeView)
        body.SetParent(root.transform, false);

        var model = ArtLibrary.SpawnModel("native", body);
        if (model)
        {
            var b = Bounds(model);
            if (b.size.y > 0.001f) model.transform.localScale *= height / b.size.y;
            b = Bounds(model);
            model.transform.position += new Vector3(-b.center.x, -b.min.y, -b.center.z);
        }
        else
            PrototypeBootstrap.Part(body, PrimitiveType.Sphere, Vector3.up * height / 2f, new Vector3(height * 0.6f, height, height * 0.6f), new Color(0.72f, 0.62f, 0.95f));

        var col = root.AddComponent<CapsuleCollider>();
        col.radius = height * 0.2f;
        col.height = height;
        col.center = Vector3.up * height / 2f;
        root.AddComponent<NativeView>().body = body;
        return root;
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        var b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.zero);
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}

// 원주민 움직임: 숨쉬듯 살짝 오르내리고, 로봇이 가까이 오면 그쪽을 본다.
public class NativeView : MonoBehaviour
{
    public Transform body;
    public float lookRange = 10f;
    float phase;
    Vector3 baseScale;

    void Start()
    {
        phase = Random.value * 10f;
        baseScale = body.localScale;
    }

    void Update()
    {
        float s = Mathf.Sin((Time.time + phase) * 2.2f);
        body.localScale = new Vector3(baseScale.x * (1f - s * 0.015f), baseScale.y * (1f + s * 0.03f), baseScale.z * (1f - s * 0.015f));
        var gm = GameManager.Instance;
        if (!gm || !gm.Player) return;
        var to = gm.Player.position - transform.position;
        to.y = 0;
        if (to.sqrMagnitude < lookRange * lookRange && to.sqrMagnitude > 0.05f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 4f * Time.deltaTime);
    }
}
