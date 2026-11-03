using UnityEngine;

// 식물을 심는 밭 한 칸(비닐하우스 안 땅). 햇빛·온도는 비닐하우스 장치(조명·난방기)가 정하고,
// 수분은 플레이어가 물을 줘서 채운다(가습기 습도가 높으면 덜 마른다).
// 방치해도 죽지 않고 느리게 자라며, 선호 환경을 맞추면 빨리 자라고 변종이 된다.
// 수분이 이미 100%인데 물을 또 주면 썩는다.
public class PlantSpot : MonoBehaviour
{
    public const int BloomStage = 4;
    public const float SoilTop = 0.25f;
    static readonly Color RottenColor = new Color(0.35f, 0.25f, 0.15f);

    [Tooltip("비닐하우스 밖에 둘 때만 쓰는 햇빛·온도")]
    [Range(0, 1)] public float sunlight = 0.8f;
    [Range(0, 1)] public float temperature = 0.6f;
    [Tooltip("밭 확장 업그레이드 전에는 숨겨 둔다")]
    public bool locked;

    public PlantData Plant { get; private set; }
    public float Water { get; private set; }
    public float Progress { get; private set; } // 0~4, 정수 부분이 성장 단계
    public bool IsVariant { get; private set; }
    public bool IsRotten { get; private set; }
    public int FertilizerGrade { get; private set; } // 0 = 안 줌, 1~5단계
    public bool CanFertilize => Plant != null && !IsRotten && !IsBloomed;
    public int Stage => Mathf.Min(BloomStage, Mathf.FloorToInt(Progress));
    public bool IsEmpty => Plant == null;
    public bool IsBloomed => Plant != null && !IsRotten && Progress >= BloomStage;

    // 비닐하우스가 있으면 그 안 환경을 따른다
    public float Sun => Greenhouse.Instance ? Greenhouse.Instance.Light : sunlight;
    public float Temp => Greenhouse.Instance ? Greenhouse.Instance.Temperature : temperature;

    float perfectTime, totalTime;
    int shownStage = -1;
    Transform visual;

    public void PlantSeed(PlantData data)
    {
        Plant = data;
        Water = 0.5f;
        Progress = 0;
        perfectTime = totalTime = 0;
        IsVariant = IsRotten = false;
        FertilizerGrade = 0;
        Refresh();
    }

    // 비료는 수확할 때까지 효과가 간다. 더 높은 단계로만 바꿔 줄 수 있다.
    public void Fertilize(int grade) => FertilizerGrade = Mathf.Max(FertilizerGrade, grade);

    // 썩었으면 true
    public bool AddWater(float amount)
    {
        if (Water >= 1f)
        {
            IsRotten = true;
            Refresh();
            return true;
        }
        Water = Mathf.Min(1f, Water + amount);
        return false;
    }

    public void Clear()
    {
        Plant = null;
        IsRotten = false;
        FertilizerGrade = 0;
        Refresh();
    }

    // 선호 범위 안에 든 환경 요소 개수(0~3)
    public int InRangeCount()
    {
        if (Plant == null) return 0;
        return (In(Water, Plant.waterRange) ? 1 : 0)
             + (In(Sun, Plant.sunRange) ? 1 : 0)
             + (In(Temp, Plant.tempRange) ? 1 : 0);
    }

    // 0개 맞으면 0.5배, 3개 다 맞으면 1.5배
    public static float GrowthRate(int inRange) => 0.5f + inRange / 3f;

    static bool In(float v, Vector2 range) => v >= range.x && v <= range.y;

    void Update()
    {
        if (Plant == null || IsRotten || IsBloomed) return;
        Tick(Time.deltaTime);
    }

    void Tick(float dt)
    {
        int inRange = InRangeCount();
        float boost = FertilizerGrade > 0 ? Progression.FertilizerBoost(FertilizerGrade) : 1f;
        Progress = Mathf.Min(BloomStage, Progress + dt / Plant.secondsPerStage * GrowthRate(inRange) * boost);
        // 습도 0% → 1.5배 빨리 마름, 100% → 0.5배
        float dry = Greenhouse.Instance ? Mathf.Lerp(1.5f, 0.5f, Greenhouse.Instance.Humidity) : 1f;
        Water = Mathf.Max(0, Water - Plant.waterUsePerSecond * dry * dt);
        if (Water < Plant.waterRange.x && GameManager.Instance.HasUpgrade(Progression.AutoWater))
            Water = (Plant.waterRange.x + Plant.waterRange.y) / 2;
        totalTime += dt;
        if (inRange == 3) perfectTime += dt;
        if (IsBloomed) IsVariant = !string.IsNullOrEmpty(Plant.variantName) && perfectTime >= totalTime * 0.8f;
        if (Stage != shownStage) Refresh();
    }

    public SpotSave ToSave() => new SpotSave
    {
        plantId = Plant ? Plant.id : "", progress = Progress, water = Water,
        perfectTime = perfectTime, totalTime = totalTime, variant = IsVariant, rotten = IsRotten,
        fertilizer = FertilizerGrade,
    };

    public void FromSave(SpotSave s, PlantData data)
    {
        Plant = data;
        Progress = s.progress;
        Water = s.water;
        perfectTime = s.perfectTime;
        totalTime = s.totalTime;
        IsVariant = s.variant;
        IsRotten = s.rotten;
        FertilizerGrade = s.fertilizer;
        Refresh();
    }

    void Start() => Refresh();

    // 보이는 모습 고르기: 3D 모델(stagePrefabs) → 땅에 심은 GPT 그림(화분을 잘라 낸 _ground) → 임시 도형 순서
    void Refresh()
    {
        if (visual) Destroy(visual.gameObject);
        shownStage = Plant ? Stage : -1;
        if (Plant == null) return;

        var prefab = !IsRotten && Plant.stagePrefabs != null && Plant.stagePrefabs.Length > Stage ? Plant.stagePrefabs[Stage] : null;
        var sprite = IsBloomed && IsVariant ? ArtLibrary.PlantVariantGround(Plant) ?? ArtLibrary.PlantStageGround(Plant, Stage) : ArtLibrary.PlantStageGround(Plant, Stage);
        if (prefab)
        {
            visual = Instantiate(prefab, transform).transform;
            visual.localPosition = Vector3.up * SoilTop;
        }
        else if (sprite)
        {
            visual = Billboard.Create(transform, sprite, SpriteWidth, IsRotten ? RottenTint : Color.white).transform;
            visual.localPosition = Vector3.up * (SoilTop - 0.14f); // 그림 밑에 남은 화분 테두리를 흙 속에 묻는다
        }
        else
        {
            // shortcut: 썩은 모습은 색만 바꾼 임시 모습. 썩은 그림이 생기면 그걸 쓴다.
            var color = IsRotten ? RottenColor : IsVariant ? Plant.variantColor : Plant.color;
            visual = PlaceholderPlant.Build(transform, Stage, color, IsRotten);
            visual.localPosition = Vector3.up * SoilTop;
        }
    }

    const float SpriteWidth = 1.5f;
    static readonly Color RottenTint = new Color(0.55f, 0.4f, 0.3f);
}

[System.Serializable]
public class SpotSave
{
    public string plantId;
    public float progress, water, perfectTime, totalTime;
    public bool variant, rotten;
    public int fertilizer;
}
