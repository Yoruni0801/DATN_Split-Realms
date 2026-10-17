using System.Reflection;
using Bao.Enemies;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

public class TimelineEnemyNpcAttackBehaviour : PlayableBehaviour
{
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public Transform enemyOverride;
    public Transform npcTarget;
    public TimelineEnemyNpcAttackType attackType;
    public float impactNormalizedTime;
    public int damage;
    public bool damageNpc;
    public bool faceTarget;
    public bool triggerAnimator;
    public string animatorTrigger;
    public bool stopEnemyVelocity;
    public UnityEvent onImpact;

    private Transform boundEnemy;
    private Transform boundNpcTarget;
    private BaoEnemyBase controlledEnemy;
    private TimelineEnemyNpcAttackType activeAttackType;
    private bool warnedMissingEnemy;
    private bool warnedMissingNpc;
    private bool entered;
    private bool impacted;
    private bool impactAnimationTriggered;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        TimelineSeekContext.Evaluate(playable, info);

        Transform enemy = ResolveEnemy(playerData);
        Transform resolvedNpcTarget = ResolveNpcTarget(playerData, enemy);
        if (enemy == null)
        {
            WarnMissingBinding(ref warnedMissingEnemy, "Enemy", playerData);
            return;
        }

        if (resolvedNpcTarget == null)
        {
            WarnMissingBinding(ref warnedMissingNpc, "NPC target", playerData);
            return;
        }

        if (!entered || boundEnemy != enemy || boundNpcTarget != resolvedNpcTarget)
        {
            Enter(enemy, resolvedNpcTarget);
        }

        RefreshTimelineControl(resolvedNpcTarget);
        FaceTarget(enemy, resolvedNpcTarget);
        StopVelocity(enemy);

        double duration = playable.GetDuration();
        float normalizedTime = duration > 0d
            ? Mathf.Clamp01((float)(playable.GetTime() / duration))
            : 1f;

        // Crossing the impact time by dragging the playhead must not deal damage, spawn roots or VFX, or raise
        // the impact event. The one shot flags are only resynced to the new playhead so normal playback from
        // before the threshold still fires exactly once.
        if (TimelineSeekContext.IsSeeking)
        {
            impacted = normalizedTime >= impactNormalizedTime;
            impactAnimationTriggered = activeAttackType == TimelineEnemyNpcAttackType.HuskfangRoot
                && normalizedTime >= Mathf.Max(0f, impactNormalizedTime - 0.1f);
            return;
        }

        if (!impactAnimationTriggered
            && activeAttackType == TimelineEnemyNpcAttackType.HuskfangRoot
            && normalizedTime >= Mathf.Max(0f, impactNormalizedTime - 0.1f))
        {
            TriggerHuskfangStomp(enemy);
        }

