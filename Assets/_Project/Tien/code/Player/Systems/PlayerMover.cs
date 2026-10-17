using UnityEngine;

public sealed class PlayerMover
{
    private readonly Rigidbody2D _rb;
    private readonly Transform _body;
    private readonly Collider2D _bodyCollider;
    private readonly PlayerMovementDataSO _data;
    private readonly GroundCheckConfig _groundCheckConfig;
    private float _baseJumpForce;

    private float _defaultGravity;
    private float _dashTimeLeft;
    private float _dashCooldownTimer;
    private float _ghostTimer;
    private float _externalLaunchX;
    private float _externalLaunchTimer;
    private float _attackBrakeTimer;
    private float _attackBrakeStrength;
    private float _attackLungeTimer;
    private float _attackLungeSpeed;
    private float _attackLungeDirection = 1f;
    private Vector2 _dashDirection;
    private int _jumpCount;
    private bool _swimming;
    private bool _jumpWasHeld;

    private float _coyoteTimer;
    private const float CoyoteTimeDuration = 0.15f;
    private const float GroundStopMultiplier = 3f;
    private const float GroundReverseMultiplier = 2.5f;
    private const float GroundStopSnapSpeed = 0.15f;

    // ─── State ───────────────────────────────────────────────────────────────
    public bool IsGrounded { get; private set; }
    public bool IsDashing  { get; private set; }
    public bool CanDash    => _dashCooldownTimer <= 0f && !IsDashing;
    public Vector2 DashDirection => _dashDirection;
    public bool IsAttackLungeActive => _attackLungeTimer > 0f;

    public float JumpForce { get; private set; }   // vẫn GIỮ NGUYÊN
    public AnimationSpine.State JumpAnimation { get; private set; }
    public AnimationSpine.State FallAnimation { get; private set; }
    public bool IsGravityInverted => _defaultGravity < 0f;

    // ─── Constructor ─────────────────────────────────────────────────────────
    public PlayerMover(
        Rigidbody2D rb, Transform body, Collider2D bodyCollider,
        LayerMask groundLayer, PlayerMovementDataSO data, float effectiveJumpForce)
    {
        _rb          = rb;
        _body        = body;
        _bodyCollider = bodyCollider;
        _data        = data;
        _baseJumpForce = Mathf.Max(0f, effectiveJumpForce);
        _groundCheckConfig = GroundCheck.Override(
            groundLayer,
            data != null ? data.groundCheckRadius : -1f,
            data != null ? data.groundCheckRadius : -1f,
            checkMode: GroundCheckMode.CircleCast);

        _defaultGravity = rb.gravityScale;

        JumpForce      = _baseJumpForce;

        JumpAnimation  = AnimationSpine.State.Jump;
        FallAnimation  = AnimationSpine.State.Fall;
    }

    // ─── Tick ────────────────────────────────────────────────────────────────
    public void Tick(float dt)
    {
        if (_dashCooldownTimer > 0f)
            _dashCooldownTimer -= dt;

        TickDash(dt);
        UpdateGroundState(dt);
    }

