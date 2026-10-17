using Spine;
using UnityEngine;
using UnityEngine.Playables;

public class TimelinePlayerSpineMovePointAToBBehaviour : PlayableBehaviour
{
    public Transform pointA;
    public Transform pointB;
    public bool snapToPointAOnEnter;
    public TimelinePlayerSpineMoveAxis moveAxis;
    public float moveSpeed;
    public float stopDistance;
    public bool controlAnimation;
    public AnimationSpine.State moveState;
    public AnimationSpine.State idleState;
    public string moveAnimationName;
    public bool loopMoveAnimation;
    public bool returnToIdleWhenStopped;
    public int animationTrackIndex;
    public Player player;
    public bool lockPlayerControl;
    public bool unlockPlayerControlOnExit;
    public bool faceMoveDirection;
    public bool invertFacing;
    public TimelinePlayerSpineFacingOverride facingOverride;
    public bool hideWhenReached;
    public bool showOnEnter;

    private AnimationSpine animationSpine;
    private Transform movingTransform;
    private Rigidbody2D body;
    private Player resolvedPlayer;
    private bool clipActive;
    private bool playerControlLocked;
    private bool hiddenWhenReached;
    private bool moveAnimationPlaying;
    private Vector3 pathStartPosition;
    private Vector3 pathEndPosition;
    private float cachedMoveDuration;

    public void Enter(AnimationSpine boundPlayerSpine)
    {
        if (boundPlayerSpine == null || pointA == null || pointB == null)
        {
            Exit();
            return;
        }

        if (showOnEnter)
        {
            boundPlayerSpine.gameObject.SetActive(true);
        }

        if (animationSpine != boundPlayerSpine)
        {
            Exit();
            Bind(boundPlayerSpine);
        }

        if (clipActive)
        {
            return;
        }

        clipActive = true;
        hiddenWhenReached = false;
        moveAnimationPlaying = false;

        pathStartPosition = ResolvePointPosition(pointA.position);
        pathEndPosition = ResolvePointPosition(pointB.position);
        cachedMoveDuration = ResolveMoveDuration(Vector3.Distance(pathStartPosition, pathEndPosition));

        if (snapToPointAOnEnter)
        {
            MoveBody(pathStartPosition);
        }

        ApplyFacingOverride();
        LockPlayerControl();
        SetMoveAnimation(true);
    }

    public void Process(float localTime)
    {
        if (!clipActive || hiddenWhenReached)
        {
            return;
        }

        if (animationSpine == null || movingTransform == null || pointA == null || pointB == null)
        {
            Exit();
            return;
        }

        LockPlayerControl();

        float t = cachedMoveDuration > 0f
            ? Mathf.Clamp01(Mathf.Max(0f, localTime) / cachedMoveDuration)
            : 1f;

        Vector3 currentPosition = movingTransform.position;
        Vector3 nextPosition = Vector3.Lerp(pathStartPosition, pathEndPosition, t);
        if (moveAxis == TimelinePlayerSpineMoveAxis.XOnly)
        {
            nextPosition.y = currentPosition.y;
            nextPosition.z = currentPosition.z;
        }

        Vector3 delta = nextPosition - currentPosition;
        if (!ApplyFacingOverride())
        {
            FaceDirection(delta.x);
        }

        MoveBody(nextPosition);

        bool stillMoving = t < 0.999f;
        SetMoveAnimation(stillMoving);

        if (!stillMoving || Vector3.Distance(nextPosition, pathEndPosition) <= stopDistance)
        {
            MoveBody(pathEndPosition);
            StopMovement();
            SetMoveAnimation(false);
            HidePlayerIfReached();
        }
    }

    public void Exit()
    {
        if (!clipActive)
        {
            ClearReferences();
            return;
        }

        StopMovement();

        if (!hiddenWhenReached)
        {
            SetMoveAnimation(false);
        }

        UnlockPlayerControl();

        clipActive = false;
        playerControlLocked = false;
        hiddenWhenReached = false;
        moveAnimationPlaying = false;
        ClearReferences();
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        Exit();
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        Exit();
    }

    private void Bind(AnimationSpine boundPlayerSpine)
    {
        animationSpine = boundPlayerSpine;
        movingTransform = animationSpine.transform;
        body = movingTransform.GetComponent<Rigidbody2D>();
        ResolvePlayer();
    }

    private void LockPlayerControl()
    {
        if (!lockPlayerControl)
        {
            return;
        }

        ResolvePlayer();
        if (resolvedPlayer == null)
        {
            return;
        }

        resolvedPlayer.SetInputActive(false);
        playerControlLocked = true;
    }

    private void UnlockPlayerControl()
    {
        if (!unlockPlayerControlOnExit || !playerControlLocked || resolvedPlayer == null)
        {
            return;
        }

        resolvedPlayer.SetInputActive(true);
    }

