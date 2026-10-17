using UnityEngine;

[DisallowMultipleComponent]
public class ParryableProjectileAdapter : MonoBehaviour, IParryable
{
    [SerializeField] private ProjectileParryResponseMode responseMode = ProjectileParryResponseMode.Reflect;
    [SerializeField] private ParryableAttackType particleAttackType = ParryableAttackType.Projectile;
    
    [SerializeField] private bool pushBackOnParry = true;
    [SerializeField] private bool ignorePlayerCollisionAfterParry = true;
    [SerializeField] private bool canBeParried = true;
    [SerializeField] private bool useParticleSystemTriggers = false;
    [SerializeField] private bool particleCanBeBlocked = true;
    [SerializeField] private bool particleIsSlowAttack = false;
    [SerializeField] private bool destroyParticleOnHit = true;

    [SerializeField] private float speedMultiplier = 1.15f;
    [SerializeField] private float minimumParriedSpeed = 8f;
    [SerializeField] private int particleDamage = 10;


    private Rigidbody2D cachedRigidbody;
    private Collider2D cachedCollider;
    private ParticleSystem cachedParticleSystem;
    private Vector2 trackedDirection = Vector2.right;
    private Vector2 trackedMotion = Vector2.right * 8f;
    private IParryable parrySource;

    public bool IsPlayerOwned { get; private set; }
    public bool HasBeenParried { get; private set; }
    public bool ShouldUseParriedMotion { get; private set; }
    public bool IgnorePlayerCollision =>
        IsPlayerOwned || (HasBeenParried && ignorePlayerCollisionAfterParry);
    public Vector2 CurrentMotion
    {
        get
        {
            if (cachedRigidbody != null && cachedRigidbody.linearVelocity.sqrMagnitude > 0.0001f)
            {
                return cachedRigidbody.linearVelocity;
            }

            return trackedMotion;
        }
    }