    public void FixedTick(
        Vector2 input,
        bool locked, bool hurt, bool blocking, bool hovering,
        PlayerMovementState movementState,
        bool jumpHeld,
        float speedMultiplier = 1f,
        bool forceFloatUp = false,
        float swimUpSpeedOverride = 0f)
    {
        if (movementState == PlayerMovementState.ExitingWater)
        {
            _swimming = false;
            RestoreGravity();
        }

        bool swimming = movementState == PlayerMovementState.Swimming;
        if (swimming)
        {
            _swimming = true;
        }
        else if (_swimming && !IsDashing)
        {
            _swimming = false;
            RestoreGravity();
        }

        if (IsDashing)
        {
            ClearAttackMovement();
            _rb.linearVelocity = _dashDirection * _data.dashSpeed;
            _jumpWasHeld = jumpHeld;
            return;
        }

        if (swimming)
        {
            ClearAttackMovement();
            FixedSwim(input, locked || hurt || blocking || hovering, speedMultiplier, forceFloatUp, swimUpSpeedOverride);
            _jumpWasHeld = jumpHeld;
            return;
        }

        if (hovering)
        {
            ClearAttackMovement();
            _rb.linearVelocity = Vector2.zero;
            _externalLaunchTimer = 0f;
            _jumpWasHeld = jumpHeld;
            return;
        }

        ApplyGravityForAirborneState();

        if (blocking || hurt || locked)
        {
            ClearAttackMovement();
            float dec = hurt ? _data.deceleration * 0.25f : _data.deceleration;

            _rb.linearVelocity = new Vector2(
                MovementMath.MoveTowardsStop(
                    _rb.linearVelocity.x,
                    dec,
                    Time.fixedDeltaTime),
                _rb.linearVelocity.y);

            _jumpWasHeld = jumpHeld;
            return;
        }

        if (_externalLaunchTimer > 0f && !IsGrounded)
        {
            _externalLaunchTimer -= Time.fixedDeltaTime;
            _rb.linearVelocity = new Vector2(_externalLaunchX, _rb.linearVelocity.y);
            _jumpWasHeld = jumpHeld;
            ApplyGravityForAirborneState();
            return;
        }

        ApplyVariableJump(jumpHeld);
        float horizontalVelocity;
        if (_attackLungeTimer > 0f)
        {
            horizontalVelocity = ApplyAttackMovement(0f);
        }
        else
        {
            horizontalVelocity = MoveHorizontalVelocity(input.x, speedMultiplier);
            horizontalVelocity = ApplyAttackMovement(horizontalVelocity);
        }
        _rb.linearVelocity = new Vector2(horizontalVelocity, _rb.linearVelocity.y);
    }

    // ─── JUMP ────────────────────────────────────────────────────────────────
    public bool TryJump(bool blocking, bool hurt, bool locked)
    {
        if (blocking || hurt || IsDashing || locked)
            return false;

        bool canCoyoteJump = !IsGrounded && _jumpCount == 0 && _coyoteTimer > 0f;
        if (!IsGrounded && !canCoyoteJump && _jumpCount >= _data.maxJumps)
            return false;

        _rb.linearVelocity = new Vector2(
            _rb.linearVelocity.x,
            JumpForce);

        _jumpCount = (IsGrounded || canCoyoteJump) ? 1 : _jumpCount + 1;
        IsGrounded = false;
        _coyoteTimer = 0f;
        _jumpWasHeld = true;

        return true;
    }

    public void SetJumpForce(float jumpForce)
    {
        JumpForce = Mathf.Max(0f, jumpForce);
    }

    public void ResetJumpForce(float baseJumpForce)
    {
        _baseJumpForce = Mathf.Max(0f, baseJumpForce);
        JumpForce = _baseJumpForce;
    }

    // ─── DASH ────────────────────────────────────────────────────────────────
    public bool TryDash(
        bool locked, bool blocking, bool hurt,
        Vector2 moveInput = default, bool isSwimming = false)
    {
        if (!CanDash || locked || blocking || hurt)
            return false;

        IsDashing          = true;
        _dashTimeLeft      = _data.dashDuration;
        _dashCooldownTimer = _data.dashCooldown;
        _ghostTimer        = 0f;

        _rb.gravityScale   = 0f;

        if (isSwimming)
        {
            if (moveInput.sqrMagnitude > 0.01f)
            {
                _dashDirection = moveInput.normalized;
            }
            else
            {
                _dashDirection = new Vector2(GetFacingDirectionSign(), 0f);
            }
        }
        else
        {
            _dashDirection = new Vector2(GetFacingDirectionSign(), 0f);
        }

        return true;
    }

    public bool TickGhost(float dt)
    {
        _ghostTimer -= dt;

        if (_ghostTimer > 0f)
            return false;

        _ghostTimer = _data.ghostDelay;
        return true;
    }

    // ─── GRAVITY ─────────────────────────────────────────────────────────────
    public void SetGravityInverted(bool inverted)
    {
        float g = inverted
            ? -Mathf.Abs(_defaultGravity)
            : Mathf.Abs(_defaultGravity);

        _defaultGravity  = g;
        ApplyGravityForAirborneState();

        JumpAnimation = inverted ? AnimationSpine.State.Fall : AnimationSpine.State.Jump;
        FallAnimation = inverted ? AnimationSpine.State.Jump : AnimationSpine.State.Fall;
    }

    public void SetGravityScale(float gravityScale)
    {
        _defaultGravity = gravityScale;
        ApplyGravityForAirborneState();
    }

