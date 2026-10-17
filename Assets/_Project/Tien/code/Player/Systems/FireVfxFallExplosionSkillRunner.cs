using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class FireVfxFallExplosionSkillRunner : MonoBehaviour
{
    private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
    private readonly HashSet<Transform> damagedRoots = new HashSet<Transform>();
    private readonly Collider2D[] explosionHitBuffer = new Collider2D[64];

    private Player owner;
    private PlayerHealth ownerHealth;
    private Rigidbody2D rb;
    private Coroutine routine;
    private GameObject activeVfx;
    private FireVfxFallExplosionSkillConfig activeConfig;
    private float originalGravityScale;
    private bool running;

    public bool IsRunning => running;

    private void Awake()
    {
        CacheReferences();
    }

    public bool TryStartSkill(SkillData skill, FireVfxFallExplosionSkillConfig config, SkillCastContext context)
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
            Debug.LogWarning("[FireVfxFallExplosionSkillRunner] Missing Rigidbody2D reference.", this);
            return false;
        }

        routine = StartCoroutine(SkillRoutine(skill, config));
        return true;
    }

    private IEnumerator SkillRoutine(SkillData skill, FireVfxFallExplosionSkillConfig config)
    {
        running = true;
        activeConfig = config;
        originalGravityScale = rb.gravityScale;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

        if (config.HidePlayerRenderers)
        {
            SetPlayerRenderersVisible(false);
        }

        SpawnActiveVfx(config);

        if (config.DisableGravityWhileActive)
        {
            rb.gravityScale = config.ActiveGravityScale;
        }

        float startY = rb.position.y;
        float apexY = startY + config.AscendHeight;
        yield return MovePlayerY(startY, apexY, config.AscendDuration, config);
        if (!running)
        {
            yield break;
        }

        yield return HoverAtHeight(apexY, config.HoverDuration, config);
        if (!running)
        {
            yield break;
        }

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

        Vector2 target = ResolveGroundPosition(config);
        yield return MovePlayerY(rb.position.y, target.y, config.DropDuration, config);
        if (!running || (ownerHealth != null && ownerHealth.IsDead))
        {
            Cleanup(config.ActiveVfxStopDelay, restoreInput: true);
            yield break;
        }

        rb.MovePosition(new Vector2(rb.position.x, target.y));
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

        if (config.ExplosionDelay > 0f)
        {
            yield return new WaitForSeconds(config.ExplosionDelay);
        }

        Vector2 explosionCenter = new Vector2(rb.position.x, target.y);
        SpawnExplosionVfx(config, explosionCenter);
        DealExplosionDamage(skill, config, explosionCenter);

        routine = null;
        Cleanup(config.ActiveVfxStopDelay, restoreInput: true);
    }

    private IEnumerator HoverAtHeight(float yPosition, float duration, FireVfxFallExplosionSkillConfig config)
    {
        if (duration <= 0f)
        {
            yield break;
        }

        float hoverEndTime = Time.time + duration;
        while (Time.time < hoverEndTime)
        {
            if (ownerHealth != null && ownerHealth.IsDead)
            {
                Cleanup(config.ActiveVfxStopDelay, restoreInput: true);
                yield break;
            }

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            rb.MovePosition(new Vector2(rb.position.x, yPosition));
            yield return null;
        }
    }

    private IEnumerator MovePlayerY(float startY, float targetY, float duration, FireVfxFallExplosionSkillConfig config)
    {
        float elapsed = 0f;
        float moveDuration = Mathf.Max(0f, duration);
        if (moveDuration <= 0f)
        {
            rb.MovePosition(new Vector2(rb.position.x, targetY));
            yield break;
        }

        while (elapsed < moveDuration)
        {
            if (ownerHealth != null && ownerHealth.IsDead)
            {
                Cleanup(config.ActiveVfxStopDelay, restoreInput: true);
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            float nextY = Mathf.Lerp(startY, targetY, t);
            rb.MovePosition(new Vector2(rb.position.x, nextY));
            yield return null;
        }

        rb.MovePosition(new Vector2(rb.position.x, targetY));
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
    }

    private Vector2 ResolveGroundPosition(FireVfxFallExplosionSkillConfig config)
    {
        RaycastHit2D hit = Physics2D.Raycast(rb.position, Vector2.down, config.GroundSearchDistance, PlayerLayerConfig.EnvironmentLayer);
        if (hit.collider == null)
        {
            return rb.position;
        }

        return hit.point + Vector2.up * config.GroundOffset;
    }

    private void DealExplosionDamage(SkillData skill, FireVfxFallExplosionSkillConfig config, Vector2 center)
    {
        damagedRoots.Clear();
        int damage = Mathf.RoundToInt(skill.Damage * Mathf.Max(0f, config.DamageMultiplier));
        GameObject colliderObject = new GameObject("Fire VFX Fall Explosion Damage Collider");
        colliderObject.transform.position = center;

        CircleCollider2D explosionCollider = colliderObject.AddComponent<CircleCollider2D>();
        explosionCollider.isTrigger = true;
        explosionCollider.radius = config.ExplosionRadius;

        ContactFilter2D targetFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = PlayerLayerConfig.AttackTargetLayer,
            useTriggers = true
        };

        Physics2D.SyncTransforms();
        int count = explosionCollider.Overlap(targetFilter, explosionHitBuffer);
        int damagedCount = 0;
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = explosionHitBuffer[i];
            if (hit == null || hit.transform.root == transform.root)
            {
                continue;
            }

            Transform root = hit.transform.root;
            if (root != null && damagedRoots.Contains(root))
            {
                continue;
            }

            if (PlayerHitResolver.TryApplyDamageToEnemy(hit, damage, owner != null ? owner.transform : null) && root != null)
            {
                damagedRoots.Add(root);
                damagedCount++;
            }

            explosionHitBuffer[i] = null;
        }

        if (damagedCount == 0)
        {
            int fallbackCount = Physics2D.OverlapCircle(center, config.ExplosionRadius, targetFilter, explosionHitBuffer);
            for (int i = 0; i < fallbackCount; i++)
            {
                Collider2D hit = explosionHitBuffer[i];
                if (hit == null || hit.transform.root == transform.root)
                {
                    continue;
                }

                Transform root = hit.transform.root;
                if (root != null && damagedRoots.Contains(root))
                {
                    continue;
                }

                if (PlayerHitResolver.TryApplyDamageToEnemy(hit, damage, owner != null ? owner.transform : null) && root != null)
                {
                    damagedRoots.Add(root);
                    damagedCount++;
                }

                explosionHitBuffer[i] = null;
            }
        }

        if (damagedCount == 0)
        {
            ContactFilter2D unfilteredTargetFilter = new ContactFilter2D
            {
                useLayerMask = false,
                useTriggers = true
            };

            int unfilteredCount = explosionCollider.Overlap(unfilteredTargetFilter, explosionHitBuffer);
            for (int i = 0; i < unfilteredCount; i++)
            {
                Collider2D hit = explosionHitBuffer[i];
                if (hit == null || hit.transform.root == transform.root)
                {
                    continue;
                }

                Transform root = hit.transform.root;
                if (root != null && damagedRoots.Contains(root))
                {
                    continue;
                }

                if (PlayerHitResolver.TryApplyDamageToEnemy(hit, damage, owner != null ? owner.transform : null) && root != null)
                {
                    damagedRoots.Add(root);
                }

                explosionHitBuffer[i] = null;
            }
        }

        if (config.ExplosionColliderLifetime > 0f && gameObject.activeInHierarchy)
        {
            StartCoroutine(DestroyColliderAfterDelay(colliderObject, config.ExplosionColliderLifetime));
            return;
        }

        Destroy(colliderObject);
    }

    private void SpawnActiveVfx(FireVfxFallExplosionSkillConfig config)
    {
        if (config.ActiveVfxPrefab == null)
        {
            return;
        }

        activeVfx = EffectPool.Spawn(config.ActiveVfxPrefab, transform.position, transform.rotation);
        if (activeVfx == null)
        {
            return;
        }

        activeVfx.transform.SetParent(transform, false);
        activeVfx.transform.localPosition = config.ActiveVfxLocalPosition;
        activeVfx.transform.localRotation = Quaternion.Euler(config.ActiveVfxLocalEuler);
        activeVfx.transform.localScale = config.ActiveVfxLocalScale;
        RestartSpawnedVfx(activeVfx);
    }

    private void SpawnExplosionVfx(FireVfxFallExplosionSkillConfig config, Vector3 position)
    {
        if (config.ExplosionVfxPrefab == null)
        {
            return;
        }

        GameObject explosion = EffectPool.Spawn(
            config.ExplosionVfxPrefab,
            position,
            Quaternion.Euler(config.ExplosionVfxLocalEuler),
            config.ExplosionVfxLocalScale);
        RestartSpawnedVfx(explosion);
        if (explosion != null && config.ExplosionVfxLifetime > 0f)
        {
            StartCoroutine(ReleaseAfterDelay(explosion, config.ExplosionVfxLifetime));
        }
    }

    private void Cleanup(float activeVfxStopDelay, bool restoreInput)
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

        rb.gravityScale = originalGravityScale;
        rb.linearVelocity = Vector2.zero;
        SetPlayerRenderersVisible(true);
        ReleaseActiveVfx(activeVfxStopDelay);

        if (restoreInput && owner != null)
        {
            owner.HideSkillPresentation();
        }

        running = false;
    }

    private void ReleaseActiveVfx(float delay)
    {
        if (activeVfx == null)
        {
            return;
        }

        GameObject instance = activeVfx;
        activeVfx = null;
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

    private IEnumerator DestroyColliderAfterDelay(GameObject colliderObject, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (colliderObject != null)
        {
            Destroy(colliderObject);
        }
    }

    private void SetPlayerRenderersVisible(bool visible)
    {
        if (!visible)
        {
            hiddenRenderers.Clear();
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null && renderer.enabled)
                {
                    renderer.enabled = false;
                    hiddenRenderers.Add(renderer);
                }
            }

            return;
        }

        for (int i = 0; i < hiddenRenderers.Count; i++)
        {
            if (hiddenRenderers[i] != null)
            {
                hiddenRenderers[i].enabled = true;
            }
        }

        hiddenRenderers.Clear();
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
