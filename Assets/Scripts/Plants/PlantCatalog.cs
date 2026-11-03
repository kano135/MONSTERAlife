using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 식물 목록. Resources/Plants 에 에셋이 있으면 그걸 쓰고, 없으면 기본 10종을 코드로 만든다.
public static class PlantCatalog
{
    public static List<PlantData> Load()
    {
        var assets = Resources.LoadAll<PlantData>("Plants");
        return assets.Length > 0 ? assets.OrderBy(p => p.id).ToList() : CreateDefaults();
    }

    public static List<PlantData> CreateDefaults()
    {
        return new List<PlantData>
        {
            // id, 이름, 분류, 희귀도, 피기까지 걸리는 날(보통 속도), 하루 수분 소모, 좋아하는 수분·햇빛·온도, 색, 변종, 변종 색
            Make("01_dandelion", "민들레", "일반 식물", 1, 1f, 0.5f, W(0.4f, 0.7f), S(0.6f, 1f), T(0.3f, 0.7f), "#F7D44C", "푸른 민들레", "#7FB8FF"),
            Make("02_sunflower", "해바라기", "꽃 식물", 1, 1.5f, 0.5f, W(0.4f, 0.7f), S(0.7f, 1f), T(0.5f, 0.8f), "#FFB400", "달빛 해바라기", "#E6E9FF"),
            Make("03_mimosa", "미모사", "반응형 식물", 2, 1.5f, 0.5f, W(0.5f, 0.8f), S(0.4f, 0.8f), T(0.5f, 0.8f), "#FF9CC8", "유리 미모사", "#BFF4FF"),
            Make("04_flytrap", "파리지옥", "식충 식물", 3, 2f, 0.5f, W(0.7f, 1f), S(0.7f, 1f), T(0.4f, 0.7f), "#7BD15A", "화염 파리지옥", "#FF5A3C"),
            Make("05_sundew", "끈끈이주걱", "식충 식물", 3, 2f, 0.5f, W(0.7f, 1f), S(0.6f, 1f), T(0.3f, 0.6f), "#E2454F", "수정 끈끈이주걱", "#C8F0FF"),
            Make("06_pitcher", "벌레잡이통풀", "식충 식물", 3, 2.5f, 0.5f, W(0.75f, 1f), S(0.3f, 0.7f), T(0.6f, 0.9f), "#9BC45A", "무지개 벌레잡이통풀", "#C88CFF"),
            Make("07_lithops", "리톱스", "다육 식물", 3, 2.5f, 0.15f, W(0.05f, 0.3f), S(0.7f, 1f), T(0.5f, 0.9f), "#C9B79C", "수정 리톱스", "#B9E8FF"),
            Make("08_resurrection", "부활초", "특수 식물", 4, 3f, 0.3f, W(0.2f, 0.6f), S(0.4f, 0.9f), T(0.5f, 0.9f), "#6E9F4A", "황금 부활초", "#FFD86B"),
            Make("09_cactus", "선인장", "다육 식물", 2, 2f, 0.15f, W(0.05f, 0.35f), S(0.7f, 1f), T(0.6f, 1f), "#5FB36B", "솜사탕 선인장", "#FFB7E1"),
            Make("10_waterlily", "수련", "수생 식물", 2, 1.5f, 0.3f, W(0.85f, 1f), S(0.6f, 1f), T(0.4f, 0.8f), "#FFC0D9", "별빛 수련", "#9FA8FF"),
        };
    }

    static Vector2 W(float a, float b) => new Vector2(a, b);
    static Vector2 S(float a, float b) => new Vector2(a, b);
    static Vector2 T(float a, float b) => new Vector2(a, b);

    static PlantData Make(string id, string name, string category, int rarity, float daysToBloom, float waterPerDay,
        Vector2 water, Vector2 sun, Vector2 temp, string color, string variant, string variantColor)
    {
        var p = ScriptableObject.CreateInstance<PlantData>();
        p.name = id;
        p.id = id;
        p.displayName = name;
        p.category = category;
        p.rarity = rarity;
        // 씨앗→개화는 4칸이라 날 수를 4로 나눈다
        p.secondsPerStage = daysToBloom * Progression.RealSecondsPerDay / PlantSpot.BloomStage;
        p.waterUsePerSecond = waterPerDay / Progression.RealSecondsPerDay;
        p.waterRange = water;
        p.sunRange = sun;
        p.tempRange = temp;
        p.variantName = variant;
        ColorUtility.TryParseHtmlString(color, out p.color);
        ColorUtility.TryParseHtmlString(variantColor, out p.variantColor);
        return p;
    }
}
