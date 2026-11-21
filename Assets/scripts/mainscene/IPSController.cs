using UnityEngine;

/// <summary>
/// Legacy bridge kept for scene/prefab compatibility; delegates to LabSetupController.
/// </summary>
public class IPSController : MonoBehaviour
{
    public RotaryKnob voltageKnob;
    public RotaryKnob j0Knob;
    public DigitController lambdaDigit;
    public DigitController[] greenDigits;

    public bool isOn => LabSetupController.Instance != null && LabSetupController.Instance.isIpsPowered;
    public int lambdaIndex => LabSetupController.Instance != null ? LabSetupController.Instance.lambda : 0;
    public float GetU() => LabSetupController.Instance != null ? LabSetupController.Instance.voltageU : 0f;
    public float GetJ0() => LabSetupController.Instance != null ? LabSetupController.Instance.baseCurrentIo : 0f;
    public void NextLambda() => LabSetupController.Instance?.NextLambda();
}
