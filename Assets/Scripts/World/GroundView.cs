using UnityEngine;

// 땅: 황무지 그림 위에 풀밭 그림을 깔고, 행성 생명력(거주 가능도)만큼 풀밭이 진해진다.
// 하늘과 시간대 색(Tint)은 DayNight 가 정한다.
public class GroundView : MonoBehaviour
{
    public const float Size = 160f; // 맵 한 변(미터). 처음 80 → 2배
    const float Tiles = 20f; // 맵이 커져도 땅 무늬 크기는 그대로
    static readonly Color DeadColor = new Color(0.55f, 0.52f, 0.5f);
    static readonly Color AliveColor = new Color(0.55f, 0.82f, 0.45f);

    Material dead, grass;

    // 시간대 색(DayNight 가 매 프레임 넣는다). 땅 그림에 곱한다.
    public Color Tint { get; set; } = Color.white;

    public static GroundView Build()
    {
        var view = new GameObject("Ground").AddComponent<GroundView>();
        var deadTex = ArtLibrary.Texture("Textures/ground_dead");
        var grassTex = ArtLibrary.Texture("Textures/ground_grass");

        var deadPlane = Plane(view.transform, "Dead", 0f);
        view.dead = deadPlane.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        if (deadTex)
        {
            view.dead.mainTexture = deadTex;
            view.dead.mainTextureScale = Vector2.one * Tiles;
        }
        else view.dead.color = DeadColor;

        if (grassTex)
        {
            var grassPlane = Plane(view.transform, "Grass", 0.02f);
            Object.Destroy(grassPlane.GetComponent<Collider>());
            view.grass = grassPlane.material = ArtLibrary.Transparent(grassTex, new Color(1, 1, 1, 0));
            view.grass.mainTextureScale = Vector2.one * Tiles;
        }

        return view;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (!gm) return;
        if (dead.mainTexture) dead.color = Tint;
        else dead.color = Color.Lerp(DeadColor, AliveColor, grass ? 0 : gm.Habitability) * Tint;
        if (grass) grass.color = new Color(Tint.r, Tint.g, Tint.b, gm.Habitability);
    }

    static Renderer Plane(Transform parent, string name, float y)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.up * y;
        go.transform.localScale = new Vector3(Size / 10f, 1, Size / 10f);
        return go.GetComponent<Renderer>();
    }
}
