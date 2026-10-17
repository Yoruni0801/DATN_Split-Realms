using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BossShockwaveProjectile : MonoBehaviour, IParryable
{
    [SerializeField, Min(0f)] private float lifetime = 3f;
    [SerializeField] private bool canBeBlocked = true;
    [SerializeField] private bool destroyOnHit = true;

    private readonly HashSet<int> hitTargets = new HashSet<int>();
    private Rigidbody2D rb;
    private Collider2D hitboxCollider;
    private Transform attacker;
    private LayerMask playerLayer;
    private int damage = 10;
    private int direction = 1;
    private float speed = 6f;
    private float lifeTimer;
    private bool launched;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        hitboxCollider = GetComponent<Collider2D>();
        if (hitboxCollider != null)
        {
            hitboxCollider.isTrigger = true;
        }
    }

    private void OnEnable()
    {
        lifeTimer = lifetime;
    }

    private void Update()
    {
        if (!launched)
        {
            return;
        }

        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private void FixedUpdate()
    {
        if (!launched)
        {
            return;
        }

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(direction * speed, rb.linearVelocity.y);
        }
        else
        {
            transform.position += Vector3.right * (direction * speed * Time.fixedDeltaTime);
        }
    }

    public void Launch(int direction, float speed, int damage, Transform attacker, LayerMask playerLayer)
    {
        this.direction = direction >= 0 ? 1 : -1;
        this.speed = Mathf.Max(0f, speed);
        this.damage = Mathf.Max(0, damage);
        this.attacker = attacker;
        this.playerLayer = playerLayer;
        hitTargets.Clear();
        lifeTimer = lifetime;
        launched = true;
    }

    public ParryResult OnParried(ParryContext context)
    {
        Destroy(gameObject);
        return ParryResult.Successful(context.DefaultHitStopDuration, true);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryHit(other);
    }

    private void TryHit(Collider2D other)
    {
        if (!launched || other == null || damage <= 0)
        {
            return;
        }

        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        if (player == null)
        {
            return;
        }

        if (playerLayer.value != 0)
        {
            bool colliderMatches = (playerLayer.value & (1 << other.gameObject.layer)) != 0;
            bool rootMatches = (playerLayer.value & (1 << player.transform.root.gameObject.layer)) != 0;
            if (!colliderMatches && !rootMatches)
            {
                return;
            }
        }

        int targetId = player.transform.root.GetInstanceID();
        if (hitTargets.Contains(targetId))
        {
            return;
        }

        Vector2 incomingDirection = new Vector2(direction, 0f);
        IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
            other,
            damage,
            ParryableAttackType.Projectile,
            this,
            attacker,
            incomingDirection,
            canBeBlocked);

        if (resolution == IncomingAttackResolution.Ignored)
        {
            return;
        }

        hitTargets.Add(targetId);
        if (destroyOnHit)
        {
            Destroy(gameObject);
        }
    }
}
