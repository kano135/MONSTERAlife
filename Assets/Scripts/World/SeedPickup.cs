using System.Collections.Generic;
using UnityEngine;

// 탐험하다 줍는 씨앗. 땅에 살짝 박혀 있고, 가까이 가서 E를 눌러야 주워진다(PlayerController).
public class SeedPickup : MonoBehaviour
{
    // 지금 맵에 있는 씨앗 전부(로봇이 가장 가까운 것을 찾을 때 쓴다)
    public static readonly List<SeedPickup> All = new List<SeedPickup>();

    public PlantData plant;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    // E로 줍기: 가방(물건 칸)에 넣고 사라진다. 화면에 띄울 말을 돌려준다.
    public string Pick()
    {
        var gm = GameManager.Instance;
        gm.AddSeed(plant, 1);
        gm.CountSeedsPicked++;
        Destroy(gameObject);
        return $"{plant.displayName} 씨앗을 주웠어요";
    }
}