    private void ResolvePlayer()
    {
        if (resolvedPlayer != null)
        {
            return;
        }

        resolvedPlayer = player;

        if (movingTransform != null)
        {
            if (resolvedPlayer == null)
            {
                resolvedPlayer = movingTransform.GetComponent<Player>();
            }

            if (resolvedPlayer == null)
            {
                resolvedPlayer = movingTransform.GetComponentInParent<Player>();
            }

            if (resolvedPlayer == null)
            {
                resolvedPlayer = movingTransform.GetComponentInChildren<Player>();
            }
        }

        if (resolvedPlayer == null)
        {
            resolvedPlayer = Object.FindAnyObjectByType<Player>();
        }
    }

    private Vector3 ResolvePointPosition(Vector3 pointPosition)
    {
        if (movingTransform == null || moveAxis == TimelinePlayerSpineMoveAxis.XY)
        {
            return pointPosition;
        }

        Vector3 currentPosition = movingTransform.position;
        return new Vector3(pointPosition.x, currentPosition.y, currentPosition.z);
    }

    private float ResolveMoveDuration(float distance)
    {
        if (distance <= 0f)
        {
            return 0f;
        }

        return moveSpeed > 0f ? distance / moveSpeed : 0f;
    }

    private void MoveBody(Vector3 position)
    {
        if (movingTransform == null)
        {
            return;
        }

        if (moveAxis == TimelinePlayerSpineMoveAxis.XOnly)
        {
            position.y = movingTransform.position.y;
            position.z = movingTransform.position.z;
        }

        if (body != null && Application.isPlaying)
        {
            body.position = (Vector2)position;
            body.linearVelocity = moveAxis == TimelinePlayerSpineMoveAxis.XOnly
                ? new Vector2(0f, body.linearVelocity.y)
                : Vector2.zero;
            return;
        }

        movingTransform.position = position;
    }

    private void StopMovement()
    {
        ResolvePlayer();
        if (resolvedPlayer != null)
        {
            resolvedPlayer.StopCutsceneMoveInput();
        }

        if (body == null || !Application.isPlaying)
        {
            return;
        }

        body.linearVelocity = moveAxis == TimelinePlayerSpineMoveAxis.XOnly
            ? new Vector2(0f, body.linearVelocity.y)
            : Vector2.zero;
    }

    private void SetMoveAnimation(bool moving)
    {
        if (!controlAnimation || animationSpine == null)
        {
            return;
        }

        if (!moving)
        {
            StopMoveAnimation();
            return;
        }

        if (moveAnimationPlaying)
        {
            return;
        }

        TrackEntry entry = null;
        if (!string.IsNullOrWhiteSpace(moveAnimationName))
        {
            entry = animationSpine.PlayAnimationForced(
                moveAnimationName,
                loopMoveAnimation,
                animationTrackIndex);
        }
        else if (moveState != AnimationSpine.State.None && animationSpine.HasStateAnimation(moveState))
        {
            entry = animationSpine.PlayForced(moveState, loopMoveAnimation, animationTrackIndex);
        }

        if (entry != null)
        {
            entry.Reverse = false;
            moveAnimationPlaying = true;
        }
    }

    private void StopMoveAnimation()
    {
        if (!controlAnimation || animationSpine == null || !moveAnimationPlaying)
        {
            return;
        }

        if (returnToIdleWhenStopped
            && idleState != AnimationSpine.State.None
            && animationSpine.HasStateAnimation(idleState))
        {
            TrackEntry entry = animationSpine.PlayForced(idleState, true, animationTrackIndex);
            if (entry != null)
            {
                entry.Reverse = false;
            }
        }

        moveAnimationPlaying = false;
    }

    private bool ApplyFacingOverride()
    {
        switch (facingOverride)
        {
            case TimelinePlayerSpineFacingOverride.FaceLeft:
                ApplyFacingSign(-1f);
                return true;
            case TimelinePlayerSpineFacingOverride.FaceRight:
                ApplyFacingSign(1f);
                return true;
            default:
                return false;
        }
    }

    private void FaceDirection(float deltaX)
    {
        if (!faceMoveDirection || Mathf.Abs(deltaX) <= 0.001f)
        {
            return;
        }

        ApplyFacingSign(deltaX > 0f ? 1f : -1f);
    }

    private void ApplyFacingSign(float sign)
    {
        if (movingTransform == null || Mathf.Abs(sign) <= 0.001f)
        {
            return;
        }

        Vector3 scale = movingTransform.localScale;
        float directionSign = invertFacing ? -sign : sign;
        scale.x = Mathf.Abs(scale.x) * directionSign;
        movingTransform.localScale = scale;
    }

    private void HidePlayerIfReached()
    {
        if (!hideWhenReached || hiddenWhenReached || movingTransform == null)
        {
            return;
        }

        hiddenWhenReached = true;
        movingTransform.gameObject.SetActive(false);
    }

    private void ClearReferences()
    {
        animationSpine = null;
        movingTransform = null;
        body = null;
        resolvedPlayer = null;
        playerControlLocked = false;
        pathStartPosition = Vector3.zero;
        pathEndPosition = Vector3.zero;
        cachedMoveDuration = 0f;
    }
}
