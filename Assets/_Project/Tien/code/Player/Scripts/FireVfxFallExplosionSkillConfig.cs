using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "Fire VFX Fall Explosion Config", menuName = "Duy/Game Data/Skill/Configs/Fire VFX Fall Explosion")]
public class FireVfxFallExplosionSkillConfig : SkillExecutionConfig
{
#if UNITY_EDITOR
    private const string DefaultActiveVfxPath = "Assets/_Project/Tien/MagicArsenal/Effects/Prefabs/Orbital/FireOrbitSphere.prefab";
    private const string DefaultExplosionVfxPath = "Assets/_Project/Tien/MagicArsenal/Effects/Prefabs/Pillar Blast/FirePillarBlast.prefab";
#endif

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float activeDuration = 1.2f;
    [SerializeField, Min(0f)] private float ascendDuration = 0.35f;
    [SerializeField, Min(0f)] private float hoverDuration = 0.15f;
    [SerializeField, Min(0f)] private float dropDuration = 0.35f;
    [SerializeField, Min(0f)] private float explosionDelay;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float ascendHeight = 4f;
    [SerializeField, Min(0f)] private float movementSpeedMultiplier = 1f;
    [SerializeField] private bool disableGravityWhileActive = true;
    [SerializeField] private float activeGravityScale;
    [SerializeField, Min(0.01f)] private float groundSearchDistance = 12f;
    [SerializeField] private float groundOffset = 0.5f;

    [Header("Damage")]
    [SerializeField, Min(0f)] private float explosionRadius = 2.25f;
    [SerializeField, Min(0f)] private float explosionColliderLifetime = 0.08f;
    [SerializeField, Min(0f)] private float damageMultiplier = 1f;

    [Header("VFX")]
    [SerializeField] private GameObject activeVfxPrefab;
    [SerializeField] private Vector3 activeVfxLocalPosition;
    [SerializeField] private Vector3 activeVfxLocalEuler;
    [SerializeField] private Vector3 activeVfxLocalScale = Vector3.one;
    [SerializeField] private GameObject explosionVfxPrefab;
    [SerializeField] private Vector3 explosionVfxLocalEuler;
    [SerializeField] private Vector3 explosionVfxLocalScale = Vector3.one;
    [SerializeField, Min(0f)] private float explosionVfxLifetime = 1.5f;
    [SerializeField, Min(0f)] private float activeVfxStopDelay = 0.1f;

    [Header("Player Visual")]
    [SerializeField] private bool hidePlayerRenderers = true;

    public override SkillAttackType AttackType => SkillAttackType.FireVfxFallExplosion;
    public float ActiveDuration => activeDuration;
    public float AscendDuration => ascendDuration;
    public float HoverDuration => hoverDuration;
    public float DropDuration => dropDuration;
    public float ExplosionDelay => explosionDelay;
    public float AscendHeight => ascendHeight;
    public float MovementSpeedMultiplier => movementSpeedMultiplier;
    public bool DisableGravityWhileActive => disableGravityWhileActive;
    public float ActiveGravityScale => activeGravityScale;
    public float GroundSearchDistance => groundSearchDistance;
    public float GroundOffset => groundOffset;
    public float ExplosionRadius => explosionRadius;
    public float ExplosionColliderLifetime => explosionColliderLifetime;
    public float DamageMultiplier => damageMultiplier;
    public GameObject ActiveVfxPrefab => activeVfxPrefab;
    public Vector3 ActiveVfxLocalPosition => activeVfxLocalPosition;
    public Vector3 ActiveVfxLocalEuler => activeVfxLocalEuler;
    public Vector3 ActiveVfxLocalScale => activeVfxLocalScale;
    public GameObject ExplosionVfxPrefab => explosionVfxPrefab;
    public Vector3 ExplosionVfxLocalEuler => explosionVfxLocalEuler;
    public Vector3 ExplosionVfxLocalScale => explosionVfxLocalScale;
    public float ExplosionVfxLifetime => explosionVfxLifetime;
    public float ActiveVfxStopDelay => activeVfxStopDelay;
    public bool HidePlayerRenderers => hidePlayerRenderers;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (activeVfxPrefab == null)
        {
            activeVfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultActiveVfxPath);
        }

        if (explosionVfxPrefab == null)
        {
            explosionVfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultExplosionVfxPath);
        }
    }
#endif
}
