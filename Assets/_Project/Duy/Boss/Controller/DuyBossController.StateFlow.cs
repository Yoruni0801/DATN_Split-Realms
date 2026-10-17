using UnityEngine;

public partial class DuyBossController
{
    private void UpdateState()
    {
        switch (state)
        {
            case BossState.Idle:
                desiredHorizontalVelocity = 0f;
                PlayMoveAnimation(false);
                break;
            case BossState.Chase:
                MoveInDirection(DirectionToTarget(), chaseSpeed);
                PlayMoveAnimation(true);
                break;
            case BossState.Approach:
                MoveInDirection(DirectionToTarget(), approachSpeed);
                PlayMoveAnimation(true);
                break;
            case BossState.Retreat:
                MoveInDirection(moveDirection, retreatSpeed);
                PlayMoveAnimation(true);
                if (stateTimer <= 0f)
                {
                    EnterState(BossState.Idle, RandomShortIdle());
                }
                break;
            case BossState.Dash:
                UpdateDashState();
                break;
            case BossState.Evade:
                UpdateEvadeState();
                break;
            case BossState.JumpEvade:
                UpdateJumpEvadeState();
                break;
            case BossState.JumpAttack:
                UpdateJumpAttackState();
                break;
            case BossState.DashAttack:
                UpdateDashAttackState();
                break;
            case BossState.Attack:
                UpdateSingleAttackState();
                break;
            case BossState.ComboAttack:
                UpdateComboAttackState();
                break;
            case BossState.Parry:
                UpdateParryState();
                break;
            case BossState.ParryCounter:
                UpdateParryCounterState();
                break;
            case BossState.RangedPressure:
                UpdateRangedPressureState();
                break;
            case BossState.SwordThrow:
                UpdateSwordThrowState();
                break;
            case BossState.JumpSlam:
                UpdateJumpSlamState();
                break;
            case BossState.AttackRecovery:
                desiredHorizontalVelocity = 0f;
                if (stateTimer <= 0f)
                {
                    EnterState(BossState.PostAttackPause, postAttackPauseDuration + postAttackRetreatDelay);
                }
                break;
            case BossState.PostAttackPause:
                StopHorizontalImmediately();
                if (stateTimer > 0f)
                {
                    break;
                }

                FinishPostAttackPause();
                break;
            case BossState.Hurt:
                desiredHorizontalVelocity = 0f;
                if (stateTimer <= 0f)
                {
                    if (isAggro && target != null && DistanceToTarget() > attackRange)
                    {
                        StartAggroApproach();
                        break;
                    }

                    EnterState(BossState.Idle, RandomShortIdle());
                }
                break;
            case BossState.Stun:
                desiredHorizontalVelocity = 0f;
                if (stateTimer <= 0f)
                {
                    if (isAggro && target != null && DistanceToTarget() > attackRange)
                    {
                        StartAggroApproach();
                        break;
                    }

                    EnterState(BossState.Idle, RandomDecisionInterval());
                }
                break;
        }
    }

    private void UpdateSingleAttackState()
    {
        UpdateAttackForwardMovement(attackMoveSpeed);
        if (stateTimer <= 0f)
        {
            BeginFinalPoseHold(normalAttackFinalPoseHold);
        }
    }

    private void UpdateComboAttackState()
    {
        if (waitingForComboStep)
        {
            StopHorizontalImmediately();
            if (comboGapTimer > 0f)
            {
                return;
            }

            BeginComboStep();
            return;
        }

        if (comboStepTimer <= 0f)
        {
            FinishComboStep();
            return;
        }

        float moveSpeed = comboIndex >= Mathf.Clamp(maxComboHits, 1, 3) - 1
            ? comboMoveSpeed * 0.35f
            : comboMoveSpeed;
        UpdateAttackForwardMovement(moveSpeed);
    }

