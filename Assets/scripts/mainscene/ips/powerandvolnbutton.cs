using UnityEngine;

public class DigitalDisplayController : MonoBehaviour
{
    // === ЗЕЛЁНЫЙ ДИСПЛЕЙ (I/Io) — 4 цифры ===
    public GameObject[] digit_Io_1; // Тысячи
    public GameObject[] digit_Io_2; // Сотни
    public GameObject[] digit_Io_3; // Десятки
    public GameObject[] digit_Io_4; // Единицы
    public GameObject dot_Io_3;     // Точка после десятков

    // === КРАСНАЯ ЦИФРА (λ) — 1 цифра ===
    public GameObject[] digit_Lambda;

    // === НИЖНИЕ КРАСНЫЕ ДИСПЛЕИ ===
    // U (0–20.0 В) — 3 цифры
    public GameObject[] digit_U_1; // Сотни (0, 1, 2)
    public GameObject[] digit_U_2; // Десятки
    public GameObject[] digit_U_3; // Единицы
    public GameObject dot_U_3;     // Точка после единиц

    // Uo (0–6.3 В) — 3 цифры
    public GameObject[] digit_Uo_1; // Десятки (0–6)
    public GameObject[] digit_Uo_2; // Единицы
    public GameObject[] digit_Uo_3; // Десятые
    public GameObject dot_Uo_3;

    // === ВНУТРЕННИЕ ЗНАЧЕНИЯ ===
    private bool isPoweredOn = false;
    private int lambda = 1;           // 1–7
    private float voltageU = 0f;      // 0–20 В
    private float voltageUo = 0f;     // 0–6.3 В
    private float currentIo = 1f;     // базовый ток
    private float currentIc = 0f;     // фототок

    void Start()
    {
        UpdateDisplay();
    }

    // === ПУБЛИЧНЫЕ МЕТОДЫ ===

    public void TogglePower()
    {
        isPoweredOn = !isPoweredOn;
        if (!isPoweredOn)
        {
            voltageU = 0f;
            voltageUo = 0f;
            lambda = 1;
            currentIc = 0f;
        }
        UpdateDisplay();
    }

    public void ChangeLambda()
    {
        if (!isPoweredOn) return;
        lambda = lambda < 7 ? lambda + 1 : 1;
        CalculateCurrent();
        UpdateDisplay();
    }

    public void SetVoltageU(float volts)
    {
        if (!isPoweredOn) return;
        voltageU = Mathf.Clamp(volts, 0f, 20f);
        CalculateCurrent();
        UpdateDisplay();
    }

    public void SetVoltageUo(float volts)
    {
        if (!isPoweredOn) return;
        voltageUo = Mathf.Clamp(volts, 0f, 6.3f);
        CalculateCurrent();
        UpdateDisplay();
    }

    // === РАСЧЁТ ТОКА ===
    void CalculateCurrent()
    {
        float baseI = 0.2f;
        float fromU = voltageU * 0.07f;
        float fromUo = voltageUo * 0.05f;
        float lambdaBoost = 1f + (lambda - 1) * 0.1f;
        currentIc = (baseI + fromU + fromUo) * lambdaBoost;
        currentIc = Mathf.Clamp(currentIc, 0f, 99.99f);
    }

    // === ОБНОВЛЕНИЕ ДИСПЛЕЕВ ===
    void UpdateDisplay()
    {
        // Выключаем всё, если нет питания
        SetDigitsActive(digit_Io_1, isPoweredOn);
        SetDigitsActive(digit_Io_2, isPoweredOn);
        SetDigitsActive(digit_Io_3, isPoweredOn);
        SetDigitsActive(digit_Io_4, isPoweredOn);
        SetDigitsActive(digit_Lambda, isPoweredOn);
        SetDigitsActive(digit_U_1, isPoweredOn);
        SetDigitsActive(digit_U_2, isPoweredOn);
        SetDigitsActive(digit_U_3, isPoweredOn);
        SetDigitsActive(digit_Uo_1, isPoweredOn);
        SetDigitsActive(digit_Uo_2, isPoweredOn);
        SetDigitsActive(digit_Uo_3, isPoweredOn);

        if (dot_Io_3 != null) dot_Io_3.SetActive(isPoweredOn);
        if (dot_U_3 != null) dot_U_3.SetActive(isPoweredOn);
        if (dot_Uo_3 != null) dot_Uo_3.SetActive(isPoweredOn);

        if (!isPoweredOn) return;

        // Обновляем I/Io
        int ratio = Mathf.RoundToInt((currentIc / currentIo) * 100);
        Display4Digit(digit_Io_1, digit_Io_2, digit_Io_3, digit_Io_4, ratio);

        // Обновляем λ
        ActivateDigit(digit_Lambda, lambda);

        // Обновляем U
        int uVal = Mathf.RoundToInt(voltageU * 10); // 0–200
        Display3Digit(digit_U_1, digit_U_2, digit_U_3, uVal);
        if (dot_U_3 != null) dot_U_3.SetActive(true);

        // Обновляем Uo
        int uoVal = Mathf.RoundToInt(voltageUo * 10); // 0–63
        Display3Digit(digit_Uo_1, digit_Uo_2, digit_Uo_3, uoVal);
        if (dot_Uo_3 != null) dot_Uo_3.SetActive(true);
    }

    // === ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ===

    void SetDigitsActive(GameObject[] segments, bool active)
    {
        if (segments == null) return;
        foreach (var s in segments)
            if (s != null) s.SetActive(active);
    }

    void ActivateDigit(GameObject[] segments, int digit)
    {
        bool[] pattern = GetSegmentPattern(digit);
        for (int i = 0; i < 7 && i < segments.Length; i++)
        {
            if (segments[i] != null)
                segments[i].SetActive(pattern[i]);
        }
    }

    void Display4Digit(GameObject[] a, GameObject[] b, GameObject[] c, GameObject[] d, int value)
    {
        ActivateDigit(a, (value / 1000) % 10);
        ActivateDigit(b, (value / 100) % 10);
        ActivateDigit(c, (value / 10) % 10);
        ActivateDigit(d, value % 10);
    }

    void Display3Digit(GameObject[] a, GameObject[] b, GameObject[] c, int value)
    {
        ActivateDigit(a, (value / 100) % 10);
        ActivateDigit(b, (value / 10) % 10);
        ActivateDigit(c, value % 10);
    }

    bool[] GetSegmentPattern(int digit)
    {
        // Защита от некорректных значений
        if (digit < 0 || digit > 9)
            return new bool[7]; // выключить все сегменты

        // Паттерны для цифр 0–9
        bool[][] patterns = {
            new bool[] { true, true, true, true, true, true, false }, // 0
            new bool[] { false, true, true, false, false, false, false }, // 1
            new bool[] { true, true, false, true, true, false, true }, // 2
            new bool[] { true, true, true, true, false, false, true }, // 3
            new bool[] { false, true, true, false, false, true, true }, // 4
            new bool[] { true, false, true, true, false, true, true }, // 5
            new bool[] { true, false, true, true, true, true, true }, // 6
            new bool[] { true, true, true, false, false, false, false }, // 7
            new bool[] { true, true, true, true, true, true, true }, // 8
            new bool[] { true, true, true, true, false, true, true }  // 9
        };

        return patterns[digit];
    }
}