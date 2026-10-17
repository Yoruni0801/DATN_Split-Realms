using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BossAttackHitbox : MonoBehaviour
{
    private readonly Dictionary<int, float> nextDamageTimeByTarget = new Dictionary<int, float>();
    private readonly RaycastHit2D[] sweepHits = new RaycastHit2D[16];

    [SerializeField] private Collider2D hitboxCollider;
    [SerializeField, Min(0f)] private float colliderEnableDelay = 0.03f;
    [SerializeField, Min(0f)] private float minimumSwingMovement = 0.02f;
    [SerializeField, Min(0.05f)] private float continuousDamageInterval = 0.2f;
    [SerializeField] private bool drawDebugSweep;

    private int damage;
    private Transform attacker;
    private bool canBeBlocked = true;
    private bool isActive;
    private bool colliderEnabledForAttack;
    private LayerMask playerLayer;
    private float colliderEnableTimer;
    private Vector2 previousPosition;
    private Vector2 currentPosition;
    private Vector2 lastSwingDirection;
    private bool hasPreviousPosition;

    public event System.Action<Collider2D> OnHit;

    private void Awake()
    {
        ResolveHitboxCollider();
        if (hitboxCollider != null)
        {
            hitboxCollider.isTrigger = true;
            hitboxCollider.enabled = false;
        }

        enabled = false;
    }

    private void FixedUpdate()
    {
        if (!isActive || hitboxCollider == null)
        {
            return;
        }

        if (!colliderEnabledForAttack)
        {
            TickColliderEnableDelay();
            return;
        }

        SweepWeapon();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryHit(other);
    }

    public void SetPlayerLayer(LayerMask layer)
    {
        playerLayer = layer;
    }

    public void BeginAttack(int damage, Transform attacker, bool canBeBlocked)
    {
        enabled = true;
        nextDamageTimeByTarget.Clear();
        this.damage = Mathf.Max(0, damage);
        this.attacker = attacker;
        this.canBeBlocked = canBeBlocked;

        if (hitboxCollider == null)
        {
            ResolveHitboxCollider();
        }

        if (hitboxCollider == null)
        {
            isActive = false;
            Debug.LogError($"{name}: BossAttackHitbox could not find a Collider2D.", this);
            return;
        }

        currentPosition = transform.position;
        previousPosition = currentPosition;
        lastSwingDirection = Vector2.zero;
        hasPreviousPosition = true;
        isActive = true;
        colliderEnabledForAttack = false;
        colliderEnableTimer = colliderEnableDelay;
        hitboxCollider.isTrigger = true;
        hitboxCollider.enabled = false;
        Physics2D.SyncTransforms();

        if (colliderEnableTimer <= 0f)
        {
            EnableColliderForAttack();
        }
    }

    public void EndAttack()
    {
        isActive = false;
        colliderEnabledForAttack = false;
        colliderEnableTimer = 0f;
        hasPreviousPosition = false;
        lastSwingDirection = Vector2.zero;
        if (hitboxCollider != null)
        {
            hitboxCollider.enabled = false;
        }

        enabled = false;
    }

    public void CancelAttack()
    {
        EndAttack();
        nextDamageTimeByTarget.Clear();
    }

    private void TickColliderEnableDelay()
    {
        colliderEnableTimer -= Time.fixedDeltaTime;
        if (colliderEnableTimer > 0f)
        {
            return;
        }

        EnableColliderForAttack();
    }

    private void EnableColliderForAttack()
    {
        if (!isActive || hitboxCollider == null)
        {
            return;
        }

        currentPosition = transform.position;
        previousPosition = currentPosition;
        lastSwingDirection = Vector2.zero;
        hasPreviousPosition = true;
        colliderEnabledForAttack = true;
        hitboxCollider.enabled = true;
        Physics2D.SyncTransforms();
    }

    private void SweepWeapon()
    {
        if (!hasPreviousPosition)
        {
            previousPosition = transform.position;
            hasPreviousPosition = true;
            return;
        }

        currentPosition = transform.position;
        Vector2 movement = currentPosition - previousPosition;
        float distance = movement.magnitude;
        if (distance < minimumSwingMovement)
        {
            previousPosition = currentPosition;
            return;
        }

        Vector2 direction = movement / distance;
        lastSwingDirection = direction;
        int hitCount = CastWeapon(previousPosition, direction, distance);
        for (int i = 0; i < hitCount; i++)
        {
            TryHit(sweepHits[i].collider, direction);
        }

        previousPosition = currentPosition;
    }

    private int CastWeapon(Vector2 fromPosition, Vector2 direction, float distance)
    {
        int layerMask = playerLayer.value != 0 ? playerLayer.value : Physics2D.DefaultRaycastLayers;
        ContactFilter2D contactFilter = new ContactFilter2D();
        contactFilter.SetLayerMask(layerMask);
        contactFilter.useTriggers = Physics2D.queriesHitTriggers;
        float angle = transform.eulerAngles.z;

        if (hitboxCollider is CapsuleCollider2D capsule)
        {
            Vector2 center = fromPosition + Rotate(ScaleVector(capsule.offset), angle);
            Vector2 size = ScaleVector(capsule.size);
            return Physics2D.CapsuleCast(
                center,
                size,
                capsule.direction,
                angle,
                direction,
                contactFilter,
                sweepHits,
                distance);
        }

        if (hitboxCollider is BoxCollider2D box)
        {
            Vector2 center = fromPosition + Rotate(ScaleVector(box.offset), angle);
            Vector2 size = ScaleVector(box.size);
            return Physics2D.BoxCast(
                center,
                size,
                angle,
                direction,
                contactFilter,
                sweepHits,
                distance);
        }

        if (hitboxCollider is CircleCollider2D circle)
        {
            Vector2 center = fromPosition + Rotate(ScaleVector(circle.offset), angle);
            float radius = circle.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
            return Physics2D.CircleCast(
                center,
                radius,
                direction,
                contactFilter,
                sweepHits,
                distance);
        }

        Bounds bounds = hitboxCollider.bounds;
        Vector2 currentBase = transform.position;
        Vector2 previousCenter = fromPosition + ((Vector2)bounds.center - currentBase);
        return Physics2D.BoxCast(
            previousCenter,
            bounds.size,
            angle,
            direction,
            contactFilter,
            sweepHits,
            distance);
    }

    private void TryHit(Collider2D other)
    {
        TryHit(other, lastSwingDirection);
    }

    private void TryHit(Collider2D other, Vector2 swingDirection)
    {
        if (!isActive || !colliderEnabledForAttack || other == null || damage <= 0)
        {
            return;
        }

        if (attacker != null && other.transform.root == attacker.root)
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
        if (nextDamageTimeByTarget.TryGetValue(targetId, out float nextTime) &&
            Time.time < nextTime)
        {
            return;
        }

        Vector2 direction = swingDirection.sqrMagnitude > 0.0001f
            ? swingDirection.normalized
            : ResolveFallbackDirection(other);
        IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
            other,
            damage,
            ParryableAttackType.Melee,
            attacker,
            direction,
            canBeBlocked);

        if (resolution == IncomingAttackResolution.Ignored)
        {
            return;
        }

        nextDamageTimeByTarget[targetId] = Time.time + continuousDamageInterval;
        OnHit?.Invoke(other);
    }

    private Vector2 ResolveFallbackDirection(Collider2D other)
    {
        Vector2 source = attacker != null ? (Vector2)attacker.position : (Vector2)transform.position;
        Vector2 direction = ((Vector2)other.bounds.center - source).normalized;
        return direction.sqrMagnitude > 0.0001f ? direction : Vector2.right * Mathf.Sign(transform.lossyScale.x);
    }

    private void ResolveHitboxCollider()
    {
        if (hitboxCollider != null)
        {
            return;
        }

        hitboxCollider = GetComponent<Collider2D>();
    }

    private Vector2 ScaleVector(Vector2 size)
    {
        return new Vector2(size.x * Mathf.Abs(transform.lossyScale.x), size.y * Mathf.Abs(transform.lossyScale.y));
    }

    private static Vector2 Rotate(Vector2 value, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugSweep)
        {
            return;
        }

        Collider2D previewCollider = hitboxCollider != null ? hitboxCollider : GetComponent<Collider2D>();
        if (previewCollider != null)
        {
            Gizmos.color = isActive ? Color.red : Color.gray;
            Gizmos.DrawWireCube(previewCollider.bounds.center, previewCollider.bounds.size);
        }

        if (!hasPreviousPosition)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(previousPosition, currentPosition);
        if (lastSwingDirection.sqrMagnitude > 0.0001f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(currentPosition, currentPosition + lastSwingDirection * 0.5f);
        }
    }
}
