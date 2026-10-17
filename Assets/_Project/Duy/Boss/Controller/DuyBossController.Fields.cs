using UnityEngine;

public partial class DuyBossController
{
    private const string AttackHitboxOnEvent = "AttackHitboxOn";
    private const string AttackHitboxOffEvent = "AttackHitboxOff";
    private const string CastReleaseEvent = "CastRelease";
    private const string SwordReleaseEvent = "SwordRelease";
    private const string SwordRecoverEvent = "SwordRecover";
    private const string JumpSlamImpactEvent = "JumpSlamImpact";
    private readonly RaycastHit2D[] groundProbeHits = new RaycastHit2D[6];
    private readonly Collider2D[] areaHitResults = new Collider2D[12];
    private readonly System.Collections.Generic.HashSet<int> jumpSlamHitTargetIds = new System.Collections.Generic.HashSet<int>();

    private enum BossState
    {
        Idle,
        Chase,
        Approach,
        Retreat,
        Dash,
        DashAttack,
        Attack,
        ComboAttack,
        AttackRecovery,
        PostAttackPause,
        Parry,
        ParryCounter,
        RangedPressure,
        SwordThrow,
        JumpSlam,
        Evade,
        JumpEvade,
        JumpAttack,
        Hurt,
        Stun,
        Dead
    }

    private enum DashMode
    {
        GapClose,
        Evade
    }

    private enum EvadeType
    {
        BackDash,
        ForwardDash,
        PassThroughDash
    }

    private enum BossSkillAction
    {
        None,
        Shockwave,
        SwordThrow,
        DashSlash,
        JumpSlash,
        JumpSlam
    }

    private enum CombatPhase
    {
        Ranged,
        Melee,
        Recovery,
        Chase
    }

    private enum JumpSlamPhase
    {
        None,
        Prepare,
        Ascend,
        Tracking,
        TargetLock,
        Dive,
        Impact,
        Recovery
    }

    public enum IncomingThreatType
    {
        Melee,
        ProjectileLow,
        ProjectileHigh,
        GroundAttack,
        HeavyAttack
    }

    private struct IncomingThreat
    {
        public Vector2 SourcePosition;
        public Vector2 Velocity;
        public IncomingThreatType ThreatType;
        public float EstimatedTimeToImpact;
        public float ReceivedTime;
    }

    [Header("Health")]
    [SerializeField, Min(1f)] private float maxHealth = 500f;
    [SerializeField, Min(0f)] private float hurtLockDuration = 0.22f;
    [SerializeField, Min(0f)] private float hurtAnimationCooldown = 0.55f;
    [SerializeField, Min(1)] private int poiseHitsBeforeInterrupt = 3;
    [SerializeField, Min(0f)] private float poiseResetDelay = 1.2f;
    [SerializeField, Min(0)] private int phaseTwoPoiseBonus = 1;
    [SerializeField, Min(0f)] private float deathDestroyDelay = 2f;

    [Header("Target")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField, Min(0.1f)] private float detectRadius = 14f;
    [SerializeField, Min(0.1f)] private float attackRange = 15f;

    [Header("Attack")]
    [SerializeField] private BossAttackHitbox weaponHitbox;
    [SerializeField, Min(1)] private int attackDamage = 20;
    [SerializeField, Min(0f)] private float attackCooldown = 0.85f;
    [SerializeField, Min(0f)] private float attackAnimationDuration = 0.75f;
    [SerializeField, Min(0f)] private float attackWindupDuration = 0.16f;
    [SerializeField, Min(0f)] private float attackHitboxActiveDuration = 0.16f;
    [SerializeField, Min(0f)] private float attackRecoveryDuration = 0.28f;
    [SerializeField, Min(0f)] private float postAttackPauseDuration = 0.1f;
    [SerializeField, Min(0f)] private float postAttackRetreatDelay = 0.15f;
    [SerializeField, Min(0f)] private float attackMoveSpeed = 1.8f;
    [SerializeField, Min(0f)] private float comboMoveSpeed = 2.2f;
    [SerializeField, Min(0f)] private float attackMoveMaxDistance = 1.5f;
#pragma warning disable CS0414
    [SerializeField, Min(0f)] private float attackLungeSpeed = 2.2f;
    [SerializeField, Min(0f)] private float attackLungeDuration = 0.08f;
    [SerializeField, Range(0f, 1f)] private float postAttackRetreatChance = 0.55f;
#pragma warning restore CS0414
    [SerializeField, Range(0f, 1f)] private float attackingDamageTakenMultiplier = 0.65f;
    [SerializeField] private bool attackCanBeBlocked = true;

