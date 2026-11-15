using System;
using UnityEngine;

// 첫날 할 일: Esc 메뉴 '미션' 탭에서 본다(화면에는 안 띄운다). 하나씩 끝낼 때마다 작은 보상과 알림.
// ★ 아래 Goals 표만 고치면 할 일·목표 수·보상이 바뀐다.
public class Objectives : MonoBehaviour
{
    struct Goal
    {
        public string text;
        public Func<GameManager, int> progress;
        public int target;
        public string rewardText;
        public Action<GameManager> reward;
    }

    public static int GoalCount => Goals.Length;

    static readonly Goal[] Goals =
    {
        new Goal { text = "비닐하우스 밭에 씨앗 3개 심기", progress = gm => gm.CountPlanted, target = 3,
            rewardText = "비료 1단계 ×1", reward = gm => gm.AddFertilizer(1, 1) },
        new Goal { text = "물뿌리개로 물 주기", progress = gm => gm.CountWatered, target = 1,
            rewardText = "물뿌리개 가득", reward = gm => gm.RefillCan() },
        new Goal { text = "길에서 씨앗 2개 줍기", progress = gm => gm.CountSeedsPicked, target = 2,
            rewardText = "씨앗 1개", reward = gm => gm.AddSeed(gm.Plants[Mathf.Min(2, gm.Plants.Count - 1)], 1) },
        new Goal { text = "작은 돌멩이 3개 줍기", progress = gm => gm.CountPebbles, target = 3,
            rewardText = "연료 +1일", reward = gm => gm.AddFuel(Progression.FuelPerDay) },
        new Goal { text = "첫 꽃 수확하기", progress = gm => gm.HarvestCount, target = 1,
            rewardText = "비료 1단계 ×1", reward = gm => gm.AddFertilizer(1, 1) },
        new Goal { text = "동쪽 원주민 마을 가 보기", progress = gm => gm.VisitedVillage ? 1 : 0, target = 1,
            rewardText = "", reward = gm => { } },
    };

    GameManager gm;
    PlayerController player;

    // Esc 메뉴 '미션' 탭에 보여 줄 글(화면에는 따로 띄우지 않는다)
    public static string Summary { get; private set; } = "";

    void Start()
    {
        gm = GameManager.Instance;
        player = gm.Player.GetComponent<PlayerController>();
    }

    void Update()
    {
        if (!gm || gm.IsGameOver) return;
        // 원주민 마을 가 보기
        if (!gm.VisitedVillage)
        {
            var d = gm.Player.position - Village.Center; d.y = 0;
            if (d.magnitude < Village.Radius + 6f) gm.VisitedVillage = true;
        }

        var lines = new System.Text.StringBuilder();
        for (int i = 0; i < Goals.Length; i++)
        {
            var g = Goals[i];
            int now = Mathf.Min(g.progress(gm), g.target);
            bool done = gm.ObjectivesDone.Contains(i);
            if (!done && now >= g.target)
            {
                gm.ObjectivesDone.Add(i);
                done = true;
                g.reward(gm);
                player.Show(g.rewardText == "" ? $"할 일 완료: {g.text}" : $"할 일 완료: {g.text}\n보상: {g.rewardText}", 3.5f);
            }
            string count = g.target > 1 ? $" ({now}/{g.target})" : "";
            lines.Append(done ? $"<color=#488C3E>● {g.text}</color>\n" : $"○ {g.text}{count}\n");
        }
        if (gm.ObjectivesDone.Count >= Goals.Length)
            lines.Append("<color=#488C3E>다 했어요! 이제 자유롭게 탐험해요</color>");
        Summary = lines.ToString();
    }
}
