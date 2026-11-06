using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 행성 곳곳에 씨앗을 듬성듬성 랜덤으로 뿌린다. 주워 가면 시간이 지나 다른 자리에 다시 생긴다.
// 흔한 식물(희귀도 낮음)일수록 자주 나온다.
public class SeedSpawner : MonoBehaviour
{
    public int maxSeeds = 24;
    public float respawnSeconds = 20f;
    public float minRadius = 19f; // 기지 울타리 밖
    public float maxRadius = GroundView.Size / 2f - 4f;

    List<PlantData> plants;
    readonly List<SeedPickup> alive = new List<SeedPickup>();
    float timer;

    public void Init(List<PlantData> plants)
    {
        this.plants = plants;
        for (int i = 0; i < maxSeeds; i++) Spawn();
    }

    void Update()
    {
        alive.RemoveAll(s => !s);
        if (alive.Count >= maxSeeds) return;
        timer += Time.deltaTime;
        if (timer < respawnSeconds) return;
        timer = 0;
        Spawn();
    }

    void Spawn()
    {
        var plant = PickWeighted();
        // 호수 위에는 안 생기게 몇 번 다시 뽑는다
        Vector3 pos;
        int tries = 0;
        do
        {
            float angle = Random.value * Mathf.PI * 2;
            float dist = Random.Range(minRadius, maxRadius);
            pos = new Vector3(Mathf.Cos(angle) * dist, 0.6f, Mathf.Sin(angle) * dist);
        } while ((Lake.Overlaps(pos, 1f) || !OnGround(ref pos)) && ++tries < 10);

        GameObject seed;
        var art = ArtLibrary.World("Props/seed");
        if (art)
        {
            // 씨앗 그림은 하나라서 식물 색을 살짝 입혀 구분한다
            seed = new GameObject();
            seed.transform.position = pos + Vector3.down * 0.4f;
            Billboard.CreateByHeight(seed.transform, art, 0.8f, Color.Lerp(Color.white, plant.color, 0.45f));
        }
        else
        {
            seed = PrototypeBootstrap.Part(null, PrimitiveType.Sphere, pos, Vector3.one * 0.4f, plant.color);
            Destroy(seed.GetComponent<Collider>());
        }
        seed.name = $"Seed_{plant.id}";
        var pickup = seed.AddComponent<SeedPickup>();
        pickup.plant = plant;
        alive.Add(pickup);
    }

    // 위에서 아래로 쏴서 씨앗을 땅 위에 올린다. 나무 줄기·바위 꼭대기처럼 높은 곳이면 false → 다시 뽑기.
    static bool OnGround(ref Vector3 pos)
    {
        if (!Physics.Raycast(new Vector3(pos.x, 30f, pos.z), Vector3.down, out var hit, 60f)) return true;
        if (hit.point.y > 1.2f) return false;
        pos.y = hit.point.y + 0.6f;
        return true;
    }

    // 희귀도 1 → 가중치 5, 희귀도 5 → 가중치 1
    PlantData PickWeighted()
    {
        int total = plants.Sum(p => 6 - p.rarity);
        int roll = Random.Range(0, total);
        foreach (var p in plants)
        {
            roll -= 6 - p.rarity;
            if (roll < 0) return p;
        }
        return plants[0];
    }
}
