using UnityEngine;

public partial class DuyBossController
{
    private void StartComboAttack()
    {
        if (comboCooldownTimer > 0f)
        {
            return;
        }

        BeginMeleeRotation();
        comboIndex = 0;
        waitingForComboStep = false;
        attackCooldownTimer = attackCooldown;
        poiseHitCount = 0;
        EnterState(BossState.ComboAttack, comboStepDuration);
        BeginComboStep();
    }

    private void BeginComboStep()
    {
        waitingForComboStep = false;
        CancelAttackHitbox();
        lockedAttackDirection = DirectionToTarget();
        FaceDirection(lockedAttackDirection);
        attackStartX = transform.position.x;
        comboStepTimer = comboStepDuration;
        comboGapTimer = 0f;
        stateTimer = comboStepDuration;
        float damageMultiplier = comboIndex >= 2 ? comboFinisherDamageMultiplier : 1f;
        currentAttackDamage = Mathf.RoundToInt(attackDamage * damageMultiplier);
        BeginAttackHitboxFallback(attackWindupDuration, attackHitboxActiveDuration);
        PlayBossAnimation(ComboAnimationForIndex(comboIndex), false, true);
    }

    private void FinishComboStep()
    {
        DisableAttackHitbox();
        StopHorizontalImmediately();

        bool isFinisher = comboIndex >= Mathf.Clamp(maxComboHits, 1, 3) - 1;
        if (isFinisher || !ShouldContinueCombo())
        {
            comboCooldownTimer = comboCooldown + (isFinisher ? comboFinisherRecoveryDuration : 0f);
            BeginFinalPoseHold(isFinisher ? comboFinisherFinalPoseHold : normalAttackFinalPoseHold);
            return;
        }

        comboIndex++;
        waitingForComboStep = true;
        comboGapTimer = comboStepFinalPoseHold;
        stateTimer = comboStepFinalPoseHold;
    }

    private bool ShouldContinueCombo()
    {
        if (comboIndex >= Mathf.Clamp(maxComboHits, 1, 3) - 1)
        {
            return false;
        }

        if (target == null || DistanceToTarget() > comboContinueRange)
        {
            return false;
        }

        float chance = comboContinueChance + playerEscapeCount * 0.08f + (IsPhaseTwo ? 0.15f : 0f);
        return Random.value < Mathf.Clamp01(chance);
    }

    private void StartSingleAttack(BossState attackState, AnimationSpine.State animationState, float damageMultiplier, float duration)
    {
        attackCooldownTimer = attackCooldown;
        lockedAttackDirection = DirectionToTarget();
        FaceDirection(lockedAttackDirection);
        attackStartX = transform.position.x;
        currentAttackDamage = Mathf.RoundToInt(attackDamage * damageMultiplier);
        CancelAttackHitbox();
        BeginAttackHitboxFallback(attackWindupDuration, attackHitboxActiveDuration);
        EnterState(attackState, duration);
        PlayBossAnimation(animationState, false, true);
    }

    private void StartParry()
    {
        CancelAttackHitbox();
        parryCooldownTimer = parryCooldown;
        desiredHorizontalVelocity = 0f;
        horizontalVelocity = 0f;
        EnterState(BossState.Parry, parryDuration);
        PlayBossAnimation(AnimationSpine.State.ParryStart, false, true);
    }

    private void StartRangedPressure()
    {
        globalSkillCooldownTimer = Mathf.Max(globalSkillCooldownTimer, globalSkillCooldown);
        rangedPressureCooldownTimer = rangedPressureCooldown;
        consecutiveRangedPressure++;
        recentRangedHits = 0;
        shockwaveSpawned = false;
        secondShockwaveSpawned = false;
        rangedPressureSpawnTimer = rangedPressurePrepareDuration;
        secondShockwaveTimer = rangedPressurePrepareDuration + secondShockwaveDelay;
        FaceDirection(DirectionToTarget());
        EnterState(BossState.RangedPressure, rangedPressurePrepareDuration + rangedPressureRecoveryDuration);
        PlayBossAnimation(AnimationSpine.State.Range, false, true);
    }

