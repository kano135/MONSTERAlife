using UnityEngine;

// 원주민 마을: 기지 동쪽 들판(Center)에 원주민들이 모여 있다. 생김새는 NativeModel(코드로 빚은 3D).
public static class Village
{
    public static readonly Vector3 Center = new Vector3(70f, 0, 30f);
    public const float Radius = 8f;

    public static bool Blocks(Vector3 p, float margin)
    {
        var d = p - Center; d.y = 0;
        return d.magnitude < Radius + margin;
    }

    public static void Build()
    {
        var root = new GameObject("Village").transform;
        root.position = Center;
        // 마당에 둥글게 모여 선 원주민 6명(키가 조금씩 다르고, 하나는 아이). 가운데를 보고 서 있다가 로봇이 오면 돌아본다.
        float[] heights = { 1.4f, 1.25f, 1.5f, 0.85f, 1.35f, 1.2f };
        for (int i = 0; i < heights.Length; i++)
        {
            float a = i * Mathf.PI / 3f;
            var native = NativeModel.Build(heights[i]).transform;
            native.SetParent(root, false);
            native.localPosition = new Vector3(Mathf.Cos(a) * 4f, 0, Mathf.Sin(a) * 4f);
            native.localRotation = Quaternion.LookRotation(-native.localPosition);
        }
    }
}

// 첫날 원주민(강제 미션): 로봇이 기지 밖으로 나오면 원주민이 로봇 앞까지 걸어온다.
// 다가오는 동안과 앞에 선 뒤에는 로봇이 멀리 가지 못한다(PlayerController.Leash). E로 말을 걸어야 풀린다.
// 말을 걸면 이야기 카드가 뜨고, 그 뒤로는 자유. 1일차에만 보인다. ★ 하는 말은 Title / Body, 빠르기·거리는 아래 숫자.
public class NativeGreeter : MonoBehaviour
{
    public const int StoryKey = 1; // 이야기 '본 날' 표시(1일차). StoryDirector 의 날짜 표와 겹치지 않는다
    public const string Title = "누군가 말을 걸어요";
    public const string Body = "보랏빛 원주민이 다가와 무언가 말을 걸어요.\n말은 통하지 않지만, 반가워하는 것 같아요.";
    const float WalkSpeed = 3f;      // 걸어오는 빠르기(m/s)
    const float StopDistance = 2.2f; // 로봇 앞 이만큼에서 멈춘다
    public const float TalkRange = 3.5f;
    const float LeashWhileComing = 7f, LeashWhenHere = 5f; // 못 벗어나는 반지름(m)

    public static NativeGreeter Instance { get; private set; }

    // 지금 강제 미션 중인가(원주민이 오는 중이거나 와서 기다리는 중)
    public bool Active { get; private set; }
    // 로봇 앞에 와서 말 걸기를 기다리는 중
    public bool Waiting { get; private set; }

    GameObject model;
    Vector3 comingFrom; // 다가오기 시작할 때 로봇이 있던 자리(그 둘레를 못 벗어난다)
    float hintAt;

    public static NativeGreeter Build(Vector3 pos)
    {
        var go = new GameObject("NativeGreeter");
        go.transform.position = pos;
        var g = go.AddComponent<NativeGreeter>();
        Instance = g;
        g.model = NativeModel.Build(1.4f);
        g.model.transform.SetParent(go.transform, false);
        g.model.transform.localRotation = Quaternion.LookRotation(Vector3.forward); // 기지 문(북쪽)을 본다
        return g;
    }

    // 로봇이 E를 누를 수 있는 거리에 있나
    public bool CanTalk(Vector3 player)
    {
        var d = player - model.transform.position; d.y = 0;
        return Waiting && d.magnitude < TalkRange;
    }

    public void Talk()
    {
        var gm = GameManager.Instance;
        Active = Waiting = false;
        PlayerController.Leash = null;
        gm.StorySeen.Add(StoryKey);
        StoryUI.Instance.Narrate(Title, Body, new Color32(72, 140, 62, 255));
        gm.Save();
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (!gm) return;
        bool here = gm.Day == 1;
        if (model.activeSelf != here) model.SetActive(here);
        bool pending = here && !gm.StorySeen.Contains(StoryKey) && !gm.IsGameOver && gm.Player;
        if (!pending)
        {
            if (Active) { Active = Waiting = false; PlayerController.Leash = null; }
            return;
        }
        var player = gm.Player.position;
        bool outside = player.y > -20f; // 기지 안 방은 땅 밑(-80m)
        if (!Active)
        {
            if (!outside || TitleScreen.Showing || Hud.AnyPanelOpen) return; // 밖으로 나오면 시작
            Active = true;
            comingFrom = player;
            gm.Player.GetComponent<PlayerController>().Show("누군가 다가와요…", 3f);
        }
        if (!outside) return; // 다시 들어가 있으면 기다린다

        // 걸어오기
        var me = model.transform.position;
        var to = player - me; to.y = 0;
        if (to.magnitude > StopDistance)
        {
            var step = to.normalized * Mathf.Min(WalkSpeed * Time.deltaTime, to.magnitude - StopDistance);
            var next = me + step;
            next.y = Landscape.HeightAt(next);
            model.transform.position = next;
            Waiting = false;
            PlayerController.Leash = (comingFrom, LeashWhileComing);
        }
        else
        {
            Waiting = true;
            PlayerController.Leash = (new Vector3(me.x, 0, me.z), LeashWhenHere);
            if (Time.time > hintAt && !Hud.AnyPanelOpen)
            {
                gm.Player.GetComponent<PlayerController>().Show("원주민이 무언가 말하고 싶어 해요. E로 말을 걸어 보세요", 3f);
                hintAt = Time.time + 6f;
            }
        }
    }
}
