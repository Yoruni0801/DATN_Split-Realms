using UnityEngine;
using UnityEngine.Playables;

public class TimelineEnemyMovePointAToBBehaviour : PlayableBehaviour
{
    public Transform pointA;
    public Transform pointB;
    public bool snapToPointAOnEnter;
    public TimelineEnemyMoveAxis moveAxis;
    public TimelineEnemyMoveTiming moveTiming;
    public float moveSpeed;
    public bool hideWhenReached;
    public bool showOnEnter;
    public AnimationClip moveAnimation;
    public string moveStateName;
    public int layerIndex;
    public bool loopAnimation;
    public TimelineEnemyMoveAnimationExitMode animationExitMode;
    public string speedParameterName;
    public float movingSpeedParameterValue;
    public float stoppedSpeedParameterValue;
    public TimelineEnemyMoveFacingMode facingMode;
    public bool defaultFaceRight;

    private Transform enemyTransform;
    private Rigidbody2D body;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private bool clipActive;
    private Vector3 pathStartPosition;
    private Vector3 pathEndPosition;
    private int stateHash;
    private int previousStateHash;
    private float previousStateTime;
    private int speedParameterHash;
    private bool hasSpeedParameter;
    private Vector3 originalScale;
    private bool hiddenWhenReached;

    public void Enter(Transform boundEnemy)
    {
        if (boundEnemy == null || pointA == null || pointB == null)
        {
            Exit();
            return;
        }

        if (showOnEnter)
        {
            boundEnemy.gameObject.SetActive(true);
        }

        if (enemyTransform != boundEnemy)
        {
            Exit();
            Bind(boundEnemy);
        }

        if (clipActive)
        {
            return;
        }

        clipActive = true;
        hiddenWhenReached = false;

        pathStartPosition = ResolvePointPosition(pointA.position);
        pathEndPosition = ResolvePointPosition(pointB.position);

        if (snapToPointAOnEnter)
        {
            MoveBody(pathStartPosition);
        }

        BeginAnimation();
        SetSpeedParameter(true);
    }

