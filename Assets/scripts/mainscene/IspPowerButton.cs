// IspPowerButton.cs
using UnityEngine;

public class IspPowerButton : MonoBehaviour
{
    public LabSetupController labManager;

    public void OnMouseDown()
    {
        labManager.ToggleIpsPower();
    }
}