    [Header("Slash VFX")]
    [SerializeField] private GameObject slashVfxPrefab;
    [SerializeField] private Transform slashVfxSpawnPoint;
    [SerializeField] private Vector2 slashVfxOffset = new Vector2(1f, 0.6f);
    [SerializeField] private Vector3 slashVfxRotationOffset;
    [SerializeField, Min(0f)] private float slashVfxLifetime = 2f;
    [SerializeField] private bool mirrorSlashVfxToFacing = true;

    [Header("Attack Final Pose Hold")]
    [SerializeField, Min(0f)] private float normalAttackFinalPoseHold = 0.24f;
    [SerializeField, Min(0f)] private float comboStepFinalPoseHold = 0.1f;
    [SerializeField, Min(0f)] private float comboFinisherFinalPoseHold = 0.34f;
    [SerializeField, Min(0f)] private float dashAttackFinalPoseHold = 0.28f;
    [SerializeField, Min(0f)] private float parryCounterFinalPoseHold = 0.26f;

    [Header("Combo")]
    [SerializeField, Range(1, 3)] private int maxComboHits = 3;
    [SerializeField, Min(0f)] private float comboStepDuration = 0.58f;
#pragma warning disable CS0414
    [SerializeField, Min(0f)] private float comboStepGap = 0.08f;
#pragma warning restore CS0414
    [SerializeField, Min(0f)] private float comboContinueRange = 3.6f;
    [SerializeField, Range(0f, 1f)] private float comboContinueChance = 0.65f;
    [SerializeField, Min(1f)] private float comboFinisherDamageMultiplier = 1.35f;
    [SerializeField, Min(0f)] private float comboFinisherRecoveryDuration = 0.45f;
    [SerializeField, Min(0f)] private float comboCooldown = 0.9f;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float chaseSpeed = 5.2f;
    [SerializeField, Min(0f)] private float approachSpeed = 3.4f;
    [SerializeField, Min(0f)] private float retreatSpeed = 4f;
    [SerializeField, Min(0f)] private float acceleration = 20f;

    [Header("Distance")]
    [SerializeField, Min(0f)] private float tooCloseDistance = 1.1f;
    [SerializeField, Min(0f)] private float preferredDistance = 3f;
    [SerializeField, Min(0f)] private float mediumDistance = 5f;
    [SerializeField, Min(0f)] private float farDistance = 8f;
    [SerializeField, Min(0f)] private float runStartDistance = 9f;
    [SerializeField, Min(0f)] private float runStopDistance = 6.5f;
    [SerializeField, Min(0f)] private float skillRangeMin = 3.2f;
    [SerializeField, Min(0f)] private float skillRangeMax = 8.5f;

    [Header("Combat Rotation")]
    [SerializeField, Min(0f)] private float combatEnterRange = 10f;
    [SerializeField, Min(0f)] private float combatExitRange = 13f;
    [SerializeField, Min(0f)] private float rangedRotationRestMin = 1.2f;
    [SerializeField, Min(0f)] private float rangedRotationRestMax = 2f;
    [SerializeField, Min(0f)] private float meleeRotationRestMin = 0.6f;
    [SerializeField, Min(0f)] private float meleeRotationRestMax = 1f;
    [SerializeField, Min(0f)] private float rangedRotationCooldown = 2.5f;
    [SerializeField, Min(0f)] private float meleeRotationCooldown = 0.8f;
    [SerializeField, Min(0)] private int minimumRangedSkillsPerCycle = 2;
    [SerializeField, Min(1)] private int maximumRangedSkillsPerCycle = 4;
    [SerializeField, Min(0f)] private float strongSkillCooldown = 1.5f;

