using UnityEngine;

public sealed class FireSpearDashSkillExecutor : ISkillExecutor
{
    public SkillAttackType AttackType => SkillAttackType.FireSpearDash;

    public bool CanExecute(SkillData skill, SkillExecutionConfig config)
    {
        return skill != null
            && skill.ElementType == SkillElementType.Fire
            && config is FireSpearDashSkillConfig;
    }

    public bool Execute(SkillData skill, SkillExecutionConfig config, SkillCastContext context)
    {
        if (context == null || context.Owner == null || !(config is FireSpearDashSkillConfig dashConfig))
        {
            return false;
        }

        FireSpearDashSkillRunner runner = context.Owner.GetComponent<FireSpearDashSkillRunner>();
        if (runner == null)
        {
            runner = context.Owner.gameObject.AddComponent<FireSpearDashSkillRunner>();
        }

        return runner.TryStartSkill(skill, dashConfig, context);
    }
}
