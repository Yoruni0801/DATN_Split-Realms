using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class ParryableMeleeHitbox : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private int damage = 10;
    [SerializeField] private bool canBeBlocked = true;
    [SerializeField] private Transform sourceTransform;
    [SerializeField] private ParryableObject parryableSource;
    [SerializeField] private bool damageOnTrigger = true;
    [SerializeField] private bool damageOnCollision = true;
    public bool isSlowAttack = false;

    [Header("Timing")]
    public float damageCooldown = 0.25f;

    private float lastHitTime = -999f;

    private void Awake()
    {
        if (sourceTransform == null)
        {
            sourceTransform = transform;
        }

        if (parryableSource == null)
        {
            parryableSource = ParryableObject.Ensure(sourceTransform);
        }
    }

    public void Configure(int newDamage, Transform newSourceTransform = null, ParryableObject newParryableSource = null)
    {
        damage = newDamage;

        if (newSourceTransform != null)
        {
            sourceTransform = newSourceTransform;
        }

        if (newParryableSource != null)
        {
            parryableSource = newParryableSource;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (damageOnTrigger)
        {
            TryHitPlayer(collision);
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (damageOnTrigger)
        {
            TryHitPlayer(collision);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (damageOnCollision)
        {
            TryHitPlayer(collision.collider);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (damageOnCollision)
        {
            TryHitPlayer(collision.collider);
        }
    }

    private void TryHitPlayer(Collider2D collision)
    {
        if (collision == null || !collision.CompareTag("Player") || Time.time < lastHitTime + damageCooldown)
        {
            return;
        }

        Transform attackSource = sourceTransform != null ? sourceTransform : transform;
        IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
            collision,
            damage,
            ParryableAttackType.Melee,
            parryableSource,
            attackSource,
            collision.transform.position - attackSource.position,
            canBeBlocked,
            isSlowAttack);

        if (resolution != IncomingAttackResolution.Ignored)
        {
            lastHitTime = Time.time;
        }
    }
}
