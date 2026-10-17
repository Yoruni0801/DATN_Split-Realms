using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMagicProjectile2D : MonoBehaviour
{
    [SerializeField] private GameObject impactParticleOverride;
    [SerializeField] private bool destroyOnFirstHit = true;

    private readonly Collider2D[] overlapBuffer = new Collider2D[8];

    private Player owner;
    private int damage;
    private LayerMask targetLayer;
    private float lifetime;
    private AudioClip enemyImpactSound;
    private AudioClip staticImpactSound;
    private float bounceBackSpeed;
    private float bounceBackLifetime;
    private bool hasHit;
    private bool hasBounced;
    private Rigidbody2D rb;
    private Collider2D hitCollider;
    private MagicProjectileScript magicProjectile;

    public void Configure(Player owner, Vector2 direction, float speed, int damage, float lifetime, LayerMask targetLayer)
    {
        Configure(owner, direction, speed, damage, lifetime, targetLayer, SkillSpawnRotationMode.FaceDirection, 0f);
    }

    public void Configure(
        Player owner,
        Vector2 direction,
        float speed,
        int damage,
        float lifetime,
        LayerMask targetLayer,
        SkillSpawnRotationMode rotationMode,
        float rotationOffset)
    {
        Configure(owner, direction, speed, damage, lifetime, targetLayer, rotationMode, rotationOffset, null, null, 0f, 0f);
    }

    public void Configure(
        Player owner,
        Vector2 direction,
        float speed,
        int damage,
        float lifetime,
        LayerMask targetLayer,
        SkillSpawnRotationMode rotationMode,
        float rotationOffset,
        AudioClip enemyImpactSound,
        AudioClip staticImpactSound,
        float bounceBackSpeed,
        float bounceBackLifetime)
    {
        this.owner = owner;
        this.damage = damage;
        this.lifetime = lifetime;
        this.targetLayer = targetLayer;
        this.enemyImpactSound = enemyImpactSound;
        this.staticImpactSound = staticImpactSound;
        this.bounceBackSpeed = Mathf.Max(0f, bounceBackSpeed);
        this.bounceBackLifetime = Mathf.Max(0f, bounceBackLifetime);

        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = direction.normalized * speed;

        hitCollider = GetComponent<Collider2D>();
        hitCollider.isTrigger = true;
        hitCollider.enabled = true;

        magicProjectile = GetComponentInChildren<MagicProjectileScript>();

        if (rotationMode == SkillSpawnRotationMode.FaceDirection)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);
        }
        else if (!Mathf.Approximately(rotationOffset, 0f))
        {
            transform.rotation *= Quaternion.Euler(0f, 0f, rotationOffset);
        }

        if (lifetime > 0f)
        {
            Destroy(gameObject, lifetime);
        }
    }

    private void Start()
    {
        if (hitCollider == null)
        {
            hitCollider = GetComponent<Collider2D>();
        }

        if (hitCollider != null)
        {
            ProcessInitialOverlaps();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        TryHit(collision);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryHit(collision.collider);
    }

    private void ProcessInitialOverlaps()
    {
        int count = hitCollider.Overlap(new ContactFilter2D
        {
            useLayerMask = false,
            useTriggers = true
        }, overlapBuffer);

        for (int i = 0; i < count; i++)
        {
            if (TryHit(overlapBuffer[i]) && destroyOnFirstHit)
            {
                return;
            }
        }
    }

    private bool TryHit(Collider2D collision)
    {
        if (hasHit || hasBounced || collision == null || owner == null)
        {
            return false;
        }

        if (collision.transform.root == owner.transform.root)
        {
            return false;
        }

        if (PlayerHitResolver.IsThreatSensorCollider(collision))
        {
            return false;
        }

        bool isTargetLayer = ((1 << collision.gameObject.layer) & targetLayer.value) != 0;
        if (PlayerHitResolver.TryApplyDamageToEnemy(collision, damage, owner.transform))
        {
            HandleEnemyImpact(collision);
            return true;
        }

        if (isTargetLayer)
        {
            InazumaCubeHit cube = collision.GetComponentInParent<InazumaCubeHit>();
            if (cube != null)
            {
                cube.OnMeleeHit();
                HandleTargetImpact(collision, enemyImpactSound);
                return true;
            }

            return false;
        }

        if (IsStaticImpact(collision))
        {
            PlayImpactSound(staticImpactSound);
            SpawnImpact(collision);
            BounceBack();
            return true;
        }

        return false;
    }

    private void HandleEnemyImpact(Collider2D collision)
    {
        Bao.Enemies.BaoEnemyBase baoEnemy = collision.GetComponentInParent<Bao.Enemies.BaoEnemyBase>();
        if (baoEnemy != null)
        {
            baoEnemy.ApplyKnockback(transform, 4f);
        }

        if (CombatFeedbackManager.Instance != null)
        {
            CombatFeedbackManager.Instance.PlayHitFeedback(collision.ClosestPoint(transform.position), false);
        }

        HandleTargetImpact(collision, enemyImpactSound);
    }

    private void HandleTargetImpact(Collider2D collision, AudioClip impactSound)
    {
        PlayImpactSound(impactSound);
        hasHit = true;
        SpawnImpact(collision);

        if (destroyOnFirstHit)
        {
            Destroy(gameObject);
        }
    }

    private static bool IsStaticImpact(Collider2D collision)
    {
        if (collision.gameObject.isStatic)
        {
            return true;
        }

        Rigidbody2D attachedBody = collision.attachedRigidbody;
        return attachedBody == null || attachedBody.bodyType == RigidbodyType2D.Static;
    }

    private void BounceBack()
    {
        if (hasBounced)
        {
            return;
        }

        hasBounced = true;

        if (rb != null && bounceBackSpeed > 0f)
        {
            Vector2 reverse = -rb.linearVelocity.normalized;
            if (reverse.sqrMagnitude <= 0.001f)
            {
                reverse = Vector2.left;
            }

            rb.linearVelocity = reverse * bounceBackSpeed;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(reverse.y, reverse.x) * Mathf.Rad2Deg);
        }

        if (bounceBackLifetime > 0f)
        {
            Destroy(gameObject, bounceBackLifetime);
        }
    }

    private void PlayImpactSound(AudioClip clip)
    {
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, transform.position);
        }
    }

    private void SpawnImpact(Collider2D collision)
    {
        GameObject impactPrefab = impactParticleOverride;
        if (impactPrefab == null && magicProjectile != null)
        {
            impactPrefab = magicProjectile.impactParticle;
        }

        if (impactPrefab == null)
        {
            return;
        }

        Vector2 hitPoint = collision.ClosestPoint(transform.position);
        GameObject impact = Instantiate(impactPrefab, hitPoint, Quaternion.identity);
        Destroy(impact, 3f);
    }
}
