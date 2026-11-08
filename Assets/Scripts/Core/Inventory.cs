using System.Collections.Generic;
using UnityEngine;

// 가진 물건의 자리(칸) 순서. 개수는 GameManager 가 그대로 세고(씨앗·재료·비료), 여기서는 "무엇이 몇 번 칸에 있나"만 기억한다.
//   0~8 = 아래 칸(숫자 키 1~9), 9~35 = 가방(Esc 메뉴 '가방' 탭).
//   처음 얻은 물건은 비어 있는 앞 칸부터 차례로 들어간다(번호를 미리 정해 두지 않음). 다 쓰면 그 칸은 비워진다.
//   가방 탭에서 마우스로 끌어 자리를 바꾸고, 클릭하면 손에 든다.
// 칸에 들어가는 열쇠: "tool:can"(물뿌리개, 늘 1개), "seed:<식물id>", "mat:<재료id>", "fert:<단계>". 빈 칸은 "".
public class Inventory
{
    public const int HotbarSize = 9;
    public const int BagSize = 27;
    public const int Size = HotbarSize + BagSize;

    public readonly string[] Slots = new string[Size];
    public int Selected { get; private set; } // 손에 든 아래 칸(0~8)

    readonly GameManager gm;

    public Inventory(GameManager gm)
    {
        this.gm = gm;
        for (int i = 0; i < Size; i++) Slots[i] = "";
    }

    public const string CanKey = "tool:can";
    public static string SeedKey(PlantData p) => "seed:" + p.id;
    public static string MaterialKey(string id) => "mat:" + id;
    public static string FertilizerKey(int grade) => "fert:" + grade;

    public string HeldKey => Slots[Selected];
    public PlantData HeldPlant => PlantOf(HeldKey);
    public bool HeldCan => HeldKey == CanKey;
    public int HeldFertilizer => HeldKey.StartsWith("fert:") ? int.Parse(HeldKey.Substring(5)) : 0;

    public void Select(int hotbarIndex) => Selected = Mathf.Clamp(hotbarIndex, 0, HotbarSize - 1);

    // 다음 물건 있는 아래 칸으로(Tab)
    public void SelectNextFilled()
    {
        for (int i = 1; i <= HotbarSize; i++)
        {
            int next = (Selected + i) % HotbarSize;
            if (Slots[next] != "") { Selected = next; return; }
        }
    }

    public void Swap(int a, int b)
    {
        if (a == b || a < 0 || b < 0 || a >= Size || b >= Size) return;
        (Slots[a], Slots[b]) = (Slots[b], Slots[a]);
    }

    // 클릭해서 쓰기 = 손에 들기. 아래 칸이면 그 칸을 고르고, 가방 칸이면 지금 손에 든 칸과 자리를 바꿔 손에 든다.
    public string Use(int index)
    {
        if (index < 0 || index >= Size) return null;
        if (Slots[index] == "") return null;
        if (index < HotbarSize) Selected = index;
        else Swap(index, Selected);
        string key = HeldKey;
        if (key == CanKey) return "물뿌리개를 들었어요. 자라는 꽃 앞에서 E로 물을 줘요";
        if (key.StartsWith("mat:")) return $"{Name(key)}을(를) 들었어요. 업그레이드(U)에 쓰는 재료예요";
        if (key.StartsWith("fert:")) return $"{Name(key)}을(를) 들었어요. 자라는 꽃 앞에서 F로 줘요";
        return $"{Name(key)}을(를) 들었어요. 빈 밭 앞에서 E로 심어요";
    }

    public int Count(string key)
    {
        if (key == CanKey) return 1; // 물뿌리개는 늘 가지고 있다
        if (key.StartsWith("seed:")) { var p = PlantOf(key); return p ? gm.SeedCount(p) : 0; }
        if (key.StartsWith("mat:")) return gm.MaterialCount(key.Substring(4));
        if (key.StartsWith("fert:")) return gm.FertilizerCount(int.Parse(key.Substring(5)));
        return 0;
    }

    // 매 프레임: 다 쓴 물건 칸은 비우고, 새로 생긴 물건은 빈 앞 칸에 넣는다
    public void Sync()
    {
        for (int i = 0; i < Size; i++)
            if (Slots[i] != "" && Count(Slots[i]) <= 0) Slots[i] = "";
        foreach (var key in AllKeys())
            if (Count(key) > 0 && System.Array.IndexOf(Slots, key) < 0)
            {
                int free = System.Array.IndexOf(Slots, "");
                if (free < 0) return; // 가방까지 꽉 참(36종) — 그런 일은 거의 없다
                Slots[free] = key;
            }
    }

    IEnumerable<string> AllKeys()
    {
        yield return CanKey; // 새 판이면 1번 칸
        foreach (var p in gm.Plants) yield return SeedKey(p);
        foreach (var m in Progression.Materials) yield return MaterialKey(m.id);
        for (int g = 1; g <= Progression.FertilizerGrades; g++) yield return FertilizerKey(g);
    }

    PlantData PlantOf(string key) => key.StartsWith("seed:") ? gm.Plants.Find(p => p.id == key.Substring(5)) : null;

    // ───────── 보여 주기 ─────────

    public string Name(string key)
    {
        if (key == CanKey) return "물뿌리개";
        if (key.StartsWith("seed:")) { var p = PlantOf(key); return p ? $"{p.displayName} 씨앗" : "씨앗"; }
        if (key.StartsWith("mat:")) return Progression.GetMaterial(key.Substring(4)).name;
        if (key.StartsWith("fert:")) return $"비료 {key.Substring(5)}단계";
        return "";
    }

    // 그림: 씨앗은 그 식물 아이콘, 비료는 Icons/fertilizer, 재료는 Icons/mat_<id>(없으면 null → 색 동그라미)
    public Sprite Icon(string key)
    {
        if (key == CanKey) return ArtLibrary.Ui("Icons/wateringcan");
        if (key.StartsWith("seed:")) { var p = PlantOf(key); return p ? ArtLibrary.PlantIcon(p) : null; }
        if (key.StartsWith("mat:")) return ArtLibrary.Ui("Icons/mat_" + key.Substring(4));
        if (key.StartsWith("fert:")) return ArtLibrary.Ui("Icons/fertilizer");
        return null;
    }

    public Color Tint(string key)
    {
        if (key == CanKey) return new Color(0.43f, 0.78f, 0.94f);
        if (key.StartsWith("seed:")) { var p = PlantOf(key); return p ? p.color : Color.white; }
        switch (key)
        {
            case "mat:scrap": return new Color(0.55f, 0.38f, 0.22f); // 나무
            case "mat:ore": return new Color(0.6f, 0.58f, 0.55f);    // 돌멩이
            case "mat:ice": return new Color(0.72f, 0.92f, 1f);      // 얼음 결정
        }
        return new Color(0.6f, 0.8f, 0.35f); // 비료
    }

    // ───────── 저장 ─────────

    public List<string> ToSave() => new List<string>(Slots);

    public void FromSave(List<string> saved, int selected)
    {
        if (saved != null)
            for (int i = 0; i < Size && i < saved.Count; i++) Slots[i] = saved[i] ?? "";
        Select(selected);
        Sync();
    }
}
