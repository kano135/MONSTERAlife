using System.Linq;
using UnityEngine;

// ★ 테스트용 '스토리 빨리 보기'(Progression.TestStoryRun 이 true 일 때만 켜진다).
// 게임에 들어가자마자: 첫날 할 일을 다 끝낸 것으로 하고, 이야기를 1일차부터 새로 시작한다.
// 이야기 카드를 닫거나 고르면 잠깐 뒤 다음 이야기가 있는 날로 바로 넘어간다(잠·미션·연료 걱정 없이).
// 95일차 마지막 선택 뒤에는 100일차로 넘어가 결말이 나온다(함께 잠들기는 그 자리에서).
// 이 모드에서는 저장하지 않는다(원래 저장은 그대로 남는다).
public class StoryAutoRun : MonoBehaviour
{
    const float Pause = 1.2f; // 카드를 닫은 뒤 다음 날로 넘어가기까지(초)

    bool started;
    float wait;

    void Update()
    {
        var gm = GameManager.Instance;
        if (!gm || gm.IsGameOver || !StoryUI.Instance) return;
        if (!started)
        {
            if (TitleScreen.Showing) return;
            Begin(gm);
            started = true;
            wait = Pause;
            return;
        }
        if (Hud.AnyPanelOpen) { wait = Pause; return; } // 카드가 떠 있는 동안 기다린다
        wait -= Time.unscaledDeltaTime;
        if (wait > 0) return;
        wait = Pause;

        int next = StoryDirector.NextBeatDay(gm);
        if (next > 0)
        {
            gm.SkipToDay(next);
            gm.Player.GetComponent<PlayerController>().Show($"{next}일차로 넘어갔어요 (스토리 빨리 보기)", 2.5f);
        }
        else if (gm.StoryFinal != 3)
        {
            // 이야기를 다 봤다 → 100일차가 지나며 결말(마지막 선택이 메신저면 메신저, 아니면 정복)
            gm.SkipToDay(Progression.TotalDays);
            gm.EndDay(false);
        }
    }

    static void Begin(GameManager gm)
    {
        gm.SkipToDay(1);
        gm.StorySeen.Clear();
        gm.StoryTrust = 0;
        gm.StoryChildFlower = false;
        gm.StoryFinal = 0;
        gm.TestSendDone = false;
        for (int i = 0; i < Objectives.GoalCount; i++) gm.ObjectivesDone.Add(i); // 첫날 할 일 끝
        // 1일차: 기지 문 앞 원주민의 인사(원래는 다가가야 뜬다)
        gm.StorySeen.Add(NativeGreeter.StoryKey);
        StoryUI.Instance.Narrate(NativeGreeter.Title, NativeGreeter.Body, new Color32(72, 140, 62, 255));
        gm.Player.GetComponent<PlayerController>().Show("스토리 빨리 보기: 카드를 닫으면 다음 이야기로 넘어가요", 4f);
    }
}
