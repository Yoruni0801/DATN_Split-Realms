using UnityEngine;

public abstract class SkillExecutionConfig : ScriptableObject
{
    // Presentation such as cast animation belongs to SkillData, not execution configs.
    public abstract SkillAttackType AttackType { get; }
}
