using UnityEngine;

public partial class DuyBossController
{
    public void TakeDamage(float damage)
    {
        TakeDamageInternal(damage);
    }

    public void TakeDamage(float damage, Transform attacker)
    {
        ActivateAggro(attacker);
        TakeDamageInternal(damage);
    }

    private void TakeDamageInternal(float damage)
    {
        SyncRuntimeFromController();
        BossDamageResult result = bossDamageModel.ApplyDamage(new BossDamageInput(
            ToBossStateId(state),
            damage,
            currentHealth,
            IsPhaseTwo,
            DistanceToTarget(),
            poiseHitCount,
            hurtAnimationCooldownTimer,
            state == BossState.Stun));

        if (result.Outcome == BossDamageOutcome.Ignored)
        {
            return;
        }

        adaptiveDecayTimer = adaptiveDecayDelay;
        if (result.CountedAsRangedHit)
        {
            recentRangedHits++;
        }

        if (result.Outcome == BossDamageOutcome.Parried)
        {
            consecutiveParries++;
            parryCounterTimer = parryCounterDelay;
            CancelAttackHitbox();
            EnterState(BossState.ParryCounter, parryCounterDelay + attackAnimationDuration);
            PlayBossAnimation(AnimationSpine.State.ParrySuccess, false, true);
            SyncRuntimeFromController();
            return;
        }

        currentHealth = result.NewHealth;
        poiseHitCount = result.PoiseHitCount;
        DamagePopupSystem.ShowEnemy(transform.position + Vector3.up, result.AppliedDamage, false);
        hitFlash?.Play();

        if (result.Outcome == BossDamageOutcome.Died)
        {
            Die();
            SyncRuntimeFromController();
            return;
        }

        poiseResetTimer = poiseResetDelay;
        if (result.Outcome != BossDamageOutcome.Interrupted)
        {
            SyncRuntimeFromController();
            return;
        }

        hurtAnimationCooldownTimer = hurtAnimationCooldown;
        CancelAttackHitbox();
        EnterState(BossState.Hurt, hurtLockDuration);
        PlayBossAnimation(AnimationSpine.State.Hurt, false, true);
        SyncRuntimeFromController();
    }

    public void ApplyStun(float duration)
    {
        if (state == BossState.Dead || duration <= 0f)
        {
            return;
        }

        rb.linearVelocity = Vector2.zero;
        horizontalVelocity = 0f;
        desiredHorizontalVelocity = 0f;
        CancelAttackHitbox();
        EnterState(BossState.Stun, duration);
        PlayBossAnimation(AnimationSpine.State.Stun, true, true);
        SyncRuntimeFromController();
    }

    public void NotifyIncomingAttack(
        Vector2 sourcePosition,
        Vector2 velocity,
        IncomingThreatType threatType,
        float estimatedTimeToImpact)
    {
        SyncRuntimeFromController();
        BossThreatData threat = new BossThreatData
        {
            SourceX = sourcePosition.x,
            SourceY = sourcePosition.y,
            VelocityX = velocity.x,
            VelocityY = velocity.y,
            ThreatType = ToBossThreatType(threatType),
            EstimatedTimeToImpact = estimatedTimeToImpact,
            ReceivedTime = Time.time
        };

        BossThreatReaction reaction = bossThreatModel.Notify(ToBossStateId(state), IsAttacking, IsPhaseTwo, threat);
        if (!reaction.Accepted)
        {
            return;
        }

        pendingThreat = new IncomingThreat
        {
            SourcePosition = new Vector2(reaction.Threat.SourceX, reaction.Threat.SourceY),
            Velocity = new Vector2(reaction.Threat.VelocityX, reaction.Threat.VelocityY),
            ThreatType = threatType,
            EstimatedTimeToImpact = reaction.Threat.EstimatedTimeToImpact,
            ReceivedTime = reaction.Threat.ReceivedTime
        };
        pendingThreatReactionTimer = reaction.ReactionDelay;
        hasPendingThreat = true;
        SyncRuntimeFromController();
    }

    public void FlyToTarget(Vector3 targetPosition, float holdTime)
    {
        if (state == BossState.Dead)
        {
            return;
        }

        transform.position = targetPosition;
        rb.linearVelocity = Vector2.zero;
        horizontalVelocity = 0f;
        desiredHorizontalVelocity = 0f;
        CancelAttackHitbox();
        EnterState(BossState.Hurt, holdTime);
        SyncRuntimeFromController();
    }

    public void ApplyVelocity(Vector2 velocity, bool forceDynamicBody)
    {
        if (state == BossState.Dead)
        {
            return;
        }

        rb.linearVelocity = velocity;
        horizontalVelocity = velocity.x;
        desiredHorizontalVelocity = velocity.x;

        if (state == BossState.Attack || state == BossState.ComboAttack ||
            state == BossState.AttackRecovery || state == BossState.PostAttackPause)
        {
            CancelAttackHitbox();
            EnterState(BossState.Hurt, 0.1f);
        }

        SyncRuntimeFromController();
    }

    public ParryResult OnParried(ParryContext context)
    {
        if (state == BossState.Dead)
        {
            return ParryResult.Failed;
        }

        CancelAttackHitbox();
        ApplyStun(Mathf.Max(0.25f, context.DefaultHitStopDuration + 0.2f));
        SyncRuntimeFromController();
        return ParryResult.Successful(context.DefaultHitStopDuration, true);
    }

}
