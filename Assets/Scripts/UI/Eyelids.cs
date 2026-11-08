using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 눈꺼풀: 위아래 검은 판이 닫히고 열린다. 잠들 때(새벽 2시·충전기·연료 바닥)와 기지 문을 드나들 때 쓴다.
// 화면 맨 위(시작 화면보다도 위)에 그린다.
public class Eyelids : MonoBehaviour
{
    public static Eyelids Instance { get; private set; }

    RectTransform top, bottom;
    Text zzz;
    GameObject canvasGo;
    float closed; // 0 = 다 뜸, 1 = 다 감음

    public bool IsClosed => closed >= 0.999f;

    public static Eyelids Build()
    {
        var e = new GameObject("Eyelids").AddComponent<Eyelids>();
        Instance = e;
        e.canvasGo = new GameObject("EyelidsCanvas", typeof(Canvas), typeof(CanvasScaler));
        e.canvasGo.transform.SetParent(e.transform, false);
        var canvas = e.canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = e.canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)e.canvasGo.transform;
        e.top = Lid(root, "Top", new Vector2(0, 1));
        e.bottom = Lid(root, "Bottom", new Vector2(0, 0));
        e.zzz = UiKit.Label(root, "", 48, new Color(1, 1, 1, 0.9f), TextAnchor.MiddleCenter);
        e.zzz.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 200));
        e.Set(0);
        return e;
    }

    static RectTransform Lid(RectTransform root, string name, Vector2 pivot)
    {
        var img = UiKit.Rect(root, name).gameObject.AddComponent<Image>();
        img.color = new Color(0.05f, 0.04f, 0.08f);
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0, pivot.y);
        rt.anchorMax = new Vector2(1, pivot.y);
        rt.pivot = pivot;
        return rt;
    }

    // 감은 정도 0~1 → 위아래 판 높이(반씩, 살짝 겹치게)
    void Set(float t)
    {
        closed = t;
        float h = Mathf.SmoothStep(0, 1, t) * 560f;
        top.sizeDelta = new Vector2(0, h);
        bottom.sizeDelta = new Vector2(0, h);
        canvasGo.SetActive(t > 0.001f);
    }

    public IEnumerator Close(float seconds, string text = "")
    {
        zzz.text = "";
        for (float t = closed; t < 1f; t += Time.unscaledDeltaTime / seconds) { Set(t); yield return null; }
        Set(1);
        zzz.text = text;
    }

    public IEnumerator Open(float seconds)
    {
        zzz.text = "";
        for (float t = closed; t > 0f; t -= Time.unscaledDeltaTime / seconds) { Set(t); yield return null; }
        Set(0);
    }

    // 엔딩 화면을 보여 줄 때: 눈꺼풀을 치운다
    public void Hide() { zzz.text = ""; Set(0); }
}
