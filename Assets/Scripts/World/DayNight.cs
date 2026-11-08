using UnityEngine;

// 하루 색: 게임 시계(GameManager.Minute)에 맞춰 하늘·땅·햇빛·주변광·안개 색을 바꾼다.
//   오전 8시 ~ 오후 5시 = 낮(파스텔 하늘색 하늘, 분홍빛 땅, 밝은 햇빛) — 참고 그림 색
//   오전 6~8시 = 해 뜰 녘, 오후 5시 ~ 6시 반 = 노을, 오후 8시 ~ 새벽 2시 = 밤. 사이는 부드럽게 섞는다.
// 색을 바꾸려면 아래 Day/Dawn/Sunset/Night 의 숫자만 고치면 된다.
// 하늘은 위(skyTop) → 지평선(skyHorizon) 그라데이션을 코드로 그려 스카이박스로 쓰고, 안개도 지평선 색으로 맞춰
// 맵 끝이 하늘에 자연스럽게 녹아들게 한다.
public class DayNight : MonoBehaviour
{
    struct Look
    {
        public Color skyTop, skyHorizon, ground, sun, ambient;
        public float sunIntensity, sunPitch;
        public Look(Color top, Color horizon, Color ground, Color sun, float intensity, float pitch, Color ambient)
        { skyTop = top; skyHorizon = horizon; this.ground = ground; this.sun = sun; sunIntensity = intensity; sunPitch = pitch; this.ambient = ambient; }

        public static Look Lerp(Look a, Look b, float t) => new Look(
            Color.Lerp(a.skyTop, b.skyTop, t), Color.Lerp(a.skyHorizon, b.skyHorizon, t), Color.Lerp(a.ground, b.ground, t),
            Color.Lerp(a.sun, b.sun, t), Mathf.Lerp(a.sunIntensity, b.sunIntensity, t), Mathf.Lerp(a.sunPitch, b.sunPitch, t),
            Color.Lerp(a.ambient, b.ambient, t));
    }

    static Color C(int r, int g, int b) => new Color32((byte)r, (byte)g, (byte)b, 255);

    // 하늘 위, 지평선, 땅(그림에 곱하는 색), 햇빛 색, 햇빛 세기, 해 높이(도), 주변광
    static readonly Look Day    = new Look(C(118, 205, 235), C(206, 238, 240), new Color(1.35f, 1.08f, 1.06f), C(255, 246, 225), 1.6f, 55f, C(184, 199, 209));
    static readonly Look Dawn   = new Look(C(120, 140, 205), C(255, 200, 165), new Color(0.95f, 0.75f, 0.72f), C(255, 205, 155), 0.9f, 12f, C(140, 128, 140));
    static readonly Look Sunset = new Look(C(105, 108, 190), C(255, 160, 128), new Color(0.95f, 0.68f, 0.62f), C(255, 158, 102), 0.8f, 8f, C(140, 115, 128));
    static readonly Look Night  = new Look(C(14, 18, 46), C(38, 46, 90), new Color(0.45f, 0.47f, 0.65f), C(140, 165, 255), 0.3f, 40f, C(46, 51, 82));

    // (게임 시각(분), 그때 색). 사이는 섞는다. 하루는 오전 6시에 시작해 새벽 2시(26시)에 끝난다.
    static readonly (float minute, Look look)[] Keys =
    {
        (6 * 60, Dawn),
        (8 * 60, Day),
        (17 * 60, Day),
        (18.5f * 60, Sunset),
        (20 * 60, Night),
        (26 * 60, Night),
    };

    const float FogStart = 45f, FogEnd = 150f;

    Light sun;
    Material skyMat;
    Texture2D skyTex;
    GroundView ground;
    readonly Color[] skyPixels = new Color[SkyHeight];
    const int SkyHeight = 64;

    public static DayNight Build(GroundView ground)
    {
        var dn = new GameObject("DayNight").AddComponent<DayNight>();
        dn.ground = ground;

        // 씬의 해(Directional Light)를 쓴다. 없으면 만든다.
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { dn.sun = l; break; }
        if (!dn.sun)
        {
            dn.sun = new GameObject("Sun").AddComponent<Light>();
            dn.sun.type = LightType.Directional;
            dn.sun.shadows = LightShadows.Soft;
        }

        // 하늘: 세로 그라데이션 그림(64칸)을 파노라마 스카이박스에 건다
        dn.skyTex = new Texture2D(1, SkyHeight, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var shader = Shader.Find("Skybox/Panoramic");
        if (shader)
        {
            dn.skyMat = new Material(shader);
            dn.skyMat.SetTexture("_MainTex", dn.skyTex);
            dn.skyMat.SetFloat("_Mapping", 1);   // 위도-경도
            dn.skyMat.SetFloat("_ImageType", 0); // 360도
            RenderSettings.skybox = dn.skyMat;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = FogStart;
        RenderSettings.fogEndDistance = FogEnd;
        dn.Apply(Day); // 첫 화면
        return dn;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (!gm) return;
        Apply(LookAt(gm.Minute));
    }

    static Look LookAt(float minute)
    {
        if (minute <= Keys[0].minute) return Keys[0].look;
        for (int i = 1; i < Keys.Length; i++)
            if (minute <= Keys[i].minute)
            {
                float t = Mathf.InverseLerp(Keys[i - 1].minute, Keys[i].minute, minute);
                return Look.Lerp(Keys[i - 1].look, Keys[i].look, Mathf.SmoothStep(0, 1, t));
            }
        return Keys[Keys.Length - 1].look;
    }

    void Apply(Look look)
    {
        // 해: 아침엔 동쪽, 저녁엔 서쪽에서 비춘다
        var gm = GameManager.Instance;
        float dayT = gm ? Mathf.InverseLerp(Progression.DayStartMinute, 20 * 60, gm.Minute) : 0.5f;
        sun.transform.rotation = Quaternion.Euler(look.sunPitch, Mathf.Lerp(-80f, 80f, dayT), 0);
        sun.color = look.sun;
        sun.intensity = look.sunIntensity;

        RenderSettings.ambientLight = look.ambient;
        RenderSettings.fogColor = look.skyHorizon;
        if (ground) ground.Tint = look.ground;

        if (skyMat)
        {
            // 아래 절반(땅 밑)은 지평선 색, 위 절반은 지평선 → 하늘 위로
            for (int y = 0; y < SkyHeight; y++)
            {
                float up = Mathf.Clamp01((y / (float)(SkyHeight - 1) - 0.5f) * 2f);
                skyPixels[y] = Color.Lerp(look.skyHorizon, look.skyTop, Mathf.Pow(up, 0.6f));
            }
            skyTex.SetPixels(skyPixels);
            skyTex.Apply(false);
        }
    }
}