    private void UpdateDashState()
    {
        PlayBossAnimation(AnimationSpine.State.Dash, false);

        if (dashPrepareTimer > 0f)
        {
            StopHorizontalImmediately();
            return;
        }

        if (!CanMoveSafely(lockedDashDirection, dashSpeed))
        {
            StopHorizontalImmediately();
            EnterState(BossState.Idle, RandomShortIdle());
            return;
        }

        desiredHorizontalVelocity = lockedDashDirection * dashSpeed * PhaseSpeedMultiplier();
        if (stateTimer <= 0f)
        {
            desiredHorizontalVelocity = 0f;
            if (queueJumpAttackAfterDash)
            {
                queueJumpAttackAfterDash = false;
                if (TryStartJumpAttack())
                {
                    return;
                }
            }

            if (dashMode == DashMode.GapClose &&
                DistanceToTarget() <= comboContinueRange &&
                attackCooldownTimer <= 0f &&
                comboCooldownTimer <= 0f)
            {
                StartComboAttack();
                return;
            }

            EnterState(BossState.Idle, RandomShortIdle());
        }
    }

    private void UpdateEvadeState()
    {
        PlayBossAnimation(AnimationSpine.State.Dash, false);
        desiredHorizontalVelocity = CanMoveSafely(lockedDashDirection, evadeSpeed)
            ? lockedDashDirection * evadeSpeed * PhaseSpeedMultiplier()
            : 0f;
        if (desiredHorizontalVelocity == 0f)
        {
            StopHorizontalImmediately();
        }

        if (stateTimer <= 0f)
        {
            FinishDefenseRecovery();
        }
    }

    private void UpdateJumpEvadeState()
    {
        if (!hasLeftGround)
        {
            hasLeftGround = !isGrounded || stateTimer <= 0f;
        }

        desiredHorizontalVelocity = HasWallAhead(jumpDirection, wallCheckDistance)
            ? 0f
            : jumpDirection * jumpForwardSpeed * PhaseSpeedMultiplier();

        if (!hasLeftGround)
        {
            PlayBossAnimation(AnimationSpine.State.Jump, false);
            return;
        }

        PlayBossAnimation(rb != null && rb.linearVelocity.y < -0.05f ? AnimationSpine.State.Fall : AnimationSpine.State.Jump, true);
        if (isGrounded && stateTimer <= 0f)
        {
            horizontalVelocity = 0f;
            desiredHorizontalVelocity = 0f;
            FinishDefenseRecovery(jumpRecoveryDuration);
        }
    }

    private bool TryStartDashJumpAttack()
    {
        float distance = DistanceToTarget();
        if (!CanDash() ||
            !CanPrepareJumpAttack() ||
            distance <= jumpAttackMaxRange ||
            distance > gapCloseDashRange ||
            !CanMoveSafely(DirectionToTarget(), dashSpeed))
        {
            return false;
        }

        queueJumpAttackAfterDash = true;
        return TryEnterDash(DirectionToTarget(), DashMode.GapClose);
    }

    private bool TryStartJumpAttack()
    {
        if (!CanStartJumpAttack())
        {
            return false;
        }

        Vector2 predictedPosition = PredictPlayerPosition(0.22f);
        int direction = predictedPosition.x >= transform.position.x ? 1 : -1;
        if (direction == 0)
        {
            direction = DirectionToTarget();
        }

        CancelAttackHitbox();
        lockedAttackDirection = direction;
        jumpDirection = direction;
        FaceDirection(lockedAttackDirection);
        currentAttackDamage = Mathf.RoundToInt(attackDamage * jumpAttackDamageMultiplier);
        BeginAttackHitboxFallback(jumpAttackHitboxDelay, jumpAttackHitboxDuration);
        jumpAttackCooldownTimer = jumpAttackCooldown * (IsPhaseTwo ? phaseTwoDashCooldownMultiplier : 1f);
        hasLeftGround = false;
        horizontalVelocity = direction * jumpAttackHorizontalSpeed * PhaseSpeedMultiplier();
        desiredHorizontalVelocity = horizontalVelocity;
        rb.linearVelocity = new Vector2(horizontalVelocity, jumpAttackForce);
        EnterState(BossState.JumpAttack, minimumAirTime);
        PlayBossAnimation(AnimationSpine.State.Jump, false, true);
        return true;
    }

    private bool CanPrepareJumpAttack()
    {
        return target != null &&
            jumpAttackCooldownTimer <= 0f &&
            !IsHardLocked &&
            isGrounded &&
            IsTargetAirborne() &&
            HasJumpClearance();
    }

