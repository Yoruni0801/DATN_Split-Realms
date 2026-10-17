using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

[DefaultExecutionOrder(100)]
public class PlayerGravityToggleInput : MonoBehaviour, ITimelineInputAction
{
    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.M;
    [SerializeField] private float toggleDelay = 0f;

    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private bool autoFindPlayer = true;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 0.08f;

    [Header("Gravity Flip")]
    [SerializeField] private bool requireClearGravityDirection = true;
    [SerializeField] private float gravityDirectionClearance = 0.12f;
    [SerializeField] private bool resetGravityOnDisable = true;

    [Header("Visual Flip")]
    [SerializeField] private Transform[] transformsToFlip;
    [SerializeField] private Vector3 normalRotation = Vector3.zero;
    [SerializeField] private Vector3 invertedRotation = new Vector3(0f, 0f, 180f);

    [Header("Cinemachine Follow")]
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private CinemachineFollow cinemachineFollow;
    [SerializeField] private bool updateCinemachineFollowOffset = true;
    [SerializeField] private Vector3 normalFollowOffset;
    [SerializeField] private Vector3 invertedFollowOffset;
    [SerializeField] private float followOffsetTransitionDuration = 0.35f;
    [SerializeField] private AnimationCurve followOffsetTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Fall Speed & Control")]
    [SerializeField] private float initialFallSpeed = 3f;
    [SerializeField] private float maxFallSpeed = 8f;
    [SerializeField] private bool useConstantFallSpeed = false;
    [SerializeField] private float fallKickImpulse = 0f;
    [SerializeField] private float landingImpulse = 0f;

    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
    private ContactFilter2D groundFilter;
    private Rigidbody2D playerRigidbody;
    private Collider2D playerCollider;
    private bool gravityInverted;
    private bool waitingForLeaveGround;
    private bool waitingForLanding;
    private Vector3 followOffsetStart;
    private Vector3 followOffsetTarget;
    private float followOffsetTransitionTimer;
    private bool isFollowOffsetTransitioning;

    public KeyCode ToggleKey
    {
        get => toggleKey;
        set => toggleKey = value;
    }

    public bool IsInverted => gravityInverted;

    public void Execute()
    {
        Debug.Log("GravityToggleInput");
        ToggleGravity();
    }

    private void Awake()
    {
        CacheReferences();
        RefreshGroundFilter();
    }

    private void OnEnable()
    {
        CacheReferences();
        RefreshGroundFilter();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (playerCollider != null && !playerCollider.enabled)
        {
            playerCollider.enabled = true;
        }

        if (resetGravityOnDisable && gravityInverted && player != null)
        {
            gravityInverted = false;
            ApplyGravityInvertedPreservingCollider(false);
        }

        waitingForLeaveGround = false;
        waitingForLanding = false;
    }

    private void Update()
    {
        CacheReferences();
        UpdateCinemachineFollowOffset();

        if (TimelineInputController.Instance != null
            && TimelineInputController.Instance.IsWaitingForInput())
        {
            return;
        }

        if (player == null || toggleKey == KeyCode.None || !Input.GetKeyDown(toggleKey))
        {
            return;
        }

        if (toggleDelay > 0f)
        {
            StartCoroutine(ToggleGravityRoutine());
        }
        else
        {
            ToggleGravity();
        }
    }

    private IEnumerator ToggleGravityRoutine()
    {
        yield return new WaitForSeconds(toggleDelay);
        ToggleGravity();
    }

    private void FixedUpdate()
    {
        if (playerRigidbody == null || playerCollider == null || !playerCollider.enabled)
        {
            return;
        }

        bool grounded = IsGroundedInGravityDirection();
        if (waitingForLeaveGround)
        {
            if (grounded)
            {
                return;
            }

            waitingForLeaveGround = false;
            waitingForLanding = true;
        }

        if (waitingForLanding)
        {
            ApplyFallSpeedControl();

            if (grounded)
            {
                ApplyDirectionalImpulse(GetGravityDirection(), landingImpulse);
                waitingForLanding = false;
            }
        }
    }

