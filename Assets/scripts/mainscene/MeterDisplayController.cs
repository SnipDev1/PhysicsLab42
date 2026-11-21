using UnityEngine;

/// <summary>
/// Controls both 4-digit 7-segment readouts (Voltmeter & Ampermeter) with range-based decimal point and overload ("1   ").
/// </summary>
public class MeterDisplayController : MonoBehaviour
{
    public LabSetupController labManager;

    [Header("Сегменты Вольтметра")]
    public GameObject[] d_V1, d_V2, d_V3, d_V4;
    public GameObject dot_V1, dot_V2, dot_V3;

    [Header("Сегменты Амперметра")]
    public GameObject[] d_A1, d_A2, d_A3, d_A4;
    public GameObject dot_A1, dot_A2, dot_A3;

    private static readonly bool[][] Digits =
    {
        new[] { true,  true,  true,  true,  true,  true,  false }, // 0
        new[] { false, true,  true,  false, false, false, false }, // 1
        new[] { true,  true,  false, true,  true,  false, true  }, // 2
        new[] { true,  true,  true,  true,  false, false, true  }, // 3
        new[] { false, true,  true,  false, false, true,  true  }, // 4
        new[] { true,  false, true,  true,  false, true,  true  }, // 5
        new[] { true,  false, true,  true,  true,  true,  true  }, // 6
        new[] { true,  true,  true,  false, false, false, false }, // 7
        new[] { true,  true,  true,  true,  true,  true,  true  }, // 8
        new[] { true,  true,  true,  true,  false, true,  true  }  // 9
    };

    private bool _hasState;
    private bool _lastOn;
    private int _lastVoltScaled = -1;
    private float _lastVoltRange = -1f;
    private int _lastAmpScaled = -1;
    private float _lastAmpRange = -1f;

    void Update()
    {
        var manager = LabSetupController.Instance != null ? LabSetupController.Instance : labManager;
        bool on = manager != null && manager.isMeterPowered;

        if (!on)
        {
            if (_hasState && !_lastOn) return;
            _hasState = true;
            _lastOn = false;
            _lastVoltScaled = -1;
            _lastAmpScaled = -1;
            DisplayBlank(d_V1, d_V2, d_V3, d_V4, dot_V1, dot_V2, dot_V3);
            DisplayBlank(d_A1, d_A2, d_A3, d_A4, dot_A1, dot_A2, dot_A3);
            return;
        }

        float v = manager.GetMeasuredVoltage();
        float vRange = manager.voltRange;
        int vScaled = ComputeScaled(v, vRange);

        float a = manager.GetMeasuredCurrent();
        float aRange = manager.ampRange;
        int aScaled = ComputeScaled(a, aRange);

        if (_hasState && _lastOn
            && vScaled == _lastVoltScaled && Mathf.Approximately(vRange, _lastVoltRange)
            && aScaled == _lastAmpScaled && Mathf.Approximately(aRange, _lastAmpRange))
            return;

        _hasState = true;
        _lastOn = true;
        _lastVoltScaled = vScaled;
        _lastVoltRange = vRange;
        _lastAmpScaled = aScaled;
        _lastAmpRange = aRange;

        RenderChannel(vScaled, vRange, d_V1, d_V2, d_V3, d_V4, dot_V1, dot_V2, dot_V3);
        RenderChannel(aScaled, aRange, d_A1, d_A2, d_A3, d_A4, dot_A1, dot_A2, dot_A3);
    }

    private static int GetMultiplier(float range)
    {
        if (range <= 2.5f) return 1000;
        if (range <= 25f) return 100;
        if (range <= 250f) return 10;
        return 1;
    }

    private static int ComputeScaled(float value, float range) =>
        Mathf.Max(0, Mathf.RoundToInt(value * GetMultiplier(range)));

    private static void RenderChannel(
        int scaled, float range,
        GameObject[] g1, GameObject[] g2, GameObject[] g3, GameObject[] g4,
        GameObject dot1, GameObject dot2, GameObject dot3)
    {
        int multiplier = GetMultiplier(range);
        int onesIndex; // Индекс разряда единиц (0..3), до которого гасятся незначащие нули
        bool showDot1 = false, showDot2 = false, showDot3 = false;

        if (range <= 2.5f)
        {
            onesIndex = 0;
            showDot1 = true;
        }
        else if (range <= 25f)
        {
            onesIndex = 1;
            showDot2 = true;
        }
        else if (range <= 250f)
        {
            onesIndex = 2;
            showDot3 = true;
        }
        else
        {
            onesIndex = 3;
        }

        int maxCount = Mathf.RoundToInt(range * multiplier);

        // Перегрузка (если значение превышает выбранный предел): «1   » без точек
        if (scaled > maxCount || scaled > 9999)
        {
            SetDigit(g1, 1);
            SetDigit(g2, -1);
            SetDigit(g3, -1);
            SetDigit(g4, -1);
            SetActiveIfChanged(dot1, false);
            SetActiveIfChanged(dot2, false);
            SetActiveIfChanged(dot3, false);
            return;
        }

        int[] d =
        {
            (scaled / 1000) % 10,
            (scaled / 100) % 10,
            (scaled / 10) % 10,
            scaled % 10
        };

        for (int i = 0; i < onesIndex; i++)
        {
            if (d[i] == 0) d[i] = -1;
            else break;
        }

        SetDigit(g1, d[0]);
        SetDigit(g2, d[1]);
        SetDigit(g3, d[2]);
        SetDigit(g4, d[3]);

        SetActiveIfChanged(dot1, showDot1);
        SetActiveIfChanged(dot2, showDot2);
        SetActiveIfChanged(dot3, showDot3);
    }

    private static void DisplayBlank(
        GameObject[] g1, GameObject[] g2, GameObject[] g3, GameObject[] g4,
        GameObject dot1, GameObject dot2, GameObject dot3)
    {
        SetDigit(g1, -1);
        SetDigit(g2, -1);
        SetDigit(g3, -1);
        SetDigit(g4, -1);
        SetActiveIfChanged(dot1, false);
        SetActiveIfChanged(dot2, false);
        SetActiveIfChanged(dot3, false);
    }

    private static void SetActiveIfChanged(GameObject go, bool target)
    {
        if (go != null && go.activeSelf != target)
            go.SetActive(target);
    }

    private static void SetDigit(GameObject[] segments, int value)
    {
        if (segments == null) return;
        for (int i = 0; i < segments.Length && i < 7; i++)
            SetActiveIfChanged(segments[i], value >= 0 && value <= 9 && Digits[value][i]);
    }
}