    [Header("Decision")]
    [SerializeField, Min(0f)] private float decisionIntervalMin = 0.28f;
    [SerializeField, Min(0f)] private float decisionIntervalMax = 0.7f;
    [SerializeField, Min(0f)] private float retreatDuration = 0.5f;
    [SerializeField, Min(0f)] private float shortIdleMin = 0.06f;
    [SerializeField, Min(0f)] private float shortIdleMax = 0.18f;

    [Header("Dash")]
    [SerializeField, Min(0f)] private float dashSpeed = 10f;
    [SerializeField, Min(0f)] private float dashDuration = 0.25f;
    [SerializeField, Min(0f)] private float dashPrepareDuration = 0.16f;
    [SerializeField, Min(0f)] private float dashCooldown = 1.8f;
    [SerializeField, Range(0f, 1f)] private float dashChance = 0.45f;
    [SerializeField, Min(0f)] private float gapCloseDashRange = 7f;

    [Header("Dash Attack")]
    [SerializeField, Min(0f)] private float dashAttackMinRange = 2.5f;
    [SerializeField, Min(0f)] private float dashAttackMaxRange = 6f;
    [SerializeField, Min(0f)] private float dashAttackSpeed = 11f;
    [SerializeField, Min(0f)] private float dashAttackPrepareDuration = 0.18f;
    [SerializeField, Min(0f)] private float dashAttackDuration = 0.32f;
    [SerializeField, Min(0f)] private float dashAttackHitboxDelay = 0.18f;
    [SerializeField, Min(0f)] private float dashAttackHitboxDuration = 0.12f;
    [SerializeField, Min(0f)] private float dashAttackRecoveryDuration = 0.35f;
    [SerializeField, Min(0f)] private float dashAttackCooldown = 2.2f;
    [SerializeField, Min(1f)] private float dashAttackDamageMultiplier = 1.2f;

    [Header("Parry")]
    [SerializeField, Min(0f)] private float parryDuration = 0.2f;
    [SerializeField, Min(0f)] private float parryCooldown = 4f;
    [SerializeField, Min(0f)] private float parryCounterDelay = 0.08f;
    [SerializeField, Min(0f)] private float parryCounterRange = 2.5f;
    [SerializeField, Range(0f, 1f)] private float parryChance = 0.3f;
    [SerializeField, Range(0f, 1f)] private float phaseTwoParryChanceBonus = 0.15f;
    [SerializeField, Min(1)] private int maxConsecutiveParries = 1;
    [SerializeField, Min(1f)] private float parryCounterDamageMultiplier = 1.35f;

    [Header("Ranged Pressure")]
    [SerializeField] private GameObject shockwavePrefab;
    [SerializeField] private Transform shockwaveSpawnPoint;
    [SerializeField, Min(0f)] private float rangedPressureMinDistance = 4f;
    [SerializeField, Min(0f)] private float rangedPressureCooldown = 5f;
    [SerializeField, Min(0f)] private float rangedPressurePrepareDuration = 0.45f;
    [SerializeField, Min(0f)] private float rangedPressureRecoveryDuration = 0.35f;
    [SerializeField, Min(1)] private int shockwaveDamage = 16;
    [SerializeField, Min(0f)] private float shockwaveSpeed = 7f;
    [SerializeField, Min(0f)] private float secondShockwaveDelay = 0.22f;

    [Header("Skill Selection")]
    [SerializeField, Min(0f)] private float globalSkillCooldown = 0.45f;
    [SerializeField, Range(0f, 1f)] private float repeatedSkillPenalty = 0.35f;
    [SerializeField, Min(1)] private int maxSameSkillInRow = 2;

    [Header("Sword Throw")]
    [SerializeField] private GameObject swordThrowProjectilePrefab;
    [SerializeField] private Transform swordThrowPoint;
    [SerializeField] private GameObject swordVisual;
    [SerializeField, Min(1)] private int swordThrowDamage = 18;
    [SerializeField, Min(0f)] private float swordThrowSpeed = 9f;
    [SerializeField, Min(0f)] private float swordThrowCooldown = 4f;
    [SerializeField, Min(0f)] private float swordThrowPrepareDuration = 0.45f;
    [SerializeField, Min(0f)] private float swordThrowReleaseFallbackTime = 0.25f;
    [SerializeField, Min(0f)] private float swordThrowRecoveryDuration = 0.35f;
    [SerializeField, Min(0f)] private float swordRecoverFallbackTime = 0.7f;

