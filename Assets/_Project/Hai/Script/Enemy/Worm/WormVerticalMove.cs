using UnityEngine;

public class WormVerticalMove : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveDistance = 2f; // Khoảng cách di chuyển
    public float speed = 2f;        // Tốc độ
    public float waitTime = 1f;     // Thời gian nghỉ
    public bool moveDownwards = true; // Tích vào để chui từ TRÊN XUỐNG

    private Vector3 startPos;
    private Vector3 targetPos;
    private bool isGoingToTarget = true;
    private float timer;

    void Start()
    {
        startPos = transform.position;
        
        // Nếu chọn moveDownwards, đích đến sẽ là tọa độ Y trừ đi moveDistance
        float offset = moveDownwards ? -moveDistance : moveDistance;
        targetPos = startPos + new Vector3(0, offset, 0);
    }

    void Update()
    {
        // 1. Xác định điểm đến hiện tại
        Vector3 currentDestination = isGoingToTarget ? targetPos : startPos;

        // 2. Di chuyển tịnh tiến tọa độ
        transform.position = Vector3.MoveTowards(transform.position, currentDestination, speed * Time.deltaTime);

        // 3. Kiểm tra đã đến đích chưa
        if (Vector3.Distance(transform.position, currentDestination) < 0.001f)
        {
            timer += Time.deltaTime;
            if (timer >= waitTime)
            {
                isGoingToTarget = !isGoingToTarget;
                timer = 0;
            }
        }
    }

    // Vẽ Gizmos để cậu thấy đường đi trong Scene
    private void OnDrawGizmos()
    {
        Gizmos.color = moveDownwards ? Color.red : Color.green;
        Vector3 sPos = Application.isPlaying ? startPos : transform.position;
        float offset = moveDownwards ? -moveDistance : moveDistance;
        Vector3 ePos = sPos + new Vector3(0, offset, 0);
        
        Gizmos.DrawLine(sPos, ePos);
        Gizmos.DrawSphere(ePos, 0.15f);
    }
}