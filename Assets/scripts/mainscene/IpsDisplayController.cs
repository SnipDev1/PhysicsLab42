using UnityEngine;

/// <summary>
/// Controls the 7-segment displays on ИПС-1: relative intensity J/J0 (.000..1.000) and emitter index λ (0..7).
/// </summary>
public class IpsDisplayController : MonoBehaviour
{
    public LabSetupController labManager;

    public GameObject[] digit_Io_1, digit_Io_2, digit_Io_3, digit_Io_4;
    public GameObject dot_Io;
    public GameObject dot_Io_2, dot_Io_3;

    public GameObject[] digit_Lambda;

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
    private bool _lastPowered;
    private int _lastJ0Scaled = -1;
    private int _lastLambda = -1;

    void Update()
    {
        var manager = LabSetupController.Instance != null ? LabSetupController.Instance : labManager;
        bool isPowered = manager != null && manager.isIpsPowered;

        int j0Scaled = isPowered ? Mathf.Clamp(Mathf.RoundToInt(manager.baseCurrentIo * 1000f), 0, 1000) : -1;
        int lambdaVal = isPowered ? Mathf.Clamp(manager.lambda, 0, 7) : -1;

        if (_hasState && isPowered == _lastPowered && j0Scaled == _lastJ0Scaled && lambdaVal == _lastLambda)
            return;

        _hasState = true;
        _lastPowered = isPowered;
        _lastJ0Scaled = j0Scaled;
        _lastLambda = lambdaVal;

        int d1 = j0Scaled >= 1000 ? 1 : -1; // На реальном ИПС-1 ведущий ноль перед точкой гасится (.407 вместо 0.407)
        int d2 = isPowered ? (j0Scaled / 100) % 10 : -1;
        int d3 = isPowered ? (j0Scaled / 10) % 10 : -1;
        int d4 = isPowered ? j0Scaled % 10 : -1;

        UpdateDigit(digit_Io_1, d1, isPowered);
        UpdateDigit(digit_Io_2, d2, isPowered);
        UpdateDigit(digit_Io_3, d3, isPowered);
        UpdateDigit(digit_Io_4, d4, isPowered);

        SetActiveIfChanged(dot_Io, isPowered);
        SetActiveIfChanged(dot_Io_2, false);
        SetActiveIfChanged(dot_Io_3, false);

        UpdateDigit(digit_Lambda, lambdaVal, isPowered);
    }

    private static void SetActiveIfChanged(GameObject go, bool target)
    {
        if (go != null && go.activeSelf != target)
            go.SetActive(target);
    }

    private static void UpdateDigit(GameObject[] segments, int value, bool active)
    {
        if (segments == null) return;
        for (int i = 0; i < segments.Length && i < 7; i++)
        {
            bool target = active && value >= 0 && value <= 9 && Digits[value][i];
            SetActiveIfChanged(segments[i], target);
        }
    }
}