    [Header("Jump Slam")]
    [SerializeField] private GameObject jumpSlamImpactVfx;
    [SerializeField, Min(1)] private int jumpSlamDamage = 28;
    [SerializeField, Min(0f)] private float jumpSlamCooldown = 6f;
    [SerializeField, Min(0f)] private float jumpSlamPrepareDuration = 0.18f;
    [SerializeField, Min(0f)] private float jumpSlamAscendForce = 14f;
    [SerializeField, Min(0f)] private float jumpSlamForwardSpeed = 7.5f;
    [SerializeField, Min(0f)] private float jumpSlamTrackingSpeed = 8.5f;
    [SerializeField, Range(0f, 1f)] private float jumpSlamTrackingStrength = 0.45f;
    [SerializeField, Min(0f)] private float jumpSlamMinAscendTime = 0.4f;
    [SerializeField, Min(0f)] private float jumpSlamMaxAscendTime = 1f;
    [SerializeField, Min(0f)] private float jumpSlamMinHeightBeforeDive = 4.5f;
    [SerializeField, Min(0f)] private float jumpSlamApexHoldDuration = 0.12f;
    [SerializeField, Min(0f)] private float jumpSlamPredictionTime = 0.25f;
    [SerializeField, Min(0f)] private float jumpSlamDiveSpeed = 22f;
    [SerializeField, Min(0f)] private float jumpSlamDiveHorizontalSpeed = 10f;
    [SerializeField, Min(0f)] private float jumpSlamMaxHorizontalTravel = 8f;
    [SerializeField, Min(0f)] private float jumpSlamImpactRadius = 2.5f;
    [SerializeField, Min(0f)] private float jumpSlamRecoveryDuration = 0.6f;
    [SerializeField, Min(0f)] private float jumpSlamCameraShakeDuration = 0.18f;
    [SerializeField, Min(0f)] private float jumpSlamCameraShakeMagnitude = 0.18f;

    [Header("Evade")]
    [SerializeField, Min(0f)] private float evadeSpeed = 8f;
    [SerializeField, Min(0f)] private float evadeDuration = 0.18f;
    [SerializeField, Min(0f)] private float evadeCooldown = 2.5f;
    [SerializeField, Range(0f, 1f)] private float evadeChance = 0.25f;

    [Header("Jump Evade")]
    [SerializeField, Min(0f)] private float jumpForce = 8f;
    [SerializeField, Min(0f)] private float jumpForwardSpeed = 2.5f;
    [SerializeField, Min(0f)] private float jumpCooldown = 3f;
    [SerializeField, Min(0f)] private float jumpRecoveryDuration = 0.2f;
    [SerializeField, Min(0f)] private float minimumAirTime = 0.15f;
#pragma warning disable CS0414
    [SerializeField, Range(0f, 1f)] private float jumpEvadeChance = 0.35f;
#pragma warning restore CS0414
    [SerializeField, Min(0f)] private float jumpClearanceDistance = 1.5f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Jump Attack")]
    [SerializeField, Min(0f)] private float jumpAttackForce = 8f;
    [SerializeField, Min(0f)] private float jumpAttackHorizontalSpeed = 5f;
    [SerializeField, Min(0f)] private float jumpAttackMinRange = 1.5f;
    [SerializeField, Min(0f)] private float jumpAttackMaxRange = 6f;
    [SerializeField, Min(0f)] private float jumpAttackCooldown = 3f;
    [SerializeField, Min(0f)] private float jumpAttackHitboxDelay = 0.16f;
    [SerializeField, Min(0f)] private float jumpAttackHitboxDuration = 0.18f;
    [SerializeField, Min(1f)] private float jumpAttackDamageMultiplier = 1.25f;
    [SerializeField, Min(0f)] private float jumpAttackLandingRecovery = 0.28f;
    [SerializeField, Range(0f, 1f)] private float jumpAttackTrackingStrength = 0.45f;
    [SerializeField, Min(0f)] private float jumpAttackMaxHorizontalCorrection = 2.5f;

