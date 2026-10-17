using System.Collections;
using UnityEngine;

public sealed class EarthSpraySkillRunner : MonoBehaviour
{
    private Coroutine routine;
    private GameObject activeSprayObject;
    private FairyPlayerCompanionController lockedCompanion;
    private Player activeOwner;

    public void Begin(
        Player owner,
        FairyElementVfxController fairy,
        SkillData skill,
        EarthSpraySkillConfig config,
        Vector3 origin,
        Vector2 direction,
        LayerMask targetLayer)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            Finish();
        }

        activeOwner = owner;
        activeOwner?.ShowSkillPresentation(skill, triggerSkillCollider: false);
        routine = StartCoroutine(Run(owner, fairy, config, origin, direction.normalized, targetLayer));
    }

    private IEnumerator Run(
        Player owner,
        FairyElementVfxController fairy,
        EarthSpraySkillConfig config,
        Vector3 origin,
        Vector2 direction,
        LayerMask targetLayer)
    {
        LockFairy(fairy, true);
        Vector3 lockedPosition = fairy != null ? fairy.transform.position : transform.position;
        FaceDirection(fairy, direction);

        float animationLength = PlayFairyAnimation(fairy, config);
        float castDelay = Mathf.Clamp(config.CastDelay, 0f, animationLength);
        float castTimer = 0f;
        while (castTimer < castDelay)
        {
            KeepFairyAtLockedPosition(fairy, lockedPosition);
            castTimer += Time.deltaTime;
            yield return null;
        }

        SpawnSpray(config, origin, direction);

        float duration = Mathf.Max(0.05f, config.Duration);
        float damageInterval = Mathf.Max(0.01f, config.DamageTickInterval);
        float timer = 0f;
        float damageTimer = 0f;
        while (timer < duration)
        {
            if (damageTimer <= 0f)
            {
                ApplySprayDamage(owner, config, origin, direction, targetLayer);
                damageTimer = damageInterval;
            }

            KeepFairyAtLockedPosition(fairy, lockedPosition);
            timer += Time.deltaTime;
            damageTimer -= Time.deltaTime;
            yield return null;
        }

        Finish();
    }

    private void SpawnSpray(EarthSpraySkillConfig config, Vector3 origin, Vector2 direction)
    {
        if (config.SprayPrefab == null)
        {
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle) *
                              Quaternion.Euler(config.SprayLocalEuler) *
                              config.SprayPrefab.transform.rotation;
        activeSprayObject = Instantiate(config.SprayPrefab, origin, rotation);
        Destroy(activeSprayObject, Mathf.Max(0.05f, config.Duration + config.VfxLifetimePadding));
    }

    private static float PlayFairyAnimation(FairyElementVfxController fairy, EarthSpraySkillConfig config)
    {
        float fallbackDuration = Mathf.Max(config.CastDelay + 0.05f, config.AnimationDurationFallback);
        if (fairy == null || string.IsNullOrEmpty(config.AnimationName))
        {
            return fallbackDuration;
        }

        Animator animator = fairy.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            return fallbackDuration;
        }

        animator.CrossFadeInFixedTime(config.AnimationName, 0.05f, 0, 0f);
        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        if (controller == null)
        {
            return fallbackDuration;
        }

        AnimationClip[] clips = controller.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip != null && clip.name == config.AnimationName)
            {
                return Mathf.Max(config.CastDelay + 0.05f, clip.length);
            }
        }

        return fallbackDuration;
    }

    private static void ApplySprayDamage(
        Player owner,
        EarthSpraySkillConfig config,
        Vector3 origin,
        Vector2 direction,
        LayerMask targetLayer)
    {
        float maxDistance = Mathf.Max(0.1f, config.MaxDistance);
        float radius = Mathf.Max(0.1f, config.SprayRadius);
        Vector2 center = (Vector2)origin + direction * (maxDistance * 0.5f);
        float searchRadius = (maxDistance * 0.5f) + radius;
        Collider2D[] hits = targetLayer.value != 0
            ? Physics2D.OverlapCircleAll(center, searchRadius, targetLayer)
            : Physics2D.OverlapCircleAll(center, searchRadius);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (FairyBeamSkillExecutor.IsSelfCollider(hit, owner) ||
                !FairyBeamSkillExecutor.IsValidEnemyTarget(hit))
            {
                continue;
            }

            Vector2 toTarget = (Vector2)hit.bounds.center - (Vector2)origin;
            float forwardDistance = Vector2.Dot(toTarget, direction);
            if (forwardDistance < 0f || forwardDistance > maxDistance)
            {
                continue;
            }

            Vector2 closestPoint = (Vector2)origin + direction * forwardDistance;
            if (((Vector2)hit.bounds.center - closestPoint).sqrMagnitude > radius * radius)
            {
                continue;
            }

            PlayerHitResolver.TryApplyDamageToEnemy(hit, Mathf.Max(1, config.DamagePerTick), owner != null ? owner.transform : null);
        }
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
        }

        Finish();
    }

    private void Finish()
    {
        if (activeSprayObject != null)
        {
            Destroy(activeSprayObject);
            activeSprayObject = null;
        }

        LockFairy(null, false);
        activeOwner?.HideSkillPresentation();
        activeOwner = null;
        routine = null;
    }

    private void LockFairy(FairyElementVfxController fairy, bool locked)
    {
        FairyPlayerCompanionController companion = fairy != null
            ? fairy.GetComponent<FairyPlayerCompanionController>()
            : null;

        if (locked)
        {
            lockedCompanion = companion;
            lockedCompanion?.SetCastingLocked(true);
            return;
        }

        lockedCompanion?.SetCastingLocked(false);
        lockedCompanion = null;
    }

    private void FaceDirection(FairyElementVfxController fairy, Vector2 direction)
    {
        FairyPlayerCompanionController companion = fairy != null
            ? fairy.GetComponent<FairyPlayerCompanionController>()
            : lockedCompanion;
        companion?.FaceDirection(direction);
    }

    private static void KeepFairyAtLockedPosition(FairyElementVfxController fairy, Vector3 lockedPosition)
    {
        if (fairy != null)
        {
            fairy.transform.position = lockedPosition;
        }
    }
}
