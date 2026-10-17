using UnityEngine;

public enum ProjectileSpawnOffsetSpace
{
    FacingDirection = 0,
    PlayerLocal = 1,
    World = 2
}

[CreateAssetMenu(fileName = "Projectile Skill Config", menuName = "Duy/Game Data/Skill/Configs/Projectile")]
public class ProjectileSkillConfig : SkillExecutionConfig
{
    public const float TinyChargeTime = 0f;
    public const float SmallChargeTime = 0.3f;
    public const float NormalChargeTime = 0.8f;
    public const float MegaChargeTime = 1.5f;
    public const float MaxChargeTime = 2f;
    public const float RequiredHoldToFire = 0.33f;

    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private GameObject tinyProjectilePrefab;
    [SerializeField] private GameObject smallProjectilePrefab;
    [SerializeField] private GameObject normalProjectilePrefab;
    [SerializeField] private GameObject megaProjectilePrefab;
    [SerializeField] private float speed = 12f;
    [SerializeField] private float lifetime = 2f;
    [SerializeField] private Vector2 spawnOffset = new Vector2(0.6f, 0f);
    [SerializeField] private ProjectileSpawnOffsetSpace spawnOffsetSpace = ProjectileSpawnOffsetSpace.FacingDirection;

    [Header("Charge Position")]
    [SerializeField] private float minimumHoldToShowAndFire = RequiredHoldToFire;
    [SerializeField] private float maxChargeAdditionalOffsetX = 0.5f;

    [Header("Auto Aim")]
    [SerializeField] private bool autoAim = true;
    [SerializeField] private float autoAimRadius = 8f;
    [SerializeField] private bool autoAimOnlyFacingDirection = true;

    [Header("Charge Damage")]
    [SerializeField] private bool overrideChargeDamage;
    [SerializeField] private int tinyChargeDamage = 10;
    [SerializeField] private int smallChargeDamage = 25;
    [SerializeField] private int normalChargeDamage = 40;
    [SerializeField] private int megaChargeDamage = 50;

    [Header("Impact")]
    [SerializeField] private AudioClip enemyImpactSound;
    [SerializeField] private AudioClip staticImpactSound;
    [SerializeField] private float bounceBackSpeed = 4f;
    [SerializeField] private float bounceBackLifetime = 0.35f;

    public override SkillAttackType AttackType => SkillAttackType.Projectile;
    public GameObject ProjectilePrefab => projectilePrefab;
    public GameObject TinyProjectilePrefab => tinyProjectilePrefab;
    public GameObject SmallProjectilePrefab => smallProjectilePrefab;
    public GameObject NormalProjectilePrefab => normalProjectilePrefab;
    public GameObject MegaProjectilePrefab => megaProjectilePrefab;
    public float Speed => speed;
    public float Lifetime => lifetime;
    public Vector2 SpawnOffset => spawnOffset;
    public ProjectileSpawnOffsetSpace SpawnOffsetSpace => spawnOffsetSpace;
    public float MinimumHoldToShowAndFire => Mathf.Max(0f, minimumHoldToShowAndFire);
    public float MaxChargeAdditionalOffsetX => Mathf.Max(0f, maxChargeAdditionalOffsetX);
    public bool AutoAim => autoAim;
    public float AutoAimRadius => Mathf.Max(0f, autoAimRadius);
    public bool AutoAimOnlyFacingDirection => autoAimOnlyFacingDirection;
    public bool OverrideChargeDamage => overrideChargeDamage;
    public int TinyChargeDamage => tinyChargeDamage;
    public int SmallChargeDamage => smallChargeDamage;
    public int NormalChargeDamage => normalChargeDamage;
    public int MegaChargeDamage => megaChargeDamage;
    public AudioClip EnemyImpactSound => enemyImpactSound;
    public AudioClip StaticImpactSound => staticImpactSound;
    public float BounceBackSpeed => bounceBackSpeed;
    public float BounceBackLifetime => bounceBackLifetime;

    public GameObject ResolveProjectilePrefab(float holdDuration)
    {
        switch (ResolveChargeLevel(holdDuration))
        {
            case 3:
                return megaProjectilePrefab != null
                    ? megaProjectilePrefab
                    : normalProjectilePrefab != null
                        ? normalProjectilePrefab
                        : smallProjectilePrefab != null
                            ? smallProjectilePrefab
                            : tinyProjectilePrefab != null ? tinyProjectilePrefab : projectilePrefab;

            case 2:
                return normalProjectilePrefab != null
                    ? normalProjectilePrefab
                    : smallProjectilePrefab != null
                        ? smallProjectilePrefab
                        : tinyProjectilePrefab != null ? tinyProjectilePrefab : projectilePrefab;

            case 1:
                return smallProjectilePrefab != null
                    ? smallProjectilePrefab
                    : tinyProjectilePrefab != null ? tinyProjectilePrefab : projectilePrefab;

            default:
                return tinyProjectilePrefab != null ? tinyProjectilePrefab : projectilePrefab;
        }
    }

    public int ResolveDamage(float holdDuration, float fallbackDamage)
    {
        if (!overrideChargeDamage)
        {
            return Mathf.RoundToInt(fallbackDamage);
        }

        switch (ResolveChargeLevel(holdDuration))
        {
            case 3:
                return Mathf.Max(0, megaChargeDamage);

            case 2:
                return Mathf.Max(0, normalChargeDamage);

            case 1:
                return Mathf.Max(0, smallChargeDamage);

            default:
                return Mathf.Max(0, tinyChargeDamage);
        }
    }

    public int ResolveChargeLevel(float holdDuration)
    {
        float clampedHoldDuration = Mathf.Clamp(holdDuration, 0f, MaxChargeTime);
        if (clampedHoldDuration >= MegaChargeTime)
        {
            return 3;
        }

        if (clampedHoldDuration >= NormalChargeTime)
        {
            return 2;
        }

        if (clampedHoldDuration >= SmallChargeTime)
        {
            return 1;
        }

        return 0;
    }

    public bool CanShowOrFire(float holdDuration)
    {
        return holdDuration >= MinimumHoldToShowAndFire;
    }

    public Vector2 ResolveChargedSpawnOffset(float holdDuration)
    {
        Vector2 offset = spawnOffset;
        offset.x += MaxChargeAdditionalOffsetX * ResolveChargeOffsetProgress(holdDuration);
        return offset;
    }

    private float ResolveChargeOffsetProgress(float holdDuration)
    {
        float minimumHold = MinimumHoldToShowAndFire;
        if (MaxChargeTime <= minimumHold)
        {
            return holdDuration >= minimumHold ? 1f : 0f;
        }

        return Mathf.InverseLerp(minimumHold, MaxChargeTime, Mathf.Clamp(holdDuration, 0f, MaxChargeTime));
    }
}
