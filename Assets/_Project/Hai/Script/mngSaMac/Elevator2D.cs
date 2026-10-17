using UnityEngine;

/// <summary>
/// Thang máy / Sàn di chuyển 2D (Elevator Platform).
/// Di chuyển mượt mà giữa Điểm A (Point A) và Điểm B (Point B) dựa trên lệnh từ Công tắc.
/// Tự động giữ nhân vật đứng trên sàn không bị trượt khi thang máy di chuyển.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Elevator2D : MonoBehaviour
{
    [Header("Cấu hình Điểm mốc")]
    [Tooltip("Vị trí ban đầu (Point A)")]
    [SerializeField] private Transform pointA;
    [Tooltip("Vị trí khi kích hoạt nút (Point B)")]
    [SerializeField] private Transform pointB;

    [Header("Cấu hình Di chuyển")]
    [SerializeField] private float moveSpeed = 3f;
    [Tooltip("Tự động quay về Point A khi nhả nút hay giữ nguyên vị trí?")]
    [SerializeField] private bool autoReturnOnRelease = true;

    private Rigidbody2D rb;
    private Vector3 targetPos;
    private bool isActivated = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Start()
    {
        if (pointA != null)
        {
            transform.position = pointA.position;
            targetPos = pointA.position;
        }
        else
        {
            targetPos = transform.position;
        }
    }

    private void FixedUpdate()
    {
        // Di chuyển Kinematic Rigidbody2D mượt mà
        Vector3 newPos = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPos);
    }

    /// <summary>
    /// Hàm nhận tín hiệu khi Công tắc / Nút bấm được nhấn (OnPressed)
    /// </summary>
    public void MoveToPointB()
    {
        if (pointB != null)
        {
            targetPos = pointB.position;
            isActivated = true;
        }
    }

    /// <summary>
    /// Hàm nhận tín hiệu khi Công tắc / Nút bấm nhả ra (OnReleased)
    /// </summary>
    public void MoveToPointA()
    {
        if (autoReturnOnRelease && pointA != null)
        {
            targetPos = pointA.position;
            isActivated = false;
        }
    }

    /// <summary>
    /// Đảo ngược vị trí di chuyển (Dành cho Cần gạt / Lever toggle)
    /// </summary>
    public void ToggleElevator()
    {
        if (isActivated)
        {
            MoveToPointA();
        }
        else
        {
            MoveToPointB();
        }
    }

    // Gắn nhân vật làm con của sàn di chuyển để nhân vật không bị trượt
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("PushableBox"))
        {
            // Kiểm tra va chạm từ phía trên sàn
            foreach (ContactPoint2D contact in collision.contacts)
            {
                if (contact.normal.y < -0.5f) // Mặt dưới của vật thể tiếp xúc với mặt trên sàn
                {
                    collision.transform.SetParent(transform);
                    break;
                }
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("PushableBox"))
        {
            if (collision.transform.parent == transform)
            {
                collision.transform.SetParent(null);
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (pointA != null && pointB != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(pointA.position, 0.3f);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(pointB.position, 0.3f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(pointA.position, pointB.position);
        }
    }
}
