using UnityEngine;
using UnityEngine.UI;

// 시작 화면: 배경 그림 + 시작 / 종료 버튼. 떠 있는 동안 게임 시간은 멈춘다(Time.timeScale = 0).
// 배경 그림은 Assets/Resources/Art/UI/title_bg.png 를 같은 이름으로 바꿔 넣으면 된다(화면을 꽉 채우게 잘라서 보여 줌).
// 그림이 없으면 하늘색 바탕에 로고(UI/logo)를 띄운다.
public class TitleScreen : MonoBehaviour
{
    public const string BackgroundPath = "UI/title_bg";

    // 시작 화면이 떠 있는지(Hud.AnyPanelOpen 에 들어가서 로봇·카메라가 멈추고 마우스가 나온다)
    public static bool Showing { get; private set; }

    GameObject canvasGo;

    void Awake()
    {
#if UNITY_EDITOR
        // 개발용 자동 실행(AutoPlayOnce)일 때는 시작 화면을 건너뛴다
        if (UnityEditor.SessionState.GetBool(DevScreenshots.TourKey, false)) { Showing = false; return; }
#endif
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

        // 바탕: 뒤에 게임 화면이 비치지 않게 먼저 단색으로 덮는다
        var back = UiKit.Rect(root, "Back").gameObject.AddComponent<Image>();
        back.rectTransform.Stretch();
        back.color = new Color32(110, 198, 240, 255);

        var tex = ArtLibrary.Texture(BackgroundPath);
        if (tex)
        {
            // 그림 비율을 지키면서 화면을 꽉 채운다(남는 부분은 잘림)
            var bg = UiKit.Rect(root, "Background").gameObject.AddComponent<RawImage>();
            bg.texture = tex;
            bg.raycastTarget = false;
            var rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = tex.width / (float)tex.height;
        }
        else
        {
            UiKit.Icon(root, ArtLibrary.Ui("UI/logo"), new Vector2(0.5f, 0.5f), new Vector2(0, 160), 700);
        }

        // 버튼: 화면 아래 가운데에 나란히
        var start = UiKit.Button(root, "시작하기", UiKit.Yellow, StartGame);
        ((RectTransform)start.transform).Place(new Vector2(0.5f, 0), new Vector2(-170, 90), new Vector2(300, 90));
        start.GetComponentInChildren<Text>().fontSize = 36;
        var quit = UiKit.Button(root, "종료", UiKit.Cream, QuitGame);
        ((RectTransform)quit.transform).Place(new Vector2(0.5f, 0), new Vector2(170, 90), new Vector2(300, 90));
        quit.GetComponentInChildren<Text>().fontSize = 36;
    }

    void StartGame()
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
