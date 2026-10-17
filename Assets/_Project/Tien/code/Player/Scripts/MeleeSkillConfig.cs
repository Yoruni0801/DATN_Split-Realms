using UnityEngine;

[CreateAssetMenu(fileName = "Melee Skill Config", menuName = "Duy/Game Data/Skill/Configs/Melee")]
public class MeleeSkillConfig : SkillExecutionConfig
{
    [SerializeField] private float radius = 1f;
    [SerializeField] private Vector2 offset = new Vector2(0.6f, 0f);
    [SerializeField] private float knockbackForce;
    [SerializeField] private float knockUpForce;

    public override SkillAttackType AttackType => SkillAttackType.Melee;
    public float Radius => radius;
    public Vector2 Offset => offset;
    public float KnockbackForce => knockbackForce;
    public float KnockUpForce => knockUpForce;
}