    [Header("Defense Reaction")]
    [SerializeField, Min(0f)] private float reactionDelayMin = 0.1f;
    [SerializeField, Min(0f)] private float reactionDelayMax = 0.25f;
    [SerializeField, Min(0f)] private float minimumThreatTime = 0.08f;
    [SerializeField, Range(0f, 1f)] private float defenseReactionChance = 0.7f;
    [SerializeField, Range(0f, 1f)] private float phaseTwoDefenseReactionBonus = 0.12f;
    [SerializeField, Range(0f, 1f)] private float postEvadeCounterChance = 0.45f;

    [Header("Defense Budget")]
    [SerializeField, Min(1)] private int maxConsecutiveDefenses = 2;
    [SerializeField, Min(0f)] private float defenseResetDelay = 2f;

    [Header("Turn Slash")]
    [SerializeField, Min(0f)] private float targetBehindDuration = 0.22f;
    [SerializeField, Min(0f)] private float turnSlashCooldown = 2.5f;
    [SerializeField, Min(1f)] private float turnSlashDamageMultiplier = 1.15f;
    [SerializeField, Min(0f)] private float dashCrossTurnDelay = 0.1f;

    [Header("Adaptive Pressure")]
    [SerializeField, Min(0f)] private float outOfMeleePressureTime = 1.2f;
    [SerializeField, Min(0f)] private float adaptiveDecayDelay = 2.5f;

    [Header("Player Behavior Tracking")]
    [SerializeField, Min(0.1f)] private float behaviorSampleWindow = 3f;
    [SerializeField, Min(1)] private int jumpPatternThreshold = 3;
    [SerializeField, Min(1)] private int dashPatternThreshold = 3;
    [SerializeField, Min(0f)] private float playerDashSpeedThreshold = 8f;
    [SerializeField, Min(0f)] private float playerAirborneMinDuration = 0.12f;
    [SerializeField, Min(0f)] private float patternMemoryDecayDelay = 2.5f;

    [Header("Adaptive Counter")]
    [SerializeField, Min(0f)] private float adaptiveCounterCooldown = 1.2f;
    [SerializeField, Range(0f, 1f)] private float dashSpamParryBonus = 0.25f;
    [SerializeField, Min(0f)] private float dashSpamParryWindow = 1.5f;

    [Header("Phase 2")]
    [SerializeField, Range(1f, 1.5f)] private float phaseTwoSpeedMultiplier = 1.15f;
    [SerializeField, Range(0.5f, 1f)] private float phaseTwoDecisionMultiplier = 0.82f;
    [SerializeField, Range(0.5f, 1f)] private float phaseTwoDashCooldownMultiplier = 0.85f;
    [SerializeField, Range(0f, 1f)] private float phaseTwoDashChanceBonus = 0.12f;

    [Header("Ground Safety")]
    [SerializeField, Min(0.01f)] private float groundCheckDistance = 1.4f;
    [SerializeField, Min(0.01f)] private float wallCheckDistance = 0.45f;
    [SerializeField, Min(0f)] private float edgeSafetyMargin = 0.2f;
    [SerializeField, Min(0f)] private float maxEdgePredictionDistance = 2f;
    [SerializeField, Min(1)] private int dashGroundProbeCount = 3;
    [SerializeField, Min(1f)] private float fallRecoveryHeight = 8f;
    [SerializeField, Min(0f)] private float fallRecoveryDelay = 0.2f;
    [SerializeField, Min(0.01f)] private float safeGroundedStableDuration = 0.2f;
    [SerializeField] private LayerMask movementLayer;

    [Header("Facing")]
    [SerializeField] private bool faceTarget = true;
    [SerializeField, Min(0f)] private float faceDeadZone = 0.15f;
    [SerializeField] private bool drawMovementDebug;

