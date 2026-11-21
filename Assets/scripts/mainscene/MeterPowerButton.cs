// MeterPowerButton.cs
using UnityEngine;

public class MeterPowerButton : MonoBehaviour
{
    public LabSetupController labManager;

    void OnMouseDown()
    {
        labManager.ToggleMeterPower();
    }
}