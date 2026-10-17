using System.Collections;
using MagicArsenal;
using UnityEngine;

public sealed class FairyBeamSkillRunner : MonoBehaviour
{
    private Coroutine routine;
    private GameObject activeBeamObject;
    private FairyPlayerCompanionController lockedCompanion;

    public void Begin(
        Player owner,
        FairyElementVfxController fairy,
        SkillData skill,
        FairyBeamSkillConfig config,
        Vector3 origin,
        Vector2 direction,
        Collider2D target,
        LayerMask targetLayer)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
        }

        DestroyActiveBeam();
        routine = StartCoroutine(Run(owner, fairy, skill, config, origin, direction, target, targetLayer));
    }

    private IEnumerator Run(
        Player owner,
        FairyElementVfxController fairy,
        SkillData skill,
        FairyBeamSkillConfig config,
        Vector3 origin,
        Vector2 direction,
        Collider2D target,
        LayerMask targetLayer)
    {
        LockFairy(fairy, true);
        Vector3 lockedPosition = fairy != null ? fairy.transform.position : transform.position;
        float animationLength = PlayFairyAnimation(fairy, config);
        float castDelay = Mathf.Clamp(config.CastDelay, 0f, animationLength);
        float castTimer = 0f;
        while (castTimer < castDelay)
        {
            KeepFairyAtLockedPosition(fairy, lockedPosition);
            castTimer += Time.deltaTime;
            yield return null;
        }

        if (!FairyBeamSkillExecutor.IsValidEnemyTarget(target))
        {
            Finish();
            yield break;
        }

        float beamDuration = Mathf.Max(0.05f, animationLength - castDelay);
        if (config.Duration > 0f)
        {
            beamDuration = Mathf.Min(beamDuration, config.Duration);
        }

        SpawnBeam(owner, skill, config, origin, direction, target, targetLayer, beamDuration);
        float timer = 0f;
        while (timer < beamDuration && FairyBeamSkillExecutor.IsValidEnemyTarget(target))
        {
            KeepFairyAtLockedPosition(fairy, lockedPosition);
            FaceTarget(fairy, target, origin);
            timer += Time.deltaTime;
            yield return null;
        }

        Finish();
    }

    private static float PlayFairyAnimation(FairyElementVfxController fairy, FairyBeamSkillConfig config)
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

    private void SpawnBeam(
        Player owner,
        SkillData skill,
        FairyBeamSkillConfig config,
        Vector3 origin,
        Vector2 direction,
        Collider2D target,
        LayerMask targetLayer,
        float beamDuration)
    {
        if (config.BeamPrefab == null)
        {
            return;
        }

        activeBeamObject = Instantiate(config.BeamPrefab, origin, config.BeamPrefab.transform.rotation);
        MagicBeamScript beam = activeBeamObject.GetComponent<MagicBeamScript>();
        if (beam == null)
        {
            beam = activeBeamObject.GetComponentInChildren<MagicBeamScript>(true);
        }

        if (beam == null)
        {
            DestroyActiveBeam();
            Debug.LogWarning($"Fairy beam skill '{skill.SkillName}' needs a MagicBeamScript on the beam prefab.");
            return;
        }

        beam.FireFrom(
            owner,
            origin,
            direction,
            beamDuration,
            Mathf.Max(0.1f, config.MaxDistance),
            Mathf.Max(1, config.DamagePerTick),
            Mathf.Max(0.01f, config.DamageTickInterval),
            target,
            targetLayer);
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        Finish();
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

    private void FaceTarget(FairyElementVfxController fairy, Collider2D target, Vector3 fallbackOrigin)
    {
        if (target == null)
        {
            return;
        }

        FairyPlayerCompanionController companion = fairy != null
            ? fairy.GetComponent<FairyPlayerCompanionController>()
            : lockedCompanion;
        if (companion == null)
        {
            return;
        }

        Vector3 origin = fairy != null && fairy.BeamOrigin != null ? fairy.BeamOrigin.position : fallbackOrigin;
        Vector2 direction = (Vector2)target.bounds.center - (Vector2)origin;
        companion.FaceDirection(direction);
    }

    private static void KeepFairyAtLockedPosition(FairyElementVfxController fairy, Vector3 lockedPosition)
    {
        if (fairy != null)
        {
            fairy.transform.position = lockedPosition;
        }
    }

    private void Finish()
    {
        DestroyActiveBeam();
        LockFairy(null, false);
        routine = null;
    }

    private void DestroyActiveBeam()
    {
        if (activeBeamObject == null)
        {
            return;
        }

        Destroy(activeBeamObject);
        activeBeamObject = null;
    }
}
