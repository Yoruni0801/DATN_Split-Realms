using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class FireSpearBarrageSkillRunner : MonoBehaviour
{
    private readonly Collider2D[] hitBuffer = new Collider2D[16];
    private readonly HashSet<Transform> hitRootsThisTick = new HashSet<Transform>();
    private readonly HashSet<Transform> hitRootsThisCast = new HashSet<Transform>();

    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Transform spearHitPoint;

    private Player owner;
    private PlayerHealth ownerHealth;
    private Coroutine routine;
    private FireSpearBarrageSkillConfig activeConfig;
    private Vector2 currentAimDirection = Vector2.right;
    private Vector2 currentHitboxCenter;
    private float currentHitboxAngle;
    private bool running;

    public bool IsRunning => running;

    private void Awake()
    {
        CacheReferences();
    }

    public bool TryStartSkill(SkillData skill, FireSpearBarrageSkillConfig config, SkillCastContext context)
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

        routine = StartCoroutine(SkillRoutine(skill, config, context));
        return true;
    }

    private IEnumerator SkillRoutine(SkillData skill, FireSpearBarrageSkillConfig config, SkillCastContext context)
    {
        running = true;
        activeConfig = config;
        hitRootsThisCast.Clear();

        Vector2 direction = ResolveFacingDirection();
        owner.SetInputActive(false);
        owner.ShowSkillPresentation(skill, triggerSkillCollider: false);
        bool useSkillColliderDamage = skill.SkillColliderEnabled && skill.ColliderDealsDamage;
        int spawnedBeamCount = 0;
        float nextBeamTime = Mathf.Max(0.01f, config.BeamInterval);
        currentAimDirection = direction;
        SpawnBeamVfx(config);
        spawnedBeamCount++;

        if (config.Startup > 0f)
        {
            yield return new WaitForSeconds(config.Startup);
        }

        float elapsed = 0f;
        float nextHitTime = 0f;
        while (elapsed < config.Duration)
        {
            if (ownerHealth != null && ownerHealth.IsDead)
            {
                Cleanup(config.VfxStopDelay, restoreInput: true);
                yield break;
            }

            StopHorizontalMovement();
            if (spawnedBeamCount < config.BeamCount && elapsed >= nextBeamTime)
            {
                direction = ResolveFacingDirection();
                currentAimDirection = direction;
                SpawnBeamVfx(config);
                spawnedBeamCount++;
                nextBeamTime += Mathf.Max(0.01f, config.BeamInterval);
            }

            if (elapsed >= nextHitTime)
            {
                if (useSkillColliderDamage)
                {
                    owner.TriggerSkillCollider(skill);
                }
                else
                {
                    direction = ResolveFacingDirection();
                    currentAimDirection = ResolveAimDirection(config, direction);
                    ProcessHitTick(skill, config, currentAimDirection);
                }

                nextHitTime += Mathf.Max(0.01f, config.HitInterval);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        routine = null;
        Cleanup(config.VfxStopDelay, restoreInput: true);
    }

    private void ProcessHitTick(SkillData skill, FireSpearBarrageSkillConfig config, Vector2 aimDirection)
    {
        if (spearHitPoint == null)
        {
            return;
        }

        hitRootsThisTick.Clear();
        Vector2 center = ResolveHitboxCenter(config, aimDirection);
        float angle = ResolveAimAngle(aimDirection);
        currentHitboxCenter = center;
        currentHitboxAngle = angle;
        ContactFilter2D targetFilter = new ContactFilter2D();
        targetFilter.SetLayerMask(PlayerLayerConfig.AttackTargetLayer);
        targetFilter.useTriggers = Physics2D.queriesHitTriggers;
        int count = Physics2D.OverlapBox(center, ResolveHitboxSize(config), angle, targetFilter, hitBuffer);
        int hitCount = 0;
        int damage = Mathf.RoundToInt(skill.Damage * Mathf.Max(0f, config.DamageMultiplier));
        for (int i = 0; i < count; i++)
        {
            if (hitCount >= config.MaximumTargetsPerTick)
            {
                break;
            }

            Collider2D hit = hitBuffer[i];
            if (hit == null || hit.transform.root == transform.root)
            {
                continue;
            }

            Transform root = hit.transform.root;
            if (root != null && hitRootsThisTick.Contains(root))
            {
                continue;
            }

            if (config.PreventRepeatedTargetPerCast && root != null && hitRootsThisCast.Contains(root))
            {
                continue;
            }

            if (!PlayerHitResolver.TryApplyDamageToEnemy(hit, damage, owner != null ? owner.transform : null))
            {
                continue;
            }

            if (root != null)
            {
                hitRootsThisTick.Add(root);
                hitRootsThisCast.Add(root);
            }

            hitCount++;
            if (CombatFeedbackManager.Instance != null)
            {
                CombatFeedbackManager.Instance.PlayHitFeedback(hit.ClosestPoint(spearHitPoint.position), false);
            }
        }
    }

    private void SpawnBeamVfx(FireSpearBarrageSkillConfig config)
    {
        if (config.AttackVfxPrefab == null || spearHitPoint == null)
        {
            return;
        }

        GameObject beam = EffectPool.Spawn(config.AttackVfxPrefab, spearHitPoint.position, spearHitPoint.rotation);
        if (beam == null)
        {
            return;
        }

        AttachLocal(
            beam,
            spearHitPoint,
            config.AttackVfxLocalPosition,
            config.AttackVfxLocalEuler,
            ResolveBeamLocalScale(config));
        if (!config.ParentBeamToSpear)
        {
            beam.transform.SetParent(null, true);
        }

        RestartSpawnedVfx(beam);
        float lifetime = config.BeamVfxLifetime > 0f ? config.BeamVfxLifetime : config.VfxStopDelay;
        if (lifetime > 0f)
        {
            StartCoroutine(ReleaseAfterDelay(beam, lifetime));
            return;
        }

        EffectPool.Release(beam);
    }

    private Vector2 ResolveFacingDirection()
    {
        if (owner != null)
        {
            return owner.FacingDirectionSign < 0f ? Vector2.left : Vector2.right;
        }

        return transform.localScale.x < 0f ? Vector2.right : Vector2.left;
    }

    private Vector3 ResolveBeamLocalScale(FireSpearBarrageSkillConfig config)
    {
        Vector3 scale = config.AttackVfxLocalScale;
        if (!config.MirrorBeamVfxWithPlayerFacing)
        {
            return scale;
        }

        float facingSign = owner != null ? owner.FacingDirectionSign : (transform.localScale.x < 0f ? 1f : -1f);
        float absoluteX = Mathf.Abs(scale.x);
        scale.x = facingSign >= 0f ? absoluteX : -absoluteX;
        return scale;
    }

    private Vector2 ResolveAimDirection(FireSpearBarrageSkillConfig config, Vector2 fallbackDirection)
    {
        if (spearHitPoint == null)
        {
            return fallbackDirection.sqrMagnitude > 0.0001f ? fallbackDirection.normalized : Vector2.right;
        }

        Vector2 baseDirection = fallbackDirection.sqrMagnitude > 0.0001f ? fallbackDirection.normalized : Vector2.right;
        Vector2 center = ResolveHitboxCenter(config, baseDirection);
        float angle = ResolveAimAngle(baseDirection);
        ContactFilter2D targetFilter = new ContactFilter2D();
        targetFilter.SetLayerMask(PlayerLayerConfig.AttackTargetLayer);
        targetFilter.useTriggers = Physics2D.queriesHitTriggers;
        int count = Physics2D.OverlapBox(center, ResolveHitboxSize(config), angle, targetFilter, hitBuffer);
        Collider2D bestTarget = null;
        float bestDistance = float.MaxValue;
        Vector2 origin = spearHitPoint.position;

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = hitBuffer[i];
            if (hit == null || hit.transform.root == transform.root)
            {
                continue;
            }

            Vector2 targetCenter = hit.bounds.center;
            float distance = Vector2.SqrMagnitude(targetCenter - origin);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestTarget = hit;
            bestDistance = distance;
        }

        if (bestTarget == null)
        {
            currentHitboxCenter = center;
            currentHitboxAngle = angle;
            return baseDirection;
        }

        Vector2 aimDirection = (Vector2)bestTarget.bounds.center - origin;
        if (aimDirection.sqrMagnitude <= 0.0001f)
        {
            return baseDirection;
        }

        aimDirection.Normalize();
        currentHitboxCenter = ResolveHitboxCenter(config, aimDirection);
        currentHitboxAngle = ResolveAimAngle(aimDirection);
        return aimDirection;
    }

    private Vector2 ResolveHitboxCenter(FireSpearBarrageSkillConfig config, Vector2 aimDirection)
    {
        Vector2 direction = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : Vector2.right;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        Vector2 origin = spearHitPoint != null ? (Vector2)spearHitPoint.position : (Vector2)transform.position;
        Vector2 offset = ScaleOffset(config.HitboxOffset);
        return origin + direction * offset.x + perpendicular * offset.y;
    }

    private Vector2 ResolveHitboxSize(FireSpearBarrageSkillConfig config)
    {
        return ScaleOffset(config.HitboxSize);
    }

    private Vector2 ScaleOffset(Vector2 offset)
    {
        if (owner == null || owner.ScaleProvider == null)
        {
            return offset;
        }

        Vector3 scaledOffset = owner.ScaleProvider.ScaleOffset(offset);
        return new Vector2(scaledOffset.x, scaledOffset.y);
    }

    private static float ResolveAimAngle(Vector2 aimDirection)
    {
        Vector2 direction = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : Vector2.right;
        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }

    private void StopHorizontalMovement()
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
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

        StopHorizontalMovement();
        hitRootsThisCast.Clear();
        hitRootsThisTick.Clear();

        if (restoreInput && owner != null)
        {
            owner.SetInputActive(true);
            owner.HideSkillPresentation();
        }

        running = false;
    }

    private IEnumerator ReleaseAfterDelay(GameObject instance, float delay)
    {
        int version = EffectPool.GetVersion(instance);
        yield return new WaitForSeconds(delay);
        StopAndClearParticles(instance);
        EffectPool.Release(instance, version);
    }

    private void CacheReferences()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
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
            particles[i].Clear(true);
            particles[i].Play(true);
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

}