    private bool TryEnterDashAttack(int direction = 0)
    {
        int dashDirection = direction != 0 ? direction : DirectionToTarget();
        if (dashAttackCooldownTimer > 0f || IsHardLocked || !CanMoveSafely(dashDirection, dashAttackSpeed))
        {
            return false;
        }

        lockedDashDirection = dashDirection;
        lockedAttackDirection = lockedDashDirection;
        FaceDirection(lockedDashDirection);
        currentAttackDamage = Mathf.RoundToInt(attackDamage * dashAttackDamageMultiplier);
        CancelAttackHitbox();
        dashAttackCooldownTimer = dashAttackCooldown * (IsPhaseTwo ? phaseTwoDashCooldownMultiplier : 1f);
        dashPrepareTimer = dashAttackPrepareDuration;
        dashAttackHitboxTimer = dashAttackHitboxDelay;
        dashAttackHitboxSpent = false;
        attackHitboxTimer = dashAttackHitboxDuration;
        useFallbackAttackHitboxTimer = false;
        attackHitboxEnabled = false;
        EnterState(BossState.DashAttack, dashAttackPrepareDuration + dashAttackDuration);
        PlayBossAnimation(AnimationSpine.State.Dash, false, true);
        return true;
    }

    private bool TryEnterDash(int direction, DashMode mode)
    {
        float speed = mode == DashMode.Evade ? evadeSpeed : dashSpeed;
        if (!CanDash() || !CanMoveSafely(direction, speed))
        {
            return false;
        }

        dashMode = mode;
        lockedDashDirection = direction;
        FaceDirection(direction);
        dashPrepareTimer = mode == DashMode.GapClose ? dashPrepareDuration : 0f;
        dashCooldownTimer = EffectiveDashCooldown();
        if (mode == DashMode.Evade)
        {
            evadeCooldownTimer = evadeCooldown * (IsPhaseTwo ? phaseTwoDashCooldownMultiplier : 1f);
        }

        EnterState(mode == DashMode.Evade ? BossState.Evade : BossState.Dash, dashPrepareTimer + (mode == DashMode.Evade ? evadeDuration : dashDuration));
        if (dashPrepareTimer <= 0f)
        {
            desiredHorizontalVelocity = lockedDashDirection * (mode == DashMode.Evade ? evadeSpeed : dashSpeed) * PhaseSpeedMultiplier();
            horizontalVelocity = desiredHorizontalVelocity;
            rb.linearVelocity = new Vector2(horizontalVelocity, rb.linearVelocity.y);
        }

        return true;
    }

    private bool TryStartSwordThrow()
    {
        if (swordThrowProjectilePrefab == null ||
            globalSkillCooldownTimer > 0f ||
            swordThrowCooldownTimer > 0f ||
            IsHardLocked)
        {
            return false;
        }

        CancelAttackHitbox();
        StopHorizontalImmediately();
        FaceDirection(DirectionToTarget());
        swordReleased = false;
        swordThrowReleaseTimer = swordThrowReleaseFallbackTime;
        swordRecoverTimer = swordRecoverFallbackTime;
        swordThrowCooldownTimer = swordThrowCooldown * (IsPhaseTwo ? phaseTwoDashCooldownMultiplier : 1f);
        EnterState(BossState.SwordThrow, swordThrowPrepareDuration + swordThrowRecoveryDuration);
        PlayBossAnimation(AnimationSpine.State.Range, false, true);
        return true;
    }

