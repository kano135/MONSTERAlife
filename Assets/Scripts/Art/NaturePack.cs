using System.Collections.Generic;
using UnityEngine;

// Stylized Nature Pack Free Sampler(soresa-assets) 모델 꺼내 쓰기.
// 모델은 Assets/Resources/StylizedNaturePack_FreeSampler/FBX 에 있다. 라이선스상 공개 저장소에는 안 올리므로(.gitignore)
// 팩이 없는 컴퓨터에서는 Spawn 이 null 을 돌려주고, 부르는 쪽이 예전 도형으로 대신 만든다.
// 크기는 실제 미터(나무 4~6m), 피벗은 바닥 가운데. 충돌(UCX_)·LOD 는 팩의 가져오기 스크립트가 붙여 준다.
public static class NaturePack
{
    const string Folder = "StylizedNaturePack_FreeSampler/FBX/";

    public static readonly string[] Trees =
        { "tree_oak_01", "tree_birch_01", "tree_cherry_02", "tree_autumn_maple_01" }; // 소나무(tree_pine_02, tree_alpine_pine_03)는 일단 뺐다
    public static readonly string[] Rocks = { "rock_02", "rock_sandstone_01" };

    static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

    public static GameObject Prefab(string name)
    {
        if (!cache.TryGetValue(name, out var prefab))
            cache[name] = prefab = Resources.Load<GameObject>(Folder + name);
        return prefab;
    }

    public static bool Available => Prefab(Trees[0]);

    // 모델 하나 놓기. 팩이 없으면 null.
    public static GameObject Spawn(string name, Transform parent, Vector3 localPos, float yRotation, float scale)
    {
        var prefab = Prefab(name);
        if (!prefab) return null;
        var go = Object.Instantiate(prefab, parent, false);
        go.name = name;
        go.transform.localPosition = localPos;
        // 모델 자체 회전·크기(블렌더에서 온 FBX 의 축 보정 등)는 살리고, 그 위에 돌리기·키우기
        go.transform.localRotation = Quaternion.Euler(0, yRotation, 0) * prefab.transform.localRotation;
        go.transform.localScale = prefab.transform.localScale * scale;
        // 멀리서는 안 그리게 층을 나눈다(CameraRig 가 층마다 보이는 거리를 정한다): 나무는 TreeLayer, 덤불·꽃·버섯 같은 작은 것은 SmallLayer.
        bool small = name.StartsWith("bush") || name.StartsWith("flower") || name.StartsWith("mushroom") || name.StartsWith("cactus");
        if (small || name.StartsWith("tree"))
            foreach (var tr in go.GetComponentsInChildren<Transform>(true))
            {
                tr.gameObject.layer = small ? SmallLayer : TreeLayer;
                if (small && tr.TryGetComponent<Renderer>(out var rend)) rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // 작은 것은 그림자 없이
            }
        return go;
    }

    // 그리는 거리 층(이름 없는 층 번호를 쓴다)
    public const int TreeLayer = 20, SmallLayer = 21;
    public const float TreeDistance = 165f, SmallDistance = 90f;

    public static bool HasCollider(GameObject go) => go.GetComponentInChildren<Collider>();

    // ── 파스텔 외계 행성 색으로 바꾸기 ──
    // 팩 모델은 팔레트 그림 한 장(칸마다 색)을 쓴다. 그 그림을 복사해 초록 칸(잎)만 다른 색으로, 또는 회색 칸(바위)을 라벤더로 바꾼다.
    // 줄기·꽃 같은 다른 색은 그대로라 나무 모양은 살아 있다.
    public enum Tint { Teal, Coral, Lavender, Stone }

    static readonly Dictionary<(Material, Tint), Material> recolored = new Dictionary<(Material, Tint), Material>();
    static readonly Dictionary<(Texture, Tint), Texture2D> recoloredTex = new Dictionary<(Texture, Tint), Texture2D>();

    public static void Recolor(GameObject go, Tint tint)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (!m || !m.mainTexture) continue;
                if (!recolored.TryGetValue((m, tint), out var copy))
                {
                    copy = new Material(m) { mainTexture = RecolorTexture(m.mainTexture, tint) };
                    recolored[(m, tint)] = copy;
                }
                mats[i] = copy;
            }
            r.sharedMaterials = mats;
        }
    }

    static Texture2D RecolorTexture(Texture src, Tint tint)
    {
        if (recoloredTex.TryGetValue((src, tint), out var done)) return done;
        // 가져온 그림은 읽기 막힘 → 렌더 텍스처로 한 번 그려서 읽는다
        var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(src, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false) { filterMode = src.filterMode, wrapMode = src.wrapMode };
        tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        var px = tex.GetPixels();
        for (int i = 0; i < px.Length; i++)
        {
            Color.RGBToHSV(px[i], out float h, out float sat, out float v);
            bool green = h > 0.17f && h < 0.5f && sat > 0.15f;
            bool grey = sat < 0.18f && v > 0.15f && v < 0.92f;
            if (tint == Tint.Teal && green) px[i] = Color.HSVToRGB(0.47f, sat * 0.75f, Mathf.Lerp(v, 1f, 0.08f));
            else if (tint == Tint.Coral && green) px[i] = Color.HSVToRGB(0.98f, 0.5f, Mathf.Lerp(v, 1f, 0.55f)); // 밝은 산호 분홍
            else if (tint == Tint.Lavender && (grey || (h > 0.05f && h < 0.15f && sat < 0.45f))) px[i] = Color.HSVToRGB(0.72f, 0.28f, Mathf.Lerp(v, 1f, 0.1f));
            // 절벽과 같은 따뜻한 연회색 돌(회색·갈색·주황 바위 칸 전부)
            else if (tint == Tint.Stone && (grey || (h < 0.15f && sat < 0.8f))) px[i] = Color.HSVToRGB(0.95f, 0.07f, Mathf.Lerp(v, 1f, 0.35f));
            px[i].a = 1f;
        }
        tex.SetPixels(px);
        tex.Apply();
        recoloredTex[(src, tint)] = tex;
        return tex;
    }
}
