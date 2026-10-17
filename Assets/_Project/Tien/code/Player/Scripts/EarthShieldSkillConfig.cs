using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "Earth Shield Config", menuName = "Duy/Game Data/Skill/Configs/Earth Shield")]
public class EarthShieldSkillConfig : SkillExecutionConfig
{
#if UNITY_EDITOR
    private const string DefaultShieldPrefabPath = "Assets/_Project/Tien/MagicArsenal/Effects/Prefabs/Shields/Shield Sphere/EarthShield.prefab";
    private const string DefaultBreakVfxPrefabPath = "Assets/_Project/Tien/MagicArsenal/Demo/Effect Prefabs/Missiles/Small/EarthSmallObj.prefab";
#endif

    [SerializeField] private GameObject shieldPrefab;
    [SerializeField] private GameObject breakVfxPrefab;
    [SerializeField, Min(0f)] private float duration = 4f;
    [SerializeField, Min(0f)] private float animationDuration = 0.6f;
    [SerializeField, Range(0, 100)] private int damageReductionPercent = 40;
    [SerializeField] private Vector3 shieldLocalPosition;
    [SerializeField] private Vector3 shieldLocalEuler;
    [SerializeField] private Vector3 shieldLocalScale = Vector3.one;
    [SerializeField, Min(0f)] private float breakVfxLifetime = 1.5f;

    public override SkillAttackType AttackType => SkillAttackType.EarthShield;
    public GameObject ShieldPrefab => shieldPrefab;
    public GameObject BreakVfxPrefab => breakVfxPrefab;
    public float Duration => duration;
    public float AnimationDuration => animationDuration;
    public int DamageReductionPercent => damageReductionPercent;
    public Vector3 ShieldLocalPosition => shieldLocalPosition;
    public Vector3 ShieldLocalEuler => shieldLocalEuler;
    public Vector3 ShieldLocalScale => shieldLocalScale;
    public float BreakVfxLifetime => breakVfxLifetime;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (shieldPrefab == null)
        {
            shieldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultShieldPrefabPath);
        }

        if (breakVfxPrefab == null)
        {
            breakVfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultBreakVfxPrefabPath);
        }
    }
#endif
}
