using System;
using System.Linq;
using UnityEngine;

// 이야기 진행(형식 확인용 1차). 날짜가 되면 아침에 이야기 카드가 뜨고, 몇몇 날은 선택지를 고른다.
// 고른 것에 따라 보이지 않는 '신뢰'(원주민이 로봇을 얼마나 믿는지, -100~100)가 오르내린다.
// 95일차 마지막 선택으로 결말이 갈린다:
//   ① 함께 잠들기 — 송신기를 끄고 원주민 곁에서 잠든다(바로 끝)
//   ② 정복 — 끝까지 꽃을 보낸다 → 100일차 지구 함대, 원주민은 터전을 잃는다
//   ③ 메신저 — 신뢰 50 이상일 때만: 꽃 대신 원주민의 이야기를 보낸다 → 100일차 지구가 손님으로 온다
// 연료가 바닥나면(어느 날이든) 잠드는 엔딩.
// ★ 아래 Beats 표만 고치면 날짜·글·선택지·신뢰 변화가 바뀐다.
public class StoryDirector : MonoBehaviour
{
    public static StoryDirector Instance { get; private set; }

    static readonly Color Earth = new Color32(70, 120, 200, 255);   // 지구 통신
    static readonly Color Planet = new Color32(72, 140, 62, 255);   // 행성·원주민

    public const int MessengerTrust = 50;

    // 하루치 이야기: 날짜, 제목, 글, 선택지(없으면 '계속'), 고른 뒤 할 일
    struct Beat
    {
        public int day; public string title, body; public Color accent;
        public Func<GameManager, string[]> options;       // null = 계속 카드
        public Action<GameManager, int> onPick;            // 고른 번호
        public Func<GameManager, bool> when;               // 이 조건일 때만(없으면 늘)
    }

    static readonly Beat[] Beats =
    {
        new Beat { day = 2, accent = Earth, title = "📡 지구에서 온 통신",
            body = "\"첫 수확을 시험 발송하라. 성공하면 채집 도구(도끼)를 보낸다.\"\n\n(꽃을 한 송이 수확해서 기지 송신기에서 E)",
            when = gm => !gm.TestSendDone },
        new Beat { day = 3, accent = Planet, title = "누군가 지켜보고 있다",
            body = "들판 끝에서 무언가가 로봇을 지켜보고 있어요.\n이 행성에는… 누군가 살고 있었어요.\n(동쪽에 원주민 마을이 있어요)" },
        new Beat { day = 5, accent = Planet, title = "첫 발송 날",
            body = "오늘은 지구로 꽃을 보내는 날이에요.\n송신기 빛을 본 원주민들이 겁에 질려 숨었어요." },
        new Beat { day = 10, accent = Planet, title = "원주민 아이",
            body = "원주민 아이가 다가와 꽃 한 송이를 내밀어요.\n말은 통하지 않지만, 웃고 있어요.",
            options = gm => new[] { "꽃을 받는다", "지나친다" },
            onPick = (gm, i) => { if (i == 0) { gm.StoryChildFlower = true; gm.StoryTrust += 10; } else gm.StoryTrust -= 5; } },
        new Beat { day = 15, accent = Planet, title = "아이가 준 꽃",
            body = "오늘은 발송일이에요.\n아이가 준 꽃은 이 행성에서 가장 귀한 꽃 같아요.\n지구에 보내면 점수가 많이 나올 거예요.",
            when = gm => gm.StoryChildFlower,
            options = gm => new[] { "지구에 보낸다", "간직한다" },
            onPick = (gm, i) => { if (i == 0) { gm.StoryTrust -= 15; gm.StoryChildFlower = false; } else gm.StoryTrust += 10; } },
        new Beat { day = 25, accent = Planet, title = "제단의 꽃",
            body = "원주민 마을 가운데 제단에 우리가 키우던 꽃이 놓여 있어요.\n그들에게 꽃은… 생명 같은 것이었어요." },
        new Beat { day = 30, accent = Earth, title = "📡 지구에서 온 통신",
            body = "\"환경 적합 판정. 이주 준비를 시작한다.\"\n\n로봇이 하던 일이 무엇이었는지 알게 됐어요." },
        new Beat { day = 40, accent = Planet, title = "꽃을 돌려 주세요",
            body = "원주민들이 기지 앞에 모였어요.\n가방 속 꽃을 가리키며 고개를 숙여요.",
            options = gm => new[] { $"가방의 꽃을 돌려준다 ({gm.FlowerBag.Count}송이)", "거절한다" },
            onPick = (gm, i) => { if (i == 0) { gm.StoryTrust += 15; gm.FlowerBag.Clear(); } else gm.StoryTrust -= 10; } },
        new Beat { day = 55, accent = Earth, title = "📡 지구에서 온 통신",
            body = "\"보고량이 부족하다. 할당량을 두 배로 늘린다.\"" },
        new Beat { day = 60, accent = Planet, title = "아픈 원주민",
            body = "원주민 하나가 아파요.\n가장 귀한 꽃이 있어야 낫는 것 같아요.\n하지만 그 꽃은 이번 미션 점수의 대부분이에요.",
            options = gm => new[] { "가장 귀한 꽃을 준다", "지구에 보낼 꽃이라 줄 수 없다" },
            onPick = (gm, i) => { if (i == 0) { gm.StoryTrust += 20; gm.RemoveRarestFlower(); } else gm.StoryTrust -= 15; } },
        new Beat { day = 70, accent = Planet, title = "원주민의 마음",
            body = "(신뢰에 따라 다른 일이 일어나요)",
            onPick = (gm, i) => { } },
        new Beat { day = 85, accent = Earth, title = "📡 지구에서 온 통신",
            body = "\"100일차에 이주 함대가 출발한다. 마지막 보고를 기다린다.\"" },
        new Beat { day = 95, accent = Earth, title = "마지막 보고",
            body = "지구가 마지막 보고를 기다려요.\n무엇을 보낼까요?",
            options = gm => gm.StoryTrust >= MessengerTrust
                ? new[] { "꽃을 전부 보낸다", "원주민의 이야기를 보낸다", "송신기를 끈다" }
                : new[] { "꽃을 전부 보낸다", "송신기를 끈다" },
            onPick = (gm, i) =>
            {
                bool messenger = gm.StoryTrust >= MessengerTrust;
                if (i == 0) gm.StoryFinal = 1;                       // 정복
                else if (messenger && i == 1) gm.StoryFinal = 2;     // 메신저
                else { gm.StoryFinal = 3; gm.EndStory(GameManager.Ending.Together); } // 함께 잠들기
            } },
    };

