using UnityEngine;

public sealed class EarthShieldSkillExecutor : ISkillExecutor
{
    public SkillAttackType AttackType => SkillAttackType.EarthShield;

    public bool CanExecute(SkillData skill, SkillExecutionConfig config)
    {
        return skill != null
            && skill.ElementType == SkillElementType.Earth
            && config is EarthShieldSkillConfig;
    }

    public bool Execute(SkillData skill, SkillExecutionConfig config, SkillCastContext context)
    {
        if (context == null || context.Owner == null || !(config is EarthShieldSkillConfig shieldConfig))
        {
            return false;
        }

        EarthShieldSkillRunner runner = context.Owner.GetComponent<EarthShieldSkillRunner>();
        if (runner == null)
        {
            runner = context.Owner.gameObject.AddComponent<EarthShieldSkillRunner>();
        }

        runner.Begin(context.Owner, skill, shieldConfig);
        return true;
    }
}
