using UnityEngine;

/// <summary>
/// Đánh dấu vị trí xuất phát mặc định của Player khi vào scene.
/// Đặt một GameObject có component này trong mỗi map, Player sẽ được dịch chuyển
/// đến đây nếu chưa có checkpoint nào được lưu cho scene đó.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Gameplay/Player Start Point")]
public sealed class PlayerStartPoint : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.35f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.7f);
    }
}