    public static ParryableProjectileAdapter Ensure(Component source)
    {
        if (source == null)
        {
            return null;
        }

        if (source.TryGetComponent(out ParryableProjectileAdapter adapter))
        {
            return adapter;
        }

        return source.gameObject.AddComponent<ParryableProjectileAdapter>();
    }

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody2D>();
        cachedCollider = GetComponent<Collider2D>();
        cachedParticleSystem = GetComponent<ParticleSystem>();
    }

    private void Start()
    {
        // Automatically setup the Player's colliders in the Particle System's Trigger module
        if (useParticleSystemTriggers && cachedParticleSystem != null)
        {
            SetupPlayerTriggerColliders();
        }
    }

    public void TrackMotion(Vector2 motion)
    {
        if (motion.sqrMagnitude > 0.0001f)
        {
            trackedMotion = motion;
            trackedDirection = motion.normalized;
        }
    }

    public void ConfigureParticleAttack(int damage, bool isSlowAttack = false, bool canBeBlocked = true, ProjectileParryResponseMode response = ProjectileParryResponseMode.ConvertToPlayer, bool parryable = true, IParryable source = null)
    {
        useParticleSystemTriggers = true;
        particleDamage = damage;
        particleIsSlowAttack = isSlowAttack;
        particleCanBeBlocked = canBeBlocked;
        responseMode = response;
        canBeParried = parryable;
        parrySource = source;

        if (cachedParticleSystem == null)
        {
            cachedParticleSystem = GetComponent<ParticleSystem>();
        }

        SetupPlayerTriggerColliders();
    }

    private void SetupPlayerTriggerColliders()
    {
        if (cachedParticleSystem == null) return;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            Collider2D[] playerColliders = playerObj.GetComponentsInChildren<Collider2D>();
            var triggerModule = cachedParticleSystem.trigger;
            
            int colliderIndex = 0;
            foreach (var col in playerColliders)
            {
                if (col != null && !col.isTrigger)
                {
                    triggerModule.SetCollider(colliderIndex, col);
                    colliderIndex++;
                    if (colliderIndex >= triggerModule.colliderCount)
                        break;
                }
            }
        }
    }

    private void SetupEnemyTriggerColliders()
    {
        if (cachedParticleSystem == null) return;

        GameObject[] enemyObjs = GameObject.FindGameObjectsWithTag("Enemy");
        var triggerModule = cachedParticleSystem.trigger;
        
        int colliderIndex = 0;
        foreach (var enemyObj in enemyObjs)
        {
            Collider2D[] enemyColliders = enemyObj.GetComponentsInChildren<Collider2D>();
            foreach (var col in enemyColliders)
            {
                if (col != null && !col.isTrigger)
                {
                    triggerModule.SetCollider(colliderIndex, col);
                    colliderIndex++;
                    if (colliderIndex >= triggerModule.colliderCount)
                        break;
                }
            }
            if (colliderIndex >= triggerModule.colliderCount)
                break;
        }
    }

    public bool TryHandleEnemyCollision(Collider2D collision, int damage)
    {
        if (!IsPlayerOwned)
        {
            return false;
        }

        return PlayerHitResolver.TryApplyDamageToEnemy(collision, damage);
    }

    public ParryResult OnParried(ParryContext context)
    {
        if (!canBeParried)
        {
            return ParryResult.Failed;
        }

        HasBeenParried = true;

        ParryResult sourceResult = parrySource != null
            ? parrySource.OnParried(context)
            : ParryResult.Successful(context.DefaultHitStopDuration);
        if (!sourceResult.Success)
        {
            return sourceResult;
        }


        if (responseMode == ProjectileParryResponseMode.Destroy)
        {
            if (cachedCollider != null)
            {
                cachedCollider.enabled = false;
            }

            if (!useParticleSystemTriggers)
            {
                Destroy(gameObject);
            }
            else
            {
                if (cachedParticleSystem != null)
                {
                    var emission = cachedParticleSystem.emission;
                    emission.enabled = false;
                    cachedParticleSystem.Clear();
                }
            }
            return new ParryResult(true, sourceResult.PreventDamage, sourceResult.HitStopDuration);
        }

        IsPlayerOwned = responseMode == ProjectileParryResponseMode.ConvertToPlayer || responseMode == ProjectileParryResponseMode.Reflect;
        if (!pushBackOnParry)
        {
            ShouldUseParriedMotion = false;
            return new ParryResult(true, sourceResult.PreventDamage, sourceResult.HitStopDuration);
        }

        Vector2 direction = context.IncomingDirection.sqrMagnitude > 0.0001f
            ? -context.IncomingDirection.normalized
            : -trackedDirection.normalized;
        float currentSpeed = cachedRigidbody != null && cachedRigidbody.linearVelocity.sqrMagnitude > 0.0001f
            ? cachedRigidbody.linearVelocity.magnitude
            : minimumParriedSpeed;
        float parriedSpeed = Mathf.Max(minimumParriedSpeed, currentSpeed * speedMultiplier);

        ShouldUseParriedMotion = true;
        TrackMotion(direction * parriedSpeed);
        if (cachedRigidbody != null)
        {
            cachedRigidbody.linearVelocity = trackedMotion;
        }

        if (!useParticleSystemTriggers)
        {
            Vector3 scale = transform.localScale;
            scale.x = -scale.x;
            transform.localScale = scale;
        }

        if (useParticleSystemTriggers && cachedParticleSystem != null)
        {
            // Set trigger colliders to enemies so reflected particles can trigger hits on them
            SetupEnemyTriggerColliders();

            // Rotate Particle System transform to point in the reflected direction so new particles emit towards the targets
            if (direction.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }

            // Reflect all active particles
            int maxParticles = cachedParticleSystem.main.maxParticles;
            ParticleSystem.Particle[] particles = new ParticleSystem.Particle[maxParticles];
            int numParticlesAlive = cachedParticleSystem.GetParticles(particles);

            for (int i = 0; i < numParticlesAlive; i++)
            {
                Vector3 reflectedVelocityWorld = trackedMotion;
                Vector3 reflectedVelocityLocal = (cachedParticleSystem.main.simulationSpace == ParticleSystemSimulationSpace.Local)
                    ? transform.InverseTransformDirection(reflectedVelocityWorld)
                    : reflectedVelocityWorld;

                particles[i].velocity = reflectedVelocityLocal;
            }

            cachedParticleSystem.SetParticles(particles, numParticlesAlive);
        }

        return new ParryResult(true, sourceResult.PreventDamage, sourceResult.HitStopDuration);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!useParticleSystemTriggers && IsPlayerOwned)
        {
            if (collision.CompareTag("Player")) return;
            if (PlayerHitResolver.IsThreatSensorCollider(collision)) return;

            if (PlayerHitResolver.TryApplyDamageToEnemy(collision, particleDamage))
            {
                if (destroyParticleOnHit)
                {
                    Destroy(gameObject);
                }
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!useParticleSystemTriggers && IsPlayerOwned)
        {
            if (collision.collider.CompareTag("Player")) return;
            if (PlayerHitResolver.IsThreatSensorCollider(collision.collider)) return;

            if (PlayerHitResolver.TryApplyDamageToEnemy(collision.collider, particleDamage))
            {
                if (destroyParticleOnHit)
                {
                    Destroy(gameObject);
                }
            }
        }
    }

    private void OnParticleTrigger()
    {
        if (!useParticleSystemTriggers || cachedParticleSystem == null)
        {
            return;
        }

        System.Collections.Generic.List<ParticleSystem.Particle> enterParticles = new System.Collections.Generic.List<ParticleSystem.Particle>();
        int numEnter = cachedParticleSystem.GetTriggerParticles(ParticleSystemTriggerEventType.Enter, enterParticles);

        if (numEnter <= 0)
        {
            return;
        }

        System.Collections.Generic.List<Collider2D> triggerColliders = new System.Collections.Generic.List<Collider2D>();
        for (int i = 0; i < cachedParticleSystem.trigger.colliderCount; i++)
        {
            Component colComp = cachedParticleSystem.trigger.GetCollider(i);
            if (colComp != null && colComp is Collider2D col2D)
            {
                triggerColliders.Add(col2D);
            }
        }

        if (triggerColliders.Count == 0)
        {
            return;
        }

        bool particlesModified = false;

        for (int i = 0; i < numEnter; i++)
        {
            ParticleSystem.Particle p = enterParticles[i];
            Vector3 worldPos = (cachedParticleSystem.main.simulationSpace == ParticleSystemSimulationSpace.Local)
                ? transform.TransformPoint(p.position)
                : p.position;

            Collider2D bestCollider = null;
            float bestDistance = float.MaxValue;

            foreach (var col in triggerColliders)
            {
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;

                Vector2 closestPoint = col.ClosestPoint(worldPos);
                float dist = Vector2.Distance(worldPos, closestPoint);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestCollider = col;
                }
            }

            if (bestCollider != null && bestDistance < 2.0f)
            {
                bool handled = HandleParticleHit(bestCollider, ref p, worldPos);
                if (handled)
                {
                    enterParticles[i] = p;
                    particlesModified = true;
                }
            }
        }

        if (particlesModified)
        {
            cachedParticleSystem.SetTriggerParticles(ParticleSystemTriggerEventType.Enter, enterParticles);
        }
    }

    private void OnParticleCollision(GameObject other)
    {
        if (!useParticleSystemTriggers || cachedParticleSystem == null)
        {
            return;
        }

        Collider2D otherCollider = other.GetComponent<Collider2D>();
        if (otherCollider == null)
        {
            return;
        }

        System.Collections.Generic.List<ParticleCollisionEvent> collisionEvents = new System.Collections.Generic.List<ParticleCollisionEvent>();
        int numCollisionEvents = cachedParticleSystem.GetCollisionEvents(other, collisionEvents);

        for (int i = 0; i < numCollisionEvents; i++)
        {
            if (other.CompareTag("Player"))
            {
                if (IgnorePlayerCollision)
                {
                    continue;
                }

                PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
                if (playerHealth != null)
                {
                    if (!canBeParried)
                    {
                        // Direct damage allows multiple particle collisions to damage in the same frame
                        playerHealth.TakeDamage(particleDamage, transform);
                    }
                    else
                    {
                        Vector3 normal = collisionEvents[i].normal;
                        PlayerHitResolver.TryApplyHitToPlayer(
                            otherCollider,
                            particleDamage,
                            particleAttackType,
                            this,
                            transform,
                            -normal,
                            particleCanBeBlocked,
                            particleIsSlowAttack);
                    }
                }
            }
            else if (IsPlayerOwned)
            {
                if (PlayerHitResolver.IsThreatSensorCollider(otherCollider))
                {
                    continue;
                }

                PlayerHitResolver.TryApplyDamageToEnemy(otherCollider, particleDamage);
            }
        }
    }

    private bool HandleParticleHit(Collider2D collision, ref ParticleSystem.Particle particle, Vector3 worldPos)
    {
        if (IsPlayerOwned)
        {
            if (collision.CompareTag("Player"))
            {
                return false;
            }

            if (PlayerHitResolver.IsThreatSensorCollider(collision))
            {
                return false;
            }

            if (PlayerHitResolver.TryApplyDamageToEnemy(collision, particleDamage))
            {
                if (destroyParticleOnHit)
                {
                    particle.remainingLifetime = 0f;
                }
                return true;
            }
        }
        else
        {
            if (!collision.CompareTag("Player"))
            {
                return false;
            }

            if (IgnorePlayerCollision)
            {
                return false;
            }

            PlayerHealth playerHealth = collision.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                if (!canBeParried)
                {
                    // Direct damage allows multiple particle colliders to damage in the same frame
                    playerHealth.TakeDamage(particleDamage, transform);
                    if (destroyParticleOnHit)
                    {
                        particle.remainingLifetime = 0f;
                    }
                    return true;
                }
                else
                {
                    Vector3 velocityWorld = (cachedParticleSystem.main.simulationSpace == ParticleSystemSimulationSpace.Local)
                        ? transform.TransformDirection(particle.velocity)
                        : (Vector3)particle.velocity;

                    IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                        collision,
                        particleDamage,
                        particleAttackType,
                        this,
                        transform,
                        velocityWorld.normalized,
                        particleCanBeBlocked,
                        particleIsSlowAttack);

                    if (resolution == IncomingAttackResolution.Parried)
                    {
                        if (responseMode == ProjectileParryResponseMode.Destroy)
                        {
                            particle.remainingLifetime = 0f;
                        }
                        else
                        {
                            Vector3 reflectedVelocityWorld = trackedMotion;
                            Vector3 reflectedVelocityLocal = (cachedParticleSystem.main.simulationSpace == ParticleSystemSimulationSpace.Local)
                                ? transform.InverseTransformDirection(reflectedVelocityWorld)
                                : reflectedVelocityWorld;

                            particle.velocity = reflectedVelocityLocal;
                        }
                        return true;
                    }
                    else if (resolution != IncomingAttackResolution.Ignored)
                    {
                        if (destroyParticleOnHit)
                        {
                            particle.remainingLifetime = 0f;
                        }
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