    private bool CanStartJumpAttack()
    {
        if (!CanPrepareJumpAttack())
        {
            return false;
        }

        float distance = DistanceToTarget();
        if (distance < jumpAttackMinRange || distance > jumpAttackMaxRange)
        {
            return false;
        }

        if (playerRb != null && playerRb.linearVelocity.y < -0.5f && playerAirborneTimer < playerAirborneMinDuration + 0.1f)
        {
            return false;
        }

        int direction = PredictPlayerPosition(0.22f).x >= transform.position.x ? 1 : -1;
        return HasSafeJumpAttackLanding(direction);
    }

    private void UpdateJumpAttackState()
    {
        if (!hasLeftGround)
        {
            hasLeftGround = !isGrounded || stateTimer <= 0f;
        }

        UpdateJumpAttackHorizontalTracking();

        bool shouldPlaySlash = attackHitboxStartTimer <= 0f || attackHitboxEnabled;
        if (shouldPlaySlash)
        {
            PlayBossAnimation(AnimationSpine.State.SpecialAttack, false);
        }
        else if (!hasLeftGround)
        {
            PlayBossAnimation(AnimationSpine.State.Jump, false);
        }
        else if (rb != null && rb.linearVelocity.y < -0.05f)
        {
            PlayBossAnimation(AnimationSpine.State.Fall, true);
        }

        if (isGrounded && hasLeftGround && stateTimer <= 0f)
        {
            FinishJumpAttack();
        }
    }

    private void UpdateJumpAttackHorizontalTracking()
    {
        if (target == null || rb == null)
        {
            return;
        }

        if (attackHitboxEnabled)
        {
            desiredHorizontalVelocity = lockedAttackDirection * jumpAttackHorizontalSpeed * 0.35f * PhaseSpeedMultiplier();
            return;
        }

        float deltaX = target.position.x - transform.position.x;
        if (Mathf.Abs(deltaX) <= faceDeadZone)
        {
            return;
        }

        int desiredDirection = deltaX > 0f ? 1 : -1;
        float desiredX = desiredDirection * jumpAttackHorizontalSpeed * PhaseSpeedMultiplier();
        float correctionStep = jumpAttackTrackingStrength * jumpAttackMaxHorizontalCorrection * Time.deltaTime * 10f;
        float correctedX = Mathf.MoveTowards(horizontalVelocity, desiredX, correctionStep);
        desiredHorizontalVelocity = correctedX;

        if (!attackHitboxEnabled)
        {
            lockedAttackDirection = desiredDirection;
            FaceDirection(lockedAttackDirection);
        }
    }

    private void FinishJumpAttack()
    {
        DisableAttackHitbox();
        StopHorizontalImmediately();
        BeginFinalPoseHold(jumpAttackLandingRecovery);
    }

    private void UpdateDashAttackState()
    {
        PlayBossAnimation(AnimationSpine.State.Dash, false);

        if (dashPrepareTimer > 0f)
        {
            StopHorizontalImmediately();
            return;
        }

        if (!CanMoveSafely(lockedDashDirection, dashAttackSpeed))
        {
            StopHorizontalImmediately();
            DisableAttackHitbox();
            EnterState(BossState.AttackRecovery, dashAttackRecoveryDuration);
            return;
        }

        desiredHorizontalVelocity = lockedDashDirection * dashAttackSpeed * PhaseSpeedMultiplier();

        if (!attackHitboxEnabled && !dashAttackHitboxSpent)
        {
            dashAttackHitboxTimer -= Time.deltaTime;
            if (dashAttackHitboxTimer <= 0f)
            {
                EnableAttackHitbox();
            }
        }
        else
        {
            attackHitboxTimer -= Time.deltaTime;
            if (attackHitboxTimer <= 0f)
            {
                dashAttackHitboxSpent = true;
                DisableAttackHitbox();
            }
        }

        if (stateTimer <= 0f)
        {
            BeginFinalPoseHold(dashAttackFinalPoseHold);
        }
    }

