using UnityEngine;

// 원주민(임시): 그냥 타원형. 이벤트 흐름을 보려는 자리표시.
// 원주민 마을은 기지 동쪽 들판(Center). 나중에 진짜 모델·표정으로 바꾼다.
public static class Village
{
    public static readonly Vector3 Center = new Vector3(70f, 0, 30f);
    public const float Radius = 8f;

    public static bool Blocks(Vector3 p, float margin)
    {
        var d = p - Center; d.y = 0;
        return d.magnitude < Radius + margin;
    }

    public static void Build()
    {
        var root = new GameObject("Village").transform;
        root.position = Center;
        var color = new Color(0.72f, 0.62f, 0.95f);
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f;
            var native = PrototypeBootstrap.Part(root, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 4f, 0.8f, Mathf.Sin(a) * 4f), new Vector3(1f, 1.6f, 1f), color);
            native.name = "Native";
            native.AddComponent<SphereCollider>();
        }
    }
}
