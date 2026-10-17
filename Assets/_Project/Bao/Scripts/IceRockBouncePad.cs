using UnityEngine;

/// <summary>
/// Đặt script này lên bất kỳ object nào có Collider2D.
/// Khi IceRockRoller va chạm, cục đá sẽ bị đẩy/bật ra xa.
/// </summary>
public class IceRockBouncePad : MonoBehaviour
{
    public enum PushMode
    {
        /// <summary>Phản xạ vận tốc hiện tại (bật nảy).</summary>
        Bounce,
        /// <summary>Áp lực mạnh bằng AddForce Impulse.</summary>
        StrongPush
    }

    [Header("Chế độ")]
    [SerializeField] private PushMode mode = PushMode.Bounce;

    [Header("Lực")]
    [Tooltip("Bounce = tốc độ nẩy nhẹ. StrongPush = lực impulse.")]
    [SerializeField] private float force = 15f;

    [Tooltip("Lực bổ sung hướng lên trên (hữu ích cho ramp / bệ phóng).")]
    [SerializeField] private float upwardForce = 0f;

    [Tooltip("Nếu bật, vận tốc hiện tại của cục đá sẽ bị xoá trước khi áp lực mới.")]
    [SerializeField] private bool resetVelocityFirst = false;

    [Header("Tuỳ chọn hướng đẩy (StrongPush)")]
    [Tooltip("Nếu bật, dùng hướng tuỳ chỉnh thay vì hướng từ tâm pad đến cục đá.")]
    [SerializeField] private bool useCustomDirection = false;

    [Tooltip("Hướng đẩy tuỳ chỉnh (sẽ được chuẩn hoá). Chỉ dùng khi useCustomDirection = true.")]
    [SerializeField] private Vector2 customDirection = Vector2.right;

    // ----------------------------------------------------------------

    private void OnCollisionEnter2D(Collision2D collision)
    {
        ApplyPush(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        ApplyPush(collision);
    }

    private void ApplyPush(Collision2D collision)
    {
        // Tìm IceRockRoller trên chính object hoặc parent
        IceRockRoller roller = collision.gameObject.GetComponent<IceRockRoller>();
        if (roller == null)
        {
            roller = collision.gameObject.GetComponentInParent<IceRockRoller>();
        }

        if (roller == null)
        {
            return;
        }

        Rigidbody2D rockRb = collision.rigidbody;
        if (rockRb == null)
        {
            return;
        }

        switch (mode)
        {
            case PushMode.Bounce:
                ApplyBounce(rockRb, collision);
                break;

            case PushMode.StrongPush:
                ApplyStrongPush(rockRb, collision);
                break;
        }
    }

    // ----------------------------------------------------------------

    /// <summary>
    /// Bounce: đẩy nhẹ ra theo pháp tuyến, không nhân vận tốc liên tục.
    /// </summary>
    private void ApplyBounce(Rigidbody2D rockRb, Collision2D collision)
    {
        // Hướng từ tâm pad đến tâm cục đá – dùng làm chuẩn "ra xa"
        Vector2 padToRock = ((Vector2)rockRb.transform.position - (Vector2)transform.position);

        // Lấy pháp tuyến trung bình từ các điểm tiếp xúc
        Vector2 normal = GetAverageContactNormal(collision);

        // Nếu pháp tuyến bằng 0 (thiếu contact data) → dùng hướng pad→rock
        if (normal.sqrMagnitude < 0.0001f)
        {
            normal = padToRock.normalized;
        }

        // Nếu pad→rock cũng bằng 0 (trùng vị trí) → dùng Vector2.up
        if (normal.sqrMagnitude < 0.0001f)
        {
            normal = Vector2.up;
        }

        // Đảm bảo pháp tuyến luôn hướng ra xa pad (cùng chiều pad→rock)
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

    /// <summary>
    /// StrongPush: đẩy mạnh cục đá – áp trực tiếp vào linearVelocity
    /// (không phụ thuộc mass).
    /// </summary>
    private void ApplyStrongPush(Rigidbody2D rockRb, Collision2D collision)
    {
        Vector2 pushDir;

        if (useCustomDirection)
        {
            pushDir = customDirection.normalized;
        }
        else
        {
            // Hướng từ tâm pad đến tâm cục đá
            pushDir = ((Vector2)rockRb.transform.position - (Vector2)transform.position).normalized;
        }

        // Tính vận tốc đẩy (force = tốc độ, không phải lực vật lý)
        Vector2 pushVelocity = pushDir * force + Vector2.up * upwardForce;

        if (resetVelocityFirst)
        {
            rockRb.linearVelocity = Vector2.zero;
            rockRb.angularVelocity = 0f;
            rockRb.linearVelocity = pushVelocity;
        }
        else
        {
            rockRb.linearVelocity += pushVelocity;
        }
    }

    // ----------------------------------------------------------------

    /// <summary>
    /// Tính pháp tuyến trung bình từ tất cả contact points.
    /// </summary>
    private static Vector2 GetAverageContactNormal(Collision2D collision)
    {
        Vector2 sum = Vector2.zero;
        int count = collision.contactCount;
        for (int i = 0; i < count; i++)
        {
            sum += collision.GetContact(i).normal;
        }

        return count > 0 ? (sum / count).normalized : Vector2.up;
    }

    // ----------------------------------------------------------------
    // Gizmo để dễ nhìn hướng đẩy trong Editor
    // ----------------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (mode != PushMode.StrongPush || !useCustomDirection)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Vector3 origin = transform.position;
        Vector3 dir = ((Vector3)(customDirection.normalized)) * 2f;
        Gizmos.DrawRay(origin, dir);
        Gizmos.DrawWireSphere(origin + dir, 0.15f);
    }
#endif
}
