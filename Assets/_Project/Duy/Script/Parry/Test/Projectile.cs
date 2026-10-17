using UnityEngine;
using PixPlays.ElementalVFX;

public class Projectile : MonoBehaviour
{
    [SerializeField] private ProjectileVfx projectileVfxPrefab;
    [SerializeField] private Transform source;
    [SerializeField] private Transform target;
    [SerializeField] private int damage = 10;
    [SerializeField] private float duration = 2f;
    [SerializeField] private float radius = 1f;
    [SerializeField] private float fireInterval = 5f;

    private float timer;

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            Shoot();
            timer = fireInterval;
        }
    }

    public void Shoot()
    {
        if (projectileVfxPrefab == null || source == null || target == null)
        {
            Debug.LogWarning("Missing references for Projectile!", this);
            return;
        }

        ProjectileVfx vfxInstance = Instantiate(projectileVfxPrefab);
        VfxData vfxData = new VfxData(source, target, duration, radius);
        vfxInstance.Play(vfxData);
        ParryTestProjectileHitbox.Create(source.position, target.position, duration, radius, damage, vfxInstance);
    }
}

public class ParryTestProjectileHitbox : MonoBehaviour
{
    private int damage;
    private float spawnTime;
    private float lifetime;
    private bool bounced;
    private Rigidbody2D rb;
    private ProjectileVfx vfx;
    private ParryableProjectileAdapter parryAdapter;

    public static ParryTestProjectileHitbox Create(
        Vector2 source,
        Vector2 target,
        float duration,
        float radius,
        int damage,
        ProjectileVfx vfx)
    {
        GameObject hitboxObject = new GameObject("ParryTestProjectileHitbox");
        hitboxObject.transform.position = source;

        CircleCollider2D collider = hitboxObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = Mathf.Max(0.05f, radius);

        Rigidbody2D body = hitboxObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;

        ParryTestProjectileHitbox hitbox = hitboxObject.AddComponent<ParryTestProjectileHitbox>();
        hitbox.Initialize(target, duration, damage, vfx);
        return hitbox;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        parryAdapter = ParryableProjectileAdapter.Ensure(this);
    }

    private void Initialize(Vector2 target, float duration, int hitDamage, ProjectileVfx projectileVfx)
    {
        damage = hitDamage;
        vfx = projectileVfx;
        lifetime = Mathf.Max(0.1f, duration);
        spawnTime = Time.time;

        Vector2 velocity = (target - (Vector2)transform.position) / lifetime;
        rb.linearVelocity = velocity;
        parryAdapter?.TrackMotion(velocity);
    }

    private void Update()
    {
        if (Time.time - spawnTime >= lifetime + 0.25f)
        {
            Destroy(gameObject);
        }

        if (!bounced && parryAdapter != null && parryAdapter.ShouldUseParriedMotion)
        {
            bounced = true;
            Vector2 motion = parryAdapter.CurrentMotion;
            parryAdapter.TrackMotion(motion);

            if (motion.sqrMagnitude > 0.0001f)
            {
                vfx?.Bounce((Vector2)transform.position + motion.normalized * 8f);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (parryAdapter != null && parryAdapter.IgnorePlayerCollision && collision.CompareTag("Player"))
        {
            return;
        }

        if (parryAdapter != null && parryAdapter.TryHandleEnemyCollision(collision, damage))
        {
            Destroy(gameObject);
            return;
        }

        if (!collision.CompareTag("Player"))
        {
            return;
        }

        IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
            collision,
            damage,
            ParryableAttackType.Projectile,
            parryAdapter,
            transform,
            rb != null ? rb.linearVelocity : Vector2.zero);

        if (resolution != IncomingAttackResolution.Parried)
        {
            vfx?.Stop();
            Destroy(gameObject);
        }
    }
}
