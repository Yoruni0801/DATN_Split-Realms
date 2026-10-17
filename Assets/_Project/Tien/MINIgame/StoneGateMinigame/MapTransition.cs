using UnityEngine;

namespace Tien.Minigames
{
    public class MapTransition : MonoBehaviour
    {
        [Header("Cài đặt Chuyển Map")]
        [Tooltip("Nếu tích vào đây, đây là điểm đi từ Map 1 sang Map 2. Nếu không, đây là điểm từ Map 2 về Map 1.")]
        public bool isToMap2 = true;

        [Tooltip("Vị trí người chơi sẽ xuất hiện sau khi chuyển map (ví dụ: điểm đến ở map 2 hoặc map 1)")]
        public Transform targetSpawnPoint;

        [Tooltip("Vị trí AN TOÀN ở Map 1 nếu người chơi chưa giải mã cổng (Chỉ cần gán khi isToMap2 = false)")]
        public Transform safePointMap1;

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("Player"))
            {
                TeleportPlayer(collision.transform);
            }
        }

        private void OnTriggerEnter(Collider collision)
        {
            if (collision.CompareTag("Player"))
            {
                TeleportPlayer(collision.transform);
            }
        }

        private void TeleportPlayer(Transform playerTransform)
        {
            if (isToMap2)
            {
                // Đi sang Map 2: Bình thường, cứ dịch chuyển qua
                if (targetSpawnPoint != null)
                {
                    playerTransform.position = targetSpawnPoint.position;
                }
            }
            else
            {
                // Đi từ Map 2 Về Map 1
                if (StoneManager.Instance != null && !StoneManager.Instance.isGateSolved && safePointMap1 != null)
                {
                    // Cổng CHƯA giải: Ép người chơi xuất hiện ở điểm an toàn (để không lách qua cổng được)
                    playerTransform.position = safePointMap1.position;
                }
                else
                {
                    // Cổng ĐÃ giải (hoặc chưa gán điểm an toàn): Chuyển về vị trí tương ứng bình thường
                    if (targetSpawnPoint != null)
                    {
                        playerTransform.position = targetSpawnPoint.position;
                    }
                }
            }
        }
    }
}
