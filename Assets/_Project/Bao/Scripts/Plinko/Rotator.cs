using UnityEngine;

public class Rotator : MonoBehaviour
{
    [Header("Rotation")]
    public float rotationSpeed = 120f;
    public bool clockwise = true;

    [Header("Break Behavior")]
    public bool stopOnDestroy = true;

    [Header("Ice Rock Bounce")]
    [Tooltip("Bật/tắt bounce khi va chạm IceRockRoller.")]
    [SerializeField] private bool bounceEnabled = true;

    [Tooltip("Tốc độ nảy ra xa (outward speed).")]
    [SerializeField] private float force = 15f;

    [Tooltip("Lực bổ sung hướng lên trên.")]
    [SerializeField] private float upwardForce = 0f;

    [Tooltip("Xoá vận tốc hiện tại trước khi áp lực nảy.")]
    [SerializeField] private bool resetVelocityFirst = false;

    // ----------------------------------------------------------------
    // Rotation
    // ----------------------------------------------------------------

    void Update()
    {
        float dir = clockwise ? -1f : 1f;
        transform.Rotate(0, 0, rotationSpeed * dir * Time.deltaTime);
    }

    void OnDestroy()
    {
        if (stopOnDestroy)
            enabled = false;
    }

    // ----------------------------------------------------------------
    // Ice Rock Bounce
    // ----------------------------------------------------------------

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryBounceRock(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryBounceRock(collision);
    }

    private void TryBounceRock(Collision2D collision)
    {
        if (!bounceEnabled) return;

        // Tìm IceRockRoller trên chính object hoặc parent
        IceRockRoller roller = collision.gameObject.GetComponent<IceRockRoller>();
        if (roller == null)
        {
            roller = collision.gameObject.GetComponentInParent<IceRockRoller>();
        }

        if (roller == null) return;

        Rigidbody2D rockRb = collision.rigidbody;
        if (rockRb == null) return;

        ApplyBounce(rockRb, collision);
    }

    private void ApplyBounce(Rigidbody2D rockRb, Collision2D collision)
    {
        // Hướng từ tâm rotator đến tâm cục đá – dùng làm chuẩn "ra xa"
        Vector2 padToRock = (Vector2)rockRb.transform.position - (Vector2)transform.position;

        // Pháp tuyến trung bình từ các điểm tiếp xúc
        Vector2 normal = GetAverageContactNormal(collision);

        // Fallback: nếu pháp tuyến bằng 0 → dùng hướng rotator→rock
        if (normal.sqrMagnitude < 0.0001f)
        {
            normal = padToRock.normalized;
        }

        // Fallback: nếu rotator→rock cũng bằng 0 → dùng Vector2.up
        if (normal.sqrMagnitude < 0.0001f)
        {
            normal = Vector2.up;
        }

        // Đảm bảo pháp tuyến luôn hướng ra xa rotator (cùng chiều padToRock)
        if (padToRock.sqrMagnitude > 0.0001f && Vector2.Dot(normal, padToRock) < 0f)
        {
            normal = -normal;
        }

        Vector2 velocity = resetVelocityFirst ? Vector2.zero : rockRb.linearVelocity;
        float outwardSpeed = Vector2.Dot(velocity, normal);
        float targetSpeed = Mathf.Max(0f, force);
        if (outwardSpeed < targetSpeed)
        {
            velocity += normal * (targetSpeed - outwardSpeed);
        }

        velocity += Vector2.up * upwardForce * Time.fixedDeltaTime;
        rockRb.linearVelocity = velocity;
    }

    private static Vector2 GetAverageContactNormal(Collision2D collision)
    {
        Vector2 sum = Vector2.zero;
        int count = collision.contactCount;
        for (int i = 0; i < count; i++)
        {
            sum += collision.GetContact(i).normal;
        }

        return count > 0 ? (sum / count).normalized : Vector2.zero;
    }
}
