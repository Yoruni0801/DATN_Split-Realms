using UnityEngine;

public partial class DuyBossController
{
    private void TickPendingThreat(float deltaTime)
    {
        if (!hasPendingThreat)
        {
            return;
        }

        if (IsHardLocked)
        {
            hasPendingThreat = false;
            return;
        }

        pendingThreatReactionTimer -= deltaTime;
        if (pendingThreatReactionTimer > 0f)
        {
            return;
        }

        if (pendingThreat.EstimatedTimeToImpact > 0f &&
            Time.time - pendingThreat.ReceivedTime > pendingThreat.EstimatedTimeToImpact + 0.15f)
        {
            hasPendingThreat = false;
            return;
        }

        TryReactToThreat(pendingThreat);
        hasPendingThreat = false;
    }

    private bool TryReactToThreat(IncomingThreat threat)
    {
        if (consecutiveDefenses >= Mathf.Max(1, maxConsecutiveDefenses))
        {
            return false;
        }

        switch (threat.ThreatType)
        {
            case IncomingThreatType.Melee:
                if (TryThreatDashCounter(threat))
                {
                    return true;
                }

                if (TryThreatParry(threat))
                {
                    return true;
                }

                return TryStartEvade(AwayFromThreat(threat), EvadeType.BackDash) ||
                    TryStartJumpEvade(AwayFromThreat(threat));

            case IncomingThreatType.HeavyAttack:
                return TryStartEvade(AwayFromThreat(threat), EvadeType.BackDash) ||
                    TryStartJumpEvade(AwayFromThreat(threat));

            case IncomingThreatType.GroundAttack:
            case IncomingThreatType.ProjectileLow:
                return TryStartJumpEvade(ResolveJumpDirection(threat)) ||
                    TryStartEvade(AwayFromThreat(threat), EvadeType.BackDash) ||
                    TryThreatParry(threat);

            case IncomingThreatType.ProjectileHigh:
                return TryStartEvade(ResolveProjectileEvadeDirection(threat), EvadeType.ForwardDash) ||
                    TryThreatParry(threat);

            default:
                return false;
        }
    }

    private bool TryThreatDashCounter(IncomingThreat threat)
    {
        if (threat.ThreatType != IncomingThreatType.Melee ||
            threat.Velocity.sqrMagnitude <= 0.01f ||
            dashAttackCooldownTimer > 0f ||
            IsHardLocked)
        {
            return false;
        }

        float distance = DistanceFromThreat(threat);
        if (distance < dashAttackMinRange || distance > dashAttackMaxRange)
        {
            return false;
        }

        if (Random.value > EffectiveDashChance())
        {
            return false;
        }

        int direction = threat.SourcePosition.x >= transform.position.x ? 1 : -1;
        return TryEnterDashAttack(direction);
    }

    private bool TryThreatParry(IncomingThreat threat)
    {
        if (threat.ThreatType == IncomingThreatType.HeavyAttack ||
            !CanParry() ||
            DistanceFromThreat(threat) > parryCounterRange ||
            threat.EstimatedTimeToImpact < minimumThreatTime)
        {
            return false;
        }

        float chance = Mathf.Min(0.5f, parryChance + (IsPhaseTwo ? phaseTwoParryChanceBonus : 0f));
        if (Random.value > chance)
        {
            return false;
        }

        RegisterDefenseUse();
        StartParry();
        return true;
    }

    private bool TryStartEvade(int direction, EvadeType type)
    {
        if (!CanDash() || !CanMoveSafely(direction, evadeSpeed) || evadeCooldownTimer > 0f)
        {
            return false;
        }

        evadeType = type;
        RegisterDefenseUse();
        return TryEnterDash(direction, DashMode.Evade);
    }

    private bool TryStartJumpEvade(int horizontalDirection)
    {
        if (!CanJumpEvade())
        {
            return false;
        }

        int direction = ResolveSafeJumpDirection(horizontalDirection == 0 ? -DirectionToTarget() : horizontalDirection);
        if (direction == int.MinValue)
        {
            return false;
        }

        RegisterDefenseUse();
        CancelAttackHitbox();
        jumpDirection = direction;
        jumpCooldownTimer = jumpCooldown * (IsPhaseTwo ? phaseTwoDashCooldownMultiplier : 1f);
        hasLeftGround = false;
        horizontalVelocity = jumpDirection * jumpForwardSpeed;
        desiredHorizontalVelocity = horizontalVelocity;
        rb.linearVelocity = new Vector2(horizontalVelocity, jumpForce);
        EnterState(BossState.JumpEvade, minimumAirTime);
        PlayBossAnimation(AnimationSpine.State.Jump, false, true);
        return true;
    }

    private bool CanJumpEvade()
    {
        return jumpCooldownTimer <= 0f &&
            !IsHardLocked &&
            isGrounded &&
            HasJumpClearance();
    }

    private int ResolveSafeJumpDirection(int preferredDirection)
    {
        if (HasSafeJumpLanding(preferredDirection))
        {
            return preferredDirection;
        }

        int opposite = -preferredDirection;
        if (opposite != preferredDirection && HasSafeJumpLanding(opposite))
        {
            return opposite;
        }

        return HasSafeJumpLanding(0) ? 0 : int.MinValue;
    }

    private void RegisterDefenseUse()
    {
        consecutiveDefenses++;
        defenseResetTimer = defenseResetDelay;
        shouldCounterAfterDefense = Random.value < postEvadeCounterChance;
    }

    private void FinishDefenseRecovery(float recoveryDuration = -1f)
    {
        if (shouldCounterAfterDefense && DistanceToTarget() <= attackRange && attackCooldownTimer <= 0f)
        {
            shouldCounterAfterDefense = false;
            StartComboAttack();
            return;
        }

        shouldCounterAfterDefense = false;
        EnterState(BossState.PostAttackPause, recoveryDuration >= 0f ? recoveryDuration : RandomShortIdle());
    }

    private int AwayFromThreat(IncomingThreat threat)
    {
        return transform.position.x >= threat.SourcePosition.x ? 1 : -1;
    }

    private int ResolveJumpDirection(IncomingThreat threat)
    {
        int away = AwayFromThreat(threat);
        if (CanMoveSafely(away, jumpForwardSpeed))
        {
            return away;
        }

        int toward = -away;
        return CanMoveSafely(toward, jumpForwardSpeed) ? toward : 0;
    }

    private int ResolveProjectileEvadeDirection(IncomingThreat threat)
    {
        if (threat.Velocity.sqrMagnitude > 0.001f)
        {
            int throughProjectile = threat.Velocity.x >= 0f ? -1 : 1;
            if (CanMoveSafely(throughProjectile, dashSpeed))
            {
                return throughProjectile;
            }
        }

        return AwayFromThreat(threat);
    }

    private float DistanceFromThreat(IncomingThreat threat)
    {
        return Vector2.Distance(transform.position, threat.SourcePosition);
    }

}
