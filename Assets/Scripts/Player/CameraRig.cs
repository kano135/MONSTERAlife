using UnityEngine;
using UnityEngine.InputSystem;

// 카메라: 1인칭(기본) / 3인칭. V 키나 설정 창(Esc)에서 바꾸고, 고른 시점·마우스 감도는 다음에도 기억한다.
//   1인칭·3인칭 모두 마우스로 둘러보기(커서 숨김). 3인칭은 휠로 거리.
//   마우스 커서는 Esc 메뉴를 열었을 때만 나온다(Hud.CursorFree).
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
        var cam = GetComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        // 멀리 있는 나무·덤불은 안 그린다(안개에 흐려진 거리). 나무가 많아도 느려지지 않게.
        var cull = new float[32];
        cull[NaturePack.TreeLayer] = NaturePack.TreeDistance;
        cull[NaturePack.SmallLayer] = NaturePack.SmallDistance;
        cam.layerCullDistances = cull;
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

        bool locked = !Hud.CursorFree;
        if (Cursor.lockState != (locked ? CursorLockMode.Locked : CursorLockMode.None)) Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;

        if (mouse != null && !ui)
        {
            if (locked)
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
        if (!FirstPerson)
        {
            // 3인칭 카메라가 언덕·나무·벽 속으로 들어가면 안이 비어 보여서(찢어진 것처럼) 그 앞에 멈춘다
            var pivot = target.position + Vector3.up * 1.2f;
            var back = position - pivot;
            if (Physics.SphereCast(pivot, 0.25f, back.normalized, out var hit, back.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore))
                position = pivot + back.normalized * Mathf.Max(0.3f, hit.distance - 0.05f);
        }
        // 카메라가 언덕 땅속으로 내려가지 않게(기지 안 방은 제외)
        if (position.y > -20f) position.y = Mathf.Max(position.y, Landscape.HeightAt(position) + 0.3f);
        transform.SetPositionAndRotation(position, rotation);
    }
}
