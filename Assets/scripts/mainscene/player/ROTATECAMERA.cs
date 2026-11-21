using UnityEngine;

public class CameraRotator : MonoBehaviour
{
    [Header("Чувствительность")]
    public float sensitivityX = 2f; // Горизонтальная (влево/вправо)
    public float sensitivityY = 2f; // Вертикальная (вверх/вниз)

    [Header("Ограничения по вертикали (в градусах)")]
    public float minYAngle = -80f;
    public float maxYAngle = 80f;

    private float rotationX = 0f; // Наклон вверх/вниз (локально)
    private float rotationY = 0f; // Поворот влево/вправо (глобально)

    void Start()
    {
        // Скрываем курсор и фиксируем его в центре (опционально)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Запоминаем начальное вращение
        rotationY = transform.eulerAngles.y;
        rotationX = transform.eulerAngles.x;
    }

    void Update()
    {
        // Получаем движение мыши
        float mouseX = Input.GetAxis("Mouse X") * sensitivityX;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivityY;

        // Горизонтальный поворот (вокруг Y-оси мира)
        rotationY += mouseX;

        // Вертикальный поворот (вокруг X-оси — но локально)
        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, minYAngle, maxYAngle);

        // Применяем вращение: сначала Y (глобально), потом X (локально)
        transform.rotation = Quaternion.Euler(rotationX, rotationY, 0f);
    }
}