    public void ToggleGravity()
    {
        CacheReferences();
        if (player == null || playerRigidbody == null || playerCollider == null)
        {
            return;
        }

        playerCollider.enabled = true;
        bool nextInverted = !gravityInverted;
        Vector2 nextGravityDirection = nextInverted ? Vector2.up : Vector2.down;
        if (requireClearGravityDirection && IsBlockedInDirection(nextGravityDirection, gravityDirectionClearance))
        {
            playerRigidbody.linearVelocity = RemoveVelocityToward(playerRigidbody.linearVelocity, nextGravityDirection);
            return;
        }

        gravityInverted = nextInverted;
        playerRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        ApplyGravityInvertedPreservingCollider(gravityInverted);

        Vector2 gravityDirection = GetGravityDirection();
        EnsureInitialFallSpeed(gravityDirection);
        ApplyDirectionalImpulse(gravityDirection, fallKickImpulse);

        waitingForLeaveGround = true;
        waitingForLanding = false;
    }

    public void SetGravityInverted(bool inverted)
    {
        CacheReferences();
        if (player == null)
        {
            return;
        }

        gravityInverted = inverted;
        ApplyGravityInvertedPreservingCollider(inverted);
    }

    private void CacheReferences()
    {
        if (player == null && autoFindPlayer)
        {
            player = PlayerRegistry.Current != null
                ? PlayerRegistry.Current
                : FindFirstObjectByType<Player>();
        }

        if (player == null)
        {
            playerRigidbody = null;
            playerCollider = null;
            return;
        }

        if (playerRigidbody == null)
        {
            playerRigidbody = player.GetComponent<Rigidbody2D>();
        }

        if (playerCollider == null)
        {
            playerCollider = player.GetComponent<Collider2D>();
        }

        if (cinemachineFollow == null && cinemachineCamera != null)
        {
            cinemachineFollow = cinemachineCamera.GetComponent<CinemachineFollow>();
        }
    }

