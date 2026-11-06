using UnityEngine;

// 땅: 황무지 그림 위에 풀밭 그림을 깔고, 행성 생명력(거주 가능도)만큼 풀밭이 진해진다. 하늘 그림도 여기서 건다.
public class GroundView : MonoBehaviour
{
    public const float Size = 80f;
    const float Tiles = 10f;
    static readonly Color DeadColor = new Color(0.55f, 0.52f, 0.5f);
    static readonly Color AliveColor = new Color(0.55f, 0.82f, 0.45f);

    Material dead, grass;

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

        var sky = ArtLibrary.Texture("Textures/sky");
        var skyShader = Shader.Find("Skybox/Panoramic");
        if (sky && skyShader)
        {
            var m = new Material(skyShader);
            m.SetTexture("_MainTex", sky);
            m.SetFloat("_ImageType", 1); // 180도 그림을 뒤쪽에 거울로 붙인다
            m.SetFloat("_MirrorOnBack", 1);
            RenderSettings.skybox = m;
        }
        return view;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (!gm) return;
        if (grass) grass.color = new Color(1, 1, 1, gm.Habitability);
        else if (!dead.mainTexture) dead.color = Color.Lerp(DeadColor, AliveColor, gm.Habitability);
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
