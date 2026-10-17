using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class EnemyPatrol : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 25;

    // ─── Tuần tra ────────────────────────────────────────────────
    [Header("Patrol Settings")]
    [Tooltip("Điểm tuần tra trái (world space X)")]
    public float patrolLeftX  = -5f;
    [Tooltip("Điểm tuần tra phải (world space X)")]
    public float patrolRightX =  5f;
    public float patrolSpeed  = 2f;

    // ─── Phát hiện player ────────────────────────────────────────
    [Header("Detection")]
    public float detectRange  = 6f;   // bán kính nhìn thấy
    public float attackRange  = 1.2f; // khoảng cách tấn công
    public LayerMask playerLayer;
    public LayerMask groundLayer;
    [Tooltip("Tag của Player — phải khớp với Tag trong Unity Inspector")]
    public string playerTag = "Player";

    // ─── Tấn công ────────────────────────────────────────────────
    [Header("Attack")]
    public float chaseSpeed       = 3.5f;
    public float attackCooldown   = 1f;   // giây giữa 2 đòn
    public int   attackDamage     = 10;
    public float knockbackForce   = 4f;

    // ─── Mất dấu ─────────────────────────────────────────────────
    [Header("Lost Target")]
    [Tooltip("Giây chờ trước khi quay về nếu mất player")]
    public float lostTargetTime = 2f;

    // ═══════════════════════════════════════════════════════════════
    //  State Machine
    // ═══════════════════════════════════════════════════════════════
    public enum EnemyState { Patrol, Chase, Attack, Return, Hurt, Dead }

    [Header("Debug (read-only)")]
    [SerializeField] private EnemyState _state = EnemyState.Patrol;
    public EnemyState State => _state;
    public int CurrentHealth => _health != null ? Mathf.RoundToInt(_health.CurrentHealth) : maxHealth;

    // ─── Private refs ─────────────────────────────────────────────
    private Rigidbody2D  _rb;
    private Animator     _anim;
    private Transform    _player;
    private SpriteRenderer _sr;
    private EnemySystem.EnemyHealthSystem _health;

    // ─── Internal state ───────────────────────────────────────────
    private float _attackTimer;
    private float _lostTimer;
    private bool  _facingRight = true;
    private Vector2 _spawnPos;

    // ─── Animation parameter IDs (hiệu quả hơn dùng string) ───────
    private static readonly int AnimSpeed      = Animator.StringToHash("Speed");
    private static readonly int AnimIsChasing  = Animator.StringToHash("IsChasing");
    private static readonly int AnimIsAttacking= Animator.StringToHash("IsAttacking");
    private static readonly int AnimIsHurt     = Animator.StringToHash("IsHurt");
    private static readonly int AnimIsDead     = Animator.StringToHash("IsDead");

    // ═══════════════════════════════════════════════════════════════
    //  Unity lifecycle
    // ═══════════════════════════════════════════════════════════════
    void Awake()
    {
        _rb   = GetComponent<Rigidbody2D>();
        _anim = GetComponent<Animator>();
        _sr   = GetComponent<SpriteRenderer>();
        _health = new EnemySystem.EnemyHealthSystem(maxHealth);
        _spawnPos = transform.position;

        // Tìm player lần đầu qua tag
        FindPlayerByTag();
    }

    /// <summary>
    /// Tìm Player bằng tag được chỉnh trong Inspector.
    /// Gọi lại tự động mỗi khi _player bị null (VD: player respawn).
    /// </summary>
    void FindPlayerByTag()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
            _player = playerObj.transform;
        else
            Debug.LogWarning($"[EnemyPatrol] Không tìm thấy GameObject với tag \"{playerTag}\". " +
                             "Kiểm tra lại tag trong Inspector và Unity Tag Manager.");
    }

    void Update()
    {
        if (_state == EnemyState.Dead) return;
        if (_attackTimer > 0f) _attackTimer -= Time.deltaTime;

        // Nếu player bị null (respawn/scene load), tìm lại qua tag
        if (_player == null) FindPlayerByTag();

        switch (_state)
        {
            case EnemyState.Patrol:  UpdatePatrol();  break;
            case EnemyState.Chase:   UpdateChase();   break;
            case EnemyState.Attack:  UpdateAttack();  break;
            case EnemyState.Return:  UpdateReturn();  break;
            case EnemyState.Hurt:    /* handled bằng Animation Event */ break;
        }

        UpdateAnimator();
    }

    // ═══════════════════════════════════════════════════════════════
    //  State updates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Đi qua lại giữa 2 điểm, liên tục kiểm tra có thấy player không.</summary>
    void UpdatePatrol()
    {
        // Di chuyển
        float moveX = _facingRight ? patrolSpeed : -patrolSpeed;
        _rb.linearVelocity = new Vector2(moveX, _rb.linearVelocity.y);

        // Đổi chiều khi chạm điểm biên
        if (_facingRight  && transform.position.x >= patrolRightX) Flip();
        if (!_facingRight && transform.position.x <= patrolLeftX)  Flip();

        // Kiểm tra phát hiện player
        if (CanSeePlayer()) TransitionTo(EnemyState.Chase);
    }

    /// <summary>Đuổi theo player. Chuyển ATTACK nếu đủ gần, RETURN nếu mất dấu.</summary>
    void UpdateChase()
    {
        if (_player == null) { TransitionTo(EnemyState.Return); return; }

        float distToPlayer = Vector2.Distance(transform.position, _player.position);

        if (distToPlayer <= attackRange)
        {
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            TransitionTo(EnemyState.Attack);
            return;
        }

        if (!CanSeePlayer())
        {
            _lostTimer += Time.deltaTime;
            if (_lostTimer >= lostTargetTime) TransitionTo(EnemyState.Return);
        }
        else
        {
            _lostTimer = 0f;
        }

        // Di chuyển về phía player
        float dir = _player.position.x > transform.position.x ? 1f : -1f;
        _rb.linearVelocity = new Vector2(dir * chaseSpeed, _rb.linearVelocity.y);
        if (dir > 0 && !_facingRight) Flip();
        if (dir < 0 &&  _facingRight) Flip();
    }

    /// <summary>Tấn công theo cooldown. Rời ATTACK nếu player chạy ra xa.</summary>
    void UpdateAttack()
    {
        if (_player == null) { TransitionTo(EnemyState.Patrol); return; }

        _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);

        float distToPlayer = Vector2.Distance(transform.position, _player.position);
        if (distToPlayer > attackRange + 0.4f)
        {
            TransitionTo(EnemyState.Chase);
            return;
        }

        // Luôn nhìn về phía player khi đang đánh
        float dir = _player.position.x > transform.position.x ? 1f : -1f;
        if (dir > 0 && !_facingRight) Flip();
        if (dir < 0 &&  _facingRight) Flip();

        // Thực hiện đòn đánh khi hết cooldown
        if (_attackTimer <= 0f)
        {
            _attackTimer = attackCooldown;
            PerformAttack();
        }
    }

    /// <summary>Quay về vị trí spawn, sau đó tiếp tục PATROL.</summary>
    void UpdateReturn()
    {
        float distToSpawn = Vector2.Distance(transform.position, _spawnPos);

        if (distToSpawn < 0.15f)
        {
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            TransitionTo(EnemyState.Patrol);
            return;
        }

        float dir = _spawnPos.x > transform.position.x ? 1f : -1f;
        _rb.linearVelocity = new Vector2(dir * patrolSpeed, _rb.linearVelocity.y);
        if (dir > 0 && !_facingRight) Flip();
        if (dir < 0 &&  _facingRight) Flip();

        // Nếu thấy player lại trong lúc về thì chase
        if (CanSeePlayer()) TransitionTo(EnemyState.Chase);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Logic phụ
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Dùng OverlapCircle để kiểm tra player trong vùng detect,
    /// rồi Raycast để chắc chắn không bị tường chặn.
    /// </summary>
    bool CanSeePlayer()
    {
        if (_player == null) return false;

        float dist = Vector2.Distance(transform.position, _player.position);
        if (dist > detectRange) return false;

        // Line-of-sight check
        Vector2 origin = transform.position;
        Vector2 dir    = (_player.position - transform.position).normalized;
        RaycastHit2D hit = Physics2D.Raycast(origin, dir, detectRange, ~playerLayer);

        // Nếu raycast không trúng tường (hoặc trúng player trước) thì thấy được
        return hit.collider == null || hit.collider.CompareTag("Player");
    }

    /// <summary>Đánh một đòn — gọi Animation Event "OnAttackHit" từ clip.</summary>
    void PerformAttack()
    {
        // Trigger animation tấn công (Animator sẽ tự phát clip rồi gọi Event)
        _anim.SetTrigger("Attack");
    }

    /// <summary>
    /// Animation Event: gắn vào frame đánh trúng trong clip Attack.
    /// Kiểm tra hitbox bán nguyệt trước mặt, gây damage + knockback.
    /// </summary>
    public void OnAttackHit()
    {
        Vector2 hitOrigin = (Vector2)transform.position + Vector2.right * (_facingRight ? attackRange * 0.5f : -attackRange * 0.5f);
        Collider2D hit = Physics2D.OverlapCircle(hitOrigin, attackRange * 0.7f, playerLayer);

        if (hit != null)
        {
            // Gây damage
            IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                hit,
                attackDamage,
                ParryableAttackType.Melee,
                transform,
                hit.transform.position - transform.position);

            if (resolution == IncomingAttackResolution.Ignored)
            {
                var health = hit.attachedRigidbody != null
                    ? hit.attachedRigidbody.GetComponent<PlayerHealth>()
                    : hit.GetComponentInParent<PlayerHealth>();
                if (health != null) health.TakeDamage(attackDamage, transform);
            }

            // Knockback
            var playerRb = hit.attachedRigidbody != null
                ? hit.attachedRigidbody
                : hit.GetComponentInParent<Rigidbody2D>();
            if (playerRb != null)
            {
                float kbDir = hit.transform.position.x > transform.position.x ? 1f : -1f;
                playerRb.linearVelocity = new Vector2(kbDir * knockbackForce, knockbackForce * 0.5f);
            }
        }
    }

    /// <summary>Nhận sát thương từ bên ngoài (player tấn công enemy).</summary>
    public void TakeDamage(int damage)
    {
        if (_state == EnemyState.Dead) return;

        HealthChangeResult result = _health.TakeDamage(damage);
        if (!result.WasApplied)
        {
            return;
        }

        global::DamagePopupSystem.ShowEnemy(transform.position + Vector3.up, result.DamageApplied);

        if (result.KilledTarget) Die();
        else                     StartHurt();
    }

    void StartHurt()
    {
        TransitionTo(EnemyState.Hurt);
        _anim.SetTrigger(AnimIsHurt);
        // Animation Event "OnHurtEnd" sẽ gọi khi clip Hurt xong
    }

    /// <summary>Animation Event: gọi khi clip Hurt kết thúc.</summary>
    public void OnHurtEnd()
    {
        TransitionTo(CanSeePlayer() ? EnemyState.Chase : EnemyState.Patrol);
    }

    void Die()
    {
        _state = EnemyState.Dead;
        _rb.linearVelocity = Vector2.zero;
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _anim.SetBool(AnimIsDead, true);
        GetComponent<Collider2D>().enabled = false;
        // Destroy sau khi animation chết xong (hoặc dùng Animation Event)
        Destroy(gameObject, 2f);
    }

    void TransitionTo(EnemyState next)
    {
        _lostTimer = 0f;
        _state = next;
    }

    void Flip()
    {
        _facingRight = !_facingRight;
        Vector3 s = transform.localScale;
        s.x = -s.x;
        transform.localScale = s;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Animator
    // ═══════════════════════════════════════════════════════════════
    void UpdateAnimator()
    {
        float speed = Mathf.Abs(_rb.linearVelocity.x);
        _anim.SetFloat(AnimSpeed,       speed);
        _anim.SetBool(AnimIsChasing,    _state == EnemyState.Chase);
        _anim.SetBool(AnimIsAttacking,  _state == EnemyState.Attack);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Gizmos (hiển thị trong Scene View)
    // ═══════════════════════════════════════════════════════════════
    void OnDrawGizmosSelected()
    {
        // Vùng phát hiện
        Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, detectRange);

        // Vùng tấn công
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Điểm tuần tra
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(
            new Vector3(patrolLeftX,  transform.position.y),
            new Vector3(patrolRightX, transform.position.y)
        );
        Gizmos.DrawWireSphere(new Vector3(patrolLeftX,  transform.position.y), 0.2f);
        Gizmos.DrawWireSphere(new Vector3(patrolRightX, transform.position.y), 0.2f);
    }
}