    private void RefreshGroundFilter()
    {
        int layerMask = groundLayer.value;
        if (layerMask == 0)
        {
            layerMask = LayerMask.GetMask("Ground", "Platform", "Default");
        }

        if (layerMask == 0)
        {
            layerMask = Physics2D.DefaultRaycastLayers;
        }

        groundFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = layerMask,
            useTriggers = false
        };
    }

    private bool IsGroundedInGravityDirection()
    {
        RefreshGroundFilter();
        Vector2 direction = GetGravityDirection();
        int hitCount = playerCollider.Cast(
            direction,
            groundFilter,
            groundHits,
            Mathf.Max(0.01f, groundCheckDistance));

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = groundHits[i].collider;
            if (hitCollider != null && hitCollider.transform.root != playerCollider.transform.root)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsBlockedInDirection(Vector2 direction, float distance)
    {
        if (playerCollider == null || !playerCollider.enabled)
        {
            return false;
        }

        RefreshGroundFilter();
        int hitCount = playerCollider.Cast(
            direction,
            groundFilter,
            groundHits,
            Mathf.Max(0.01f, distance));

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = groundHits[i].collider;
            if (hitCollider != null && hitCollider.transform.root != playerCollider.transform.root)
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyGravityInvertedPreservingCollider(bool inverted)
    {
        if (player == null)
        {
            return;
        }

        Vector2 colliderCenterBefore = playerCollider != null && playerCollider.enabled
            ? (Vector2)playerCollider.bounds.center
            : Vector2.zero;

        player.SetGravityInverted(inverted);
        ApplyVisualFlip(inverted);
        ApplyCinemachineFollowOffset(inverted);
        Physics2D.SyncTransforms();

        if (playerRigidbody == null || playerCollider == null || !playerCollider.enabled)
        {
            return;
        }

        Vector2 colliderCenterAfter = playerCollider.bounds.center;
        Vector2 correction = colliderCenterBefore - colliderCenterAfter;
        if (correction.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        playerRigidbody.position += correction;
        Physics2D.SyncTransforms();
    }

    private void ApplyVisualFlip(bool inverted)
    {
        Vector3 rotation = inverted ? invertedRotation : normalRotation;
        foreach (Transform transformToFlip in transformsToFlip)
        {
            if (transformToFlip != null)
            {
                transformToFlip.localRotation = Quaternion.Euler(rotation);
            }
        }
    }

    private void ApplyCinemachineFollowOffset(bool inverted)
    {
        if (!updateCinemachineFollowOffset || cinemachineFollow == null)
        {
            return;
        }

        followOffsetStart = cinemachineFollow.FollowOffset;
        followOffsetTarget = inverted ? invertedFollowOffset : normalFollowOffset;
        followOffsetTransitionTimer = 0f;
        isFollowOffsetTransitioning = followOffsetTransitionDuration > 0f;

        if (!isFollowOffsetTransitioning)
        {
            cinemachineFollow.FollowOffset = followOffsetTarget;
        }
    }

    private void UpdateCinemachineFollowOffset()
    {
        if (!isFollowOffsetTransitioning || cinemachineFollow == null)
        {
            return;
        }

        followOffsetTransitionTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(followOffsetTransitionTimer / followOffsetTransitionDuration);
        float easedProgress = followOffsetTransitionCurve != null
            ? followOffsetTransitionCurve.Evaluate(progress)
            : progress;

        cinemachineFollow.FollowOffset = Vector3.LerpUnclamped(
            followOffsetStart,
            followOffsetTarget,
            easedProgress);

        if (progress >= 1f)
        {
            cinemachineFollow.FollowOffset = followOffsetTarget;
            isFollowOffsetTransitioning = false;
        }
    }

    private Vector2 GetGravityDirection()
    {
        return playerRigidbody != null && playerRigidbody.gravityScale < 0f
            ? Vector2.up
            : Vector2.down;
    }

    private void EnsureInitialFallSpeed(Vector2 direction)
    {
        float targetSpeed = Mathf.Max(0f, initialFallSpeed);
        float currentSpeed = Vector2.Dot(playerRigidbody.linearVelocity, direction);
        if (currentSpeed >= targetSpeed)
        {
            return;
        }

        playerRigidbody.linearVelocity += direction * (targetSpeed - currentSpeed);
    }

    private void ApplyFallSpeedControl()
    {
        if (playerRigidbody == null)
        {
            return;
        }

        Vector2 direction = GetGravityDirection();
        float currentFallSpeed = Vector2.Dot(playerRigidbody.linearVelocity, direction);

        if (useConstantFallSpeed)
        {
            float targetSpeed = Mathf.Max(0f, maxFallSpeed);
            if (Mathf.Abs(currentFallSpeed - targetSpeed) > 0.01f)
            {
                playerRigidbody.linearVelocity += direction * (targetSpeed - currentFallSpeed);
            }
        }
        else
        {
            if (maxFallSpeed > 0f && currentFallSpeed > maxFallSpeed)
            {
                playerRigidbody.linearVelocity += direction * (maxFallSpeed - currentFallSpeed);
            }
        }
    }

    private void ApplyDirectionalImpulse(Vector2 direction, float impulse)
    {
        if (impulse <= 0f)
        {
            return;
        }

        playerRigidbody.AddForce(direction * impulse, ForceMode2D.Impulse);
    }

    private static Vector2 RemoveVelocityToward(Vector2 velocity, Vector2 direction)
    {
        float speedToward = Vector2.Dot(velocity, direction);
        if (speedToward <= 0f)
        {
            return velocity;
        }

        return velocity - direction * speedToward;
    }
}
