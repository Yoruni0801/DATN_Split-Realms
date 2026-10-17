using UnityEngine;

public class SpikeTrap : MonoBehaviour
{
    [Header("Spike Trap Settings")]
    [Tooltip("Nếu bật, player chạm vào là chết ngay lập tức (bỏ qua cả lúc đang bất tử/hurt).")]
    [SerializeField] private bool absoluteInstantKill = true;

    [Tooltip("Sát thương gây ra nếu không dùng absoluteInstantKill.")]
    [SerializeField] private int damageAmount = 99999;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleCollision(collision.gameObject);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        // Gọi cả ở Stay phòng trường hợp player chạm vào lúc đang bất tử,
        // sau đó hết bất tử mà vẫn đang đứng trên gai.
        HandleCollision(collision.gameObject);
    }

    private void HandleCollision(GameObject obj)
    {
        if (obj.CompareTag("Player"))
        {
            PlayerHealth playerHealth = obj.GetComponent<PlayerHealth>();
            if (playerHealth != null && !playerHealth.IsDead)
            {
                if (absoluteInstantKill)
                {
                    // KillByFall() là hàm có sẵn trong PlayerHealth giúp giết player ngay lập tức
                    playerHealth.KillByFall();
                }
                else
                {
                    playerHealth.TakeDamage(damageAmount, transform);
                }
            }
        }
    }
}
