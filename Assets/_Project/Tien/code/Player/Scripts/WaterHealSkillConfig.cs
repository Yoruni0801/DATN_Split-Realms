using UnityEngine;

[CreateAssetMenu(fileName = "Water Heal Config", menuName = "Duy/Game Data/Skill/Configs/Water Heal")]
public class WaterHealSkillConfig : SkillExecutionConfig
{
    [SerializeField, Min(1)] private int healAmount = 20;
    [SerializeField, Min(0f)] private float animationDuration = 0.6f;
    [SerializeField] private GameObject healVfxPrefab;
    [SerializeField, Min(0f)] private float healVfxLifetime = 1.5f;

    public override SkillAttackType AttackType => SkillAttackType.WaterHeal;
    public int HealAmount => healAmount;
    public float AnimationDuration => animationDuration;
    public GameObject HealVfxPrefab => healVfxPrefab;
    public float HealVfxLifetime => healVfxLifetime;
}
