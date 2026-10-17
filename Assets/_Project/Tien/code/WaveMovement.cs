using UnityEngine;

public class WaveMovement : MonoBehaviour
{
    [Header("Cài đặt di chuyển")]
    public float speed = 15f;     // Tốc độ sóng cuộn tới
    public float lifetime = 3f;   // Sống được 3 giây rồi tan biến

    private void Start()
    {
        // Tự động hủy sau vài giây để khỏi rác bộ nhớ
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        // Tự động di chuyển về phía trước dựa theo hướng nó đang quay mặt
        float facingDirection = Mathf.Sign(transform.localScale.x);
        transform.position += new Vector3(facingDirection * speed * Time.deltaTime, 0f, 0f);
    }
}