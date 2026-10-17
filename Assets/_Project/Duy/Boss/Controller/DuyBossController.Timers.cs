using UnityEngine;

public partial class DuyBossController
{
    private void TickTimers(float deltaTime)
    {
        attackCooldownTimer -= deltaTime;
        dashCooldownTimer -= deltaTime;
        dashAttackCooldownTimer -= deltaTime;
        parryCooldownTimer -= deltaTime;
        rangedPressureCooldownTimer -= deltaTime;
        evadeCooldownTimer -= deltaTime;
        jumpCooldownTimer -= deltaTime;
        jumpAttackCooldownTimer -= deltaTime;
        comboCooldownTimer -= deltaTime;
        globalSkillCooldownTimer -= deltaTime;
        swordThrowCooldownTimer -= deltaTime;
        jumpSlamCooldownTimer -= deltaTime;
        rangedRotationCooldownTimer -= deltaTime;
        meleeRotationCooldownTimer -= deltaTime;
        strongSkillCooldownTimer -= deltaTime;
        combatPhaseTimer -= deltaTime;
        turnSlashCooldownTimer -= deltaTime;
        adaptiveCounterCooldownTimer -= deltaTime;
        dashCrossCounterTimer -= deltaTime;
        decisionTimer -= deltaTime;
        stateTimer -= deltaTime;
        dashPrepareTimer -= deltaTime;
        hurtAnimationCooldownTimer -= deltaTime;
        comboStepTimer -= deltaTime;
        comboGapTimer -= deltaTime;
        parryCounterTimer -= deltaTime;
        rangedPressureSpawnTimer -= deltaTime;
        secondShockwaveTimer -= deltaTime;
        swordThrowReleaseTimer -= deltaTime;
        swordRecoverTimer -= deltaTime;
        jumpSlamPhaseTimer -= deltaTime;

        isGrounded = CheckGrounded();
        TrackSafeGroundedPosition(deltaTime);
        RecoverIfFallenTooFar();
        TickPendingThreat(deltaTime);
        TrackPlayerBehavior(deltaTime);
        DecayBehaviorMemory(deltaTime);

        if (poiseResetTimer > 0f)
        {
            poiseResetTimer -= deltaTime;
            if (poiseResetTimer <= 0f)
            {
                poiseHitCount = 0;
            }
        }

        if (adaptiveDecayTimer > 0f)
        {
            adaptiveDecayTimer -= deltaTime;
            if (adaptiveDecayTimer <= 0f)
            {
                playerEscapeCount = Mathf.Max(0, playerEscapeCount - 1);
                recentRangedHits = Mathf.Max(0, recentRangedHits - 1);
                consecutiveRangedPressure = Mathf.Max(0, consecutiveRangedPressure - 1);
            }
        }

        if (parryCooldownTimer <= 0f && state != BossState.Parry && state != BossState.ParryCounter)
        {
            consecutiveParries = 0;
        }

        if (defenseResetTimer > 0f)
        {
            defenseResetTimer -= deltaTime;
            if (defenseResetTimer <= 0f)
            {
                consecutiveDefenses = 0;
            }
        }

        TickAttackHitboxFallback(deltaTime);
        TickAttackLunge(deltaTime);
        SyncRuntimeFromController();
    }

    private void TickAttackHitboxFallback(float deltaTime)
    {
        if (!IsAttacking || !useFallbackAttackHitboxTimer)
        {
            return;
        }

        if (!attackHitboxEnabled)
        {
            attackHitboxStartTimer -= deltaTime;
            if (attackHitboxStartTimer <= 0f)
            {
                enablingHitboxFromFallback = true;
                EnableAttackHitbox();
                enablingHitboxFromFallback = false;
            }

            return;
        }

        attackHitboxTimer -= deltaTime;
        if (attackHitboxTimer <= 0f)
        {
            DisableAttackHitbox();
        }
    }

    private void TickAttackLunge(float deltaTime)
    {
        if (attackLungeTimer <= 0f)
        {
            return;
        }

        attackLungeTimer -= deltaTime;
        if (attackLungeTimer <= 0f)
        {
            desiredHorizontalVelocity = 0f;
        }
    }


}
