using UnityEngine;

public sealed class WaterHealSkillExecutor : ISkillExecutor
{
    public SkillAttackType AttackType => SkillAttackType.WaterHeal;

    public bool CanExecute(SkillData skill, SkillExecutionConfig config)
    {
        return skill != null
            && skill.ElementType == SkillElementType.Water
            && config is WaterHealSkillConfig;
    }

    public bool Execute(SkillData skill, SkillExecutionConfig config, SkillCastContext context)
    {
        if (context == null || context.Owner == null || !(config is WaterHealSkillConfig healConfig))
            return false;

        WaterHealSkillRunner runner = context.Owner.GetComponent<WaterHealSkillRunner>();
        if (runner == null)
            runner = context.Owner.gameObject.AddComponent<WaterHealSkillRunner>();

        runner.Begin(context.Owner, skill, healConfig);
        return true;
    }
}
