using UnityEngine;

public sealed class AreaSkillApexSpreadShotRunner : MonoBehaviour
{
    private Player owner;
    private Transform body;
    private Transform castPoint;
    private SkillData skill;
    private AreaSkillConfig areaConfig;
    private LayerMask targetLayer;
    private Rigidbody2D ownerBody;
    private float elapsed;
    private float pullTimer;
    private bool wasRising;
    private bool active;
    private bool pulling;

    public bool IsActive => active;

    public void Begin(
        Player owner,
        Transform body,
        Transform castPoint,
        SkillData skill,
        AreaSkillConfig areaConfig,
        LayerMask targetLayer)
    {
        this.owner = owner;
        this.body = body;
        this.castPoint = castPoint;
        this.skill = skill;
        this.areaConfig = areaConfig;
        this.targetLayer = targetLayer;
        ownerBody = body != null ? body.GetComponent<Rigidbody2D>() : null;
        elapsed = 0f;
        pullTimer = 0f;
        wasRising = false;
        active = true;
        pulling = false;
    }

    private void Update()
    {
        if (!active)
        {
            return;
        }

        elapsed += Time.deltaTime;
        if (pulling)
        {
            pullTimer -= Time.deltaTime;
            if (pullTimer > 0f)
            {
                return;
            }

            FireSpread();
            if (owner != null)
            {
                owner.HideSkillPresentation();
            }
            active = false;
            Destroy(this);
            return;
        }

        float verticalVelocity = ownerBody != null ? ownerBody.linearVelocity.y : 0f;
        wasRising |= verticalVelocity > areaConfig.ApexVelocityThreshold;

        bool reachedApex = wasRising && verticalVelocity <= areaConfig.ApexVelocityThreshold;
        bool timedOut = elapsed >= areaConfig.MaxApexWait;
        if (!reachedApex && !timedOut)
        {
            return;
        }

        BeginPull();
    }

    private void BeginPull()
    {
        if (owner == null || skill == null)
        {
            FireSpread();
            active = false;
            Destroy(this);
            return;
        }

        owner.ShowSkillPresentation(skill);
        pullTimer = Mathf.Max(0f, skill.PullPoseTime);
        pulling = true;

        if (pullTimer <= 0f)
        {
            FireSpread();
            if (owner != null)
            {
                owner.HideSkillPresentation();
            }
            active = false;
            Destroy(this);
        }
    }

    private void FireSpread()
    {
        if (owner == null || skill == null || areaConfig == null || areaConfig.ProjectilePrefab == null)
        {
            return;
        }

        float facing = owner.FacingDirectionSign < 0f ? -1f : 1f;
        Vector3 origin = castPoint != null ? castPoint.position : body != null ? body.position : owner.transform.position;
        origin += new Vector3(areaConfig.ProjectileSpawnOffset.x * facing, areaConfig.ProjectileSpawnOffset.y, 0f);

        int projectileCount = Mathf.Max(1, areaConfig.ProjectileCount);
        float totalSpread = Mathf.Max(0f, areaConfig.SpreadAngle);
        float startAngleOffset = projectileCount > 1 ? -0.5f * totalSpread : 0f;
        float angleStep = projectileCount > 1 ? totalSpread / (projectileCount - 1) : 0f;
        float centerAngle = facing > 0f
            ? -areaConfig.CenterShotAngleDown
            : 180f + areaConfig.CenterShotAngleDown;

        for (int i = 0; i < projectileCount; i++)
        {
            float angle = centerAngle + startAngleOffset + angleStep * i;
            Vector2 direction = DirectionFromAngle(angle);
            Vector3 spawnPosition = origin + (Vector3)(GetPerpendicular(direction) * GetVisualSpawnOffset(i, projectileCount));
            GameObject projectile = Instantiate(
                areaConfig.ProjectilePrefab,
                spawnPosition,
                areaConfig.ProjectilePrefab.transform.rotation);

            AreaSkillProjectile2D areaProjectile = AreaSkillExecutor.PrepareAreaProjectile(projectile);
            areaProjectile.Configure(
                owner,
                skill,
                areaConfig,
                targetLayer,
                direction * Mathf.Max(0.01f, areaConfig.SpreadShotSpeed),
                0f);
        }
    }

    private float GetVisualSpawnOffset(int index, int count)
    {
        if (count <= 1)
        {
            return 0f;
        }

        return (index - (count - 1) * 0.5f) * Mathf.Max(0f, areaConfig.ProjectileSpacing);
    }

    private static Vector2 DirectionFromAngle(float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
    }

    private static Vector2 GetPerpendicular(Vector2 direction)
    {
        return new Vector2(-direction.y, direction.x).normalized;
    }
}