    [Header("Debug")]
    [SerializeField] private bool debugBossState;
    [SerializeField, Min(0.1f)] private float debugStateInterval = 0.5f;

    [Header("Hit Flash")]
    [SerializeField] private BossSpineHitFlash hitFlash;

    private Rigidbody2D rb;
    private Rigidbody2D playerRb;
    private Collider2D bodyCollider;
    private Collider2D playerCollider;
    private AnimationSpine animationSpine;
    private Transform target;
    private Transform aggroTarget;
    private Transform cachedTarget;
    private BossState state = BossState.Idle;
    private AnimationSpine.State lastAnimation = AnimationSpine.State.None;
    private float currentHealth;
    private float attackCooldownTimer;
    private float dashCooldownTimer;
    private float dashAttackCooldownTimer;
    private float parryCooldownTimer;
    private float rangedPressureCooldownTimer;
    private float evadeCooldownTimer;
    private float jumpCooldownTimer;
    private float jumpAttackCooldownTimer;
    private float comboCooldownTimer;
    private float globalSkillCooldownTimer;
    private float swordThrowCooldownTimer;
    private float jumpSlamCooldownTimer;
    private float rangedRotationCooldownTimer;
    private float meleeRotationCooldownTimer;
    private float strongSkillCooldownTimer;
    private float combatPhaseTimer;
    private float turnSlashCooldownTimer;
    private float adaptiveCounterCooldownTimer;
    private float decisionTimer;
    private float stateTimer;
    private float dashPrepareTimer;
    private float dashAttackHitboxTimer;
    private float attackHitboxStartTimer;
    private float attackHitboxTimer;
    private float attackLungeTimer;
    private float hurtAnimationCooldownTimer;
    private float poiseResetTimer;
    private float comboStepTimer;
    private float comboGapTimer;
    private float parryCounterTimer;
    private float rangedPressureSpawnTimer;
    private float secondShockwaveTimer;
    private float swordThrowReleaseTimer;
    private float swordRecoverTimer;
    private float jumpSlamPhaseTimer;
    private float jumpSlamAscendElapsed;
    private float targetOutOfRangeTimer;
    private float targetBehindTimer;
    private float adaptiveDecayTimer;
    private float defenseResetTimer;
    private float pendingThreatReactionTimer;
    private float playerAirborneTimer;
    private float jumpPatternTimer;
    private float dashPatternTimer;
    private float dashSpamTimer;
    private float dashCrossCounterTimer;
    private float previousPlayerVelocityY;
    private float previousPlayerX;
    private float previousTargetDistance;
    private float attackStartX;
    private float groundedStableTimer;
    private float debugStateTimer;
    private float horizontalVelocity;
    private float desiredHorizontalVelocity;
    private Vector2 lastSafeGroundedPosition;
    private bool attackHitboxEnabled;
    private bool useFallbackAttackHitboxTimer;
    private bool enablingHitboxFromFallback;
    private bool slashVfxSpawned;
    private bool dashAttackHitboxSpent;
    private bool shockwaveSpawned;
    private bool secondShockwaveSpawned;
    private bool swordReleased;
    private bool jumpSlamImpacted;
    private bool isRunMode;
    private bool isChaseMode;
    private bool hasPendingThreat;
    private bool isAggro;
    private bool isGrounded;
    private bool hasLeftGround;
    private bool playerWasGrounded;
    private bool playerWasDashing;
    private bool queueJumpAttackAfterDash;
    private bool waitingForComboStep;
    private bool shouldCounterAfterDefense;
    private bool hasSafeGroundedPosition;
    private int poiseHitCount;
    private int consecutiveParries;
    private int consecutiveDefenses;
    private int playerEscapeCount;
    private int consecutiveRangedPressure;
    private int recentRangedHits;
    private int recentPlayerJumps;
    private int recentPlayerDashes;
    private int recentDashCrosses;
    private int dashStartSide;
    private int moveDirection = 1;
    private int facingDirection = 1;
    private int lockedAttackDirection = 1;
    private int lockedDashDirection = 1;
    private int currentAttackDamage;
    private int comboIndex;
    private int jumpDirection;
    private Vector2 jumpSlamTargetPosition;
    private Vector2 jumpSlamStartPosition;
    private DashMode dashMode;
    private EvadeType evadeType;
    private BossSkillAction lastSkillAction = BossSkillAction.None;
    private BossSkillAction currentSkillAction = BossSkillAction.None;
    private CombatPhase combatPhase = CombatPhase.Ranged;
    private CombatPhase nextCombatPhase = CombatPhase.Melee;
    private JumpSlamPhase jumpSlamPhase = JumpSlamPhase.None;
    private int sameSkillInRow;
    private int usedRangedSkillMask;
    private int rangedSkillsUsedThisCycle;
    private IncomingThreat pendingThreat;
    private BossConfig bossConfig;
    private BossRuntimeModel bossRuntime;
    private BossDamageModel bossDamageModel;
    private BossThreatModel bossThreatModel;
    private BossMovementPlanner bossMovementPlanner;
    private BossStateDriver bossStateDriver;
    private BossCombatBrain bossCombatBrain;

