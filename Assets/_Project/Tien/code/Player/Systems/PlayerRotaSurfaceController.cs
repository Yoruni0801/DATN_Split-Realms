using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerRotaSurfaceController : MonoBehaviour
{
    public enum SurfaceSide
    {
        Floor,
        LeftWall,
        Ceiling,
        RightWall
    }

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 15f;
    [SerializeField] private float acceleration = 100f;
    [SerializeField] private float deceleration = 140f;
    [SerializeField] private float turnAcceleration = 180f;
    [SerializeField] private float airAcceleration = 45f;
    [SerializeField] private float jumpSpeed = 18f;
    [SerializeField, Range(0.1f, 1f)] private float jumpReleaseSpeedMultiplier = 0.45f;
    [SerializeField] private float gravity = 28f;
    [SerializeField] private float fallGravityMultiplier = 3f;
    [SerializeField] private float maxFallSpeed = 100f;
    [SerializeField] private float surfaceStickSpeed = 2f;
    
    [Header("Surface Check")]
    [Tooltip("Use PlayerLayerConfig.EnvironmentLayer (Default/Ground/Platform/Wall/Obstacle) as the only valid ROTA surfaces.")]
    [SerializeField] private bool useProjectEnvironmentLayers = true;

    [Tooltip("Used only when Use Project Environment Layers is disabled.")]
    [SerializeField] private LayerMask groundLayer = ~0;

    [Tooltip("While ROTA is active, the main body collider physically ignores every layer outside Ground Layer. Other player hitbox/trigger colliders are not changed.")]
    [SerializeField] private bool ignoreNonSurfacePhysicsLayers = true;

    [SerializeField] private SurfaceSide startSurface = SurfaceSide.Floor;
    [SerializeField] private float groundCheckDistance = 0.08f;
    [SerializeField, Range(0f, 1f)] private float groundNormalMinDot = 0.65f;
    [SerializeField] private float edgeCornerNudge = 0.14f;
    [SerializeField] private float edgeProbeDistance = 0.45f;
    [SerializeField] private float edgeAttachDistance = 0.9f;
    [SerializeField] private float edgeAttachCooldown = 0.12f;
    [SerializeField] private bool autoAttachAtEdges = true;

    [Header("Rotation")]
    [SerializeField] private bool rotatePlayerToSurface = true;
    [SerializeField] private float visualRotateSpeed = 720f;
    
    [Header("Camera")]
    [SerializeField] private bool rotateCameraToPlayer = true;
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private float cameraRotateSpeed = 360f;

    [Header("Animation")]
    [SerializeField] private bool updateSpineAnimation = true;
    [SerializeField] private AnimationSpine animationSpine;
    [SerializeField] private float walkAnimationThreshold = 0.05f;
    [SerializeField] private AnimationSpine.State idleState = AnimationSpine.State.Idle;
    [SerializeField] private AnimationSpine.State walkState = AnimationSpine.State.Walk;
    [SerializeField] private AnimationSpine.State jumpState = AnimationSpine.State.Jump;
    [SerializeField] private AnimationSpine.State fallState = AnimationSpine.State.Fall;

    [Header("Facing")]
    [SerializeField] private bool flipSpineOnMove = true;
    [SerializeField] private bool rightFacingUsesFlipX = false;
    [SerializeField] private bool invertFacingDirection;
    [SerializeField] private float flipMoveThreshold = 0.05f;

    [Header("Audio (Âm Thanh Di Chuyển & Nhảy)")]
    [SerializeField] private bool enableAudio = true;
    [SerializeField] private PlayerAudio playerAudio;
    [SerializeField] private AudioClip walkSound;
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioClip landSound;
    [SerializeField] private float walkStepInterval = 0.35f;
    [SerializeField, Range(0f, 1f)] private float audioVolume = 1f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool isGrounded;
    [SerializeField] private SurfaceSide currentSurface;
    [SerializeField] private Vector2 surfaceNormal;
    [SerializeField] private float localSpeedX;
    [SerializeField] private float localSpeedY;
    private float lastMoveSign = 1f;
    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private ContactFilter2D groundFilter;
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[16];
    private readonly ContactPoint2D[] contactPoints = new ContactPoint2D[16];

    private bool initialized;
    private bool hasGroundContact;
    private float inputX;
    private bool jumpHeld;
    private bool jumpQueued;
    private bool jumpReleasedQueued;
    private float edgeAttachCooldownTimer;
    private float targetSurfaceAngle;
    private AnimationSpine.State currentAnimationState = AnimationSpine.State.None;

    private RigidbodyType2D originalBodyType;
    private float originalGravityScale;
    private bool originalFreezeRotation;
    private LayerMask originalBodyExcludeLayers;
    private Quaternion originalPlayerRotation;
    private Quaternion originalCameraRotation;
    private bool cameraRotationCached;
    private float nextFootstepTime;
    private bool wasGroundedLastTick;

    public bool IsActive { get; private set; }
    public bool IsGrounded => isGrounded;
    public SurfaceSide CurrentSurface => currentSurface;
    public Vector2 SurfaceNormal => surfaceNormal;
    public Vector2 GravityDirection => -surfaceNormal;


    public void Configure(PlayerMovementDataSO movementData)
    {
        PlayerRotaMovementSettings settings = movementData != null
            ? movementData.Rota
            : PlayerRotaMovementSettings.Default;

        moveSpeed = settings.moveSpeed;
        acceleration = settings.acceleration;
        deceleration = settings.deceleration;
        turnAcceleration = settings.turnAcceleration;
        airAcceleration = settings.airAcceleration;
        jumpSpeed = settings.jumpSpeed;
        jumpReleaseSpeedMultiplier = settings.jumpReleaseSpeedMultiplier;
        gravity = settings.gravity;
        fallGravityMultiplier = settings.fallGravityMultiplier;
        maxFallSpeed = settings.maxFallSpeed;
        surfaceStickSpeed = settings.surfaceStickSpeed;
        groundCheckDistance = settings.groundCheckDistance;
        groundNormalMinDot = settings.groundNormalMinDot;
        edgeCornerNudge = settings.edgeCornerNudge;
        edgeAttachDistance = settings.edgeAttachDistance;
        edgeAttachCooldown = settings.edgeAttachCooldown;
        autoAttachAtEdges = settings.autoAttachAtEdges;
    }
    public void Initialize()
    {
        if (initialized)
            return;

        initialized = true;

        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();

        originalBodyType = rb.bodyType;
        originalGravityScale = rb.gravityScale;
        originalFreezeRotation = rb.freezeRotation;
        originalBodyExcludeLayers = bodyCollider.excludeLayers;
        originalPlayerRotation = transform.rotation;

        groundFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = false
        };

        RefreshGroundLayerMask();

        if (animationSpine == null)
            animationSpine = GetComponentInChildren<AnimationSpine>();

        if (playerAudio == null)
            playerAudio = GetComponent<PlayerAudio>() ?? GetComponentInParent<PlayerAudio>();

        if (cinemachineCamera == null)
            cinemachineCamera = FindFirstObjectByType<CinemachineCamera>();

        SetSurface(startSurface, false);
    }

    public void SetActive(bool active)
    {
        Initialize();

        if (IsActive == active)
            return;

        IsActive = active;

        if (active)
        {
            originalBodyType = rb.bodyType;
            originalGravityScale = rb.gravityScale;
            originalFreezeRotation = rb.freezeRotation;
            originalBodyExcludeLayers = bodyCollider.excludeLayers;
            originalPlayerRotation = transform.rotation;

            RefreshGroundLayerMask();
            ApplyRotaCollisionFilter();

            if (cinemachineCamera != null)
            {
                originalCameraRotation = cinemachineCamera.transform.rotation;
                cameraRotationCached = true;
            }

            // Important difference from the broken version:
            // keep Unity's Dynamic collision solver active and drive the body
            // with linearVelocity. Do not manually emulate move_and_collide().
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            inputX = 0f;
            localSpeedX = 0f;
            localSpeedY = 0f;
            jumpHeld = false;
            jumpQueued = false;
            jumpReleasedQueued = false;
            edgeAttachCooldownTimer = 0f;
            currentAnimationState = AnimationSpine.State.None;

            Vector3 currentScale = transform.localScale;
            currentScale.x = -Mathf.Abs(currentScale.x);
            transform.localScale = currentScale;

            if (animationSpine != null)
            {
                animationSpine.SetFlipX(false);
            }

            SetSurface(currentSurface, false);
            RefreshGrounded();

            // If the zone enables ROTA slightly above the floor, attach cleanly.
            if (!isGrounded)
                SnapToSurface();

            ApplySurfaceVelocity();
        }
        else
        {
            inputX = 0f;
            localSpeedX = 0f;
            localSpeedY = 0f;
            jumpHeld = false;
            jumpQueued = false;
            jumpReleasedQueued = false;
            isGrounded = false;
            hasGroundContact = false;
            currentAnimationState = AnimationSpine.State.None;

            if (animationSpine != null)
            {
                animationSpine.SetFlipX(false);
            }

            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = originalBodyType;
            rb.gravityScale = originalGravityScale;
            rb.freezeRotation = originalFreezeRotation;

            RestoreBodyCollisionFilter();

            transform.rotation = Quaternion.identity;

            if (cinemachineCamera != null && cameraRotationCached)
            {
                cinemachineCamera.transform.rotation = originalCameraRotation;
                cinemachineCamera.PreviousStateIsValid = false;
            }
        }
    }

    public void FixedTick(Vector2 moveInput, bool jumpPressed, bool jumpIsHeld)
    {
        if (!IsActive)
            return;

        // Do not Round() this value. Rounding was able to turn valid analog
        // input into zero and made the controller appear unable to move.
        inputX = Mathf.Clamp(moveInput.x, -1f, 1f);

        if (Mathf.Abs(inputX) > 0.01f)
        {
            lastMoveSign = Mathf.Sign(inputX);
        }
        else if (Mathf.Abs(localSpeedX) > 0.05f)
        {
            lastMoveSign = Mathf.Sign(localSpeedX);
        }

        if (jumpPressed)
            jumpQueued = true;

        if (!jumpIsHeld && (jumpHeld || jumpPressed))
            jumpReleasedQueued = true;

        jumpHeld = jumpIsHeld;

        if (edgeAttachCooldownTimer > 0f)
            edgeAttachCooldownTimer = Mathf.Max(0f, edgeAttachCooldownTimer - Time.fixedDeltaTime);

        bool wasGroundedBeforeRefresh = isGrounded;
        bool hadGroundContact = hasGroundContact || isGrounded;
        RefreshGrounded();

        if (!wasGroundedBeforeRefresh && isGrounded && localSpeedY <= 0.5f)
        {
            PlayLandAudio();
        }

        if (edgeAttachCooldownTimer <= 0f
            && autoAttachAtEdges
            && hadGroundContact
            && !hasGroundContact
            && !jumpQueued
            && localSpeedY <= 0.01f)
        {
            TryWalkAroundLostGround();
        }

        float targetSpeedX = inputX * moveSpeed;
        float accel;

        if(!isGrounded)
        {
            accel = airAcceleration;
        }
        else if (Mathf.Abs(inputX) < 0.01f)
        {
            accel = deceleration;
        }
        else if (Mathf.Sign(inputX) != Mathf.Sign(localSpeedX) && Mathf.Abs(localSpeedX) > 0.01f)
        {
            accel = turnAcceleration;
        }
        else
        {
            accel = acceleration;
        }

        localSpeedX = Mathf.MoveTowards(
            localSpeedX,
            targetSpeedX,
            accel * Time.fixedDeltaTime);

        if (isGrounded)
        {
            // Small velocity into the current surface keeps contact stable.
            localSpeedY = -surfaceStickSpeed;

            if (jumpQueued)
            {
                isGrounded = false;
                hasGroundContact = false;
                localSpeedY = jumpSpeed;
                PlayJumpAudio();
            }
        }
        else
        {
            float gravityMultiplier = localSpeedY <= 0f ? fallGravityMultiplier : 1f;
            localSpeedY = Mathf.Max(
                localSpeedY - gravity * gravityMultiplier * Time.fixedDeltaTime,
                -maxFallSpeed);
        }

        if (localSpeedY > 0f && jumpReleasedQueued)
            localSpeedY *= jumpReleaseSpeedMultiplier;

        jumpQueued = false;
        jumpReleasedQueued = false;

        ApplySurfaceVelocity();
        UpdateSpineFacing();

        if (rotatePlayerToSurface)
            RotateToSurface();

        UpdateSpineAnimation();
        TickMovementAudio();
    }
    private bool ProbeCurrentSurface(Vector2 direction)
    {
        RefreshGroundLayerMask();

        int hitCount = bodyCollider.Cast(
            direction.normalized,
            groundFilter,
            castHits,
            edgeProbeDistance);

        return HasSolidHit(hitCount);
    }

    private void LateUpdate()
    {
        if (!IsActive || !rotateCameraToPlayer)
            return;

        RotateCameraToPlayerDirection();
    }

    public void AttachToSurfaceFromFallPoint(SurfaceSide surface, bool keepMoveDirection)
    {
        Initialize();

        Vector2 carryVelocity = rb.linearVelocity;
        if (carryVelocity.sqrMagnitude < 0.0001f)
            carryVelocity = GetWorldVelocity();

        SetSurface(surface, false);
        localSpeedX = keepMoveDirection ? Vector2.Dot(carryVelocity, GetTangent()) : 0f;
        localSpeedY = -surfaceStickSpeed;
        jumpQueued = false;
        jumpReleasedQueued = false;

        SnapToSurface();
        ApplySurfaceVelocity();
    }

    public void AttachToSurface(SurfaceSide surface, bool keepMoveDirection = true)
    {
        Initialize();
        SetSurface(surface, keepMoveDirection);
        transform.rotation = Quaternion.Euler(0f, 0f, targetSurfaceAngle);
        SnapToSurface();
        ApplySurfaceVelocity();
    }

    public void ResetForRespawn()
    {
        ResetForRespawn(startSurface);
    }

    public void ResetForRespawn(SurfaceSide checkpointSurface)
    {
        Initialize();

        inputX = 0f;
        localSpeedX = 0f;
        localSpeedY = 0f;
        jumpHeld = false;
        jumpQueued = false;
        jumpReleasedQueued = false;
        edgeAttachCooldownTimer = 0f;
        isGrounded = false;
        hasGroundContact = false;
        currentAnimationState = AnimationSpine.State.None;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        Vector3 currentScale = transform.localScale;
        currentScale.x = -Mathf.Abs(currentScale.x);
        transform.localScale = currentScale;

        if (animationSpine != null)
        {
            animationSpine.SetFlipX(false);
        }

        SetSurface(checkpointSurface, false);
        transform.rotation = Quaternion.Euler(0f, 0f, targetSurfaceAngle);

        if (IsActive)
        {
            SnapToSurface();
            RefreshGrounded();
            ApplySurfaceVelocity();
        }
    }

    public void ResetHorizontalMotionAfterTeleport()
    {
        Initialize();
        inputX = 0f;
        localSpeedX = 0f;
        jumpQueued = false;
        jumpReleasedQueued = false;
    }

    private void RefreshGrounded()
    {
        RefreshGroundLayerMask();

        int hitCount = bodyCollider.Cast(
            GravityDirection,
            groundFilter,
            castHits,
            groundCheckDistance);

        hasGroundContact = HasSolidHit(hitCount);

        if (!hasGroundContact)
        {
            int contactCount = bodyCollider.GetContacts(groundFilter, contactPoints);
            hasGroundContact = HasSolidContact(contactCount);
        }

        // Positive local Y means jumping away from the surface.
        if (localSpeedY > 0.01f)
            hasGroundContact = false;

        isGrounded = hasGroundContact;

        if (isGrounded && localSpeedY < 0f)
            localSpeedY = 0f;
    }

    private bool TryWalkAroundLostGround()
    {
        Vector2 tangent = GetTangent();

        // Tương đương probe chéo của ROTA.
        //
        // Ví dụ đang đứng trên floor:
        //
        //     groundNegative = down + left
        //     groundPositive = down + right

        bool groundNegative = ProbeCurrentSurface(
            GravityDirection - tangent);

        bool groundPositive = ProbeCurrentSurface(
            GravityDirection + tangent);

        float moveSign;

        // Platform cũ còn ở phía sau bên trái
        // => player vừa bước sang phải.
        if (groundNegative && !groundPositive)
        {
            moveSign = 1f;
        }
        // Platform cũ còn ở phía sau bên phải
        // => player vừa bước sang trái.
        else if (groundPositive && !groundNegative)
        {
            moveSign = -1f;
        }
        else
        {
            // Trường hợp probe không rõ ràng:
            // dùng hướng di chuyển gần nhất.
            moveSign = lastMoveSign;
        }

        WalkAroundCorner(moveSign);
        return true;
    }

    private void WalkAroundCorner(float moveSign)
    {
        SurfaceSide oldSurface = currentSurface;
        Vector2 oldPosition = rb.position;

        Vector2 oldGravity = GravityDirection;
        Vector2 oldTangent = GetTangent();

        Vector2 moveDirection = oldTangent * moveSign;

        // Di chuyển nhẹ ra quanh góc.
        rb.position += oldGravity.normalized * edgeCornerNudge;
        rb.position += moveDirection.normalized * edgeCornerNudge;

        // Surface 90 độ tiếp theo.
        SurfaceSide nextSurface = ClosestSurface(moveDirection);

        SetSurface(nextSurface, false);

        localSpeedX = moveSpeed * 0.72f * moveSign;
        localSpeedY = -surfaceStickSpeed;

        // Quan trọng:
        // chỉ commit nếu thật sự tìm thấy surface mới.
        if (!SnapToSurface())
        {
            rb.position = oldPosition;

            SetSurface(oldSurface, false);

            localSpeedY = 0f;

            return;
        }

        ApplySurfaceVelocity();

        // Chỉ cooldown khi attach THÀNH CÔNG.
        edgeAttachCooldownTimer = edgeAttachCooldown;
    }

    private bool SnapToSurface()
    {
        RefreshGroundLayerMask();

        int hitCount = bodyCollider.Cast(
            GravityDirection,
            groundFilter,
            castHits,
            edgeAttachDistance);

        RaycastHit2D bestHit = default;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            if (!IsValidGroundHit(castHits[i]))
                continue;

            if (castHits[i].distance < bestDistance)
            {
                bestDistance = castHits[i].distance;
                bestHit = castHits[i];
            }
        }

        if (bestHit.collider == null)
        {
            isGrounded = false;
            hasGroundContact = false;
            return false;
        }

        if (bestDistance > groundCheckDistance)
            rb.position += GravityDirection * (bestDistance - groundCheckDistance);

        isGrounded = true;
        hasGroundContact = true;
        localSpeedY = -surfaceStickSpeed;
        return true;
    }

    private void SetSurface(SurfaceSide surface, bool keepMoveDirection)
    {
        bool surfaceChanged = currentSurface != surface;

        Vector2 previousVelocity = rb != null ? rb.linearVelocity : Vector2.zero;
        if (previousVelocity.sqrMagnitude < 0.0001f)
            previousVelocity = GetWorldVelocity();

        currentSurface = surface;
        surfaceNormal = GetNormal(surface);
        targetSurfaceAngle = GetSurfaceAngle(surfaceNormal);

        if (keepMoveDirection)
            localSpeedX = Vector2.Dot(previousVelocity, GetTangent());

        if (surfaceChanged)
            currentAnimationState = AnimationSpine.State.None;
    }

    private Vector2 GetWorldVelocity()
    {
        return (GetTangent() * localSpeedX) - (GravityDirection * localSpeedY);
    }

    private void ApplySurfaceVelocity()
    {
        rb.linearVelocity = GetWorldVelocity();
    }

    private Vector2 GetTangent()
    {
        return new Vector2(surfaceNormal.y, -surfaceNormal.x);
    }

    public static Vector2 GetNormal(SurfaceSide surface)
    {
        switch (surface)
        {
            case SurfaceSide.LeftWall:
                return Vector2.left;
            case SurfaceSide.Ceiling:
                return Vector2.down;
            case SurfaceSide.RightWall:
                return Vector2.right;
            default:
                return Vector2.up;
        }
    }

    private static SurfaceSide ClosestSurface(Vector2 normal)
    {
        if (normal.sqrMagnitude < 0.000001f)
            return SurfaceSide.Floor;

        normal.Normalize();

        float up = Vector2.Dot(normal, Vector2.up);
        float right = Vector2.Dot(normal, Vector2.right);
        float down = Vector2.Dot(normal, Vector2.down);
        float left = Vector2.Dot(normal, Vector2.left);

        float best = up;
        SurfaceSide side = SurfaceSide.Floor;

        if (left > best)
        {
            best = left;
            side = SurfaceSide.LeftWall;
        }

        if (down > best)
        {
            best = down;
            side = SurfaceSide.Ceiling;
        }

        if (right > best)
            side = SurfaceSide.RightWall;

        return side;
    }

    private bool HasSolidHit(int hitCount)
    {
        for (int i = 0; i < hitCount; i++)
        {
            if (IsValidGroundHit(castHits[i]))
                return true;
        }

        return false;
    }

    private bool HasSolidContact(int contactCount)
    {
        for (int i = 0; i < contactCount; i++)
        {
            if (IsValidGroundContact(contactPoints[i]))
                return true;
        }

        return false;
    }

    private bool IsValidGroundHit(RaycastHit2D hit)
    {
        return IsValidGroundCollider(hit.collider)
            && hit.distance >= 0f
            && Vector2.Dot(hit.normal, surfaceNormal) >= groundNormalMinDot;
    }

    private bool IsValidGroundContact(ContactPoint2D contact)
    {
        Collider2D hitCollider = contact.collider == bodyCollider
            ? contact.otherCollider
            : contact.collider;

        return IsValidGroundCollider(hitCollider)
            && Vector2.Dot(contact.normal, surfaceNormal) >= groundNormalMinDot;
    }

    private bool IsValidGroundCollider(Collider2D hitCollider)
    {
        if (hitCollider == null
            || hitCollider == bodyCollider
            || hitCollider.transform.root == bodyCollider.transform.root)
        {
            return false;
        }

        int hitLayerMask = 1 << hitCollider.gameObject.layer;
        return (groundLayer.value & hitLayerMask) != 0;
    }

    private void RefreshGroundLayerMask()
    {
        if (useProjectEnvironmentLayers)
            groundLayer = PlayerLayerConfig.EnvironmentLayer;

        groundFilter.SetLayerMask(groundLayer);
    }

    private void ApplyRotaCollisionFilter()
    {
        if (bodyCollider == null || !ignoreNonSurfacePhysicsLayers)
            return;

        int nonSurfaceLayers = ~groundLayer.value;
        bodyCollider.excludeLayers = originalBodyExcludeLayers.value | nonSurfaceLayers;
    }

    private void RestoreBodyCollisionFilter()
    {
        if (bodyCollider != null)
            bodyCollider.excludeLayers = originalBodyExcludeLayers;
    }

    private void RotateToSurface()
    {
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetSurfaceAngle);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            visualRotateSpeed * Time.fixedDeltaTime);
    }

    private void UpdateSpineFacing()
    {
        if (!flipSpineOnMove || animationSpine == null || Mathf.Abs(localSpeedX) <= flipMoveThreshold)
            return;

        bool flipX = localSpeedX < 0f ? rightFacingUsesFlipX : !rightFacingUsesFlipX;
        if (invertFacingDirection)
            flipX = !flipX;

        animationSpine.SetFlipX(flipX);
    }

    private void UpdateSpineAnimation()
    {
        if (!updateSpineAnimation || animationSpine == null)
            return;

        AnimationSpine.State nextState;
        bool loop;

        if (isGrounded)
        {
            nextState = Mathf.Abs(localSpeedX) > walkAnimationThreshold ? walkState : idleState;
            loop = true;
        }
        else if (localSpeedY > 0f)
        {
            nextState = jumpState;
            loop = false;
        }
        else
        {
            nextState = fallState;
            loop = true;
        }

        if (nextState == AnimationSpine.State.None || !animationSpine.HasStateAnimation(nextState))
            return;

        if (nextState == currentAnimationState && animationSpine.IsPlaying(nextState))
            return;

        animationSpine.Play(nextState, loop);
        currentAnimationState = nextState;
    }

    private void RotateCameraToPlayerDirection()
    {
        if (cinemachineCamera == null)
            return;

        Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetSurfaceAngle);
        cinemachineCamera.transform.rotation = Quaternion.RotateTowards(
            cinemachineCamera.transform.rotation,
            targetRotation,
            cameraRotateSpeed * Time.deltaTime);
    }

    public static float GetSurfaceAngle(Vector2 normal)
    {
        return Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg - 90f;
    }

    #region Audio System

    public void PlayJumpAudio()
    {
        if (!enableAudio) return;

        if (jumpSound != null)
        {
            PlayAudioClip(jumpSound);
        }
        else if (playerAudio != null)
        {
            playerAudio.PlayJump();
        }
    }

    public void PlayLandAudio()
    {
        if (!enableAudio) return;

        if (landSound != null)
        {
            PlayAudioClip(landSound);
        }
        else if (walkSound != null)
        {
            PlayAudioClip(walkSound);
        }
    }

    private void TickMovementAudio()
    {
        if (!enableAudio) return;

        bool isMoving = isGrounded && (Mathf.Abs(inputX) > 0.05f || Mathf.Abs(localSpeedX) > walkAnimationThreshold);
        if (!isMoving) return;

        if (Time.time >= nextFootstepTime)
        {
            if (walkSound != null)
            {
                PlayAudioClip(walkSound);
            }
            else if (playerAudio != null)
            {
                playerAudio.PlayOneShot(walkSound);
            }

            nextFootstepTime = Time.time + Mathf.Max(0.1f, walkStepInterval);
        }
    }

    private void PlayAudioClip(AudioClip clip)
    {
        if (clip == null) return;

        if (AudioManager.Instance != null)
        {
            AudioManager.PlaySFXIfAvailable(clip, transform.position);
            return;
        }

        AudioSource.PlayClipAtPoint(clip, transform.position, audioVolume);
    }

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        Vector2 normal = Application.isPlaying && surfaceNormal.sqrMagnitude > 0.1f
            ? surfaceNormal
            : GetNormal(startSurface);

        Vector2 tangent = new Vector2(normal.y, -normal.x);
        Vector2 origin = Application.isPlaying && rb != null
            ? rb.position
            : (Vector2)transform.position;

        // Green: gravity / surface attach direction.
        Gizmos.color = Color.green;
        Gizmos.DrawLine(origin, origin - normal * edgeAttachDistance);

        // Cyan: tangent directions along the current surface.
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, origin + tangent * edgeProbeDistance);
        Gizmos.DrawLine(origin, origin - tangent * edgeProbeDistance);

        // Yellow: ground check length.
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin - normal * groundCheckDistance);
    }
#endif
}
