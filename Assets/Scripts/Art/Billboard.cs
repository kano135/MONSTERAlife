using UnityEngine;

// 그림을 세워 두고 항상 카메라 쪽을 보게 한다(좌우로만 돈다).
[RequireComponent(typeof(SpriteRenderer))]
public class Billboard : MonoBehaviour
{
    public SpriteRenderer Renderer { get; private set; }

    void Awake() => Renderer = GetComponent<SpriteRenderer>();

    void LateUpdate()
    {
        var cam = Camera.main;
        if (!cam) return;
        var forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (forward.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(forward);
    }

    // 폭(width) 기준으로 크기를 맞춘다. 화분 그림은 폭이 비슷해서 식물끼리 화분 크기가 맞는다.
    public static Billboard Create(Transform parent, Sprite sprite, float width, Color tint)
    {
        var go = new GameObject("Billboard", typeof(SpriteRenderer));
        go.transform.SetParent(parent, false);
        var b = go.AddComponent<Billboard>();
        b.Renderer.sprite = sprite;
        b.Renderer.color = tint;
        float scale = width / sprite.bounds.size.x;
        go.transform.localScale = Vector3.one * scale;
        return b;
    }

    public static Billboard CreateByHeight(Transform parent, Sprite sprite, float height, Color tint)
        => Create(parent, sprite, height * sprite.bounds.size.x / sprite.bounds.size.y, tint);
}
