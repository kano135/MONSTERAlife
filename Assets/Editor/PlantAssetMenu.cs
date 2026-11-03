using System.IO;
using UnityEditor;
using UnityEngine;

// 메뉴 [몬스테라 > 식물 10종 데이터 만들기]: 기본 10종을 Assets/Resources/Plants 에 에셋으로 꺼낸다.
// 그 뒤로는 에셋에서 수치를 고치고 단계별 모델(stagePrefabs)·아이콘을 끼우면 된다.
public static class PlantAssetMenu
{
    const string Folder = "Assets/Resources/Plants";

    [MenuItem("몬스테라/식물 10종 데이터 만들기")]
    static void CreatePlantAssets()
    {
        Directory.CreateDirectory(Folder);
        int created = 0;
        foreach (var p in PlantCatalog.CreateDefaults())
        {
            var path = $"{Folder}/{p.id}.asset";
            if (File.Exists(path)) continue; // 이미 고친 에셋은 덮어쓰지 않는다
            AssetDatabase.CreateAsset(p, path);
            created++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"식물 데이터 {created}개 만듦 → {Folder}");
    }

    [MenuItem("몬스테라/저장 파일 지우기")]
    static void DeleteSave()
    {
        var path = Path.Combine(Application.persistentDataPath, "save.json");
        if (File.Exists(path) && EditorUtility.DisplayDialog("저장 지우기", "진행 상황을 지우고 처음부터 시작할까요?", "지우기", "취소"))
            File.Delete(path);
    }
}