    public Transform Transform => transform;
    public bool IsAlive => state != BossState.Dead;
    public float CurrentHealth => currentHealth;

    private bool IsPhaseTwo => currentHealth <= maxHealth * 0.5f;
    private bool IsAttacking => bossStateDriver != null
        ? bossStateDriver.IsAttacking(ToBossStateId(state))
        : state == BossState.Attack || state == BossState.ComboAttack ||
            state == BossState.DashAttack || state == BossState.ParryCounter || state == BossState.JumpAttack;
    private bool IsHardLocked => bossStateDriver != null
        ? bossStateDriver.IsHardLocked(ToBossStateId(state))
        : IsAttacking ||
            state == BossState.AttackRecovery ||
            state == BossState.PostAttackPause ||
            state == BossState.Parry ||
            state == BossState.RangedPressure ||
            state == BossState.Evade ||
            state == BossState.JumpEvade ||
            state == BossState.JumpAttack ||
            state == BossState.Hurt ||
            state == BossState.Stun ||
            state == BossState.Dead;
    private bool PlayerFrequentlyJumps => recentPlayerJumps >= jumpPatternThreshold;
    private bool PlayerFrequentlyDashes => recentPlayerDashes >= dashPatternThreshold;
    private bool PlayerFrequentlyDashesThrough => recentDashCrosses >= dashPatternThreshold;

    private BossConfig BuildBossConfig()
    {
        return new BossConfig
        {
            MaxHealth = maxHealth,
            HurtLockDuration = hurtLockDuration,
            HurtAnimationCooldown = hurtAnimationCooldown,
            PoiseHitsBeforeInterrupt = poiseHitsBeforeInterrupt,
            PoiseResetDelay = poiseResetDelay,
            PhaseTwoPoiseBonus = phaseTwoPoiseBonus,
            AttackRange = attackRange,
            AttackingDamageTakenMultiplier = attackingDamageTakenMultiplier,
            AttackAnimationDuration = attackAnimationDuration,
            ParryCounterDelay = parryCounterDelay,
            MinimumThreatTime = minimumThreatTime,
            DefenseReactionChance = defenseReactionChance,
            PhaseTwoDefenseReactionBonus = phaseTwoDefenseReactionBonus,
            ReactionDelayMin = reactionDelayMin,
            ReactionDelayMax = reactionDelayMax,
            PhaseTwoDecisionMultiplier = phaseTwoDecisionMultiplier
        };
    }

    private void InitializeBossCore()
    {
        bossConfig = BuildBossConfig();
        bossRuntime = new BossRuntimeModel();
        bossRuntime.Reset(maxHealth);
        bossDamageModel = new BossDamageModel(bossConfig);
        bossThreatModel = new BossThreatModel(bossConfig);
        bossMovementPlanner = new BossMovementPlanner();
        bossStateDriver = new BossStateDriver();
        bossCombatBrain = new BossCombatBrain();
        SyncRuntimeFromController();
    }

