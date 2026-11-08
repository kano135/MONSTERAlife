using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// 게임 화면 UI 전체(UGUI, 코드로 만든다). 프루트버스 느낌: 크림색 둥근 판 + 진한 테두리 + 큰 아이콘.
//   왼쪽 위 연료(막대·며칠 남음) · 비닐하우스 안에서만 습도·온도·빛 · 가운데 위 날짜/시계
//   아래 가운데 씨앗 칸(물뿌리개를 들고 있을 때만 남은 횟수) · 오른쪽 아래 행동 안내 · 가운데 알림
//   Esc 메뉴(미션·상태·설정) · B 도감 · U 업그레이드 · 시작 안내 · 엔딩
public class Hud : MonoBehaviour
{
    public PlayerController player;

    const float IntroSeconds = 12f;
    const string Story =
        "지구는 더 이상 사람이 살 수 없게 됐어요.\n" +
        "AI 정원사 로봇은 100일 동안 이 행성에서 꽃을 키워요.\n" +
        "5일마다 꽃을 지구로 보내 미션을 넘기면 연료통과 도구·설계키트·비료가 와요.\n" +
        "15일 동안 미션을 못 넘기면 연료가 바닥나요.";

    // 엔딩 그림: Assets/Resources/Endings/ 에 이 이름으로 넣으면 배경으로 나온다
    static readonly (GameManager.Ending ending, string image, string title, string text, Color bg)[] Endings =
    {
        (GameManager.Ending.Sleep, "Endings/ending_sleep", "잠드는 엔딩",
            "15일 동안 미션을 넘지 못해 연료가 바닥났어요.\n로봇은 이 행성에서 조용히 잠듭니다.", new Color32(58, 48, 96, 255)),
        (GameManager.Ending.Lava, "Endings/ending_lava", "용암 엔딩",
            "행성을 살리지 못했어요.\n로봇은 끓어오르는 용암 속으로 천천히 녹아 사라집니다.", new Color32(120, 40, 30, 255)),
        (GameManager.Ending.OtherPlanet, "Endings/ending_otherplanet", "다른 행성 엔딩",
            "그럭저럭 해냈어요.\n로봇은 다시 캡슐에 실려 또 다른 행성으로 보내집니다.", new Color32(52, 92, 150, 255)),
        (GameManager.Ending.Migration, "Endings/ending_migration", "로봇 이주 엔딩",
            "행성이 살아났어요!\n지구에서 여러 로봇들이 우주선을 타고 이 행성으로 옮겨 옵니다.", new Color32(70, 150, 90, 255)),
    };

    GameManager gm;
    RectTransform root;

    Text fuelText, lifeText, bagText, matText, fertText;
    RectTransform fuelBar, lifeBar;
    Image face;
    Sprite faceNeutral, faceHappy, faceSleepy, faceSurprised;

    Text dayText, timeText, sendText;

    Text missionTitle, missionBody;
    RectTransform missionBar;
    RectTransform[] missionMarks;

    Text selectedName;

    GameObject promptBox;
    Text promptTitle, promptBody, promptAction;
    GameObject envRows;
    RectTransform[] envBar, envRange;
    Text[] envText;

    RectTransform toast;
    Text toastText;
    string shownMessage;

    GameObject book, upgrade, intro, ending, settings, crosshair, greenhouseBox;
    RectTransform[] ghBar;
    Text[] ghText;
    Text promptKey, viewText, sensitivityText;

    // 창이 열려 있으면 로봇은 멈추고 마우스 커서가 나온다
    public static bool AnyPanelOpen { get; private set; }
    Image[] bookIcon;
    Text[] bookText;
    Text upgradeTools;
    Text[] upgradeText;
    Text endingCard;
    RawImage endingImage;
    Image endingBg;

    void Start()
    {
        gm = GameManager.Instance;
        faceNeutral = ArtLibrary.Ui("Robot/face_neutral");
        faceHappy = ArtLibrary.Ui("Robot/face_happy");
        faceSleepy = ArtLibrary.Ui("Robot/face_sleepy");
        faceSurprised = ArtLibrary.Ui("Robot/face_surprised");

        if (!FindFirstObjectByType<EventSystem>())
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        root = (RectTransform)canvasGo.transform;

        BuildStatus();
        BuildGreenhousePanel();
        BuildClock();

        // 장치가 고장 나면 어디 있든 알려 준다
        if (Greenhouse.Instance) Greenhouse.Instance.Broke += d => player.Show($"비닐하우스 {d.Name} 고장! 가서 E로 고쳐 주세요", 5f);
        BuildHotbar();
        BuildPrompt();
        BuildToast();
        BuildHints();
        BuildBook();
        BuildUpgrade();
        BuildSettings();
        BuildCrosshair();
        BuildIntro();
        BuildEnding();
    }

    // ───────── 만들기 ─────────

    void BuildStatus()
    {
        UiKit.Icon(root, ArtLibrary.Ui("UI/logo"), new Vector2(0, 1), new Vector2(28, -10), 250);

        // 화면에는 연료만(막대 + 며칠치 남았는지). 행성 생명력·가방·재료·비료는 Esc 메뉴 '상태' 탭에.
        var card = UiKit.Panel(root, "Status", UiKit.Cream);
        card.Root().Place(new Vector2(0, 1), new Vector2(24, -150), new Vector2(460, 84));
        (fuelText, fuelBar) = BarRow(card.transform, -22, "연료", UiKit.Orange);

        // 로봇 얼굴은 카드 오른쪽 위 바깥에 스티커처럼(숫자를 가리지 않게)
        face = UiKit.Icon(card.Root(), faceNeutral, new Vector2(1, 1), new Vector2(10, 100), 96);
    }

    // labelWidth: 이름 칸 폭(이름이 길면 넓혀서 막대와 안 겹치게)
    (Text, RectTransform) BarRow(Transform parent, float y, string label, Color color, float x = 20, float labelWidth = 110)
    {
        UiKit.Label(parent, label, 22, UiKit.Ink).rectTransform.Place(new Vector2(0, 1), new Vector2(x, y), new Vector2(labelWidth, 40));
        var bar = UiKit.Bar(parent, UiKit.Muted, color);
        ((RectTransform)bar.parent).Place(new Vector2(0, 1), new Vector2(x + labelWidth, y - 6), new Vector2(190, 28));
        var value = UiKit.Label(parent, "", 20, UiKit.Ink, TextAnchor.MiddleRight);
        value.rectTransform.Place(new Vector2(0, 1), new Vector2(x + labelWidth + 190, y), new Vector2(120, 40));
        return (value, bar);
    }

