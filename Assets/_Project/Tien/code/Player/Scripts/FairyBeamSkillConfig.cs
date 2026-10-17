using UnityEngine;

[CreateAssetMenu(fileName = "Fairy Beam Skill Config", menuName = "Duy/Game Data/Skill/Configs/Fairy Beam")]
public class FairyBeamSkillConfig : SkillExecutionConfig
{
    [SerializeField] private GameObject beamPrefab;
    [SerializeField] private string animationName = "Blow Petals";
    [SerializeField] private float castDelay = 0.2f;
    [SerializeField] private float animationDurationFallback = 1f;
    [SerializeField] private float duration = 0.35f;
    [SerializeField] private float maxDistance = 12f;
    [SerializeField] private float targetSearchRadius = 12f;
    [SerializeField] private int damagePerTick = 2;
    [SerializeField] private float damageTickInterval = 0.25f;
    [SerializeField] private Vector2 fairyOriginOffset;

    public override SkillAttackType AttackType => SkillAttackType.FairyBeam;
    public GameObject BeamPrefab => beamPrefab;
    public string AnimationName => animationName;
    public float CastDelay => castDelay;
    public float AnimationDurationFallback => animationDurationFallback;
    public float Duration => duration;
    public float MaxDistance => maxDistance;
    public float TargetSearchRadius => targetSearchRadius;
    public int DamagePerTick => damagePerTick;
    public float DamageTickInterval => damageTickInterval;
    public Vector2 FairyOriginOffset => fairyOriginOffset;
}
