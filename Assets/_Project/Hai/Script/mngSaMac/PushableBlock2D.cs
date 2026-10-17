using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Tilemaps;

/// <summary>
/// Khối đá / Thùng gỗ có thể đẩy được 2D (Pushable Block/Box).
/// Đẩy siêu mượt bất chấp cấu hình Player hay Tilemap.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PushableBlock2D : MonoBehaviour
{
    [Header("Cấu hình Đẩy")]
    [Tooltip("Tốc độ di chuyển của thùng khi bị đẩy (mặc định = 4)")]
    [SerializeField] private float pushSpeed = 4f;
    [Tooltip("Khối lượng thùng")]
    [SerializeField] private float blockMass = 1f;
    [Tooltip("Độ ma sát dừng khi không có ai đẩy")]
    [SerializeField] private float idleDamping = 5f;

    [Header("Tự động Hồi phục (Respawn)")]
    [Tooltip("Tự động đưa thùng về vị trí ban đầu nếu rơi xuống vực (Y < threshold)")]
    [SerializeField] private bool enableRespawn = true;
    [SerializeField] private float respawnThresholdY = -20f;

    [Header("Sự kiện Đẩy (Âm thanh / Hiệu ứng)")]
    public UnityEvent OnStartPush;
    public UnityEvent OnStopPush;

    private Rigidbody2D rb;
    private Collider2D blockCollider;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    
    private bool isBeingPushed = false;
    private float activePushDirX = 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        blockCollider = GetComponent<Collider2D>();
        SetupPhysics();
    }

    private void Start()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    private void SetupPhysics()
    {
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.mass = blockMass;
            rb.linearDamping = idleDamping;
            // Khóa xoay Z để thùng luôn thẳng đứng
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        // Tạo PhysicsMaterial2D không ma sát gán cho thùng để trượt mượt trên sàn
        if (blockCollider != null)
        {
            PhysicsMaterial2D noFrictionMat = new PhysicsMaterial2D("BlockNoFrictionMat")
            {
                friction = 0f,
                bounciness = 0f
            };
            blockCollider.sharedMaterial = noFrictionMat;
        }
    }

    private void Update()
    {
        // Kiểm tra Respawn khi rơi khỏi map
        if (enableRespawn && transform.position.y < respawnThresholdY)
        {
            ResetToInitialPosition();
        }
    }

    private void FixedUpdate()
    {
        if (isBeingPushed && rb != null)
        {
            // Ép vận tốc ngang cho thùng di chuyển theo hướng lực đẩy
            rb.linearDamping = 0f;
            rb.linearVelocity = new Vector2(activePushDirX * pushSpeed, rb.linearVelocity.y);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckAndApplyPush(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        CheckAndApplyPush(collision);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (IsPusher(collision))
        {
            StopPushing();
        }
    }

    private void CheckAndApplyPush(Collision2D collision)
    {
        if (!IsPusher(collision)) return;

        foreach (ContactPoint2D contact in collision.contacts)
        {
            // Kiểm tra va chạm hông (normal.x khoảng -1 hoặc 1)
            if (Mathf.Abs(contact.normal.x) > 0.4f && Mathf.Abs(contact.normal.y) < 0.8f)
            {
                float pushDirX = -Mathf.Sign(contact.normal.x);
                activePushDirX = pushDirX;
                
                if (!isBeingPushed)
                {
                    isBeingPushed = true;
                    OnStartPush?.Invoke();
                }
                return;
            }
        }
    }

    private bool IsPusher(Collision2D collision)
    {
        // Bỏ qua va chạm với sàn/tường Tilemap static
        if (collision.gameObject.isStatic) return false;
        if (collision.gameObject.GetComponent<TilemapCollider2D>() != null) return false;
        if (collision.rigidbody != null && collision.rigidbody.bodyType == RigidbodyType2D.Static) return false;

        // Bỏ qua va chạm với mặt đất bên dưới thùng (chỉ nhận lực đẩy ngang)
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y > 0.7f) // Đang đứng trên mặt đất/sàn
            {
                return false;
            }
        }

        return true;
    }

    private void StopPushing()
    {
        if (isBeingPushed)
        {
            isBeingPushed = false;
            activePushDirX = 0f;

            if (rb != null)
            {
                rb.linearDamping = idleDamping;
            }

            OnStopPush?.Invoke();
        }
    }

    /// <summary>
    /// Đưa khối về lại vị trí ban đầu
    /// </summary>
    public void ResetToInitialPosition()
    {
        StopPushing();
        transform.position = initialPosition;
        transform.rotation = initialRotation;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.linearDamping = idleDamping;
        }
    }
}
