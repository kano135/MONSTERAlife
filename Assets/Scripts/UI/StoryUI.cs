using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 이야기 카드: 가운데에 제목·글·버튼(선택지)이 뜬다. 떠 있는 동안 시간·로봇이 멈추고 마우스 커서가 나온다.
//   Show(제목, 글, 선택지들, 고른 번호를 받을 함수). 선택지가 하나면 "계속" 카드.
//   숫자 키 1~3 으로도 고르고, 선택지가 하나면 E·Space·Enter 로 넘긴다. 여러 장은 차례로(줄 서서) 뜬다.
public class StoryUI : MonoBehaviour
{
    public static StoryUI Instance { get; private set; }
    public static bool IsOpen => Instance && Instance.panel && Instance.panel.activeSelf;
    // 지금 떠 있는 카드(확인·시험용)
    public static string CurrentTitle => IsOpen ? Instance.current.title : null;
    public static int CurrentOptions => IsOpen ? Instance.current.options.Length : 0;
    public static void Choose(int index) { if (IsOpen) Instance.Pick(index); }

    struct Card { public string title, body; public string[] options; public Action<int> onPick; public Color accent; }
    readonly Queue<Card> queue = new Queue<Card>();

    GameObject panel;
    Image accentBar;
    Text titleText, bodyText;
    readonly List<Button> buttons = new List<Button>();
    Card current;
    float openedAt;

    public static StoryUI Build()
    {
        var ui = new GameObject("StoryUI").AddComponent<StoryUI>();
        Instance = ui;
        var canvasGo = new GameObject("StoryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(ui.transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)canvasGo.transform;

        var dim = UiKit.Rect(root, "Dim").gameObject.AddComponent<Image>();
        dim.rectTransform.Stretch();
        dim.color = new Color(0.08f, 0.06f, 0.12f, 0.45f);
        ui.panel = dim.gameObject;

        var card = UiKit.Panel(dim.transform, "Card", UiKit.Cream, UiKit.Ink, 6);
        card.Root().Place(new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(980, 460));
        ui.accentBar = UiKit.Fill(card.transform, "Accent", UiKit.DarkGreen);
        ui.accentBar.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(940, 10));
        ui.titleText = UiKit.Label(card.transform, "", 36, UiKit.DarkGreen, TextAnchor.MiddleCenter);
        ui.titleText.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(920, 56));
        ui.bodyText = UiKit.Label(card.transform, "", 26, UiKit.Ink, TextAnchor.UpperCenter);
        ui.bodyText.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(900, 230));
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            var b = UiKit.Button(card.transform, "", UiKit.Yellow, () => ui.Pick(index));
            ui.buttons.Add(b);
        }
        ui.panel.SetActive(false);
        return ui;
    }

    public void Show(string title, string body, string[] options, Action<int> onPick, Color? accent = null)
    {
        queue.Enqueue(new Card { title = title, body = body, options = options ?? new[] { "계속" }, onPick = onPick, accent = accent ?? UiKit.DarkGreen });
        if (!IsOpen) Next();
    }

    public void Narrate(string title, string body, Color? accent = null) => Show(title, body, null, null, accent);

    void Next()
    {
        if (queue.Count == 0) { panel.SetActive(false); return; }
        current = queue.Dequeue();
        titleText.text = current.title;
        titleText.color = current.accent;
        accentBar.color = current.accent;
        bodyText.text = current.body;
        int n = current.options.Length;
        float w = n == 1 ? 300 : n == 2 ? 400 : 290, gap = 20;
        for (int i = 0; i < buttons.Count; i++)
        {
            bool on = i < n;
            buttons[i].gameObject.SetActive(on); // 버튼만 끈다(부모는 카드 본문이라 끄면 글까지 사라진다)
            if (!on) continue;
            var label = buttons[i].GetComponentInChildren<Text>();
            label.text = n == 1 ? current.options[i] : $"{i + 1}. {current.options[i]}";
            label.fontSize = 24;
            float x = (i - (n - 1) / 2f) * (w + gap);
            ((RectTransform)buttons[i].transform).Place(new Vector2(0.5f, 0), new Vector2(x, 30), new Vector2(w, 70));
        }
        panel.SetActive(true);
        openedAt = Time.unscaledTime;
    }

    void Pick(int index)
    {
        if (!IsOpen || index >= current.options.Length || Time.unscaledTime - openedAt < 0.25f) return; // 실수로 바로 넘기지 않게
        var pick = current.onPick;
        Next();
        pick?.Invoke(index);
    }

    void Update()
    {
        if (!IsOpen) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        if (current.options.Length == 1 && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) Pick(0);
        if (kb.digit1Key.wasPressedThisFrame) Pick(0);
        if (kb.digit2Key.wasPressedThisFrame) Pick(1);
        if (kb.digit3Key.wasPressedThisFrame) Pick(2);
    }
}
