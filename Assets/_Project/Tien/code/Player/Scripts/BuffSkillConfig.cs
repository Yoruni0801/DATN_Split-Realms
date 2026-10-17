using UnityEngine;

[CreateAssetMenu(fileName = "Buff Skill Config", menuName = "Duy/Game Data/Skill/Configs/Buff")]
public class BuffSkillConfig : SkillExecutionConfig
{
    [SerializeField] private GameObject buffVfxPrefab;
    [SerializeField] private float duration = 3f;
    [SerializeField] private bool invincible;
    [SerializeField] private float moveSpeedMultiplier = 1f;

    public override SkillAttackType AttackType => SkillAttackType.Buff;
    public GameObject BuffVfxPrefab => buffVfxPrefab;
    public float Duration => duration;
    public bool Invincible => invincible;
    public float MoveSpeedMultiplier => moveSpeedMultiplier;
}
