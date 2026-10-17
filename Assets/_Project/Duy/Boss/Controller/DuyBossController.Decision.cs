using UnityEngine;

public partial class DuyBossController
{
    private void UpdateDecision()
    {
        if (IsHardLocked)
        {
            return;
        }

        if (target == null)
        {
            if (state != BossState.Idle)
            {
                EnterState(BossState.Idle, RandomDecisionInterval());
            }

            return;
        }

        if (decisionTimer > 0f && stateTimer > 0f)
        {
            return;
        }

        if (TryRunCombatBrainOpeningAction())
        {
            return;
        }

        ChooseCombatAction();
    }

    private bool TryRunCombatBrainOpeningAction()
    {
        if (bossCombatBrain == null)
        {
            return false;
        }

        float distance = DistanceToTarget();
        UpdateCombatZone(distance);
        if (!isChaseMode)
        {
            return false;
        }

        BossActionRequest request = bossCombatBrain.DecideOpeningAction(new BossCombatBrainInput(
            target != null,
            isAggro,
            IsHardLocked,
            decisionTimer,
            stateTimer,
            distance,
            attackRange,
            DirectionToTarget(),
            CanDash() && CanMoveSafely(DirectionToTarget(), dashSpeed),
            attackCooldownTimer <= 0f && comboCooldownTimer <= 0f && distance <= comboContinueRange,
            EffectiveDashChance()));

        switch (request.Type)
        {
            case BossActionType.Idle:
                if (state != BossState.Idle)
                {
                    EnterState(BossState.Idle, RandomDecisionInterval());
                }

                return true;

            case BossActionType.GapCloseDash:
                return TryEnterDash(request.Direction, DashMode.GapClose);

            case BossActionType.Chase:
                moveDirection = request.Direction;
                EnterState(BossState.Chase, RandomDecisionInterval());
                return true;

            case BossActionType.ComboAttack:
                StartComboAttack();
                return true;

            default:
                return false;
        }
    }

    private void ChooseCombatAction()
    {
        float distance = DistanceToTarget();
        UpdateCombatZone(distance);

        if (TryChooseTurnSlash(distance))
        {
            return;
        }

        if (TryChooseAdaptiveCounter(distance))
        {
            return;
        }

        if (combatPhase == CombatPhase.Recovery)
        {
            UpdateCombatRecovery(distance);
            return;
        }

        if (isChaseMode)
        {
            ChooseChaseAction(distance);
            return;
        }

        if (IsTargetAirborne())
        {
            if (TryChooseAirborneAction(distance))
            {
                return;
            }
        }

        if (distance <= tooCloseDistance)
        {
            BeginMeleeRotation();
            ChooseMeleeRotationAction(distance);
            return;
        }

        if (combatPhase == CombatPhase.Ranged)
        {
            ChooseRangedRotationAction(distance);
            return;
        }

        ChooseMeleeRotationAction(distance);
    }

    private void ChooseChaseAction(float distance)
    {
        if (distance <= combatEnterRange)
        {
            isChaseMode = false;
            BeginCombatRecovery(CombatPhase.Ranged, 0.25f, 0.45f);
            return;
        }

        if (isAggro && isRunMode)
        {
            int direction = DirectionToTarget();
            if (CanDash() &&
                CanMoveSafely(direction, dashSpeed) &&
                Random.value < EffectiveDashChance())
            {
                TryEnterDash(direction, DashMode.GapClose);
                return;
            }

            moveDirection = direction;
            EnterState(BossState.Chase, RandomDecisionInterval());
            return;
        }

        if (distance >= dashAttackMinRange && distance <= dashAttackMaxRange && TryEnterDashAttack())
        {
            RegisterSkillUse(BossSkillAction.DashSlash);
            return;
        }

        if (isRunMode && CanDash() && Random.value < EffectiveDashChance())
        {
            if (TryEnterDash(DirectionToTarget(), DashMode.GapClose))
            {
                return;
            }
        }

        if (IsWithinGapCloseDashRange(distance) && CanDash() && Random.value < EffectiveDashChance())
        {
            if (TryEnterDash(DirectionToTarget(), DashMode.GapClose))
            {
                return;
            }
        }

        if (distance > farDistance)
        {
            moveDirection = DirectionToTarget();
            EnterState(BossState.Chase, RandomDecisionInterval());
            return;
        }

        if (distance <= mediumDistance)
        {
            ChooseMediumRangeMovement();
            return;
        }

        moveDirection = DirectionToTarget();
        EnterState(BossState.Chase, RandomDecisionInterval());
    }

