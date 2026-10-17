using UnityEngine;

public sealed class NPCQuestMovementController
{
    private readonly NPCQuestGiver owner;
    private readonly Rigidbody2D rb;
    private readonly AnimationSpine animationSpine;

    private AnimationSpine.State currentAnimState = AnimationSpine.State.None;
    private bool isGrounded;
    private float jumpCooldown;

    public NPCQuestMovementController(NPCQuestGiver owner, Rigidbody2D rb, AnimationSpine animationSpine)
    {
        this.owner = owner;
        this.rb = rb;
        this.animationSpine = animationSpine;
    }

    public void MoveTowardsTarget(Transform target, float stopDistance, NPCQuestGiver.NPCState nextState, bool unlockPlayerOnReach = true)
    {
        if (target == null)
        {
            return;
        }

        float distanceX = Mathf.Abs(target.position.x - owner.transform.position.x);
        if (distanceX > stopDistance)
        {
            MoveHorizontally(target);
            return;
        }

        StopHorizontal();
        owner.currentState = nextState;

        if (unlockPlayerOnReach && owner.playerTransform != null)
        {
            Player player = owner.playerTransform.GetComponent<Player>();
            if (player != null)
            {
                player.SetInputActive(true);
            }

            if (owner.currentState == NPCQuestGiver.NPCState.Idle_WaitingForHelp && owner.exclamationMark != null)
            {
                owner.exclamationMark.SetActive(true);
            }
        }

        if (owner.currentState == NPCQuestGiver.NPCState.QuestCompleted)
        {
            owner.gameObject.SetActive(false);
        }
    }

    public void StopWhenNotMovingState(NPCQuestGiver.NPCState state)
    {
        if (IsMovingState(state))
        {
            return;
        }

        StopHorizontal();
    }

    public void StopHorizontal()
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }

    public void UpdateSpineAnimation()
    {
        if (animationSpine == null || rb == null)
        {
            return;
        }

        UpdateGrounded();

        AnimationSpine.State nextAnimState = owner.idleAnimState;
        if (!isGrounded)
        {
            nextAnimState = rb.linearVelocity.y > 0f ? owner.jumpAnimState : owner.fallAnimState;
        }
        else if (Mathf.Abs(rb.linearVelocity.x) > 0.1f)
        {
            nextAnimState = owner.walkAnimState;
        }

        if (currentAnimState == nextAnimState
            || nextAnimState == AnimationSpine.State.None
            || !animationSpine.HasStateAnimation(nextAnimState))
        {
            return;
        }

        animationSpine.Play(nextAnimState, ShouldLoopSpineAnimation(nextAnimState));
        currentAnimState = nextAnimState;
    }

    private void MoveHorizontally(Transform target)
    {
        float directionX = target.position.x - owner.transform.position.x;
        float sign = Mathf.Sign(directionX);

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(sign * owner.moveSpeed, rb.linearVelocity.y);
            CheckAndJump(sign, target);
        }
        else
        {
            Vector3 newPosition = owner.transform.position;
            newPosition.x += sign * owner.moveSpeed * Time.deltaTime;
            owner.transform.position = newPosition;
        }

        FaceDirection(sign);
    }

    private void FaceDirection(float sign)
    {
        if (sign < 0f)
        {
            owner.transform.localScale = new Vector3(-Mathf.Abs(owner.transform.localScale.x), owner.transform.localScale.y, owner.transform.localScale.z);
        }
        else if (sign > 0f)
        {
            owner.transform.localScale = new Vector3(Mathf.Abs(owner.transform.localScale.x), owner.transform.localScale.y, owner.transform.localScale.z);
        }
    }

    private void UpdateGrounded()
    {
        if (owner.groundCheck != null)
        {
            isGrounded = Physics2D.OverlapCircle(owner.groundCheck.position, owner.groundCheckRadius, owner.obstacleLayer);
        }
        else
        {
            isGrounded = Physics2D.Raycast(owner.transform.position, Vector2.down, 1.2f, owner.obstacleLayer);
        }
    }

    private bool ShouldLoopSpineAnimation(AnimationSpine.State state)
    {
        return state == owner.idleAnimState
            || state == owner.walkAnimState
            || state == owner.fallAnimState;
    }

    private void CheckAndJump(float directionSign, Transform target)
    {
        if (jumpCooldown > 0f)
        {
            jumpCooldown -= Time.deltaTime;
        }

        if (!isGrounded || rb == null || jumpCooldown > 0f)
        {
            return;
        }

        bool shouldJump = HasObstacleAhead(directionSign)
            || HasGapAhead(directionSign)
            || TargetIsHigher(target);

        if (!shouldJump)
        {
            return;
        }

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, owner.jumpForce);
        jumpCooldown = 0.5f;
    }

    private bool HasObstacleAhead(float directionSign)
    {
        Vector2 rayDirection = new Vector2(directionSign, 0f);
        Vector2 rayStartMid = (Vector2)owner.transform.position + new Vector2(0f, 0.5f);
        Vector2 rayStartLow = (Vector2)owner.transform.position + new Vector2(0f, 0.1f);

        RaycastHit2D hitMid = Physics2D.Raycast(rayStartMid, rayDirection, owner.obstacleDetectDistance, owner.obstacleLayer);
        RaycastHit2D hitLow = Physics2D.Raycast(rayStartLow, rayDirection, owner.obstacleDetectDistance, owner.obstacleLayer);

        return (hitMid.collider != null && !hitMid.collider.isTrigger)
            || (hitLow.collider != null && !hitLow.collider.isTrigger);
    }

    private bool HasGapAhead(float directionSign)
    {
        Vector2 gapRayStart = (Vector2)owner.transform.position + new Vector2(directionSign * owner.obstacleDetectDistance, 0.5f);
        RaycastHit2D hitGap = Physics2D.Raycast(gapRayStart, Vector2.down, 2f, owner.obstacleLayer);
        return hitGap.collider == null;
    }

    private bool TargetIsHigher(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        float heightDifference = target.position.y - owner.transform.position.y;
        float distanceX = Mathf.Abs(target.position.x - owner.transform.position.x);
        return heightDifference > 1.5f && distanceX < 4f;
    }

    private bool IsMovingState(NPCQuestGiver.NPCState state)
    {
        return state == NPCQuestGiver.NPCState.RunningToPlayer_1
            || state == NPCQuestGiver.NPCState.RunningToPlayer_2
            || state == NPCQuestGiver.NPCState.RunningToLumberyard
            || state == NPCQuestGiver.NPCState.RunningToBridge
            || state == NPCQuestGiver.NPCState.RunningAway;
    }
}
