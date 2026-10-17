using UnityEngine;

namespace Quoc.Boss
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class BossMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField, Tooltip("Tốc độ di chuyển cơ bản")]
        private float moveSpeed = 4f;

        [Header("Arena Boundaries")]
        [SerializeField, Tooltip("Điểm giới hạn lề trái arena")]
        private Transform leftBound;

        [SerializeField, Tooltip("Điểm giới hạn lề phải arena")]
        private Transform rightBound;

        [Header("Ground Snap & Gravity (No Collider)")]
        [SerializeField, Tooltip("Layer của mặt đất (Ground) để Boss đáp xuống")]
        private LayerMask groundLayer;

        [SerializeField, Tooltip("Trọng lực rơi tự do cho Boss khi ở trên không (mét/giây^2)")]
        private float gravity = 25f;

        [SerializeField, Tooltip("Khoảng cách tia Raycast kiểm tra sàn dưới chân")]
        private float rayDistance = 15f;

        [SerializeField, Tooltip("Offset chân Boss so với mặt đất (chỉnh nếu lún/lơ lửng)")]
        private float footOffset = 0f;

        [SerializeField, Tooltip("Độ cao bắt đầu bắn Raycast tính từ pivot Boss")]
        private float rayOriginHeight = 1.0f;

        [Header("Wall Collision Check (No Collider)")]
        [SerializeField, Tooltip("Khoảng cách quét tường phía trước Boss (tránh đi xuyên tường)")]
        private float wallCheckDistance = 0.5f;

        [SerializeField, Tooltip("Chiều cao khung quét tường (tính từ chân Boss lên)")]
        private float wallCheckHeight = 1.6f;

        private float verticalVelocity = 0f;
        public bool IsGrounded { get; private set; } = false;

        [Header("Facing Direction")]
        [SerializeField, Tooltip("Sprite mặc định có quay mặt về bên phải không?")]
        private bool defaultFacingRight = true;

        private Rigidbody2D rb;
        private BossController controller;

        public bool IsFacingRight { get; private set; } = true;
        public float LeftX => leftBound != null ? leftBound.position.x : float.MinValue;
        public float RightX => rightBound != null ? rightBound.position.x : float.MaxValue;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            controller = GetComponent<BossController>();
            IsFacingRight = defaultFacingRight;

            // Tắt gravity để Boss không rơi xuống – Raycast Snap sẽ lo việc bám sàn
            if (rb != null)
            {
                rb.gravityScale = 0f;
            }
        }

        private void OnEnable()
        {
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            if (controller == null) controller = GetComponent<BossController>();
        }

        public void Initialize(BossController controller)
        {
            this.controller = controller;
        }

        public void MoveTowardsPlayer()
        {
            if (controller == null || !controller.IsAggroEnabled) return;

            Transform target = controller.Player ?? controller.ResolvePlayer();
            if (target == null) return;

            FacePlayer();
            float direction = IsFacingRight ? 1f : -1f;

            if (rb != null)
            {
                rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
            }
        }

        public void MoveInDirection(float dirX, float speed)
        {
            if (IsAtBoundary(dirX) || IsWalled(dirX))
            {
                StopMovement();
                return;
            }

            if (rb != null)
            {
                rb.linearVelocity = new Vector2(dirX * speed, rb.linearVelocity.y);
            }
        }

        public void StopMovement()
        {
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
        }

        public void FacePlayer()
        {
            if (controller == null || !controller.IsAggroEnabled) return;

            Transform target = controller.Player ?? controller.ResolvePlayer();
            if (target == null) return;

            float dirToPlayer = target.position.x - transform.position.x;
            if (dirToPlayer > 0.1f && !IsFacingRight)
            {
                Flip();
            }
            else if (dirToPlayer < -0.1f && IsFacingRight)
            {
                Flip();
            }
        }

        public void Flip()
        {
            IsFacingRight = !IsFacingRight;
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (IsFacingRight ? 1f : -1f);
            transform.localScale = scale;
        }

        public float ClampToArenaBounds(float xPos)
        {
            if (leftBound != null && xPos < leftBound.position.x)
                return leftBound.position.x;
            if (rightBound != null && xPos > rightBound.position.x)
                return rightBound.position.x;
            return xPos;
        }

        public bool IsAtBoundary(float directionX)
        {
            if (leftBound != null && directionX < 0 && transform.position.x <= leftBound.position.x + 0.3f)
                return true;
            if (rightBound != null && directionX > 0 && transform.position.x >= rightBound.position.x - 0.3f)
                return true;

            return IsWalled(directionX);
        }

        /// <summary>
        /// Kiểm tra xem phía trước mặt Boss có bị vướng tường/Ground Collider không.
        /// </summary>
        public bool IsWalled(float directionX)
        {
            if (groundLayer == 0 || Mathf.Abs(directionX) < 0.01f) return false;

            float dir = directionX > 0 ? 1f : -1f;
            Vector2 origin = (Vector2)transform.position + Vector2.up * (wallCheckHeight * 0.5f);
            Vector2 size = new Vector2(0.1f, wallCheckHeight * 0.8f);

            RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, new Vector2(dir, 0f), wallCheckDistance, groundLayer);
            return hit.collider != null;
        }

        private void LateUpdate()
        {
            // Xử lý Trọng lực rơi & Bám sàn Ground bằng Raycast
            ApplyGravityAndSnap();

            // Kiểm tra giới hạn biên hoặc va chạm tường phía trước
            float currentDirX = rb != null ? rb.linearVelocity.x : 0f;
            if (Mathf.Abs(currentDirX) > 0.05f && IsWalled(currentDirX))
            {
                StopMovement();
            }

            if (leftBound != null || rightBound != null)
            {
                float clampedX = ClampToArenaBounds(transform.position.x);
                if (Mathf.Abs(clampedX - transform.position.x) > 0.01f)
                {
                    transform.position = new Vector3(clampedX, transform.position.y, transform.position.z);
                    if (rb != null)
                    {
                        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                    }
                }
            }
        }

        /// <summary>
        /// Xử lý trọng lực và bám sàn: Rơi xuống nếu ở trên không, đứng yên khi chạm đất Ground.
        /// </summary>
        private void ApplyGravityAndSnap()
        {
            if (groundLayer == 0) return;

            Vector3 rayOrigin = transform.position + Vector3.up * rayOriginHeight;
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, rayOriginHeight + rayDistance, groundLayer);

            if (hit.collider != null)
            {
                float groundY = hit.point.y + footOffset;

                // Nếu vị trí Y của Boss cao hơn mặt đất -> Đang ở trên không -> Rơi xuống theo trọng lực
                if (transform.position.y > groundY + 0.05f)
                {
                    IsGrounded = false;
                    verticalVelocity -= gravity * Time.deltaTime;
                    float newY = transform.position.y + verticalVelocity * Time.deltaTime;

                    // Nếu nhỡ rơi quá mặt đất trong frame này thì tiếp đất ngay
                    if (newY <= groundY)
                    {
                        newY = groundY;
                        verticalVelocity = 0f;
                        IsGrounded = true;
                    }

                    transform.position = new Vector3(transform.position.x, newY, transform.position.z);
                }
                else
                {
                    // Đã tiếp đất
                    IsGrounded = true;
                    verticalVelocity = 0f;
                    transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
                }
            }
            else
            {
                // Không thấy sàn bên dưới -> Rơi tự do
                IsGrounded = false;
                verticalVelocity -= gravity * Time.deltaTime;
                transform.position += new Vector3(0f, verticalVelocity * Time.deltaTime, 0f);
            }
        }

        public void SetVerticalVelocity(float velocityY)
        {
            verticalVelocity = velocityY;
            IsGrounded = false;
        }

        public void SetupBounds(Transform left, Transform right)
        {
            leftBound = left;
            rightBound = right;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 rayOrigin = transform.position + Vector3.up * rayOriginHeight;
            float totalDist = rayOriginHeight + rayDistance;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(rayOrigin, rayOrigin + Vector3.down * totalDist);

            // Điểm bắt đầu Raycast
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(rayOrigin, 0.1f);

            // Điểm nổ Raycast nếu chạm sàn ở Edit Mode
            if (groundLayer != 0)
            {
                RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, totalDist, groundLayer);
                if (hit.collider != null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(hit.point, 0.15f);

                    // Điểm đứng dự kiến của Boss (sau khi tính footOffset)
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawWireSphere(new Vector3(transform.position.x, hit.point.y + footOffset, transform.position.z), 0.1f);
                }
            }

            // Hiển thị khung kiểm tra va chạm tường phía trước
            float facingDir = IsFacingRight ? 1f : -1f;
            Vector2 boxOrigin = (Vector2)transform.position + Vector2.up * (wallCheckHeight * 0.5f);
            Vector2 boxSize = new Vector2(0.1f, wallCheckHeight * 0.8f);
            Gizmos.color = IsWalled(facingDir) ? Color.red : Color.blue;
            Gizmos.DrawWireCube(boxOrigin + new Vector2(facingDir * wallCheckDistance, 0), boxSize);
        }
    }
}
