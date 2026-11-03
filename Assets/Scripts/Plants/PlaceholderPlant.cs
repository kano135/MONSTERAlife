using UnityEngine;

// shortcut: 기본 도형으로 만든 임시 식물. PlantData.stagePrefabs 에 진짜 모델을 넣으면 쓰이지 않는다.
public static class PlaceholderPlant
{
    static readonly Color Seed = new Color(0.55f, 0.38f, 0.22f);
    static readonly Color Stem = new Color(0.35f, 0.75f, 0.35f);

    public static Transform Build(Transform parent, int stage, Color flower, bool rotten = false)
    {
        var root = new GameObject("Visual").transform;
        root.SetParent(parent, false);
        if (rotten) root.localRotation = Quaternion.Euler(0, 0, 50f); // 축 늘어져 쓰러진 모습

        if (stage == 0)
        {
            Part(root, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.15f, Seed);
            return root;
        }

        var stem = rotten ? flower : Stem;
        float height = new[] { 0f, 0.25f, 0.6f, 0.9f, 1.1f }[stage];
        Part(root, PrimitiveType.Cylinder, Vector3.up * height / 2, new Vector3(0.06f, height / 2, 0.06f), stem);
        if (stage >= 2)
        {
            Part(root, PrimitiveType.Sphere, new Vector3(0.15f, height * 0.4f, 0), new Vector3(0.25f, 0.06f, 0.12f), stem);
            Part(root, PrimitiveType.Sphere, new Vector3(-0.15f, height * 0.55f, 0), new Vector3(0.25f, 0.06f, 0.12f), stem);
        }
        if (stage == 3) Part(root, PrimitiveType.Sphere, Vector3.up * height, Vector3.one * 0.18f, Color.Lerp(stem, flower, 0.5f));
        if (stage == 4) Part(root, PrimitiveType.Sphere, Vector3.up * height, new Vector3(0.5f, 0.25f, 0.5f), flower);
        return root;
    }

    static void Part(Transform root, PrimitiveType type, Vector3 pos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(root, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().material.color = color;
    }
}
