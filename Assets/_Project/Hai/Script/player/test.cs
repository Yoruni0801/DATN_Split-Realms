using UnityEngine;

public class test : MonoBehaviour
{
    public float moveSpeed = 10f;
    public float jumpForce = 12f;
    
    private Rigidbody2D rb;
    private float horizontalInput;
    private bool isGrounded;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // Lấy input từ bàn phím
        horizontalInput = Input.GetAxisRaw("Horizontal");

        // Nhảy (có thể kiểm tra grounded ở đây nếu muốn xịn hơn)
        if (Input.GetButtonDown("Jump") && Mathf.Abs(rb.linearVelocity.y) < 0.01f)
        {
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }
    }

    void FixedUpdate()
    {
        // 1. Tính toán lực cần thiết để đạt tới tốc độ mong muốn
        float targetSpeed = horizontalInput * moveSpeed;
        float speedDiff = targetSpeed - rb.linearVelocity.x;
        
        // 2. Hệ số gia tốc (acceleration) - số càng nhỏ thì Player càng "yếu" trước gió
        float accelRate = 10f; 
        
        // 3. Thay vì gán cứng velocity, ta cộng dồn lực
        float movement = speedDiff * accelRate;
        rb.AddForce(Vector2.right * movement, ForceMode2D.Force);
    }
}