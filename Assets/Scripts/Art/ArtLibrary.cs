using System.Collections.Generic;
using UnityEngine;

// Assets/Resources/Art 의 GPT 그림을 스프라이트로 꺼내 준다. 그림이 없으면 null → 임시 도형을 쓴다.
// 파일 이름 규칙: Plants/<식물id>_1~5, Plants/<식물id>_variant, Icons/plant_<식물id>, Props/..., Robot/..., UI/..., Textures/...
public static class ArtLibrary
{
    static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    static readonly Vector2 Bottom = new Vector2(0.5f, 0f);
    static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

    public static Texture2D Texture(string path) => Resources.Load<Texture2D>("Art/" + path);

    // 세워 둘 그림(바닥 가운데 기준)
    public static Sprite World(string path) => Get(path, Bottom);

    // 화면 UI용 그림(가운데 기준)
    public static Sprite Ui(string path) => Get(path, Center);

    public static Sprite PlantStage(PlantData p, int stage) => World($"Plants/{p.id}_{stage + 1}");
    public static Sprite PlantVariant(PlantData p) => World($"Plants/{p.id}_variant");
    // 땅에 심은 모습(화분을 잘라 낸 그림). 없으면 화분 그림을 쓴다.
    public static Sprite PlantStageGround(PlantData p, int stage) => World($"Plants/{p.id}_{stage + 1}_ground") ?? PlantStage(p, stage);
    public static Sprite PlantVariantGround(PlantData p) => World($"Plants/{p.id}_variant_ground") ?? PlantVariant(p);
    public static Sprite PlantIcon(PlantData p) => Ui($"Icons/plant_{p.id}");

    // 3D 모델(Resources/Art/Models). 재질 이름이 col_RRGGBB 면 그 색의 URP 재질로 바꿔 입힌다
    // (OBJ 가져오기 재질은 URP 에서 분홍색이 될 수 있어서).
    public static GameObject SpawnModel(string path, Transform parent)
    {
        var prefab = Resources.Load<GameObject>("Art/Models/" + path);
        if (!prefab) return null;
        var go = Object.Instantiate(prefab, parent, false);
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var name = mats[i] ? mats[i].name : "";
                int at = name.IndexOf("col_");
                if (at < 0 || !ColorUtility.TryParseHtmlString("#" + name.Substring(at + 4, 6), out var color)) continue;
                if (!colorMaterials.TryGetValue(color, out var m))
                {
                    m = new Material(lit) { color = color };
                    m.SetFloat("_Smoothness", 0.35f);
                    colorMaterials[color] = m;
                }
                mats[i] = m;
            }
            r.sharedMaterials = mats;
        }
        return go;
    }

    static readonly Dictionary<Color, Material> colorMaterials = new Dictionary<Color, Material>();

    // 반투명 재질(풀밭 덮개·비닐 벽). renderQueue 2999: 세워 둔 그림(3000)보다 먼저 그려 그림을 덮지 않게.
    // shortcut: URP Unlit 을 코드로 반투명 설정. 빌드에서 셰이더가 빠지면 Always Included Shaders 에 Unlit 을 넣는다.
    public static Material Transparent(Texture2D tex, Color color)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetFloat("_Surface", 1);
        m.SetFloat("_Blend", 0);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 2999;
        if (tex) m.mainTexture = tex;
        m.color = color;
        return m;
    }

    static Sprite Get(string path, Vector2 pivot)
    {
        string key = path + pivot;
        if (sprites.TryGetValue(key, out var s)) return s;
        var tex = Texture(path);
        s = tex ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, 100f) : null;
        sprites[key] = s;
        return s;
    }
}
