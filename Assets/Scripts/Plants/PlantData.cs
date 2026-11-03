using UnityEngine;

// 식물 한 종의 설정. 수분·햇빛·온도는 모두 0~1(=0~100%) 기준.
[CreateAssetMenu(menuName = "몬스테라/식물 데이터", fileName = "Plant")]
public class PlantData : ScriptableObject
{
    public static readonly string[] StageNames = { "씨앗", "발아", "성장", "성숙", "개화" };

    public string id;
    public string displayName;
    public string category;
    [Range(1, 5)] public int rarity = 1;

    [Tooltip("한 단계 자라는 데 걸리는 기본 시간(초)")]
    public float secondsPerStage = 30f;

    public Vector2 waterRange = new Vector2(0.4f, 0.8f);
    public Vector2 sunRange = new Vector2(0.5f, 1f);
    public Vector2 tempRange = new Vector2(0.4f, 0.7f);
    [Tooltip("초당 줄어드는 수분")]
    public float waterUsePerSecond = 0.01f;

    public Color color = Color.yellow;
    [Tooltip("선호 환경을 80% 이상 유지하며 키우면 이 변종으로 핀다")]
    public string variantName;
    public Color variantColor = Color.cyan;

    [Tooltip("단계별 모델(씨앗~개화 5개). 비워 두면 임시 도형으로 보인다")]
    public GameObject[] stagePrefabs = new GameObject[5];
    public Sprite icon;
}