    public void Process(float localTime, float clipDuration)
    {
        if (!clipActive || enemyTransform == null || pointA == null || pointB == null)
        {
            Exit();
            return;
        }

        if (hiddenWhenReached)
        {
            return;
        }

        float t = ResolveMoveProgress(localTime, clipDuration);
        Vector3 currentPosition = enemyTransform.position;
        Vector3 nextPosition = Vector3.Lerp(pathStartPosition, pathEndPosition, t);

        if (moveAxis == TimelineEnemyMoveAxis.XOnly)
        {
            nextPosition.y = currentPosition.y;
            nextPosition.z = currentPosition.z;
        }

        FaceDirection(nextPosition.x - currentPosition.x);
        MoveBody(nextPosition);
        PlayMoveAnimation(localTime);
        SetSpeedParameter(t < 0.999f);

        if (t >= 0.999f)
        {
            StopMovement();
            HideEnemyIfReached();
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
        SetSpeedParameter(false);
        if (!hiddenWhenReached)
        {
            RestorePreviousAnimationIfNeeded();
        }

        clipActive = false;
        ClearReferences();
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        Exit();
    }

    private void Bind(Transform boundEnemy)
    {
        enemyTransform = boundEnemy;
        body = enemyTransform.GetComponent<Rigidbody2D>();
        animator = enemyTransform.GetComponentInChildren<Animator>();
        spriteRenderer = enemyTransform.GetComponentInChildren<SpriteRenderer>();
        originalScale = enemyTransform.localScale;

        hasSpeedParameter = !string.IsNullOrWhiteSpace(speedParameterName);
        if (hasSpeedParameter)
        {
            speedParameterHash = Animator.StringToHash(speedParameterName);
        }
    }

    private Vector3 ResolvePointPosition(Vector3 pointPosition)
    {
        if (enemyTransform == null || moveAxis == TimelineEnemyMoveAxis.XY)
        {
            return pointPosition;
        }

        Vector3 currentPosition = enemyTransform.position;
        return new Vector3(pointPosition.x, currentPosition.y, currentPosition.z);
    }

    private float ResolveMoveProgress(float localTime, float clipDuration)
    {
        float duration = Mathf.Max(0.001f, clipDuration);
        if (moveTiming == TimelineEnemyMoveTiming.MoveSpeed)
        {
            float distance = Vector3.Distance(pathStartPosition, pathEndPosition);
            duration = moveSpeed > 0f ? distance / moveSpeed : 0.001f;
        }

        return Mathf.Clamp01(Mathf.Max(0f, localTime) / duration);
    }

    private void MoveBody(Vector3 position)
    {
        if (body != null && Application.isPlaying)
        {
            body.MovePosition(position);
            return;
        }

        enemyTransform.position = position;
    }

    private void StopMovement()
    {
        if (body == null || !Application.isPlaying)
        {
            return;
        }

        if (moveAxis == TimelineEnemyMoveAxis.XOnly)
        {
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        }
        else
        {
            body.linearVelocity = Vector2.zero;
        }
    }

    private void BeginAnimation()
    {
        if (animator == null)
        {
            return;
        }

        string resolvedStateName = ResolveStateName();
        stateHash = string.IsNullOrWhiteSpace(resolvedStateName)
            ? 0
            : Animator.StringToHash(resolvedStateName);

        int resolvedLayerIndex = ResolveLayerIndex();
        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(resolvedLayerIndex);
        previousStateHash = currentState.fullPathHash;
        previousStateTime = currentState.normalizedTime;
    }

    private void PlayMoveAnimation(float localTime)
    {
        if (animator == null || stateHash == 0)
        {
            return;
        }

        float animationLength = moveAnimation != null && moveAnimation.length > 0f
            ? moveAnimation.length
            : 1f;

        float normalizedTime = localTime / animationLength;
        normalizedTime = loopAnimation
            ? Mathf.Repeat(normalizedTime, 1f)
            : Mathf.Clamp01(normalizedTime);

        animator.Play(stateHash, ResolveLayerIndex(), normalizedTime);
        animator.Update(0f);
    }

    private void RestorePreviousAnimationIfNeeded()
    {
        if (animator == null ||
            animationExitMode != TimelineEnemyMoveAnimationExitMode.RestorePrevious ||
            previousStateHash == 0)
        {
            return;
        }

        animator.Play(previousStateHash, ResolveLayerIndex(), previousStateTime);
        animator.Update(0f);
    }

    private string ResolveStateName()
    {
        if (!string.IsNullOrWhiteSpace(moveStateName))
        {
            return moveStateName;
        }

        return moveAnimation != null ? moveAnimation.name : string.Empty;
    }

    private int ResolveLayerIndex()
    {
        if (animator == null || animator.layerCount <= 0)
        {
            return 0;
        }

        return Mathf.Clamp(layerIndex, 0, animator.layerCount - 1);
    }

    private void SetSpeedParameter(bool moving)
    {
        if (!hasSpeedParameter || animator == null)
        {
            return;
        }

        animator.SetFloat(
            speedParameterHash,
            moving ? movingSpeedParameterValue : stoppedSpeedParameterValue);
    }

    private void FaceDirection(float directionX)
    {
        if (facingMode == TimelineEnemyMoveFacingMode.None ||
            Mathf.Abs(directionX) < 0.001f)
        {
            return;
        }

        bool movingRight = directionX > 0f;
        if (facingMode == TimelineEnemyMoveFacingMode.SpriteRendererFlipX && spriteRenderer != null)
        {
            spriteRenderer.flipX = defaultFaceRight ? !movingRight : movingRight;
            return;
        }

        if (facingMode == TimelineEnemyMoveFacingMode.LocalScaleX && enemyTransform != null)
        {
            float xSign = defaultFaceRight == movingRight ? 1f : -1f;
            enemyTransform.localScale = new Vector3(
                Mathf.Abs(originalScale.x) * xSign,
                originalScale.y,
                originalScale.z);
        }
    }

    private void HideEnemyIfReached()
    {
        if (!hideWhenReached || hiddenWhenReached || enemyTransform == null)
        {
            return;
        }

        hiddenWhenReached = true;
        enemyTransform.gameObject.SetActive(false);
    }

    private void ClearReferences()
    {
        enemyTransform = null;
        body = null;
        animator = null;
        spriteRenderer = null;
        stateHash = 0;
        previousStateHash = 0;
        previousStateTime = 0f;
    }
}
