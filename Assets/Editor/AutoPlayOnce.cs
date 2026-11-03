using System.IO;
using UnityEditor;

// 개발용(작업 확인):
//   프로젝트 폴더에 "autoplay.flag" 가 생기면 → 바뀐 파일을 먼저 가져오고 컴파일까지 끝난 뒤 Play 를 한 번 누른다.
//   "autostop.flag" 가 생기면 → Play 를 멈춘다.
// 두 파일 모두 쓰고 나면 지운다.
[InitializeOnLoad]
static class AutoPlayOnce
{
    const string PlayFlag = "autoplay.flag";
    const string StopFlag = "autostop.flag";

    static AutoPlayOnce() => EditorApplication.update += Check;

    static void Check()
    {
        if (File.Exists(StopFlag) && EditorApplication.isPlaying)
        {
            File.Delete(StopFlag);
            EditorApplication.ExitPlaymode();
            return;
        }
        if (!File.Exists(PlayFlag) || EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        // 바뀐 코드가 있으면 여기서 컴파일이 시작된다 → 끝난 뒤 다시 들어와서 Play
        AssetDatabase.Refresh();
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        File.Delete(PlayFlag);
        SessionState.SetBool("monstera_dev_tour", true); // 자동 실행 때만 확인용 둘러보기(DevScreenshots.TourKey)
        EditorApplication.EnterPlaymode();
    }
}