    private bool TryStartJumpSlam()
    {
        if (globalSkillCooldownTimer > 0f ||
            jumpSlamCooldownTimer > 0f ||
            IsHardLocked ||
            !isGrounded ||
            !HasJumpSlamClearance())
        {
            return false;
        }

        int direction = DirectionToTarget();
        jumpSlamStartPosition = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 predictedLanding = ClampJumpSlamTarget(PredictPlayerPosition(jumpSlamPredictionTime));
        if (!HasSafeJumpSlamLanding(predictedLanding))
        {
            if (debugBossState)
            {
                Debug.Log("[JUMP SLAM] Cancelled | unsafe landing", this);
            }

            return false;
        }

        CancelAttackHitbox();
        jumpSlamPhase = JumpSlamPhase.Prepare;
        jumpSlamPhaseTimer = jumpSlamPrepareDuration;
        jumpSlamAscendElapsed = 0f;
        jumpSlamImpacted = false;
        hasLeftGround = false;
        jumpSlamTargetPosition = predictedLanding;
        jumpDirection = direction;
        lockedAttackDirection = direction;
        FaceDirection(direction);
        jumpSlamCooldownTimer = jumpSlamCooldown * (IsPhaseTwo ? phaseTwoDashCooldownMultiplier : 1f);
        EnterState(BossState.JumpSlam, jumpSlamPrepareDuration + jumpSlamMaxAscendTime + jumpSlamApexHoldDuration + jumpSlamRecoveryDuration + 1.2f);
        PlayBossAnimation(AnimationSpine.State.Jump, false, true);
        if (debugBossState)
        {
            Debug.Log($"[JUMP SLAM] Started | target={jumpSlamTargetPosition}", this);
        }

        return true;
    }

    private void BeginAttackHitboxFallback(float startup, float activeDuration)
    {
        attackHitboxStartTimer = startup;
        attackHitboxTimer = activeDuration;
        attackLungeTimer = 0f;
        slashVfxSpawned = false;
        attackHitboxEnabled = false;
        useFallbackAttackHitboxTimer = activeDuration > 0f;
    }

    public void EnableAttackHitbox()
    {
        if (!IsAttacking || weaponHitbox == null || attackHitboxEnabled)
        {
            return;
        }

        if (state == BossState.DashAttack && dashPrepareTimer > 0f)
        {
            return;
        }

        int damage = currentAttackDamage > 0 ? currentAttackDamage : attackDamage;
        weaponHitbox.BeginAttack(damage, transform, attackCanBeBlocked);
        attackHitboxEnabled = true;
        SpawnSlashVfx();
        if (!enablingHitboxFromFallback)
        {
            useFallbackAttackHitboxTimer = false;
        }

        if (attackHitboxTimer <= 0f)
        {
            attackHitboxTimer = state == BossState.DashAttack ? dashAttackHitboxDuration : attackHitboxActiveDuration;
        }

        attackLungeTimer = 0f;
    }

    public void DisableAttackHitbox()
    {
        weaponHitbox?.EndAttack();
        if (state == BossState.DashAttack)
        {
            dashAttackHitboxSpent = true;
        }

        attackHitboxEnabled = false;
        useFallbackAttackHitboxTimer = false;
        attackHitboxTimer = 0f;
        attackLungeTimer = 0f;
        slashVfxSpawned = false;
        if (state != BossState.DashAttack)
        {
            desiredHorizontalVelocity = 0f;
        }
    }

    private void SpawnSlashVfx()
    {
        if (slashVfxSpawned || slashVfxPrefab == null)
        {
            return;
        }

        Transform spawnTransform = slashVfxSpawnPoint != null
            ? slashVfxSpawnPoint
            : weaponHitbox != null
                ? weaponHitbox.transform
                : transform;
        Vector3 spawnPosition = spawnTransform.position;
        if (slashVfxSpawnPoint == null)
        {
            spawnPosition += new Vector3(slashVfxOffset.x * lockedAttackDirection, slashVfxOffset.y, 0f);
        }

        Quaternion rotation = spawnTransform.rotation * Quaternion.Euler(slashVfxRotationOffset);
        GameObject slash = Instantiate(slashVfxPrefab, spawnPosition, rotation);
        if (mirrorSlashVfxToFacing)
        {
            Vector3 scale = slash.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(lockedAttackDirection == 0 ? facingDirection : lockedAttackDirection);
            slash.transform.localScale = scale;
        }

        if (slashVfxLifetime > 0f)
        {
            Destroy(slash, slashVfxLifetime);
        }

        slashVfxSpawned = true;
    }