    private void ChooseRangedRotationAction(float distance)
    {
        if (distance <= tooCloseDistance)
        {
            BeginMeleeRotation();
            ChooseCloseRangeMovement();
            return;
        }

        if (rangedSkillsUsedThisCycle >= maximumRangedSkillsPerCycle)
        {
            FinishRangedRotation();
            return;
        }

        if (TryChooseSkillRangeAction(distance))
        {
            return;
        }

        if (rangedSkillsUsedThisCycle >= minimumRangedSkillsPerCycle || !HasReadyRangedRotationSkill(distance))
        {
            FinishRangedRotation();
            return;
        }

        moveDirection = DirectionToTarget();
        EnterState(BossState.Approach, RandomDecisionInterval());
    }

    private void ChooseMeleeRotationAction(float distance)
    {
        if (distance <= comboContinueRange)
        {
            if (TryChooseParry(distance))
            {
                return;
            }

            if (attackCooldownTimer <= 0f && comboCooldownTimer <= 0f && meleeRotationCooldownTimer <= 0f)
            {
                StartComboAttack();
                return;
            }

            ChooseCloseRangeMovement();
            return;
        }

        moveDirection = DirectionToTarget();
        if (CanMoveSafely(moveDirection, approachSpeed))
        {
            EnterState(BossState.Approach, RandomDecisionInterval());
            return;
        }

        EnterState(BossState.Idle, RandomShortIdle());
    }

    private void UpdateRunMode(float distance)
    {
        if (distance >= runStartDistance)
        {
            isRunMode = true;
        }
        else if (distance <= runStopDistance)
        {
            isRunMode = false;
        }
    }

    private void UpdateCombatZone(float distance)
    {
        UpdateRunMode(distance);

        if (distance > combatExitRange)
        {
            isChaseMode = true;
            combatPhase = CombatPhase.Chase;
            return;
        }

        if (distance <= combatEnterRange && isChaseMode)
        {
            isChaseMode = false;
            BeginCombatRecovery(CanStartRangedRotation(distance) ? CombatPhase.Ranged : CombatPhase.Melee, 0.25f, 0.45f);
            return;
        }

        if (combatPhase == CombatPhase.Chase && !isChaseMode)
        {
            BeginRangedRotation();
        }
    }

    private void UpdateCombatRecovery(float distance)
    {
        StopHorizontalImmediately();

        if (distance > combatExitRange)
        {
            isChaseMode = true;
            combatPhase = CombatPhase.Chase;
            ChooseChaseAction(distance);
            return;
        }

        if (combatPhaseTimer > 0f)
        {
            if (state != BossState.Idle)
            {
                EnterState(BossState.Idle, RandomShortIdle());
            }

            return;
        }

        if (nextCombatPhase == CombatPhase.Ranged && CanStartRangedRotation(distance))
        {
            BeginRangedRotation();
            ChooseRangedRotationAction(distance);
            return;
        }

        BeginMeleeRotation();
        ChooseMeleeRotationAction(distance);
    }

    private void BeginCombatRecovery(CombatPhase nextPhase, float minDuration, float maxDuration)
    {
        combatPhase = CombatPhase.Recovery;
        nextCombatPhase = nextPhase;
        combatPhaseTimer = Random.Range(minDuration, Mathf.Max(minDuration, maxDuration));
        StopHorizontalImmediately();
        EnterState(BossState.Idle, RandomShortIdle());
    }

    private void BeginRangedRotation()
    {
        combatPhase = CombatPhase.Ranged;
        nextCombatPhase = CombatPhase.Melee;
        usedRangedSkillMask = 0;
        rangedSkillsUsedThisCycle = 0;
    }

    private void BeginMeleeRotation()
    {
        combatPhase = CombatPhase.Melee;
        nextCombatPhase = CombatPhase.Ranged;
    }

    private void FinishRangedRotation()
    {
        rangedRotationCooldownTimer = rangedRotationCooldown;
        BeginCombatRecovery(CombatPhase.Melee, rangedRotationRestMin, rangedRotationRestMax);
    }

