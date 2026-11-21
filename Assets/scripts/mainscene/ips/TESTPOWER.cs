using UnityEngine;

public class IPS_POWERCONTROLE : MonoBehaviour
{
    // Зелёные цифры
    public GameObject[] greenDigit1Segments = new GameObject[7]; // тысячи
    public GameObject[] greenDigit2Segments = new GameObject[7]; // сотни
    public GameObject[] greenDigit3Segments = new GameObject[7]; // десятки
    public GameObject[] greenDigit4Segments = new GameObject[7]; // единицы

    public GameObject greenDigit1Dot;
    public GameObject greenDigit2Dot;
    public GameObject greenDigit3Dot;
    public GameObject greenDigit4Dot;

    // Красная цифра
    public GameObject[] redDigitSegments = new GameObject[7];

    private bool isDisplayOn = false;
    private int greenValue = 0;
    private int redDigit = 1;

    void Start()
    {
        UpdateDisplay();
    }

    // Методы для вызова из Mouse Action
    public void ToggleDisplay()
    {
        isDisplayOn = !isDisplayOn;
        UpdateDisplay();
    }

    public void ChangeRedDigit()
    {
        if (!isDisplayOn) return;

        redDigit++;
        if (redDigit > 7) redDigit = 1;

        UpdateDisplay();
    }

    void UpdateDisplay()
    {
        // Включаем/выключаем все объекты
        SetSegmentsActive(greenDigit1Segments, isDisplayOn);
        SetSegmentsActive(greenDigit2Segments, isDisplayOn);
        SetSegmentsActive(greenDigit3Segments, isDisplayOn);
        SetSegmentsActive(greenDigit4Segments, isDisplayOn);
        SetDigitsDotsActive(isDisplayOn);
        SetSegmentsActive(redDigitSegments, isDisplayOn);

        if (!isDisplayOn) return;

        // Разбиваем greenValue на цифры
        int thousands = (greenValue / 1000) % 10;
        int hundreds = (greenValue / 100) % 10;
        int tens = (greenValue / 10) % 10;
        int units = greenValue % 10;

        ActivateSegments(greenDigit1Segments, thousands);
        ActivateSegments(greenDigit2Segments, hundreds);
        ActivateSegments(greenDigit3Segments, tens);
        ActivateSegments(greenDigit4Segments, units);

        // Точки
        if (greenDigit1Dot != null) greenDigit1Dot.SetActive(false);
        if (greenDigit2Dot != null) greenDigit2Dot.SetActive(false);
        if (greenDigit3Dot != null) greenDigit3Dot.SetActive(true);  // точка после десятков
        if (greenDigit4Dot != null) greenDigit4Dot.SetActive(false);

        // Красная цифра
        ActivateSegments(redDigitSegments, redDigit);
    }

    void SetSegmentsActive(GameObject[] segments, bool active)
    {
        if (segments == null) return;
        foreach (var seg in segments)
        {
            if (seg != null)
                seg.SetActive(active);
        }
    }

    void SetDigitsDotsActive(bool active)
    {
        if (greenDigit1Dot != null) greenDigit1Dot.SetActive(active);
        if (greenDigit2Dot != null) greenDigit2Dot.SetActive(active);
        if (greenDigit3Dot != null) greenDigit3Dot.SetActive(active);
        if (greenDigit4Dot != null) greenDigit4Dot.SetActive(active);
    }

    void ActivateSegments(GameObject[] segments, int digit)
    {
        bool[] pattern = GetSegmentPattern(digit);
        if (segments == null || pattern == null) return;

        for (int i = 0; i < 7 && i < segments.Length; i++)
        {
            if (segments[i] != null)
                segments[i].SetActive(pattern[i]);
        }
    }

    bool[] GetSegmentPattern(int digit)
    {
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

        return (digit >= 0 && digit <= 9) ? patterns[digit] : new bool[7];
    }

    public void SetGreenValue(int value)
    {
        greenValue = Mathf.Clamp(value, 0, 9999);
        if (isDisplayOn)
            UpdateDisplay();
    }
}