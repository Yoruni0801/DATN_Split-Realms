using UnityEngine;

public class RightLever : MonoBehaviour
{
    [Header("Cấu hình tương tác")]
    [SerializeField] private LiftPlatformController liftController; // Bộ điều khiển thang máy
    [SerializeField] private GameObject leftLeverObj;               // GameObject cần gạt trái (Trạng thái di chuyển)

    private bool _isPlayerInRange = false;

    void OnEnable()
    {
        // Đăng ký lắng nghe sự kiện thang máy chạm đích khi cần gạt bên phải được bật hoạt động
        if (liftController != null)
        {
            liftController.OnReachedTarget += HandleReachedTarget;
        }
    }

    void OnDisable()
    {
        // Hủy đăng ký sự kiện tránh rò rỉ bộ nhớ
        if (liftController != null)
        {
            liftController.OnReachedTarget -= HandleReachedTarget;
        }
    }

    void Update()
    {
        // Khi người chơi ở trong vùng tương tác và nhấn phím F
        if (_isPlayerInRange && Input.GetKeyDown(KeyCode.F))
        {
            DeactivateLift();
        }
    }

    private void DeactivateLift()
    {
        if (liftController != null)
        {
            liftController.StopMoving();
        }

        // Chuyển lại về cần gạt bên trái
        SwitchToLeftLever();
        Debug.Log("[RightLever] Gạt cần sang PHẢI - Thang máy TẠM DỪNG.");
    }

    private void HandleReachedTarget()
    {
        // Khi thang máy chạm đích thành công
        SwitchToLeftLever();
        Debug.Log("[RightLever] Thang máy tự động chạm đích và dừng hẳn. Cần gạt tự động chuyển về bên TRÁI.");
    }

    private void SwitchToLeftLever()
    {
        if (leftLeverObj != null)
        {
            leftLeverObj.SetActive(true);
        }
        gameObject.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = true;
            Debug.Log("[RightLever] Nhấn F để gạt cần PHẢI (Dừng thang máy).");
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = false;
            Debug.Log("[RightLever] Đã đi ra ngoài vùng tương tác cần PHẢI.");
        }
    }
}
