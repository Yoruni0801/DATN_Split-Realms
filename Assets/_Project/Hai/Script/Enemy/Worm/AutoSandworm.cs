using UnityEngine;

public class SmoothSinkingWorm : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveDistance = 2f; // Khoảng cách thò lên/rụt xuống
    public float speed = 2f;        // Tốc độ di chuyển
    public float waitTime = 1f;     // Thời gian nghỉ ở mỗi đầu

    private Vector3 startPos;
    private Vector3 endPos;
    private bool movingUp = true;
    private float timer;

    void Start()
    {
        // Lưu vị trí ban đầu (đang ẩn hoặc đang thò tùy cậu đặt trong Scene)
        startPos = transform.position;
        // Điểm đích là vị trí ban đầu cộng thêm trục Y
        endPos = startPos + new Vector3(0, moveDistance, 0);
    }

    void Update()
    {
        // 1. Tính toán vị trí mục tiêu hiện tại
        Vector3 target = movingUp ? endPos : startPos;

        // 2. Di chuyển mượt mà tới mục tiêu
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        // 3. Kiểm tra nếu đã đến đích
        if (Vector3.Distance(transform.position, target) < 0.01f)
        {
            // Đợi một chút rồi mới đổi chiều
            timer += Time.deltaTime;
            if (timer >= waitTime)
            {
                movingUp = !movingUp;
                timer = 0;
            }
        }
    }

    // Vẽ đường đi trong Scene để cậu dễ hình dung
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Vector3 sPos = Application.isPlaying ? startPos : transform.position;
        Vector3 ePos = sPos + new Vector3(0, moveDistance, 0);
        Gizmos.DrawLine(sPos, ePos);
        Gizmos.DrawSphere(ePos, 0.1f);
    }
}