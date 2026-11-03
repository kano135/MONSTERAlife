using UnityEngine;

// 행성에서 캐는 재료 덩어리(고철 더미·광석 바위·얼음 결정). 맞는 도구가 있어야 캔다(GameManager.Gather).
// 하루에 maxAmount 번 캘 수 있고, 다음 날 다시 찬다.
public class ResourceNode : MonoBehaviour
{
    public string materialId;
    public int maxAmount = 3;

    public int Amount { get; set; }

    void Awake() => Amount = maxAmount;

    public void Take() => Amount--;

    public void Refill() => Amount = maxAmount;
}
