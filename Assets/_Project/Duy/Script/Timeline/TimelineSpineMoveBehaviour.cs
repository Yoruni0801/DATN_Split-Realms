using Spine;
using UnityEngine;
using UnityEngine.Playables;

public class TimelineSpineMoveBehaviour : PlayableBehaviour
{
    public Transform target;
    public Vector2 targetOffset;

    /// <summary>
    /// Optional authored start of the move. When assigned the path is rebuilt from it on every evaluation, which
    /// makes editor scrubbing deterministic: jumping straight to any time gives the position the clip should have
    /// reached instead of continuing from wherever the character currently stands. Left empty, the clip falls back
    /// to the old behaviour and starts from the live transform.
    /// </summary>
    public Transform startTarget;
    public Vector2 startOffset;
    public float stopDistance;
    public TimelineSpineMoveAxis moveAxis;
    public TimelineSpineMoveTiming moveTiming;
    public float moveSpeed;
    public float moveDuration;
    public bool stopWhenReached;
    public bool keepStoppedOnExit;
    public bool faceMoveDirection;
    public bool invertFacing;
    public TimelineSpineFacingOverride facingOverride;
    public bool controlAnimation;
    public AnimationSpine.State moveState;
    public AnimationSpine.State idleState;
    public bool returnToIdleWhenStopped;
    public int animationTrackIndex;
    public bool lockPlayerControl;
    public bool unlockPlayerControlOnExit;
    public bool applyGravity = true;
    public bool applyInEditMode;
    public TimelineSpineMoveBindingMode bindingMode;

    /// <summary>
    /// Set by the mixer for the single frame a Timeline seek is processed. The clip then only repositions the
    /// character and skips the player control lock, animation restarts and Rigidbody velocity writes.
    /// </summary>
    public bool seekPreview;

    private AnimationSpine animationSpine;
    private Transform movingTransform;
    private Rigidbody2D body;
    private Player player;
    private PetrifyController petrifyController;
    private bool clipActive;
    private bool reachedTarget;
    private bool hasApproachDirection;
    private bool hasMovePath;
    private bool moveAnimationPlaying;
    private Vector3 approachDirection;
    private Vector3 pathStartPosition;
    private Vector3 pathStopPosition;
    private float cachedMoveDuration;
    private float gravityScaleBeforeClip;
    private bool gravitySuppressed;

    public AnimationSpine ResolveAnimationSpine(AnimationSpine boundAnimationSpine)
    {
        if (bindingMode == TimelineSpineMoveBindingMode.TrackBinding)
        {
            return boundAnimationSpine;
        }

        if (bindingMode == TimelineSpineMoveBindingMode.CurrentPlayerWhenBindingMissing &&
            boundAnimationSpine != null)
        {
            return boundAnimationSpine;
        }

        Player currentPlayer = PlayerRegistry.Current;
        if (currentPlayer == null)
        {
            currentPlayer = TimelineTrigger2D.CurrentPlayer;
        }
#if UNITY_2023_1_OR_NEWER
        if (currentPlayer == null)
        {
            currentPlayer = Object.FindFirstObjectByType<Player>();
        }
#else
        if (currentPlayer == null)
        {
            currentPlayer = Object.FindObjectOfType<Player>();
        }
#endif
        return currentPlayer != null ? currentPlayer.GetComponent<AnimationSpine>() : null;
    }

    public void Enter(AnimationSpine boundAnimationSpine)
    {
        if (boundAnimationSpine == null)
        {
            return;
        }

        if (animationSpine != boundAnimationSpine)
        {
            Exit();
            Bind(boundAnimationSpine);
        }

        if (clipActive)
        {
            ApplyGravitySetting();
            return;
        }

        clipActive = true;
        reachedTarget = false;
        hasApproachDirection = false;
        hasMovePath = false;
        moveAnimationPlaying = false;
        cachedMoveDuration = 0f;

        ApplyGravitySetting();

        if (lockPlayerControl && player != null && !seekPreview)
        {
            player.CancelActiveSkillForTimeline();
            player.SetControlLocked(true);
        }

        if (IsPetrifying())
        {
            StopForPetrifying();
            return;
        }

        ApplyFacingOverride();
        SetMoveAnimation(true);
    }

    public void Process(float localTime)
    {
        if (animationSpine == null || movingTransform == null || target == null)
        {
            StopMovement();
            StopMoveAnimation();
            return;
        }

        if (IsPetrifying())
        {
            StopForPetrifying();
            return;
        }

        MoveToTarget(localTime);
    }

