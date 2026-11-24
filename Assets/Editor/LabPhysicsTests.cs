#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class LabPhysicsTests
{
    [Test]
    public void Wavelengths_HasEightPresets_AndPeakAt565Nm()
    {
        Assert.AreEqual(8, LabSetupController.WavelengthsNm.Length, "Should have 8 emitter wavelengths");
        Assert.AreEqual(430, LabSetupController.WavelengthsNm[0]);
        Assert.AreEqual(565, LabSetupController.WavelengthsNm[3], "Peak sensitivity wavelength");
        Assert.AreEqual(860, LabSetupController.WavelengthsNm[7], "IR wavelength");
    }

    [Test]
    public void Voltmeter_ReturnsZero_WhenCircuitDisconnected()
    {
        var go = new GameObject("TestSetup");
        var lab = go.AddComponent<LabSetupController>();
        try
        {
            lab.isIpsPowered = true;
            lab.isMeterPowered = true;
            lab.SetVoltageU(15f);

            // With no wires connected, IsVoltmeterConnected must be false
            Assert.IsFalse(lab.IsVoltmeterConnected, "Voltmeter must not be connected without wires");
            Assert.IsFalse(lab.IsPhotoCircuitConnected, "Photo circuit must not be connected without wires");
            Assert.AreEqual(0f, lab.GetMeasuredVoltage(), "Open circuit voltage must be 0V");
            Assert.AreEqual(0f, lab.GetTotalCurrentMicroA(), "Open circuit current must be 0 uA");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void StandardCircuit_EvaluatesCorrectly_AndMeasuresVoltage()
    {
        var go = new GameObject("TestSetup");
        var lab = go.AddComponent<LabSetupController>();
        try
        {
            lab.AutoConnectStandardCircuit();
            lab.isIpsPowered = true;
            lab.isMeterPowered = true;
            lab.SetVoltageU(10f);
            lab.SetIntensity(0.5f);
            lab.SetLambda(3);

            Assert.IsTrue(lab.IsPhotoCircuitConnected, "Standard circuit must connect photoresistor");
            Assert.IsTrue(lab.IsVoltmeterConnected, "Standard circuit must connect voltmeter");
            Assert.IsFalse(lab.IsPowerShorted, "Standard circuit must not short power");
            Assert.AreEqual(10f, lab.GetMeasuredVoltage(), 0.001f, "Voltmeter must read supply voltage");
            Assert.Greater(lab.GetTotalCurrentMicroA(), 0f, "Photocurrent must flow through connected circuit");
        }
        finally
        {
            lab.ClearAllWires();
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void GameSettings_ClampsAndResets_Correctly()
    {
        GameSettings.ResetToDefaults();
        Assert.AreEqual(GameSettings.DefaultFov, GameSettings.Fov, 0.01f);
        Assert.AreEqual(GameSettings.DefaultSens, GameSettings.Sensitivity, 0.01f);
        Assert.AreEqual(GameSettings.DefaultZoomFov, GameSettings.ZoomFov, 0.01f);

        // Clamp checks
        GameSettings.Fov = 10f;
        Assert.AreEqual(GameSettings.MinFov, GameSettings.Fov, 0.01f);
        GameSettings.Fov = 150f;
        Assert.AreEqual(GameSettings.MaxFov, GameSettings.Fov, 0.01f);

        GameSettings.Sensitivity = 5f;
        Assert.AreEqual(GameSettings.MinSens, GameSettings.Sensitivity, 0.01f);
        GameSettings.Sensitivity = 500f;
        Assert.AreEqual(GameSettings.MaxSens, GameSettings.Sensitivity, 0.01f);

        bool eventFired = false;
        System.Action handler = () => eventFired = true;
        GameSettings.OnChanged += handler;
        try
        {
            GameSettings.Fov = 65f;
            Assert.IsTrue(eventFired, "OnChanged must fire when FOV is updated");
        }
        finally
        {
            GameSettings.OnChanged -= handler;
            GameSettings.ResetToDefaults();
        }
    }

    [MenuItem("Tests/Run Physics Verification")]
    public static void RunVerificationFromMenu()
    {
        var suite = new LabPhysicsTests();
        suite.Wavelengths_HasEightPresets_AndPeakAt565Nm();
        suite.Voltmeter_ReturnsZero_WhenCircuitDisconnected();
        suite.StandardCircuit_EvaluatesCorrectly_AndMeasuresVoltage();
        suite.GameSettings_ClampsAndResets_Correctly();
        Debug.Log("[LabPhysicsTests] All verification tests passed successfully!");
    }
}
#endif