    private void UpdateParryState()
    {
        desiredHorizontalVelocity = 0f;
        if (stateTimer <= 0f)
        {
            consecutiveParries = 0;
            EnterState(BossState.AttackRecovery, attackRecoveryDuration * 0.65f);
            PlayBossAnimation(AnimationSpine.State.ParryFail, false, true);
        }
    }

    private void UpdateParryCounterState()
    {
        desiredHorizontalVelocity = 0f;
        if (parryCounterTimer <= 0f && !attackHitboxEnabled)
        {
            lockedAttackDirection = DirectionToTarget();
            FaceDirection(lockedAttackDirection);
            currentAttackDamage = Mathf.RoundToInt(attackDamage * parryCounterDamageMultiplier);
            BeginAttackHitboxFallback(0f, attackHitboxActiveDuration);
            PlayBossAnimation(AnimationSpine.State.NormalAttack1, false, true);
        }

        if (stateTimer <= 0f)
        {
            BeginFinalPoseHold(parryCounterFinalPoseHold);
        }
    }

    private void UpdateRangedPressureState()
    {
        desiredHorizontalVelocity = 0f;

        if (!shockwaveSpawned && rangedPressureSpawnTimer <= 0f)
        {
            ReleaseRangedPressure();
        }

        if (IsPhaseTwo && shockwaveSpawned && !secondShockwaveSpawned && secondShockwaveTimer <= 0f)
        {
            SpawnShockwave();
            secondShockwaveSpawned = true;
        }

        if (stateTimer <= 0f)
        {
            EnterState(BossState.AttackRecovery, rangedPressureRecoveryDuration);
        }
    }

    private void UpdateSwordThrowState()
    {
        StopHorizontalImmediately();
        if (!swordReleased && swordThrowReleaseTimer <= 0f)
        {
            ReleaseSwordThrow();
        }

        if (swordReleased && swordRecoverTimer <= 0f)
        {
            RestoreSwordVisual();
        }

        if (stateTimer <= 0f)
        {
            RestoreSwordVisual();
            EnterState(BossState.AttackRecovery, swordThrowRecoveryDuration);
        }
    }

    private void UpdateJumpSlamState()
    {
        switch (jumpSlamPhase)
        {
            case JumpSlamPhase.Prepare:
                StopHorizontalImmediately();
                if (jumpSlamPhaseTimer <= 0f)
                {
                    jumpSlamPhase = JumpSlamPhase.Ascend;
                    jumpSlamAscendElapsed = 0f;
                    rb.linearVelocity = new Vector2(jumpDirection * jumpSlamForwardSpeed, jumpSlamAscendForce);
                    horizontalVelocity = rb.linearVelocity.x;
                    desiredHorizontalVelocity = horizontalVelocity;
                    if (debugBossState)
                    {
                        Debug.Log($"[JUMP SLAM] Launch | targetX={jumpSlamTargetPosition.x:0.00} | verticalForce={jumpSlamAscendForce:0.00} | horizontalSpeed={jumpSlamForwardSpeed:0.00}", this);
                    }
                }
                break;

            case JumpSlamPhase.Ascend:
                jumpSlamPhase = JumpSlamPhase.Tracking;
                break;

            case JumpSlamPhase.Tracking:
                TrackJumpSlamTarget();
                jumpSlamAscendElapsed += Time.deltaTime;
                if (!hasLeftGround && !isGrounded)
                {
                    hasLeftGround = true;
                    if (debugBossState)
                    {
                        Debug.Log("[JUMP SLAM] Left Ground", this);
                    }
                }

                if (ShouldLockJumpSlamTarget())
                {
                    LockJumpSlamTarget();
                    jumpSlamPhase = JumpSlamPhase.TargetLock;
                    jumpSlamPhaseTimer = jumpSlamApexHoldDuration;
                    desiredHorizontalVelocity = 0f;
                    horizontalVelocity = 0f;
                    if (rb != null)
                    {
                        rb.linearVelocity = new Vector2(0f, Mathf.Min(rb.linearVelocity.y, 0f));
                    }

                    if (debugBossState)
                    {
                        float height = transform.position.y - jumpSlamStartPosition.y;
                        Debug.Log($"[JUMP SLAM] Target Locked | position={jumpSlamTargetPosition} | height={height:0.00}", this);
                    }
                }
                break;

            case JumpSlamPhase.TargetLock:
                desiredHorizontalVelocity = 0f;
                horizontalVelocity = 0f;
                if (rb != null)
                {
                    rb.linearVelocity = new Vector2(0f, Mathf.Min(rb.linearVelocity.y, 0f));
                }

                if (jumpSlamPhaseTimer <= 0f)
                {
                    jumpSlamPhase = JumpSlamPhase.Dive;
                    PlayBossAnimation(AnimationSpine.State.Fall, true);
                    if (debugBossState)
                    {
                        float height = transform.position.y - jumpSlamStartPosition.y;
                        Debug.Log($"[JUMP SLAM] Dive Started | currentHeight={height:0.00}", this);
                    }
                }
                break;

            case JumpSlamPhase.Dive:
                DiveTowardJumpSlamTarget();
                if (hasLeftGround && isGrounded && !jumpSlamImpacted)
                {
                    DoJumpSlamImpact();
                    jumpSlamPhase = JumpSlamPhase.Impact;
                    stateTimer = jumpSlamRecoveryDuration;
                }
                break;

            case JumpSlamPhase.Impact:
                jumpSlamPhase = JumpSlamPhase.Recovery;
                break;

            case JumpSlamPhase.Recovery:
                StopHorizontalImmediately();
                if (stateTimer <= 0f)
                {
                    BeginFinalPoseHold(jumpSlamRecoveryDuration);
                }
                break;
        }
    }

