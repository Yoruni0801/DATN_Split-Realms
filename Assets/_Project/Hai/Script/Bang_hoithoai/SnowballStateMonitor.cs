using UnityEngine;

public class SnowballStateMonitor : MonoBehaviour
{
    [Header("Mục tiêu theo dõi")]
    [SerializeField] private GameObject targetWall; // Cánh cửa băng (wall) cần phá hủy
    [SerializeField] private SnowZone snowZone;     // Vùng tạo bóng tuyết (nếu có)

    private IceRockRoller activeRoller;
    private bool spawnedDialogueTriggered = false;
    private bool victoryDialogueTriggered = false;
    private bool hasWallBeenAssigned = false;

    void Start()
    {
        // Kiểm tra xem đã gán wall chưa, tránh việc trigger chiến thắng ngay lập tức ở frame đầu tiên nếu để trống
        if (targetWall != null)
        {
            hasWallBeenAssigned = true;
        }
    }

    void Update()
    {
        // 1. Kiểm tra trạng thái Chiến thắng (Chỉ chạy khi trước đó đã gán cửa băng và nay cửa băng bị phá hủy thành null)
        if (hasWallBeenAssigned && !victoryDialogueTriggered && targetWall == null)
        {
            victoryDialogueTriggered = true;
            if (SnowballDialogueManager.Instance != null)
            {
                SnowballDialogueManager.Instance.TriggerVictory();
            }
        }

        // 2. Tự động tìm quả cầu tuyết đang hoạt động trong scene
        if (activeRoller == null)
        {
            activeRoller = FindFirstObjectByType<IceRockRoller>();
            if (activeRoller != null)
            {
                // Khi bóng tuyết vừa được sinh ra (Nhấn F tạo bóng)
                if (!spawnedDialogueTriggered)
                {
                    spawnedDialogueTriggered = true;
                    if (SnowballDialogueManager.Instance != null)
                    {
                        // Kích hoạt thoại Lăn bóng (trong đó sẽ thực hiện zoom out và hiện thoại "Đúng như ta nghĩ")
                        SnowballDialogueManager.Instance.TriggerRollingDialogue();
                    }
                }
            }
        }
    }
}
