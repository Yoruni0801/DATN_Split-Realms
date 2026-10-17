using UnityEngine;

namespace Project.Hai.Map5
{
    /// <summary>
    /// Script gắn vào Box (hoặc vùng Trigger 2D/3D).
    /// Khi Player bước vào vùng -> Bật GameObject (SetActive true).
    /// Khi Player rời khỏi vùng -> Tắt GameObject (SetActive false).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class BoxImageTrigger : MonoBehaviour
    {
        [Header("=== Đối tượng cần bật / tắt ===")]
        [Tooltip("Kéo GameObject bạn muốn bật khi lại gần và tắt khi đi xa vào đây")]
        [SerializeField] private GameObject targetObject;

        [Tooltip("Nếu có nhiều GameObject muốn bật/tắt cùng lúc, gán vào danh sách này (tùy chọn)")]
        [SerializeField] private GameObject[] additionalObjects;

        [Header("=== Cài đặt ===")]
        [Tooltip("Tag của nhân vật người chơi")]
        [SerializeField] private string playerTag = "Player";

        [Tooltip("Tự động tắt GameObject ngay khi bắt đầu game")]
        [SerializeField] private bool hideOnStart = true;

        private void Awake()
        {
            // Đảm bảo Collider2D được đặt là Trigger để người chơi đi xuyên qua
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void Start()
        {
            if (hideOnStart)
            {
                SetObjectsActive(false);
            }
        }

        #region Xử lý Va chạm 2D
        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (IsPlayer(collision.gameObject))
            {
                SetObjectsActive(true);
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (IsPlayer(collision.gameObject))
            {
                SetObjectsActive(false);
            }
        }
        #endregion

        #region Xử lý Va chạm 3D (Dự phòng)
        private void OnTriggerEnter(Collider other)
        {
            if (IsPlayer(other.gameObject))
            {
                SetObjectsActive(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsPlayer(other.gameObject))
            {
                SetObjectsActive(false);
            }
        }
        #endregion

        private bool IsPlayer(GameObject obj)
        {
            return obj.CompareTag(playerTag) || (obj.transform.parent != null && obj.transform.parent.CompareTag(playerTag));
        }

        private void SetObjectsActive(bool active)
        {
            if (targetObject != null)
            {
                targetObject.SetActive(active);
            }

            if (additionalObjects != null)
            {
                for (int i = 0; i < additionalObjects.Length; i++)
                {
                    if (additionalObjects[i] != null)
                    {
                        additionalObjects[i].SetActive(active);
                    }
                }
            }
        }
    }
}