        if (!impacted && normalizedTime >= impactNormalizedTime)
        {
            Impact(enemy, resolvedNpcTarget);
        }
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        ReleaseTimelineControl();
        entered = false;
        impacted = false;
        impactAnimationTriggered = false;
        boundEnemy = null;
        boundNpcTarget = null;
    }

    public override void OnGraphStop(Playable playable)
    {
        ReleaseTimelineControl();
        entered = false;
        impacted = false;
        impactAnimationTriggered = false;
        boundEnemy = null;
        boundNpcTarget = null;
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        ReleaseTimelineControl();
    }

    private void Enter(Transform enemy, Transform resolvedNpcTarget)
    {
        if (controlledEnemy != null && (boundEnemy != enemy || boundNpcTarget != resolvedNpcTarget))
        {
            ReleaseTimelineControl();
        }

        entered = true;
        impacted = false;
        impactAnimationTriggered = false;
        boundEnemy = enemy;
        boundNpcTarget = resolvedNpcTarget;
        controlledEnemy = enemy.GetComponentInParent<BaoEnemyBase>();
        activeAttackType = ResolveAttackType(enemy);

        if (controlledEnemy != null)
        {
            controlledEnemy.BeginTimelineControl(resolvedNpcTarget, stopEnemyVelocity);
        }

        FaceTarget(enemy, resolvedNpcTarget);
        StopVelocity(enemy);
        BeginAttackAnimation(enemy);
    }

    private void Impact(Transform enemy, Transform resolvedNpcTarget)
    {
        impacted = true;

        switch (activeAttackType)
        {
            case TimelineEnemyNpcAttackType.RiftcoreAoe:
                PlayRiftcoreImpact(enemy);
                break;

            case TimelineEnemyNpcAttackType.HuskfangRoot:
                PlayHuskfangImpact(enemy, resolvedNpcTarget);
                break;
        }

        if (damageNpc && damage > 0)
        {
            ApplyDamageToNpc(enemy, resolvedNpcTarget);
        }

        onImpact?.Invoke();
    }

    private Transform ResolveEnemy(object playerData)
    {
        if (enemyOverride != null)
        {
            return enemyOverride;
        }

        Transform bindingTransform = ResolveTransform(playerData);
        if (bindingTransform != null && bindingTransform.GetComponentInParent<BaoEnemyBase>() != null)
        {
            return bindingTransform;
        }

        return null;
    }

    private Transform ResolveNpcTarget(object playerData, Transform enemy)
    {
        if (npcTarget != null)
        {
            return npcTarget;
        }

        Transform bindingTransform = ResolveTransform(playerData);
        if (bindingTransform == null || bindingTransform == enemy)
        {
            return null;
        }

        if (bindingTransform.GetComponentInParent<BaoEnemyBase>() != null)
        {
            return null;
        }

        return bindingTransform;
    }

    private Transform ResolveTransform(object playerData)
    {
        if (playerData is Transform transformBinding)
        {
            return transformBinding;
        }

        if (playerData is GameObject gameObjectBinding)
        {
            return gameObjectBinding.transform;
        }

        return playerData is Component componentBinding ? componentBinding.transform : null;
    }

    private TimelineEnemyNpcAttackType ResolveAttackType(Transform enemy)
    {
        if (attackType != TimelineEnemyNpcAttackType.Auto)
        {
            return attackType;
        }

        if (enemy.GetComponentInParent<RiftcoreEnemy>() != null)
        {
            return TimelineEnemyNpcAttackType.RiftcoreAoe;
        }

        if (enemy.GetComponentInParent<HuskfangEnemy>() != null)
        {
            return TimelineEnemyNpcAttackType.HuskfangRoot;
        }

        return TimelineEnemyNpcAttackType.RiftcoreAoe;
    }

    private void FaceTarget(Transform enemy, Transform target)
    {
        if (!faceTarget || target == null)
        {
            return;
        }

        BaoEnemyBase baoEnemy = enemy.GetComponentInParent<BaoEnemyBase>();
        if (baoEnemy != null)
        {
            baoEnemy.FacePosition(target.position);
            return;
        }

        Vector3 scale = enemy.localScale;
        float direction = target.position.x >= enemy.position.x ? 1f : -1f;
        enemy.localScale = new Vector3(Mathf.Abs(scale.x) * direction, scale.y, scale.z);
    }

    private void StopVelocity(Transform enemy)
    {
        if (!stopEnemyVelocity)
        {
            return;
        }

        Rigidbody2D body = enemy.GetComponentInParent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        }
    }

    private void BeginAttackAnimation(Transform enemy)
    {
        Animator animator = enemy.GetComponentInParent<Animator>();
        if (animator == null)
        {
            return;
        }

        switch (activeAttackType)
        {
            case TimelineEnemyNpcAttackType.RiftcoreAoe:
                animator.SetTrigger("ChargePower");
                animator.SetBool("isCharging", true);
                return;

            case TimelineEnemyNpcAttackType.HuskfangRoot:
                HuskfangEnemy huskfang = enemy.GetComponentInParent<HuskfangEnemy>();
                if (huskfang != null)
                {
                    huskfang.ShowWarningNow();
                }
                return;
        }

        TriggerCustomAttackAnimation(animator);
    }

    private void TriggerCustomAttackAnimation(Animator animator)
    {
        if (!triggerAnimator || string.IsNullOrWhiteSpace(animatorTrigger))
        {
            return;
        }

        animator.SetTrigger(animatorTrigger);
    }

    private void PlayRiftcoreImpact(Transform enemy)
    {
        RiftcoreEnemy riftcore = enemy.GetComponentInParent<RiftcoreEnemy>();
        if (riftcore == null)
        {
            return;
        }

        float aoeRange = GetPrivateValue(riftcore, "aoeRange", riftcore.AoeRange);
        GameObject aoeEffectPrefab = GetPrivateField<GameObject>(riftcore, "aoeEffectPrefab");

        if (aoeEffectPrefab != null)
        {
            Object.Instantiate(aoeEffectPrefab, riftcore.transform.position + Vector3.left * (aoeRange / 2f), Quaternion.identity);
            Object.Instantiate(aoeEffectPrefab, riftcore.transform.position + Vector3.right * (aoeRange / 2f), Quaternion.identity);
        }

        RiftcoreVFXController vfx = riftcore.GetComponent<RiftcoreVFXController>();
        if (vfx != null)
        {
            vfx.TriggerExplosionFlash();
        }

        Animator animator = riftcore.GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetTrigger("ReleaseAttack");
        }
    }

    private void PlayHuskfangImpact(Transform enemy, Transform resolvedNpcTarget)
    {
        HuskfangEnemy huskfang = enemy.GetComponentInParent<HuskfangEnemy>();
        if (huskfang == null || resolvedNpcTarget == null)
        {
            return;
        }

        GameObject rootPrefab = GetPrivateField<GameObject>(huskfang, "rootPrefab");
        if (rootPrefab == null)
        {
            return;
        }

        Vector3 spawnPosition = GetTargetGroundPosition(resolvedNpcTarget);
        GameObject rootInstance = Object.Instantiate(rootPrefab, spawnPosition, Quaternion.identity);
        HuskfangRootAttack rootAttack = rootInstance.GetComponent<HuskfangRootAttack>();
        if (rootAttack != null)
        {
            rootAttack.Initialize(huskfang);
        }
    }

    private void TriggerHuskfangStomp(Transform enemy)
    {
        impactAnimationTriggered = true;

        HuskfangEnemy huskfang = enemy.GetComponentInParent<HuskfangEnemy>();
        if (huskfang != null)
        {
            huskfang.StopWarningRoutine();
        }

        Animator animator = enemy.GetComponentInParent<Animator>();
        if (animator != null)
        {
            animator.SetTrigger("Stomp");
        }
    }

    private void ApplyDamageToNpc(Transform enemy, Transform resolvedNpcTarget)
    {
        Vector3 direction = resolvedNpcTarget.position - enemy.position;
        NpcTimelineDamageContext context = new NpcTimelineDamageContext(damage, enemy, direction);

        TimelineNpcDamageReceiver receiver = resolvedNpcTarget.GetComponentInParent<TimelineNpcDamageReceiver>();
        if (receiver == null)
        {
            receiver = resolvedNpcTarget.gameObject.AddComponent<TimelineNpcDamageReceiver>();
        }

        if (receiver != null)
        {
            receiver.TakeDamage(context);
            return;
        }

        if (TimelineEnemyNpcAttackMessageUtility.TryInvoke(resolvedNpcTarget, "TakeDamage", context))
        {
            return;
        }

        if (TimelineEnemyNpcAttackMessageUtility.TryInvoke(resolvedNpcTarget, "TakeDamage", damage, enemy))
        {
            return;
        }

        if (TimelineEnemyNpcAttackMessageUtility.TryInvoke(resolvedNpcTarget, "TakeDamage", (float)damage, enemy))
        {
            return;
        }

        if (TimelineEnemyNpcAttackMessageUtility.TryInvoke(resolvedNpcTarget, "TakeDamage", damage))
        {
            return;
        }

        if (TimelineEnemyNpcAttackMessageUtility.TryInvoke(resolvedNpcTarget, "TakeDamage", (float)damage))
        {
            return;
        }

        resolvedNpcTarget.SendMessage("OnTimelineEnemyAttack", context, SendMessageOptions.DontRequireReceiver);
    }

    private void RefreshTimelineControl(Transform resolvedNpcTarget)
    {
        if (controlledEnemy != null)
        {
            controlledEnemy.BeginTimelineControl(resolvedNpcTarget, stopEnemyVelocity);
        }
    }

    private void ReleaseTimelineControl()
    {
        if (controlledEnemy != null)
        {
            controlledEnemy.EndTimelineControl(boundNpcTarget);
            ResetEnemyAnimation(controlledEnemy.transform);
            controlledEnemy = null;
        }
    }

    private void ResetEnemyAnimation(Transform enemy)
    {
        if (enemy == null)
        {
            return;
        }

        Animator animator = enemy.GetComponentInParent<Animator>();
        if (activeAttackType == TimelineEnemyNpcAttackType.RiftcoreAoe && animator != null)
        {
            animator.SetBool("isCharging", false);
        }

        if (activeAttackType == TimelineEnemyNpcAttackType.HuskfangRoot)
        {
            HuskfangEnemy huskfang = enemy.GetComponentInParent<HuskfangEnemy>();
            if (huskfang != null)
            {
                huskfang.StopWarningRoutine();
            }
        }
    }

    private void WarnMissingBinding(ref bool warned, string bindingName, object playerData)
    {
        if (warned)
        {
            return;
        }

        warned = true;
        Debug.LogWarning($"[{nameof(TimelineEnemyNpcAttackBehaviour)}] Missing {bindingName}. Track binding: {playerData}");
    }

    private static Vector3 GetTargetGroundPosition(Transform target)
    {
        Vector3 position = target.position;
        Collider2D collider = target.GetComponentInChildren<Collider2D>();
        if (collider != null && collider.enabled)
        {
            position.x = collider.bounds.center.x;
            position.y = collider.bounds.min.y;
        }

        return position;
    }

    private static T GetPrivateField<T>(object source, string fieldName) where T : Object
    {
        if (source == null)
        {
            return null;
        }

        FieldInfo field = source.GetType().GetField(fieldName, InstanceFields);
        return field != null ? field.GetValue(source) as T : null;
    }

    private static T GetPrivateValue<T>(object source, string fieldName, T fallback)
    {
        if (source == null)
        {
            return fallback;
        }

        FieldInfo field = source.GetType().GetField(fieldName, InstanceFields);
        if (field == null)
        {
            return fallback;
        }

        object value = field.GetValue(source);
        return value is T typedValue ? typedValue : fallback;
    }
}

[System.Serializable]
public struct NpcTimelineDamageContext
{
    public int Damage;
    public Transform Attacker;
    public Vector3 Direction;

    public NpcTimelineDamageContext(int damage, Transform attacker, Vector3 direction)
    {
        Damage = damage;
        Attacker = attacker;
        Direction = direction;
    }
}

internal static class TimelineEnemyNpcAttackMessageUtility
{
    public static bool TryInvoke(Transform target, string methodName, params object[] values)
    {
        if (target == null)
        {
            return false;
        }

        Component[] components = target.GetComponentsInChildren<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
            {
                continue;
            }

            MethodInfo method = FindMethod(component.GetType(), methodName, values);

            if (method == null)
            {
                continue;
            }

            method.Invoke(component, values);
            return true;
        }

        return false;
    }

    private static MethodInfo FindMethod(System.Type type, string methodName, object[] values)
    {
        MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            if (method.Name != methodName)
            {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != values.Length)
            {
                continue;
            }

            bool matches = true;
            for (int j = 0; j < parameters.Length; j++)
            {
                object value = values[j];
                if (value != null && !parameters[j].ParameterType.IsInstanceOfType(value))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return method;
            }
        }

        return null;
    }
}
