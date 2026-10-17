using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public sealed class PlayerSwimController : MonoBehaviour
{
    private const int AcidDamagePerTick = 70;
    private const float AcidDamageTickInterval = 1f;

    [Header("Checks")]
    [SerializeField] private Transform headWaterCheck;
    [SerializeField] private Transform bodyWaterCheck;
    [SerializeField] private Transform feetWaterCheck;

    [Header("Swim Flip")]
    [SerializeField] private Transform flipPivot;

    [Header("Swim Rotation")]
    [SerializeField, Min(0f)] private float swimDownRotationAngle = 45f;
    [SerializeField, Min(0f)] private float swimUpRotationAngle = 45f;

    [Header("Swim Speed")]
    [SerializeField, Min(0f)] private float swimUpSpeed = 0f;

    [Header("Water Detection")]
    private PlayerWaterDetectionSettings waterSettings = PlayerWaterDetectionSettings.Default;
    [SerializeField] private bool logMovementStateChanges;

    private Rigidbody2D _rb;
    private Collider2D _bodyCollider;
    private CapsuleCollider2D _capsuleCollider;
    private CapsuleDirection2D _defaultCapsuleDirection;
    private bool _hasCapsuleCollider;
    private PlayerHealth _health;
    private PlayerWaterDetector _waterDetector;
    private PlayerMovementStateMachine _stateMachine;
    private PlayerWaterSnapshot _latestWaterSnapshot;
    private bool _hasWaterSnapshot;
    private int _acidZoneCount;
    private float _acidDamageTimer;
    private Quaternion _swimBaseRotation;
    private bool _hasSwimBaseRotation;
    private float _defaultGravityScale;
    private bool _hasDefaultGravityScale;

    public LayerMask GroundLayer => PlayerLayerConfig.GroundLayer;
    public PlayerMovementState State => _stateMachine != null ? _stateMachine.State : PlayerMovementState.Airborne;
    public bool IsSwimming => _stateMachine != null && _stateMachine.IsSwimming;
    public bool IsExitingWater => _stateMachine != null && _stateMachine.IsExitingWater;
    public bool IsInWaterMovementState => IsSwimming || IsExitingWater;
    public int WaterZoneCount => _waterDetector != null ? _waterDetector.WaterZoneCount : 0;
    public bool IsInAcid => _acidZoneCount > 0;
    public float SwimUpSpeed => swimUpSpeed;
    public Transform FlipPivot => flipPivot;

    public bool TryGetFlipPivotPosition(out Vector3 pivotPosition)
    {
        if (WaterZoneCount > 0 && flipPivot != null)
        {
            pivotPosition = flipPivot.position;
            return true;
        }

        pivotPosition = default;
        return false;
    }

    private void Awake()
    {
        Initialize();
    }

    private void OnDisable()
    {
        ClearWaterZones();
        ClearAcidZones();
        ApplySwimColliderDirection(false);
        ApplySwimGravity(false);
        RestoreSwimRotation();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Initialize();

        bool isWaterCollider = _waterDetector.IsWaterCollider(other);
        bool isAcidCollider = IsAcidCollider(other);

        if (isWaterCollider || isAcidCollider)
        {
            _waterDetector.AddWaterZone(other);
        }

        if (isAcidCollider)
        {
            _acidZoneCount++;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Initialize();

        bool isWaterCollider = _waterDetector.IsWaterCollider(other);
        bool isAcidCollider = IsAcidCollider(other);

        if (isWaterCollider || isAcidCollider)
        {
            _waterDetector.RemoveWaterZone(other);
        }

        if (isAcidCollider)
        {
            _acidZoneCount = Mathf.Max(0, _acidZoneCount - 1);
            if (_acidZoneCount == 0)
            {
                _acidDamageTimer = 0f;
            }
        }
    }

    private void Update()
    {
        TickAcidDamage(Time.deltaTime);
    }

    public PlayerMovementState FixedTick(Vector2 moveInput, float facingSign)
    {
        Initialize();

        PlayerWaterSnapshot waterSnapshot = _waterDetector.Sample(facingSign);
        _latestWaterSnapshot = waterSnapshot;
        _hasWaterSnapshot = true;

        PlayerMovementState previousState = _stateMachine.State;
        PlayerMovementState currentState = _stateMachine.Tick(waterSnapshot, moveInput, facingSign);
        ApplySwimColliderDirection(currentState == PlayerMovementState.Swimming);
        ApplySwimGravity(currentState == PlayerMovementState.Swimming);
        UpdateSwimRotation(currentState, moveInput, facingSign);
        LogMovementStateChange(previousState, currentState, waterSnapshot);
        return currentState;
    }

    public Vector2 FilterAcidSwimInput(Vector2 moveInput)
    {
        return moveInput;
    }

    public void ClearWaterZones()
    {
        _waterDetector?.ClearWaterZones();
        _hasWaterSnapshot = false;
    }

    private void Initialize()
    {
        if (_waterDetector != null && _stateMachine != null)
        {
            return;
        }

        _rb = GetComponent<Rigidbody2D>();
        _defaultGravityScale = _rb.gravityScale;
        _hasDefaultGravityScale = true;
        _bodyCollider = GetComponent<Collider2D>();
        _capsuleCollider = _bodyCollider as CapsuleCollider2D;
        _hasCapsuleCollider = _capsuleCollider != null;
        if (_hasCapsuleCollider)
        {
            _defaultCapsuleDirection = _capsuleCollider.direction;
        }

        _health = GetComponent<PlayerHealth>();
        _stateMachine = new PlayerMovementStateMachine();
        _waterDetector = new PlayerWaterDetector(
            transform,
            _bodyCollider,
            headWaterCheck,
            bodyWaterCheck,
            feetWaterCheck,
            PlayerLayerConfig.WaterLayer,
            PlayerLayerConfig.GroundLayer,
            waterSettings);
    }

    private void ApplySwimColliderDirection(bool swimming)
    {
        if (!_hasCapsuleCollider)
        {
            return;
        }

        CapsuleDirection2D targetDirection = swimming
            ? CapsuleDirection2D.Horizontal
            : _defaultCapsuleDirection;

        if (_capsuleCollider.direction != targetDirection)
        {
            _capsuleCollider.direction = targetDirection;
        }
    }

    private void ApplySwimGravity(bool swimming)
    {
        if (_rb == null || !_hasDefaultGravityScale)
        {
            return;
        }

        _rb.gravityScale = swimming ? 0f : _defaultGravityScale;
    }

    private void UpdateSwimRotation(
        PlayerMovementState movementState,
        Vector2 moveInput,
        float facingSign)
    {
        if (movementState != PlayerMovementState.Swimming)
        {
            RestoreSwimRotation();
            return;
        }

        if (!_hasSwimBaseRotation)
        {
            _swimBaseRotation = transform.rotation;
            _hasSwimBaseRotation = true;
        }

        if (moveInput.y > 0.01f)
        {
            SetSwimRotation(_swimBaseRotation * Quaternion.Euler(
                0f,
                0f,
                swimUpRotationAngle * Mathf.Sign(facingSign)));
            return;
        }

        if (moveInput.y < -0.01f)
        {
            SetSwimRotation(_swimBaseRotation * Quaternion.Euler(
                0f,
                0f,
                -swimDownRotationAngle * Mathf.Sign(facingSign)));
            return;
        }

        SetSwimRotation(_swimBaseRotation);
    }

    private void RestoreSwimRotation()
    {
        if (!_hasSwimBaseRotation)
        {
            return;
        }

        SetSwimRotation(_swimBaseRotation);
        _hasSwimBaseRotation = false;
    }

    private void SetSwimRotation(Quaternion targetRotation)
    {
        Vector3 pivotPosition = flipPivot != null ? flipPivot.position : transform.position;
        transform.rotation = targetRotation;

        if (flipPivot != null)
        {
            transform.position += pivotPosition - flipPivot.position;
        }
    }

    private void TickAcidDamage(float deltaTime)
    {
        if (_acidZoneCount <= 0 || _health == null)
        {
            return;
        }

        _acidDamageTimer += deltaTime;
        while (_acidDamageTimer >= AcidDamageTickInterval)
        {
            _acidDamageTimer -= AcidDamageTickInterval;
            _health.ApplyEnvironmentalDamageWithoutReaction(AcidDamagePerTick);
        }
    }

    private void ClearAcidZones()
    {
        _acidZoneCount = 0;
        _acidDamageTimer = 0f;
    }

    private static bool IsAcidCollider(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        int acidLayerMask = PlayerLayerConfig.AcidLayer.value;
        return (acidLayerMask & (1 << other.gameObject.layer)) != 0;
    }

    private void LogMovementStateChange(
        PlayerMovementState previousState,
        PlayerMovementState currentState,
        PlayerWaterSnapshot snapshot)
    {
        if (!logMovementStateChanges || previousState == currentState)
        {
            return;
        }

        Debug.Log(
            $"[PlayerSwimController] Movement state {previousState} -> {currentState}. " +
            $"waterZones={WaterZoneCount}, grounded={snapshot.Grounded}, " +
            $"head={snapshot.HeadInWater}, body={snapshot.BodyInWater}, feet={snapshot.FeetInWater}, " +
            $"velocity={_rb.linearVelocity}",
            this);
    }

    private void OnDrawGizmosSelected()
    {
        PlayerWaterDetector.DrawGizmos(
            transform,
            Application.isPlaying ? _bodyCollider : GetComponent<Collider2D>(),
            headWaterCheck,
            bodyWaterCheck,
            feetWaterCheck,
            waterSettings,
            _latestWaterSnapshot,
            _hasWaterSnapshot);
    }
}
