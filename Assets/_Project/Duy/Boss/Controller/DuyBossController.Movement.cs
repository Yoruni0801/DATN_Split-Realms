using UnityEngine;

public partial class DuyBossController
{
    private void ApplyMovement()
    {
        if (state == BossState.Dead)
        {
            return;
        }

        if (UsesImmediateHorizontalVelocity)
        {
            horizontalVelocity = desiredHorizontalVelocity;
        }
        else
        {
            horizontalVelocity = Mathf.MoveTowards(
                horizontalVelocity,
                desiredHorizontalVelocity,
                acceleration * Time.fixedDeltaTime);
        }

        rb.linearVelocity = new Vector2(horizontalVelocity, rb.linearVelocity.y);
    }

    private bool UsesImmediateHorizontalVelocity =>
        state == BossState.Dash ||
        state == BossState.Evade ||
        state == BossState.DashAttack ||
        state == BossState.JumpSlam;

    private bool IsWithinGapCloseDashRange(float distance)
    {
        return distance > attackRange && distance <= gapCloseDashRange;
    }

    private void StopHorizontalImmediately()
    {
        BossMovementIntent intent = bossMovementPlanner != null
            ? bossMovementPlanner.Stop()
            : new BossMovementIntent(0f, true);
        horizontalVelocity = 0f;
        desiredHorizontalVelocity = intent.HorizontalVelocity;
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }

    private void TickDebugStateLog(float deltaTime)
    {
        if (!debugBossState)
        {
            return;
        }

        debugStateTimer -= deltaTime;
        if (debugStateTimer > 0f)
        {
            return;
        }

        debugStateTimer = debugStateInterval;
        Debug.Log(
            $"[DuyBoss] state={state}, hp={currentHealth:0}/{maxHealth:0}, stateTimer={stateTimer:0.00}, decisionTimer={decisionTimer:0.00}, grounded={isGrounded}, hasSafePos={hasSafeGroundedPosition}, pos={(rb != null ? rb.position : (Vector2)transform.position)}, rbVel={(rb != null ? rb.linearVelocity : Vector2.zero)}, hVel={horizontalVelocity:0.00}, desiredH={desiredHorizontalVelocity:0.00}, targetDist={DistanceToTarget():0.00}, bodyCollider={DescribeCollider(bodyCollider)}, weaponActive={attackHitboxEnabled}",
            this);
    }

    private static string DescribeCollider(Collider2D collider)
    {
        if (collider == null)
        {
            return "null";
        }

        return $"{collider.GetType().Name}(enabled={collider.enabled}, trigger={collider.isTrigger}, layer={LayerMask.LayerToName(collider.gameObject.layer)})";
    }

    private void MoveInDirection(int direction, float speed)
    {
        if (!CanMoveSafely(direction, speed))
        {
            StopHorizontalImmediately();
            decisionTimer = 0f;
            return;
        }

        moveDirection = direction;
        BossMovementIntent intent = bossMovementPlanner != null
            ? bossMovementPlanner.Move(direction, speed, PhaseSpeedMultiplier(), false)
            : new BossMovementIntent(direction * speed * PhaseSpeedMultiplier(), false);
        desiredHorizontalVelocity = intent.HorizontalVelocity;
    }

    private bool CanDash()
    {
        return dashCooldownTimer <= 0f && !IsHardLocked;
    }

    private bool CanParry()
    {
        return parryCooldownTimer <= 0f &&
            consecutiveParries < Mathf.Max(1, maxConsecutiveParries) &&
            !attackHitboxEnabled &&
            !IsHardLocked;
    }

    private bool CanUseRangedPressure(float distance)
    {
        return shockwavePrefab != null &&
            rangedPressureCooldownTimer <= 0f &&
            distance >= rangedPressureMinDistance &&
            !IsHardLocked &&
            consecutiveRangedPressure < 2;
    }

    private void StartAdaptiveCounterCooldown()
    {
        adaptiveCounterCooldownTimer = adaptiveCounterCooldown * (IsPhaseTwo ? 0.85f : 1f);
    }

    private Vector2 PredictPlayerPosition(float predictionTime)
    {
        if (target == null)
        {
            return transform.position;
        }

        predictionTime = Mathf.Clamp(predictionTime, 0.05f, 0.35f);
        if (playerRb == null)
        {
            return target.position;
        }

        return playerRb.position + playerRb.linearVelocity * predictionTime;
    }

