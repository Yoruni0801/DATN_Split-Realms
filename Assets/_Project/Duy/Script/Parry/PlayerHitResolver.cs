using UnityEngine;

public static class PlayerHitResolver
{
    public static IncomingAttackResolution TryApplyHitToPlayer(
        Collider2D playerCollider,
        int damage,
        ParryableAttackType attackType,
        Transform sourceTransform,
        Vector3 incomingDirection,
        bool canBeBlocked = true,
        bool isSlowAttack = false)
    {
        return TryApplyHitToPlayer(
            playerCollider,
            damage,
            attackType,
            sourceTransform,
            (Vector2?)((Vector2)incomingDirection),
            canBeBlocked,
            isSlowAttack);
    }

    public static IncomingAttackResolution TryApplyHitToPlayer(
        Collider2D playerCollider,
        int damage,
        ParryableAttackType attackType,
        Transform sourceTransform,
        Vector2? incomingDirection = null,
        bool canBeBlocked = true,
        bool isSlowAttack = false)
    {
        return TryApplyHitToPlayer(
            playerCollider,
            damage,
            attackType,
            ResolveParryableSource(sourceTransform),
            sourceTransform,
            incomingDirection,
            canBeBlocked,
            isSlowAttack);
    }

    public static IncomingAttackResolution TryApplyHitToPlayer(
        Collider2D playerCollider,
        int damage,
        ParryableAttackType attackType,
        IParryable parryableSource,
        Transform sourceTransform,
        Vector3 incomingDirection,
        bool canBeBlocked = true,
        bool isSlowAttack = false)
    {
        return TryApplyHitToPlayer(
            playerCollider,
            damage,
            attackType,
            parryableSource,
            sourceTransform,
            (Vector2?)((Vector2)incomingDirection),
            canBeBlocked,
            isSlowAttack);
    }

    public static IncomingAttackResolution TryApplyHitToPlayer(
        Collider2D playerCollider,
        int damage,
        ParryableAttackType attackType,
        IParryable parryableSource,
        Transform sourceTransform,
        Vector2? incomingDirection = null,
        bool canBeBlocked = true,
        bool isSlowAttack = false)
    {
        if (playerCollider == null)
        {
            return IncomingAttackResolution.Ignored;
        }

        PlayerHealth playerHealth = playerCollider.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null)
        {
            return IncomingAttackResolution.Ignored;
        }

        Vector2 sourcePoint = sourceTransform != null
            ? (Vector2)sourceTransform.position
            : (Vector2)playerCollider.transform.position;
        Vector2 hitPoint = playerCollider.bounds.ClosestPoint(sourcePoint);
        Vector2 direction = incomingDirection.HasValue && incomingDirection.Value.sqrMagnitude > 0.0001f
            ? incomingDirection.Value.normalized
            : (hitPoint - sourcePoint).normalized;

        IncomingAttackData attack = new IncomingAttackData(
            damage,
            sourceTransform,
            hitPoint,
            direction,
            attackType,
            canBeBlocked,
            isSlowAttack);

        return playerHealth.TryReceiveAttack(attack, parryableSource ?? ResolveParryableSource(sourceTransform));
    }

    public static bool TryApplyDamageToEnemy(Collider2D targetCollider, int damage)
    {
        return TryApplyDamageToEnemy(targetCollider, damage, null);
    }

    public static bool TryApplyDamageToEnemy(Collider2D targetCollider, int damage, Transform attacker)
    {
        if (targetCollider == null)
        {
            return false;
        }

        if (IsThreatSensorCollider(targetCollider))
        {
            return false;
        }

        // Support Quoc.Boss.BossHealth directly
        Quoc.Boss.BossHealth quocBoss = targetCollider.GetComponentInParent<Quoc.Boss.BossHealth>();
        if (quocBoss != null && !quocBoss.IsDead)
        {
            quocBoss.TakeDamage(damage);
            return true;
        }

        Quoc.QuyVuong.QuyVuongBossHealth quyVuongBoss = targetCollider.GetComponentInParent<Quoc.QuyVuong.QuyVuongBossHealth>();
        if (quyVuongBoss != null && !quyVuongBoss.IsDead)
        {
            quyVuongBoss.TakeDamage(damage);
            return true;
        }

        Quoc.Boss.IDamageable quocDamageable = targetCollider.GetComponentInParent<Quoc.Boss.IDamageable>();
        if (quocDamageable != null)
        {
            quocDamageable.TakeDamage(damage);
            return true;
        }

        IEnemyTarget enemyTarget = targetCollider.GetComponentInParent<IEnemyTarget>();
        if (enemyTarget != null && enemyTarget.IsAlive)
        {
            EnemyDamageUtility.ApplyDamage(enemyTarget, damage, attacker);
            return true;
        }

        Bao.Enemies.BaoEnemyBase baoEnemy = targetCollider.GetComponentInParent<Bao.Enemies.BaoEnemyBase>();
        if (baoEnemy != null && !baoEnemy.IsDead)
        {
            baoEnemy.TakeDamage(damage);
            return true;
        }

        Bao.Enemies.IDamageable baoDamageable = targetCollider.GetComponentInParent<Bao.Enemies.IDamageable>();
        if (baoDamageable != null)
        {
            baoDamageable.TakeDamage(damage);
            return true;
        }

        EnemySystem.IDamageable duyEnemy = targetCollider.GetComponentInParent<EnemySystem.IDamageable>();
        if (duyEnemy != null)
        {
            duyEnemy.TakeDamage(damage);
            return true;
        }

        EnemySystem.StandaloneFlyingShooterEnemy standaloneEnemy = targetCollider.GetComponentInParent<EnemySystem.StandaloneFlyingShooterEnemy>();
        if (standaloneEnemy != null && !standaloneEnemy.IsDead)
        {
            standaloneEnemy.TakeDamage(damage);
            return true;
        }

        Bao.Bosses.DreadwormBoss dreadwormBoss = targetCollider.GetComponentInParent<Bao.Bosses.DreadwormBoss>();
        if (dreadwormBoss != null)
        {
            dreadwormBoss.TakeDamage(damage);
            return true;
        }

        Bao.Bosses.NidusCocoonBoss nidusBoss = targetCollider.GetComponentInParent<Bao.Bosses.NidusCocoonBoss>();
        if (nidusBoss != null)
        {
            nidusBoss.TakeDamage(damage);
            return true;
        }

        Bao.Bosses.RedSlimeBoss redSlimeBoss = targetCollider.GetComponentInParent<Bao.Bosses.RedSlimeBoss>();
        if (redSlimeBoss != null)
        {
            redSlimeBoss.TakeDamage(damage);
            return true;
        }

        return false;
    }

    public static bool IsThreatSensorCollider(Collider2D collider)
    {
        return DamageColliderUtility.IsNonDamageSensor(collider);
    }

    public static IParryable ResolveParryableSource(Transform sourceTransform)
    {
        if (sourceTransform == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours = sourceTransform.GetComponentsInParent<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IParryable parryable)
            {
                return parryable;
            }
        }

        return null;
    }
}
