using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class AreaSkillProjectile2D : MonoBehaviour
{
    private Player owner;
    private SkillData skill;
    private AreaSkillConfig areaConfig;
    private LayerMask targetLayer;
    private float lifetime;
    private bool explodeOnLifetimeEnd;
    private bool exploded;
    private Rigidbody2D rb;
    private Collider2D hitCollider;
    private Vector2 travelVelocity;
    private bool lockVelocity;

    public void Configure(
        Player owner,
        SkillData skill,
        AreaSkillConfig areaConfig,
        LayerMask targetLayer,
        Vector2 velocity,
        float gravityScale)
    {
        this.owner = owner;
        this.skill = skill;
        this.areaConfig = areaConfig;
        this.targetLayer = targetLayer;
        lifetime = areaConfig != null ? Mathf.Max(0f, areaConfig.ProjectileLifetime) : 0f;
        explodeOnLifetimeEnd = areaConfig != null && areaConfig.ExplodeOnLifetimeEnd;

        travelVelocity = velocity;
        lockVelocity = gravityScale <= 0f;

        rb = GetComponent<Rigidbody2D>();
        rb.simulated = true;
        rb.gravityScale = Mathf.Max(0f, gravityScale);
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.constraints = RigidbodyConstraints2D.None;
        rb.bodyType = rb.gravityScale > 0f ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
        rb.linearVelocity = velocity;
        rb.angularVelocity = 0f;

        hitCollider = GetComponent<Collider2D>();
        if (hitCollider != null)
        {
            hitCollider.isTrigger = true;
            hitCollider.enabled = true;
        }

        FaceVelocity(velocity);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        hitCollider = GetComponent<Collider2D>();
    }

    private void FixedUpdate()
    {
        if (!exploded && lockVelocity && rb != null)
        {
            rb.linearVelocity = travelVelocity;
        }
    }

    private void Update()
    {
        if (exploded)
        {
            return;
        }

        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.001f)
        {
            FaceVelocity(rb.linearVelocity);
        }

        if (lifetime <= 0f)
        {
            return;
        }

        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            if (explodeOnLifetimeEnd)
            {
                Explode(transform.position);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        TryExplode(collision);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryExplode(collision.collider);
    }

    private void TryExplode(Collider2D collision)
    {
        if (exploded || collision == null)
        {
            return;
        }

        if (owner != null && collision.transform.root == owner.transform.root)
        {
            return;
        }

        if (PlayerHitResolver.IsThreatSensorCollider(collision))
        {
            return;
        }

        if (collision.GetComponentInParent<AreaSkillProjectile2D>() != null)
        {
            return;
        }

        Vector3 hitPoint = collision.ClosestPoint(transform.position);
        Transform directHitRoot = null;
        if (owner != null
            && skill != null
            && PlayerHitResolver.TryApplyDamageToEnemy(collision, Mathf.RoundToInt(skill.Damage), owner.transform))
        {
            directHitRoot = collision.transform.root;
        }

        Explode(hitPoint, directHitRoot);
    }

    private void Explode(Vector3 center, Transform directHitRoot = null)
    {
        if (exploded)
        {
            return;
        }

        exploded = true;
        AreaSkillExecutor.SpawnArea(skill, areaConfig, owner, center, targetLayer, directHitRoot);
        Destroy(gameObject);
    }

    private void FaceVelocity(Vector2 velocity)
    {
        if (velocity.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
