using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CactusTrap : MonoBehaviour
{
    [Header("Damage Settings")]
    [Tooltip("Sát thương khi chạm bình thường.")]
    [SerializeField] private int contactDamage = 10;
    
    [Tooltip("Sát thương khi nổ/bắn gai.")]
    [SerializeField] private int burstDamage = 30;
    
    [Tooltip("Bán kính vùng nổ sát thương.")]
    [SerializeField] private float burstRadius = 3f;

    [Header("Trigger Settings")]
    [Tooltip("Bán kính phát hiện Player. Player vào vùng này xương rồng mới bắt đầu phồng lên.")]
    [SerializeField] private float detectionRadius = 5f;
    
    [Tooltip("Thời gian phồng to lên cảnh báo trước khi nổ.")]
    [SerializeField] private float swellDuration = 1f;

    [Tooltip("Thời gian nghỉ (cooldown) sau khi nổ, trước khi có thể nổ lại.")]
    [SerializeField] private float cooldownDuration = 2f;

    [Header("Visual & Effects")]
    [Tooltip("Kích thước phóng to khi xương rồng phồng lên.")]
    [SerializeField] private float swellScaleMultiplier = 1.3f;
    
    [Tooltip("Màu sắc cảnh báo khi đang phồng (ví dụ: chuyển dần sang đỏ).")]
    [SerializeField] private Color warningColor = Color.red;

    [Tooltip("Prefab hiệu ứng Particle nổ/bắn gai (Kéo thả Prefab VFX vào đây).")]
    [SerializeField] private GameObject burstVfxPrefab;

    [Tooltip("Layer mask để xác định Player.")]
    [SerializeField] private LayerMask playerLayer;

    private Vector3 originalScale;
    private Color originalColor = Color.white;
    private SpriteRenderer spriteRenderer;
    
    private bool isActive = false;
    private bool isOnCooldown = false;

    private void Awake()
    {
        originalScale = transform.localScale;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }

    private void Update()
    {
        // Nếu đang trong quá trình nổ hoặc đang cooldown thì không cần check nữa
        if (isActive || isOnCooldown) return;

        // Quét tìm Player xung quanh trong bán kính nhận diện
        Collider2D player = Physics2D.OverlapCircle(transform.position, detectionRadius, playerLayer);
        if (player != null && player.CompareTag("Player"))
        {
            // Bắt đầu chu kỳ nổ
            StartCoroutine(BurstRoutine());
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            ApplyDamage(collision.gameObject, contactDamage);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            ApplyDamage(collision.gameObject, contactDamage);
        }
    }

    private IEnumerator BurstRoutine()
    {
        isActive = true;

        // 1. Giai đoạn Phồng to (Warning)
        float timer = 0f;
        while (timer < swellDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / swellDuration;
            
            // scale to dần
            transform.localScale = Vector3.Lerp(originalScale, originalScale * swellScaleMultiplier, progress);
            
            // Đổi màu dần sang màu đỏ (hoặc màu cảnh báo) để tạo cảm giác nguy hiểm
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.Lerp(originalColor, warningColor, progress);
            }
            
            yield return null;
        }

        // 2. Giai đoạn Nổ (Burst)
        BurstAttack();

        // Rung giật thu nhỏ thật nhanh
        transform.localScale = originalScale * 0.8f; 
        if (spriteRenderer != null) spriteRenderer.color = originalColor;
        
        yield return new WaitForSeconds(0.1f);
        transform.localScale = originalScale;

        // 3. Giai đoạn Nghỉ (Cooldown)
        isActive = false;
        isOnCooldown = true;
        yield return new WaitForSeconds(cooldownDuration);
        isOnCooldown = false;
    }

    private void BurstAttack()
    {
        // 1. Sinh ra hiệu ứng hình ảnh (VFX) nếu có
        if (burstVfxPrefab != null)
        {
            Instantiate(burstVfxPrefab, transform.position, Quaternion.identity);
        }

        // 2. Tính toán sát thương nổ
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, burstRadius, playerLayer);
        foreach (Collider2D hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                ApplyDamage(hit.gameObject, burstDamage);
            }
        }
    }

    private void ApplyDamage(GameObject playerObj, int damageAmount)
    {
        PlayerHealth playerHealth = playerObj.GetComponent<PlayerHealth>();
        if (playerHealth != null && !playerHealth.IsDead && !playerHealth.IsHurt)
        {
            playerHealth.TakeDamage(damageAmount, transform);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Vẽ vòng tròn vàng cho vùng Nhận Diện (Trigger)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Vẽ vòng tròn đỏ cho vùng Sát Thương nổ (Damage)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, burstRadius);
    }
}
