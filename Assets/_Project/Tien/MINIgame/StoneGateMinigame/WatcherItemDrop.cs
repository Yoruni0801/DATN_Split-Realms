using UnityEngine;

namespace Tien.Minigames
{
    public class WatcherItemDrop : MonoBehaviour
    {
        [Header("Cài đặt Giám sát")]
        [Tooltip("Kéo object (quái, tảng đá, v.v...) cần theo dõi xem chừng nào nó chết/ẩn vào đây")]
        public GameObject targetObject;
        
        [Tooltip("Kéo Prefab vật phẩm (ví dụ: Mảnh đá, gỗ...) muốn rớt ra vào đây")]
        public GameObject itemPrefab;

        [Tooltip("Vị trí rớt vật phẩm. Nếu để trống sẽ rớt ngay tại vị trí của cục đang chứa script này")]
        public Transform dropPoint;

        private bool _hasDropped = false;
        private bool _hasTarget = false;

        void Start()
        {
            // Kiểm tra xem đầu game có gán mục tiêu chưa, tránh lỗi chưa gán đã tự rớt đồ
            if (targetObject != null)
            {
                _hasTarget = true;
                // Bắt đầu tiến trình kiểm tra (Tối ưu hóa: không dùng Update)
                StartCoroutine(CheckTargetRoutine());
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] WatcherItemDrop chưa được gán targetObject (Mục tiêu cần theo dõi)!");
            }
        }

        // Dùng Coroutine thay cho Update để tối ưu hiệu suất (CPU). 
        // Thay vì kiểm tra 60-144 lần/giây, giờ ta chỉ kiểm tra 4 lần/giây.
        private System.Collections.IEnumerator CheckTargetRoutine()
        {
            WaitForSeconds waitTime = new WaitForSeconds(0.25f);
            
            while (!_hasDropped && _hasTarget)
            {
                // Dòng này cực kỳ quan trọng:
                // 1. targetObject == null : Bị xóa hoàn toàn bằng lệnh Destroy()
                // 2. !targetObject.activeInHierarchy : Bị ẩn đi bằng lệnh SetActive(false)
                if (targetObject == null || !targetObject.activeInHierarchy)
                {
                    DropItems();
                    yield break; // Thoát và hủy Coroutine ngay lập tức sau khi rớt đồ
                }
                
                yield return waitTime; // Nghỉ 0.25 giây rồi mới kiểm tra tiếp
            }
        }

        private void DropItems()
        {
            _hasDropped = true; // Khóa lại, đảm bảo chỉ rớt đồ 1 lần duy nhất
            
            if (itemPrefab != null)
            {
                // Lấy vị trí rớt đồ: nếu có dropPoint thì theo dropPoint, không thì lấy vị trí của chính script này
                Vector3 spawnPos = dropPoint != null ? dropPoint.position : transform.position;
                
                Instantiate(itemPrefab, spawnPos, Quaternion.identity);
                Debug.Log($"[{gameObject.name}] Mục tiêu đã bị tiêu diệt/ẩn đi! Rớt vật phẩm thành công.");
            }
        }
    }
}
