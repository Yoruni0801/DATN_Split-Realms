using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "Fire Spear Dash Skill Config", menuName = "Duy/Game Data/Skill/Configs/Fire Spear Dash")]
public class FireSpearDashSkillConfig : SkillExecutionConfig
{
#if UNITY_EDITOR
    private const string DefaultAttackVfxPath = "Assets/_Project/Tien/MagicArsenal/Effects/Prefabs/Flames/FireFlame.prefab";
    private const string DefaultImpactVfxPath = "Assets/_Project/Tien/MagicArsenal/Effects/Prefabs/Missiles & Explosions/Fire/FireExplosionTiny.prefab";
#endif

    [Header("Dash")]
    [SerializeField, Min(0f)] private float distance = 5f;
    [SerializeField, Min(0.01f)] private float duration = 0.28f;
    [SerializeField] private AnimationCurve speedCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool autoTarget = true;
    [SerializeField, Min(0f)] private float targetRange = 6f;
    [SerializeField, Min(0f)] private float targetStopDistance = 0.65f;
    [SerializeField, Min(0f)] private float wallStopDistance = 0.08f;

    [Header("Adaptive Speed")]
    [SerializeField] private bool adaptiveSpeedEnabled = true;
    [SerializeField, Min(0f)] private float minAdaptiveDistance = 0.5f;
    [SerializeField, Min(0.01f)] private float maxAdaptiveDistance = 5f;
    [SerializeField, Range(0.5f, 2f)] private float nearAnimationSpeed = 0.85f;
    [SerializeField, Range(0.5f, 2f)] private float farAnimationSpeed = 1.2f;
    [SerializeField, Min(0.01f)] private float nearDashDuration = 0.28f;
    [SerializeField, Min(0.01f)] private float farDashDuration = 0.14f;
    [SerializeField, Min(0.1f)] private float animationCompletionTimeoutMultiplier = 1.25f;

    [Header("Combat")]
    [SerializeField, Min(0f)] private float damageMultiplier = 1f;
    [SerializeField] private Vector2 hitboxSize = new Vector2(0.9f, 0.45f);
    [SerializeField, Min(0f)] private float knockbackForce = 7f;
    [SerializeField, Min(0f)] private float knockUpForce;
    [SerializeField] private bool piercing;
    [SerializeField, Min(1)] private int maximumTargets = 1;
    [SerializeField] private bool burnEnabled;
    [SerializeField, Range(0f, 1f)] private float burnChance = 0.35f;
    [SerializeField, Min(0f)] private float burnDamage = 4f;
    [SerializeField, Min(0f)] private float burnDuration = 3f;

    [Header("VFX")]
    [SerializeField] private GameObject attackVfxPrefab;
    [SerializeField] private Vector3 attackVfxLocalPosition;
    [SerializeField] private Vector3 attackVfxLocalEuler;
    [SerializeField] private Vector3 attackVfxLocalScale = Vector3.one;
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private Vector3 impactVfxLocalEuler;
    [SerializeField] private Vector3 impactVfxLocalScale = Vector3.one;
    [SerializeField, Min(0f)] private float impactLifetime = 1.25f;
    [SerializeField, Min(0f)] private float vfxStopDelay = 0.15f;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float startup = 0.08f;
    [SerializeField, Min(0f)] private float active = 0.22f;
    [SerializeField, Min(0f)] private float recovery = 0.12f;

    public override SkillAttackType AttackType => SkillAttackType.FireSpearDash;
    public float Distance => distance;
    public float Duration => duration;
    public AnimationCurve SpeedCurve => speedCurve;
    public bool AutoTarget => autoTarget;
    public float TargetRange => targetRange;
    public float TargetStopDistance => targetStopDistance;
    public float WallStopDistance => wallStopDistance;
    public bool AdaptiveSpeedEnabled => adaptiveSpeedEnabled;
    public float MinAdaptiveDistance => minAdaptiveDistance;
    public float MaxAdaptiveDistance => maxAdaptiveDistance;
    public float NearAnimationSpeed => nearAnimationSpeed;
    public float FarAnimationSpeed => farAnimationSpeed;
    public float NearDashDuration => nearDashDuration;
    public float FarDashDuration => farDashDuration;
    public float AnimationCompletionTimeoutMultiplier => animationCompletionTimeoutMultiplier;
    public float DamageMultiplier => damageMultiplier;
    public Vector2 HitboxSize => hitboxSize;
    public float KnockbackForce => knockbackForce;
    public float KnockUpForce => knockUpForce;
    public bool Piercing => piercing;
    public int MaximumTargets => maximumTargets;
    public bool BurnEnabled => burnEnabled;
    public float BurnChance => burnChance;
    public float BurnDamage => burnDamage;
    public float BurnDuration => burnDuration;
    public GameObject AttackVfxPrefab => attackVfxPrefab;
    public Vector3 AttackVfxLocalPosition => attackVfxLocalPosition;
    public Vector3 AttackVfxLocalEuler => attackVfxLocalEuler;
    public Vector3 AttackVfxLocalScale => attackVfxLocalScale;
    public GameObject ImpactVfxPrefab => impactVfxPrefab;
    public Vector3 ImpactVfxLocalEuler => impactVfxLocalEuler;
    public Vector3 ImpactVfxLocalScale => impactVfxLocalScale;
    public float ImpactLifetime => impactLifetime;
    public float VfxStopDelay => vfxStopDelay;
    public float Startup => startup;
    public float Active => active;
    public float Recovery => recovery;

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxAdaptiveDistance = Mathf.Max(minAdaptiveDistance + 0.01f, maxAdaptiveDistance);

        if (attackVfxPrefab == null)
        {
            attackVfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultAttackVfxPath);
        }

        if (impactVfxPrefab == null)
        {
            impactVfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultImpactVfxPath);
        }
    }
#endif
}
