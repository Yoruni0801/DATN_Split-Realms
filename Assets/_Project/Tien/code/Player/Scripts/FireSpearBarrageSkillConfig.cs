using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "Fire Spear Barrage Config", menuName = "Duy/Game Data/Skill/Configs/Fire Spear Barrage")]
public class FireSpearBarrageSkillConfig : SkillExecutionConfig
{
#if UNITY_EDITOR
    private const string DefaultAttackVfxPath = "Assets/_Project/Tien/MagicArsenal/Effects/Prefabs/Beam Blast/FireBeamBlast.prefab";
#endif

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float duration = 0.9f;
    [SerializeField, Min(0.01f)] private float hitInterval = 0.15f;
    [SerializeField, Min(0f)] private float startup = 0.05f;

    [Header("Combat")]
    [SerializeField] private Vector2 hitboxSize = new Vector2(3.2f, 0.65f);
    [SerializeField] private Vector2 hitboxOffset = new Vector2(1.6f, 0f);
    [SerializeField, Min(0f)] private float damageMultiplier = 0.35f;
    [SerializeField, Min(1)] private int maximumTargetsPerTick = 8;
    [SerializeField] private bool preventRepeatedTargetPerCast;

    [Header("VFX")]
    [SerializeField] private GameObject attackVfxPrefab;
    [SerializeField, Min(1)] private int beamCount = 5;
    [SerializeField, Min(0.01f)] private float beamInterval = 0.12f;
    [SerializeField, Min(0f)] private float beamVfxLifetime = 0.45f;
    [SerializeField] private bool parentBeamToSpear;
    [SerializeField] private bool mirrorBeamVfxWithPlayerFacing = true;
    [SerializeField] private Vector3 attackVfxLocalPosition;
    [SerializeField] private Vector3 attackVfxLocalEuler;
    [SerializeField] private Vector3 attackVfxLocalScale = Vector3.one;
    [SerializeField, Min(0f)] private float vfxStopDelay = 0.15f;

    public override SkillAttackType AttackType => SkillAttackType.FireSpearBarrage;
    public float Duration => duration;
    public float HitInterval => hitInterval;
    public float Startup => startup;
    public Vector2 HitboxOffset => hitboxOffset;
    public Vector2 HitboxSize => hitboxSize;
    public float HitboxLength => hitboxSize.x;
    public float DamageMultiplier => damageMultiplier;
    public int MaximumTargetsPerTick => maximumTargetsPerTick;
    public bool PreventRepeatedTargetPerCast => preventRepeatedTargetPerCast;
    public GameObject AttackVfxPrefab => attackVfxPrefab;
    public int BeamCount => beamCount;
    public float BeamInterval => beamInterval;
    public float BeamVfxLifetime => beamVfxLifetime;
    public bool ParentBeamToSpear => parentBeamToSpear;
    public bool MirrorBeamVfxWithPlayerFacing => mirrorBeamVfxWithPlayerFacing;
    public Vector3 AttackVfxLocalPosition => attackVfxLocalPosition;
    public Vector3 AttackVfxLocalEuler => attackVfxLocalEuler;
    public Vector3 AttackVfxLocalScale => attackVfxLocalScale;
    public float VfxStopDelay => vfxStopDelay;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (attackVfxPrefab == null)
        {
            attackVfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultAttackVfxPath);
        }
    }
#endif
}
