using UnityEngine;

public class LavaRise : MonoBehaviour
{
    [Header("Target")]
    public Transform targetPoint;

    [Header("Move")]
    public float moveSpeed = 2f;

    private bool isRising = false;

    void Update()
    {
        if (!isRising) return;

        // Di chuyển chỉ theo trục Y
        float newY = Mathf.MoveTowards(
            transform.position.y,
            targetPoint.position.y,
            moveSpeed * Time.deltaTime
        );

        transform.position = new Vector3(
            transform.position.x,
            newY,
            transform.position.z
        );

        // Khi tới điểm dừng
        if (Mathf.Abs(transform.position.y - targetPoint.position.y) < 0.01f)
        {
            isRising = false;
        }
    }

    public void StartRising()
    {
        isRising = true;
    }
}