    // 비닐하우스 안에 있을 때만 뜨는 상태판: 습도·온도·빛과 장치 상태(세기 / 고장)
    void BuildGreenhousePanel()
    {
        if (!Greenhouse.Instance) return;
        var card = UiKit.Panel(root, "GreenhouseInfo", UiKit.Cream, UiKit.DarkGreen);
        greenhouseBox = card.Root().Place(new Vector2(0, 1), new Vector2(24, -256), new Vector2(460, 236)).gameObject;
        UiKit.Label(card.transform, "비닐하우스", 28, UiKit.DarkGreen).rectTransform.Place(new Vector2(0, 1), new Vector2(20, -12), new Vector2(420, 40));
        string[] icons = { "Icons/water", "Icons/temp", "Icons/sun" };
        Color[] colors = { UiKit.Sky, UiKit.Pink, UiKit.Yellow };
        int n = Greenhouse.Instance.Devices.Length;
        ghBar = new RectTransform[n];
        ghText = new Text[n];
        for (int i = 0; i < n; i++)
        {
            float y = -62 - i * 56;
            UiKit.Icon(card.transform, ArtLibrary.Ui(icons[i]), new Vector2(0, 1), new Vector2(16, y + 4), 40);
            ghBar[i] = UiKit.Bar(card.transform, UiKit.Muted, colors[i]);
            ((RectTransform)ghBar[i].parent).Place(new Vector2(0, 1), new Vector2(64, y - 6), new Vector2(150, 26));
            ghText[i] = UiKit.Label(card.transform, "", 20, UiKit.Ink);
            ghText[i].rectTransform.Place(new Vector2(0, 1), new Vector2(226, y + 2), new Vector2(220, 44));
        }
        greenhouseBox.SetActive(false);
    }

    void UpdateGreenhousePanel()
    {
        if (!greenhouseBox) return;
        greenhouseBox.SetActive(player.InGreenhouse);
        if (!player.InGreenhouse) return;
        var gh = Greenhouse.Instance;
        float[] values = { gh.Humidity, gh.Temperature, gh.Light };
        for (int i = 0; i < gh.Devices.Length; i++)
        {
            var d = gh.Devices[i];
            UiKit.SetBar(ghBar[i], values[i]);
            ghText[i].text = !d.Installed
                ? $"{d.Stat} {values[i]:P0}\n<color=#8A7A66>{d.Name} 없음 · 설계키트 필요</color>"
                : d.Broken
                ? $"{d.Stat} {values[i]:P0}\n<color=#D23C2D>{d.Name} 고장!</color>"
                : $"{d.Stat} {values[i]:P0}\n{d.Name} · {d.LevelName}";
        }
    }

