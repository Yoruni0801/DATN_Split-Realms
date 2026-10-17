using System.Collections.Generic;
using UnityEngine;

public sealed class AreaSkillExecutor : ISkillExecutor
{
    public SkillAttackType AttackType => SkillAttackType.Area;

    public bool CanExecute(SkillData skill, SkillExecutionConfig config)
    {
        return skill != null && config is AreaSkillConfig;
    }

    public bool Execute(SkillData skill, SkillExecutionConfig config, SkillCastContext context)
    {
        AreaSkillConfig areaConfig = config as AreaSkillConfig;
        if (areaConfig == null || context == null)
        {
            return false;
        }

        if (areaConfig.DeliveryMode != AreaSkillDeliveryMode.InstantArea)
        {
            return areaConfig.DeliveryMode == AreaSkillDeliveryMode.JumpApexSpreadShot
                ? LaunchApexSpreadShot(skill, areaConfig, context)
                : LaunchAreaProjectile(skill, areaConfig, context);
        }

        Vector2 direction = context.FacingDirection < 0f ? Vector2.left : Vector2.right;
        Vector3 scaledOffset = context.ScaleOffset(areaConfig.Offset);
        Vector3 center = context.Origin + new Vector3(scaledOffset.x * direction.x, scaledOffset.y, 0f);
        SpawnArea(skill, areaConfig, context.Owner, center, context.TargetLayer);

        return true;
    }

    public static void SpawnArea(
        SkillData skill,
        AreaSkillConfig areaConfig,
        Player owner,
        Vector3 center,
        LayerMask targetLayer,
        Transform excludedRoot = null)
    {
        if (skill == null || areaConfig == null || owner == null)
        {
            return;
        }

        if (areaConfig.AreaPrefab != null)
        {
            GameObject area = Object.Instantiate(
                areaConfig.AreaPrefab,
                center,
                areaConfig.AreaPrefab.transform.rotation);
            foreach (HitboxDamage hitbox in area.GetComponentsInChildren<HitboxDamage>(true))
            {
                hitbox.Configure(owner, new SkillHitData(skill, skill.Damage));
            }

            Object.Destroy(area, Mathf.Max(0.05f, areaConfig.Duration));
        }

        int damage = Mathf.RoundToInt(skill.Damage);
        HashSet<Transform> damagedRoots = new HashSet<Transform>();
        float radius = owner != null && owner.ScaleProvider != null
            ? owner.ScaleProvider.ScaleDistance(areaConfig.Radius)
            : areaConfig.Radius;
        foreach (Collider2D collider in Physics2D.OverlapCircleAll(center, radius, targetLayer))
        {
            if (collider == null)
            {
                continue;
            }

            Transform root = collider.transform.root;
            if (root == excludedRoot)
            {
                continue;
            }

            if (root != null && !damagedRoots.Add(root))
            {
                continue;
            }

            PlayerHitResolver.TryApplyDamageToEnemy(collider, damage, owner.transform);
        }
    }

    private static bool LaunchAreaProjectile(SkillData skill, AreaSkillConfig areaConfig, SkillCastContext context)
    {
        if (areaConfig.ProjectilePrefab == null)
        {
            return false;
        }

        float facing = context.FacingDirection < 0f ? -1f : 1f;
        Vector3 scaledOffset = context.ScaleOffset(areaConfig.ProjectileSpawnOffset);
        Vector3 spawnPosition = context.Origin + new Vector3(
            scaledOffset.x * facing,
            scaledOffset.y,
            0f);

        GameObject projectile = Object.Instantiate(
            areaConfig.ProjectilePrefab,
            spawnPosition,
            areaConfig.ProjectilePrefab.transform.rotation);

        AreaSkillProjectile2D areaProjectile = PrepareAreaProjectile(projectile);

        Vector2 velocity = new Vector2(
            Mathf.Abs(areaConfig.ArcLaunchVelocity.x) * facing,
            areaConfig.ArcLaunchVelocity.y);
        float gravityScale = Mathf.Max(0f, areaConfig.ArcGravityScale);

        areaProjectile.Configure(
            context.Owner,
            skill,
            areaConfig,
            context.TargetLayer,
            velocity,
            gravityScale);

        return true;
    }

    private static bool LaunchApexSpreadShot(SkillData skill, AreaSkillConfig areaConfig, SkillCastContext context)
    {
        if (areaConfig.ProjectilePrefab == null || context.Owner == null)
        {
            return false;
        }

        ApplyJumpImpulse(context, areaConfig.JumpImpulse);

        AreaSkillApexSpreadShotRunner runner = context.Owner.GetComponent<AreaSkillApexSpreadShotRunner>();
        if (runner == null)
        {
            runner = context.Owner.gameObject.AddComponent<AreaSkillApexSpreadShotRunner>();
        }

        runner.Begin(context.Owner, context.Body, context.CastPoint, skill, areaConfig, context.TargetLayer);
        return true;
    }

    public static AreaSkillProjectile2D PrepareAreaProjectile(GameObject projectile)
    {
        PlayerMagicProjectile2D magicProjectile = projectile.GetComponent<PlayerMagicProjectile2D>();
        if (magicProjectile != null)
        {
            magicProjectile.enabled = false;
        }

        AreaSkillProjectile2D areaProjectile = projectile.GetComponent<AreaSkillProjectile2D>();
        if (areaProjectile == null)
        {
            areaProjectile = projectile.AddComponent<AreaSkillProjectile2D>();
        }

        return areaProjectile;
    }

    private static void ApplyJumpImpulse(SkillCastContext context, float jumpImpulse)
    {
        if (context.Body == null || jumpImpulse <= 0f)
        {
            return;
        }

        Rigidbody2D body = context.Body.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.AddForce(Vector2.up * jumpImpulse, ForceMode2D.Impulse);
        }
    }
}