    private void FinishMeleeRotation()
    {
        meleeRotationCooldownTimer = meleeRotationCooldown;
        CombatPhase nextPhase = CanStartRangedRotation(DistanceToTarget()) ? CombatPhase.Ranged : CombatPhase.Melee;
        BeginCombatRecovery(nextPhase, meleeRotationRestMin, meleeRotationRestMax);
    }

    private bool CanStartRangedRotation(float distance)
    {
        return rangedRotationCooldownTimer <= 0f && HasReadyRangedRotationSkill(distance);
    }

    private bool HasReadyRangedRotationSkill(float distance)
    {
        return GetRangedSkillScore(BossSkillAction.JumpSlam, distance) > 0f ||
            GetRangedSkillScore(BossSkillAction.SwordThrow, distance) > 0f ||
            GetRangedSkillScore(BossSkillAction.DashSlash, distance) > 0f ||
            GetRangedSkillScore(BossSkillAction.JumpSlash, distance) > 0f ||
            GetRangedSkillScore(BossSkillAction.Shockwave, distance) > 0f;
    }

    private bool IsRangedSkillUsedThisCycle(BossSkillAction skill)
    {
        int mask = SkillMask(skill);
        return mask != 0 && (usedRangedSkillMask & mask) != 0;
    }

    private bool CanSelectSkillInCurrentRotation(BossSkillAction skill)
    {
        return combatPhase != CombatPhase.Ranged || !IsRangedSkillUsedThisCycle(skill);
    }

    private int SkillMask(BossSkillAction skill)
    {
        return skill == BossSkillAction.None ? 0 : 1 << ((int)skill - 1);
    }

    private bool IsInSkillRange(float distance)
    {
        return distance >= skillRangeMin && distance <= skillRangeMax;
    }

    private bool TryChooseAirborneAction(float distance)
    {
        if (CanSelectSkillInCurrentRotation(BossSkillAction.JumpSlash) && TryStartJumpAttack())
        {
            RegisterSkillUse(BossSkillAction.JumpSlash);
            return true;
        }

        if (CanSelectSkillInCurrentRotation(BossSkillAction.JumpSlam) && TryStartJumpSlam())
        {
            RegisterSkillUse(BossSkillAction.JumpSlam);
            return true;
        }

        if (CanSelectSkillInCurrentRotation(BossSkillAction.SwordThrow) && TryStartSwordThrow())
        {
            RegisterSkillUse(BossSkillAction.SwordThrow);
            return true;
        }

        return TryChooseAntiAirFallback(distance);
    }

    private bool TryChooseSkillRangeAction(float distance)
    {
        BossSkillAction bestSkill = BossSkillAction.None;
        float bestScore = 0f;
        ScoreSkill(BossSkillAction.JumpSlam, GetRangedSkillScore(BossSkillAction.JumpSlam, distance), ref bestSkill, ref bestScore);
        ScoreSkill(BossSkillAction.SwordThrow, GetRangedSkillScore(BossSkillAction.SwordThrow, distance), ref bestSkill, ref bestScore);
        ScoreSkill(BossSkillAction.DashSlash, GetRangedSkillScore(BossSkillAction.DashSlash, distance), ref bestSkill, ref bestScore);
        ScoreSkill(BossSkillAction.JumpSlash, GetRangedSkillScore(BossSkillAction.JumpSlash, distance), ref bestSkill, ref bestScore);
        ScoreSkill(BossSkillAction.Shockwave, GetRangedSkillScore(BossSkillAction.Shockwave, distance), ref bestSkill, ref bestScore);

        switch (bestSkill)
        {
            case BossSkillAction.JumpSlam:
                if (TryStartJumpSlam())
                {
                    RegisterSkillUse(bestSkill);
                    return true;
                }

                break;
            case BossSkillAction.SwordThrow:
                if (TryStartSwordThrow())
                {
                    RegisterSkillUse(bestSkill);
                    return true;
                }

                break;
            case BossSkillAction.DashSlash:
                if (TryEnterDashAttack())
                {
                    RegisterSkillUse(bestSkill);
                    return true;
                }

                break;
            case BossSkillAction.JumpSlash:
                if (TryStartJumpAttack())
                {
                    RegisterSkillUse(bestSkill);
                    return true;
                }

                break;
            case BossSkillAction.Shockwave:
                StartRangedPressure();
                RegisterSkillUse(bestSkill);
                return true;
        }

        return false;
    }

