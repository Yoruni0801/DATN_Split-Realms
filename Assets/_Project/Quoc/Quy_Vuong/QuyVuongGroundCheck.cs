using UnityEngine;

namespace Quoc.QuyVuong
{
    /// <summary>
    /// Component quản lý Raycast kiểm tra và bám sàn (Ground Snap) cho Boss Quỷ Vương trên Layer Ground.
    /// </summary>
    public class QuyVuongGroundCheck : MonoBehaviour
    {
        [Header("Ground Check Settings")]
        [SerializeField, Tooltip("Layer đại diện cho mặt đất (Ground/Environment)")]
        private LayerMask groundLayer;

        [SerializeField, Tooltip("Độ dài tia Raycast bắn xuống dưới (mét)")]
        private float rayDistance = 10f;

        [SerializeField, Tooltip("Offset độ cao bắt đầu bắn Raycast tính từ Pivot của Boss")]
        private float rayOriginOffset = 1.0f;

        [SerializeField, Tooltip("Offset điều chỉnh chân Boss so với điểm va chạm mặt đất (tránh lún/lơ lửng)")]
        private float footOffset = 0f;

        [Header("Gravity & Snap Option")]
        [SerializeField, Tooltip("Bật tính năng tự động hút/rơi xuống mặt đất trong Update")]
        private bool enableAutoSnap = true;

        [SerializeField, Tooltip("Tốc độ rơi khi lơ lửng trên không (m/s^2)")]
        private float gravity = 25f;

        private float verticalVelocity = 0f;

        public bool IsGrounded { get; private set; }
        public RaycastHit2D CurrentGroundHit { get; private set; }
        public float GroundY { get; private set; }

        private void Update()
        {
            CheckGround();

            if (enableAutoSnap)
            {
                ApplyGravityAndSnap();
            }
        }

        /// <summary>
        /// Thực hiện bắn Raycast xuống phía dưới để phát hiện mặt đất.
        /// </summary>
        public bool CheckGround()
        {
            Vector2 rayOrigin = (Vector2)transform.position + Vector2.up * rayOriginOffset;
            float totalRayLength = rayOriginOffset + rayDistance;

            CurrentGroundHit = Physics2D.Raycast(rayOrigin, Vector2.down, totalRayLength, groundLayer);

            if (CurrentGroundHit.collider != null)
            {
                GroundY = CurrentGroundHit.point.y + footOffset;
                IsGrounded = transform.position.y <= GroundY + 0.05f;
            }
            else
            {
                IsGrounded = false;
            }

            return IsGrounded;
        }

        /// <summary>
        /// Xử lý rơi tự do và khóa vị trí Y của Boss khớp với bề mặt sàn Ground.
        /// </summary>
        private void ApplyGravityAndSnap()
        {
            if (groundLayer == 0) return;

            if (CurrentGroundHit.collider != null)
            {
                // Nếu đang ở trên cao so với sàn -> Rơi dần xuống
                if (transform.position.y > GroundY + 0.05f)
                {
                    IsGrounded = false;
                    verticalVelocity -= gravity * Time.deltaTime;
                    float newY = transform.position.y + verticalVelocity * Time.deltaTime;

                    if (newY <= GroundY)
                    {
                        newY = GroundY;
                        verticalVelocity = 0f;
                        IsGrounded = true;
                    }

                    transform.position = new Vector3(transform.position.x, newY, transform.position.z);
                }
                else
                {
                    // Đã chạm sàn -> Snap vị trí Y về đúng mặt đất
                    IsGrounded = true;
                    verticalVelocity = 0f;
                    transform.position = new Vector3(transform.position.x, GroundY, transform.position.z);
                }
            }
            else
            {
                // Không tìm thấy mặt đất phía dưới -> Rơi tự do
                IsGrounded = false;
                verticalVelocity -= gravity * Time.deltaTime;
                transform.position += new Vector3(0f, verticalVelocity * Time.deltaTime, 0f);
            }
        }

        /// <summary>
        /// Đặt vận tốc rơi theo chiều dọc (ví dụ khi Boss nhảy hoặc bị hất văng)
        /// </summary>
        public void SetVerticalVelocity(float velocityY)
        {
            verticalVelocity = velocityY;
            IsGrounded = false;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 rayOrigin = transform.position + Vector3.up * rayOriginOffset;
            float totalDist = rayOriginOffset + rayDistance;

            // Đường tia Raycast
            Gizmos.color = IsGrounded ? Color.green : Color.yellow;
            Gizmos.DrawLine(rayOrigin, rayOrigin + Vector3.down * totalDist);

            // Điểm bắt đầu Raycast
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(rayOrigin, 0.1f);

            // Điểm tiếp đất nếu chạm Layer Ground
            if (groundLayer != 0)
            {
                RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, totalDist, groundLayer);
                if (hit.collider != null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(hit.point, 0.15f);

                    // Điểm đứng của chân Boss sau offset
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawWireSphere(new Vector3(transform.position.x, hit.point.y + footOffset, transform.position.z), 0.1f);
                }
            }
        }
    }
}
