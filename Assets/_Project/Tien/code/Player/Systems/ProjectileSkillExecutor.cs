using UnityEngine;

public sealed class ProjectileSkillExecutor : ISkillExecutor
{
    public SkillAttackType AttackType => SkillAttackType.Projectile;

    public bool CanExecute(SkillData skill, SkillExecutionConfig config)
    {
        return skill != null && config is ProjectileSkillConfig;
    }

    public bool Execute(SkillData skill, SkillExecutionConfig config, SkillCastContext context)
    {
        ProjectileSkillConfig projectileConfig = config as ProjectileSkillConfig;
        if (projectileConfig == null || context == null)
        {
            return false;
        }

        float holdDuration = Mathf.Min(context.HoldDuration, ProjectileSkillConfig.MaxChargeTime);
        GameObject projectilePrefab = projectileConfig.ResolveProjectilePrefab(holdDuration);
        if (projectilePrefab == null)
        {
            return false;
        }

        Vector2 direction = context.FacingDirection < 0f ? Vector2.left : Vector2.right;
        Vector3 origin = context.Origin + ResolveSpawnOffset(projectileConfig, context, direction, holdDuration);
        direction = ResolveAimDirection(projectileConfig, context, origin, direction);
        GameObject projectile = Object.Instantiate(
            projectilePrefab,
            origin,
            projectilePrefab.transform.rotation);

        PlayerMagicProjectile2D projectile2D = projectile.GetComponent<PlayerMagicProjectile2D>();
        if (projectile2D == null)
        {
            EnsureProjectilePhysicsComponents(projectile);
            projectile2D = projectile.AddComponent<PlayerMagicProjectile2D>();
        }

        projectile2D.Configure(
            context.Owner,
            direction,
            Mathf.Max(0.01f, projectileConfig.Speed),
            projectileConfig.ResolveDamage(holdDuration, skill.Damage),
            Mathf.Max(0f, projectileConfig.Lifetime),
            context.TargetLayer,
            skill.SpawnRotationMode,
            skill.SpawnRotationOffset,
            projectileConfig.EnemyImpactSound,
            projectileConfig.StaticImpactSound,
            projectileConfig.BounceBackSpeed,
            projectileConfig.BounceBackLifetime);

        return true;
    }

    private static Vector2 ResolveAimDirection(
        ProjectileSkillConfig config,
        SkillCastContext context,
        Vector3 origin,
        Vector2 fallbackDirection)
    {
        Collider2D target = ResolveAimTarget(config, context, origin, fallbackDirection);
        if (target == null)
        {
            return fallbackDirection;
        }

        Vector2 aimDirection = ((Vector2)target.bounds.center - (Vector2)origin).normalized;
        return aimDirection.sqrMagnitude > 0.0001f ? aimDirection : fallbackDirection;
    }

    private static Collider2D ResolveAimTarget(
        ProjectileSkillConfig config,
        SkillCastContext context,
        Vector3 origin,
        Vector2 facingDirection)
    {
        if (config == null || context == null || !config.AutoAim || config.AutoAimRadius <= 0f)
        {
            return null;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, config.AutoAimRadius, context.TargetLayer);
        Collider2D closest = null;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || IsSelfCollider(hit, context.Owner) || !FairyBeamSkillExecutor.IsValidEnemyTarget(hit))
            {
                continue;
            }

            Vector2 toTarget = (Vector2)hit.bounds.center - (Vector2)origin;
            if (config.AutoAimOnlyFacingDirection && toTarget.x * facingDirection.x < 0f)
            {
                continue;
            }

            float distance = toTarget.sqrMagnitude;
            if (distance >= closestDistance)
            {
                continue;
            }

            closestDistance = distance;
            closest = hit;
        }

        return closest;
    }

    private static bool IsSelfCollider(Collider2D target, Player owner)
    {
        return target != null && owner != null && target.transform.root == owner.transform.root;
    }

    private static void EnsureProjectilePhysicsComponents(GameObject projectile)
    {
        if (projectile == null)
        {
            return;
        }

        if (projectile.GetComponent<Collider2D>() == null)
        {
            CircleCollider2D collider = projectile.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.35f;
        }

        if (projectile.GetComponent<Rigidbody2D>() == null)
        {
            Rigidbody2D body = projectile.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    private static Vector3 ResolveSpawnOffset(ProjectileSkillConfig config, SkillCastContext context, Vector2 direction, float holdDuration)
    {
        Vector2 offset = config.ResolveChargedSpawnOffset(holdDuration);
        switch (config.SpawnOffsetSpace)
        {
            case ProjectileSpawnOffsetSpace.PlayerLocal:
                return context.Body != null ? context.Body.TransformVector(offset) : (Vector3)offset;

            case ProjectileSpawnOffsetSpace.World:
                return context.ScaleOffset(offset);

            default:
                Vector3 scaledOffset = context.ScaleOffset(offset);
                return new Vector3(scaledOffset.x * direction.x, scaledOffset.y, 0f);
        }
    }
}
