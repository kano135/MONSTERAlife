using UnityEngine;

// 탐험하다 줍는 씨앗. 로봇이 가까이 가면 가방에 들어간다.
public class SeedPickup : MonoBehaviour
{
    public PlantData plant;
    public float pickupRange = 1.3f;

    Vector3 basePosition;

    void Start() => basePosition = transform.position;

    void Update()
    {
        transform.Rotate(0, 90f * Time.deltaTime, 0);
        transform.position = basePosition + Vector3.up * Mathf.Sin(Time.time * 2f) * 0.15f;

        var gm = GameManager.Instance;
        if (!gm || (gm.Player.position - basePosition).sqrMagnitude > pickupRange * pickupRange) return;
        gm.AddSeed(plant, 1);
        gm.Player.GetComponent<PlayerController>().Show($"{plant.displayName} 씨앗을 주웠어요");
        Destroy(gameObject);
    }
}