    // ─── SWIM ────────────────────────────────────────────────────────────────
    public void SetSwimming(bool swimming)
    {
        _swimming = swimming;

        if (swimming)
        {
            _rb.gravityScale = 0f;
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0f);

            return;
        }

        if (!IsDashing)
        {
            _swimming = false;
            RestoreGravity();
        }
    }

    // ─── UTILITY ─────────────────────────────────────────────────────────────
    public void RestoreGravity()
        => _rb.gravityScale = _defaultGravity;

    public void FreezeAll()
        => _rb.constraints = RigidbodyConstraints2D.FreezeAll;

    public void FreezeRotation()
        => _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

    public void ZeroVelocity()
        => _rb.linearVelocity = Vector2.zero;

    public void ApplyExternalLaunch(Vector2 velocity, float horizontalHoldTime)
    {
        _rb.linearVelocity = velocity;
        _externalLaunchX = velocity.x;
        _externalLaunchTimer = Mathf.Max(0f, horizontalHoldTime);
        IsGrounded = false;
        _coyoteTimer = 0f;
    }

    public void ApplyAttackBrake(float duration, float strength)
    {
        if (duration <= 0f || IsDashing || _swimming)
        {
            return;
        }

        _attackBrakeTimer = Mathf.Max(_attackBrakeTimer, duration);
        _attackBrakeStrength = Mathf.Clamp01(strength);
        _attackLungeTimer = 0f;
    }

    public void ApplyAttackLunge(float speed, float duration, float directionSign)
    {
        if (speed <= 0f || duration <= 0f || IsDashing || _swimming || _attackLungeTimer > 0f)
        {
            return;
        }

        _attackLungeSpeed = Mathf.Abs(speed);
        _attackLungeTimer = duration;
        _attackLungeDirection = directionSign >= 0f ? 1f : -1f;
        _attackBrakeTimer = 0f;
    }

    // ─── GROUND CHECK ────────────────────────────────────────────────────────
    private float GetFacingDirectionSign()
        => _body.localScale.x < 0f ? 1f : -1f;

    private float MoveHorizontalVelocity(float inputX, float speedMultiplier)
    {
        float currentVelocity = _rb.linearVelocity.x;
        float targetVelocity = inputX * _data.walkSpeed * speedMultiplier;
        bool hasInput = Mathf.Abs(inputX) > 0.01f;
        float deltaTime = Time.fixedDeltaTime;

        if (!hasInput)
        {
            float nextVelocity = Mathf.MoveTowards(
                currentVelocity,
                0f,
                GetStopDeceleration() * deltaTime);

            return Mathf.Abs(nextVelocity) <= GroundStopSnapSpeed ? 0f : nextVelocity;
        }

        bool reversing = Mathf.Abs(currentVelocity) > 0.01f
            && Mathf.Sign(currentVelocity) != Mathf.Sign(targetVelocity);
        float acceleration = reversing
            ? GetReverseAcceleration()
            : _data.acceleration;

        return Mathf.MoveTowards(
            currentVelocity,
            targetVelocity,
            acceleration * deltaTime);
    }

    private float GetStopDeceleration()
        => IsGrounded ? _data.deceleration * GroundStopMultiplier : _data.deceleration;

    private float GetReverseAcceleration()
        => IsGrounded ? _data.acceleration * GroundReverseMultiplier : _data.acceleration;

    private float ApplyAttackMovement(float horizontalVelocity)
    {
        float deltaTime = Time.fixedDeltaTime;

        if (_attackLungeTimer > 0f)
        {
            _attackLungeTimer -= deltaTime;
            return _attackLungeDirection * _attackLungeSpeed;
        }

        if (_attackBrakeTimer <= 0f)
        {
            return horizontalVelocity;
        }

        _attackBrakeTimer -= deltaTime;
        float brakeDeceleration = GetStopDeceleration() * Mathf.Lerp(1f, 4f, _attackBrakeStrength);
        return MovementMath.MoveTowardsStop(
            _rb.linearVelocity.x,
            brakeDeceleration,
            deltaTime);
    }

    private void ClearAttackMovement()
    {
        _attackBrakeTimer = 0f;
        _attackLungeTimer = 0f;
    }

    private void ApplyGravityForAirborneState()
    {
        if (_swimming)
        {
            return;
        }

        if (IsGrounded)
        {
            _rb.gravityScale = _defaultGravity;
            return;
        }

        float baseGravity = Mathf.Abs(_defaultGravity);
        float gravitySign = Mathf.Sign(_defaultGravity);
        bool fallingWithGravity = gravitySign >= 0f
            ? _rb.linearVelocity.y < 0f
            : _rb.linearVelocity.y > 0f;
        float multiplier = fallingWithGravity
            ? Mathf.Max(1f, _data.fallGravityMultiplier)
            : 1f;

        _rb.gravityScale = gravitySign * baseGravity * multiplier;
    }

    private void ApplyVariableJump(bool jumpHeld)
    {
        if (IsGrounded || _rb.linearVelocity.y <= 0f)
        {
            _jumpWasHeld = jumpHeld;
            return;
        }

        bool jumpReleased = _jumpWasHeld && !jumpHeld;
        if (jumpReleased)
        {
            float cutMultiplier = Mathf.Clamp01(_data.jumpCutMultiplier);
            _rb.linearVelocity = new Vector2(
                _rb.linearVelocity.x,
                Mathf.Max(0f, _rb.linearVelocity.y * cutMultiplier));
        }

        _jumpWasHeld = jumpHeld;
    }

    private void UpdateGroundState(float dt)
    {
        IsGrounded = GroundCheck.IsGrounded(_body, _bodyCollider, _groundCheckConfig);

        if (IsGrounded && _rb.linearVelocity.y <= 0.1f)
            _jumpCount = 0;

        if (IsGrounded)
        {
            _coyoteTimer = CoyoteTimeDuration;
        }
        else
        {
            _coyoteTimer -= dt;
        }
    }

    private void TickDash(float dt)
    {
        if (!IsDashing)
            return;

        _dashTimeLeft -= dt;

        if (_dashTimeLeft <= 0f)
        {
            IsDashing = false;
            if (_swimming)
            {
                _rb.gravityScale = _data.swimGravityScale;
            }
            else
            {
                _rb.gravityScale = _defaultGravity;
            }
        }
    }

    private void FixedSwim(
        Vector2 input,
        bool locked,
        float speedMultiplier = 1f,
        bool forceFloatUp = false,
        float swimUpSpeedOverride = 0f)
    {
        if (!_swimming)
        {
            _swimming = true;

            if (_rb.linearVelocity.y < -_data.maxWaterEntryFallSpeed)
            {
                _rb.linearVelocity = new Vector2(
                    _rb.linearVelocity.x,
                    -_data.maxWaterEntryFallSpeed);
            }
        }

        _rb.gravityScale = _data.swimGravityScale;

        if (locked)
        {
            if (forceFloatUp)
            {
                float floatUpSpeed = Mathf.Max(0f, _data.acidFloatUpSpeed);
                _rb.linearVelocity = Vector2.MoveTowards(
                    _rb.linearVelocity,
                    new Vector2(0f, floatUpSpeed),
                    _data.swimDeceleration * Time.fixedDeltaTime);

                return;
            }

            _rb.linearVelocity = Vector2.MoveTowards(
                _rb.linearVelocity,
                Vector2.zero,
                _data.swimDeceleration * Time.fixedDeltaTime);

            return;
        }

        float swimSpeed = Mathf.Max(0f, _data.swimSpeed) * speedMultiplier;
        Vector2 swimInput = Vector2.ClampMagnitude(input, 1f);
        Vector2 desired = swimInput * swimSpeed;
        if (swimUpSpeedOverride > 0f && swimInput.y > 0.01f)
        {
            desired.y = swimInput.y * Mathf.Max(0f, swimUpSpeedOverride) * speedMultiplier;
        }
        if (forceFloatUp)
        {
            desired.y = Mathf.Max(desired.y, Mathf.Max(0f, _data.acidFloatUpSpeed));
        }

        float accel = swimInput.sqrMagnitude > 0.01f
            ? _data.swimAcceleration
            : _data.swimDeceleration;

        Vector2 nextVelocity = Vector2.MoveTowards(
            _rb.linearVelocity,
            desired,
            accel * Time.fixedDeltaTime);

        _rb.linearVelocity = nextVelocity;
    }
}
