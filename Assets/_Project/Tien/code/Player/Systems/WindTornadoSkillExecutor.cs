using UnityEngine;

public sealed class WindTornadoSkillExecutor : ISkillExecutor
{
    public SkillAttackType AttackType => SkillAttackType.WindTornado;

    public bool CanExecute(SkillData skill, SkillExecutionConfig config)
    {
        return skill != null && config is WindTornadoSkillConfig;
    }

    public bool Execute(SkillData skill, SkillExecutionConfig config, SkillCastContext context)
    {
        WindTornadoSkillConfig tornadoConfig = config as WindTornadoSkillConfig;
        if (tornadoConfig == null || context == null || context.Owner == null)
        {
            return false;
        }

        Collider2D target = ResolveTarget(context.Owner, context.Owner.transform.position, tornadoConfig.TargetSearchRadius, context.TargetLayer);
        if (target == null)
        {
            return false;
        }

        Vector3 start = ResolveSpawnPosition(context, tornadoConfig);
        Vector3 targetCenter = ResolveTornadoCenter(target, tornadoConfig);
        WindTornadoSkillRunner runner = context.Owner.GetComponent<WindTornadoSkillRunner>();
        if (runner == null)
        {
            runner = context.Owner.gameObject.AddComponent<WindTornadoSkillRunner>();
        }

        runner.Begin(context.Owner, skill, tornadoConfig, start, targetCenter, context.TargetLayer);
        return true;
    }

    private static Collider2D ResolveTarget(Player owner, Vector3 origin, float radius, LayerMask targetLayer)
    {
        if (owner == null || radius <= 0f)
        {
            return null;
        }

        Collider2D[] hits = targetLayer.value != 0
            ? Physics2D.OverlapCircleAll(origin, radius, targetLayer)
            : Physics2D.OverlapCircleAll(origin, radius);

        Collider2D closest = null;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (FairyBeamSkillExecutor.IsSelfCollider(hit, owner))
            {
                continue;
            }

            if (!FairyBeamSkillExecutor.IsValidEnemyTarget(hit))
            {
                continue;
            }

            float distance = ((Vector2)hit.bounds.center - (Vector2)origin).sqrMagnitude;
            if (distance >= closestDistance)
            {
                continue;
            }

            closestDistance = distance;
            closest = hit;
        }

        return closest;
    }

    private static Vector3 ResolveTornadoCenter(Collider2D target, WindTornadoSkillConfig config)
    {
        Bounds bounds = target.bounds;
        Vector2 offset = config.TornadoCenterOffset;
        return new Vector3(bounds.center.x + offset.x, bounds.min.y + offset.y, target.transform.position.z);
    }

    private static Vector3 ResolveSpawnPosition(SkillCastContext context, WindTornadoSkillConfig config)
    {
        Vector3 origin = context.Origin;
        float facing = context.FacingDirection < 0f ? -1f : 1f;
        return origin + new Vector3(config.SpawnForwardOffset * facing, config.SpawnHeightOffset, 0f);
    }
}
