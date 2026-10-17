using UnityEngine;

[CreateAssetMenu(fileName = "Wind Staff Beam Config", menuName = "Duy/Game Data/Skill/Configs/Wind Staff Beam")]
public class WindStaffBeamSkillConfig : SkillExecutionConfig
{
    [SerializeField] private GameObject beamPrefab;
    [SerializeField] private string animationName = "Blow Petals";
    [SerializeField] private Vector2 fairyOriginOffset;
    [SerializeField] private Vector3 beamLocalEuler;
    [SerializeField, Min(0f)] private float castDelay = 0.15f;
    [SerializeField, Min(0.05f)] private float animationDurationFallback = 1f;
    [SerializeField, Min(0.05f)] private float duration = 0.35f;
    [SerializeField, Min(0.1f)] private float maxDistance = 12f;
    [SerializeField, Min(0.1f)] private float targetSearchRadius = 12f;
    [SerializeField, Min(1)] private int damagePerTick = 2;
    [SerializeField, Min(0.01f)] private float damageTickInterval = 0.25f;

    public override SkillAttackType AttackType => SkillAttackType.WindStaffBeam;
    public GameObject BeamPrefab => beamPrefab;
    public string AnimationName => animationName;
    public Vector2 FairyOriginOffset => fairyOriginOffset;
    public Vector3 BeamLocalEuler => beamLocalEuler;
    public float CastDelay => castDelay;
    public float AnimationDurationFallback => animationDurationFallback;
    public float Duration => duration;
    public float MaxDistance => maxDistance;
    public float TargetSearchRadius => targetSearchRadius;
    public int DamagePerTick => damagePerTick;
    public float DamageTickInterval => damageTickInterval;
}
