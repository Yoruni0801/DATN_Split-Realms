using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BossThreatDetector : MonoBehaviour
{
    [SerializeField] private DuyBossController boss;
    [SerializeField, Min(0f)] private float lowProjectileHeight = 0.8f;
    [SerializeField, Min(0f)] private float highProjectileHeight = 2.2f;
    [SerializeField, Min(0f)] private float minimumIncomingSpeed = 0.5f;
    [SerializeField, Min(0f)] private float defaultEstimatedTime = 0.2f;
    [SerializeField, Min(0f)] private float sameThreatNotifyInterval = 0.25f;
    [SerializeField, Min(0f)] private float meleeThreatDistance = 2.5f;
    [SerializeField, Min(0.1f)] private float fallbackMeleeThreatSpeed = 6f;
    [SerializeField, Min(1)] private int maxCachedThreats = 32;

    private readonly Dictionary<int, float> nextNotifyTimeByCollider = new Dictionary<int, float>();
    private Collider2D detectorCollider;
    private Collider2D bossBodyCollider;
    private bool invalidSharedBossCollider;

    private void Awake()
    {
        if (boss == null)
        {
            boss = GetComponentInParent<DuyBossController>();
        }

        detectorCollider = GetComponent<Collider2D>();
        bossBodyCollider = boss != null ? boss.GetComponent<Collider2D>() : null;

        invalidSharedBossCollider = boss != null &&
            (boss.gameObject == gameObject || detectorCollider == bossBodyCollider);
        if (invalidSharedBossCollider)
        {
            if (detectorCollider != null)
            {
                detectorCollider.isTrigger = false;
            }

            Debug.LogWarning(
                "BossThreatDetector must be placed on a child object with its own trigger Collider2D. It will not convert the boss body collider into a trigger.",
                this);
            enabled = false;
            return;
        }

        if (detectorCollider != null)
        {
            detectorCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryNotify(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryNotify(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other != null)
        {
            nextNotifyTimeByCollider.Remove(other.GetInstanceID());
        }
    }

    private void TryNotify(Collider2D other)
    {
        if (invalidSharedBossCollider || boss == null || other == null || other.transform.root == boss.transform.root)
        {
            return;
        }

        int colliderId = other.GetInstanceID();
        if (nextNotifyTimeByCollider.TryGetValue(colliderId, out float nextTime) && Time.time < nextTime)
        {
            return;
        }

        if (!TryBuildThreat(other, out DuyBossController.IncomingThreatType threatType, out Vector2 velocity, out float estimatedTime))
        {
            return;
        }

        PruneNotifyCache();
        nextNotifyTimeByCollider[colliderId] = Time.time + sameThreatNotifyInterval;
        boss.NotifyIncomingAttack(other.bounds.center, velocity, threatType, estimatedTime);
    }

    private bool TryBuildThreat(
        Collider2D other,
        out DuyBossController.IncomingThreatType threatType,
        out Vector2 velocity,
        out float estimatedTime)
    {
        threatType = DuyBossController.IncomingThreatType.ProjectileHigh;
        velocity = ResolveVelocity(other);
        estimatedTime = defaultEstimatedTime;

        if (IsMeleeThreat(other))
        {
            float meleeDistance = Vector2.Distance(other.bounds.center, boss.transform.position);
            if (meleeDistance > meleeThreatDistance)
            {
                return false;
            }

            threatType = DuyBossController.IncomingThreatType.Melee;
            if (velocity.sqrMagnitude <= 0.01f)
            {
                Vector2 directionToBoss = ((Vector2)boss.transform.position - (Vector2)other.bounds.center).normalized;
                velocity = directionToBoss.sqrMagnitude > 0.001f
                    ? directionToBoss * fallbackMeleeThreatSpeed
                    : Vector2.right * fallbackMeleeThreatSpeed;
            }

            estimatedTime = defaultEstimatedTime;
            return true;
        }

        if (!IsProjectileThreat(other))
        {
            return false;
        }

        if (!IsIncoming(other.bounds.center, velocity))
        {
            return false;
        }

        float speed = velocity.magnitude;
        if (speed < minimumIncomingSpeed)
        {
            return false;
        }

        float height = ResolveHeightRelativeToBoss(other);
        if (height <= lowProjectileHeight)
        {
            threatType = DuyBossController.IncomingThreatType.ProjectileLow;
        }
        else if (height >= highProjectileHeight)
        {
            threatType = DuyBossController.IncomingThreatType.ProjectileHigh;
        }
        else
        {
            threatType = DuyBossController.IncomingThreatType.ProjectileHigh;
        }

        float projectileDistance = Vector2.Distance(other.bounds.center, boss.transform.position);
        estimatedTime = speed > 0.001f ? Mathf.Max(0f, projectileDistance / speed) : defaultEstimatedTime;
        return true;
    }

    private Vector2 ResolveVelocity(Collider2D other)
    {
        Rigidbody2D body = other.attachedRigidbody != null ? other.attachedRigidbody : other.GetComponentInParent<Rigidbody2D>();
        return body != null ? body.linearVelocity : Vector2.zero;
    }

    private bool IsIncoming(Vector2 sourcePosition, Vector2 velocity)
    {
        if (velocity.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        Vector2 toBoss = (Vector2)boss.transform.position - sourcePosition;
        return Vector2.Dot(velocity.normalized, toBoss.normalized) > 0.35f;
    }

    private bool IsMeleeThreat(Collider2D other)
    {
        return other.isTrigger && other.GetComponentInParent<PlayerMeleeHitbox>() != null;
    }

    private bool IsProjectileThreat(Collider2D other)
    {
        return other.GetComponentInParent<PlayerMagicProjectile2D>() != null ||
            other.GetComponentInParent<AreaSkillProjectile2D>() != null ||
            other.GetComponentInParent<ParryableProjectileAdapter>() != null;
    }

    private float ResolveHeightRelativeToBoss(Collider2D other)
    {
        float bossBottom = bossBodyCollider != null ? bossBodyCollider.bounds.min.y : boss.transform.position.y;
        return other.bounds.center.y - bossBottom;
    }

    private void PruneNotifyCache()
    {
        if (nextNotifyTimeByCollider.Count <= maxCachedThreats)
        {
            return;
        }

        expiredColliderIds.Clear();
        foreach (KeyValuePair<int, float> pair in nextNotifyTimeByCollider)
        {
            if (Time.time >= pair.Value)
            {
                expiredColliderIds.Add(pair.Key);
            }
        }

        for (int i = 0; i < expiredColliderIds.Count; i++)
        {
            nextNotifyTimeByCollider.Remove(expiredColliderIds[i]);
        }
    }

    private readonly List<int> expiredColliderIds = new List<int>(16);
}
