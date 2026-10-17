using System.Collections;
using UnityEngine;

public class RetractableSpikes2D : MonoBehaviour
{
    [Header("Cấu hình Hướng & Khoảng Cách")]
    [Tooltip("Khoảng cách gai đâm nhô ra (đơn vị Unity)")]
    public float extendDistance = 1.2f;
    [Tooltip("Hướng đâm ra: (0,1) là đâm lên, (1,0) là đâm sang phải, (0,-1) là đâm xuống...")]
    public Vector2 extendDirection = Vector2.up;

    [Header("Cấu hình Nhịp Thời Gian (Giây)")]
    [Tooltip("Thời gian chờ đợt đâm đầu tiên (dùng để xếp gai đâm so le lệch nhịp)")]
    public float startDelay = 0f;
    [Tooltip("Thời gian gai ẩn đứng chờ ở dưới đất")]
    public float retractedDuration = 0.4f;
    [Tooltip("Thời gian gai nhô ra đứng chờ ở trên")]
    public float extendedDuration = 0.4f;
    [Tooltip("Tốc độ đâm ra và thụt vào")]
    public float moveSpeed = 10f;

    [Header("Cấu hình Sát Thương")]
    private int damageAmount = 2;
    public float knockbackForce = 8f;
    public float damageCooldown = 0.4f;

    private Vector3 retractedPos;
    private Vector3 extendedPos;
    private float lastDamageTime = -99f;

    private Coroutine loopCoroutine;
    private Collider2D spikeCollider;

    private void Awake()
    {
        retractedPos = transform.position;
        spikeCollider = GetComponent<Collider2D>();
        if (spikeCollider != null) spikeCollider.enabled = false;
        Vector3 dir = (Vector3)extendDirection.normalized;
        extendedPos = retractedPos + dir * extendDistance;
    }

    private void OnEnable()
    {
        // Khi GameObject/World được bật lại, tự động khởi động lại vòng lặp đâm rút
        if (loopCoroutine != null)
        {
            StopCoroutine(loopCoroutine);
        }
        loopCoroutine = StartCoroutine(SpikeLoopRoutine());
    }

    private void OnDisable()
    {
        // Khi GameObject/World bị tắt (phím M), dừng coroutine và đưa gai về vị trí an toàn
        if (loopCoroutine != null)
        {
            StopCoroutine(loopCoroutine);
            loopCoroutine = null;
        }
        transform.position = retractedPos;
        if (spikeCollider != null) spikeCollider.enabled = false;
    }

    private IEnumerator SpikeLoopRoutine()
    {
        WaitForSeconds waitRetracted = new WaitForSeconds(retractedDuration);
        WaitForSeconds waitExtended = new WaitForSeconds(extendedDuration);

        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        while (true)
        {
            transform.position = retractedPos;
            if (spikeCollider != null) spikeCollider.enabled = false;
            yield return waitRetracted;

            if (spikeCollider != null) spikeCollider.enabled = true;

            while (Vector3.Distance(transform.position, extendedPos) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(transform.position, extendedPos, moveSpeed * Time.deltaTime);
                yield return null;
            }
            transform.position = extendedPos;

            yield return waitExtended;

            while (Vector3.Distance(transform.position, retractedPos) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(transform.position, retractedPos, moveSpeed * Time.deltaTime);
                yield return null;
            }
            transform.position = retractedPos;
        }
    }

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
            Vector2 knockDir = (Vector2)(hitObject.transform.position - transform.position).normalized;
            if (knockDir == Vector2.zero) knockDir = Vector2.up;

#if UNITY_6000_0_OR_NEWER
            hitRb.linearVelocity = knockDir * knockbackForce;
#else
            hitRb.velocity = knockDir * knockbackForce;
#endif
        }
    }

    // Vẽ đường đâm ra trực quan trong Unity Editor
    private void OnDrawGizmosSelected()
    {
        Vector3 start = Application.isPlaying ? retractedPos : transform.position;
        Vector3 end = start + (Vector3)extendDirection.normalized * extendDistance;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawWireSphere(end, 0.2f);
    }
}