    private void SyncRuntimeFromController()
    {
        if (bossRuntime == null)
        {
            return;
        }

        bossRuntime.State = ToBossStateId(state);
        bossRuntime.CurrentHealth = currentHealth;
        bossRuntime.StateTimer = stateTimer;
        bossRuntime.DecisionTimer = decisionTimer;
        bossRuntime.AttackCooldownTimer = attackCooldownTimer;
        bossRuntime.DashCooldownTimer = dashCooldownTimer;
        bossRuntime.DashAttackCooldownTimer = dashAttackCooldownTimer;
        bossRuntime.ParryCooldownTimer = parryCooldownTimer;
        bossRuntime.EvadeCooldownTimer = evadeCooldownTimer;
        bossRuntime.JumpCooldownTimer = jumpCooldownTimer;
        bossRuntime.HurtAnimationCooldownTimer = hurtAnimationCooldownTimer;
        bossRuntime.PoiseResetTimer = poiseResetTimer;
        bossRuntime.DefenseResetTimer = defenseResetTimer;
        bossRuntime.PendingThreatReactionTimer = pendingThreatReactionTimer;
        bossRuntime.PoiseHitCount = poiseHitCount;
        bossRuntime.ConsecutiveParries = consecutiveParries;
        bossRuntime.ConsecutiveDefenses = consecutiveDefenses;
        bossRuntime.RecentRangedHits = recentRangedHits;
        bossRuntime.FacingDirection = facingDirection;
        bossRuntime.MoveDirection = moveDirection;
        bossRuntime.IsAggro = isAggro;
        bossRuntime.HasPendingThreat = hasPendingThreat;
    }

    private void SyncControllerFromRuntime()
    {
        if (bossRuntime == null)
        {
            return;
        }

        currentHealth = bossRuntime.CurrentHealth;
        poiseHitCount = bossRuntime.PoiseHitCount;
        recentRangedHits = bossRuntime.RecentRangedHits;
        consecutiveDefenses = bossRuntime.ConsecutiveDefenses;
        consecutiveParries = bossRuntime.ConsecutiveParries;
        isAggro = bossRuntime.IsAggro;
        hasPendingThreat = bossRuntime.HasPendingThreat;
        pendingThreatReactionTimer = bossRuntime.PendingThreatReactionTimer;
    }

    private static BossStateId ToBossStateId(BossState source)
    {
        switch (source)
        {
            case BossState.Chase: return BossStateId.Chase;
            case BossState.Approach: return BossStateId.Approach;
            case BossState.Retreat: return BossStateId.Retreat;
            case BossState.Dash: return BossStateId.Dash;
            case BossState.DashAttack: return BossStateId.DashAttack;
            case BossState.Attack: return BossStateId.Attack;
            case BossState.ComboAttack: return BossStateId.ComboAttack;
            case BossState.AttackRecovery: return BossStateId.AttackRecovery;
            case BossState.PostAttackPause: return BossStateId.PostAttackPause;
            case BossState.Parry: return BossStateId.Parry;
            case BossState.ParryCounter: return BossStateId.ParryCounter;
            case BossState.RangedPressure: return BossStateId.RangedPressure;
            case BossState.SwordThrow: return BossStateId.SwordThrow;
            case BossState.JumpSlam: return BossStateId.JumpSlam;
            case BossState.Evade: return BossStateId.Evade;
            case BossState.JumpEvade: return BossStateId.JumpEvade;
            case BossState.JumpAttack: return BossStateId.JumpAttack;
            case BossState.Hurt: return BossStateId.Hurt;
            case BossState.Stun: return BossStateId.Stun;
            case BossState.Dead: return BossStateId.Dead;
            default: return BossStateId.Idle;
        }
    }

    private static BossThreatType ToBossThreatType(IncomingThreatType source)
    {
        switch (source)
        {
            case IncomingThreatType.Melee: return BossThreatType.Melee;
            case IncomingThreatType.ProjectileLow: return BossThreatType.ProjectileLow;
            case IncomingThreatType.ProjectileHigh: return BossThreatType.ProjectileHigh;
            case IncomingThreatType.GroundAttack: return BossThreatType.GroundAttack;
            case IncomingThreatType.HeavyAttack: return BossThreatType.HeavyAttack;
            default: return BossThreatType.ProjectileHigh;
        }
    }
}