    void BuildClock()
    {
        var pill = UiKit.Panel(root, "Clock", UiKit.Cream);
        pill.Root().Place(new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(400, 128));
        UiKit.Icon(pill.transform, ArtLibrary.Ui("Icons/sun"), new Vector2(0, 1), new Vector2(14, -14), 60);
        dayText = UiKit.Label(pill.transform, "", 34, UiKit.Ink, TextAnchor.MiddleCenter);
        dayText.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(20, -8), new Vector2(300, 46));
        timeText = UiKit.Label(pill.transform, "", 26, UiKit.DarkGreen, TextAnchor.MiddleCenter);
        timeText.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(20, -52), new Vector2(300, 34));
        sendText = UiKit.Label(pill.transform, "", 20, UiKit.Ink, TextAnchor.MiddleCenter);
        sendText.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -86), new Vector2(380, 30));
    }

    // 미션 카드: Esc 메뉴 '미션' 탭 안에 놓는다
    void BuildMission(Transform parent)
    {
        var card = UiKit.Panel(parent, "Mission", Color.white, UiKit.DarkGreen);
        card.Root().Place(new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(470, 290));
        missionTitle = UiKit.Label(card.transform, "", 28, UiKit.DarkGreen);
        missionTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(20, -14), new Vector2(430, 40));

        missionBar = UiKit.Bar(card.transform, UiKit.Muted, UiKit.Yellow);
        var barRt = (RectTransform)missionBar.parent;
        barRt.Place(new Vector2(0, 1), new Vector2(20, -62), new Vector2(420, 30));
        string[] names = { "생존", "업글", "최종" };
        missionMarks = new RectTransform[3];
        for (int i = 0; i < 3; i++)
        {
            var mark = UiKit.Rect(barRt, "Mark");
            mark.gameObject.AddComponent<Image>().color = UiKit.Ink;
            mark.anchorMin = mark.anchorMax = new Vector2(0, 0.5f);
            mark.sizeDelta = new Vector2(4, 40);
            var label = UiKit.Label(mark, names[i], 16, UiKit.Ink, TextAnchor.UpperCenter);
            label.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, -24), new Vector2(60, 22));
            missionMarks[i] = mark;
        }
        missionBody = UiKit.Label(card.transform, "", 21, UiKit.Ink, TextAnchor.UpperLeft);
        missionBody.rectTransform.Place(new Vector2(0, 1), new Vector2(20, -120), new Vector2(430, 160));
    }

    // 아래 칸 9개(숫자 키 1~9). 칸 번호만 고정이고, 무엇이 들어가는지는 얻은 순서(Inventory). Esc 가방에서 옮길 수 있다.
    void BuildHotbar()
    {
        int n = Inventory.HotbarSize;
        const float slot = 84, gap = 6;
        var bar = UiKit.Panel(root, "Hotbar", UiKit.Cream);
        bar.Root().Place(new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(n * (slot + gap) + 24, slot + 28));
        for (int i = 0; i < n; i++)
            MakeSlot(bar.transform, i, new Vector2(10 + i * (slot + gap), 2), new Vector2(0, 0.5f), slot, true, true);
        // 손에 든 것 이름(물뿌리개 판과 안 겹치게 살짝 오른쪽)
        var namePill = UiKit.Panel(root, "SeedName", UiKit.Yellow);
        namePill.Root().Place(new Vector2(0.5f, 0), new Vector2(60, slot + 56), new Vector2(300, 46));
        selectedName = UiKit.Label(namePill.transform, "", 22, UiKit.Ink, TextAnchor.MiddleCenter);
        selectedName.rectTransform.Stretch();

        // 물뿌리개: 씨앗 칸 왼쪽 위. 그림은 Icons/wateringcan, 없으면 하늘색 동그라미.
        float barWidth = n * (slot + gap) + 24;
        canBox = UiKit.Panel(root, "WateringCan", UiKit.Sky);
        canBox.Root().Place(new Vector2(0.5f, 0), new Vector2(-barWidth / 2 + 110, slot + 52), new Vector2(300, 56));
        var canSprite = ArtLibrary.Ui("Icons/wateringcan");
        var canIcon = UiKit.Icon(canBox.transform, canSprite ? canSprite : UiKit.Rounded, new Vector2(0, 0.5f), new Vector2(6, 0), 46);
        if (!canSprite) { canIcon.enabled = true; canIcon.color = UiKit.DarkGreen; canIcon.rectTransform.sizeDelta = Vector2.one * 30; }
        canText = UiKit.Label(canBox.transform, "", 22, UiKit.Ink);
        canText.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(58, 0), new Vector2(236, 50));
    }

    Image canBox;
    Text canText;

    // ───────── 물건 칸(아래 칸·가방 공용) ─────────

    readonly System.Collections.Generic.List<InventorySlotUI> slotViews = new System.Collections.Generic.List<InventorySlotUI>();

    // grow: 손에 든 칸을 살짝 키운다(아래 칸만)
    InventorySlotUI MakeSlot(Transform parent, int index, Vector2 pos, Vector2 anchor, float size, bool number, bool grow = false)
    {
        var fill = UiKit.Panel(parent, "Slot", Color.white, UiKit.Muted, 4);
        fill.Root().Place(anchor, pos, new Vector2(size, size));
        var view = fill.Root().gameObject.AddComponent<InventorySlotUI>();
        view.index = index;
        view.fill = fill;
        view.icon = UiKit.Icon(fill.transform, null, new Vector2(0.5f, 0.5f), Vector2.zero, size - 14);
        view.count = UiKit.Label(fill.transform, "", size > 70 ? 22 : 18, UiKit.Ink, TextAnchor.LowerRight);
        view.count.rectTransform.Stretch(4);
        view.count.gameObject.AddComponent<Outline>().effectColor = Color.white;
        if (number)
        {
            var num = UiKit.Label(fill.transform, (index + 1).ToString(), size > 70 ? 20 : 16, UiKit.Ink, TextAnchor.UpperLeft);
            num.rectTransform.Stretch(5);
            num.gameObject.AddComponent<Outline>().effectColor = Color.white;
        }
        view.dragLayer = root;
        view.grow = grow;
        view.onUse = i => { var msg = gm.Inv.Use(i); if (msg != null) player.Show(msg, 2.5f); };
        view.onMove = (from, to) => gm.Inv.Swap(from, to);
        slotViews.Add(view);
        return view;
    }

    void UpdateSlots()
    {
        var inv = gm.Inv;
        foreach (var v in slotViews)
        {
            string key = inv.Slots[v.index];
            bool held = v.index == inv.Selected;
            v.fill.color = held ? UiKit.Yellow : Color.white;
            if (v.grow) v.fill.Root().localScale = Vector3.one * (held ? 1.1f : 1f);
            if (key == "")
            {
                v.icon.enabled = false;
                v.count.text = "";
                continue;
            }
            var sprite = inv.Icon(key);
            v.icon.enabled = true;
            v.icon.sprite = sprite ? sprite : UiKit.Rounded;
            v.icon.color = sprite ? Color.white : inv.Tint(key);
            float full = ((RectTransform)v.transform).sizeDelta.x - 14;
            v.icon.rectTransform.sizeDelta = Vector2.one * (sprite ? full : full * 0.55f);
            v.count.text = key == Inventory.CanKey ? $"{gm.CanWater}/{gm.CanCapacity}" : $"×{inv.Count(key)}";
        }
        string heldKey = inv.HeldKey;
        selectedName.text = heldKey == "" ? "빈 손" : heldKey == Inventory.CanKey ? $"물뿌리개 {gm.CanWater}/{gm.CanCapacity}" : $"{inv.Name(heldKey)} ×{inv.Count(heldKey)}";
    }

    // ●●○ 처럼 남은 횟수. 칸이 많으면(8회 이상) 숫자만.
    string CanDots() => gm.CanCapacity <= 6
        ? new string('●', gm.CanWater) + new string('○', gm.CanCapacity - gm.CanWater)
        : "";

    // 물뿌리개 남은 횟수는 손에 물뿌리개를 들고 있을 때만
    void UpdateCan()
    {
        var box = canBox.Root().gameObject;
        if (box.activeSelf != HandItemView.HoldingCan) box.SetActive(HandItemView.HoldingCan);
        if (!HandItemView.HoldingCan) return;
        canBox.color = gm.CanWater > 0 ? UiKit.Sky : UiKit.Pink;
        canText.text = gm.CanWater > 0
            ? $"물뿌리개 Lv{gm.CanLevel} {CanDots()} {gm.CanWater}/{gm.CanCapacity}"
            : $"물 0/{gm.CanCapacity} · 호수로!";
    }

    void BuildPrompt()
    {
        var card = UiKit.Panel(root, "Prompt", UiKit.Cream);
        card.Root().Place(new Vector2(1, 0), new Vector2(-24, 24), new Vector2(430, 330));
        promptBox = card.Root().gameObject;
        promptTitle = UiKit.Label(card.transform, "", 28, UiKit.DarkGreen);
        promptTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(20, -12), new Vector2(390, 40));
        promptBody = UiKit.Label(card.transform, "", 21, UiKit.Ink, TextAnchor.UpperLeft);
        promptBody.rectTransform.Place(new Vector2(0, 1), new Vector2(20, -56), new Vector2(390, 150));

        envRows = UiKit.Rect(card.transform, "EnvRows").gameObject;
        ((RectTransform)envRows.transform).Stretch();
        string[] icons = { "Icons/water", "Icons/sun", "Icons/temp" };
        Color[] colors = { UiKit.Sky, UiKit.Yellow, UiKit.Pink };
        envBar = new RectTransform[3];
        envRange = new RectTransform[3];
        envText = new Text[3];
        for (int i = 0; i < 3; i++)
        {
            float y = -124 - i * 44;
            UiKit.Icon(envRows.transform, ArtLibrary.Ui(icons[i]), new Vector2(0, 1), new Vector2(18, y + 6), 38);
            envBar[i] = UiKit.Bar(envRows.transform, UiKit.Muted, colors[i]);
            var barRt = (RectTransform)envBar[i].parent;
            barRt.Place(new Vector2(0, 1), new Vector2(66, y), new Vector2(230, 26));
            // 좋아하는 범위 표시(초록 테두리 칸)
            var range = UiKit.Fill(barRt, "Range", new Color(UiKit.DarkGreen.r, UiKit.DarkGreen.g, UiKit.DarkGreen.b, 0.35f));
            range.raycastTarget = false;
            envRange[i] = range.rectTransform;
            envText[i] = UiKit.Label(envRows.transform, "", 20, UiKit.Ink, TextAnchor.MiddleRight);
            envText[i].rectTransform.Place(new Vector2(0, 1), new Vector2(300, y + 6), new Vector2(110, 38));
        }

        var key = UiKit.Panel(card.transform, "Key", UiKit.Yellow, UiKit.Ink, 4);
        key.Root().Place(new Vector2(0, 0), new Vector2(18, 16), new Vector2(52, 52));
        promptKey = UiKit.Label(key.transform, "E", 28, UiKit.Ink, TextAnchor.MiddleCenter);
        promptKey.rectTransform.Stretch();
        promptAction = UiKit.Label(card.transform, "", 22, UiKit.Ink);
        promptAction.rectTransform.Place(new Vector2(0, 0), new Vector2(84, 16), new Vector2(330, 52));
    }

    void BuildToast()
    {
        var card = UiKit.Panel(root, "Toast", Color.white, UiKit.DarkGreen);
        // 시계 바로 밑: 화면 가운데 로봇을 가리지 않게
        toast = card.Root().Place(new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(760, 96));
        toastText = UiKit.Label(card.transform, "", 24, UiKit.Ink, TextAnchor.MiddleCenter);
        toastText.rectTransform.Stretch(10);
        toast.gameObject.SetActive(false);
    }

    void BuildHints()
    {
        var card = UiKit.Panel(root, "Hints", new Color(1, 1, 1, 0.85f), UiKit.Muted, 3);
        card.Root().Place(new Vector2(0, 0), new Vector2(24, 24), new Vector2(420, 160));
        UiKit.Label(card.transform, "WASD 이동 · Shift 달리기 · E 행동\nF 비료 · Q 충전기에서 쉬기 · 1~9·Tab 손에 들기\n호수에서 E 물뿌리개 채우기\nB 도감 · U 업그레이드\nV 1인칭/3인칭 · Esc 메뉴(가방·미션·상태·설정)", 18, UiKit.Ink, TextAnchor.MiddleCenter)
            .rectTransform.Stretch(6);
    }

    Image BigPanel(string name, Vector2 size)
    {
        var frame = ArtLibrary.Ui("UI/panel_frame");
        Image panel;
        if (frame)
        {
            panel = UiKit.Rect(root, name).gameObject.AddComponent<Image>();
            panel.sprite = frame;
        }
        else panel = UiKit.Panel(root, name, UiKit.Cream);
        var rt = frame ? panel.rectTransform : panel.Root();
        rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 30), size);
        return panel;
    }

    void BuildBook()
    {
        var panel = BigPanel("Book", new Vector2(1240, 800));
        book = (panel.sprite == UiKit.Rounded ? panel.Root() : panel.rectTransform).gameObject;
        UiKit.Label(panel.transform, "식물 도감", 44, UiKit.DarkGreen, TextAnchor.MiddleCenter)
            .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(600, 60));
        UiKit.Label(panel.transform, "B 닫기", 22, UiKit.Ink, TextAnchor.MiddleRight)
            .rectTransform.Place(new Vector2(1, 1), new Vector2(-90, -70), new Vector2(200, 40));

        int n = gm.Plants.Count;
        bookIcon = new Image[n];
        bookText = new Text[n];
        for (int i = 0; i < n; i++)
        {
            var card = UiKit.Panel(panel.transform, "Card", Color.white, UiKit.Muted, 4);
            card.Root().Place(new Vector2(0.5f, 1), new Vector2(-440 + i % 5 * 220, -140 - i / 5 * 300), new Vector2(200, 280));
            bookIcon[i] = UiKit.Icon(card.transform, ArtLibrary.PlantIcon(gm.Plants[i]), new Vector2(0.5f, 1), new Vector2(0, -10), 160);
            bookText[i] = UiKit.Label(card.transform, "", 20, UiKit.Ink, TextAnchor.UpperCenter);
            bookText[i].rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(190, 96));
        }
        book.SetActive(false);
    }

    void BuildUpgrade()
    {
        var panel = UiKit.Panel(root, "Upgrade", UiKit.Cream, UiKit.DarkGreen, 6);
        upgrade = panel.Root().Place(new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1440, 700)).gameObject;
        UiKit.Label(panel.transform, "업그레이드", 44, UiKit.DarkGreen, TextAnchor.MiddleCenter)
            .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(600, 60));
        UiKit.Label(panel.transform, "U 닫기", 22, UiKit.Ink, TextAnchor.MiddleRight)
            .rectTransform.Place(new Vector2(1, 1), new Vector2(-30, -30), new Vector2(200, 40));
        upgradeTools = UiKit.Label(panel.transform, "", 24, UiKit.Ink, TextAnchor.MiddleCenter);
        upgradeTools.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(1100, 40));

        var bps = Progression.Blueprints;
        upgradeText = new Text[bps.Length];
        for (int i = 0; i < bps.Length; i++)
        {
            var bp = bps[i];
            var card = UiKit.Panel(panel.transform, "Card", Color.white, UiKit.Muted, 4);
            card.Root().Place(new Vector2(0.5f, 1), new Vector2(-600 + i * 200, -150), new Vector2(186, 500));
            UiKit.Icon(card.transform, ArtLibrary.Ui($"Icons/blueprint_{bp.id}") ?? ArtLibrary.Ui("Icons/book"), new Vector2(0.5f, 1), new Vector2(0, -12), 90);
            upgradeText[i] = UiKit.Label(card.transform, "", 21, UiKit.Ink, TextAnchor.UpperCenter);
            upgradeText[i].rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(176, 300));
            var btn = UiKit.Button(card.transform, $"만들기 [{i + 1}]", UiKit.Yellow, () => player.Show(gm.Craft(bp), 3.5f));
            ((RectTransform)btn.transform).Place(new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(170, 56));
        }

        // 마지막 칸: 물뿌리개 강화(설계도 대신 미션 강화권을 쓴다)
        var canCard = UiKit.Panel(panel.transform, "Card_WateringCan", new Color32(232, 246, 255, 255), UiKit.Sky, 4);
        canCard.Root().Place(new Vector2(0.5f, 1), new Vector2(-600 + bps.Length * 200, -150), new Vector2(186, 500));
        UiKit.Icon(canCard.transform, ArtLibrary.Ui("Icons/wateringcan") ?? ArtLibrary.Ui("Icons/water"), new Vector2(0.5f, 1), new Vector2(0, -12), 90);
        upgradeCanText = UiKit.Label(canCard.transform, "", 21, UiKit.Ink, TextAnchor.UpperCenter);
        upgradeCanText.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(176, 300));
        var canBtn = UiKit.Button(canCard.transform, $"강화 [{CanUpgradeKey + 1}]", UiKit.Sky, () => player.Show(gm.UpgradeCan(), 3.5f));
        ((RectTransform)canBtn.transform).Place(new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(170, 56));
        upgrade.SetActive(false);
    }

    Text upgradeCanText;
    // 업그레이드 창에서 물뿌리개 강화 숫자 키 = 설계도 다음 칸(7)
    static int CanUpgradeKey => Progression.Blueprints.Length;

    void BuildIntro()
    {
        var dim = UiKit.Rect(root, "Intro").gameObject.AddComponent<Image>();
        dim.rectTransform.Stretch();
        dim.color = new Color(0.15f, 0.12f, 0.2f, 0.55f);
        intro = dim.gameObject;
        UiKit.Icon(dim.transform, ArtLibrary.Ui("UI/logo"), new Vector2(0.5f, 0.5f), new Vector2(0, 230), 620);
        var card = UiKit.Panel(dim.transform, "Story", UiKit.Cream);
        card.Root().Place(new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(980, 270));
        UiKit.Label(card.transform, Story, 28, UiKit.Ink, TextAnchor.MiddleCenter).rectTransform.Stretch(16);
        UiKit.Label(dim.transform, "아무 키나 누르면 시작", 26, Color.white, TextAnchor.MiddleCenter)
            .rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -250), new Vector2(600, 40));
    }

    // Esc 메뉴: 탭 4개(가방 · 미션 · 상태 · 설정). 화면에는 연료만 두고 나머지 정보는 여기서 본다.
    //   가방 = 물건 칸 옮기기·손에 들기, 미션 = 다음 발송 미션 기준선, 상태 = 행성 생명력·꽃 가방·재료·비료·도구·물뿌리개, 설정 = 시점·마우스 감도
    void BuildSettings()
    {
        var panel = UiKit.Panel(root, "Menu", UiKit.Cream, UiKit.DarkGreen, 6);
        settings = panel.Root().Place(new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(760, 560)).gameObject;
        UiKit.Label(panel.transform, "메뉴", 40, UiKit.DarkGreen, TextAnchor.MiddleLeft)
            .rectTransform.Place(new Vector2(0, 1), new Vector2(30, -18), new Vector2(200, 56));
        UiKit.Label(panel.transform, "Esc 닫기", 22, UiKit.Ink, TextAnchor.MiddleRight)
            .rectTransform.Place(new Vector2(1, 1), new Vector2(-24, -26), new Vector2(200, 40));

        string[] names = { "가방", "미션", "상태", "설정" };
        menuTabs = new GameObject[names.Length];
        menuTabButtons = new Image[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var btn = UiKit.Button(panel.transform, names[i], UiKit.Muted, () => ShowTab(index));
            ((RectTransform)btn.transform).Place(new Vector2(0.5f, 1), new Vector2(-255 + i * 170, -90), new Vector2(150, 56));
            menuTabButtons[i] = (Image)btn.targetGraphic;
            var tab = UiKit.Rect(panel.transform, "Tab_" + names[i]);
            tab.Place(new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(700, 380));
            menuTabs[i] = tab.gameObject;
        }

        // 가방 탭: 위 3줄 = 가방(27칸), 아래 1줄 = 아래 칸 1~9. 끌어서 옮기고, 클릭하면 손에 든다.
        var bag = menuTabs[0].transform;
        UiKit.Label(bag, "클릭: 손에 들기 · 끌어서 놓기: 자리 바꾸기", 20, UiKit.Ink, TextAnchor.MiddleCenter)
            .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(680, 30));
        const float cell = 64, step = 72;
        float left = -(Inventory.HotbarSize * step - (step - cell)) / 2f;
        for (int i = 0; i < Inventory.BagSize; i++)
        {
            int col = i % Inventory.HotbarSize, row = i / Inventory.HotbarSize;
            MakeSlot(bag, Inventory.HotbarSize + i, new Vector2(left + col * step + cell / 2, -36 - row * step - cell / 2), new Vector2(0.5f, 1), cell, false)
                .GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
        }
        UiKit.Label(bag, "아래 칸 (숫자 키 1~9)", 20, UiKit.DarkGreen, TextAnchor.MiddleLeft)
            .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(left + 290, -256), new Vector2(580, 26));
        for (int i = 0; i < Inventory.HotbarSize; i++)
            MakeSlot(bag, i, new Vector2(left + i * step + cell / 2, -288 - cell / 2), new Vector2(0.5f, 1), cell, true)
                .GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);

        // 미션 탭
        BuildMission(menuTabs[1].transform);

        // 상태 탭
        var st = menuTabs[2].transform;
        (lifeText, lifeBar) = BarRow(st, -10, "행성 생명력", UiKit.Green, 60, 160);
        bagText = UiKit.Label(st, "", 24, UiKit.Ink);
        bagText.rectTransform.Place(new Vector2(0, 1), new Vector2(60, -70), new Vector2(600, 36));
        matText = UiKit.Label(st, "", 24, UiKit.Ink);
        matText.rectTransform.Place(new Vector2(0, 1), new Vector2(60, -116), new Vector2(600, 36));
        fertText = UiKit.Label(st, "", 24, UiKit.Ink);
        fertText.rectTransform.Place(new Vector2(0, 1), new Vector2(60, -162), new Vector2(600, 36));
        toolText = UiKit.Label(st, "", 24, UiKit.Ink);
        toolText.rectTransform.Place(new Vector2(0, 1), new Vector2(60, -208), new Vector2(600, 36));
        canInfoText = UiKit.Label(st, "", 24, UiKit.Ink);
        canInfoText.rectTransform.Place(new Vector2(0, 1), new Vector2(60, -254), new Vector2(600, 36));

        // 설정 탭: 시점(1인칭/3인칭)과 마우스 감도. 고른 값은 다음에 켜도 남는다(CameraRig).
        var set = menuTabs[3].transform;
        viewText = UiKit.Label(set, "", 26, UiKit.Ink, TextAnchor.MiddleCenter);
        viewText.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(560, 40));
        var first = UiKit.Button(set, "1인칭", UiKit.Yellow, () => CameraRig.SetFirstPerson(true));
        ((RectTransform)first.transform).Place(new Vector2(0.5f, 1), new Vector2(-120, -60), new Vector2(200, 64));
        var third = UiKit.Button(set, "3인칭", UiKit.Sky, () => CameraRig.SetFirstPerson(false));
        ((RectTransform)third.transform).Place(new Vector2(0.5f, 1), new Vector2(120, -60), new Vector2(200, 64));
        sensitivityText = UiKit.Label(set, "", 26, UiKit.Ink, TextAnchor.MiddleCenter);
        sensitivityText.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(560, 40));
        var slower = UiKit.Button(set, "−", UiKit.Muted, () => CameraRig.SetSensitivity(CameraRig.Sensitivity - 0.03f));
        ((RectTransform)slower.transform).Place(new Vector2(0.5f, 1), new Vector2(-120, -210), new Vector2(120, 64));
        var faster = UiKit.Button(set, "+", UiKit.Muted, () => CameraRig.SetSensitivity(CameraRig.Sensitivity + 0.03f));
        ((RectTransform)faster.transform).Place(new Vector2(0.5f, 1), new Vector2(120, -210), new Vector2(120, 64));
        UiKit.Label(set, "V 키로도 시점을 바꿀 수 있어요", 20, UiKit.Ink, TextAnchor.MiddleCenter)
            .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -310), new Vector2(560, 34));

        ShowTab(0);
        settings.SetActive(false);
    }

    GameObject[] menuTabs;
    Image[] menuTabButtons;
    Text toolText, canInfoText;

    void ShowTab(int index)
    {
        for (int i = 0; i < menuTabs.Length; i++)
        {
            menuTabs[i].SetActive(i == index);
            menuTabButtons[i].color = i == index ? UiKit.Yellow : UiKit.Muted;
        }
    }

    // 1인칭 조준점: 화면 가운데 작은 점. 이 쪽에 있는 밭·장치·자원을 고른다.
    void BuildCrosshair()
    {
        var dot = UiKit.Fill(root, "Crosshair", Color.white);
        dot.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 14);
        dot.raycastTarget = false;
        dot.gameObject.AddComponent<Outline>().effectColor = UiKit.Ink;
        crosshair = dot.gameObject;
    }

    void BuildEnding()
    {
        endingBg = UiKit.Rect(root, "Ending").gameObject.AddComponent<Image>();
        endingBg.rectTransform.Stretch();
        ending = endingBg.gameObject;
        endingImage = UiKit.Rect(ending.transform, "Image").gameObject.AddComponent<RawImage>();
        endingImage.rectTransform.Stretch();
        var card = UiKit.Panel(ending.transform, "Report", UiKit.Cream, UiKit.DarkGreen, 6);
        card.Root().Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 760));
        endingCard = UiKit.Label(card.transform, "", 28, UiKit.Ink, TextAnchor.MiddleCenter);
        endingCard.rectTransform.Stretch(30);
        var btn = UiKit.Button(card.transform, "처음부터 다시 [R]", UiKit.Yellow, () => gm.Restart());
        ((RectTransform)btn.transform).Place(new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(320, 64));
        ending.SetActive(false);
    }

    // ───────── 매 프레임 ─────────

    void Update()
    {
        if (!gm || !root) return;
        HandleKeys();
        AnyPanelOpen = TitleScreen.Showing || gm.IsGameOver || book.activeSelf || upgrade.activeSelf || settings.activeSelf || intro.activeSelf;
        crosshair.SetActive(CameraRig.FirstPerson && !AnyPanelOpen);
        if (settings.activeSelf)
        {
            viewText.text = $"시점: {(CameraRig.FirstPerson ? "1인칭" : "3인칭")}";
            sensitivityText.text = $"마우스 감도: {CameraRig.Sensitivity / 0.15f:0.0}";
        }

        if (gm.IsGameOver)
        {
            ShowEnding();
            return;
        }
        UpdateStatus();
        UpdateGreenhousePanel();
        UpdateClock();
        UpdateMission();
        UpdateSlots();
        UpdateCan();
        UpdatePrompt();
        UpdateToast();
        if (book.activeSelf) UpdateBook();
        if (upgrade.activeSelf) UpdateUpgrade();
        if (intro.activeSelf && Time.timeSinceLevelLoad > IntroSeconds) intro.SetActive(false);
    }

    void HandleKeys()
    {
        var kb = Keyboard.current;
        if (kb == null || TitleScreen.Showing) return; // 시작 화면에서는 단축키를 안 받는다
        if (gm.IsGameOver)
        {
            if (kb.rKey.wasPressedThisFrame) gm.Restart();
            return;
        }
        if (intro.activeSelf && kb.anyKey.wasPressedThisFrame)
        {
            intro.SetActive(false);
            return;
        }
        if (kb.escapeKey.wasPressedThisFrame) Toggle(settings);
        if (kb.bKey.wasPressedThisFrame) Toggle(book);
        if (kb.uKey.wasPressedThisFrame) Toggle(upgrade);

        // 숫자 키: 업그레이드 창이 열려 있으면 만들기, 아니면 아래 칸 1~9 고르기(손에 들기)
        var keys = new[] { kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key, kb.digit6Key, kb.digit7Key, kb.digit8Key, kb.digit9Key, kb.digit0Key };
        for (int i = 0; i < keys.Length; i++)
        {
            if (!keys[i].wasPressedThisFrame) continue;
            if (upgrade.activeSelf)
            {
                if (i < Progression.Blueprints.Length) player.Show(gm.Craft(Progression.Blueprints[i]), 3.5f);
                else if (i == CanUpgradeKey) player.Show(gm.UpgradeCan(), 3.5f);
            }
            else if (i < Inventory.HotbarSize) gm.Inv.Select(i);
        }

        // 1인칭에서는 휠로 아래 칸 넘기기(3인칭에서는 휠이 카메라 거리)
        var mouse = Mouse.current;
        if (CameraRig.FirstPerson && mouse != null && !AnyPanelOpen)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0) gm.Inv.Select((gm.Inv.Selected + (scroll < 0 ? 1 : -1) + Inventory.HotbarSize) % Inventory.HotbarSize);
        }
    }

    // 창은 하나만 열린다
    void Toggle(GameObject panel)
    {
        bool open = !panel.activeSelf;
        book.SetActive(false);
        upgrade.SetActive(false);
        settings.SetActive(false);
        panel.SetActive(open);
    }

    void UpdateStatus()
    {
        float days = gm.Fuel / Progression.FuelPerDay;
        fuelText.text = $"{days:0.#}일 남음";
        UiKit.SetBar(fuelBar, gm.Fuel / Progression.MaxFuel);
        if (!settings.activeSelf) { UpdateFace(); return; } // 나머지는 Esc 메뉴가 열려 있을 때만
        toolText.text = "도구 " + (gm.Tools.Count > 0 ? string.Join(", ", gm.Tools.Select(t => Progression.GetTool(t).name)) : "없음");
        canInfoText.text = $"물뿌리개 Lv{gm.CanLevel} · {gm.CanWater}/{gm.CanCapacity}회 · 강화권 {gm.CanTokens}장";
        lifeText.text = $"{gm.Habitability:P0}";
        UiKit.SetBar(lifeBar, gm.Habitability);
        bagText.text = $"꽃 가방 {gm.FlowerBag.Count}송이 ({gm.BagPoints}점) · 보낸 꽃 {gm.FlowersSent}";
        matText.text = string.Join("  ·  ", Progression.Materials.Select(m => $"{m.name} {gm.MaterialCount(m.id)}"));
        var ferts = Enumerable.Range(1, Progression.FertilizerGrades).Where(g => gm.FertilizerCount(g) > 0).Select(g => $"{g}단계×{gm.FertilizerCount(g)}");
        fertText.text = "비료 " + (ferts.Any() ? string.Join(" ", ferts) : "없음");
        UpdateFace();
    }

    void UpdateFace()
    {
        var f = gm.Fuel < Progression.FuelPerDay * 4 ? faceSleepy : player.CurrentMessage != null ? faceHappy : faceNeutral;
        if (f) face.sprite = f;
    }

    void UpdateClock()
    {
        dayText.text = $"{gm.Day}일차 / {Progression.TotalDays}";
        timeText.text = Progression.ClockText(gm.Minute);
        if (Progression.IsSendDay(gm.Day))
            sendText.text = gm.LastMissionDay == gm.Day ? "오늘 미션 완료!" : "오늘은 지구 발송일!";
        else
        {
            int next = Progression.NextSendDay(gm.Day);
            sendText.text = $"다음 발송 {next}일차 ({next - gm.Day}일 남음)";
        }
    }

    void UpdateMission()
    {
        int sendDay = Progression.NextSendDay(gm.Day);
        if (gm.LastMissionDay == gm.Day) sendDay += Progression.SendEveryDays;
        int mission = Progression.MissionNumber(sendDay);
        int survive = Progression.SurviveLine(mission), up = Progression.UpgradeLine(mission), fin = Progression.FinalLine(mission);
        float max = fin * 1.15f;
        int points = gm.BagPoints;

        missionTitle.text = $"미션 {mission} · {sendDay}일차 발송";
        UiKit.SetBar(missionBar, points / max);
        int[] lines = { survive, up, fin };
        for (int i = 0; i < 3; i++)
        {
            var rt = missionMarks[i];
            rt.anchorMin = rt.anchorMax = new Vector2(lines[i] / max, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }
        string Check(int line) => points >= line ? "●" : "○";
        missionBody.text =
            $"가방 점수 {points}점 (꽃 1송이 = ★ 수, 변종 2배)\n" +
            $"{Check(survive)} 생존 {survive}점 → 연료통 (5일치)\n" +
            $"{Check(up)} 업그레이드 {up}점 → 도구·설계키트·비료\n" +
            $"{Check(fin)} 최종 {fin}점 → 넘은 만큼 성적 +";
    }

    void UpdatePrompt()
    {
        envRows.SetActive(false);
        promptBody.text = "";
        promptKey.text = "E";
        if (player.NearStation)
        {
            promptTitle.text = "지구 송신기";
            bool sendDay = Progression.IsSendDay(gm.Day) && gm.LastMissionDay != gm.Day;
            // 보낼 때 미션 기준선을 같이 보여 준다(평소에는 Esc 메뉴 '미션' 탭)
            int sendAt = Progression.NextSendDay(gm.Day) + (gm.LastMissionDay == gm.Day ? Progression.SendEveryDays : 0);
            int mission = Progression.MissionNumber(sendAt), pts = gm.BagPoints;
            string Line(int line, string reward) => $"{(pts >= line ? "●" : "○")} {line}점 {reward}";
            promptBody.text =
                $"{(sendDay ? "오늘은 발송일!" : gm.LastMissionDay == gm.Day ? "오늘 미션은 끝났어요" : $"다음 발송 {sendAt}일차")} · 미션 {mission}\n" +
                $"가방 {gm.FlowerBag.Count}송이 · {pts}점\n" +
                $"{Line(Progression.SurviveLine(mission), "생존 → 연료통")}\n" +
                $"{Line(Progression.UpgradeLine(mission), "업그레이드 → 도구·설계키트")}\n" +
                $"{Line(Progression.FinalLine(mission), "최종 → 성적 +")}";
            promptKey.text = "E";
            promptAction.text = sendDay ? "꽃을 지구로 보내기" : "아직 보낼 수 없어요";
        }
        else if (player.NearPad)
        {
            promptTitle.text = "기지 충전기";
            promptBody.text = $"충전하고 쉬면 다음 날 아침이 돼요.\n하루에 연료 {Progression.FuelPerDay:0}씩 써요.\n(새벽 2시가 되면 저절로 여기로 와요)";
            promptKey.text = "Q";
            promptAction.text = "충전하고 하루 끝내기";
        }
        else if (player.NearestSpot) SpotPrompt(player.NearestSpot);
        else if (player.NearestDevice)
        {
            var d = player.NearestDevice;
            promptTitle.text = d.Broken ? $"{d.Name} · 고장!" : $"{d.Name} · 세기 {d.LevelName}";
            promptBody.text = d.Broken
                ? $"고장 나서 {d.Stat} 수치가 점점 나빠지고 있어요.\n그대로 두면 꽃이 느리게 자라요."
                : $"세기를 바꾸면 {d.Stat} 수치를 맞춰요(약·중·강).\n꽃마다 좋아하는 {d.Stat} 수치가 달라요.";
            promptAction.text = d.Broken ? "고치기" : "세기 바꾸기";
        }
        else if (player.NearLake)
        {
            promptTitle.text = "호수";
            promptBody.text = $"물뿌리개 Lv{gm.CanLevel} · {gm.CanWater}/{gm.CanCapacity}회\n여기서 물뿌리개를 가득 채울 수 있어요.\n(미션 생존선을 넘어 받은 강화권으로 U 창에서 강화)";
            promptAction.text = gm.CanWater >= gm.CanCapacity ? "이미 가득 찼어요" : "물 채우기";
        }
        else if (player.NearestNode)
        {
            var node = player.NearestNode;
            var mat = Progression.GetMaterial(node.materialId);
            var tool = Progression.ToolFor(node.materialId);
            bool has = !node.needsTool || gm.Tools.Contains(tool.id);
            promptTitle.text = node.needsTool ? mat.name : $"작은 {mat.name}";
            promptBody.text = !node.needsTool ? "땅에 박힌 작은 돌이에요.\n도구 없이 주울 수 있어요. (다음 날 다시 생겨요)"
                : has ? $"오늘 남은 양 {node.Amount}/{node.maxAmount}\n(다음 날 다시 차요)"
                : $"'{tool.name}'이(가) 있어야 캘 수 있어요.\n미션 업그레이드선을 넘으면 받아요.";
            promptAction.text = !node.needsTool ? "줍기" : has ? "채집하기" : "도구 없음";
        }
        else
        {
            promptBox.SetActive(false);
            return;
        }
        promptBox.SetActive(true);
    }

    void SpotPrompt(PlantSpot spot)
    {
        if (spot.IsEmpty)
        {
            promptTitle.text = "빈 밭";
            var held = gm.Inv.HeldPlant;
            promptBody.text = $"빛 {spot.Sun:P0} · 온도 {spot.Temp:P0}\n" +
                (held ? $"심을 씨앗: {held.displayName} ×{gm.SeedCount(held)}" : "손에 씨앗이 없어요") +
                "\n(숫자 키 1~9 · Esc 가방에서 들기)";
            promptAction.text = "씨앗 심기";
            return;
        }
        var p = spot.Plant;
        if (spot.IsRotten)
        {
            promptTitle.text = $"{p.displayName} · 썩음";
            promptBody.text = "물을 너무 많이 줬어요.\n수분이 100%일 때 또 주면 썩어요.";
            promptAction.text = "치우기";
            return;
        }
        promptTitle.text = $"{(spot.IsBloomed && spot.IsVariant ? p.variantName : p.displayName)} · {PlantData.StageNames[spot.Stage]} {spot.Stage + 1}/5";
        float boost = spot.FertilizerGrade > 0 ? Progression.FertilizerBoost(spot.FertilizerGrade) : 1f;
        promptBody.text = $"맞춘 환경 {spot.InRangeCount()}/3 · 성장 ×{PlantSpot.GrowthRate(spot.InRangeCount()) * boost:0.0#}\n" +
                          (spot.FertilizerGrade > 0 ? $"비료 {spot.FertilizerGrade}단계 줌" : spot.CanFertilize ? "F 비료 주기 (빨리 자라요)" : "");
        envRows.SetActive(true);
        float[] values = { spot.Water, spot.Sun, spot.Temp };
        Vector2[] ranges = { p.waterRange, p.sunRange, p.tempRange };
        for (int i = 0; i < 3; i++)
        {
            UiKit.SetBar(envBar[i], values[i]);
            envRange[i].anchorMin = new Vector2(ranges[i].x, 0);
            envRange[i].anchorMax = new Vector2(ranges[i].y, 1);
            envRange[i].offsetMin = envRange[i].offsetMax = Vector2.zero;
            envText[i].text = $"{values[i]:P0}";
        }
        promptAction.text = spot.IsBloomed ? "수확하기"
            : !gm.Inv.HeldCan ? "물뿌리개를 들어야 물 줘요"
            : gm.CanWater <= 0 ? "물뿌리개 비었음 → 호수에서 채우기"
            : spot.Water >= 1f ? $"물 주기 {gm.CanWater}/{gm.CanCapacity} (! 썩어요)"
            : $"물 주기 (물뿌리개 {gm.CanWater}/{gm.CanCapacity})";
    }

    void UpdateToast()
    {
        var msg = player.CurrentMessage;
        toast.gameObject.SetActive(msg != null);
        if (msg == null) { shownMessage = null; return; }
        if (msg != shownMessage)
        {
            shownMessage = msg;
            toastText.text = msg;
            toast.localScale = Vector3.one * 0.6f; // 톡 튀어나오기
        }
        toast.localScale = Vector3.Lerp(toast.localScale, Vector3.one, 14f * Time.deltaTime);
    }

    void UpdateBook()
    {
        for (int i = 0; i < gm.Plants.Count; i++)
        {
            var p = gm.Plants[i];
            bool found = gm.Discovered.Contains(p.id);
            bookIcon[i].color = found ? Color.white : new Color(0.15f, 0.12f, 0.1f, 0.5f);
            string variant = gm.VariantsFound.Contains(p.id) ? $"\n변종: {p.variantName}" : "";
            bookText[i].text = found ? $"{p.displayName}\n{new string('★', p.rarity)}{variant}" : "???\n아직 못 피웠어요";
        }
    }

    void UpdateUpgrade()
    {
        upgradeTools.text = "가진 도구: " + (gm.Tools.Count > 0 ? string.Join(", ", gm.Tools.Select(t => Progression.GetTool(t).name)) : "없음 (미션 업그레이드선을 넘으면 받아요)");
        var bps = Progression.Blueprints;
        for (int i = 0; i < bps.Length; i++)
        {
            var bp = bps[i];
            if (!gm.Blueprints.Contains(bp.id)) { upgradeText[i].text = "???\n\n설계키트가 아직 없어요"; continue; }
            string cost = string.Join("\n", bp.cost.Select(c =>
                $"{(gm.MaterialCount(c.materialId) >= c.count ? "●" : "○")} {Progression.GetMaterial(c.materialId).name} {gm.MaterialCount(c.materialId)}/{c.count}"));
            upgradeText[i].text = $"{bp.name}\n\n{bp.effect}\n\n{(gm.HasUpgrade(bp.id) ? "● 완성!" : cost)}";
        }

        string next = gm.CanMaxed ? "● 최고 단계!" :
            $"다음 Lv{gm.CanLevel + 1}: {Progression.CanCapacity[gm.CanLevel]}회\n\n" +
            $"{(gm.CanTokens >= Progression.CanUpgradeCost ? "●" : "○")} 강화권 {gm.CanTokens}/{Progression.CanUpgradeCost}";
        upgradeCanText.text = $"물뿌리개 Lv{gm.CanLevel}\n\n지금 {gm.CanCapacity}회 사용\n(미션 생존선 통과 = 강화권 1장)\n\n{next}";
    }

    void ShowEnding()
    {
        if (ending.activeSelf) return;
        foreach (Transform child in root) child.gameObject.SetActive(false);
        ending.SetActive(true);

        var e = Endings.First(x => x.ending == gm.Result);
        var tex = Resources.Load<Texture2D>(e.image);
        endingBg.color = e.bg;
        endingImage.texture = tex;
        endingImage.enabled = tex;

        string reason = gm.OutOfFuel ? $"{gm.Day}일차에 연료가 바닥났어요" : "100일이 지나 지구로 돌아왔어요";
        endingCard.text =
            $"<size=46>성적표</size>\n{reason}\n\n" +
            $"행성 생명력   {gm.HabitabilityScore} / 40\n" +
            $"도감   {gm.BookScore} / 20  ({gm.Discovered.Count}/{gm.Plants.Count}종)\n" +
            $"변종   {gm.VariantScore} / 20  ({gm.VariantsFound.Count}종)\n" +
            $"미션 초과 점수   {gm.MissionScore} / 20  (+{gm.MissionBonus})\n\n" +
            $"<size=40>총점 {gm.Score}점</size>\n<color=#488C3E>{e.title}</color>\n\n{e.text}\n\n\n";
    }
}
