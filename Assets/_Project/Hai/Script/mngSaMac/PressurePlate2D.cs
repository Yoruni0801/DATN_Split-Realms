using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Công tắc dạng Nút Nhấn Giữ 2D (Pressure Plate).
/// Khi có Player hoặc Vật thể đè lên, nút bị lún xuống và kích hoạt sự kiện OnPressed.
/// Khi không còn vật nào đè lên, nút nhả ra và kích hoạt sự kiện OnReleased.
/// </summary>
public class PressurePlate2D : MonoBehaviour
{
    [Header("Cấu hình va chạm")]
    [Tooltip("Tag của các vật thể có thể kích hoạt nút (ví dụ: Player, PushableBox)")]
    [SerializeField] private string[] targetTags = { "Player", "PushableBox" };

    [Header("Hiệu ứng Nút nhấn (Tùy chọn)")]
    [SerializeField] private Transform buttonVisual; // Phần hình ảnh nút sẽ lún xuống
    [SerializeField] private float pressedOffset = -0.1f; // Độ lún khi nhấn xuống (Y-axis local)
    [SerializeField] private float visualSpeed = 5f;

    [Header("Sự kiện Kích hoạt")]
    public UnityEvent OnPressed;
    public UnityEvent OnReleased;

    private int objectsOnPlateCount = 0;
    private Vector3 initialVisualPos;
    private Vector3 targetVisualPos;

    private void Start()
    {
        if (buttonVisual != null)
        {
            initialVisualPos = buttonVisual.localPosition;
            targetVisualPos = initialVisualPos;
        }
    }

    private void Update()
    {
        // Di chuyển hình ảnh nút bấm lún xuống / nhả ra mượt mà
        if (buttonVisual != null)
        {
            buttonVisual.localPosition = Vector3.Lerp(
                buttonVisual.localPosition, 
                targetVisualPos, 
                Time.deltaTime * visualSpeed
            );
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsTargetObject(other))
        {
            objectsOnPlateCount++;

            // Chỉ kích hoạt khi vật thể ĐẦU TIÊN đè lên nút
            if (objectsOnPlateCount == 1)
            {
                if (buttonVisual != null)
                {
                    targetVisualPos = initialVisualPos + new Vector3(0, pressedOffset, 0);
                }

                OnPressed?.Invoke();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (IsTargetObject(other))
        {
            objectsOnPlateCount--;

            // Khi KHÔNG CÒN vật thể nào đè lên nút
            if (objectsOnPlateCount <= 0)
            {
                objectsOnPlateCount = 0;

                if (buttonVisual != null)
                {
                    targetVisualPos = initialVisualPos;
                }

                OnReleased?.Invoke();
            }
        }
    }

    private bool IsTargetObject(Collider2D other)
    {
        if (targetTags == null || targetTags.Length == 0) return true;

        foreach (string tag in targetTags)
        {
            if (other.CompareTag(tag))
            {
                return true;
            }
        }
        return false;
    }
}
