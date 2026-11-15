using UnityEngine;

// 하늘에 떠 있는 고리 달린 큰 보라 행성(파스텔 외계 행성 느낌). 그림은 코드로 그린다.
// 카메라를 따라다니는 아주 먼 판(하늘처럼)이라 걸어가도 크기가 그대로이고, 안개에 묻히지 않는다(스프라이트).
// 그림이 있으면 Resources/Art/Textures/planet.png 로 바꿔 넣을 수 있다.
public class SkyPlanet : MonoBehaviour
{
    // 하늘에서의 방향(북동쪽 위)과 크기
    static readonly Vector3 Direction = new Vector3(0.55f, 0.2f, 0.72f); // 지평선 위 약 12도
    const float Distance = 700f, Size = 260f;

    Transform board;

    public static SkyPlanet Build()
    {
        var sp = new GameObject("SkyPlanet").AddComponent<SkyPlanet>();
        var tex = ArtLibrary.Texture("Textures/planet") ?? Draw(512, 288);
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width / Size);
        var go = new GameObject("PlanetSprite", typeof(SpriteRenderer));
        go.transform.SetParent(sp.transform, false);
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = -100;
        sp.board = go.transform;
        return sp;
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (!cam) return;
        var dir = Direction.normalized;
        board.position = cam.transform.position + dir * Distance;
        board.rotation = Quaternion.LookRotation(dir); // 카메라 쪽을 본다
        board.localScale = Vector3.one;
    }

    // 행성 그림: 연보라 구(왼쪽 위가 밝게) + 비스듬한 고리(뒤쪽은 행성에 가려지고 앞쪽은 행성 위로)
    static Texture2D Draw(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[w * h];
        var center = new Vector2(w * 0.5f, h * 0.5f);
        float r = h * 0.36f;
        var light = new Color(0.86f, 0.8f, 1f);
        var dark = new Color(0.62f, 0.52f, 0.86f);
        var ringCol = new Color(0.82f, 0.74f, 0.98f);
        const float tilt = -0.32f; // 고리 기울기(라디안)
        float cos = Mathf.Cos(tilt), sin = Mathf.Sin(tilt);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(x, y) - center;
                Color c = Color.clear;
                // 고리 좌표(기울여서 납작한 타원)
                float rx = p.x * cos - p.y * sin, ry = (p.x * sin + p.y * cos) / 0.22f;
                float ringD = Mathf.Sqrt(rx * rx + ry * ry) / r;
                float ringA = Mathf.Clamp01(1f - Mathf.Abs(ringD - 1.55f) / 0.16f) * 0.85f;
                bool ringFront = ry < 0; // 아래쪽 반이 앞
                float d = p.magnitude / r;
                if (ringA > 0 && !ringFront) c = new Color(ringCol.r, ringCol.g, ringCol.b, ringA);
                if (d < 1f)
                {
                    float shade = Mathf.Clamp01(0.55f + Vector2.Dot(p / r, new Vector2(-0.6f, 0.6f)) * 0.5f);
                    var body = Color.Lerp(dark, light, shade);
                    float edge = Mathf.Clamp01((1f - d) * 12f); // 가장자리 부드럽게
                    c = Color.Lerp(c, new Color(body.r, body.g, body.b, 1f), edge);
                }
                if (ringA > 0 && ringFront)
                    c = Color.Lerp(c, new Color(ringCol.r, ringCol.g, ringCol.b, 1f), ringA);
                c.a = Mathf.Max(c.a, ringFront ? ringA : 0f);
                px[y * w + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
