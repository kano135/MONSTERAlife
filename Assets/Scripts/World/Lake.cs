using UnityEngine;

// 기지 울타리 바로 밖(남쪽 출입구 왼쪽)의 작은 호수. 물가에서 E를 누르면 물뿌리개를 가득 채운다(GameManager.RefillCan).
// 물 그림은 Resources/Art/Textures/water.png 로 넣으면 단색 대신 나온다. 걸어 들어갈 수 있는 얕은 물이다.
public class Lake : MonoBehaviour
{
    public static Lake Instance { get; private set; }

    public float radius = 4f;
    // 물가에서 이만큼 떨어진 곳까지 E가 닿는다
    public float reach = 1.8f;

    static readonly Color WaterColor = new Color(0.35f, 0.7f, 0.95f, 0.85f);
    static readonly Color ShoreColor = new Color(0.78f, 0.7f, 0.52f);

    public bool InReach(Vector3 p)
    {
        var d = p - transform.position;
        d.y = 0;
        return d.magnitude < radius + reach;
    }

    // 이 점이 호수(+여유)에 겹치면 true. 씨앗·채집 자원이 물 위에 생기지 않게 쓴다.
    public static bool Overlaps(Vector3 p, float margin)
    {
        if (!Instance) return false;
        var d = p - Instance.transform.position;
        d.y = 0;
        return d.magnitude < Instance.radius + margin;
    }

    public static Lake Build(Vector3 center, float radius)
    {
        var lake = new GameObject("Lake").AddComponent<Lake>();
        Instance = lake;
        lake.radius = radius;
        lake.transform.position = center;

        // 물가(모래) 고리 + 물. 둘 다 땅 위에 아주 얇게 깔아 걸어 다닐 수 있다.
        PrototypeBootstrap.Part(lake.transform, PrimitiveType.Cylinder, Vector3.up * 0.03f, new Vector3(radius * 2 + 1.6f, 0.01f, radius * 2 + 1.6f), ShoreColor).name = "Shore";
        var water = PrototypeBootstrap.Part(lake.transform, PrimitiveType.Cylinder, Vector3.up * 0.06f, new Vector3(radius * 2, 0.01f, radius * 2), WaterColor);
        water.name = "Water";
        var tex = ArtLibrary.Texture("Textures/water");
        var mat = ArtLibrary.Transparent(tex, tex ? Color.white : WaterColor);
        water.GetComponent<Renderer>().sharedMaterial = mat;

        // 물가 바위 몇 개(어디가 호수인지 멀리서도 보이게)
        for (int i = 0; i < 5; i++)
        {
            float a = i / 5f * Mathf.PI * 2 + 0.4f;
            var pos = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (radius + 0.6f) + Vector3.up * 0.2f;
            PrototypeBootstrap.Part(lake.transform, PrimitiveType.Sphere, pos, new Vector3(0.7f, 0.4f, 0.6f), new Color(0.6f, 0.58f, 0.55f));
        }
        return lake;
    }
}
