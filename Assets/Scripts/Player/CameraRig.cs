using UnityEngine;
using UnityEngine.InputSystem;

// 카메라: 1인칭(기본) / 3인칭. V 키나 설정 창(Esc)에서 바꾸고, 고른 시점·마우스 감도는 다음에도 기억한다.
//   1인칭: 마우스로 둘러보기(커서 숨김)
//   3인칭: 마우스 오른쪽 드래그로 돌리기, 휠로 거리
// 카메라가 먼저 자리를 잡아야 카메라를 보는 그림(Billboard)·손에 든 것이 한 프레임 늦게 따라오며 떨리지 않는다
[DefaultExecutionOrder(-100)]
public class CameraRig : MonoBehaviour
{
    const string ViewKey = "camera_first_person";
    const string SensitivityKey = "mouse_sensitivity";

    public static bool FirstPerson { get; private set; } = true;
    public static float Sensitivity { get; private set; } = 0.15f;

    public Transform target;
    public float distance = 7f;
    public float minDistance = 3f;
    public float maxDistance = 14f;
    public float eyeHeight = 0.55f;

    float yaw;
    float pitch = 10f;

    public static void SetFirstPerson(bool on)
    {
        FirstPerson = on;
        PlayerPrefs.SetInt(ViewKey, on ? 1 : 0);
    }

    public static void SetSensitivity(float value)
    {
        Sensitivity = Mathf.Clamp(value, 0.03f, 0.6f);
        PlayerPrefs.SetFloat(SensitivityKey, Sensitivity);
    }

    public void SetYaw(float value) => yaw = value;

    void Awake()
    {
        FirstPerson = PlayerPrefs.GetInt(ViewKey, 1) == 1;
        Sensitivity = PlayerPrefs.GetFloat(SensitivityKey, 0.15f);
        GetComponent<Camera>().nearClipPlane = 0.05f;
    }

    void Start()
    {
        if (target) yaw = target.eulerAngles.y;
    }

    void LateUpdate()
    {
        if (!target) return;
        var mouse = Mouse.current;
        var kb = Keyboard.current;
        bool ui = Hud.AnyPanelOpen;

        if (kb != null && kb.vKey.wasPressedThisFrame && !ui) SetFirstPerson(!FirstPerson);

        bool locked = FirstPerson && !ui;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;

        if (mouse != null && !ui)
        {
            if (FirstPerson || mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                yaw += delta.x * Sensitivity;
                pitch -= delta.y * Sensitivity;
            }
            if (!FirstPerson) distance = Mathf.Clamp(distance - mouse.scroll.ReadValue().y * 0.01f, minDistance, maxDistance);
        }
        pitch = FirstPerson ? Mathf.Clamp(pitch, -80f, 80f) : Mathf.Clamp(pitch, 5f, 70f);

        var rotation = Quaternion.Euler(pitch, yaw, 0);
        var position = FirstPerson
            ? target.position + Vector3.up * eyeHeight
            : target.position + Vector3.up * 1.2f - rotation * Vector3.forward * distance;
        transform.SetPositionAndRotation(position, rotation);
    }
}
