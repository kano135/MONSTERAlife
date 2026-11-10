using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// 씨앗·꽃 가방, 도감, 연료, 도구·재료·설계도·업그레이드, 하루 시계, 엔딩, 저장을 맡는다.
// 핵심 고리: 씨앗 줍기 → 기지에서 키우기 → 수확 → 지구로 보내기 → 연료·도구·설계도 받기
//            → 도구로 재료 채집 → 설계도로 업그레이드. 100일 뒤 지구로 돌아가 성적표와 엔딩.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // shortcut: 수확 100번이면 거주 가능도 100%. 행성 전체 하나로 센다.
    public const int HarvestsForFullHabitability = 100;

    // Sleep = 연료가 바닥나 행성에서 잠듦(중간 종료). 나머지는 100일 뒤 점수로 정해진다.
    // Sleep = 연료가 바닥나 잠듦. 나머지는 이야기(StoryDirector) 95일차 마지막 선택으로 정해진다.
    public enum Ending { None, Sleep, Together, Conquest, Messenger }

    public List<PlantData> Plants { get; private set; }
    public PlantSpot[] Spots { get; private set; }
    public ResourceNode[] Nodes { get; private set; }
    public Transform Player { get; private set; }
    public readonly HashSet<string> Discovered = new HashSet<string>();
    public readonly HashSet<string> VariantsFound = new HashSet<string>();
    public readonly List<HarvestedFlower> FlowerBag = new List<HarvestedFlower>();
    public readonly HashSet<string> Tools = new HashSet<string>();
    public readonly HashSet<string> Blueprints = new HashSet<string>();
    public readonly HashSet<string> Upgrades = new HashSet<string>();
    public int HarvestCount { get; private set; }
    public int FlowersSent { get; private set; }
    public int MissionBonus { get; private set; }   // 최종선을 넘은 점수 합계
    public int LastMissionDay { get; private set; }  // 이미 미션을 낸 발송일
    public int BagPoints => FlowerBag.Sum(f => Progression.FlowerPoints(Plants.Find(p => p.id == f.id), f.variant));
    public float Fuel { get; private set; }
    public int Day { get; private set; } = 1;
    public float Minute { get; private set; } = Progression.DayStartMinute;
    public bool OutOfFuel { get; private set; }
    public Ending Result { get; private set; }
    public bool IsGameOver => Result != Ending.None;
    public float Habitability => Mathf.Clamp01(HarvestCount / (float)HarvestsForFullHabitability);
    // 이야기 상태(StoryDirector): 원주민 신뢰(-100~100, 화면에 안 보임), 이미 본 날, 아이 꽃을 받았나, 마지막 선택(1 정복·2 메신저·3 함께 잠들기)
    public int StoryTrust;
    public readonly HashSet<int> StorySeen = new HashSet<int>();
    public bool StoryChildFlower;
    public int StoryFinal;

    // 첫날 할 일(Objectives)용 횟수: 심기·물 주기·길 씨앗 줍기·작은 돌 줍기, 원주민 마을에 가 봤나, 받은 보상
    public int CountPlanted, CountWatered, CountSeedsPicked, CountPebbles;
    public bool VisitedVillage;
    public readonly HashSet<int> ObjectivesDone = new HashSet<int>();
    public bool TestSendDone;
    public bool IsTestSendDay => !TestSendDone && Day >= Progression.TestSendDay && Day < Progression.SendEveryDays;

    // 가진 물건의 칸 순서(아래 칸 1~9 + 가방). 손에 든 씨앗으로 심는다.
    public Inventory Inv { get; private set; }

    // 물뿌리개: 레벨(1~4), 남은 물(횟수), 미션으로 받은 강화권
    public int CanLevel { get; private set; } = 1;
    public int CanWater { get; private set; }
    public int CanTokens { get; private set; }
    public int CanCapacity => Progression.CanCapacity[CanLevel - 1];
    public bool CanMaxed => CanLevel >= Progression.CanMaxLevel;
    // 물을 줄 때마다(손에 든 물뿌리개 기울이기 등)
    public event Action Poured;

    readonly Dictionary<string, int> seeds = new Dictionary<string, int>();
    readonly Dictionary<string, int> materials = new Dictionary<string, int>();
    int[] fertilizer = new int[Progression.FertilizerGrades];
    int rewardsGiven;

    string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    public void Init(List<PlantData> plants, PlantSpot[] spots, ResourceNode[] nodes, Transform player)
    {
        Instance = this;
        Plants = plants;
        Spots = spots;
        Nodes = nodes;
        Player = player;
        Inv = new Inventory(this);
        Load();
        ApplyUpgrades();
    }

    public bool HasUpgrade(string id) => Upgrades.Contains(id);
    public int SeedCount(PlantData p) => seeds.TryGetValue(p.id, out var n) ? n : 0;
    public void AddSeed(PlantData p, int n) => seeds[p.id] = SeedCount(p) + n;
    public int MaterialCount(string id) => materials.TryGetValue(id, out var n) ? n : 0;
    public void AddMaterial(string id, int n) => materials[id] = MaterialCount(id) + n;

    // 비료 1~5단계 가진 개수
    public int FertilizerCount(int grade) => fertilizer[grade - 1];
    public void AddFertilizer(int grade, int n) => fertilizer[grade - 1] += n;

    // 밭에서 F: 손에 든 비료가 있으면 그 단계, 아니면 가진 비료 중 가장 높은 단계를 준다
    public string Fertilize(PlantSpot spot)
    {
        if (!spot.CanFertilize) return spot.IsEmpty ? "씨앗을 심은 뒤에 비료를 줄 수 있어요" : "자라는 중인 꽃에만 비료를 줄 수 있어요";
        int grade = Inv.HeldFertilizer > 0 && FertilizerCount(Inv.HeldFertilizer) > 0 ? Inv.HeldFertilizer : 0;
        for (int g = Progression.FertilizerGrades; g >= 1 && grade == 0; g--)
            if (fertilizer[g - 1] > 0) grade = g;
        if (grade == 0) return "비료가 없어요. 미션 업그레이드선을 넘으면 지구가 보내 줘요";
        if (grade <= spot.FertilizerGrade) return $"이미 비료 {spot.FertilizerGrade}단계를 줬어요";
        fertilizer[grade - 1]--;
        spot.Fertilize(grade);
        return $"비료 {grade}단계를 줬어요! 수확할 때까지 ×{Progression.FertilizerBoost(grade):0.##} 빨리 자라요";
    }

    // 밭에서 E: 비었으면 심고, 썩었으면 치우고, 피었으면 수확하고, 자라는 중이면 물 준다. 화면에 띄울 말을 돌려준다.
    public string Interact(PlantSpot spot)
    {
        if (spot.IsEmpty)
        {
            var p = Inv.HeldPlant;
            if (!p || SeedCount(p) <= 0) return "씨앗을 손에 들어야 심어요 (숫자 키 1~9 · Esc 가방)";
            seeds[p.id]--;
            spot.PlantSeed(p);
            CountPlanted++;
            return $"{p.displayName} 씨앗을 심었어요";
        }
        if (spot.IsRotten)
        {
            spot.Clear();
            return "썩은 꽃을 치웠어요";
        }
        if (spot.IsBloomed)
        {
            var p = spot.Plant;
            bool variant = spot.IsVariant;
            bool isNew = Discovered.Add(p.id);
            if (variant) isNew |= VariantsFound.Add(p.id);
            FlowerBag.Add(new HarvestedFlower { id = p.id, variant = variant });
            HarvestCount++;
            AddSeed(p, 1);
            spot.Clear();
            string name = variant ? p.variantName : p.displayName;
            return (isNew ? $"새로 발견! {name} 도감 등록. " : $"{name} 수확. ") + "송신기로 지구에 보내세요";
        }
        if (!Inv.HeldCan) return Inv.HeldPlant
            ? "여기는 이미 심겨 있어요. 빈 밭(둔덕 없는 칸)에 심어요"
            : "물을 주려면 물뿌리개를 손에 들어요 (숫자 키 · Esc 가방)";
        if (CanWater <= 0) return "물뿌리개가 비었어요! 기지 남서쪽 호수에서 E로 채우세요";
        CanWater--;
        CountWatered++;
        Poured?.Invoke();
        if (spot.AddWater(Progression.WaterPerPour)) return $"물을 너무 많이 줘서 꽃이 썩었어요… (물뿌리개 {CanWater}/{CanCapacity})";
        return $"물을 줬어요 (수분 {spot.Water:P0} · 물뿌리개 {CanWater}/{CanCapacity})" + (CanWater == 0 ? "\n물이 떨어졌어요. 호수에서 채우세요" : "");
    }

    public void AddFuel(float amount) => Fuel = Mathf.Min(Progression.MaxFuel, Fuel + amount);

    // 가방에서 가장 귀한 꽃 한 송이를 뺀다(이야기: 아픈 원주민에게 주기)
    public void RemoveRarestFlower()
    {
        if (FlowerBag.Count == 0) return;
        var rarest = FlowerBag.OrderByDescending(f => Progression.FlowerPoints(Plants.Find(p => p.id == f.id), f.variant)).First();
        FlowerBag.Remove(rarest);
    }

    // 이야기로 바로 끝내기(함께 잠들기): 눈이 감기고 엔딩 화면
    public void EndStory(Ending ending)
    {
        if (IsGameOver) return;
        StartCoroutine(EndStoryRoutine(ending));
    }

    System.Collections.IEnumerator EndStoryRoutine(Ending ending)
    {
        Sleeping = true;
        var lids = Eyelids.Instance;
        if (lids) yield return lids.Close(2.5f, "송신기를 껐어요…\nZzz…");
        Result = ending;
        if (lids) { yield return new WaitForSecondsRealtime(1.5f); lids.Hide(); }
        Sleeping = false;
    }

    // 테스트용 연료통을 들고 E: 연료 MissionFuel(5일치)만큼, 최대치까지
    public string UseFuelCan()
    {
        if (Fuel >= Progression.MaxFuel) return $"연료가 이미 가득 찼어요 ({Fuel / Progression.FuelPerDay:0.#}일치)";
        Fuel = Mathf.Min(Progression.MaxFuel, Fuel + Progression.MissionFuel);
        return $"연료통을 썼어요! 연료 {Fuel / Progression.FuelPerDay:0.#}일치 (테스트용 무제한)";
    }

    // 호수에서 E: 물뿌리개를 가득 채운다
    public string RefillCan()
    {
        if (CanWater >= CanCapacity) return $"물뿌리개가 이미 가득 찼어요 ({CanWater}/{CanCapacity})";
        CanWater = CanCapacity;
        return $"물뿌리개를 가득 채웠어요! ({CanWater}/{CanCapacity})";
    }

    // 업그레이드 창에서 물뿌리개 강화: 강화권으로 사용 횟수를 늘리고, 늘어난 만큼 바로 채워 준다
    public string UpgradeCan()
    {
        if (CanMaxed) return $"물뿌리개가 이미 최고 단계예요 (Lv{CanLevel} · {CanCapacity}회)";
        if (CanTokens < Progression.CanUpgradeCost) return "강화권이 없어요. 미션 생존선을 넘으면 1장씩 받아요";
        CanTokens -= Progression.CanUpgradeCost;
        CanLevel++;
        CanWater = CanCapacity;
        return $"물뿌리개 Lv{CanLevel} 강화 완료! 이제 {CanCapacity}번 쓸 수 있어요";
    }

    // 채집 자원에서 E: 맞는 도구가 있어야 캔다
    public string Gather(ResourceNode node)
    {
        var mat = Progression.GetMaterial(node.materialId);
        var tool = Progression.ToolFor(node.materialId);
        if (node.needsTool && !Tools.Contains(tool.id)) return $"{mat.name}을(를) 캐려면 {tool.name}이(가) 필요해요. 지구에 꽃을 보내면 받을 수 있어요";
        if (node.Amount <= 0) return "다 캤어요. 내일 다시 생겨요";
        node.Take();
        AddMaterial(mat.id, 1);
        if (!node.needsTool) CountPebbles++;
        return $"{mat.name} {(node.needsTool ? "+1" : "1개 주웠어요")} (가진 것 {MaterialCount(mat.id)})";
    }

    // 송신기에서 E: 발송일(5일마다)에 한 번, 가방의 꽃을 전부 보내 미션을 판정한다(기준선은 Progression).
    public string SendToEarth()
    {
        if (IsTestSendDay)
        {
            if (FlowerBag.Count == 0) return "시험 발송: 꽃을 한 송이 이상 수확해서 가져와요";
            StoryDirector.OnFlowersSent(this, FlowerBag.Count);
            FlowersSent += FlowerBag.Count;
            FlowerBag.Clear();
            TestSendDone = true;
            var tool = Progression.GetTool(Progression.TestSendTool);
            Tools.Add(tool.id);
            return $"시험 발송 성공! 지구가 '{tool.name}'을(를) 보내 줬어요.\n이제 나무를 캘 수 있어요(나무 앞에서 E)";
        }
        if (!Progression.IsSendDay(Day))
            return $"지구로는 {Progression.SendEveryDays}일마다 보낼 수 있어요. 다음 발송일: {Progression.NextSendDay(Day)}일차";
        if (LastMissionDay == Day) return "오늘 미션은 이미 끝났어요";
        if (FlowerBag.Count == 0) return "보낼 꽃이 없어요";

        int mission = Progression.MissionNumber(Day);
        int points = BagPoints;
        StoryDirector.OnFlowersSent(this, FlowerBag.Count);
        FlowersSent += FlowerBag.Count;
        FlowerBag.Clear();
        LastMissionDay = Day;

        if (points < Progression.SurviveLine(mission))
            return $"미션 {mission} 실패… {points}점 / 생존선 {Progression.SurviveLine(mission)}점. 연료통을 못 받았어요";

        Fuel = Mathf.Min(Progression.MaxFuel, Fuel + Progression.MissionFuel);
        CanTokens++;
        string msg = $"미션 {mission} 성공! {points}점 → 연료통 +{Progression.MissionFuel:0} · 물뿌리개 강화권 +1";

        if (points >= Progression.UpgradeLine(mission))
        {
            var gifts = new List<string>();
            if (rewardsGiven < Progression.Rewards.Length)
            {
                var r = Progression.Rewards[rewardsGiven++];
                if (r.toolId != null && Tools.Add(r.toolId)) gifts.Add($"도구 '{Progression.GetTool(r.toolId).name}'");
                if (r.blueprintId != null && Blueprints.Add(r.blueprintId)) gifts.Add($"설계키트 '{Progression.GetBlueprint(r.blueprintId).name}'");
            }
            // 비료는 설계키트를 다 받은 뒤에도 업그레이드선을 넘으면 계속 온다
            int grade = Progression.FertilizerGradeFor(points, mission);
            fertilizer[grade - 1] += Progression.FertilizerPerReward;
            gifts.Add($"비료 {grade}단계 ×{Progression.FertilizerPerReward}");
            msg += "\n업그레이드선 통과: " + string.Join(", ", gifts);
        }

        int bonus = points - Progression.FinalLine(mission);
        if (bonus > 0)
        {
            MissionBonus += bonus;
            msg += $"\n최종선 통과! 성적 +{bonus}";
        }
        return msg;
    }

    // 업그레이드 창에서 만들기
    public string Craft(Progression.Blueprint bp)
    {
        if (!Blueprints.Contains(bp.id)) return "설계도가 없어요";
        if (Upgrades.Contains(bp.id)) return "이미 만들었어요";
        var missing = bp.cost.Where(c => MaterialCount(c.materialId) < c.count).ToList();
        if (missing.Count > 0)
            return "재료가 모자라요: " + string.Join(", ", missing.Select(c => $"{Progression.GetMaterial(c.materialId).name} {c.count - MaterialCount(c.materialId)}개 더"));
        foreach (var c in bp.cost) materials[c.materialId] -= c.count;
        Upgrades.Add(bp.id);
        ApplyUpgrades();
        return $"{bp.name} 완성! {bp.effect}";
    }

    void ApplyUpgrades()
    {
        foreach (var s in Spots.Where(s => s.locked)) s.gameObject.SetActive(HasUpgrade(Progression.ExtraPots));
    }

    void Update()
    {
        Inv.Sync(); // 새로 얻은 물건은 빈 칸에, 다 쓴 물건 칸은 비운다
        if (IsGameOver || Sleeping) return;
        Minute += 10f / Progression.RealSecondsPer10Minutes * Time.deltaTime;
        if (Minute >= Progression.DayEndMinute) Sleep(passedOut: true);
    }

    // 잠자는 중(눈 감기 → 하루 넘김 → 눈 뜨기). 이 동안 시계·로봇이 멈춘다(Hud.AnyPanelOpen).
    public bool Sleeping { get; private set; }

    // 잠들기: 새벽 2시면 어디에 있든 그 자리에서 쓰러져 잠든다(passedOut). 충전기에서 Q 도 같은 잠.
    // 눈이 감기고 → 하루가 넘어가며 연료 하루치가 빠지고 → 그 자리에서 눈을 뜬다.
    // 연료(15일치)가 바닥나면 눈을 감은 채 잠들고, 그다음 게임오버(잠드는 엔딩) 화면이 뜬다.
    public void Sleep(bool passedOut)
    {
        if (Sleeping || IsGameOver) return;
        StartCoroutine(SleepRoutine(passedOut));
    }

    System.Collections.IEnumerator SleepRoutine(bool passedOut)
    {
        Sleeping = true;
        var lids = Eyelids.Instance;
        string text = passedOut ? "새벽 2시… 배터리가 다 됐어요\nZzz…" : "충전하며 쉬는 중… Zzz";
        if (lids) yield return lids.Close(passedOut ? 2.2f : 1.2f, text);
        string msg = EndDay(passedOut);
        if (lids) yield return new WaitForSecondsRealtime(1.3f);
        if (IsGameOver)
        {
            // 연료 바닥: 눈을 뜨지 못하고 그대로 잠든다 → 엔딩 화면
            if (lids) { yield return new WaitForSecondsRealtime(1.2f); lids.Hide(); }
            Sleeping = false;
            yield break;
        }
        if (lids) yield return lids.Open(1.2f);
        Sleeping = false;
        Player.GetComponent<PlayerController>().Show(msg, 4f);
    }

    // 하루 끝(잠에서 깰 때): 연료 하루치를 쓰고 다음 날 아침으로. 로봇은 잠든 자리 그대로.
    public string EndDay(bool passedOut)
    {
        if (Day < Progression.TotalDays)
        {
            Fuel -= Progression.FuelPerDay * (HasUpgrade(Progression.FuelSaver) ? 0.7f : 1f);
            if (Fuel <= 0)
            {
                Fuel = 0;
                OutOfFuel = true;
                Result = Ending.Sleep;
                return "연료가 바닥났어요";
            }
        }
        Day++;
        Minute = Progression.DayStartMinute;
        foreach (var n in Nodes) n.Refill();

        if (Day > Progression.TotalDays)
        {
            Day = Progression.TotalDays;
            Result = StoryFinal == 2 ? Ending.Messenger : Ending.Conquest; // 마지막 선택(메신저가 아니면 정복)
            return "100일이 지났어요. 지구로 돌아갑니다";
        }
        Save();
        float days = Fuel / Progression.FuelPerDay;
        return passedOut
            ? $"쓰러졌던 자리에서 깼어요. {Day}일차 아침 · 연료 {days:0.#}일 남음"
            : $"충전 완료. {Day}일차 아침이에요 · 연료 {days:0.#}일 남음";
    }

    // 성적표 (100점 만점)
    public int HabitabilityScore => Mathf.RoundToInt(Habitability * 40);
    public int BookScore => Mathf.RoundToInt(Discovered.Count / (float)Plants.Count * 20);
    public int VariantScore => Mathf.RoundToInt(VariantsFound.Count / (float)Plants.Count * 20);
    public int MissionScore => Mathf.RoundToInt(Mathf.Clamp01(MissionBonus / (float)Progression.BonusForFullMissionScore) * 20);
    public int Score => HabitabilityScore + BookScore + VariantScore + MissionScore;

    public void Restart()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnApplicationPause(bool paused) { if (paused) Save(); }
    void OnApplicationQuit() => Save();

    public void Save()
    {
        // 끝난 판은 남기지 않는다: 다음에 켜면 새 판
        if (IsGameOver)
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
            return;
        }
        var save = new GameSave
        {
            seeds = seeds.Select(kv => new CountEntry { id = kv.Key, count = kv.Value }).ToList(),
            materials = materials.Select(kv => new CountEntry { id = kv.Key, count = kv.Value }).ToList(),
            discovered = Discovered.ToList(),
            variants = VariantsFound.ToList(),
            tools = Tools.ToList(),
            blueprints = Blueprints.ToList(),
            upgrades = Upgrades.ToList(),
            flowerBag = FlowerBag.ToList(),
            harvestCount = HarvestCount,
            flowersSent = FlowersSent,
            missionBonus = MissionBonus,
            lastMissionDay = LastMissionDay,
            rewardsGiven = rewardsGiven,
            fertilizer = fertilizer.ToArray(),
            inventory = Inv.ToSave(),
            storyTrust = StoryTrust,
            storySeen = StorySeen.ToList(),
            storyChildFlower = StoryChildFlower,
            storyFinal = StoryFinal,
            counts = new[] { CountPlanted, CountWatered, CountSeedsPicked, CountPebbles },
            visitedVillage = VisitedVillage,
            objectivesDone = ObjectivesDone.ToList(),
            testSendDone = TestSendDone,
            hasPosition = true,
            position = Player.position,
            yaw = Player.eulerAngles.y,
            heldSlot = Inv.Selected,
            canLevel = CanLevel,
            canWater = CanWater,
            canTokens = CanTokens,
            fuel = Fuel,
            day = Day,
            minute = Minute,
            spots = Spots.Select(s => s.ToSave()).ToList(),
            nodeAmounts = Nodes.Select(n => n.Amount).ToList(),
            greenhouse = Greenhouse.Instance ? Greenhouse.Instance.ToSave() : null,
        };
        File.WriteAllText(SavePath, JsonUtility.ToJson(save, true));
    }

    // 게임을 꺼 두면 시간도 멈춘다(스타듀밸리처럼). 꽃도 연료도 그대로.
    void Load()
    {
        GameSave save = null;
        try
        {
            if (File.Exists(SavePath)) save = JsonUtility.FromJson<GameSave>(File.ReadAllText(SavePath));
        }
        catch (Exception e)
        {
            // 망가진 저장은 지우지 않고 옆에 남겨 둔다
            Debug.LogWarning($"저장 파일을 못 읽어 새로 시작합니다: {e.Message}");
            File.Copy(SavePath, SavePath + ".broken", true);
        }

        if (save == null)
        {
            Fuel = Progression.StartFuel;
            CanWater = CanCapacity;
            AddSeed(Plants[0], 3);
            if (Plants.Count > 1) AddSeed(Plants[1], 2);
            Inv.Sync();
            return;
        }

        foreach (var s in save.seeds) seeds[s.id] = s.count;
        foreach (var m in save.materials) materials[m.id] = m.count;
        Discovered.UnionWith(save.discovered);
        VariantsFound.UnionWith(save.variants);
        Tools.UnionWith(save.tools);
        Blueprints.UnionWith(save.blueprints);
        Upgrades.UnionWith(save.upgrades);
        FlowerBag.AddRange(save.flowerBag);
        HarvestCount = save.harvestCount;
        FlowersSent = save.flowersSent;
        MissionBonus = save.missionBonus;
        LastMissionDay = save.lastMissionDay;
        rewardsGiven = save.rewardsGiven;
        if (save.fertilizer != null && save.fertilizer.Length == Progression.FertilizerGrades) fertilizer = save.fertilizer;
        // canLevel 0 = 물뿌리개가 생기기 전 옛 저장 → Lv1 가득 찬 걸로 시작
        CanLevel = Mathf.Clamp(save.canLevel, 1, Progression.CanMaxLevel);
        CanWater = save.canLevel == 0 ? CanCapacity : Mathf.Clamp(save.canWater, 0, CanCapacity);
        CanTokens = Mathf.Max(0, save.canTokens);
        // 연료 0 저장은 없다(끝난 판은 지움). 0이면 연료가 생기기 전 옛 저장이다.
        Fuel = save.fuel > 0 ? save.fuel : Progression.StartFuel;
        Day = Mathf.Max(1, save.day);
        Minute = save.minute >= Progression.DayStartMinute ? save.minute : Progression.DayStartMinute;

        for (int i = 0; i < Spots.Length && i < save.spots.Count; i++)
            Spots[i].FromSave(save.spots[i], Plants.Find(p => p.id == save.spots[i].plantId));
        // 채집 자원 수가 바뀐 옛 저장(예: 보라 바위 → 돌멩이)이면 순서가 안 맞으니 오늘 양은 가득 찬 채로 둔다
        if (save.nodeAmounts.Count == Nodes.Length)
            for (int i = 0; i < Nodes.Length; i++)
                Nodes[i].Amount = Mathf.Clamp(save.nodeAmounts[i], 0, Nodes[i].maxAmount);
        if (Greenhouse.Instance) Greenhouse.Instance.FromSave(save.greenhouse);
        Inv.FromSave(save.inventory, save.heldSlot); // 옛 저장(칸 순서 없음)이면 가진 것부터 차례로 채운다
        StoryTrust = save.storyTrust;
        if (save.storySeen != null) StorySeen.UnionWith(save.storySeen);
        StoryChildFlower = save.storyChildFlower;
        StoryFinal = save.storyFinal;
        if (save.counts != null && save.counts.Length == 4) { CountPlanted = save.counts[0]; CountWatered = save.counts[1]; CountSeedsPicked = save.counts[2]; CountPebbles = save.counts[3]; }
        VisitedVillage = save.visitedVillage;
        if (save.objectivesDone != null) ObjectivesDone.UnionWith(save.objectivesDone);
        TestSendDone = save.testSendDone || Day >= Progression.SendEveryDays; // 옛 저장(이미 5일차 넘음)이면 시험 발송은 건너뜀
        if (save.hasPosition)
        {
            var cc = Player.GetComponent<CharacterController>();
            cc.enabled = false; // 순간이동하려면 잠깐 꺼야 한다
            Player.SetPositionAndRotation(save.position + Vector3.up * 0.1f, Quaternion.Euler(0, save.yaw, 0));
            cc.enabled = true;
        }
    }

    [Serializable]
    public class HarvestedFlower
    {
        public string id;
        public bool variant;
    }

    [Serializable]
    class GameSave
    {
        public List<CountEntry> seeds = new List<CountEntry>();
        public List<CountEntry> materials = new List<CountEntry>();
        public List<string> discovered = new List<string>();
        public List<string> variants = new List<string>();
        public List<string> tools = new List<string>();
        public List<string> blueprints = new List<string>();
        public List<string> upgrades = new List<string>();
        public List<HarvestedFlower> flowerBag = new List<HarvestedFlower>();
        public int harvestCount, flowersSent, day, missionBonus, lastMissionDay, rewardsGiven;
        public int canLevel, canWater, canTokens;
        public int[] fertilizer;
        public List<string> inventory;
        public int storyTrust, storyFinal;
        public List<int> storySeen = new List<int>();
        public bool storyChildFlower;
        public int[] counts;
        public bool visitedVillage, testSendDone;
        public List<int> objectivesDone = new List<int>();
        // 로봇 자리: 이어 하면 저장한 그 자리에서 시작(새 게임은 기지 안)
        public bool hasPosition;
        public Vector3 position;
        public float yaw;
        public int heldSlot;
        public float fuel, minute;
        public List<SpotSave> spots = new List<SpotSave>();
        public List<int> nodeAmounts = new List<int>();
        public GreenhouseSave greenhouse;
    }

    [Serializable]
    class CountEntry
    {
        public string id;
        public int count;
    }
}
