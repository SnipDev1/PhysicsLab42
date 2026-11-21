using UnityEngine;

/// <summary>
/// Legacy bridge kept for scene/prefab compatibility; actual metering runs in LabSetupController + MeterDisplayController.
/// </summary>
public class MeterController : MonoBehaviour
{
    public IPSController ips;
    public bool isOn = false;
    public bool isMicro = false;
    public bool isMilliV = false;

    public DigitController[] vDigits;
    public DigitController[] aDigits;

    public void TogglePower() => LabSetupController.Instance?.ToggleMeterPower();
    public void SwitchA() => LabSetupController.Instance?.ToggleAmpUnits();
    public void SwitchV() => LabSetupController.Instance?.ToggleVoltUnits();
}
