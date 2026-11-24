using UnityEngine;

namespace Player
{
    public class rotateMovement : MonoBehaviour
    {
        [SerializeField] private float _rotateSpeed = 75f;
        [SerializeField] private float _normalFov = 60f;
        [SerializeField] private float _zoomFov = 24f;
        [SerializeField] private float _zoomSpeed = 10f;
        [SerializeField] private float _heightSpeed = 0.65f;
        [SerializeField] private float _minHeight = 1.50f;
        [SerializeField] private float _maxHeight = 2.85f;

        private Camera _playerCamera;
        private Vector2 _rotation;

        private void Start()
        {
            _playerCamera = GetComponentInChildren<Camera>();
            if (_playerCamera != null)
            {
                Vector3 euler = _playerCamera.transform.localEulerAngles;
                float pitch = euler.x > 180f ? euler.x - 360f : euler.x;
                _rotation = new Vector2(pitch, euler.y);
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (_playerCamera == null || ExitButton.IsPaused || HelpPanelController.IsOpen || LabTutorialController.IsModalOpen || DevMenuController.IsOpen) return;

            bool zooming = Input.GetMouseButton(1);
            HandleCameraAdjustments(zooming);

            float targetFov = zooming ? _zoomFov : _normalFov;
            _playerCamera.fieldOfView = Mathf.Lerp(_playerCamera.fieldOfView, targetFov, Time.deltaTime * _zoomSpeed);

            if (RotaryKnob.IsAnyKnobDragging) return;

            float zoomScale = _playerCamera.fieldOfView / _normalFov;
            Vector2 mouseDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            mouseDelta *= _rotateSpeed * zoomScale * Time.deltaTime;

            _rotation.y += mouseDelta.x;
            _rotation.x = Mathf.Clamp(_rotation.x - mouseDelta.y, -90f, 90f);
            _playerCamera.transform.localEulerAngles = _rotation;
        }

        private void HandleCameraAdjustments(bool zooming)
        {
            bool plusDown = Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.KeypadPlus);
            bool minusDown = Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus);
            bool plusHeld = Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.Plus) || Input.GetKey(KeyCode.KeypadPlus);
            bool minusHeld = Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus);
            bool leftBracket = Input.GetKeyDown(KeyCode.LeftBracket);
            bool rightBracket = Input.GetKeyDown(KeyCode.RightBracket);
            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            if ((ctrlHeld && (plusDown || minusDown)) || leftBracket || rightBracket)
            {
                float dir = ((ctrlHeld && plusDown) || rightBracket) ? 1f : -1f;
                _rotateSpeed = Mathf.Clamp(_rotateSpeed + dir * 15f, 15f, 300f);
                LabSetupController.Instance?.ShowCameraStatus(
                    $"Чувствительность камеры: {_rotateSpeed:0}  ([+/-] высота, [Ctrl + +/-] скорость, [Shift + +/-] зум)");
            }
            else if (zooming && (plusDown || minusDown))
            {
                float dir = plusDown ? 1f : -1f;
                _zoomFov = Mathf.Clamp(_zoomFov - dir * 3f, 14f, 40f);
                LabSetupController.Instance?.ShowCameraStatus(
                    $"Зум лупы (FOV): {_zoomFov:0}°  ([+/-] высота, [ПКМ + +/-] лупа, [Shift + +/-] обзор)");
            }
            else if (shiftHeld && (plusDown || minusDown))
            {
                float dir = plusDown ? 1f : -1f;
                _normalFov = Mathf.Clamp(_normalFov - dir * 5f, 25f, 75f);
                _zoomFov = Mathf.Clamp(_zoomFov - dir * 3f, 14f, 40f);
                LabSetupController.Instance?.ShowCameraStatus(
                    $"Угол обзора камеры (FOV): {_normalFov:0}°  ([+/-] высота, [Shift + +/-] зум, [Ctrl + +/-] скорость)");
            }
            else if (!zooming && !shiftHeld && !ctrlHeld && (plusHeld || minusHeld || plusDown || minusDown))
            {
                float dir = (plusHeld || plusDown) ? 1f : -1f;
                float deltaY = dir * _heightSpeed * Time.deltaTime + ((plusDown || minusDown) ? dir * 0.04f : 0f);
                Vector3 pos = transform.position;
                pos.y = Mathf.Clamp(pos.y + deltaY, _minHeight, _maxHeight);
                transform.position = pos;
                LabSetupController.Instance?.ShowCameraStatus(
                    $"Высота камеры: {pos.y:0.00} м  ([+/-] высота, [Shift + +/-] зум, [Ctrl + +/-] скорость)");
            }

            if (zooming && LabSetupController.HoveredObject == null && Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
            {
                _zoomFov = Mathf.Clamp(_zoomFov - Input.mouseScrollDelta.y * 2f, 14f, 40f);
                LabSetupController.Instance?.ShowCameraStatus($"Зум лупы (FOV): {_zoomFov:0}°");
            }
        }
    }
}