    private void TrackJumpSlamTarget()
    {
        if (target == null || rb == null)
        {
            return;
        }

        Vector2 predictedTarget = ClampJumpSlamTarget(PredictPlayerPosition(jumpSlamPredictionTime));
        if (HasSafeJumpSlamLanding(predictedTarget))
        {
            jumpSlamTargetPosition = predictedTarget;
        }

        float deltaX = jumpSlamTargetPosition.x - transform.position.x;
        if (Mathf.Abs(deltaX) <= faceDeadZone)
        {
            return;
        }

        int direction = deltaX > 0f ? 1 : -1;
        float desiredX = direction * jumpSlamTrackingSpeed * PhaseSpeedMultiplier();
        desiredHorizontalVelocity = Mathf.MoveTowards(
            desiredHorizontalVelocity,
            desiredX,
            jumpSlamTrackingStrength * jumpSlamTrackingSpeed * Time.deltaTime);
        FaceDirection(direction);
    }

    private bool ShouldLockJumpSlamTarget()
    {
        if (rb == null)
        {
            return true;
        }

        float height = transform.position.y - jumpSlamStartPosition.y;
        bool reachedMinimumTime = jumpSlamAscendElapsed >= jumpSlamMinAscendTime;
        bool reachedMinimumHeight = height >= jumpSlamMinHeightBeforeDive;
        bool nearApex = rb.linearVelocity.y <= 0.75f;
        bool falling = rb.linearVelocity.y <= 0f;
        bool timedOut = jumpSlamAscendElapsed >= jumpSlamMaxAscendTime;

        return timedOut || (reachedMinimumTime && reachedMinimumHeight && (nearApex || falling));
    }

    private void LockJumpSlamTarget()
    {
        Vector2 predictedTarget = ClampJumpSlamTarget(PredictPlayerPosition(jumpSlamPredictionTime));
        if (HasSafeJumpSlamLanding(predictedTarget))
        {
            jumpSlamTargetPosition = predictedTarget;
        }
    }

    private void DiveTowardJumpSlamTarget()
    {
        if (rb == null)
        {
            return;
        }

        float deltaX = jumpSlamTargetPosition.x - transform.position.x;
        int direction = Mathf.Abs(deltaX) > faceDeadZone ? (deltaX > 0f ? 1 : -1) : 0;
        float xVelocity = direction * jumpSlamDiveHorizontalSpeed * PhaseSpeedMultiplier();
        rb.linearVelocity = new Vector2(xVelocity, -jumpSlamDiveSpeed);
        horizontalVelocity = xVelocity;
        desiredHorizontalVelocity = xVelocity;
    }

}