    private void HandleSpineEvent(Spine.TrackEntry trackEntry, Spine.Event spineEvent)
    {
        if (spineEvent == null || spineEvent.Data == null)
        {
            return;
        }

        string eventName = spineEvent.Data.Name;
        if (eventName == AttackHitboxOnEvent)
        {
            EnableAttackHitbox();
        }
        else if (eventName == AttackHitboxOffEvent)
        {
            DisableAttackHitbox();
        }
        else if (eventName == CastReleaseEvent && state == BossState.RangedPressure)
        {
            ReleaseRangedPressure();
        }
        else if (eventName == SwordReleaseEvent && state == BossState.SwordThrow)
        {
            ReleaseSwordThrow();
        }
        else if (eventName == SwordRecoverEvent && state == BossState.SwordThrow)
        {
            RestoreSwordVisual();
        }
        else if (eventName == JumpSlamImpactEvent && state == BossState.JumpSlam)
        {
            DoJumpSlamImpact();
        }
    }

    private void CancelAttackHitbox()
    {
        weaponHitbox?.CancelAttack();
        RestoreSwordVisual();
        attackHitboxEnabled = false;
        useFallbackAttackHitboxTimer = false;
        enablingHitboxFromFallback = false;
        queueJumpAttackAfterDash = false;
        waitingForComboStep = false;
        swordReleased = false;
        jumpSlamPhase = JumpSlamPhase.None;
        jumpSlamImpacted = false;
        jumpSlamAscendElapsed = 0f;
        slashVfxSpawned = false;
        attackHitboxStartTimer = 0f;
        attackHitboxTimer = 0f;
        attackLungeTimer = 0f;
        swordThrowReleaseTimer = 0f;
        swordRecoverTimer = 0f;
        jumpSlamPhaseTimer = 0f;
        dashAttackHitboxSpent = false;
        currentAttackDamage = 0;
        currentSkillAction = BossSkillAction.None;
    }

    private void ReleaseRangedPressure()
    {
        if (shockwaveSpawned)
        {
            return;
        }

        SpawnShockwave();
        shockwaveSpawned = true;
    }

    private void ReleaseSwordThrow()
    {
        if (swordReleased || swordThrowProjectilePrefab == null)
        {
            return;
        }

        swordReleased = true;
        if (swordVisual != null)
        {
            swordVisual.SetActive(false);
        }

        Transform spawnTransform = swordThrowPoint != null ? swordThrowPoint : transform;
        GameObject projectile = Instantiate(swordThrowProjectilePrefab, spawnTransform.position, Quaternion.identity);
        Vector2 predictedPosition = PredictPlayerPosition(0.22f);
        Vector2 direction = (predictedPosition - (Vector2)spawnTransform.position).normalized;
        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.right * DirectionToTarget();
        }

        Vector3 scale = projectile.transform.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(direction.x == 0f ? DirectionToTarget() : direction.x);
        projectile.transform.localScale = scale;

        BossThrownSwordProjectile swordProjectile = projectile.GetComponent<BossThrownSwordProjectile>();
        if (swordProjectile == null)
        {
            swordProjectile = projectile.AddComponent<BossThrownSwordProjectile>();
        }

