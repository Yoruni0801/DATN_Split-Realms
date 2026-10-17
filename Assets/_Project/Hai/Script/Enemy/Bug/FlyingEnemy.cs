using UnityEngine;

public class FlyingEnemy : MonoBehaviour
{
    [Header("Movement Settings")]
    public float flySpeed = 3f;
    public Vector2 boundarySize = new Vector2(10f, 5f);
    public float waitTimeAtTarget = 1.5f; 
    private Vector2 centerPosition;
    private Vector2 nextTargetPoint;
    private float waitTimer;

    [Header("Detection & Combat")]
    public float detectionRange = 5f;
    public float attackCooldown = 1.5f;
    public float attackDuration = 0.5f;
    public int attackDamage = 10;
    public float knockbackForce = 6f;
    private float lastAttackTime;
    
    private Transform player;
    private Rigidbody2D rb;
    private Animator anim;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        centerPosition = transform.position; 

        rb.gravityScale = 0f; 
        if (anim != null) anim.SetBool("IsFlying", true);

        nextTargetPoint = GetRandomPointInBoundary();

        ResolvePlayer();
    }

    private void ResolvePlayer()
    {
        if (player != null) return;
        Player p = PlayerRegistry.Current;
        if (p != null)
        {
            player = p.transform;
        }
        else
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }
    }

    void Update()
    {
        ResolvePlayer();

        if (Time.time < lastAttackTime + attackDuration)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float distanceToPlayer = (player != null) ? Vector2.Distance(transform.position, player.position) : float.MaxValue;

        if (distanceToPlayer <= detectionRange)
        {
            ChaseLogic();
            waitTimer = 0;
        }
        else
        {
            WanderLogic();
        }

        HandleFlip();
    }
    Vector2 GetRandomPointInBoundary()
    {
        float randomX = Random.Range(centerPosition.x - boundarySize.x / 2, centerPosition.x + boundarySize.x / 2);
        float randomY = Random.Range(centerPosition.y - boundarySize.y / 2, centerPosition.y + boundarySize.y / 2);
        return new Vector2(randomX, randomY);
    }

    void WanderLogic()
    {
        float distanceToTarget = Vector2.Distance(transform.position, nextTargetPoint);

        if (distanceToTarget > 0.2f)
        {
            // Bay tới điểm đã chọn
            Vector2 direction = (nextTargetPoint - (Vector2)transform.position).normalized;
            rb.linearVelocity = direction * flySpeed;
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
            waitTimer += Time.deltaTime;

            if (waitTimer >= waitTimeAtTarget)
            {
                nextTargetPoint = GetRandomPointInBoundary();
                waitTimer = 0;
            }
        }
    }

    void ChaseLogic()
    {
        Vector2 chaseDir = (player.position - transform.position).normalized;
        rb.linearVelocity = chaseDir * (flySpeed * 1.3f);
        
        nextTargetPoint = transform.position; 
    }

    void HandleFlip()
    {
        if (Mathf.Abs(rb.linearVelocity.x) > 0.1f)
        {
            float dir = Mathf.Sign(rb.linearVelocity.x);
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x) * dir, transform.localScale.y, transform.localScale.z);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                if (anim != null) anim.SetTrigger("Attack");
                lastAttackTime = Time.time;

                IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                    collision.collider,
                    attackDamage,
                    ParryableAttackType.Melee,
                    transform,
                    collision.transform.position - transform.position);

                if (resolution == IncomingAttackResolution.Ignored)
                {
                    PlayerHealth health = collision.rigidbody != null
                        ? collision.rigidbody.GetComponent<PlayerHealth>()
                        : collision.gameObject.GetComponentInParent<PlayerHealth>();
                    if (health != null) health.TakeDamage(attackDamage, transform);
                }

                Rigidbody2D playerRb = collision.rigidbody != null
                    ? collision.rigidbody
                    : collision.gameObject.GetComponentInParent<Rigidbody2D>();
                if (playerRb != null)
                {
                    Vector2 knockbackDir = (collision.transform.position - transform.position).normalized;
                    playerRb.linearVelocity = Vector2.zero;
                    playerRb.AddForce(new Vector2(knockbackDir.x * knockbackForce, 3f), ForceMode2D.Impulse);
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Vector3 center = Application.isPlaying ? (Vector3)centerPosition : transform.position;
        Gizmos.DrawWireCube(center, new Vector3(boundarySize.x, boundarySize.y, 0));
        
        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, nextTargetPoint);
            Gizmos.DrawSphere(nextTargetPoint, 0.8f);
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
