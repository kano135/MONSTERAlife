using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 시작 화면: 배경 그림(UI/title_main — 로봇이 지구를 바라보는 그림) 위에 종이 메뉴 카드.
//   새 게임 · 이어 하기 · 설정 · 종료. 마우스로 누르거나, ↑↓(W/S)로 고르고 Enter/Space/E 로 정한다.
//   새 게임은 저장이 있으면 한 번 더 눌러야 지운다. 이어 하기는 저장이 없으면 흐리게.
// 떠 있는 동안 게임 시간은 멈춘다(Time.timeScale = 0).
// 그림 속 영어 메뉴 자리를 우리 카드가 그대로 덮는다(그림이 1672×941, 16:9 라서 화면 비율이 같으면 자리가 맞는다).
// 그림이 없으면 예전 그림(UI/title_bg) → 그것도 없으면 하늘색 바탕에 로고.
public class TitleScreen : MonoBehaviour
{
    public const string BackgroundPath = "UI/title_main";
    const string OldBackgroundPath = "UI/title_bg";

    // 시작 화면이 떠 있는지(Hud.AnyPanelOpen 에 들어가서 로봇·카메라가 멈추고 마우스가 나온다)
    public static bool Showing { get; private set; }
    // 새 게임으로 씬을 다시 불러온 직후에는 시작 화면을 건너뛴다
    static bool skipOnce;

    static readonly Color Paper = new Color32(250, 246, 234, 255);
    static readonly Color PaperEdge = new Color32(214, 222, 210, 255);
    static readonly Color Pill = new Color32(200, 230, 214, 255);
    static readonly Color PillOn = new Color32(150, 214, 190, 255);
    static readonly Color PillOff = new Color32(222, 226, 222, 255);
    static readonly Color TealInk = new Color32(52, 120, 104, 255);
    static readonly Color GrayInk = new Color32(150, 160, 156, 255);

    GameObject canvasGo;
    Button[] buttons;
    Image[] pills;
    Text[] labels;
    Text arrow;
    int selected;
    bool newGameArmed;

    void Awake()
    {
#if UNITY_EDITOR
        // 개발용 자동 실행(AutoPlayOnce)일 때는 시작 화면을 건너뛴다
        if (UnityEditor.SessionState.GetBool(DevScreenshots.TourKey, false)) { Showing = false; return; }
#endif
        if (skipOnce) { skipOnce = false; Showing = false; return; }
        Build();
        Showing = true;
        Time.timeScale = 0f;
    }

    void Build()
    {
        canvasGo = new GameObject("TitleScreen", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // 게임 HUD 위
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)canvasGo.transform;

        var back = UiKit.Rect(root, "Back").gameObject.AddComponent<Image>();
        back.rectTransform.Stretch();
        back.color = new Color32(150, 190, 240, 255);

        var tex = ArtLibrary.Texture(BackgroundPath);
        bool mainArt = tex;
        if (!tex) tex = ArtLibrary.Texture(OldBackgroundPath);
        // 뒤판: 같은 그림을 화면을 꽉 채우게 크게 깔고 흐리게(화면 비율이 16:9 가 아닐 때 남는 띠를 비슷한 색으로 채운다)
        if (tex)
        {
            var fill = UiKit.Rect(root, "BackFill").gameObject.AddComponent<RawImage>();
            fill.texture = tex;
            fill.color = new Color(0.78f, 0.8f, 0.88f);
            fill.raycastTarget = false;
            var frt = fill.rectTransform;
            frt.anchorMin = frt.anchorMax = frt.pivot = new Vector2(0.5f, 0.5f);
            var ffit = fill.gameObject.AddComponent<AspectRatioFitter>();
            ffit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            ffit.aspectRatio = tex.width / (float)tex.height;
        }
        // 그림을 담는 16:9 판: 화면 안에 통째로 들어가게(잘리지 않게) 맞춘다. 카드는 이 판 기준으로 놓는다
        var stage = UiKit.Rect(root, "Stage");
        stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
        stage.sizeDelta = new Vector2(1920, 1080);
        var fit = stage.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fit.aspectRatio = 1920f / 1080f;
        var scale = stage.gameObject.AddComponent<StageScale>(); // 판 크기가 바뀌어도 카드·안내가 같은 비율로 따라 줄고 는다
        if (tex)
        {
            var bg = UiKit.Rect(stage, "Background").gameObject.AddComponent<RawImage>();
            bg.texture = tex;
            bg.raycastTarget = false;
            bg.rectTransform.Stretch();
        }
        else UiKit.Icon(root, ArtLibrary.Ui("UI/logo"), new Vector2(0.5f, 0.5f), new Vector2(0, 160), 700);

        // 메뉴 카드: 그림 속 영어 메뉴 자리(판 기준 가운데에서 왼쪽 아래)를 덮는다
        var content = UiKit.Rect(stage, "Content"); // 1920×1080 기준 좌표(판 크기에 맞춰 StageScale 이 키운다)
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 0.5f);
        content.sizeDelta = new Vector2(1920, 1080);
        scale.content = content;
        var card = UiKit.Panel(content, "MenuCard", Paper, PaperEdge, 4);
        var cardRt = card.Root();
        cardRt.anchorMin = cardRt.anchorMax = cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = mainArt ? new Vector2(-392, -150) : new Vector2(0, -150);
        cardRt.sizeDelta = new Vector2(486, 418);
        cardRt.localRotation = Quaternion.Euler(0, 0, 1.2f);
        // 클립
        var clip = UiKit.Rect(cardRt, "Clip").gameObject.AddComponent<Image>();
        clip.color = new Color32(170, 180, 190, 255);
        clip.rectTransform.Place(new Vector2(1, 1), new Vector2(-70, 26), new Vector2(18, 70));
        clip.rectTransform.localRotation = Quaternion.Euler(0, 0, -28f);

