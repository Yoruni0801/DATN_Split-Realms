using UnityEngine;

public sealed class SummonSkillExecutor : ISkillExecutor
{
    public SkillAttackType AttackType => SkillAttackType.Summon;

    public bool CanExecute(SkillData skill, SkillExecutionConfig config)
    {
        return skill != null && config is SummonSkillConfig;
    }

    public bool Execute(SkillData skill, SkillExecutionConfig config, SkillCastContext context)
    {
        SummonSkillConfig summonConfig = config as SummonSkillConfig;
        if (summonConfig == null || context == null || summonConfig.SummonPrefab == null)
        {
            return false;
        }

        Vector2 direction = context.FacingDirection < 0f ? Vector2.left : Vector2.right;
        Vector3 scaledOffset = context.ScaleOffset(summonConfig.SpawnOffset);
        Vector3 position = context.Origin + new Vector3(scaledOffset.x * direction.x, scaledOffset.y, 0f);
        GameObject summon = Object.Instantiate(summonConfig.SummonPrefab, position, Quaternion.identity);
        if (summonConfig.Lifetime > 0f)
        {
            Object.Destroy(summon, summonConfig.Lifetime);
        }

        return true;
    }
}
