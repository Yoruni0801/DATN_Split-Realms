using UnityEngine;

public class WindBlowTrap : MonoBehaviour
{
    public float kickForce = 15f; // Lực hất (cậu chỉnh tùy ý)

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Rigidbody2D rb = collision.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                // Hất văng người chơi sang trái ngay lập tức
                rb.linearVelocity = Vector2.zero; // Triệt tiêu hết vận tốc hiện tại
                rb.AddForce(Vector2.left * kickForce, ForceMode2D.Impulse);
            }
        }
    }
}