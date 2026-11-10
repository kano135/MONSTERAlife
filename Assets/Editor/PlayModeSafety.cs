using UnityEditor;

// 플레이 중에 코드가 바뀌어도(다른 창에서 고치거나 Claude 가 고칠 때) 게임이 깨지지 않게:
// Unity 설정 'Script Changes While Playing' 을 '플레이가 끝난 뒤에 다시 불러오기(Recompile After Finished Playing)'로 맞춘다.
// (기본값 '그대로 다시 불러오기'는 플레이 중 정적 값이 지워져 로봇이 멈춘다.)
// 이 설정은 이 컴퓨터의 Unity 전체에 적용된다. 바꾸려면 Unity > Settings > General 에서.
[InitializeOnLoad]
static class PlayModeSafety
{
    const string Key = "ScriptCompilationDuringPlay";
    const int RecompileAfterFinishedPlaying = 1;
    const string DoneKey = "monstera_play_safety_set";

    static PlayModeSafety()
    {
        if (EditorPrefs.GetBool(DoneKey, false)) return; // 한 번만(직접 바꾼 설정은 덮어쓰지 않게)
        EditorPrefs.SetInt(Key, RecompileAfterFinishedPlaying);
        EditorPrefs.SetBool(DoneKey, true);
    }
}
