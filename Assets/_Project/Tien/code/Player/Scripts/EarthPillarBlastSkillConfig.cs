using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "Earth Pillar Blast Config", menuName = "Duy/Game Data/Skill/Configs/Earth Pillar Blast")]
public class EarthPillarBlastSkillConfig : SkillExecutionConfig
{
#if UNITY_EDITOR
    private const string DefaultPillarPrefabPath = "Assets/_Project/Tien/MagicArsenal/Effects/Prefabs/Pillar Blast/EarthPillarBlast.prefab";
#endif

    [SerializeField] private GameObject pillarPrefab;
    [SerializeField, Min(0.1f)] private float targetSearchRadius = 12f;
    [SerializeField, Min(0f)] private float castDelay = 0.1f;
    [SerializeField, Min(0f)] private float damageDelay = 0.15f;
    [SerializeField, Min(0f)] private float damageRadius = 2f;
    [SerializeField, Min(1)] private int damage = 35;
    [SerializeField] private Vector2 pillarOffset;
    [SerializeField] private Vector3 pillarLocalEuler;
    [SerializeField] private Vector3 pillarLocalScale = Vector3.one;
    [SerializeField, Min(0f)] private float vfxLifetime = 1.5f;

    public override SkillAttackType AttackType => SkillAttackType.EarthPillarBlast;
    public GameObject PillarPrefab => pillarPrefab;
    public float TargetSearchRadius => targetSearchRadius;
    public float CastDelay => castDelay;
    public float DamageDelay => damageDelay;
    public float DamageRadius => damageRadius;
    public int Damage => damage;
    public Vector2 PillarOffset => pillarOffset;
    public Vector3 PillarLocalEuler => pillarLocalEuler;
    public Vector3 PillarLocalScale => pillarLocalScale;
    public float VfxLifetime => vfxLifetime;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (pillarPrefab == null)
        {
            pillarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPillarPrefabPath);
        }
    }
#endif
}
