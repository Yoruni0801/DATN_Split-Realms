public interface ISkillExecutor
{
    SkillAttackType AttackType { get; }
    bool CanExecute(SkillData skill, SkillExecutionConfig config);
    bool Execute(SkillData skill, SkillExecutionConfig config, SkillCastContext context);
}