    private bool IsPlayerDashingAway()
    {
        if (target == null || playerRb == null || !IsPlayerDashing())
        {
            return false;
        }

        Vector2 bossPosition = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 fromBossToPlayer = ((Vector2)target.position - bossPosition).normalized;
        Vector2 playerVelocity = playerRb.linearVelocity;
        if (playerVelocity.sqrMagnitude <= 0.01f)
        {
            return false;
        }

        return Vector2.Dot(playerVelocity.normalized, fromBossToPlayer) > 0.6f;
    }

    private bool CanMove(int direction)
    {
        return CanMoveSafely(direction, Mathf.Max(approachSpeed, Mathf.Abs(horizontalVelocity)));
    }

    private bool CanMoveSafely(int direction, float speed)
    {
        if (direction == 0)
        {
            return false;
        }

        float predictionDistance = ResolveMovementPredictionDistance(speed);
        return !HasWallAhead(direction, Mathf.Max(wallCheckDistance, predictionDistance)) &&
            HasGroundAhead(direction, predictionDistance);
    }

    private float ResolveMovementPredictionDistance(float speed)
    {
        float frameTravel = Mathf.Abs(speed) * Time.fixedDeltaTime;
        float brakingDistance = horizontalVelocity * horizontalVelocity / (2f * Mathf.Max(acceleration, 0.01f));
        return frameTravel + Mathf.Min(brakingDistance, maxEdgePredictionDistance) + edgeSafetyMargin;
    }

    private bool CheckGrounded()
    {
        if (bodyCollider == null)
        {
            return false;
        }

        Bounds bounds = bodyCollider.bounds;
        float inset = Mathf.Min(bounds.extents.x * 0.6f, 0.35f);
        Vector2 leftOrigin = new Vector2(bounds.center.x - inset, bounds.min.y + 0.05f);
        Vector2 centerOrigin = new Vector2(bounds.center.x, bounds.min.y + 0.05f);
        Vector2 rightOrigin = new Vector2(bounds.center.x + inset, bounds.min.y + 0.05f);
        return HasGroundBelow(leftOrigin) || HasGroundBelow(centerOrigin) || HasGroundBelow(rightOrigin);
    }

