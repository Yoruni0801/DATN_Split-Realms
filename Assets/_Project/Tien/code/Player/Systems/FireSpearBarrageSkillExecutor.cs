public sealed class FireSpearBarrageSkillExecutor : ISkillExecutor
{
    public SkillAttackType AttackType => SkillAttackType.FireSpearBarrage;

    public bool CanExecute(SkillData skill, SkillExecutionConfig config)
    {
        return skill != null
            && skill.ElementType == SkillElementType.Fire
            && config is FireSpearBarrageSkillConfig;
    }

    public bool Execute(SkillData skill, SkillExecutionConfig config, SkillCastContext context)
    {
        if (context == null || context.Owner == null || !(config is FireSpearBarrageSkillConfig barrageConfig))
        {
            return false;
        }

        FireSpearBarrageSkillRunner runner = context.Owner.GetComponent<FireSpearBarrageSkillRunner>();
        if (runner == null)
        {
            runner = context.Owner.gameObject.AddComponent<FireSpearBarrageSkillRunner>();
        }

        return runner.TryStartSkill(skill, barrageConfig, context);
    }
}
