public sealed class FireVfxFallExplosionSkillExecutor : ISkillExecutor
{
    public SkillAttackType AttackType => SkillAttackType.FireVfxFallExplosion;

    public bool CanExecute(SkillData skill, SkillExecutionConfig config)
    {
        return skill != null
            && skill.ElementType == SkillElementType.Fire
            && config is FireVfxFallExplosionSkillConfig;
    }

    public bool Execute(SkillData skill, SkillExecutionConfig config, SkillCastContext context)
    {
        if (context == null || context.Owner == null || !(config is FireVfxFallExplosionSkillConfig explosionConfig))
        {
            return false;
        }

        FireVfxFallExplosionSkillRunner runner = context.Owner.GetComponent<FireVfxFallExplosionSkillRunner>();
        if (runner == null)
        {
            runner = context.Owner.gameObject.AddComponent<FireVfxFallExplosionSkillRunner>();
        }

        return runner.TryStartSkill(skill, explosionConfig, context);
    }
}
