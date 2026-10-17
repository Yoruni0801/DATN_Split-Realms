using UnityEngine;

public sealed class EarthPillarBlastSkillExecutor : ISkillExecutor
{
    public SkillAttackType AttackType => SkillAttackType.EarthPillarBlast;

    public bool CanExecute(SkillData skill, SkillExecutionConfig config)
    {
        return skill != null
            && skill.ElementType == SkillElementType.Earth
            && config is EarthPillarBlastSkillConfig;
    }

    public bool Execute(SkillData skill, SkillExecutionConfig config, SkillCastContext context)
    {
        if (context == null || context.Owner == null || !(config is EarthPillarBlastSkillConfig pillarConfig))
        {
            return false;
        }

        Collider2D target = EarthSkillTargeting.ResolveClosestEnemy(
            context.Owner,
            context.Owner.transform.position,
            pillarConfig.TargetSearchRadius,
            context.TargetLayer);
        if (target == null)
        {
            return false;
        }

        EarthPillarBlastSkillRunner runner = context.Owner.GetComponent<EarthPillarBlastSkillRunner>();
        if (runner == null)
        {
            runner = context.Owner.gameObject.AddComponent<EarthPillarBlastSkillRunner>();
        }

        runner.Begin(context.Owner, skill, pillarConfig, ResolvePillarPosition(target, pillarConfig), context.TargetLayer);
        return true;
    }

    private static Vector3 ResolvePillarPosition(Collider2D target, EarthPillarBlastSkillConfig config)
    {
        Bounds bounds = target.bounds;
        Vector2 offset = config.PillarOffset;
        return new Vector3(bounds.center.x + offset.x, bounds.min.y + offset.y, target.transform.position.z);
    }
}