    public void Exit()
    {
        if (!clipActive)
        {
            ClearReferences();
            return;
        }

        bool petrifyingOnExit = IsPetrifying();
        if (petrifyingOnExit)
        {
            StopHorizontalMovement();
        }
        else
        {
            StopMovement();
            StopMoveAnimation();
        }

        RestoreGravity();

        if (unlockPlayerControlOnExit && player != null && !seekPreview)
        {
            player.SetControlLocked(false);
        }

        clipActive = false;
        hasApproachDirection = false;
        hasMovePath = false;
        reachedTarget = false;
        moveAnimationPlaying = false;
        ClearReferences();
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        Exit();
    }

    private void Bind(AnimationSpine boundAnimationSpine)
    {
        animationSpine = boundAnimationSpine;
        movingTransform = animationSpine.transform;
        body = movingTransform.GetComponent<Rigidbody2D>();
        player = movingTransform.GetComponent<Player>();
        petrifyController = movingTransform.GetComponent<PetrifyController>();
    }

    private void ApplyGravitySetting()
    {
        if (applyGravity || body == null || !Application.isPlaying || seekPreview)
        {
            return;
        }

        if (!gravitySuppressed)
        {
            gravityScaleBeforeClip = body.gravityScale;
            gravitySuppressed = true;
            body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);
        }

