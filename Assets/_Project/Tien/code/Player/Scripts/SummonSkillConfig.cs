using UnityEngine;

[CreateAssetMenu(fileName = "Summon Skill Config", menuName = "Duy/Game Data/Skill/Configs/Summon")]
public class SummonSkillConfig : SkillExecutionConfig
{
    [SerializeField] private GameObject summonPrefab;
    [SerializeField] private Vector2 spawnOffset = new Vector2(1f, 0f);
    [SerializeField] private float lifetime = 5f;

    public override SkillAttackType AttackType => SkillAttackType.Summon;
    public GameObject SummonPrefab => summonPrefab;
    public Vector2 SpawnOffset => spawnOffset;
    public float Lifetime => lifetime;
}
