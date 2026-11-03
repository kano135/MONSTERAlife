using UnityEngine;

// 비닐하우스 장치 하나(가습기·난방기·성장 조명). E로 세기(약·중·강)를 바꾸고, 고장 나면 E로 고친다.
public class GreenhouseDevice : MonoBehaviour
{
    public enum Kind { Humidifier, Heater, Lamp }

    static readonly string[] Names = { "가습기", "난방기", "성장 조명" };
    static readonly string[] NamesWithObject = { "가습기를", "난방기를", "성장 조명을" };
    static readonly string[] Stats = { "습도", "온도", "빛" };
    static readonly string[] LevelNames = { "약", "중", "강" };
    // 세기별로 맞춰 주는 값(0~1), 고장 나면 이 값으로 점점 떨어진다
    static readonly float[][] Levels = { new[] { 0.4f, 0.6f, 0.8f }, new[] { 0.35f, 0.55f, 0.75f }, new[] { 0.45f, 0.7f, 0.95f } };
    static readonly float[] BrokenValues = { 0.2f, 0.15f, 0.25f };

    public Kind kind;
    [Tooltip("이 업그레이드를 만들어야 설치된다(비워 두면 처음부터 있음)")]
    public string requiredUpgrade;
    public int Level { get; private set; } = 1;
    public bool Broken { get; private set; }

    public bool Installed => string.IsNullOrEmpty(requiredUpgrade) || (GameManager.Instance && GameManager.Instance.HasUpgrade(requiredUpgrade));

    public string Name => Names[(int)kind];
    public string Stat => Stats[(int)kind];
    public string LevelName => LevelNames[Level];
    public float Target => Broken ? BrokenValues[(int)kind] : Levels[(int)kind][Level];

    GameObject alarm;
    SpriteRenderer art;

    public string Use()
    {
        if (Broken)
        {
            Broken = false;
            return $"{NamesWithObject[(int)kind]} 고쳤어요!";
        }
        Level = (Level + 1) % LevelNames.Length;
        return $"{Name} 세기 {LevelName} → {Stat} {Target:P0}로 맞춰요";
    }

    public void Break() => Broken = true;

    public void Load(int level, bool broken)
    {
        Level = Mathf.Clamp(level, 0, LevelNames.Length - 1);
        Broken = broken;
    }

    public void SetVisual(SpriteRenderer sprite, GameObject alarmLight)
    {
        art = sprite;
        alarm = alarmLight;
    }

    // 고장 나면 빨간 경고등이 깜빡이고 그림이 어두워진다
    void Update()
    {
        if (alarm) alarm.SetActive(Broken && Mathf.Repeat(Time.time, 0.8f) < 0.5f);
        if (art) art.color = Broken ? new Color(0.6f, 0.55f, 0.55f) : Color.white;
    }
}
