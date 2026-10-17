using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerAudio))]
[RequireComponent(typeof(AnimationSpine))]
public class PlayerParryController : MonoBehaviour
{
    [SerializeField] private PlayerParryDataSO parryData;

    private Player player;
    private PlayerHealth health;
    private PlayerAudio audioController;
    private AnimationSpine animationSpine;
    private ParryScreenTint screenTint;
    private PlayerParryService service;
    private Coroutine hitStopRoutine;
    private float timeScaleBeforeHitStop = 1f;
    private float visualHoldRemaining;

    public static PlayerParryController Ensure(Player owner)
    {
        if (owner == null)
        {
            return null;
        }

        if (owner.TryGetComponent(out PlayerParryController controller))
        {
            return controller;
        }

        return owner.gameObject.AddComponent<PlayerParryController>();
    }

    public bool IsBusy => service != null && service.IsBusy;
    public bool IsVisualBusy => visualHoldRemaining > 0f;
    public bool IsMovementLocked => service != null && service.IsMovementLocked;
    public bool IsBlockSuppressed => service != null && service.IsBlockSuppressed;

    private void Awake()
    {
        player = GetComponent<Player>();
        health = GetComponent<PlayerHealth>();
        audioController = GetComponent<PlayerAudio>();
        animationSpine = GetComponent<AnimationSpine>();
        screenTint = GetComponent<ParryScreenTint>();

        if (parryData == null)
        {
            Debug.LogError("PlayerParryController requires a PlayerParryDataSO reference.", this);
            enabled = false;
            return;
        }

        service = new PlayerParryService(parryData);
        service.Started += OnParryStarted;
        service.Failed += OnParryFailed;
        service.Succeeded += OnParrySucceeded;
        service.Guarded += OnGuarded;
    }

    private void OnDisable()
    {
        ResetDefenseState();
    }

    private void OnDestroy()
    {
        if (service == null)
        {
            return;
        }

        service.Started -= OnParryStarted;
        service.Failed -= OnParryFailed;
        service.Succeeded -= OnParrySucceeded;
        service.Guarded -= OnGuarded;
    }

    public void Tick(float deltaTime)
    {
        bool wasBusy = IsBusy;
        service?.Tick(deltaTime);
        TickVisualHold(deltaTime);
        if (health != null)
        {
            health.SetBlockSuppressed(IsBlockSuppressed);
        }

        if (wasBusy && !IsBusy && !IsVisualBusy)
        {
            PlayIdle();
        }
    }

    public bool TryStartParry()
    {
        if (health == null || health.IsDead || health.IsHurt || service == null)
        {
            return false;
        }

        return service.TryStart();
    }

    public void ReleaseParry()
    {
        bool wasBusy = IsBusy;
        service?.Release();

        if (wasBusy && !IsBusy)
        {
            visualHoldRemaining = 0f;
            PlayIdle();
        }
    }

    public void ResetDefenseState()
    {
        StopHitStop(restoreTimeScale: true);
        visualHoldRemaining = 0f;
        service?.Reset();
        if (health != null)
        {
            health.SetBlockSuppressed(false);
        }
    }

    public bool TryParry(IParryable parryable, IncomingAttackData attack, out ParryResult result)
    {
        result = ParryResult.Failed;
        if (service == null || parryable == null)
        {
            return false;
        }

        Vector2 facing = GetFacingSign() > 0f ? Vector2.right : Vector2.left;
        ParryContext context = new ParryContext(
            player,
            transform,
            attack.SourceTransform,
            attack.HitPoint,
            attack.IncomingDirection,
            facing,
            attack.AttackType,
            parryData != null ? parryData.hitStopDuration : 0.05f);

        return service.TryResolve(parryable, context, out result);
    }

    public PlayerDefenseResult ResolveIncomingAttack(IParryable parryable, IncomingAttackData attack)
    {
        if (service == null)
        {
            return PlayerDefenseResult.None(attack.Damage);
        }

        Vector2 facing = GetFacingSign() > 0f ? Vector2.right : Vector2.left;
        ParryContext context = new ParryContext(
            player,
            transform,
            attack.SourceTransform,
            attack.HitPoint,
            attack.IncomingDirection,
            facing,
            attack.AttackType,
            parryData != null ? parryData.hitStopDuration : 0.05f);

        return service.ResolveIncomingAttack(parryable, context, attack.Damage);
    }

    private void OnParryStarted()
    {
        PlayStateOrFallback(AnimationSpine.State.ParryStart, AnimationSpine.State.Block, true);
    }

    private void OnParryFailed()
    {
        PlayStateOrFallback(
            AnimationSpine.State.ParryFail,
            AnimationSpine.State.Block,
            visualHoldDuration: parryData != null ? parryData.failAnimationDuration : 0.3f);

        audioController?.PlayParryFail();
    }

