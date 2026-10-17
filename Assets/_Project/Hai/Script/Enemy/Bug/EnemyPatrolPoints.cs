using UnityEngine;

public class EnemyPatrolPoints: MonoBehaviour
{
    [Header("Movement Settings")]
    public Transform pointA;
    public Transform pointB;
    public float speed = 2f;
    public float chaseSpeed = 4f; 

    [Header("Detection Settings")]
    public float detectionRange = 5f; 
    public LayerMask playerLayer;    
    private Transform player;

    [Header("Combat Settings")]
    public float attackCooldown = 1.0f;
    public float attackDuration = 0.5f;
    public int attackDamage = 10;
    public float knockbackForce = 7f;
    public float upwardForce = 3f;
    private float lastAttackTime;
    
    private Transform currentTarget;
    private Rigidbody2D rb;
    private Animator anim;
    private bool isChasing = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        currentTarget = pointB;
        
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
    }

    void Update()
    {
        if (Time.time < lastAttackTime + attackDuration)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            return;
        }

        CheckForPlayer();

        if (isChasing && player != null)
        {
            ChaseLogic();
        }
        else
        {
            PatrolLogic();
        }
    }

    void CheckForPlayer()
    {
        if (player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        
        if (distanceToPlayer <= detectionRange)
        {
            isChasing = true;
        }
        else
        {
            isChasing = false;
        }
    }

    void ChaseLogic()
    {
        float directionX = Mathf.Sign(player.position.x - transform.position.x);
        rb.linearVelocity = new Vector2(directionX * chaseSpeed, rb.linearVelocity.y);

        Flip(directionX);
    }

    void PatrolLogic()
    {
        float directionX = currentTarget.position.x - transform.position.x;
        float moveDir = Mathf.Sign(directionX);
        
        rb.linearVelocity = new Vector2(moveDir * speed, rb.linearVelocity.y);
        Flip(moveDir);

        if (Mathf.Abs(directionX) < 0.3f)
        {
            currentTarget = (currentTarget == pointA) ? pointB : pointA;
        }
    }

    void Flip(float dir)
    {
        transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x) * dir, transform.localScale.y, transform.localScale.z);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                anim.SetTrigger("Attack");
                lastAttackTime = Time.time;

                // Xử lý Knockback
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
                    playerRb.AddForce(new Vector2(knockbackDir.x * knockbackForce, upwardForce), ForceMode2D.Impulse);
                }
            }
        }
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