    void Awake() => Instance = this;

    public int Trust => GameManager.Instance ? GameManager.Instance.StoryTrust : 0;

    void Update()
    {
        var gm = GameManager.Instance;
        if (!gm || gm.IsGameOver || Hud.AnyPanelOpen || !StoryUI.Instance) return; // 시작 화면·잠·다른 창·이야기 카드가 떠 있으면 기다린다
        foreach (var b in Beats)
        {
            if (b.day > gm.Day || gm.StorySeen.Contains(b.day)) continue;
            gm.StorySeen.Add(b.day);
            if (b.when != null && !b.when(gm)) continue;
            Show(gm, b);
            return; // 한 번에 하나씩
        }
    }

    void Show(GameManager gm, Beat b)
    {
        string body = b.body;
        if (b.day == 70) body = Day70(gm);
        if (b.options == null)
        {
            StoryUI.Instance.Narrate(b.title, body, b.accent);
            return;
        }
        var options = b.options(gm);
        StoryUI.Instance.Show(b.title, body, options, i =>
        {
            gm.StoryTrust = Mathf.Clamp(gm.StoryTrust, -100, 100);
            b.onPick(gm, i);
            gm.StoryTrust = Mathf.Clamp(gm.StoryTrust, -100, 100);
            gm.Save();
        }, b.accent);
    }

    // 70일차: 믿으면 도와주고, 미워하면 해친다
    static string Day70(GameManager gm)
    {
        if (gm.StoryTrust >= 30)
        {
            gm.AddFuel(Progression.FuelPerDay * 5);
            return "원주민들이 몰래 열매를 가져다줬어요.\n연료로 쓸 수 있어요. (연료 +5일)";
        }
        if (gm.StoryTrust <= -20)
        {
            var spot = gm.Spots.FirstOrDefault(s => s.gameObject.activeInHierarchy && !s.IsEmpty && !s.IsRotten);
            if (spot) spot.Rot();
            return "누군가 밤사이 밭을 망쳐 놓았어요.\n원주민들이 멀리서 노려보고 있어요.";
        }
        return "원주민들이 멀리서 로봇을 지켜보기만 해요.\n아직 마음을 정하지 못한 것 같아요.";
    }

    // 꽃을 보낼 때마다 원주민의 신뢰가 조금씩 깎인다(보낸 송이 수만큼)
    public static void OnFlowersSent(GameManager gm, int count) => gm.StoryTrust = Mathf.Clamp(gm.StoryTrust - count, -100, 100);
}
