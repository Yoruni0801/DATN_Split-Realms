using UnityEngine;

public partial class DuyBossController
{
    private void EnterState(BossState nextState, float duration)
    {
        BossState previousState = state;
        state = nextState;
        stateTimer = Mathf.Max(0f, duration);
        decisionTimer = RandomDecisionInterval();
        SyncRuntimeFromController();

        if (nextState != BossState.Dash && nextState != BossState.DashAttack)
        {
            dashPrepareTimer = 0f;
        }

        if (nextState != BossState.Parry)
        {
            consecutiveParries = Mathf.Min(consecutiveParries, maxConsecutiveParries);
        }

        if (IsHardLocked || nextState == BossState.Idle)
        {
            desiredHorizontalVelocity = 0f;
        }

        if (debugBossState && previousState != nextState)
        {
            Debug.Log(
                $"[DuyBoss] State {previousState} -> {nextState}, duration={stateTimer:0.00}, hp={currentHealth:0}/{maxHealth:0}, grounded={isGrounded}, rbVel={(rb != null ? rb.linearVelocity : Vector2.zero)}, bodyCollider={DescribeCollider(bodyCollider)}",
                this);
        }
    }

    private void Die()
    {
        currentHealth = 0f;
        isAggro = false;
        aggroTarget = null;
        CancelAttackHitbox();
        EnterState(BossState.Dead, 0f);
        rb.linearVelocity = Vector2.zero;
        horizontalVelocity = 0f;
        desiredHorizontalVelocity = 0f;
        PlayBossAnimation(AnimationSpine.State.Die, false, true);

        if (deathDestroyDelay > 0f)
        {
            Destroy(gameObject, deathDestroyDelay);
        }
    }

    private void PlayMoveAnimation(bool moving)
    {
        PlayBossAnimation(moving ? AnimationSpine.State.Walk : AnimationSpine.State.Idle, moving);
    }

    private void PlayBossAnimation(AnimationSpine.State animationState, bool loop, bool forceRestart = false)
    {
        if (animationSpine == null || (!forceRestart && lastAnimation == animationState))
        {
            return;
        }

        if (!animationSpine.HasStateAnimation(animationState))
        {
            if (animationState == AnimationSpine.State.NormalAttack1 ||
                animationState == AnimationSpine.State.NormalAttack2 ||
                animationState == AnimationSpine.State.NormalAttack3 ||
                animationState == AnimationSpine.State.SpecialAttack)
            {
                animationState = AnimationSpine.State.Attack;
            }
            else if (animationState == AnimationSpine.State.ParryStart ||
                animationState == AnimationSpine.State.ParryFail ||
                animationState == AnimationSpine.State.ParrySuccess)
            {
                animationState = AnimationSpine.State.Block;
            }
            else if (animationState == AnimationSpine.State.Range)
            {
                animationState = AnimationSpine.State.Magic;
            }
            else if (animationState == AnimationSpine.State.Dash)
            {
                animationState = AnimationSpine.State.Walk;
            }
            else if (animationState == AnimationSpine.State.Jump ||
                animationState == AnimationSpine.State.Fall ||
                animationState == AnimationSpine.State.Stun)
            {
                animationState = AnimationSpine.State.Idle;
            }
        }

        if (forceRestart)
        {
            animationSpine.PlayForced(animationState, loop, 0);
        }
        else
        {
            animationSpine.Play(animationState, loop);
        }

        lastAnimation = animationState;
    }

}