        swordProjectile.Finished -= RestoreSwordVisual;
        swordProjectile.Finished += RestoreSwordVisual;
        swordProjectile.Launch(direction * swordThrowSpeed, swordThrowDamage, transform, playerLayer);
    }

    private void RestoreSwordVisual()
    {
        if (swordVisual != null)
        {
            swordVisual.SetActive(true);
        }
    }

    private void DoJumpSlamImpact()
    {
        if (jumpSlamImpacted)
        {
            return;
        }

        if (!hasLeftGround || !isGrounded)
        {
            return;
        }

        jumpSlamImpacted = true;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        horizontalVelocity = 0f;
        desiredHorizontalVelocity = 0f;
        PlayJumpSlamCameraShake();
        if (debugBossState)
        {
            Debug.Log("[JUMP SLAM] Impact", this);
        }

        ContactFilter2D contactFilter = new ContactFilter2D();
        contactFilter.SetLayerMask(playerLayer);
        contactFilter.useTriggers = Physics2D.queriesHitTriggers;
        int hitCount = Physics2D.OverlapCircle(transform.position, jumpSlamImpactRadius, contactFilter, areaHitResults);
        jumpSlamHitTargetIds.Clear();
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = areaHitResults[i];
            if (hit == null)
            {
                continue;
            }

            int targetId = hit.transform.root.GetInstanceID();
            if (!jumpSlamHitTargetIds.Add(targetId))
            {
                continue;
            }

            Vector2 direction = ((Vector2)hit.bounds.center - (Vector2)transform.position).normalized;
            PlayerHitResolver.TryApplyHitToPlayer(
                hit,
                jumpSlamDamage,
                ParryableAttackType.Melee,
                transform,
                direction.sqrMagnitude > 0.001f ? direction : Vector2.right * facingDirection,
                attackCanBeBlocked);
        }

        if (jumpSlamImpactVfx != null)
        {
            Instantiate(jumpSlamImpactVfx, transform.position, Quaternion.identity);
        }

        if (shockwavePrefab != null)
        {
            SpawnShockwave();
        }
    }

    private void PlayJumpSlamCameraShake()
    {
        if (jumpSlamCameraShakeDuration <= 0f ||
            jumpSlamCameraShakeMagnitude <= 0f ||
            CameraController.Instance == null)
        {
            return;
        }

        CameraController.Instance.ShakeCamera(jumpSlamCameraShakeDuration, jumpSlamCameraShakeMagnitude);
    }

    private void HandleAttackImpact(Collider2D hit)
    {
        if (state != BossState.DashAttack)
        {
            desiredHorizontalVelocity = 0f;
            horizontalVelocity = 0f;
        }

        OnAttackImpact();
    }

    private void OnAttackImpact()
    {
        // Hook for hit stop, camera shake or VFX.
    }

    private void SpawnShockwave()
    {
        if (shockwavePrefab == null)
        {
            return;
        }

        Transform spawnTransform = shockwaveSpawnPoint != null ? shockwaveSpawnPoint : transform;
        Vector3 spawnPosition = spawnTransform.position;
        Quaternion rotation = Quaternion.identity;
        GameObject shockwave = Instantiate(shockwavePrefab, spawnPosition, rotation);
        int direction = DirectionToTarget();
        Vector3 scale = shockwave.transform.localScale;
        scale.x = Mathf.Abs(scale.x) * direction;
        shockwave.transform.localScale = scale;

        BossShockwaveProjectile projectile = shockwave.GetComponent<BossShockwaveProjectile>();
        if (projectile != null)
        {
            projectile.Launch(direction, shockwaveSpeed, shockwaveDamage, transform, playerLayer);
            return;
        }

        Rigidbody2D projectileBody = shockwave.GetComponent<Rigidbody2D>();
        if (projectileBody != null)
        {
            projectileBody.linearVelocity = new Vector2(direction * shockwaveSpeed, projectileBody.linearVelocity.y);
        }
    }

    private void ChooseMediumRangeMovement()
    {
        float distance = DistanceToTarget();
        if (distance >= dashAttackMinRange && distance <= dashAttackMaxRange && Random.value < EffectiveDashChance())
        {
            if (TryEnterDashAttack())
            {
                return;
            }
        }

        if (CanUseRangedPressure(distance) && Random.value < 0.25f)
        {
            StartRangedPressure();
            return;
        }

        float roll = Random.value;
        if (roll < 0.55f)
        {
            moveDirection = DirectionToTarget();
            EnterState(BossState.Approach, RandomDecisionInterval());
            return;
        }

        if (roll < 0.75f)
        {
            EnterState(BossState.Idle, RandomShortIdle());
            return;
        }

        TryEnterRetreatOrIdle();
    }

    private void ChooseCloseRangeMovement()
    {
        float distance = DistanceToTarget();
        if (distance < tooCloseDistance && CanDash() && Random.value < evadeChance)
        {
            if (TryEnterDash(-DirectionToTarget(), DashMode.Evade))
            {
                return;
            }
        }

        if (distance < preferredDistance && TryEnterRetreatOrIdle())
        {
            return;
        }

        if (distance < preferredDistance && TryStartJumpEvade(-DirectionToTarget()))
        {
            return;
        }

        if (Random.value < 0.45f)
        {
            moveDirection = DirectionToTarget();
            EnterState(BossState.Approach, RandomShortIdle());
            return;
        }

        EnterState(BossState.Idle, RandomShortIdle());
    }

    private bool TryEnterRetreatOrIdle()
    {
        int retreatDirection = -DirectionToTarget();
        if (CanMoveSafely(retreatDirection, retreatSpeed))
        {
            moveDirection = retreatDirection;
            FaceDirection(retreatDirection);
            EnterState(BossState.Retreat, retreatDuration);
            return true;
        }

        EnterState(BossState.Idle, RandomShortIdle());
        return false;
    }

    private void BeginFinalPoseHold(float duration)
    {
        DisableAttackHitbox();
        StopHorizontalImmediately();
        EnterState(BossState.PostAttackPause, duration);
    }

    private void FinishPostAttackPause()
    {
        if (target == null)
        {
            EnterState(BossState.Idle, RandomDecisionInterval());
            PlayBossAnimation(AnimationSpine.State.Idle, true);
            return;
        }

        float distance = DistanceToTarget();
        UpdateCombatZone(distance);

        if (isChaseMode || distance > combatExitRange)
        {
            StartAggroApproach();
            combatPhase = CombatPhase.Chase;
            return;
        }

        if (combatPhase == CombatPhase.Ranged)
        {
            if (rangedSkillsUsedThisCycle >= maximumRangedSkillsPerCycle ||
                (rangedSkillsUsedThisCycle >= minimumRangedSkillsPerCycle && !HasReadyRangedRotationSkill(distance)))
            {
                FinishRangedRotation();
                return;
            }

            EnterState(BossState.Idle, RandomShortIdle());
            return;
        }

        if (combatPhase == CombatPhase.Melee)
        {
            FinishMeleeRotation();
            return;
        }

        EnterState(BossState.Idle, RandomShortIdle());
    }

    private void UpdateAttackForwardMovement(float speed)
    {
        if (!ShouldMoveForwardDuringAttack())
        {
            desiredHorizontalVelocity = 0f;
            return;
        }

        desiredHorizontalVelocity = CanMoveSafely(lockedAttackDirection, speed)
            ? lockedAttackDirection * speed * PhaseSpeedMultiplier()
            : 0f;
        if (desiredHorizontalVelocity == 0f)
        {
            StopHorizontalImmediately();
        }
    }

    private bool ShouldMoveForwardDuringAttack()
    {
        if (lockedAttackDirection == 0 ||
            Mathf.Abs(transform.position.x - attackStartX) >= attackMoveMaxDistance)
        {
            return false;
        }

        if (!attackHitboxEnabled)
        {
            return useFallbackAttackHitboxTimer && attackHitboxStartTimer > 0f;
        }

        return attackHitboxTimer > attackHitboxActiveDuration * 0.55f;
    }

}
