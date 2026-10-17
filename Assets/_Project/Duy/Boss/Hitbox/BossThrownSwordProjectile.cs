using System.Collections.Generic;
using UnityEngine;

public class BossThrownSwordProjectile : MonoBehaviour
{
    private readonly HashSet<int> hitTargets = new HashSet<int>();

    [SerializeField, Min(0.1f)] private float lifetime = 3f;
    [SerializeField] private bool destroyOnHit = true;

    private int damage;
    private Transform attacker;
    private LayerMask playerLayer;
    private Rigidbody2D rb;
    private float lifetimeTimer;
    private bool initialized;

    public event System.Action Finished;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        Collider2D projectileCollider = GetComponent<Collider2D>();
        if (projectileCollider == null)
        {
            projectileCollider = gameObject.AddComponent<CircleCollider2D>();
        }

        projectileCollider.isTrigger = true;
    }

    private void OnEnable()
    {
        hitTargets.Clear();
        lifetimeTimer = lifetime;
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        lifetimeTimer -= Time.deltaTime;
        if (lifetimeTimer <= 0f)
        {
            Finish();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other);
    }

    public void Launch(Vector2 velocity, int damage, Transform attacker, LayerMask playerLayer)
    {
        this.damage = Mathf.Max(0, damage);
        this.attacker = attacker;
        this.playerLayer = playerLayer;
        initialized = true;
        hitTargets.Clear();
        lifetimeTimer = lifetime;

        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
            }
        }

        if (rb != null)
        {
            rb.linearVelocity = velocity;
        }
    }

    private void TryHit(Collider2D other)
    {
        if (!initialized || other == null || damage <= 0)
        {
            return;
        }

        if (attacker != null && other.transform.root == attacker.root)
        {
            return;
        }

        if (playerLayer.value != 0 &&
            (playerLayer.value & (1 << other.gameObject.layer)) == 0 &&
            (playerLayer.value & (1 << other.transform.root.gameObject.layer)) == 0)
        {
            return;
        }

        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        if (player == null)
        {
            return;
        }

        int targetId = player.transform.root.GetInstanceID();
        if (!hitTargets.Add(targetId))
        {
            return;
        }

        Vector2 direction = rb != null && rb.linearVelocity.sqrMagnitude > 0.01f
            ? rb.linearVelocity.normalized
            : Vector2.right * Mathf.Sign(transform.lossyScale.x);
        IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
            other,
            damage,
            ParryableAttackType.Melee,
            attacker,
            direction,
            true);

        if (resolution == IncomingAttackResolution.Ignored)
        {
            return;
        }

        if (destroyOnHit)
        {
            Finish();
        }
    }

    private void Finish()
    {
        Finished?.Invoke();
        Destroy(gameObject);
    }
}
