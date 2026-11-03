using System.Linq;

// 100일 일정·시계·도구·재료·설계도·보상 순서·성적 기준. 밸런스는 여기 숫자만 고치면 된다.
public static class Progression
{
    public const int TotalDays = 100;

    // 시계는 스타듀밸리와 같다: 오전 6시 시작, 새벽 2시에 하루 끝, 게임 10분 = 실제 7초 → 하루 14분
    public const int DayStartMinute = 6 * 60;
    public const int DayEndMinute = 26 * 60;
    public const float RealSecondsPer10Minutes = 7f;
    public const float RealSecondsPerDay = (DayEndMinute - DayStartMinute) / 10f * RealSecondsPer10Minutes;

    // 지구로 꽃을 보낼 수 있는 날: 5일마다(5, 10, … 100일차) → 100일 동안 20번
    public const int SendEveryDays = 5;
    public static bool IsSendDay(int day) => day % SendEveryDays == 0;
    public static int NextSendDay(int day) => (day + SendEveryDays - 1) / SendEveryDays * SendEveryDays;

    // 연료 가득(150) = 15일치. 생존선을 못 넘어 15일(미션 3번) 동안 연료를 못 받으면 바닥나서 잠드는 엔딩.
    public const float StartFuel = 150f;
    public const float MaxFuel = 150f;
    public const float FuelPerDay = 10f;

    // 미션: 발송일마다 보낸 꽃 점수(꽃 1송이 = 희귀도 ★ 수, 변종은 2배)로 판정한다.
    //   생존선 넘음      → 연료통(MissionFuel) 받음. 못 넘으면 연료를 못 받는다.
    //   업그레이드선 넘음 → 보상 순서(Rewards)대로 도구·설계키트 하나 + 비료(점수가 높을수록 높은 단계)
    //   최종선 넘음      → 최종선을 넘은 만큼이 성적 점수로 쌓임
    // 미션 n(1~20)의 생존선 = 3 + n. 업그레이드선은 2배, 최종선은 3배.
    public const float MissionFuel = 50f; // 5일치
    public static int MissionNumber(int day) => day / SendEveryDays;
    public static int SurviveLine(int mission) => 3 + mission;
    public static int UpgradeLine(int mission) => SurviveLine(mission) * 2;
    public static int FinalLine(int mission) => SurviveLine(mission) * 3;
    public static int FlowerPoints(PlantData p, bool variant) => p.rarity * (variant ? 2 : 1);
    // 최종선 초과 점수 100점을 모으면 성적표 '미션' 칸 20점 만점
    public const int BonusForFullMissionScore = 100;

    // 비료 1~5단계. 준 꽃은 수확할 때까지 FertilizerBoost 배로 빨리 자란다(임시 수치, 밸런스는 나중에).
    // 업그레이드선을 넘을 때 FertilizerPerReward 개씩 받고, 단계는 업그레이드선(1단계)~최종선(5단계) 사이 어디까지 갔는지로 정한다.
    public const int FertilizerGrades = 5;
    public const int FertilizerPerReward = 2;
    public static float FertilizerBoost(int grade) => 1f + 0.25f * grade;
    public static int FertilizerGradeFor(int points, int mission)
    {
        float t = (points - UpgradeLine(mission)) / (float)(FinalLine(mission) - UpgradeLine(mission));
        return UnityEngine.Mathf.Clamp(1 + UnityEngine.Mathf.FloorToInt(t * 4), 1, FertilizerGrades);
    }

    // 가습기·난방기를 만들기 전 비닐하우스 습도·온도(행성 바깥 날씨)
    public const float AmbientHumidity = 0.3f;
    public const float AmbientTemperature = 0.3f;

    // 성적 70점 이상이면 로봇 이주 엔딩, 40점 이상이면 다른 행성 엔딩, 그 밑이면 용암 엔딩
    public const int GoodScore = 70;
    public const int OkScore = 40;

    public class Material
    {
        public readonly string id, name;
        public Material(string id, string name) { this.id = id; this.name = name; }
    }

    public class Tool
    {
        public readonly string id, name, materialId;
        public Tool(string id, string name, string materialId) { this.id = id; this.name = name; this.materialId = materialId; }
    }

    public class Blueprint
    {
        public readonly string id, name, effect;
        public readonly (string materialId, int count)[] cost;
        public Blueprint(string id, string name, string effect, params (string, int)[] cost)
        { this.id = id; this.name = name; this.effect = effect; this.cost = cost; }
    }

    // 업그레이드선을 넘을 때마다 순서대로 하나씩 받는 것
    public class Reward
    {
        public readonly string toolId, blueprintId;
        public Reward(string toolId, string blueprintId) { this.toolId = toolId; this.blueprintId = blueprintId; }
    }

    public static readonly Material[] Materials =
    {
        new Material("scrap", "고철"),
        new Material("ore", "광석"),
        new Material("ice", "얼음 결정"),
    };

    public static readonly Tool[] Tools =
    {
        new Tool("cutter", "절단기", "scrap"),
        new Tool("drill", "드릴", "ore"),
        new Tool("pump", "채수기", "ice"),
    };

    public const string Humidifier = "humidifier";
    public const string Heater = "heater";
    public const string AutoWater = "auto_water";
    public const string ExtraPots = "extra_pots";
    public const string Booster = "booster";
    public const string FuelSaver = "fuel_saver";

    // 설계키트로 만드는 것(업그레이드 창 순서 = 숫자 키 1~6)
    public static readonly Blueprint[] Blueprints =
    {
        new Blueprint(Humidifier, "가습기", "비닐하우스에 설치 · 습도를 맞춰 줌", ("scrap", 3)),
        new Blueprint(Heater, "난방기", "비닐하우스에 설치 · 온도를 맞춰 줌", ("scrap", 3), ("ore", 2)),
        new Blueprint(ExtraPots, "밭 확장", "비닐하우스 밭 4칸 추가", ("scrap", 4), ("ore", 2)),
        new Blueprint(AutoWater, "자동 급수기", "수분이 모자라면 꽃이 좋아하는 만큼 알아서 채움", ("scrap", 3)),
        new Blueprint(Booster, "로봇 부스터", "이동 속도 1.4배", ("ore", 3), ("scrap", 2)),
        new Blueprint(FuelSaver, "연료 절약 장치", "연료 소모 30% 줄임", ("ice", 3), ("ore", 2)),
    };

    // 재료를 캘 도구가 설계키트보다 먼저(또는 같이) 오게 짠다
    public static readonly Reward[] Rewards =
    {
        new Reward("cutter", Humidifier),
        new Reward("drill", Heater),
        new Reward(null, ExtraPots),
        new Reward(null, AutoWater),
        new Reward("pump", Booster),
        new Reward(null, FuelSaver),
    };

    public static Material GetMaterial(string id) => Materials.First(m => m.id == id);
    public static Tool GetTool(string id) => Tools.First(t => t.id == id);
    public static Blueprint GetBlueprint(string id) => Blueprints.First(b => b.id == id);
    public static Tool ToolFor(string materialId) => Tools.First(t => t.materialId == materialId);

    // 분 → "오전 6:00" (10분 단위로 끊어서 보여 준다)
    public static string ClockText(float minute)
    {
        int m = (int)minute / 10 * 10 % (24 * 60);
        int h = m / 60;
        string ampm = h < 12 ? "오전" : "오후";
        int h12 = h % 12 == 0 ? 12 : h % 12;
        return $"{ampm} {h12}:{m % 60:00}";
    }
}
