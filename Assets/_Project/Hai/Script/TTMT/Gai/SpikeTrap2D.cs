using UnityEngine;

public class SpikeTrap2D : MonoBehaviour
{
    [Header("Cấu hình Gai Bẫy")]
    [Tooltip("Số sát thương gây ra (Mặc định 20 để thấy rõ thanh máu giảm)")]
    public int damageAmount = 20;
    [Tooltip("Lực đẩy nẩy Player bật ngược lên khỏi hố gai")]
    public float knockbackUpForce = 10f;
    [Tooltip("Thời gian nạp lại giữa 2 lần gây sát thương (giây)")]
    public float damageCooldown = 0.5f;

    private float lastDamageTime = -99f;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryDamage(collision.gameObject, collision.rigidbody);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryDamage(collision.gameObject, collision.rigidbody);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamage(other.gameObject, other.attachedRigidbody);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamage(other.gameObject, other.attachedRigidbody);
    }

    private void TryDamage(GameObject hitObject, Rigidbody2D hitRb)
    {
        if (Time.time < lastDamageTime + damageCooldown) return;

        PlayerHealth health = null;
        Player activePlayer = PlayerRegistry.Current;

        if (activePlayer != null && (hitObject == activePlayer.gameObject || hitObject.transform.IsChildOf(activePlayer.transform)))
        {
            health = activePlayer.GetComponent<PlayerHealth>();
        }
        else if (hitObject.CompareTag("Player"))
        {
            health = hitObject.GetComponent<PlayerHealth>() ?? hitObject.GetComponentInParent<PlayerHealth>();
        }
        else
        {
            return;
        }

        if (health == null) return;

        lastDamageTime = Time.time;

        health.ApplyEnvironmentalDamage(damageAmount);

        if (hitRb == null)
        {
            hitRb = health.GetComponent<Rigidbody2D>();
        }

        if (hitRb != null)
        {
#if UNITY_6000_0_OR_NEWER
            hitRb.linearVelocity = new Vector2(hitRb.linearVelocity.x, knockbackUpForce);
#else
            hitRb.velocity = new Vector2(hitRb.velocity.x, knockbackUpForce);
#endif
        }
    }
}
