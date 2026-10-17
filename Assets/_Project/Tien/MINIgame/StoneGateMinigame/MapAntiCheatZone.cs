using UnityEngine;

namespace Tien.Minigames
{
    public class MapAntiCheatZone : MonoBehaviour
    {
        [Tooltip("Điểm người chơi sẽ bị ném về nếu lách luật qua cửa")]
        public Transform safeTeleportPoint;

        private void OnEnable()
        {
            // Kiểm tra ngay khi GameObject (Map 1) vừa được bật lên
            CheckAndTeleportPlayer();
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("Player"))
            {
                CheckAndTeleportPlayer(collision.transform);
            }
        }

        private void OnTriggerStay2D(Collider2D collision)
        {
            // Kiểm tra liên tục nếu người chơi đang đứng trong vùng này
            if (collision.CompareTag("Player"))
            {
                CheckAndTeleportPlayer(collision.transform);
            }
        }

        private void CheckAndTeleportPlayer(Transform playerTrans = null)
        {
            // Logic MỚI: Bất kể đã nhận nhiệm vụ hay chưa, CHỈ CẦN cửa chưa được giải là bị dịch chuyển
            if (StoneManager.Instance != null && !StoneManager.Instance.isGateSolved)
            {
                // Tìm Player nếu chưa truyền vào
                if (playerTrans == null)
                {
                    GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                    if (playerObj != null) playerTrans = playerObj.transform;
                }

                if (playerTrans != null)
                {
                    // Lấy TẤT CẢ các Collider2D đang có trên Object này (kể cả Polygon)
                    Collider2D[] myColliders = GetComponents<Collider2D>();
                    bool isInside = false;

                    if (myColliders.Length > 0)
                    {
                        foreach (var col in myColliders)
                        {
                            // Kiểm tra xem vị trí của Player có nằm trong Collider này không
                            // Cách này hoạt động hoàn hảo với cả PolygonCollider2D
                            if (col.OverlapPoint(playerTrans.position))
                            {
                                isInside = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Nếu do hàm Trigger gọi, mặc định là đã ở bên trong
                        isInside = true;
                    }

                    if (isInside)
                    {
                        Debug.Log("Phát hiện người chơi lách luật qua Map 2! Dịch chuyển về vùng an toàn.");
                        if (safeTeleportPoint != null)
                        {
                            playerTrans.position = safeTeleportPoint.position;
                        }
                        else
                        {
                            Debug.LogWarning("Chưa gán safeTeleportPoint (điểm ném về) trong MapAntiCheatZone!");
                        }
                    }
                }
            }
        }
    }
}
