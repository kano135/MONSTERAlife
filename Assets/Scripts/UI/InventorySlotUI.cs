using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 물건 칸 하나(아래 칸·가방 칸 공용). 마우스로 끌어서 다른 칸에 놓으면 자리가 바뀌고, 클릭하면 손에 든다.
public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public int index;
    public bool grow;
    public Image fill, icon;
    public Text count;
    public System.Action<int> onUse;     // 클릭
    public System.Action<int, int> onMove; // 끌어 놓기(from, to)
    public RectTransform dragLayer;      // 끄는 동안 그림이 따라다닐 곳(화면 맨 위)

    static Image ghost;
    static int dragFrom = -1;

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left) onUse?.Invoke(index);
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (!icon.enabled) return; // 빈 칸은 못 끈다
        dragFrom = index;
        ghost = UiKit.Rect(dragLayer, "DragGhost").gameObject.AddComponent<Image>();
        ghost.sprite = icon.sprite;
        ghost.color = icon.color;
        ghost.preserveAspect = true;
        ghost.raycastTarget = false; // 놓을 칸이 마우스를 받게
        ghost.rectTransform.sizeDelta = icon.rectTransform.sizeDelta;
        ghost.rectTransform.position = e.position;
        ghost.transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData e)
    {
        if (ghost) ghost.rectTransform.position = e.position;
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (ghost) Destroy(ghost.gameObject);
        ghost = null;
        dragFrom = -1;
    }

    public void OnDrop(PointerEventData e)
    {
        if (dragFrom >= 0 && dragFrom != index) onMove?.Invoke(dragFrom, index);
    }
}
