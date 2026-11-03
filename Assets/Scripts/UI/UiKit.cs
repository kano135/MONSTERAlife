using UnityEngine;
using UnityEngine.UI;

// 둥글둥글한 UI 부품(프루트버스 느낌: 크림색 판, 진한 테두리, 아래 그림자, 굵은 글씨).
public static class UiKit
{
    public static readonly Color Cream = new Color32(255, 246, 224, 255);
    public static readonly Color Ink = new Color32(74, 52, 40, 255);
    public static readonly Color Shadow = new Color32(74, 52, 40, 90);
    public static readonly Color Green = new Color32(124, 195, 91, 255);
    public static readonly Color DarkGreen = new Color32(72, 140, 62, 255);
    public static readonly Color Orange = new Color32(255, 159, 67, 255);
    public static readonly Color Yellow = new Color32(255, 211, 78, 255);
    public static readonly Color Pink = new Color32(255, 122, 162, 255);
    public static readonly Color Sky = new Color32(110, 198, 240, 255);
    public static readonly Color Muted = new Color32(232, 216, 190, 255);

    static Font font;
    static Sprite rounded;

    // shortcut: 윈도우에 깔린 맑은 고딕을 빌려 쓴다(게임에 넣지 않음). 다른 PC용 빌드 전에 무료 한글 글꼴을 넣는다.
    public static Font Font => font ??= Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 32);

    public static Sprite Rounded => rounded ??= MakeRounded(64, 24);

    static Sprite MakeRounded(int size, int radius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
            float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
            tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - d + 0.5f)));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, Vector4.one * radius);
    }

    public static RectTransform Rect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // 앵커(0~1)와 크기·위치로 놓기
    public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static RectTransform Stretch(this RectTransform rt, float inset = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.one * inset;
        rt.offsetMax = -Vector2.one * inset;
        return rt;
    }

    public static Image Fill(Transform parent, string name, Color color)
    {
        var img = Rect(parent, name).gameObject.AddComponent<Image>();
        img.sprite = Rounded;
        img.type = Image.Type.Sliced;
        img.color = color;
        return img;
    }

    // 그림자 + 테두리 + 속 판. 돌려주는 건 속 판(여기에 내용을 단다).
    public static Image Panel(Transform parent, string name, Color fill, Color? outline = null, float border = 5)
    {
        var root = Rect(parent, name);
        var shadow = Fill(root, "Shadow", Shadow);
        shadow.rectTransform.Stretch();
        shadow.rectTransform.anchoredPosition = new Vector2(0, -7);
        shadow.raycastTarget = false;
        Fill(root, "Outline", outline ?? Ink).rectTransform.Stretch();
        var inner = Fill(root, "Fill", fill);
        inner.rectTransform.Stretch(border);
        return inner;
    }

    public static RectTransform Root(this Image panelFill) => (RectTransform)panelFill.transform.parent;

    public static Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
    {
        var t = Rect(parent, "Label").gameObject.AddComponent<Text>();
        t.font = Font;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.color = color;
        t.alignment = anchor;
        t.text = text;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.lineSpacing = 1.1f;
        return t;
    }

    public static Image Icon(Transform parent, Sprite sprite, Vector2 anchor, Vector2 pos, float size)
    {
        var img = Rect(parent, "Icon").gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.rectTransform.Place(anchor, pos, Vector2.one * size);
        img.enabled = sprite;
        return img;
    }

    // 둥근 막대기. 돌려준 채움 막대의 anchorMax.x 로 0~1 을 정한다(SetBar).
    public static RectTransform Bar(Transform parent, Color back, Color fill)
    {
        var bg = Fill(parent, "Bar", back);
        var inner = Fill(bg.transform, "Value", fill);
        inner.rectTransform.anchorMin = Vector2.zero;
        inner.rectTransform.anchorMax = Vector2.one;
        inner.rectTransform.offsetMin = Vector2.one * 3;
        inner.rectTransform.offsetMax = -Vector2.one * 3;
        return inner.rectTransform;
    }

    public static void SetBar(RectTransform value, float t)
    {
        t = Mathf.Clamp01(t);
        value.anchorMax = new Vector2(t, 1);
        value.gameObject.SetActive(t > 0.01f);
    }

    public static Button Button(Transform parent, string text, Color color, System.Action onClick)
    {
        var fill = Panel(parent, "Button", color, Ink, 4);
        var btn = fill.Root().gameObject.AddComponent<Button>();
        btn.targetGraphic = fill;
        btn.onClick.AddListener(() => onClick());
        Label(fill.transform, text, 26, Ink, TextAnchor.MiddleCenter).rectTransform.Stretch();
        return btn;
    }
}
