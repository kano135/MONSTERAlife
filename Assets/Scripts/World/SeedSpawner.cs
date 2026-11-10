using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 행성 곳곳에 씨앗을 듬성듬성 랜덤으로 뿌린다. 주워 가면 시간이 지나 다른 자리에 다시 생긴다.
// 흔한 식물(희귀도 낮음)일수록 자주 나온다.
public class SeedSpawner : MonoBehaviour
{
    public int maxSeeds = 60;
    public float respawnSeconds = 20f;
    public float baseClearance = 8f; // 기지 울타리(비닐하우스 포함)에서 이만큼 더 떨어진 곳에만 생긴다
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
        // 기지 울타리 안·근처, 호수·절벽·마을에는 안 생기게 여러 번 다시 뽑는다. 끝내 못 찾으면 이번엔 안 만든다.
        Vector3 pos = default;
        bool found = false;
        float minRadius = PrototypeBootstrap.FenceRadius + baseClearance;
        for (int tries = 0; tries < 20 && !found; tries++)
        {
            float angle = Random.value * Mathf.PI * 2;
            float dist = Random.Range(minRadius, maxRadius);
            pos = PrototypeBootstrap.BaseCenter + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
            found = !Lake.Overlaps(pos, 1f) && !Cliff.Blocks(pos, 1f) && !Village.Blocks(pos, 1f) && OnGround(ref pos);
        }
        if (!found) return;

        GameObject seed;
        var art = ArtLibrary.World("Props/seed");
        // 씨앗은 크게, 땅에 살짝 박힌 모양(아래쪽 SeedBuried 만큼이 땅속). 움직이지 않는다.
        if (art)
        {
            // 씨앗 그림은 하나라서 식물 색을 살짝 입혀 구분한다
            seed = new GameObject();
            seed.transform.position = pos + Vector3.down * SeedHeight * SeedBuried;
            Billboard.CreateByHeight(seed.transform, art, SeedHeight, Color.Lerp(Color.white, plant.color, 0.45f));
        }
        else
        {
            seed = PrototypeBootstrap.Part(null, PrimitiveType.Sphere, pos + Vector3.up * SeedHeight * (0.5f - SeedBuried), new Vector3(0.75f, 1f, 0.75f) * SeedHeight, plant.color);
            Destroy(seed.GetComponent<Collider>());
        }
        seed.name = $"Seed_{plant.id}";
        var pickup = seed.AddComponent<SeedPickup>();
        pickup.plant = plant;
        alive.Add(pickup);
    }

    const float SeedHeight = 1.0f;  // 씨앗 그림 높이(m). 1.3 → 1.0 (조금 줄임)
    const float SeedBuried = 0.3f;  // 그중 땅에 묻히는 비율

    // 위에서 아래로 쏴서 씨앗 자리를 땅 높이에 맞춘다. 바위 위·나무 줄기처럼 땅이 아니면 false → 다시 뽑기.
    static bool OnGround(ref Vector3 pos)
    {
        if (!Physics.Raycast(new Vector3(pos.x, 30f, pos.z), Vector3.down, out var hit, 60f)) return true;
        if (hit.point.y > 0.2f) return false;
        pos.y = hit.point.y;
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
