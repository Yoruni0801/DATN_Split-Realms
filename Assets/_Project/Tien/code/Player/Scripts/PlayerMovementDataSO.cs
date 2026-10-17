using UnityEngine;

[System.Serializable]
public struct PlayerRotaMovementSettings
{
    public float moveSpeed;
    public float acceleration;
    public float deceleration;
    public float turnAcceleration;
    public float airAcceleration;
    public float jumpSpeed;
    [Range(0.1f, 1f)] public float jumpReleaseSpeedMultiplier;
    public float gravity;
    public float fallGravityMultiplier;
    public float maxFallSpeed;
    public float surfaceStickSpeed;
    public float groundCheckDistance;
    [Range(0f, 1f)] public float groundNormalMinDot;
    public float edgeCornerNudge;
    public float edgeAttachDistance;
    public float edgeAttachCooldown;
    public bool autoAttachAtEdges;

    public static PlayerRotaMovementSettings Default => new PlayerRotaMovementSettings
    {
        moveSpeed = 15f,
        acceleration = 100f,
        deceleration = 140f,
        turnAcceleration = 180f,
        airAcceleration = 45f,
        jumpSpeed = 18f,
        jumpReleaseSpeedMultiplier = 0.45f,
        gravity = 28f,
        fallGravityMultiplier = 3f,
        maxFallSpeed = 100f,
        surfaceStickSpeed = 2f,
        groundCheckDistance = 0.08f,
        groundNormalMinDot = 0.65f,
        edgeCornerNudge = 0.14f,
        edgeAttachDistance = 0.9f,
        edgeAttachCooldown = 0.12f,
        autoAttachAtEdges = true
    };
}

[CreateAssetMenu(fileName = "PlayerMovementData", menuName = "Duy/Game Data/Player Movement Data")]
public class PlayerMovementDataSO : ScriptableObject
{
    [Header("Walk")]
    public float walkSpeed = 5f;
    public float acceleration = 55f;
    public float deceleration = 70f;
    public float groundFriction = 12f;
    public float airDrag = 3f;
    public float airControlMultiplier = 0.4f;

    [Header("Jump")]
    public float jumpForce = 11f;
    public int maxJumps = 2;
    public float jumpCutMultiplier = 0.45f;
    public float fallGravityMultiplier = 2.2f;

    [Header("Slope")]
    public float maxSlopeAngle = 45f;
    public float slopeSpeedMultiplier = 0.7f;
    public float slopeDownhillMultiplier = 1.2f;

    [Header("Landing")]
    public float landingGravityBoost = 1.5f;
    public float landingBoostDuration = 0.08f;
    public float landingImpactThreshold = 8f;

    [Header("Swim")]
    public float swimSpeed = 6f;
    public float swimAcceleration = 18f;
    public float swimDeceleration = 24f;
    public float swimGravityScale = 0f;
    public float maxWaterEntryFallSpeed = 4f;

    [Header("Acid")]
    public float acidFloatUpSpeed = 2.5f;

    [Header("Dash")]
    public float dashSpeed = 20f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    public float ghostDelay = 0.03f;
    public GameObject ghostPrefab;
    public GameObject swimDashVfxPrefab;
    public Vector3 swimDashVfxOffset = Vector3.zero;

    [Header("Ground Detection")]
    public float groundCheckRadius = 0.2f;

    [Header("Gravity")]
    public float gravityColliderDisableDuration = 0.4f;

    [Header("ROTA")]
    [SerializeField] private PlayerRotaMovementSettings rota = PlayerRotaMovementSettings.Default;

    public PlayerRotaMovementSettings Rota => rota;
}
