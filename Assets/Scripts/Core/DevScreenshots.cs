#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

// 개발용: Play 하면 몇 초 뒤 화면을 찍어 프로젝트 폴더 Screenshots/ 에 남긴다(작업 확인용). F12로도 찍는다.
// 자동 실행(AutoPlayOnce)일 때만: 마지막 한 장은 로봇을 비닐하우스 안으로 옮기고 3인칭으로 찍은 뒤 시점을 돌려놓는다.
public class DevScreenshots : MonoBehaviour
{
    public const string TourKey = "monstera_dev_tour";
    static readonly float[] Times = { 3f, 15f, 24f };
    const float TourFrom = 20f;

    int next;
    bool tour, toured;
    bool? viewBefore;

    void Start()
    {
        tour = SessionState.GetBool(TourKey, false);
        SessionState.SetBool(TourKey, false);
    }

    void Update()
    {
        float t = Time.timeSinceLevelLoad;
        if (tour && !toured && t >= TourFrom) StartTour();
        if (next < Times.Length && t >= Times[next]) Capture($"play_{++next}");
        else if (next == Times.Length && viewBefore != null)
        {
            CameraRig.SetFirstPerson(viewBefore.Value);
            viewBefore = null;
            next++;
        }
        if (Keyboard.current != null && Keyboard.current.f12Key.wasPressedThisFrame) Capture($"shot_{System.DateTime.Now:HHmmss}");
    }

    void StartTour()
    {
        toured = true;
        viewBefore = CameraRig.FirstPerson;
        CameraRig.SetFirstPerson(false);
        var gh = Greenhouse.Instance;
        var player = GameManager.Instance ? GameManager.Instance.Player : null;
        if (!gh || !player) return;
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.position = gh.transform.position + new Vector3(0, 1, -6.5f); // 문 바로 안쪽
        player.rotation = Quaternion.identity;
        cc.enabled = true;
        var rig = FindFirstObjectByType<CameraRig>();
        if (rig) rig.SetYaw(0); // 하우스 안쪽(+Z)을 보게
    }

    static void Capture(string name)
    {
        var dir = Path.Combine(Application.dataPath, "..", "Screenshots");
        Directory.CreateDirectory(dir);
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
    }
}
#endif
