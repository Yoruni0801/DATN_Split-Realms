using UnityEngine;

public partial class DuyBossController
{
    private void ResolveTarget()
    {
        if (isAggro && IsValidTarget(aggroTarget))
        {
            target = aggroTarget;
            CacheTargetComponents();
            return;
        }

        if (target != null && Vector2.Distance(transform.position, target.position) <= detectRadius)
        {
            CacheTargetComponents();
            return;
        }

        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectRadius, playerLayer);
        target = hit != null ? hit.transform.root : null;
        previousTargetDistance = target != null ? DistanceToTarget() : 0f;
        CacheTargetComponents();
    }

    private void CacheTargetComponents()
    {
        if (cachedTarget == target)
        {
            return;
        }

        cachedTarget = target;
        playerRb = target != null ? target.GetComponentInParent<Rigidbody2D>() : null;
        playerCollider = target != null ? target.GetComponentInParent<Collider2D>() : null;
        previousPlayerX = target != null ? target.position.x : 0f;
        previousPlayerVelocityY = playerRb != null ? playerRb.linearVelocity.y : 0f;
        playerWasGrounded = IsPlayerGrounded();
        playerWasDashing = false;
        dashStartSide = 0;
    }

    private void EvaluateTarget(float deltaTime)
    {
        if (target == null)
        {
            targetOutOfRangeTimer = 0f;
            targetBehindTimer = 0f;
            return;
        }

        float distance = DistanceToTarget();
        if (distance > attackRange)
        {
            targetOutOfRangeTimer += deltaTime;
        }
        else
        {
            if (previousTargetDistance > attackRange + 0.6f)
            {
                playerEscapeCount++;
                adaptiveDecayTimer = adaptiveDecayDelay;
            }

            targetOutOfRangeTimer = 0f;
        }

        if (IsTargetBehind())
        {
            targetBehindTimer += deltaTime;
        }
        else
        {
            targetBehindTimer = 0f;
        }

        previousTargetDistance = distance;
    }

    private void TrackPlayerBehavior(float deltaTime)
    {
        if (target == null || playerRb == null)
        {
            playerAirborneTimer = 0f;
            playerWasDashing = false;
            dashStartSide = 0;
            return;
        }

        bool playerGrounded = IsPlayerGrounded();
        Vector2 playerVelocity = playerRb.linearVelocity;

        if (!playerGrounded)
        {
            playerAirborneTimer += deltaTime;
        }
        else
        {
            playerAirborneTimer = 0f;
        }

        if (playerWasGrounded && !playerGrounded && playerVelocity.y > 0.1f)
        {
            recentPlayerJumps++;
            jumpPatternTimer = behaviorSampleWindow;
        }

        bool playerDashing = IsPlayerDashing();
        if (playerDashing && !playerWasDashing)
        {
            recentPlayerDashes++;
            dashPatternTimer = behaviorSampleWindow;
            dashSpamTimer = dashSpamParryWindow;
            dashStartSide = SideOfBoss(target.position.x);

            if (IsPlayerDashingAway())
            {
                playerEscapeCount++;
                adaptiveDecayTimer = adaptiveDecayDelay;
            }
        }

        if (playerDashing && dashStartSide != 0)
        {
            int currentSide = SideOfBoss(target.position.x);
            if (currentSide != 0 && currentSide != dashStartSide)
            {
                recentDashCrosses++;
                dashPatternTimer = behaviorSampleWindow;
                targetBehindTimer = Mathf.Max(targetBehindTimer, dashCrossTurnDelay);
                dashCrossCounterTimer = dashCrossTurnDelay;
                dashStartSide = currentSide;
            }
        }

        playerWasGrounded = playerGrounded;
        playerWasDashing = playerDashing;
        previousPlayerVelocityY = playerVelocity.y;
        previousPlayerX = target.position.x;
    }

    private void DecayBehaviorMemory(float deltaTime)
    {
        if (jumpPatternTimer > 0f)
        {
            jumpPatternTimer -= deltaTime;
        }
        else if (recentPlayerJumps > 0)
        {
            recentPlayerJumps--;
            jumpPatternTimer = patternMemoryDecayDelay;
        }

        if (dashPatternTimer > 0f)
        {
            dashPatternTimer -= deltaTime;
        }
        else
        {
            bool decayed = false;
            if (recentPlayerDashes > 0)
            {
                recentPlayerDashes--;
                decayed = true;
            }

            if (recentDashCrosses > 0)
            {
                recentDashCrosses--;
                decayed = true;
            }

            if (decayed)
            {
                dashPatternTimer = patternMemoryDecayDelay;
            }
        }

        if (dashSpamTimer > 0f)
        {
            dashSpamTimer -= deltaTime;
        }
    }

    private bool IsPlayerGrounded()
    {
        if (playerCollider == null)
        {
            return playerRb == null || Mathf.Abs(playerRb.linearVelocity.y) < 0.05f;
        }

        Bounds bounds = playerCollider.bounds;
        Vector2 origin = new Vector2(bounds.center.x, bounds.min.y + 0.05f);
        int hitCount = Physics2D.RaycastNonAlloc(origin, Vector2.down, groundProbeHits, 0.18f, groundLayer);
        for (int i = 0; i < hitCount; i++)
        {
            if (IsValidGroundCollider(groundProbeHits[i].collider))
            {
                return true;
            }
        }

        return Mathf.Abs(playerRb != null ? playerRb.linearVelocity.y : 0f) < 0.05f;
    }

    private bool IsTargetAirborne()
    {
        return target != null && !IsPlayerGrounded() && playerAirborneTimer >= playerAirborneMinDuration;
    }

    private bool IsPlayerDashing()
    {
        return playerRb != null && Mathf.Abs(playerRb.linearVelocity.x) >= playerDashSpeedThreshold;
    }

    private int SideOfBoss(float x)
    {
        float delta = x - transform.position.x;
        if (Mathf.Abs(delta) <= faceDeadZone)
        {
            return 0;
        }

        return delta > 0f ? 1 : -1;
    }
}
