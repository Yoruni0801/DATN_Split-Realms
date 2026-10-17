using System;
using UnityEngine;

public sealed class PlayerParryService
{
    private readonly PlayerParryDataSO data;
    private float perfectWindowRemaining;
    private float guardGraceRemaining;
    private float recoveryRemaining;
    private float cooldownRemaining;
    private bool guardInputHeld;
    private bool guardGracePendingAfterPerfect;

    public PlayerParryService(PlayerParryDataSO data)
    {
        this.data = data;
    }

    public event Action Started;
    public event Action Failed;
    public event Action<ParryResult> Succeeded;
    public event Action GuardStarted;
    public event Action GuardEnded;
    public event Action<int, int> Guarded;

    public bool IsPerfectWindowOpen => perfectWindowRemaining > 0f;
    public bool IsGuardActive => !IsPerfectWindowOpen && (guardInputHeld || guardGraceRemaining > 0f);
    public bool IsRecovering => recoveryRemaining > 0f;
    public bool IsCoolingDown => cooldownRemaining > 0f;
    public bool IsBusy => IsPerfectWindowOpen || IsGuardActive || IsRecovering;
    public bool CanStart => !IsPerfectWindowOpen && !IsGuardActive && !IsRecovering && cooldownRemaining <= 0f;

    public bool IsMovementLocked =>
        (IsPerfectWindowOpen && data.lockMovementDuringWindow) ||
        (IsGuardActive && data.lockMovementDuringGuard) ||
        (IsRecovering && data.lockMovementDuringRecovery);

    public bool IsBlockSuppressed =>
        data.suppressBlockDuringParry && (IsPerfectWindowOpen || IsGuardActive || IsRecovering);

    public bool TryStart()
    {
        if (!CanStart)
        {
            return false;
        }

        perfectWindowRemaining = data.perfectWindowDuration;
        guardGraceRemaining = 0f;
        guardInputHeld = true;
        guardGracePendingAfterPerfect = false;
        Started?.Invoke();
        return true;
    }

    public void Release()
    {
        if (!guardInputHeld)
        {
            return;
        }

        guardInputHeld = false;
        perfectWindowRemaining = 0f;
        guardGraceRemaining = 0f;
        guardGracePendingAfterPerfect = false;
        cooldownRemaining = Math.Max(cooldownRemaining, data.cooldownDuration);
    }

    public void Tick(float deltaTime)
    {
        bool wasGuardActive = IsGuardActive;

        if (cooldownRemaining > 0f)
        {
            cooldownRemaining -= deltaTime;
        }

        if (recoveryRemaining > 0f)
        {
            recoveryRemaining -= deltaTime;
        }

        TickPerfectWindow(deltaTime);
        TickGuardGrace(deltaTime);
        NotifyGuardStateChanged(wasGuardActive);
    }

    public void Reset()
    {
        bool wasGuardActive = IsGuardActive;
        perfectWindowRemaining = 0f;
        guardGraceRemaining = 0f;
        recoveryRemaining = 0f;
        cooldownRemaining = 0f;
        guardInputHeld = false;
        guardGracePendingAfterPerfect = false;

        if (wasGuardActive)
        {
            GuardEnded?.Invoke();
        }
    }

    public PlayerDefenseResult ResolveIncomingAttack(
        IParryable parryable,
        ParryContext context,
        int damage)
    {
        if (IsPerfectWindowOpen)
        {
            if (!IsFacingAttackSource(context))
            {
                return PlayerDefenseResult.None(damage);
            }

            ParryResult parryResult = ResolvePerfectParry(parryable, context);
            if (parryResult.Success)
            {
                int damageToApply = parryResult.PreventDamage ? 0 : Math.Max(0, damage);
                return PlayerDefenseResult.PerfectParry(parryResult, damageToApply);
            }

            return PlayerDefenseResult.None(damage);
        }

        if (IsGuardActive)
        {
            if (!IsFacingAttackSource(context))
            {
                return PlayerDefenseResult.None(damage);
            }

            int guardedDamage = CalculateGuardedDamage(damage);
            Guarded?.Invoke(Math.Max(0, damage), guardedDamage);
            return PlayerDefenseResult.Guard(guardedDamage);
        }

        return PlayerDefenseResult.None(damage);
    }

