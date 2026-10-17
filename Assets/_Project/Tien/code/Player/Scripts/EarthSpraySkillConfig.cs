using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "Earth Spray Config", menuName = "Duy/Game Data/Skill/Configs/Earth Spray")]
public class EarthSpraySkillConfig : SkillExecutionConfig
{
#if UNITY_EDITOR
    private const string DefaultSprayPrefabPath = "Assets/_Project/Tien/MagicArsenal/Effects/Prefabs/Sprays/EarthSpray.prefab";
#endif

    [SerializeField] private GameObject sprayPrefab;
    [SerializeField] private string animationName = "Blow Petals";
    [SerializeField] private Vector2 originOffset;
    [SerializeField] private Vector3 sprayLocalEuler;
    [SerializeField, Min(0f)] private float castDelay = 0.15f;
    [SerializeField, Min(0.05f)] private float animationDurationFallback = 1f;
    [SerializeField, Min(0.05f)] private float duration = 0.45f;
    [SerializeField, Min(0.1f)] private float maxDistance = 12f;
    [SerializeField, Min(0.1f)] private float sprayRadius = 1.25f;
    [SerializeField, Min(0.1f)] private float targetSearchRadius = 12f;
    [SerializeField, Min(1)] private int damagePerTick = 3;
    [SerializeField, Min(0.01f)] private float damageTickInterval = 0.2f;
    [SerializeField, Min(0f)] private float vfxLifetimePadding = 0.2f;

    public override SkillAttackType AttackType => SkillAttackType.EarthSpray;
    public GameObject SprayPrefab => sprayPrefab;
    public string AnimationName => animationName;
    public Vector2 OriginOffset => originOffset;
    public Vector3 SprayLocalEuler => sprayLocalEuler;
    public float CastDelay => castDelay;
    public float AnimationDurationFallback => animationDurationFallback;
    public float Duration => duration;
    public float MaxDistance => maxDistance;
    public float SprayRadius => sprayRadius;
    public float TargetSearchRadius => targetSearchRadius;
    public int DamagePerTick => damagePerTick;
    public float DamageTickInterval => damageTickInterval;
    public float VfxLifetimePadding => vfxLifetimePadding;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (sprayPrefab == null)
        {
            sprayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultSprayPrefabPath);
        }
    }
#endif
}