    private void ScoreSkill(BossSkillAction skill, float score, ref BossSkillAction bestSkill, ref float bestScore)
    {
        if (score <= 0f)
        {
            return;
        }

        if (combatPhase == CombatPhase.Ranged && IsRangedSkillUsedThisCycle(skill))
        {
            return;
        }

        if (skill == lastSkillAction)
        {
            if (sameSkillInRow >= maxSameSkillInRow)
            {
                return;
            }

            score *= 1f - repeatedSkillPenalty;
        }

        score += Random.Range(0f, 0.12f);
        if (score > bestScore)
        {
            bestScore = score;
            bestSkill = skill;
        }
    }

    private float GetRangedSkillScore(BossSkillAction skill, float distance)
    {
        if (combatPhase == CombatPhase.Ranged && IsRangedSkillUsedThisCycle(skill))
        {
            return 0f;
        }

        switch (skill)
        {
            case BossSkillAction.JumpSlam:
                return ScoreJumpSlam(distance);
            case BossSkillAction.SwordThrow:
                return ScoreSwordThrow(distance);
            case BossSkillAction.DashSlash:
                return ScoreDashSlash(distance);
            case BossSkillAction.JumpSlash:
                return ScoreJumpSlash(distance);
            case BossSkillAction.Shockwave:
                return ScoreShockwave(distance);
            default:
                return 0f;
        }
    }

    private float ScoreShockwave(float distance)
    {
        return CanUseRangedPressure(distance) ? 0.5f + recentRangedHits * 0.15f : 0f;
    }

    private float ScoreSwordThrow(float distance)
    {
        if (globalSkillCooldownTimer > 0f || swordThrowCooldownTimer > 0f || strongSkillCooldownTimer > 0f ||
            swordThrowProjectilePrefab == null)
        {
            return 0f;
        }

        float airborneBonus = IsTargetAirborne() ? 0.35f : 0f;
        return distance >= skillRangeMin && distance <= skillRangeMax ? 0.65f + airborneBonus : 0f;
    }

    private float ScoreDashSlash(float distance)
    {
        if (dashAttackCooldownTimer > 0f || distance < dashAttackMinRange || distance > dashAttackMaxRange)
        {
            return 0f;
        }

        return IsPlayerDashingAway() ? 0.95f : 0.55f;
    }

    private float ScoreJumpSlash(float distance)
    {
        if (!IsTargetAirborne() || jumpAttackCooldownTimer > 0f)
        {
            return 0f;
        }

        return distance >= jumpAttackMinRange && distance <= jumpAttackMaxRange ? 0.9f : 0f;
    }

    private float ScoreJumpSlam(float distance)
    {
        if (globalSkillCooldownTimer > 0f || jumpSlamCooldownTimer > 0f || strongSkillCooldownTimer > 0f ||
            !isGrounded || !HasJumpSlamClearance())
        {
            return 0f;
        }

        float airborneBonus = IsTargetAirborne() || PlayerFrequentlyJumps ? 0.35f : 0f;
        return distance >= skillRangeMin && distance <= skillRangeMax ? 0.55f + airborneBonus : 0f;
    }

    private void RegisterSkillUse(BossSkillAction skill)
    {
        if (skill == BossSkillAction.None)
        {
            return;
        }

        sameSkillInRow = skill == lastSkillAction ? sameSkillInRow + 1 : 1;
        lastSkillAction = skill;
        currentSkillAction = skill;
        globalSkillCooldownTimer = globalSkillCooldown;

        if (combatPhase == CombatPhase.Ranged)
        {
            int mask = SkillMask(skill);
            if (mask != 0 && (usedRangedSkillMask & mask) == 0)
            {
                usedRangedSkillMask |= mask;
                rangedSkillsUsedThisCycle++;
            }
        }

        if (skill == BossSkillAction.JumpSlam || skill == BossSkillAction.SwordThrow)
        {
            strongSkillCooldownTimer = strongSkillCooldown;
        }
    }

