using System.Collections;
using UnityEngine;

public class CrystalRepulsor : MonoBehaviour
{
    private Material mat;
    private static readonly int HitTimeProp = Shader.PropertyToID("_HitTime");

    [Header("Hiệu ứng Hào quang (Force Field)")]
    public Color shieldColor = new Color(0f, 0.8f, 1f, 1f) * 3f; // Màu HDR chói lóa
    public float maxRadius = 5f;
    public float expandDuration = 0.4f;

    [Header("Lực đẩy vật thể (Repulsion)")]
    [Tooltip("Lực/vận tốc hất cục đá IceRock ra xa")]
    public float repulsionForce = 15f;

    [Header("Đẩy Tinh thể (Pushing Mechanic)")]
    [Tooltip("Tốc độ khi người chơi đẩy cục tinh thể")]
    public float pushSpeed = 3f;

    [Tooltip("Khối lượng của tinh thể. Điều chỉnh để người chơi đẩy nặng/nhẹ theo ý muốn.")]
    public float crystalMass = 100f;

    [Tooltip("Lực cản ma sát của tinh thể giúp dừng lại nhanh hơn khi ngừng đẩy.")]
    public float crystalDrag = 5f;

    [Tooltip("Trọng lực tác dụng lên tinh thể.")]
    public float crystalGravity = 1f;

    private Rigidbody2D rb;
    #pragma warning disable CS0414
    private bool isPlayerPushing = false;
    #pragma warning restore CS0414

    void Start()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) mat = sr.material;

        // Đảm bảo có Rigidbody2D để hỗ trợ đẩy và trọng lực
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }

        // Cấu hình Rigidbody2D dựa trên các thông số tùy chỉnh từ Inspector
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.mass = crystalMass;
        rb.linearDamping = crystalDrag;
        rb.angularDamping = 0.05f;
        rb.gravityScale = crystalGravity;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation; // Không cho xoay tròn

        // Xóa PhysicsMaterial2D để tránh tự động đẩy tất cả các vật thể (như Player)
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.sharedMaterial = null;
        }
    }

    void Update()
    {
        // Cập nhật lại các thông số thời gian thực từ Inspector nếu người chơi điều chỉnh trong lúc chạy game
        if (rb != null)
        {
            rb.mass = crystalMass;
            rb.linearDamping = crystalDrag;
            rb.gravityScale = crystalGravity;
        }
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (mat != null && col.gameObject.CompareTag("IceRock"))
        {
            // 1. Chớp sáng lõi tinh thể (Shader cũ)
            mat.SetFloat(HitTimeProp, Time.time);

            // 2. Sinh ra vầng hào quang khổng lồ đẩy ra ngoài
            StartCoroutine(SpawnEnergyShield());

            // 3. Đẩy vật thể IceRock ra xa
            Rigidbody2D rockRb = col.gameObject.GetComponent<Rigidbody2D>();
            if (rockRb != null)
            {
                // Tính hướng đẩy từ tâm tinh thể tới cục đá
                Vector2 pushDir = (col.transform.position - transform.position).normalized;
                if (pushDir == Vector2.zero) pushDir = Vector2.up;

                // Thiết lập vận tốc đẩy trực tiếp để đảm bảo độ nảy ổn định độc lập với khối lượng
                rockRb.linearVelocity = pushDir * repulsionForce;
            }

            // 4. Kích hoạt hội thoại khi quả bóng tuyết lần đầu tiên chạm vào tinh thể
            if (SnowballDialogueManager.Instance != null)
            {
                SnowballDialogueManager.Instance.TriggerBounceDialogue();
            }
        }
    }

    void OnCollisionStay2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Player"))
        {
            if (col.contactCount > 0)
            {
                Vector2 normal = col.GetContact(0).normal;
                // Nếu lực va chạm chủ yếu theo phương ngang (sườn bên)
                if (Mathf.Abs(normal.x) > 0.7f)
                {
                    float pushSign = Mathf.Sign(normal.x);
                    
                    // Lấy hướng nhấn phím di chuyển của người chơi (dùng cả GetAxisRaw và phím cứng để luôn chính xác)
                    float horizontalInput = Input.GetAxisRaw("Horizontal");
                    if (horizontalInput == 0f)
                    {
                        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontalInput = 1f;
                        else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontalInput = -1f;
                    }

                    // Nếu player đang nhấn nút di chuyển ngang hướng về phía tinh thể
                    if (Mathf.Abs(horizontalInput) > 0.1f && Mathf.Sign(horizontalInput) == pushSign)
                    {
                        isPlayerPushing = true;
                        rb.linearVelocity = new Vector2(pushSign * pushSpeed, rb.linearVelocity.y);
                        return;
                    }
                }
            }

            // Nếu chạm nhưng không đẩy thì dừng di chuyển ngang
            isPlayerPushing = false;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }

    void OnCollisionExit2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Player"))
        {
            isPlayerPushing = false;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }

    IEnumerator SpawnEnergyShield()
    {
        // Tạo một GameObject mới chứa LineRenderer để vẽ vòng tròn
        GameObject shieldObj = new GameObject("EnergyShield");
        shieldObj.transform.position = transform.position;

        LineRenderer lr = shieldObj.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default")); // Dùng material cơ bản
        lr.startColor = shieldColor;
        lr.endColor = shieldColor;
        lr.useWorldSpace = false;
        lr.loop = true;

        // Vẽ hình tròn bằng 50 điểm
        int segments = 50;
        lr.positionCount = segments;

        float timer = 0;
        while (timer < expandDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / expandDuration; // Tăng từ 0 đến 1

            // Tính bán kính vòng tròn (nhỏ -> khổng lồ)
            float currentRadius = Mathf.Lerp(0.5f, maxRadius, progress);

            // Viền dày lúc đầu, mỏng dần và biến mất lúc sau
            lr.startWidth = Mathf.Lerp(0.5f, 0f, progress);
            lr.endWidth = lr.startWidth;

            // Tính toán vị trí các điểm của hình tròn
            for (int i = 0; i < segments; i++)
            {
                float angle = ((float)i / segments) * 2f * Mathf.PI;
                float x = Mathf.Sin(angle) * currentRadius;
                float y = Mathf.Cos(angle) * currentRadius;
                lr.SetPosition(i, new Vector3(x, y, 0));
            }

            yield return null;
        }

        // Dọn dẹp sau khi nổ xong
        Destroy(shieldObj);
    }
}
