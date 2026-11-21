using UnityEngine;

/// <summary>
/// Legacy stub for Стенд С3-ОК01 Rп knob; preserves authored rotation so the mesh stays off the PCB.
/// </summary>
public class RotaryKnob2 : MonoBehaviour
{
    public float minAngle = -340f;
    public float maxAngle = -20f;
    public float sensitivity = 50f;
    public float dragSensitivity = 2.5f;
    public Camera mainCamera;

    public static bool IsAnyKnobDragging => false;

    void Start()
    {
        // Preserves authored localRotation on Circle so its offset pivot stays in the Rп spot
        if (gameObject.name == "Circle")
            transform.localRotation = new Quaternion(0f, -0.9896928f, -0.14320667f, 0f);
    }

    void Update() { }
}