    private void OnParrySucceeded(ParryResult result)
    {
        PlayStateOrFallback(
            AnimationSpine.State.ParrySuccess,
            AnimationSpine.State.Block,
            visualHoldDuration: parryData != null ? parryData.successAnimationDuration : 0.12f);

        audioController?.PlayParry();
        SpawnSuccessVfx();
        screenTint?.Play();

        float hitStopDuration = result.HitStopDuration > 0f
            ? result.HitStopDuration
            : (parryData != null ? parryData.hitStopDuration : 0f);
        if (hitStopDuration > 0f)
        {
            if (hitStopRoutine != null)
            {
                StopHitStop(restoreTimeScale: true);
            }

            hitStopRoutine = StartCoroutine(HitStopRoutine(hitStopDuration));
        }
    }

    private void OnGuarded(int originalDamage, int guardedDamage)
    {
        SpawnGuardVfx();
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        timeScaleBeforeHitStop = Time.timeScale;
        Time.timeScale = 0f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Time.timeScale = timeScaleBeforeHitStop;
        hitStopRoutine = null;
    }

    private void StopHitStop(bool restoreTimeScale)
    {
        if (hitStopRoutine == null)
        {
            return;
        }

        StopCoroutine(hitStopRoutine);
        hitStopRoutine = null;
        if (restoreTimeScale)
        {
            Time.timeScale = timeScaleBeforeHitStop;
        }
    }

    private void SpawnSuccessVfx()
    {
        if (parryData == null)
        {
            return;
        }

        float facingSign = GetFacingSign();
        Vector2 mirroredOffset = parryData.successVfxOffset;
        mirroredOffset.x *= facingSign;

        Vector3 spawnPosition = transform.position + (Vector3)mirroredOffset;
        if (parryData.successVfxPrefab != null)
        {
            Vector3 vfxScale = parryData.successVfxPrefab.transform.localScale;
            vfxScale.x = Mathf.Abs(vfxScale.x) * facingSign;
            EffectPool.Spawn(parryData.successVfxPrefab, spawnPosition, Quaternion.identity, vfxScale);
            return;
        }

        if (!parryData.useDefaultSuccessVfx)
        {
            return;
        }

        ParrySuccessEffect.Spawn(
            spawnPosition,
            parryData.defaultSuccessVfxColor,
            parryData.defaultSuccessVfxDuration,
            parryData.defaultSuccessVfxStartRadius,
            parryData.defaultSuccessVfxEndRadius);
    }

    private void SpawnGuardVfx()
    {
        if (parryData == null || parryData.guardHitVfxPrefab == null)
        {
            return;
        }

        float facingSign = GetFacingSign();
        Vector2 mirroredOffset = parryData.guardHitVfxOffset;
        mirroredOffset.x *= facingSign;

        Vector3 spawnPosition = transform.position + (Vector3)mirroredOffset;
        Vector3 vfxScale = parryData.guardHitVfxPrefab.transform.localScale;
        vfxScale.x = Mathf.Abs(vfxScale.x) * facingSign;
        EffectPool.Spawn(parryData.guardHitVfxPrefab, spawnPosition, Quaternion.identity, vfxScale);
    }

    private float GetFacingSign()
        => transform.localScale.x < 0f ? 1f : -1f;

    private void PlayIdle()
    {
        PlayStateOrFallback(AnimationSpine.State.Idle, AnimationSpine.State.None, true);
    }

    private void PlayStateOrFallback(
        AnimationSpine.State state,
        AnimationSpine.State fallbackState,
        bool loop = false,
        float visualHoldDuration = 0f)
    {
        if (animationSpine == null)
        {
            return;
        }

        if (animationSpine.HasStateAnimation(state))
        {
            TrackParryVisual(animationSpine.PlayForcedFromSetupPose(state, loop, 0), loop, visualHoldDuration);
            return;
        }

        if (fallbackState != AnimationSpine.State.None && animationSpine.HasStateAnimation(fallbackState))
        {
            TrackParryVisual(animationSpine.PlayForcedFromSetupPose(fallbackState, loop, 0), loop, visualHoldDuration);
        }
    }

    private void TrackParryVisual(Spine.TrackEntry entry, bool loop, float visualHoldDuration = 0f)
    {
        if (loop)
        {
            visualHoldRemaining = 0f;
            return;
        }

        float entryDuration = entry != null && entry.Animation != null ? entry.Animation.Duration : 0f;
        float holdDuration = visualHoldDuration > 0f ? visualHoldDuration : entryDuration;
        visualHoldRemaining = Mathf.Max(visualHoldRemaining, holdDuration);
    }

    private void TickVisualHold(float deltaTime)
    {
        if (visualHoldRemaining <= 0f)
        {
            return;
        }

        visualHoldRemaining = Mathf.Max(0f, visualHoldRemaining - deltaTime);
        if (visualHoldRemaining <= 0f && !IsBusy)
        {
            PlayIdle();
        }
    }
}
