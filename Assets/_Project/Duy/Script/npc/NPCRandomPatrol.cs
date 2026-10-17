using UnityEngine;

public enum NPCFacingDirection
{
    Right,
    Left
}

[RequireComponent(typeof(PetrifyController))]
public class NPCRandomPatrol : MonoBehaviour
{
    public bool showDebugGUI = true;
    public bool enablePatrol = true;
    public bool canMove = true;
    [SerializeField] private bool returnToIdleWhenStopped = true;

    public LayerMask groundLayer;
    public Transform detectionOrigin;

    public float patrolRange = 100f;
    public float moveSpeed = 2f;
    public float wallCheckDistance = 0.5f;
    public float ledgeCheckDistance = 0.8f;
    public float guiOffsetHeight = 1.5f;

    public NPCFacingDirection startFacingDirection = NPCFacingDirection.Right;
    public bool invertFacing = false;
    [SerializeField] private AnimationSpine.State walkState = AnimationSpine.State.Walk;
    [SerializeField] private AnimationSpine.State idleState = AnimationSpine.State.Idle;
    private bool controlMoveAnimation = true;

    private float minX;
    private float maxX;
    private Vector3 targetPosition;

    // Debug GUI
    private bool isHittingWall = false;
    private bool isMissingLedge = false;

    // Components & state
    private AnimationSpine animationSpine;
    private PetrifyController petrifyController;
    private Collider2D cachedCollider;
    private bool isFacingRight = true;
    private bool timelinePaused;
    private bool isPlayingWalkAnimation;
    private bool timelineMoveAnimationControlled;

    void Awake()
    {
        animationSpine = GetComponentInChildren<AnimationSpine>();
        petrifyController = GetComponent<PetrifyController>();
        cachedCollider = GetComponent<Collider2D>();
    }

    void Start()
    {
        EnsureBounds();
        ApplyFacingDirection(startFacingDirection);

        if (enablePatrol && canMove)
        {
            if (isFacingRight)
            {
                TurnRight();
            }
            else
            {
                TurnLeft();
            }

            PlayMoveAnimation(true);
        }
        else
        {
            PlayStoppedAnimation();
        }
    }

    void Update()
    {
        if (petrifyController != null && petrifyController.IsPetrifying)
        {
            return;
        }

        if (timelinePaused)
        {
            if (!timelineMoveAnimationControlled)
            {
                PlayStoppedAnimation();
            }

            return;
        }

        if (!enablePatrol || !canMove)
        {
            StopPatrolMovement();
            return;
        }

        if (CheckForObstacles())
        {
            TurnAround();
        }

        Move();
    }

    void Move()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
        PlayMoveAnimation(true);