        body.gravityScale = 0f;
    }

    private void RestoreGravity()
    {
        if (!gravitySuppressed || body == null)
        {
            return;
        }

        body.gravityScale = gravityScaleBeforeClip;
        gravitySuppressed = false;
    }

    private void MoveToTarget(float localTime)
    {
        Vector3 currentPosition = movingTransform.position;
        Vector3 targetPosition = target.position + (Vector3)targetOffset;

        // With an authored start the whole path is recomputed every evaluation, so scrubbing forward, backward or
        // directly into the middle of the clip always lands on the same position. Without one the path is still
        // captured once from the live transform, which is the previous behaviour.
        bool useAuthoredStart = startTarget != null;
        if (useAuthoredStart || !hasMovePath)
        {
            Vector3 pathOrigin = useAuthoredStart
                ? startTarget.position + (Vector3)startOffset
                : currentPosition;

            pathStartPosition = pathOrigin;
            pathStopPosition = ResolveStopPosition(targetPosition, pathOrigin, useAuthoredStart);
            cachedMoveDuration = ResolveMoveDuration(GetMoveDistance(pathStartPosition, pathStopPosition));
            hasMovePath = true;
        }

        float t = cachedMoveDuration > 0f
            ? Mathf.Clamp01(Mathf.Max(0f, localTime) / cachedMoveDuration)
            : 1f;
        Vector3 nextPosition = Vector3.Lerp(pathStartPosition, pathStopPosition, t);
        if (moveAxis == TimelineSpineMoveAxis.Horizontal)
        {
            if (applyGravity && body != null)
            {
                nextPosition.y = body.position.y;
            }
            else
            {
                nextPosition.y = currentPosition.y;
            }
        }

        nextPosition.z = currentPosition.z;

        Vector3 delta = nextPosition - currentPosition;
        if (delta.sqrMagnitude <= 0.000001f)
        {
            if (t >= 0.999f)
            {
                StopMovement();
                StopMoveAnimation();
                reachedTarget = true;
            }

            return;
        }

        if (!ApplyFacingOverride())
        {
            FaceDirection(delta.x);
        }

        MoveBody(currentPosition, nextPosition);
        SetMoveAnimation(t < 0.999f && (!stopWhenReached || !reachedTarget));

        if (t >= 0.999f)
        {
            StopMovement();
            StopMoveAnimation();
            reachedTarget = true;
        }
    }

    /// <summary>
    /// Forces the next Process call to recapture the path. Used after a Timeline seek, where the cached path was
    /// built from a position the playhead has since moved away from.
    /// </summary>
    public void ResetPath()
    {
        hasMovePath = false;
        hasApproachDirection = false;
        reachedTarget = false;
    }

    /// <summary>
    /// Installs the path this clip would have had if it were entered normally, starting from
    /// <paramref name="origin"/>. Used after a seek so a clip without a startTarget chains from the result of the
    /// earlier clips instead of recapturing from wherever the character currently stands, both on the seek frame
    /// and on the normal frames that follow it. Ignored when a startTarget is assigned, since that path is
    /// authored and recomputed every evaluation anyway.
    /// </summary>
    public void PrimePath(Vector3 origin)
    {
        if (target == null || startTarget != null)
        {
            return;
        }

        Vector3 targetPosition = target.position + (Vector3)targetOffset;

        hasApproachDirection = false;
        pathStartPosition = origin;
        pathStopPosition = ResolveStopPosition(targetPosition, origin, true);
        cachedMoveDuration = ResolveMoveDuration(GetMoveDistance(pathStartPosition, pathStopPosition));
        hasMovePath = true;
        reachedTarget = false;
    }

    /// <summary>
    /// Pure position evaluation used to replay this clip after a Timeline seek: returns where the clip would have
    /// placed the character at <paramref name="localTime"/> when starting from <paramref name="fromPosition"/>,
    /// without touching animation, facing, the player control lock or the Rigidbody.
    /// </summary>
    public Vector3 EvaluatePositionAt(Vector3 fromPosition, float localTime)
    {
        if (target == null)
        {
            return fromPosition;
        }

        Vector3 origin = startTarget != null
            ? startTarget.position + (Vector3)startOffset
            : fromPosition;

        Vector3 targetPosition = target.position + (Vector3)targetOffset;

        Vector3 toTarget = targetPosition - origin;
        if (moveAxis == TimelineSpineMoveAxis.Horizontal)
        {
            toTarget.y = 0f;
        }

        toTarget.z = 0f;
        Vector3 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.right;

        Vector3 stopPosition = targetPosition - direction * stopDistance;
        if (moveAxis == TimelineSpineMoveAxis.Horizontal)
        {
            stopPosition.y = origin.y;
        }

        float duration = ResolveMoveDuration(GetMoveDistance(origin, stopPosition));
        float t = duration > 0f ? Mathf.Clamp01(Mathf.Max(0f, localTime) / duration) : 1f;

        Vector3 result = Vector3.Lerp(origin, stopPosition, t);
        if (moveAxis == TimelineSpineMoveAxis.Horizontal)
        {
            result.y = fromPosition.y;
        }

        result.z = fromPosition.z;
        return result;
    }

    private float GetMoveDistance(Vector3 from, Vector3 to)
    {
        if (moveAxis == TimelineSpineMoveAxis.Horizontal)
        {
            return Mathf.Abs(to.x - from.x);
        }

        Vector2 delta = (Vector2)(to - from);
        return delta.magnitude;
    }

    private float ResolveMoveDuration(float distance)
    {
        if (distance <= 0f)
        {
            return 0f;
        }

        if (moveTiming == TimelineSpineMoveTiming.MoveDuration)
        {
            return Mathf.Max(0.01f, moveDuration);
        }

        return moveSpeed > 0f
            ? distance / moveSpeed
            : 0f;
    }

    private Vector3 ResolveStopPosition(Vector3 targetPosition, Vector3 originPosition, bool alwaysResolve)
    {
        if (alwaysResolve || !hasApproachDirection)
        {
            Vector3 toTarget = targetPosition - originPosition;
            if (moveAxis == TimelineSpineMoveAxis.Horizontal)
            {
                toTarget.y = 0f;
            }

            toTarget.z = 0f;
            approachDirection = toTarget.sqrMagnitude > 0.0001f
                ? toTarget.normalized
                : GetFacingDirection();

            hasApproachDirection = true;
        }

        Vector3 stopPosition = targetPosition - approachDirection * stopDistance;
        if (moveAxis == TimelineSpineMoveAxis.Horizontal)
        {
            stopPosition.y = originPosition.y;
        }

        stopPosition.z = originPosition.z;

        return stopPosition;
    }

    private Vector3 GetFacingDirection()
    {
        if (movingTransform == null)
        {
            return Vector3.right;
        }

        if (player != null || invertFacing)
        {
            return movingTransform.localScale.x < 0f ? Vector3.right : Vector3.left;
        }

        return movingTransform.localScale.x < 0f ? Vector3.left : Vector3.right;
    }

    private void MoveBody(Vector3 currentPosition, Vector3 nextPosition)
    {
        // A seek teleports rather than moves, so the Rigidbody is bypassed to avoid sweeping across the scene.
        if (seekPreview)
        {
            ApplySeekPosition(nextPosition);
            return;
        }

        if (body == null || !Application.isPlaying)
        {
            movingTransform.position = nextPosition;
            return;
        }

        if (applyGravity && moveAxis == TimelineSpineMoveAxis.Horizontal)
        {
            body.position = new Vector2(nextPosition.x, body.position.y);
            movingTransform.position = new Vector3(nextPosition.x, movingTransform.position.y, nextPosition.z);
        }
        else
        {
            body.position = (Vector2)nextPosition;
            movingTransform.position = nextPosition;
        }

        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }

    private void ApplySeekPosition(Vector3 position)
    {
        if (movingTransform == null)
        {
            return;
        }

        position.z = movingTransform.position.z;

        if (body != null && Application.isPlaying)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = (Vector2)position;
            movingTransform.position = position;
            return;
        }

        movingTransform.position = position;
    }

    private void FaceDirection(float deltaX)
    {
        if (!faceMoveDirection || movingTransform == null || Mathf.Abs(deltaX) <= 0.001f)
        {
            return;
        }

        ApplyFacingSign(deltaX > 0f ? 1f : -1f);
    }

    private bool ApplyFacingOverride()
    {
        switch (facingOverride)
        {
            case TimelineSpineFacingOverride.FaceLeft:
                ApplyFacingSign(-1f);
                return true;
            case TimelineSpineFacingOverride.FaceRight:
                ApplyFacingSign(1f);
                return true;
            default:
                return false;
        }
    }

    private void ApplyFacingSign(float sign)
    {
        if (movingTransform == null || Mathf.Abs(sign) <= 0.001f)
        {
            return;
        }

        if (player != null)
        {
            player.FacePosition(movingTransform.position + Vector3.right * sign);
            return;
        }

        Vector3 scale = movingTransform.localScale;
        if (invertFacing)
        {
            scale.x = Mathf.Abs(scale.x) * (sign > 0f ? -1f : 1f);
        }
        else
        {
            scale.x = Mathf.Abs(scale.x) * sign;
        }

        movingTransform.localScale = scale;
    }

    private void SnapToPosition(Vector3 position)
    {
        if (movingTransform == null)
        {
            return;
        }

        if (moveAxis == TimelineSpineMoveAxis.Horizontal)
        {
            position.y = movingTransform.position.y;
        }

        position.z = movingTransform.position.z;

        if (body != null && Application.isPlaying)
        {
            body.MovePosition((Vector2)position);
        }
        else
        {
            movingTransform.position = position;
        }
    }

    private void StopMovement()
    {
        if (seekPreview)
        {
            return;
        }

        if (player != null)
        {
            player.StopCutsceneMoveInput();
        }

        if (body == null || !Application.isPlaying)
        {
            return;
        }

        body.linearVelocity = applyGravity
            ? new Vector2(0f, body.linearVelocity.y)
            : Vector2.zero;
    }

    private void StopHorizontalMovement()
    {
        if (body == null || !Application.isPlaying)
        {
            return;
        }

        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }

    private void StopForPetrifying()
    {
        StopHorizontalMovement();
    }

    private bool IsPetrifying()
    {
        return petrifyController != null && petrifyController.IsPetrifying;
    }

    private void SetMoveAnimation(bool moving)
    {
        if (!controlAnimation || animationSpine == null || seekPreview)
        {
            return;
        }

        if (!moving)
        {
            StopMoveAnimation();
            return;
        }

        if (moveAnimationPlaying && animationSpine.IsPlaying(moveState))
        {
            return;
        }

        if (moveState != AnimationSpine.State.None && animationSpine.HasStateAnimation(moveState))
        {
            TrackEntry entry = animationSpine.Play(moveState, true, animationTrackIndex);
            if (entry != null)
            {
                entry.Reverse = false;
            }

            moveAnimationPlaying = true;
        }
    }

    private void StopMoveAnimation()
    {
        if (!controlAnimation || animationSpine == null || !moveAnimationPlaying || seekPreview)
        {
            return;
        }

        if (returnToIdleWhenStopped
            && idleState != AnimationSpine.State.None
            && animationSpine.HasStateAnimation(idleState))
        {
            TrackEntry entry = animationSpine.Play(idleState, true, animationTrackIndex);
            if (entry != null)
            {
                entry.Reverse = false;
            }
        }

        moveAnimationPlaying = false;
    }

    private void ClearReferences()
    {
        animationSpine = null;
        movingTransform = null;
        body = null;
        player = null;
        petrifyController = null;
        pathStartPosition = Vector3.zero;
        pathStopPosition = Vector3.zero;
        cachedMoveDuration = 0f;
        gravityScaleBeforeClip = 0f;
        gravitySuppressed = false;
    }
}