        string[] names = { "새 게임", "이어 하기", "설정", "종료" };
        System.Action[] actions = { NewGame, Continue, OpenSettings, QuitGame };
        buttons = new Button[names.Length];
        pills = new Image[names.Length];
        labels = new Text[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var pill = UiKit.Rect(card.transform, "Pill_" + i).gameObject.AddComponent<Image>();
            pill.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(28, -38 - i * 88), new Vector2(310, 70));
            pill.color = Pill;
            var btn = pill.gameObject.AddComponent<Button>();
            btn.targetGraphic = pill;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => { selected = index; actions[index](); });
            var label = UiKit.Label(pill.transform, names[i], 34, TealInk, TextAnchor.MiddleCenter);
            label.rectTransform.Stretch();
            var hover = pill.gameObject.AddComponent<TitleHover>();
            hover.onEnter = () => selected = index;
            buttons[i] = btn; pills[i] = pill; labels[i] = label;
        }
        arrow = UiKit.Label(card.transform, "▶", 34, TealInk, TextAnchor.MiddleCenter);
        arrow.rectTransform.sizeDelta = new Vector2(50, 70);

        // 오른쪽 아래 조작 안내(그림 속 'A Select' 자리)
        var hint = UiKit.Panel(content, "Hint", new Color32(88, 140, 130, 235), new Color32(88, 140, 130, 235), 0);
        var hintRt = hint.Root();
        hintRt.anchorMin = hintRt.anchorMax = hintRt.pivot = new Vector2(0.5f, 0.5f);
        hintRt.anchoredPosition = mainArt ? new Vector2(780, -465) : new Vector2(700, -465);
        hintRt.sizeDelta = new Vector2(300, 66);
        UiKit.Label(hint.transform, "↑↓ 고르기 · Enter 선택", 24, Color.white, TextAnchor.MiddleCenter).rectTransform.Stretch();

        selected = GameManager.SaveExists ? 1 : 0; // 저장이 있으면 '이어 하기'부터
        Refresh();
    }

    void Update()
    {
        if (!canvasGo) return;
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Move(-1);
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Move(1);
            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                buttons[selected].onClick.Invoke();
        }
        Refresh();
    }

    void Move(int dir)
    {
        for (int k = 0; k < buttons.Length; k++)
        {
            selected = (selected + dir + buttons.Length) % buttons.Length;
            if (selected != 1 || GameManager.SaveExists) break; // 저장이 없으면 '이어 하기'는 건너뛴다
        }
    }

    void Refresh()
    {
        bool canContinue = GameManager.SaveExists;
        for (int i = 0; i < pills.Length; i++)
        {
            bool off = i == 1 && !canContinue;
            pills[i].color = off ? PillOff : i == selected ? PillOn : Pill;
            labels[i].color = off ? GrayInk : TealInk;
        }
        labels[0].text = newGameArmed ? "정말? 한 번 더 누르면 새로" : "새 게임";
        labels[0].fontSize = newGameArmed ? 26 : 34;
        var p = pills[selected].rectTransform; // 앵커·기준점이 위 가운데
        arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = arrow.rectTransform.pivot = new Vector2(0.5f, 1);
        arrow.rectTransform.anchoredPosition = p.anchoredPosition + new Vector2(-p.sizeDelta.x / 2f - 34, 0);
    }

    void NewGame()
    {
        if (GameManager.SaveExists && !newGameArmed) { newGameArmed = true; return; } // 저장을 지우기 전에 한 번 더
        skipOnce = true;
        Close();
        if (GameManager.Instance) GameManager.Instance.Restart(); // 저장을 지우고 처음부터(씬 다시 불러오기)
    }

    void Continue()
    {
        if (!GameManager.SaveExists) return;
        Close();
    }

    void OpenSettings()
    {
        Close();
        var hud = FindFirstObjectByType<Hud>();
        if (hud) hud.OpenSettings();
    }

    void Close()
    {
        Showing = false;
        Time.timeScale = 1f;
        if (canvasGo) Destroy(canvasGo);
    }

    static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // 에디터에서는 Play 멈추기
#else
        Application.Quit();
#endif
    }

    void OnDestroy()
    {
        // 씬을 다시 불러올 때(처음부터 다시) 멈춘 시간이 남지 않게
        if (Showing) { Showing = false; Time.timeScale = 1f; }
    }
}

// 16:9 판 안의 것(카드·안내)이 판 크기에 맞춰 함께 줄고 늘게: 판 안에 1920×1080 기준 내용 판을 두고 비율만큼 키운다
public class StageScale : MonoBehaviour
{
    public RectTransform content;
    void LateUpdate()
    {
        if (!content) return;
        var rt = (RectTransform)transform;
        float k = rt.rect.width / 1920f;
        content.localScale = new Vector3(k, k, 1f);
    }
}

// 마우스를 올리면 그 메뉴가 골라진다
public class TitleHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler
{
    public System.Action onEnter;
    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) => onEnter?.Invoke();
}
