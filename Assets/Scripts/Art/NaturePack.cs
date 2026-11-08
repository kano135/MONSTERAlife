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
        { "tree_oak_01", "tree_pine_02", "tree_birch_01", "tree_cherry_02", "tree_autumn_maple_01", "tree_alpine_pine_03" };
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
        return go;
    }

    public static bool HasCollider(GameObject go) => go.GetComponentInChildren<Collider>();
}
