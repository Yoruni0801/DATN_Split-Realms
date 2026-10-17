using System.Collections;
using System.Collections.Generic;
using Spine;
using UnityEngine;

public sealed class FireSpearDashSkillRunner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private AnimationSpine animationSpine;
    [SerializeField] private Transform spearHitPoint;

    [Header("Animation")]
    [SerializeField] private AnimationSpine.State skillAnimationState = AnimationSpine.State.Spear;

    private readonly Collider2D[] hitBuffer = new Collider2D[16];
    private readonly HashSet<int> hitTargetIds = new HashSet<int>();
    private readonly List<GameObject> activeVfx = new List<GameObject>();

    private Player owner;
    private PlayerHealth ownerHealth;
    private Coroutine routine;
    private TrackEntry activeSkillTrackEntry;
    private float originalGravityScale;
    private bool running;
    private bool skillAnimationCompleted;

    private struct SpearDashTargetData
    {
        public Transform Target;
        public Vector2 Direction;
        public float TargetDistance;
        public float DashDistance;
        public float DistanceRatio;
    }

    public bool IsRunning => running;

    private void Awake()
    {
        CacheReferences();
    }

    public bool TryStartSkill(SkillData skill, FireSpearDashSkillConfig config, SkillCastContext context)
    {
        if (running || skill == null || config == null || context == null || context.Owner == null)
        {
            return false;
        }

        CacheReferences();
        owner = context.Owner;
        if (ownerHealth != null && ownerHealth.IsDead)
        {
            return false;
        }

        if (skill.ElementType != SkillElementType.Fire)
        {
            Debug.LogWarning($"Skill '{skill.SkillName}' must use SkillElementType.Fire.", this);
            return false;
        }

        if (rb == null)
        {
            Debug.LogWarning("[FireSpearDashSkillRunner] Missing Rigidbody2D reference.", this);
            return false;
        }

        routine = StartCoroutine(SkillRoutine(skill, config, context));
        return true;
    }

    private IEnumerator SkillRoutine(SkillData skill, FireSpearDashSkillConfig config, SkillCastContext context)
    {
        running = true;
        hitTargetIds.Clear();
        originalGravityScale = rb.gravityScale;

        SpearDashTargetData targetData = ResolveTargetData(config, context);
        Vector2 direction = targetData.Direction;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = context.FacingDirection < 0f ? Vector2.left : Vector2.right;
        }

        direction = direction.x < 0f ? Vector2.left : Vector2.right;
        owner.FacePosition(transform.position + (Vector3)direction);
        owner.SetInputActive(false);

        ResolveAdaptiveTiming(config, targetData, out float animationSpeed, out float dashDuration);
        bool skillColliderHandlesDamage = skill.SkillColliderEnabled && skill.ColliderDealsDamage;
        bool stopFromSkillColliderHit = false;
        owner.ShowSkillPresentation(skill, hit =>
        {
            if (HandleSkillColliderHit(config, hit) && (!config.Piercing || hitTargetIds.Count >= config.MaximumTargets))
            {
                stopFromSkillColliderHit = true;
            }
        });
        float animationStartTime = Time.time;
        PlaySkillAnimation(animationSpeed);
        SpawnAttachedVfx(config);

        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;

        float scaledStartup = config.Startup / Mathf.Max(0.01f, animationSpeed);
        if (scaledStartup > 0f)
        {
            yield return new WaitForSeconds(scaledStartup);
        }

        float scaledActive = config.Active / Mathf.Max(0.01f, animationSpeed);
        float activeUntil = Time.time + scaledActive;
        Vector2 startPosition = rb.position;
        float lockedY = rb.position.y;
        float totalDistance = Mathf.Max(0f, targetData.DashDistance);
        float elapsed = 0f;
        bool movementFinished = totalDistance <= 0.001f;

        while (!movementFinished || Time.time <= activeUntil)
        {
            if (ownerHealth != null && ownerHealth.IsDead)
            {
                break;
            }

            if (!movementFinished)
            {
                elapsed += Time.fixedDeltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / dashDuration);
                float curveValue = config.SpeedCurve != null
                    ? Mathf.Clamp01(config.SpeedCurve.Evaluate(normalizedTime))
                    : normalizedTime;
                float travelledDistance = totalDistance * curveValue;

                Vector2 desired = new Vector2(startPosition.x + direction.x * travelledDistance, lockedY);
                Vector2 current = rb.position;
                Vector2 next = ClampAgainstWall(current, desired, direction, config.WallStopDistance, out bool hitWall);

                rb.MovePosition(next);
                rb.linearVelocity = Vector2.zero;

                if (hitWall || normalizedTime >= 1f || Mathf.Abs(next.x - startPosition.x) >= totalDistance - 0.001f)
                {
                    movementFinished = true;
                }
            }
            else
            {
                rb.linearVelocity = Vector2.zero;
            }

            if (skillColliderHandlesDamage)
            {
                movementFinished |= stopFromSkillColliderHit;
            }
            else if (Time.time <= activeUntil)
            {
                movementFinished |= ProcessHits(skill, config, direction);
            }

            if (movementFinished && Time.time > activeUntil)
            {
                break;
            }

            yield return new WaitForFixedUpdate();
        }

        rb.linearVelocity = Vector2.zero;

        yield return WaitForSkillAnimationOrRecovery(config, animationSpeed, animationStartTime);

        routine = null;
        Cleanup(config.VfxStopDelay, restoreInput: true);
    }

    private SpearDashTargetData ResolveTargetData(FireSpearDashSkillConfig config, SkillCastContext context)
    {
        Vector2 facing = context.FacingDirection < 0f ? Vector2.left : Vector2.right;
        Vector3 origin = context.Body != null ? context.Body.position : transform.position;
        Transform target = config.AutoTarget
            ? FindNearestFrontTarget(origin, facing, config.TargetRange)
            : null;

        if (target == null)
        {
            return new SpearDashTargetData
            {
                Target = null,
                Direction = facing,
                TargetDistance = config.Distance,
                DashDistance = Mathf.Max(0f, config.Distance),
                DistanceRatio = 1f
            };
        }

        float targetDistance = Mathf.Abs(target.position.x - origin.x);
        float dashDistance = Mathf.Clamp(targetDistance - config.TargetStopDistance, 0f, Mathf.Max(0f, config.Distance));
        float distanceRatio = Mathf.InverseLerp(config.MinAdaptiveDistance, config.MaxAdaptiveDistance, targetDistance);

        return new SpearDashTargetData
        {
            Target = target,
            Direction = target.position.x >= origin.x ? Vector2.right : Vector2.left,
            TargetDistance = targetDistance,
            DashDistance = dashDistance,
            DistanceRatio = distanceRatio
        };
    }

    private static void ResolveAdaptiveTiming(FireSpearDashSkillConfig config, SpearDashTargetData targetData, out float animationSpeed, out float dashDuration)
    {
        animationSpeed = 1f;
        dashDuration = config.Duration;

        if (config.AdaptiveSpeedEnabled && targetData.Target != null)
        {
            animationSpeed = Mathf.Lerp(config.NearAnimationSpeed, config.FarAnimationSpeed, targetData.DistanceRatio);
            dashDuration = Mathf.Lerp(config.NearDashDuration, config.FarDashDuration, targetData.DistanceRatio);
        }

        animationSpeed = Mathf.Clamp(animationSpeed, 0.5f, 2f);
        dashDuration = Mathf.Max(0.01f, dashDuration);
    }

    private Transform FindNearestFrontTarget(Vector3 origin, Vector2 direction, float range)
    {
        if (range <= 0f)
        {
            return null;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, range, PlayerLayerConfig.AttackTargetLayer);
        Transform best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.transform.root == transform.root)
            {
                continue;
            }

            float offsetX = hit.bounds.center.x - origin.x;
            if (Mathf.Sign(offsetX) != Mathf.Sign(direction.x) && Mathf.Abs(offsetX) > 0.05f)
            {
                continue;
            }

            if (!IsDamageableTarget(hit))
            {
                continue;
            }

            float distance = Mathf.Abs(offsetX);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = hit.transform;
            }
        }

        return best;
    }

    private Vector2 ClampAgainstWall(Vector2 current, Vector2 desired, Vector2 direction, float stopDistance, out bool hitWall)
    {
        hitWall = false;
        float distance = Mathf.Abs(desired.x - current.x);
        LayerMask obstacleLayer = PlayerLayerConfig.EnvironmentLayer;
        if (distance <= 0f || obstacleLayer.value == 0)
        {
            return desired;
        }

        RaycastHit2D hit = Physics2D.Raycast(current, direction, distance + stopDistance, obstacleLayer);
        if (!hit.collider)
        {
            return desired;
        }

        hitWall = true;
        float allowed = Mathf.Max(0f, hit.distance - stopDistance);
        return new Vector2(current.x + direction.x * allowed, current.y);
    }

    private bool ProcessHits(SkillData skill, FireSpearDashSkillConfig config, Vector2 direction)
    {
        if (spearHitPoint == null)
        {
            return false;
        }

        ContactFilter2D targetFilter = new ContactFilter2D();
        targetFilter.SetLayerMask(PlayerLayerConfig.AttackTargetLayer);
        targetFilter.useTriggers = Physics2D.queriesHitTriggers;
        int count = Physics2D.OverlapBox(spearHitPoint.position, config.HitboxSize, 0f, targetFilter, hitBuffer);
        bool shouldStop = false;

        for (int i = 0; i < count; i++)
        {
            if (hitTargetIds.Count >= config.MaximumTargets)
            {
                shouldStop = true;
                break;
            }

            Collider2D hit = hitBuffer[i];
            if (hit == null || hit.transform.root == transform.root)
            {
                continue;
            }

            int targetId = ResolveTargetId(hit);
            if (targetId == 0 || hitTargetIds.Contains(targetId))
            {
                continue;
            }

            Vector3 hitPoint = hit.ClosestPoint(spearHitPoint.position);
            bool damaged = ApplyDamage(skill, config, hit, hitPoint, direction);
            if (!damaged)
            {
                continue;
            }

            hitTargetIds.Add(targetId);
            SpawnImpactVfx(config, ResolveTargetVfxPosition(hit));
            ApplyBurnIfSupported(config, hit);

            if (CombatFeedbackManager.Instance != null)
            {
                CombatFeedbackManager.Instance.PlayHitFeedback(hitPoint, false);
            }

            if (!config.Piercing || hitTargetIds.Count >= config.MaximumTargets)
            {
                shouldStop = true;
                break;
            }
        }

        return shouldStop;
    }

    private bool HandleSkillColliderHit(FireSpearDashSkillConfig config, Collider2D hit)
    {
        if (hit == null || hit.transform.root == transform.root)
        {
            return false;
        }

        if (hitTargetIds.Count >= config.MaximumTargets)
        {
            return false;
        }

        int targetId = ResolveTargetId(hit);
        if (targetId == 0 || hitTargetIds.Contains(targetId))
        {
            return false;
        }

        hitTargetIds.Add(targetId);
        SpawnImpactVfx(config, ResolveTargetVfxPosition(hit));
        ApplyBurnIfSupported(config, hit);
        return true;
    }

    private bool ApplyDamage(SkillData skill, FireSpearDashSkillConfig config, Collider2D hit, Vector3 hitPoint, Vector2 direction)
    {
        if (PlayerHitResolver.IsThreatSensorCollider(hit))
        {
            return false;
        }

        float damage = skill.Damage * Mathf.Max(0f, config.DamageMultiplier);
        IEnemyTarget enemyTarget = hit.GetComponentInParent<IEnemyTarget>();
        if (enemyTarget != null && enemyTarget.IsAlive)
        {
            SkillHitData hitData = new SkillHitData(skill, damage, config.KnockbackForce, config.KnockUpForce);
            owner.ApplySkillHit(hitData, enemyTarget, hitPoint);
            return true;
        }

        bool damaged = PlayerHitResolver.TryApplyDamageToEnemy(hit, Mathf.RoundToInt(damage), owner != null ? owner.transform : null);
        if (!damaged)
        {
            InazumaCubeHit cube = hit.GetComponentInParent<InazumaCubeHit>();
            if (cube != null)
            {
                cube.OnMeleeHit();
                return true;
            }

            return false;
        }

        Bao.Enemies.BaoEnemyBase baoEnemy = hit.GetComponentInParent<Bao.Enemies.BaoEnemyBase>();
        if (baoEnemy != null && config.KnockbackForce > 0f)
        {
            baoEnemy.ApplyKnockback(transform, config.KnockbackForce);
        }

        Rigidbody2D targetBody = hit.attachedRigidbody;
        if (targetBody != null && config.KnockbackForce > 0f)
        {
            targetBody.linearVelocity = new Vector2(direction.x * config.KnockbackForce, config.KnockUpForce);
        }

        return true;
    }

    private void SpawnAttachedVfx(FireSpearDashSkillConfig config)
    {
        if (config.AttackVfxPrefab != null && spearHitPoint != null)
        {
            GameObject spearFlame = EffectPool.Spawn(config.AttackVfxPrefab, spearHitPoint.position, spearHitPoint.rotation);
            AttachLocal(spearFlame, spearHitPoint, config.AttackVfxLocalPosition, config.AttackVfxLocalEuler, config.AttackVfxLocalScale);
            RestartSpawnedVfx(spearFlame);
            activeVfx.Add(spearFlame);
        }
    }

    private void SpawnImpactVfx(FireSpearDashSkillConfig config, Vector3 position)
    {
        GameObject impactPrefab = config.ImpactVfxPrefab;
        if (impactPrefab == null)
        {
            return;
        }

        Quaternion rotation = Quaternion.Euler(config.ImpactVfxLocalEuler);
        GameObject impactVfx = Instantiate(impactPrefab, position, rotation);
        impactVfx.transform.localScale = config.ImpactVfxLocalScale;
        RestartSpawnedVfx(impactVfx);
        if (config.ImpactLifetime > 0f)
        {
            Destroy(impactVfx, config.ImpactLifetime);
        }
    }

    private Vector3 ResolveTargetVfxPosition(Collider2D hit)
    {
        Vector3 position = hit.bounds.center;
        position.z = spearHitPoint != null ? spearHitPoint.position.z : transform.position.z;
        return position;
    }

    private void Cleanup(float stopDelay, bool restoreInput)
    {
        if (!running && routine == null)
        {
            return;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        UnregisterSkillTrackEntry();

        if (rb != null)
        {
            rb.gravityScale = originalGravityScale;
            rb.linearVelocity = Vector2.zero;
        }

        for (int i = 0; i < activeVfx.Count; i++)
        {
            ReleaseAttachedVfx(activeVfx[i], stopDelay);
        }

        activeVfx.Clear();
        hitTargetIds.Clear();

        if (restoreInput && owner != null)
        {
            owner.SetInputActive(true);
            owner.HideSkillPresentation();
        }

        running = false;
    }

    private void ReleaseAttachedVfx(GameObject instance, float delay)
    {
        if (instance == null)
        {
            return;
        }

        StopAndClearParticles(instance);
        if (delay > 0f && gameObject.activeInHierarchy)
        {
            StartCoroutine(ReleaseAfterDelay(instance, delay));
            return;
        }

        EffectPool.Release(instance);
    }

    private IEnumerator ReleaseAfterDelay(GameObject instance, float delay)
    {
        int version = EffectPool.GetVersion(instance);
        yield return new WaitForSeconds(delay);
        EffectPool.Release(instance, version);
    }

    private void ApplyBurnIfSupported(FireSpearDashSkillConfig config, Collider2D hit)
    {
        if (!config.BurnEnabled || Random.value > config.BurnChance)
        {
            return;
        }

        if (PlayerHitResolver.IsThreatSensorCollider(hit))
        {
            return;
        }

        Transform target = hit.GetComponentInParent<IEnemyTarget>()?.Transform ?? hit.transform.root;
        if (target == null)
        {
            return;
        }

        BurnStatusPayload payload = new BurnStatusPayload(config.BurnDamage, config.BurnDuration, owner);
        target.SendMessage("ApplyBurn", payload, SendMessageOptions.DontRequireReceiver);
        target.SendMessage("ApplyStatusEffect", payload, SendMessageOptions.DontRequireReceiver);
    }

    private void PlaySkillAnimation(float animationSpeed)
    {
        UnregisterSkillTrackEntry();

        if (animationSpine == null || skillAnimationState == AnimationSpine.State.None)
        {
            return;
        }

        skillAnimationCompleted = false;
        activeSkillTrackEntry = animationSpine.PlayForced(skillAnimationState, false, 0);
        if (activeSkillTrackEntry == null)
        {
            skillAnimationCompleted = true;
            return;
        }

        activeSkillTrackEntry.TimeScale = animationSpeed;
        activeSkillTrackEntry.Complete += OnSkillAnimationComplete;
    }

    private void OnSkillAnimationComplete(TrackEntry entry)
    {
        if (entry != activeSkillTrackEntry)
        {
            return;
        }

        skillAnimationCompleted = true;
    }

    private IEnumerator WaitForSkillAnimationOrRecovery(FireSpearDashSkillConfig config, float animationSpeed, float animationStartTime)
    {
        bool hasSkillAnimation = activeSkillTrackEntry != null;
        float recoveryEndTime = Time.time + config.Recovery;
        float animationDuration = hasSkillAnimation && activeSkillTrackEntry.Animation != null
            ? activeSkillTrackEntry.Animation.Duration
            : config.Duration + config.Recovery;
        float scaledAnimationDuration = animationDuration / Mathf.Max(0.01f, animationSpeed);
        float animationTimeout = scaledAnimationDuration * Mathf.Max(0.1f, config.AnimationCompletionTimeoutMultiplier);

        while (true)
        {
            if (ownerHealth != null && ownerHealth.IsDead)
            {
                break;
            }

            bool waitingForAnimation = hasSkillAnimation
                && !skillAnimationCompleted
                && Time.time - animationStartTime < animationTimeout;
            bool waitingForRecovery = Time.time < recoveryEndTime;
            if (!waitingForAnimation && !waitingForRecovery)
            {
                break;
            }

            yield return null;
        }
    }

    private void UnregisterSkillTrackEntry()
    {
        if (activeSkillTrackEntry != null)
        {
            activeSkillTrackEntry.Complete -= OnSkillAnimationComplete;
            activeSkillTrackEntry.TimeScale = 1f;
            activeSkillTrackEntry = null;
        }

        skillAnimationCompleted = false;
    }

    private void CacheReferences()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (animationSpine == null)
        {
            animationSpine = GetComponentInChildren<AnimationSpine>();
        }

        if (owner == null)
        {
            owner = GetComponent<Player>();
        }

        if (ownerHealth == null)
        {
            ownerHealth = GetComponent<PlayerHealth>();
        }

        if (spearHitPoint == null)
        {
            spearHitPoint = FindChildByName("SpearHitPoint");
        }

        if (spearHitPoint == null)
        {
            spearHitPoint = transform;
        }

    }

    private Transform FindChildByName(string childName)
    {
        if (string.IsNullOrEmpty(childName))
        {
            return null;
        }

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private static void AttachLocal(GameObject instance, Transform parent, Vector3 localPosition, Vector3 localEuler, Vector3 localScale)
    {
        if (instance == null || parent == null)
        {
            return;
        }

        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = Quaternion.Euler(localEuler);
        instance.transform.localScale = localScale;
    }

    private static void StopAndClearParticles(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles[i].Clear(true);
        }
    }

    private static void RestartSpawnedVfx(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];
            if (particle == null)
            {
                continue;
            }

            particle.Clear(true);
            particle.Play(true);
        }

        AudioSource[] audioSources = instance.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < audioSources.Length; i++)
        {
            AudioSource audioSource = audioSources[i];
            if (audioSource != null && audioSource.enabled && audioSource.gameObject.activeInHierarchy)
            {
                audioSource.Stop();
                audioSource.Play();
            }
        }
    }

    private static bool IsDamageableTarget(Collider2D hit)
    {
        if (PlayerHitResolver.IsThreatSensorCollider(hit)) return false;
        if (hit.GetComponentInParent<IEnemyTarget>() != null) return true;
        if (hit.GetComponentInParent<Bao.Enemies.BaoEnemyBase>() != null) return true;
        if (hit.GetComponentInParent<Bao.Enemies.IDamageable>() != null) return true;
        if (hit.GetComponentInParent<EnemySystem.IDamageable>() != null) return true;
        if (hit.GetComponentInParent<Bao.Bosses.DreadwormBoss>() != null) return true;
        if (hit.GetComponentInParent<Bao.Bosses.NidusCocoonBoss>() != null) return true;
        if (hit.GetComponentInParent<InazumaCubeHit>() != null) return true;
        return false;
    }

    private static int ResolveTargetId(Collider2D hit)
    {
        if (hit == null)
        {
            return 0;
        }

        if (PlayerHitResolver.IsThreatSensorCollider(hit))
        {
            return 0;
        }

        IEnemyTarget enemyTarget = hit.GetComponentInParent<IEnemyTarget>();
        if (enemyTarget != null)
        {
            return enemyTarget.Transform.GetInstanceID();
        }

        Transform root = hit.transform.root;
        return root != null ? root.GetInstanceID() : hit.GetInstanceID();
    }

    private void OnDisable()
    {
        if (running)
        {
            Cleanup(0f, restoreInput: true);
        }
    }

    private void OnDestroy()
    {
        if (running)
        {
            Cleanup(0f, restoreInput: false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (spearHitPoint == null)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.25f, 0f, 0.8f);
        Gizmos.DrawWireCube(spearHitPoint.position, Vector3.one * 0.35f);
    }
}

public sealed class BurnStatusPayload
{
    public BurnStatusPayload(float damagePerTick, float duration, Player source)
    {
        DamagePerTick = damagePerTick;
        Duration = duration;
        Source = source;
    }

    public float DamagePerTick { get; }
    public float Duration { get; }
    public Player Source { get; }
}