    private void TickPerfectWindow(float deltaTime)
    {
        if (perfectWindowRemaining <= 0f)
        {
            return;
        }

        perfectWindowRemaining -= deltaTime;
        if (perfectWindowRemaining > 0f)
        {
            return;
        }

        perfectWindowRemaining = 0f;
        cooldownRemaining = Math.Max(cooldownRemaining, data.cooldownDuration);

        if (guardInputHeld || guardGracePendingAfterPerfect)
        {
            if (!guardInputHeld)
            {
                guardGraceRemaining = data.guardGraceDuration;
            }

            guardGracePendingAfterPerfect = false;
            return;
        }

        recoveryRemaining = Math.Max(recoveryRemaining, GetFailRecoveryDuration());
        Failed?.Invoke();
    }

    private void TickGuardGrace(float deltaTime)
    {
        if (IsPerfectWindowOpen || guardInputHeld || guardGraceRemaining <= 0f)
        {
            return;
        }

        guardGraceRemaining = Math.Max(0f, guardGraceRemaining - deltaTime);
    }

    private void NotifyGuardStateChanged(bool wasGuardActive)
    {
        bool isGuardActive = IsGuardActive;
        if (!wasGuardActive && isGuardActive)
        {
            GuardStarted?.Invoke();
        }
        else if (wasGuardActive && !isGuardActive)
        {
            GuardEnded?.Invoke();
        }
    }

    public bool TryResolve(IParryable parryable, ParryContext context, out ParryResult result)
    {
        result = ParryResult.Failed;

        if (!IsPerfectWindowOpen || parryable == null || !IsFacingAttackSource(context))
        {
            return false;
        }

        result = ResolvePerfectParry(parryable, context);
        return result.Success;
    }

    private ParryResult ResolvePerfectParry(IParryable parryable, ParryContext context)
    {
        ParryResult result = parryable != null
            ? parryable.OnParried(context)
            : ParryResult.Successful(context.DefaultHitStopDuration);

        if (!result.Success)
        {
            return result;
        }

        perfectWindowRemaining = 0f;
        guardGraceRemaining = 0f;
        guardInputHeld = false;
        guardGracePendingAfterPerfect = false;
        recoveryRemaining = Math.Max(recoveryRemaining, GetSuccessRecoveryDuration());
        cooldownRemaining = Math.Max(cooldownRemaining, data.cooldownDuration);
        Succeeded?.Invoke(result);
        return result;
    }

    private int CalculateGuardedDamage(int damage)
    {
        int sanitizedDamage = Math.Max(0, damage);
        float damageMultiplier = Math.Max(0f, 1f - data.guardDamageReduction);
        return (int)Math.Ceiling(sanitizedDamage * damageMultiplier);
    }

    private float GetFailRecoveryDuration()
        => data.failRecoveryDuration;

    private float GetSuccessRecoveryDuration()
        => data.successRecoveryDuration;

    private bool IsFacingAttackSource(ParryContext context)
    {
        if (!data.requireFacingSource)
        {
            return true;
        }

        Vector2 facing = context.DefenderFacing;
        if (facing.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        Vector2 toSource = ResolveDirectionToSource(context);
        if (toSource.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        float halfAngle = Mathf.Clamp(data.defenseFacingAngle, 1f, 180f) * 0.5f;
        float requiredDot = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
        return Vector2.Dot(facing.normalized, toSource.normalized) >= requiredDot;
    }

    private static Vector2 ResolveDirectionToSource(ParryContext context)
    {
        if (context.SourceTransform != null && context.DefenderTransform != null)
        {
            Vector2 toSource = (Vector2)(context.SourceTransform.position - context.DefenderTransform.position);
            if (toSource.sqrMagnitude > 0.0001f)
            {
                return toSource;
            }
        }

        if (context.IncomingDirection.sqrMagnitude > 0.0001f)
        {
            return -context.IncomingDirection;
        }

        return Vector2.zero;
    }
}
