using System.Collections;
using UnityEngine;

public class FallingObstacle : MonoBehaviour
{
    [Header("Chế độ")]
    [Tooltip("Bật nếu object này là vùng kích hoạt DƯỚI ĐẤT; tắt nếu object này là đá rơi trên trần.")]
    [SerializeField] private bool isTriggerZone;
    [Tooltip("Object đá rơi trên trần sẽ kích hoạt khi Player đi vào vùng kích hoạt này.")]
    [SerializeField] private FallingObstacle targetObstacle;

    [Header("Cấu hình Va chạm")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string groundTag = "Ground";
    [SerializeField] private LayerMask groundLayer;
    [Tooltip("Layer của Quái vật để đá đi xuyên qua không bị cản.")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private string enemyTag = "Enemy";

    [Header("Cấu hình Mảnh Vỡ")]
    [SerializeField] private GameObject[] fragmentPrefabs;
    [SerializeField] private int fragmentCount = 5;
    [SerializeField] private float fragmentForce = 3f;
    [SerializeField] private int damageToPlayer = 1;

    [Header("Cấu hình Rung & Rơi")]
    [SerializeField] private float shakeDuration = 0.5f;
    [SerializeField] private float shakeMagnitude = 0.08f;
    [SerializeField] private float fallGravityScale = 3f;

    private Rigidbody2D rb;
    private Collider2D col;
    private Vector3 originalPosition;
    private bool isTriggered;
    private bool isGrounded;
    private bool hasDamagedPlayer;
    private bool isFalling;

    private void Start()
    {
        col = GetComponent<Collider2D>();

        if (isTriggerZone)
        {
            return;
        }

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError("[FallingObstacle] Đá rơi cần có Rigidbody2D.", this);
            enabled = false;
            return;
        }

        originalPosition = transform.position;
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;

        // Tự động bỏ qua va chạm với Layer Quái vật nếu có thiết lập
        if (enemyLayer.value != 0)
        {
            int myLayer = gameObject.layer;
            for (int i = 0; i < 32; i++)
            {
                if ((enemyLayer.value & (1 << i)) != 0)
                {
                    Physics2D.IgnoreLayerCollision(myLayer, i, true);
                }
            }
        }
    }

    public void TriggerFall()
    {
        if (isTriggerZone || isTriggered)
        {
            return;
        }

        isTriggered = true;
        StartCoroutine(ShakeAndDrop());
    }

    private IEnumerator ShakeAndDrop()
    {
        float elapsedTime = 0f;

        while (elapsedTime < shakeDuration)
        {
            float randomX = Random.Range(-1f, 1f) * shakeMagnitude;
            float randomY = Random.Range(-1f, 1f) * shakeMagnitude;

            transform.position = new Vector3(
                originalPosition.x + randomX,
                originalPosition.y + randomY,
                originalPosition.z);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = originalPosition;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = fallGravityScale;
        rb.WakeUp();

        // Chờ 0.12s để quả đá rớt khỏi trần nhà trước khi bật nhận diện va chạm
        yield return new WaitForSeconds(0.12f);
        isFalling = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Trường hợp 1: Object này là Vùng kích hoạt DƯỚI ĐẤT
        if (isTriggerZone)
        {
            if (other.CompareTag(playerTag) && targetObstacle != null)
            {
                targetObstacle.TriggerFall();
                Destroy(gameObject);
            }
            return;
        }

        // Trường hợp 2: Object này là Đá rơi (Chỉ nhận va chạm khi đang rơi)
        if (!isFalling || isGrounded) return;

        // Bỏ qua va chạm với Trigger khác
        if (other.isTrigger) return;

        HandleImpact(other.gameObject, other.transform);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isTriggerZone || !isFalling || isGrounded)
        {
            return;
        }

        // Bỏ qua va chạm với Quái vật theo Tag
        if (collision.gameObject.CompareTag(enemyTag))
        {
            if (col != null && collision.collider != null)
            {
                Physics2D.IgnoreCollision(col, collision.collider, true);
            }
            return;
        }

        HandleImpact(collision.gameObject, collision.transform);
    }

    private void HandleImpact(GameObject hitObject, Transform hitTransform)
    {
        if (isGrounded) return;

        // 1. Chỉ va chạm với Player (Gây sát thương + Vỡ vụn)
        Player activePlayer = PlayerRegistry.Current;
        bool isPlayer = (activePlayer != null && (hitObject == activePlayer.gameObject || hitObject.transform.IsChildOf(activePlayer.transform)))
                        || hitObject.CompareTag(playerTag);

        if (isPlayer || hitObject.GetComponentInParent<PlayerHealth>() != null)
        {
            isGrounded = true;
            TryDamagePlayer(hitObject, hitTransform);
            BreakObstacle();
            return;
        }

        // 2. Chỉ va chạm với Ground (Tag "Ground" HOẶC thuộc Layer "Ground") -> Vỡ vụn
        bool isGroundByTag = !string.IsNullOrEmpty(groundTag) && hitObject.CompareTag(groundTag);
        bool isGroundByLayer = groundLayer.value != 0 && ((1 << hitObject.layer & groundLayer.value) != 0);

        if (isGroundByTag || isGroundByLayer)
        {
            isGrounded = true;
            BreakObstacle();
            return;
        }
    }

    private bool TryDamagePlayer(GameObject hitObject, Transform hitTransform)
    {
        if (hasDamagedPlayer)
        {
            return false;
        }

        PlayerHealth playerHealth = null;
        Player activePlayer = PlayerRegistry.Current;
        if (activePlayer != null && (hitObject == activePlayer.gameObject || hitObject.transform.IsChildOf(activePlayer.transform)))
        {
            playerHealth = activePlayer.GetComponent<PlayerHealth>();
        }
        else
        {
            playerHealth = hitObject.GetComponentInParent<PlayerHealth>();
        }

        if (playerHealth == null)
        {
            return false;
        }

        hasDamagedPlayer = true;
        playerHealth.TakeDamage(damageToPlayer, hitTransform);
        return true;
    }

    private void BreakObstacle()
    {
        SpawnFragments();

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        if (col != null)
        {
            col.enabled = false;
        }

        if (rb != null)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector2.zero;
#else
            rb.velocity = Vector2.zero;
#endif
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }

        Destroy(gameObject, 3f);
    }

    private void SpawnFragments()
    {
        if (fragmentPrefabs == null || fragmentPrefabs.Length == 0)
        {
            return;
        }

        for (int i = 0; i < fragmentCount; i++)
        {
            GameObject prefab = fragmentPrefabs[Random.Range(0, fragmentPrefabs.Length)];
            GameObject fragment = Instantiate(prefab, transform.position, Quaternion.identity);

            Rigidbody2D fragmentRigidbody = fragment.GetComponent<Rigidbody2D>();
            if (fragmentRigidbody != null)
            {
                Vector2 direction = new Vector2(
                    Random.Range(-1f, 1f),
                    Random.Range(0.5f, 1.2f)
                ).normalized;

                fragmentRigidbody.AddForce(direction * fragmentForce, ForceMode2D.Impulse);
                fragmentRigidbody.angularVelocity = Random.Range(-200f, 200f);
            }

            Destroy(fragment, 2f);
        }
    }

    // Vẽ đường nối trực quan giữa Trigger Zone dưới đất và Đá rơi trên trần trong Unity Editor
    private void OnDrawGizmos()
    {
        if (isTriggerZone && targetObstacle != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, targetObstacle.transform.position);
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}