using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class QuicksandTrap : MonoBehaviour
{
    [Header("Quicksand Settings")]
    [Tooltip("Lực kéo người chơi chìm xuống.")]
    [SerializeField] private float sinkSpeed = 1f;

    [Tooltip("Thời gian bị làm chậm áp dụng liên tục khi ở trong cát lún.")]
    [SerializeField] private float slowDuration = 0.2f;

    [Tooltip("Vị trí (theo trục Y) mà nếu người chơi chìm qua sẽ bị tính là chết. (Thường là Y của đáy cát lún). Để trống nếu không dùng.")]
    [SerializeField] private Transform deathPoint;

    [Tooltip("Có cho phép Insta-kill khi chìm quá sâu không?")]
    [SerializeField] private bool instaKillWhenSubmerged = true;

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            // 1. Làm chậm người chơi
            PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
            if (playerHealth != null && !playerHealth.IsDead)
            {
                playerHealth.ApplySlow(slowDuration); // Gọi hàm ApplySlow có sẵn trong PlayerHealth

                // 2. Kéo người chơi chìm xuống (thêm velocity hoặc addforce)
                Rigidbody2D rb = collision.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    // Chèn thêm một vận tốc kéo xuống. 
                    // Chú ý không ghi đè x để người chơi vẫn có thể nhích sang 2 bên
                    Vector2 vel = rb.linearVelocity;
                    
                    if (vel.y > 0)
                    {
                        // Nếu đang nhảy lên, bị lực cát kéo xuống dần
                        vel.y -= sinkSpeed * Time.deltaTime * 10f;
                    }
                    else
                    {
                        // Nếu không nhảy, ép vận tốc rơi bằng đúng tốc độ sinkSpeed (cố định, không bị gia tốc bởi trọng lực)
                        vel.y = -sinkSpeed;
                    }
                    
                    rb.linearVelocity = vel;
                }

                // 3. Kiểm tra xem đã chìm quá đầu chưa
                if (instaKillWhenSubmerged)
                {
                    float killY = deathPoint != null ? deathPoint.position.y : transform.position.y - (GetComponent<Collider2D>().bounds.size.y / 2f);
                    
                    if (collision.transform.position.y < killY)
                    {
                        playerHealth.KillByFall();
                    }
                }
            }
        }
    }
}