    private bool TryChooseAdaptiveCounter(float distance)
    {
        if (adaptiveCounterCooldownTimer > 0f || IsHardLocked)
        {
            return false;
        }

        if (IsTargetAirborne())
        {
            if (PlayerFrequentlyJumps && TryStartDashJumpAttack())
            {
                StartAdaptiveCounterCooldown();
                return true;
            }

            if (TryStartJumpAttack())
            {
                StartAdaptiveCounterCooldown();
                return true;
            }

            return TryChooseAntiAirFallback(distance);
        }

        if (PlayerFrequentlyDashesThrough && TryCounterDashCross(distance))
        {
            StartAdaptiveCounterCooldown();
            return true;
        }

        if (PlayerFrequentlyDashes && TryChooseDashSpamParry(distance))
        {
            StartAdaptiveCounterCooldown();
            return true;
        }

        if (IsPlayerDashingAway() && distance >= dashAttackMinRange && distance <= dashAttackMaxRange && TryEnterDashAttack())
        {
            StartAdaptiveCounterCooldown();
            return true;
        }

        return false;
    }

    private bool TryChooseAntiAirFallback(float distance)
    {
        if (!IsTargetAirborne())
        {
            return false;
        }

        if (CanUseRangedPressure(distance))
        {
            StartRangedPressure();
            return true;
        }

        MoveTowardPredictedLandingPosition();
        return true;
    }

    private void MoveTowardPredictedLandingPosition()
    {
        Vector2 predictedPosition = PredictPlayerPosition(0.28f);
        int direction = predictedPosition.x >= transform.position.x ? 1 : -1;
        moveDirection = direction;
        EnterState(BossState.Approach, RandomShortIdle());
    }

    private bool TryCounterDashCross(float distance)
    {
        if (turnSlashCooldownTimer > 0f ||
            attackCooldownTimer > 0f ||
            dashCrossCounterTimer > 0f ||
            distance > comboContinueRange)
        {
            return false;
        }

        StartSingleAttack(BossState.Attack, AnimationSpine.State.SpecialAttack, turnSlashDamageMultiplier, attackAnimationDuration);
        turnSlashCooldownTimer = turnSlashCooldown;
        return true;
    }

    private bool TryChooseDashSpamParry(float distance)
    {
        if (dashSpamTimer <= 0f ||
            target == null ||
            distance > parryCounterRange ||
            consecutiveDefenses >= Mathf.Max(1, maxConsecutiveDefenses) ||
            !CanParry())
        {
            return false;
        }

        float chance = Mathf.Clamp01(parryChance + dashSpamParryBonus + (IsPhaseTwo ? phaseTwoParryChanceBonus : 0f));
        if (Random.value > Mathf.Min(chance, 0.75f))
        {
            return false;
        }

        RegisterDefenseUse();
        StartParry();
        return true;
    }

    private bool TryChooseAntiKiteAction(float distance)
    {
        bool pressured = targetOutOfRangeTimer >= outOfMeleePressureTime ||
            recentRangedHits >= 2 ||
            playerEscapeCount >= 2;

        if (!pressured && distance < farDistance)
        {
            return false;
        }

        if (CanUseRangedPressure(distance) && Random.value < RangedPressureChance())
        {
            StartRangedPressure();
            return true;
        }

        if (distance <= dashAttackMaxRange && TryEnterDashAttack())
        {
            return true;
        }

        if (IsWithinGapCloseDashRange(distance) && CanDash() && TryEnterDash(DirectionToTarget(), DashMode.GapClose))
        {
            return true;
        }

        return false;
    }

    private bool TryChooseParry(float distance)
    {
        if (target == null ||
            distance > parryCounterRange ||
            consecutiveDefenses >= Mathf.Max(1, maxConsecutiveDefenses) ||
            !CanParry())
        {
            return false;
        }

        float chance = parryChance + (IsPhaseTwo ? phaseTwoParryChanceBonus : 0f);
        if (PlayerFrequentlyDashes)
        {
            chance += dashSpamParryBonus;
        }

        chance = Mathf.Min(Mathf.Clamp01(chance), 0.75f);
        if (Random.value > chance)
        {
            return false;
        }

        RegisterDefenseUse();
        StartParry();
        return true;
    }

    private bool TryChooseTurnSlash(float distance)
    {
        if (targetBehindTimer < targetBehindDuration ||
            turnSlashCooldownTimer > 0f ||
            attackCooldownTimer > 0f ||
            distance > comboContinueRange)
        {
            return false;
        }

        StartSingleAttack(BossState.Attack, AnimationSpine.State.SpecialAttack, turnSlashDamageMultiplier, attackAnimationDuration);
        turnSlashCooldownTimer = turnSlashCooldown;
        return true;
    }
}
