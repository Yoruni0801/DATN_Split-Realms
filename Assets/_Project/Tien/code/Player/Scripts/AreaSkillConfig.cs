using UnityEngine;

public enum AreaSkillDeliveryMode
{
    InstantArea = 0,
    JumpApexSpreadShot = 1,
    ArcProjectile = 2
}

[CreateAssetMenu(fileName = "Area Skill Config", menuName = "Duy/Game Data/Skill/Configs/Area")]
public class AreaSkillConfig : SkillExecutionConfig
{
    [SerializeField] private AreaSkillDeliveryMode deliveryMode = AreaSkillDeliveryMode.InstantArea;
    [SerializeField] private GameObject areaPrefab;
    [SerializeField] private GameObject projectilePrefab;

    [Header("Area")]    
    [SerializeField] private float radius = 2f;
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private float tickInterval = 0.2f;
    [SerializeField] private float projectileLifetime = 2f;
    [SerializeField] private float jumpImpulse = 8f;
    [SerializeField, Min(1)] private int projectileCount = 5;
    [SerializeField, Min(0f)] private float projectileSpacing = 0.75f;
    [SerializeField, Min(0f)] private float spreadAngle = 35f;
    [SerializeField, Min(0f)] private float centerShotAngleDown = 35f;
    [SerializeField, Min(0f)] private float apexVelocityThreshold = 0.25f;
    [SerializeField] private float arcGravityScale = 1.5f;
    [SerializeField] private float spreadShotSpeed = 14f;
    [SerializeField, Min(0.01f)] private float maxApexWait = 0.6f;

    [SerializeField] private Vector2 offset;
    [SerializeField] private Vector2 projectileSpawnOffset = new Vector2(0.6f, 0.5f);
    [SerializeField] private Vector2 arcLaunchVelocity = new Vector2(8f, 7f);

    private bool explodeOnLifetimeEnd = true;

    public override SkillAttackType AttackType => SkillAttackType.Area;
    public GameObject AreaPrefab => areaPrefab;
    public float Radius => radius;
    public Vector2 Offset => offset;
    public float Duration => duration;
    public float TickInterval => tickInterval;
    public AreaSkillDeliveryMode DeliveryMode => deliveryMode;
    public GameObject ProjectilePrefab => projectilePrefab;
    public Vector2 ProjectileSpawnOffset => projectileSpawnOffset;
    public float ProjectileLifetime => projectileLifetime;
    public bool ExplodeOnLifetimeEnd => explodeOnLifetimeEnd;
    public float JumpImpulse => jumpImpulse;
    public int ProjectileCount => projectileCount;
    public float ProjectileSpacing => projectileSpacing;
    public float SpreadAngle => spreadAngle;
    public float CenterShotAngleDown => centerShotAngleDown;
    public float ApexVelocityThreshold => apexVelocityThreshold;
    public float MaxApexWait => maxApexWait;
    public float SpreadShotSpeed => spreadShotSpeed;
    public Vector2 ArcLaunchVelocity => arcLaunchVelocity;
    public float ArcGravityScale => arcGravityScale;
}
