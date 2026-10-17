using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class ParryableObject : MonoBehaviour, IParryable
{
    [Header("Result")]
    [SerializeField] private bool preventDamage = true;
    [SerializeField] private float hitStopOverride = -1f;

    [Header("Reaction")]
    [SerializeField] private Transform reactionRoot;
    [SerializeField] private Rigidbody2D reactionRigidbody;
    [SerializeField] private float stunDuration = 0.35f;
    [SerializeField] private float knockbackForce = 8f;
    [SerializeField] private float knockUpForce = 1.5f;
    [SerializeField] private string parriedTrigger = "Parried";
    [SerializeField] private MonoBehaviour[] behavioursToPause;
    [SerializeField] private bool autoPauseParentBehaviours = true;

    [Header("Feedback")]
    [SerializeField] private GameObject parriedVfxPrefab;
    [SerializeField] private Transform parriedVfxSpawnPoint;
    [SerializeField] private UnityEvent parried;

    private Animator cachedAnimator;
    private Coroutine stunRoutine;
    private List<MonoBehaviour> autoPausedBehaviours;
    private IReadOnlyList<MonoBehaviour> activePausedBehaviours;

    public static ParryableObject Ensure(Component source)
    {
        if (source == null)
        {
            return null;
        }

        ParryableObject parryable = source.GetComponentInParent<ParryableObject>();
        return parryable != null ? parryable : source.gameObject.AddComponent<ParryableObject>();
    }

    protected virtual void Awake()
    {
        ResolveCachedReferences();
    }

    protected virtual void OnDisable()
    {
        if (stunRoutine != null)
        {
            StopCoroutine(stunRoutine);
            stunRoutine = null;
        }

        ResumePausedBehaviours();
    }

    public virtual ParryResult OnParried(ParryContext context)
    {
        ResolveCachedReferences();
        PlayFeedback(context);
        
        BeginStun();
        ApplyKnockback(context);

        float hitStopDuration = hitStopOverride >= 0f
            ? hitStopOverride
            : context.DefaultHitStopDuration;
        return new ParryResult(true, preventDamage, hitStopDuration);
    }

    private void ResolveCachedReferences()
    {
        if (reactionRoot == null)
        {
            reactionRoot = transform;
        }

        if (reactionRigidbody == null)
        {
            reactionRigidbody = reactionRoot.GetComponentInParent<Rigidbody2D>();
        }

        if (cachedAnimator == null)
        {
            cachedAnimator = reactionRoot.GetComponentInParent<Animator>();
        }
    }

    private void PlayFeedback(ParryContext context)
    {
        if (cachedAnimator != null && !string.IsNullOrWhiteSpace(parriedTrigger))
        {
            cachedAnimator.SetTrigger(parriedTrigger);
        }

        if (parriedVfxPrefab != null)
        {
            Transform spawnPoint = parriedVfxSpawnPoint != null ? parriedVfxSpawnPoint : reactionRoot;
            EffectPool.Spawn(parriedVfxPrefab, spawnPoint.position, Quaternion.identity);
        }

        parried?.Invoke();
    }

    private void ApplyKnockback(ParryContext context)
    {
        if (reactionRigidbody == null || knockbackForce <= 0f && knockUpForce <= 0f)
        {
            return;
        }

        Vector2 origin = reactionRoot != null
            ? (Vector2)reactionRoot.position
            : (Vector2)transform.position;
        Vector2 defenderPosition = context.DefenderTransform != null
            ? (Vector2)context.DefenderTransform.position
            : context.HitPoint;
        Vector2 awayFromDefender = (origin - defenderPosition).normalized;
        if (awayFromDefender.sqrMagnitude < 0.0001f)
        {
            awayFromDefender = context.DefenderFacing.sqrMagnitude > 0.0001f
                ? context.DefenderFacing.normalized
                : Vector2.right;
        }

        float appliedKnockUp = reactionRigidbody.gravityScale > 0.01f ? Mathf.Max(0f, knockUpForce) : 0f;
        reactionRigidbody.linearVelocity = new Vector2(
            awayFromDefender.x * knockbackForce,
            appliedKnockUp);
    }

    private void BeginStun()
    {
        if (stunDuration <= 0f)
        {
            return;
        }

        if (stunRoutine != null)
        {
            StopCoroutine(stunRoutine);
            ResumePausedBehaviours();
        }

        stunRoutine = StartCoroutine(StunRoutine(stunDuration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        PauseBehaviours();

        Bao.Enemies.BaoEnemyBase enemyBase = reactionRoot != null ? reactionRoot.GetComponentInParent<Bao.Enemies.BaoEnemyBase>() : GetComponentInParent<Bao.Enemies.BaoEnemyBase>();
        if (enemyBase != null) enemyBase.SetStunEffectActive(true);

        yield return new WaitForSeconds(duration);

        if (enemyBase != null) enemyBase.SetStunEffectActive(false);

        ResumePausedBehaviours();
        stunRoutine = null;
    }

    private void PauseBehaviours()
    {
        activePausedBehaviours = ResolvePausedBehaviours();
        for (int i = 0; i < activePausedBehaviours.Count; i++)
        {
            MonoBehaviour behaviour = activePausedBehaviours[i];
            if (behaviour != null && behaviour.enabled)
            {
                behaviour.enabled = false;
            }
        }
    }

    private void ResumePausedBehaviours()
    {
        if (activePausedBehaviours == null)
        {
            return;
        }

        for (int i = 0; i < activePausedBehaviours.Count; i++)
        {
            if (activePausedBehaviours[i] != null)
            {
                activePausedBehaviours[i].enabled = true;
            }
        }

        activePausedBehaviours = null;
    }

    private IReadOnlyList<MonoBehaviour> ResolvePausedBehaviours()
    {
        if (behavioursToPause != null && behavioursToPause.Length > 0)
        {
            return behavioursToPause;
        }

        if (!autoPauseParentBehaviours)
        {
            return System.Array.Empty<MonoBehaviour>();
        }

        if (autoPausedBehaviours != null)
        {
            return autoPausedBehaviours;
        }

        autoPausedBehaviours = new List<MonoBehaviour>();
        MonoBehaviour[] behaviours = reactionRoot.GetComponentsInParent<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour == this)
            {
                continue;
            }

            autoPausedBehaviours.Add(behaviour);
        }

        return autoPausedBehaviours;
    }
}
