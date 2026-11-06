using UnityEngine;

// 행성에서 캐는 재료 덩어리(나무·돌멩이·얼음 결정). 보통은 맞는 도구가 있어야 캔다(GameManager.Gather).
// 하루에 maxAmount 번 캘 수 있고, 다음 날 다시 찬다.
// 작은 돌처럼 도구 없이 줍는 것(needsTool = false)은 다 주우면 사라졌다가(hideWhenEmpty) 다음 날 다시 생긴다.
public class ResourceNode : MonoBehaviour
{
    public string materialId;
    public int maxAmount = 3;
    [Tooltip("끄면 도구 없이 E로 줍는다")]
    public bool needsTool = true;
    [Tooltip("다 가져가면 안 보이게 숨긴다(다음 날 다시 생김)")]
    public bool hideWhenEmpty;

    int amount;
    public int Amount
    {
        get => amount;
        set
        {
            amount = value;
            if (hideWhenEmpty) gameObject.SetActive(amount > 0);
        }
    }

    void Awake() => Amount = maxAmount;

    public void Take() => Amount--;

    public void Refill() => Amount = maxAmount;
}
