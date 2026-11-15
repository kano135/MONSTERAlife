using UnityEngine;

// 땅: 황무지 그림 위에 풀밭 그림을 깔고, 행성 생명력(거주 가능도)만큼 풀밭이 진해진다.
// 하늘과 시간대 색(Tint)은 DayNight 가 정한다.
public class GroundView : MonoBehaviour
{
    public const float Size = 320f; // 맵 한 변(미터). 처음 80 → 160 → 320
    public const float ExtraWest = 150f; // 서쪽(폭포 산 쪽)으로 더 넓힌 길이. 맵은 가로 Size + ExtraWest, 세로 Size
    // 맵 밖은 바다(Sea). 가장자리에서 Beach(m) 안쪽부터 땅이 모래사장으로 낮아지고,
    // SeaWall(m) 안쪽에 보이지 않는 벽이 있어 바다에 가슴께까지만 들어갈 수 있다. ★ SeaWall 을 줄이면 더 깊이 들어간다.
    public const float Beach = 16f, SeaWall = 5f;
    // 맵 테두리(세계 좌표)
    public static float MinX => -Size / 2f - ExtraWest;
    public static float MaxX => Size / 2f;
    public static float MinZ => -Size / 2f;
    public static float MaxZ => Size / 2f;
    static Vector2 Tiling => new Vector2(Tiles * (Size + ExtraWest) / Size, Tiles); // 넓어져도 땅 무늬 크기는 그대로
    const float Tiles = 40f; // 맵이 커져도 땅 무늬 크기는 그대로
    static readonly Color DeadColor = new Color(0.55f, 0.52f, 0.5f);
    static readonly Color AliveColor = new Color(0.55f, 0.82f, 0.45f);

    Material dead, grass;

    // 언덕(Landscape)이 같은 그림·색을 이어 쓴다
    public Material DeadMaterial => dead;
    public Material GrassMaterial => grass;
    public Transform DeadPlane { get; private set; }

    // 시간대 색(DayNight 가 매 프레임 넣는다). 땅 그림에 곱한다.
    public Color Tint { get; set; } = Color.white;

    public static GroundView Build()
    {
        var view = new GameObject("Ground").AddComponent<GroundView>();
        var deadTex = ArtLibrary.Texture("Textures/ground_dead");
        var grassTex = ArtLibrary.Texture("Textures/ground_grass");

        var deadPlane = Plane(view.transform, "Dead", 0f);
        view.DeadPlane = deadPlane.transform;
        EdgeWalls(view.transform);
        view.dead = deadPlane.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        if (deadTex)
        {
            view.dead.mainTexture = deadTex;
            view.dead.mainTextureScale = Tiling;
        }
        else view.dead.color = DeadColor;

        if (grassTex)
        {
            var grassPlane = Plane(view.transform, "Grass", 0.02f);
            Object.Destroy(grassPlane.GetComponent<Collider>());
            view.grass = grassPlane.material = ArtLibrary.Transparent(grassTex, new Color(1, 1, 1, 0));
            view.grass.mainTextureScale = Tiling;
        }

        return view;
    }

    // 땅을 지형 메시(Landscape)가 대신 그리고 받칠 때: 평평한 판은 안 보이게, 충돌도 없앤다(재질·색 계산은 계속 한다).
    public void HidePlanes()
    {
        foreach (var r in GetComponentsInChildren<MeshRenderer>()) r.enabled = false;
        foreach (var c in GetComponentsInChildren<MeshCollider>()) Destroy(c);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (!gm) return;
        if (dead.mainTexture) dead.color = Tint;
        else dead.color = Color.Lerp(DeadColor, AliveColor, grass ? 0 : gm.Habitability) * Tint;
        if (grass) grass.color = new Color(Tint.r, Tint.g, Tint.b, gm.Habitability);
    }

    // 바다 속 보이지 않는 벽: 가장자리에서 SeaWall 안쪽. 더 깊이는 못 들어간다
    static void EdgeWalls(Transform parent)
    {
        float width = MaxX - MinX, midX = (MinX + MaxX) / 2f;
        foreach (var (pos, size) in new[]
        {
            (new Vector3(MaxX - SeaWall + 0.5f, 20f, 0), new Vector3(1f, 60f, Size + 2f)),
            (new Vector3(MinX + SeaWall - 0.5f, 20f, 0), new Vector3(1f, 60f, Size + 2f)),
            (new Vector3(midX, 20f, MaxZ - SeaWall + 0.5f), new Vector3(width + 2f, 60f, 1f)),
            (new Vector3(midX, 20f, MinZ + SeaWall - 0.5f), new Vector3(width + 2f, 60f, 1f)),
        })
        {
            var wall = new GameObject("EdgeWall").AddComponent<BoxCollider>();
            wall.transform.SetParent(parent, false);
            wall.transform.position = pos;
            wall.size = size;
        }
    }

    static Renderer Plane(Transform parent, string name, float y)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3((MinX + MaxX) / 2f, y, 0);
        go.transform.localScale = new Vector3((MaxX - MinX) / 10f, 1, Size / 10f);
        return go.GetComponent<Renderer>();
    }
}