    private bool HasGroundBelow(Vector2 origin)
    {
        int hitCount = Physics2D.RaycastNonAlloc(origin, Vector2.down, groundProbeHits, 0.18f, groundLayer);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = groundProbeHits[i].collider;
            if (IsValidGroundCollider(hitCollider))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasJumpClearance()
    {
        if (bodyCollider == null)
        {
            return true;
        }

        Bounds bounds = bodyCollider.bounds;
        Vector2 size = bounds.size * 0.8f;
        RaycastHit2D hit = Physics2D.BoxCast(bounds.center, size, 0f, Vector2.up, jumpClearanceDistance, movementLayer);
        return hit.collider == null || hit.collider.transform.root == transform.root || DamageColliderUtility.IsNonDamageSensor(hit.collider);
    }

    private bool HasJumpSlamClearance()
    {
        if (bodyCollider == null)
        {
            return true;
        }

        Bounds bounds = bodyCollider.bounds;
        Vector2 size = bounds.size * 0.8f;
        float clearanceDistance = Mathf.Max(jumpClearanceDistance, jumpSlamMinHeightBeforeDive + bounds.extents.y);
        RaycastHit2D hit = Physics2D.BoxCast(bounds.center, size, 0f, Vector2.up, clearanceDistance, movementLayer);
        return hit.collider == null || hit.collider.transform.root == transform.root || DamageColliderUtility.IsNonDamageSensor(hit.collider);
    }

    private Vector2 ClampJumpSlamTarget(Vector2 targetPosition)
    {
        Vector2 start = rb != null ? rb.position : (Vector2)transform.position;
        float maxTravel = Mathf.Max(0.5f, jumpSlamMaxHorizontalTravel);
        float minX = jumpSlamStartPosition == Vector2.zero ? start.x - maxTravel : jumpSlamStartPosition.x - maxTravel;
        float maxX = jumpSlamStartPosition == Vector2.zero ? start.x + maxTravel : jumpSlamStartPosition.x + maxTravel;
        targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
        return targetPosition;
    }

    private bool HasSafeJumpSlamLanding(Vector2 targetPosition)
    {
        if (bodyCollider == null)
        {
            return true;
        }

        Bounds bounds = bodyCollider.bounds;
        Vector2 origin = new Vector2(targetPosition.x, targetPosition.y + jumpSlamMinHeightBeforeDive + bounds.extents.y);
        float probeDistance = jumpSlamMinHeightBeforeDive + groundCheckDistance + bounds.size.y + 1f;
        int hitCount = Physics2D.RaycastNonAlloc(origin, Vector2.down, groundProbeHits, probeDistance, groundLayer);
        for (int i = 0; i < hitCount; i++)
        {
            if (IsValidGroundCollider(groundProbeHits[i].collider))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasSafeJumpLanding(int direction)
    {
        if (bodyCollider == null)
        {
            return true;
        }

        if (direction != 0 && HasWallAhead(direction, wallCheckDistance + edgeSafetyMargin))
        {
            return false;
        }

        float gravity = rb != null ? Mathf.Abs(Physics2D.gravity.y * rb.gravityScale) : 0f;
        float airTime = gravity > 0.01f ? (2f * jumpForce) / gravity : minimumAirTime;
        float landingOffset = direction * jumpForwardSpeed * airTime * PhaseSpeedMultiplier();
        Bounds bounds = bodyCollider.bounds;
        Vector2 origin = new Vector2(bounds.center.x + landingOffset, bounds.min.y + jumpClearanceDistance);
        int hitCount = Physics2D.RaycastNonAlloc(origin, Vector2.down, groundProbeHits, jumpClearanceDistance + groundCheckDistance + 0.5f, groundLayer);
        for (int i = 0; i < hitCount; i++)
        {
            if (IsValidGroundCollider(groundProbeHits[i].collider))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasSafeJumpAttackLanding(int direction)
    {
        if (bodyCollider == null)
        {
            return true;
        }

        if (direction != 0 && HasWallAhead(direction, wallCheckDistance + edgeSafetyMargin))
        {
            return false;
        }

        float gravity = rb != null ? Mathf.Abs(Physics2D.gravity.y * rb.gravityScale) : 0f;
        float airTime = gravity > 0.01f ? (2f * jumpAttackForce) / gravity : minimumAirTime;
        float landingOffset = direction * jumpAttackHorizontalSpeed * airTime * PhaseSpeedMultiplier();
        Bounds bounds = bodyCollider.bounds;
        Vector2 origin = new Vector2(bounds.center.x + landingOffset, bounds.min.y + jumpClearanceDistance);
        int hitCount = Physics2D.RaycastNonAlloc(origin, Vector2.down, groundProbeHits, jumpClearanceDistance + groundCheckDistance + 0.5f, groundLayer);
        for (int i = 0; i < hitCount; i++)
        {
            if (IsValidGroundCollider(groundProbeHits[i].collider))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasGroundAhead(int direction, float predictionDistance)
    {
        int probeCount = Mathf.Max(1, dashGroundProbeCount);
        for (int i = 1; i <= probeCount; i++)
        {
            float t = i / (float)probeCount;
            float offset = predictionDistance * t;
            if (!HasGroundAtOffset(direction, offset))
            {
                return false;
            }
        }

        return true;
    }

    private bool HasGroundAtOffset(int direction, float offset)
    {
        Bounds bounds = bodyCollider != null ? bodyCollider.bounds : new Bounds(transform.position, Vector3.one);
        Vector2 origin = new Vector2(bounds.center.x + direction * (bounds.extents.x + offset), bounds.min.y + 0.05f);
        int hitCount = Physics2D.RaycastNonAlloc(origin, Vector2.down, groundProbeHits, groundCheckDistance, groundLayer);
        for (int i = 0; i < hitCount; i++)
        {
            if (IsValidGroundCollider(groundProbeHits[i].collider))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasWallAhead(int direction, float checkDistance)
    {
        if (direction == 0)
        {
            return false;
        }

        Bounds bounds = bodyCollider != null ? bodyCollider.bounds : new Bounds(transform.position, Vector3.one);
        Vector2 origin = new Vector2(bounds.center.x + direction * bounds.extents.x, bounds.center.y);
        int hitCount = Physics2D.RaycastNonAlloc(origin, Vector2.right * direction, groundProbeHits, checkDistance, movementLayer);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = groundProbeHits[i].collider;
            if (hitCollider != null &&
                hitCollider.transform.root != transform.root &&
                !hitCollider.isTrigger &&
                !DamageColliderUtility.IsNonDamageSensor(hitCollider))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsValidGroundCollider(Collider2D collider)
    {
        return collider != null &&
            collider.transform.root != transform.root &&
            !collider.isTrigger &&
            !DamageColliderUtility.IsNonDamageSensor(collider);
    }

    private void TrackSafeGroundedPosition(float deltaTime)
    {
        if (!isGrounded ||
            state == BossState.JumpEvade ||
            state == BossState.JumpSlam ||
            state == BossState.Hurt ||
            state == BossState.Stun ||
            state == BossState.Dash ||
            state == BossState.Evade ||
            state == BossState.DashAttack ||
            state == BossState.JumpAttack ||
            rb == null ||
            Mathf.Abs(rb.linearVelocity.y) > 0.1f)
        {
            groundedStableTimer = 0f;
            return;
        }

        groundedStableTimer += deltaTime;
        if (groundedStableTimer >= safeGroundedStableDuration)
        {
            lastSafeGroundedPosition = rb.position;
            hasSafeGroundedPosition = true;
        }
    }

    private void RecoverIfFallenTooFar()
    {
        if (!hasSafeGroundedPosition ||
            rb == null ||
            state == BossState.Dead ||
            state == BossState.Hurt ||
            state == BossState.Stun ||
            state == BossState.JumpEvade ||
            state == BossState.JumpSlam ||
            state == BossState.JumpAttack)
        {
            return;
        }

        if (rb.position.y >= lastSafeGroundedPosition.y - fallRecoveryHeight)
        {
            return;
        }

        rb.position = lastSafeGroundedPosition;
        rb.linearVelocity = Vector2.zero;
        horizontalVelocity = 0f;
        desiredHorizontalVelocity = 0f;
        CancelAttackHitbox();
        EnterState(BossState.Idle, fallRecoveryDelay);
    }

    private void UpdateFacing()
    {
        if (!faceTarget || target == null || state == BossState.Dash || state == BossState.DashAttack || state == BossState.Retreat || IsHardLocked)
        {
            return;
        }

        float deltaX = target.position.x - transform.position.x;
        if (Mathf.Abs(deltaX) <= faceDeadZone)
        {
            return;
        }

        FaceDirection(deltaX >= 0f ? 1 : -1);
    }

    private void FaceDirection(int direction)
    {
        if (direction == 0 || direction == facingDirection)
        {
            return;
        }

        facingDirection = direction;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facingDirection;
        transform.localScale = scale;
    }

    private int DirectionToTarget()
    {
        if (target == null)
        {
            return facingDirection;
        }

        return target.position.x >= transform.position.x ? 1 : -1;
    }

    private bool IsTargetBehind()
    {
        return target != null && DirectionToTarget() != facingDirection;
    }

    private float DistanceToTarget()
    {
        return target != null ? Vector2.Distance(transform.position, target.position) : float.PositiveInfinity;
    }

    private float PhaseSpeedMultiplier()
    {
        return IsPhaseTwo ? phaseTwoSpeedMultiplier : 1f;
    }

    private float EffectiveDashCooldown()
    {
        return dashCooldown * (IsPhaseTwo ? phaseTwoDashCooldownMultiplier : 1f);
    }

    private float EffectiveDashChance()
    {
        float pressureBonus = Mathf.Clamp01(playerEscapeCount * 0.08f + recentRangedHits * 0.06f);
        return Mathf.Clamp01(dashChance + pressureBonus + (IsPhaseTwo ? phaseTwoDashChanceBonus : 0f));
    }

    private float RangedPressureChance()
    {
        float chance = 0.35f + recentRangedHits * 0.12f + targetOutOfRangeTimer * 0.08f;
        if (consecutiveRangedPressure > 0)
        {
            chance -= 0.25f;
        }

        return Mathf.Clamp01(chance);
    }

    private float RandomDecisionInterval()
    {
        float min = Mathf.Min(decisionIntervalMin, decisionIntervalMax);
        float max = Mathf.Max(decisionIntervalMin, decisionIntervalMax);
        return Random.Range(min, max) * (IsPhaseTwo ? phaseTwoDecisionMultiplier : 1f);
    }

    private float RandomShortIdle()
    {
        float min = Mathf.Min(shortIdleMin, shortIdleMax);
        float max = Mathf.Max(shortIdleMin, shortIdleMax);
        return Random.Range(min, max);
    }

    private AnimationSpine.State ComboAnimationForIndex(int index)
    {
        switch (index)
        {
            case 0:
                return AnimationSpine.State.NormalAttack1;
            case 1:
                return AnimationSpine.State.NormalAttack2;
            default:
                return AnimationSpine.State.NormalAttack3;
        }
    }

}
