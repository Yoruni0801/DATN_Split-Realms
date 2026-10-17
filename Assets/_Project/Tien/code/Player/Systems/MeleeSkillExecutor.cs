using UnityEngine;

public sealed class MeleeSkillExecutor : ISkillExecutor
{
    public SkillAttackType AttackType => SkillAttackType.Melee;

    public bool CanExecute(SkillData skill, SkillExecutionConfig config)
    {
        return skill != null && config is MeleeSkillConfig;
    }

    public bool Execute(SkillData skill, SkillExecutionConfig config, SkillCastContext context)
    {
        MeleeSkillConfig meleeConfig = config as MeleeSkillConfig;
        if (meleeConfig == null || context == null)
        {
            return false;
        }

        Vector2 direction = context.FacingDirection < 0f ? Vector2.left : Vector2.right;
        Vector3 scaledOffset = context.ScaleOffset(meleeConfig.Offset);
        Vector3 center = context.Origin + new Vector3(scaledOffset.x * direction.x, scaledOffset.y, 0f);
        float radius = context.ScaleDistance(meleeConfig.Radius);
        SkillHitData hitData = new SkillHitData(skill, skill.Damage, meleeConfig.KnockbackForce, meleeConfig.KnockUpForce);

        foreach (Collider2D collider in Physics2D.OverlapCircleAll(center, radius, context.TargetLayer))
        {
            if (PlayerHitResolver.IsThreatSensorCollider(collider))
            {
                continue;
            }

            IEnemyTarget target = collider.GetComponentInParent<IEnemyTarget>();
            if (target != null && target.IsAlive)
            {
                context.Owner.ApplySkillHit(hitData, target, center);
            }
        }

        return true;
    }
}
