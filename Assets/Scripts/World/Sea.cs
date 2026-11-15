using UnityEngine;

// 맵 밖은 바다. 맵 가장자리 GroundView.Beach(m) 안쪽부터 땅이 모래사장으로 낮아지며 바다에 잠기고(Landscape.HeightAt),
// 여기서는 그 위에 수면(맵 둘레에서 수평선까지)과 맵 밖 바다 밑바닥을 깐다.
// 로봇은 가슴께까지만 들어갈 수 있다: 가장자리에서 GroundView.SeaWall(m) 안쪽에 보이지 않는 벽(GroundView.EdgeWalls).
public static class Sea
{
    const float Far = 1400f; // 수면이 뻗는 거리(수평선 너머)
    static readonly Color Water = new Color(0.42f, 0.6f, 0.95f, 0.8f);
    static readonly Color Bed = new Color(0.62f, 0.66f, 0.72f);

    public static void Build()
    {
        var root = new GameObject("Sea").transform;
        // 수면: 모래사장이 시작되는 선 바로 안쪽부터 바깥으로(안쪽은 땅이 물보다 높아 어차피 안 보인다 → 강물과 겹치지 않게 비워 둔다)
        float inset = GroundView.Beach + 0.5f;
        Ring(root, "Surface", GroundView.MinX + inset, GroundView.MaxX - inset, GroundView.MinZ + inset, GroundView.MaxZ - inset, Cliff.WaterLevel,
            ArtLibrary.Transparent(null, Water));
        // 맵 밖 바닥(물이 반투명이라 아래가 비어 보이지 않게). 맵 안쪽 바닥은 지형이 맡는다.
        var bed = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Bed };
        bed.SetFloat("_Smoothness", 0f);
        Ring(root, "Bed", GroundView.MinX, GroundView.MaxX, GroundView.MinZ, GroundView.MaxZ, -Landscape.SeaDepth - 0.2f, bed);
    }

    // 가운데 네모(x0~x1, z0~z1)가 뚫린 큰 판(위를 본다)
    static void Ring(Transform parent, string name, float x0, float x1, float z0, float z1, float y, Material mat)
    {
        var rects = new[]
        {
            (-Far, x0, -Far, Far), (x1, Far, -Far, Far), // 서 · 동
            (x0, x1, z1, Far), (x0, x1, -Far, z0),       // 북 · 남
        };
        var v = new Vector3[16];
        var tri = new int[24];
        for (int i = 0; i < 4; i++)
        {
            var (ax, bx, az, bz) = rects[i];
            v[i * 4] = new Vector3(ax, y, az); v[i * 4 + 1] = new Vector3(bx, y, az);
            v[i * 4 + 2] = new Vector3(ax, y, bz); v[i * 4 + 3] = new Vector3(bx, y, bz);
            int k = i * 4;
            tri[i * 6] = k; tri[i * 6 + 1] = k + 2; tri[i * 6 + 2] = k + 1;
            tri[i * 6 + 3] = k + 1; tri[i * 6 + 4] = k + 2; tri[i * 6 + 5] = k + 3;
        }
        var mesh = new Mesh { vertices = v, triangles = tri };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
