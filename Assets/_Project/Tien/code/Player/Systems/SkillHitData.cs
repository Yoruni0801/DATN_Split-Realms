using UnityEngine;

public struct SkillHitData
{
    public SkillHitData(SkillData skill, float damage, float knockbackForce = 0f, float knockUpForce = 0f)
    {
        Skill = skill;
        Damage = damage;
        KnockbackForce = knockbackForce;
        KnockUpForce = knockUpForce;
    }

    public SkillData Skill { get; }
    public float Damage { get; }
    public float KnockbackForce { get; }
    public float KnockUpForce { get; }
}
