using UnityEngine;

/// <summary>
/// Rotates panel knobs via LMB drag or mouse scroll wheel while pausing camera look during drag.
/// </summary>
public class RotaryKnob : MonoBehaviour
{
    public enum KnobType { Voltage, BaseCurrentIo, Voltage6_3 }

    public static bool IsAnyKnobDragging { get; private set; }

    [Header("Связь с контроллером")]
    public LabSetupController labManager;
    public KnobType typeOfKnob;

    [Header("Настройки вращения")]
    public float minAngle = -340f;
    public float maxAngle = -20f;
    public float sensitivity = 50f;
    public float dragSensitivity = 2.5f;

    [Header("Камера")]
    public Camera mainCamera;

    public float currentAngle;
    private bool _isDragging;

    void Start()
    {
        if (mainCamera == null) mainCamera = LabSetupController.GetMainCamera();
        if (labManager == null) labManager = LabSetupController.Instance != null
            ? LabSetupController.Instance
            : Object.FindAnyObjectByType<LabSetupController>();

        AutoDetectKnobType();
        labManager?.RegisterKnob(this);

        // Начальное положение: крайний левый упор (0 В / 0.000) соответствует maxAngle (-20°)
        currentAngle = maxAngle;
        UpdateRotation();
        SyncValueWithManager();
    }

    void OnDisable()
    {
        if (_isDragging)
        {
            _isDragging = false;
            IsAnyKnobDragging = false;
        }
    }

    void Update()
    {
        bool hovered = IsPointerOverThisObject();

        if (hovered && Input.GetMouseButtonDown(0))
        {
            _isDragging = true;
            IsAnyKnobDragging = true;
        }

        if (_isDragging)
        {
            if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0))
            {
                _isDragging = false;
                IsAnyKnobDragging = false;
            }
            else
            {
                float dragDelta = Input.GetAxis("Mouse X") + Input.GetAxis("Mouse Y") * 0.5f;
                if (Mathf.Abs(dragDelta) > 0.0001f)
                    ApplyAngleDelta(-dragDelta * dragSensitivity);
            }
        }

        if (hovered || _isDragging)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
                ApplyAngleDelta(-scroll * sensitivity);
        }
    }

    public void SetNormalizedValue(float normalized)
    {
        currentAngle = Mathf.Lerp(maxAngle, minAngle, Mathf.Clamp01(normalized));
        UpdateRotation();
    }

    private void ApplyAngleDelta(float delta)
    {
        currentAngle = Mathf.Clamp(currentAngle + delta, minAngle, maxAngle);
        UpdateRotation();
        SyncValueWithManager();
    }

    private void AutoDetectKnobType()
    {
        if (gameObject.name.Contains("0_6"))
            typeOfKnob = KnobType.Voltage6_3;
        else if (gameObject.name.Contains("0_20"))
            typeOfKnob = KnobType.Voltage;
        else if (gameObject.name.Contains("J0") || gameObject.name.Contains("Circle"))
            typeOfKnob = KnobType.BaseCurrentIo;
    }

    private void SyncValueWithManager()
    {
        var manager = labManager != null ? labManager : LabSetupController.Instance;
        if (manager == null) return;

        // maxAngle (-20°) -> 0.0 (минимум), minAngle (-340°) -> 1.0 (максимум)
        float t = Mathf.InverseLerp(maxAngle, minAngle, currentAngle);

        switch (typeOfKnob)
        {
            case KnobType.Voltage:
                manager.SetVoltageU(t * 20f);
                break;
            case KnobType.Voltage6_3:
                manager.SetVoltageUo(t * 6.3f);
                break;
            case KnobType.BaseCurrentIo:
                manager.SetIntensity(t, this);
                break;
        }
    }

    private bool IsPointerOverThisObject()
    {
        if (mainCamera == null) mainCamera = LabSetupController.GetMainCamera();
        if (mainCamera == null) return false;

        Ray ray = LabSetupController.GetPointerRay(mainCamera);
        return Physics.Raycast(ray, out RaycastHit hit, 25f)
            && hit.collider != null
            && (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform));
    }

    private void UpdateRotation()
    {
        transform.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
    }
}
