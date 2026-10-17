using UnityEngine;

public class LeftLever : MonoBehaviour
{
    [Header("Cấu hình tương tác")]
    [SerializeField] private LiftPlatformController liftController; // Bộ điều khiển thang máy
    [SerializeField] private GameObject rightLeverObj;              // GameObject cần gạt phải (Trạng thái dừng)

    private bool _isPlayerInRange = false;

    void Update()
    {
        // Khi người chơi ở trong vùng tương tác và nhấn phím F
        if (_isPlayerInRange && Input.GetKeyDown(KeyCode.F))
        {
            ActivateLift();
        }
    }

    private void ActivateLift()
    {
        if (liftController != null)
        {
            liftController.StartMoving();
        }

        // Kích hoạt cần gạt bên phải
        if (rightLeverObj != null)
        {
            rightLeverObj.SetActive(true);
        }

        // Tắt cần gạt bên trái (chính nó) để không bị hiện cả 2 gạc cùng lúc
        gameObject.SetActive(false);
        Debug.Log("[LeftLever] Gạt cần sang TRÁI - Thang máy BẮT ĐẦU CHẠY.");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = true;
            Debug.Log("[LeftLever] Nhấn F để gạt cần TRÁI (Khởi động thang máy).");
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = false;
            Debug.Log("[LeftLever] Đã đi ra ngoài vùng tương tác cần TRÁI.");
        }
    }
}
