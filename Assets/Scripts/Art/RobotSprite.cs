using UnityEngine;

// 로봇 그림: 카메라 쪽으로 오면 앞모습, 멀어지면 뒷모습, 옆으로 가면 옆모습(왼쪽이면 뒤집기).
public class RobotSprite : MonoBehaviour
{
    public float height = 1.7f;

    Billboard board;
    Sprite front, back, side;
    Vector3 lastPos;

    public static bool HasArt => ArtLibrary.World("Robot/robot_front");

    void Start()
    {
        front = ArtLibrary.World("Robot/robot_front");
        back = ArtLibrary.World("Robot/robot_back") ?? front;
        side = ArtLibrary.World("Robot/robot_side") ?? front;
        board = Billboard.CreateByHeight(transform, front, height, Color.white);
        board.transform.localPosition = Vector3.down * 0.8f; // 캐릭터 컨트롤러 바닥
        lastPos = transform.position;
    }

    void Update()
    {
        board.gameObject.SetActive(!CameraRig.FirstPerson); // 1인칭이면 내 몸은 안 보인다
        var move = transform.position - lastPos;
        lastPos = transform.position;
        move.y = 0;
        var cam = Camera.main;
        if (!cam || move.sqrMagnitude < 0.0001f) return;

        float toward = Vector3.Dot(move.normalized, Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized);
        float sideways = Vector3.Dot(move.normalized, cam.transform.right);
        var sr = board.Renderer;
        if (toward > 0.6f) { sr.sprite = back; sr.flipX = false; }
        else if (toward < -0.6f) { sr.sprite = front; sr.flipX = false; }
        else { sr.sprite = side; sr.flipX = sideways < 0; }
        // 걸을 때 통통 튀기
        board.transform.localPosition = Vector3.down * 0.8f + Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * 12f)) * 0.06f;
    }
}