        if (Mathf.Abs(transform.position.x - targetPosition.x) < 0.1f)
        {
            TurnAround();
        }
    }

    public void TurnAround()
    {
        EnsureBounds();

        if (isFacingRight)
        {
            TurnLeft();
        }
        else
        {
            TurnRight();
        }
    }

    private void TurnLeft()
    {
        ApplyFacingDirection(NPCFacingDirection.Left);
        float currentX = transform.position.x;
        float targetX = minX < currentX - 1f ? Random.Range(minX, currentX - 1f) : minX;
        targetPosition = new Vector3(targetX, transform.position.y, transform.position.z);
    }

    private void TurnRight()
    {
        ApplyFacingDirection(NPCFacingDirection.Right);
        float currentX = transform.position.x;
        float targetX = maxX > currentX + 1f ? Random.Range(currentX + 1f, maxX) : maxX;
        targetPosition = new Vector3(targetX, transform.position.y, transform.position.z);
    }

    public void SetNewRandomTarget()
    {
        if (this == null) return;
        TurnAround();
    }

    private void EnsureBounds()
    {
        if (minX == 0f && maxX == 0f)
        {
            minX = transform.position.x - patrolRange;
            maxX = transform.position.x + patrolRange;
        }
    }

    bool CheckForObstacles()
    {
        Transform origin = detectionOrigin != null ? detectionOrigin : transform;
        Vector2 direction = isFacingRight ? Vector2.right : Vector2.left;

        RaycastHit2D wallHit = Physics2D.Raycast(origin.position, direction, wallCheckDistance, groundLayer);
        Debug.DrawRay(origin.position, direction * wallCheckDistance, Color.red);
        isHittingWall = wallHit.collider != null && (cachedCollider == null || wallHit.collider != cachedCollider);

        Vector2 ledgeOrigin = (Vector2)origin.position + direction * wallCheckDistance;
        GroundCheckResult groundAhead = GroundCheck.CheckAt(
            ledgeOrigin,
            cachedCollider != null ? cachedCollider : GetComponent<Collider2D>(),
            GroundCheck.Override(
                groundLayer,
                rayLength: ledgeCheckDistance,
                checkMode: GroundCheckMode.Raycast));
        isMissingLedge = !groundAhead.IsGrounded;
        Debug.DrawRay(ledgeOrigin, Vector2.down * ledgeCheckDistance, isMissingLedge ? Color.red : Color.green);

        return isHittingWall || isMissingLedge;
    }

    public void SetFacingDirection(NPCFacingDirection direction)
    {
        ApplyFacingDirection(direction);
        if (direction == NPCFacingDirection.Right)
        {
            TurnRight();
        }
        else
        {
            TurnLeft();
        }
    }

    public void SetInvertFacing(bool invert)
    {
        if (invertFacing == invert)
        {
            return;
        }

        invertFacing = invert;
        ApplyFacingDirection(isFacingRight ? NPCFacingDirection.Right : NPCFacingDirection.Left);
    }

    public static void SetAllInvertFacing(bool invert, bool includeInactive = false)
    {
        NPCRandomPatrol[] patrols = FindObjectsByType<NPCRandomPatrol>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < patrols.Length; i++)
        {
            if (patrols[i] != null)
            {
                patrols[i].SetInvertFacing(invert);
            }
        }
    }

    public void SetEnablePatrol(bool enable)
    {
        enablePatrol = enable;

        if (enable)
        {
            EnsureBounds();

            if (canMove && !timelinePaused && (petrifyController == null || !petrifyController.IsPetrifying))
            {
                if (isFacingRight)
                {
                    TurnRight();
                }
                else
                {
                    TurnLeft();
                }

                PlayMoveAnimation(true);
            }
        }
        else
        {
            StopPatrolMovement();
        }
    }

    public static void EnableAllPatrol(bool includeInactive = false)
    {
        SetAllPatrolEnabled(true, includeInactive);
    }

    public static void DisableAllPatrol(bool includeInactive = false)
    {
        SetAllPatrolEnabled(false, includeInactive);
    }

    public static void SetAllPatrolEnabled(bool enable, bool includeInactive = false)
    {
        NPCRandomPatrol[] patrols = FindObjectsByType<NPCRandomPatrol>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < patrols.Length; i++)
        {
            if (patrols[i] != null)
            {
                patrols[i].SetEnablePatrol(enable);
            }
        }
    }

    private void ApplyFacingDirection(NPCFacingDirection direction)
    {
        bool shouldFaceRight = direction == NPCFacingDirection.Right;
        Vector3 scale = transform.localScale;
        float scaleX = Mathf.Abs(scale.x);
        bool positiveScale = invertFacing ? !shouldFaceRight : shouldFaceRight;
        scale.x = positiveScale ? scaleX : -scaleX;
        transform.localScale = scale;
        isFacingRight = shouldFaceRight;
    }

    public void SetTimelinePaused(bool paused)
    {
        SetTimelinePaused(paused, true);
    }

    public void SetTimelinePaused(bool paused, bool stopAnimationOnPause)
    {
        timelinePaused = paused;

        if (paused)
        {
            if (stopAnimationOnPause && !timelineMoveAnimationControlled)
            {
                PlayStoppedAnimation();
            }

            return;
        }

        timelineMoveAnimationControlled = false;
        if (enablePatrol && canMove)
        {
            EnsureBounds();
            if (isFacingRight)
            {
                TurnRight();
            }
            else
            {
                TurnLeft();
            }

            PlayMoveAnimation(true);
        }
        else
        {
            StopPatrolMovement();
        }
    }

    public void FaceTimelineDirection(float directionX)
    {
        if (Mathf.Abs(directionX) <= 0.001f)
        {
            return;
        }

        ApplyFacingDirection(directionX > 0f ? NPCFacingDirection.Right : NPCFacingDirection.Left);
    }

    public void SetTimelineMoveAnimation(bool moving)
    {
        timelineMoveAnimationControlled = moving;
        PlayMoveAnimation(moving);
    }

    public void StopTimelineMoveAnimation()
    {
        timelineMoveAnimationControlled = false;
        PlayStoppedAnimation();
    }

    private void StopPatrolMovement()
    {
        PlayStoppedAnimation();
    }

    private void PlayMoveAnimation(bool moving)
    {
        if (!controlMoveAnimation)
        {
            return;
        }

        if (!moving)
        {
            PlayStoppedAnimation();
            return;
        }

        if (isPlayingWalkAnimation && IsAnimationStillPlaying(walkState))
        {
            return;
        }

        PlayAnimation(walkState, true);
        isPlayingWalkAnimation = true;
    }

    private void PlayStoppedAnimation()
    {
        if (!controlMoveAnimation || !isPlayingWalkAnimation)
        {
            return;
        }

        if (returnToIdleWhenStopped)
        {
            PlayAnimation(idleState, true);
        }

        isPlayingWalkAnimation = false;
    }

    private void PlayAnimation(AnimationSpine.State state, bool loop)
    {
        if (state == AnimationSpine.State.None)
        {
            return;
        }

        if (animationSpine != null && animationSpine.HasStateAnimation(state))
        {
            animationSpine.Play(state, loop);
        }
    }

    private bool IsAnimationStillPlaying(AnimationSpine.State state)
    {
        if (state == AnimationSpine.State.None)
        {
            return false;
        }

        if (animationSpine != null)
        {
            return animationSpine.IsPlaying(state);
        }

        return false;
    }
}
