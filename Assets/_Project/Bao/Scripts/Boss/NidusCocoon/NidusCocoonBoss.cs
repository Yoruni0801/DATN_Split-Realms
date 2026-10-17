using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Bao.Bosses
{
    public enum BossPhase
    {
        Phase1,
        Phase2
    }

    [RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(Collider2D))]
    [RequireComponent(typeof(NidusCocoonDeathCutsceneController))]
    public class NidusCocoonBoss : MonoBehaviour, Bao.Enemies.IDamageable
    {
        [Header("Boss - Health & Phase")]
        [SerializeField] private int maxHealth = 200;
        [SerializeField] private float phase2Threshold = 0.4f;
        [SerializeField] private BossPhase currentPhase = BossPhase.Phase1;
        [SerializeField] private GameObject phaseTransitionVFXPrefab;
        [SerializeField] private GameObject deathVFXPrefab;
        [SerializeField] private float phaseTransitionDuration = 1.5f;

        [Header("Boss - Warning")]
        [Tooltip("Kéo object chứa dấu chấm than vào đây")]
        [SerializeField] private GameObject warningObject;
        [Tooltip("Thời gian gồng (dấu chấm than hiện lên) trước khi ra đòn")]
        [SerializeField] private float chargeDelay = 0.5f;

        public float ChargeDelay => chargeDelay;

        [Header("Boss - Movement & Detection")]
        [SerializeField] private float moveSpeed = 1.5f;
        [SerializeField] private float detectionRange = 10f;
        [SerializeField] private float attackRange = 1.6f;
        [SerializeField] private LayerMask environmentLayer;

        [Header("Boss - Melee Attack (Phase 1 & 2)")]
        [SerializeField] private int meleeDamage = 15;
        [SerializeField] private float meleeCooldown = 2.0f;
        [SerializeField] private float meleeRadius = 1.5f;
        [SerializeField] private float meleeDelay = 0.3f;
        [SerializeField] private float meleeAnimDuration = 0.8f;

        [Header("Boss - Summon Minions (Phase 1 & 2)")]
        [SerializeField] private GameObject minionPrefab;
        [SerializeField] private int summonCount = 2;
        [SerializeField] private float summonCooldown = 12.0f;
        [SerializeField] private float summonRadius = 3f;

        [Header("Boss - Parasite Brood")]
        [SerializeField] private GameObject parasiteSeedPrefab;
        [SerializeField, Min(1)] private int parasiteSeedHealth = 8;
        [SerializeField, Min(0.6f)] private float parasiteSeedHatchTime = 2.35f;
        [SerializeField, Min(1)] private int maxActiveBrood = 3;

        [Header("Boss - Root Spike Wave (Phase 1 & 2)")]
        [SerializeField] private float rootSpikeCooldown = 6.5f;
        [SerializeField] private float rootSpikeWarningDuration = 0.65f;
        [SerializeField] private float rootSpikeInterval = 0.12f;
        [SerializeField] private int rootSpikeDamage = 8;
        [SerializeField] private float rootSpikeRadius = 0.55f;
        [SerializeField] private int rootSpikeCountPhase1 = 5;
        [SerializeField] private int rootSpikeCountPhase2 = 6;

        [Header("Boss - Root Sprite Visual (Inspector Control)")]
        [Tooltip("Sprite và animation dùng cho rễ trồi lên. Chỉnh các giá trị này để preview/tune mà không cần sửa code.")]
        [SerializeField] private NidusRootSpikeVisualSettings rootSpikeVisualSettings = new NidusRootSpikeVisualSettings();

        [Header("Boss - Root Prison (Phase 2 Only)")]
        [SerializeField] private float rootPrisonCooldown = 9f;
        [SerializeField] private float rootPrisonOuterWarning = 0.85f;
        [SerializeField] private float rootPrisonWallLifetime = 1.5f;
        [SerializeField] private int rootPrisonWallDamage = 5;
        [SerializeField] private float rootPrisonHalfWidth = 1.5f;
        [SerializeField] private float rootPrisonCenterWarning = 0.45f;
        [SerializeField] private int rootPrisonCenterDamage = 8;

        [Header("Boss - Acid Rain (Phase 2 Only)")]
        [SerializeField] private float acidRainCooldown = 10.0f;
        [SerializeField] private float acidRainAnimDuration = 2.5f;
        [SerializeField] private float acidRainCastDelay = 1.0f;
        [SerializeField] private int acidRainProjectileCount = 4;
        [SerializeField] private float acidRainSpreadRadius = 5f;
        [SerializeField] private float acidWarningDuration = 1.5f;
        [SerializeField] private float acidDamageRadius = 1.2f;
        [SerializeField] private int acidDamage = 8;
        [SerializeField] private float acidPoolDuration = 4f;
        [SerializeField] private int acidPoolDamagePerSecond = 3;
        [SerializeField] private GameObject acidWarningPrefab;
        [SerializeField] private GameObject acidProjectilePrefab;
        [SerializeField] private GameObject acidPoolPrefab;

        [Header("Boss - Acid Pool Expiry Detonation")]
        [SerializeField, Min(0.1f)] private float acidPoolExpiryPrimeDuration = 0.55f;
        [SerializeField, Min(0)] private int acidPoolExpiryExplosionDamage = 8;
        [SerializeField, Min(1f)] private float acidPoolExpiryExplosionRadiusMultiplier = 1.15f;
        [SerializeField, Min(0f)] private float acidPoolExpiryExplosionKnockbackForce = 3.5f;

        [Header("Boss - Toxic Detonation (Phase 2 Only)")]
        [SerializeField] private float toxicDetonationCooldown = 8f;
        [SerializeField, Min(2)] private int toxicDetonationMinimumPools = 2;
        [SerializeField, Min(1)] private int toxicDetonationMaxPools = 5;
        [SerializeField, Min(0.45f)] private float toxicDetonationPrimeDuration = 0.9f;
        [SerializeField] private int toxicDetonationDamage = 11;
        [SerializeField] private float toxicDetonationRadiusMultiplier = 1.35f;
        [SerializeField] private float toxicDetonationKnockbackForce = 4.5f;

        [Header("Boss - Burrow & Emerge (Phase 2 Only)")]
        [SerializeField] private float burrowCooldown = 8.0f;
        [SerializeField] private float burrowFadeOutDelay = 0.2f;
        [SerializeField] private float burrowFadeOutDuration = 0.5f;
        [SerializeField] private float emergeFadeInDuration = 0.3f;
        [SerializeField] private float burrowSpeed = 6.0f;
        [SerializeField] private float burrowTimeUnderground = 2.5f;
        [SerializeField] private int burrowDamage = 20;
        [SerializeField] private float burrowKnockbackForce = 10f;
        [SerializeField] private float emergeWarningDuration = 1.2f;
        [SerializeField] private float emergeDamageRadius = 2.0f;
        [SerializeField] private GameObject emergeWarningPrefab;
        [SerializeField] private GameObject burrowVFXPrefab;
        [SerializeField] private GameObject emergeVFXPrefab;
        [SerializeField] private GameObject burrowTrailVFXPrefab;
        [SerializeField] private float burrowTrailInterval = 0.3f;

        [Header("Boss - AI Behavior & Pacing")]
        [SerializeField] private float globalActionCooldown = 0.65f;
        [Tooltip("Boss chases until this distance before it may spend a combat action.")]
        [SerializeField, Min(0.5f)] private float actionEngageRange = 8f;
        [SerializeField] private float optimalSpacingDistance = 4f;
        [SerializeField] private float backingSpeedMultiplier = 0.5f;

        [Header("Combo Cooldown")]
        [SerializeField, Min(0f)] private float comboCooldown = 0.7f;
        [SerializeField] private bool facePlayerDuringCooldown = true;

        [Header("Core Exposed Recovery")]
        [SerializeField, Min(1f)] private float coreExposeDamageMultiplier = 1.30f;
        [SerializeField, Min(0f)] private float coreExposeDelay = 0.18f;
        [SerializeField, Min(0.1f)] private float coreExposeDuration = 0.75f;
        [SerializeField, Min(0f)] private float coreCloseDuration = 0.35f;

        [Header("Desperation - Low HP")]
        [SerializeField, Range(0.05f, 0.30f)] private float desperationThreshold = 0.15f;
        [SerializeField, Range(0.5f, 1f)] private float desperationActionCooldownMultiplier = 0.82f;
        [SerializeField, Range(0.5f, 1f)] private float desperationRecoveryMultiplier = 0.80f;
        [SerializeField, Range(1f, 1.5f)] private float desperationBurrowSpeedMultiplier = 1.10f;

        [Header("Boss - Debug & Visuals")]
        [SerializeField] private SpriteRenderer[] spriteRenderers;
        [SerializeField] private float facingVisualMultiplier = -1f;
        [SerializeField] private bool drawGizmos = true;
        [Tooltip("Tick this in inspector to force phase 2 transition")]
        [SerializeField] private bool testForcePhase2 = false;

        [Header("Boss - Shader Controller")]
        [SerializeField] private NidusBossShaderController shaderController;
        private HealthPool health;

        [Header("Boss - Death Drop")]
        [SerializeField] private GameObject deathDropItemPrefab;
        [SerializeField] private Vector3 deathDropOffset = Vector3.up;
        [SerializeField] private bool addDropPhysicsIfMissing = true;

        [Header("Boss - Death Shake")]
        [SerializeField] private float deathShakeDuration = 0.8f;
        [SerializeField] private float deathShakeAmplitude = 0.08f;
        [SerializeField] private float deathShakeFrequency = 35f;

        [Header("Boss - Death Cutscene")]
        [SerializeField] private NidusCocoonDeathCutsceneController deathCutsceneController;
        [SerializeField, HideInInspector] private GameObject deathCutsceneObject;
        [SerializeField, HideInInspector] private float destroyDelayIfNoNextScene = 3f;

        [Header("Boss - UI")]
        [SerializeField] private BossUIEventDispatcher uiEventDispatcher;

        // Public properties for state machine access
        public int MaxHealth => maxHealth;
        public int CurrentHealth => health != null ? Mathf.RoundToInt(health.CurrentHealth) : maxHealth;
        public BossPhase CurrentPhase => currentPhase;
        public float MoveSpeed => moveSpeed;
        public LayerMask EnvironmentLayer => environmentLayer;
        
        public float GlobalActionCooldown => globalActionCooldown;
        public float CurrentGlobalActionCooldown => globalActionCooldown * (IsDesperate ? desperationActionCooldownMultiplier : 1f);
        public float ActionEngageRange => Mathf.Max(0.5f, actionEngageRange);
        public float OptimalSpacingDistance => optimalSpacingDistance;
        public float BackingSpeedMultiplier => backingSpeedMultiplier;
        public float ComboCooldown => comboCooldown;
        public float CurrentComboCooldown => comboCooldown * (IsDesperate ? desperationRecoveryMultiplier : 1f);
        public bool FacePlayerDuringCooldown => facePlayerDuringCooldown;

        // References
        public Rigidbody2D Rigidbody2D { get; private set; }
        public Animator Animator { get; private set; }
        public Collider2D Collider2D { get; private set; }
        public Transform Target { get; private set; }
        public SpriteRenderer[] SpriteRenderers => spriteRenderers;

        // Melee
        public int MeleeDamage => meleeDamage;
        public float AttackRange => attackRange;
        public float MeleeCooldown => meleeCooldown;
        public float MeleeRadius => meleeRadius;
        public float MeleeDelay => meleeDelay;
        public float MeleeAnimDuration => meleeAnimDuration;

        // Summon / Parasite Brood
        public GameObject MinionPrefab => minionPrefab;
        public GameObject ParasiteSeedPrefab => parasiteSeedPrefab;
        public int SummonCount => summonCount;
        public int EffectiveSummonCount => Mathf.Min(summonCount + (IsDesperate ? 1 : 0), AvailableBroodSlots);
        public float SummonRadius => summonRadius;
        public int ParasiteSeedHealth => parasiteSeedHealth;
        public float ParasiteSeedHatchTime => parasiteSeedHatchTime;
        public int MaxActiveBrood => maxActiveBrood;
        public int ActiveBroodCount { get { PruneOwnedCollections(); return activeSeeds.Count + activeMinions.Count; } }
        public int AvailableBroodSlots => Mathf.Max(0, maxActiveBrood - ActiveBroodCount);

        // Root Spike
        public float RootSpikeWarningDuration => Mathf.Max(0.45f, rootSpikeWarningDuration);
        public float RootSpikeInterval => rootSpikeInterval;
        public int RootSpikeDamage => rootSpikeDamage;
        public float RootSpikeRadius => rootSpikeRadius;
        public NidusRootSpikeVisualSettings RootSpikeVisualSettings => rootSpikeVisualSettings;
        public int EffectiveRootSpikeCount => (currentPhase == BossPhase.Phase1 ? rootSpikeCountPhase1 : rootSpikeCountPhase2) + (IsDesperate ? 1 : 0);

        // Root Prison
        public float RootPrisonOuterWarning => Mathf.Max(0.45f, rootPrisonOuterWarning);
        public float RootPrisonWallLifetime => rootPrisonWallLifetime;
        public int RootPrisonWallDamage => rootPrisonWallDamage;
        public float RootPrisonHalfWidth => rootPrisonHalfWidth;
        public float RootPrisonCenterWarning => Mathf.Max(0.45f, rootPrisonCenterWarning);
        public int RootPrisonCenterDamage => rootPrisonCenterDamage;

        // Acid Rain
        public float AcidRainAnimDuration => acidRainAnimDuration;
        public float AcidRainCastDelay => acidRainCastDelay;
        public int AcidRainProjectileCount => acidRainProjectileCount;
        public int EffectiveAcidRainProjectileCount => acidRainProjectileCount + (IsDesperate ? 1 : 0);
        public float AcidRainSpreadRadius => acidRainSpreadRadius;
        public float AcidWarningDuration => acidWarningDuration;
        public float AcidDamageRadius => acidDamageRadius;
        public int AcidDamage => acidDamage;
        public float AcidPoolDuration => acidPoolDuration;
        public int AcidPoolDamagePerSecond => acidPoolDamagePerSecond;
        public GameObject AcidWarningPrefab => acidWarningPrefab;
        public GameObject AcidProjectilePrefab => acidProjectilePrefab;
        public GameObject AcidPoolPrefab => acidPoolPrefab;
        public float AcidPoolExpiryPrimeDuration => acidPoolExpiryPrimeDuration;
        public int AcidPoolExpiryExplosionDamage => acidPoolExpiryExplosionDamage;
        public float AcidPoolExpiryExplosionRadiusMultiplier => acidPoolExpiryExplosionRadiusMultiplier;
        public float AcidPoolExpiryExplosionKnockbackForce => acidPoolExpiryExplosionKnockbackForce;

        // Toxic Detonation
        public int ToxicDetonationMinimumPools => toxicDetonationMinimumPools;
        public int ToxicDetonationMaxPools => toxicDetonationMaxPools;
        public float ToxicDetonationPrimeDuration => toxicDetonationPrimeDuration;
        public int ToxicDetonationDamage => toxicDetonationDamage;
        public float ToxicDetonationRadiusMultiplier => toxicDetonationRadiusMultiplier;
        public float ToxicDetonationKnockbackForce => toxicDetonationKnockbackForce;
        public int ActiveAcidPoolCount
        {
            get
            {
                PruneOwnedCollections();
                int count = 0;
                for (int i = 0; i < activeAcidPools.Count; i++)
                    if (activeAcidPools[i] != null && activeAcidPools[i].IsAvailableForDetonation) count++;
                return count;
            }
        }

        // Burrow
        public float BurrowFadeOutDelay => burrowFadeOutDelay;
        public float BurrowFadeOutDuration => burrowFadeOutDuration;
        public float EmergeFadeInDuration => emergeFadeInDuration;
        public float BurrowSpeed => burrowSpeed;
        public float EffectiveBurrowSpeed => burrowSpeed * (IsDesperate ? desperationBurrowSpeedMultiplier : 1f);
        public float BurrowTimeUnderground => burrowTimeUnderground;
        public int BurrowDamage => burrowDamage;
        public float BurrowKnockbackForce => burrowKnockbackForce;
        public float EmergeWarningDuration => emergeWarningDuration;
        public float EmergeDamageRadius => emergeDamageRadius;
        public GameObject EmergeWarningPrefab => emergeWarningPrefab;
        public GameObject BurrowVFXPrefab => burrowVFXPrefab;
        public GameObject EmergeVFXPrefab => emergeVFXPrefab;
        public GameObject BurrowTrailVFXPrefab => burrowTrailVFXPrefab;
        public float BurrowTrailInterval => burrowTrailInterval;

        public bool IsInvulnerable { get; set; } = false;
        public bool IsTransitioningPhases { get; private set; } = false;
        public bool IsDead { get; private set; } = false;
        public bool IsCoolingDown { get; private set; } = false;
        public bool IsCoreExposed { get; private set; } = false;
        public bool IsDesperate { get; private set; } = false;
        public float FacingDirection { get; private set; } = 1f;
        public NidusBossShaderController ShaderController => shaderController;

        public Vector3 CenterPosition => Collider2D != null ? Collider2D.bounds.center : transform.position;

        // State Machine
        public NidusBossState CurrentState { get; private set; }

        public enum SkillCategory { None, Melee, Ranged, Summon, Control, Hazard, Movement }
        public enum SkillId { None, Melee, ParasiteSeed, RootSpike, AcidRain, Burrow, RootPrison, ToxicDetonation }
        public SkillCategory LastUsedSkillCategory { get; private set; } = SkillCategory.None;
        public SkillId LastUsedSkill { get; private set; } = SkillId.None;

        // Owned encounter objects
        private readonly List<NidusParasiteSeed> activeSeeds = new List<NidusParasiteSeed>();
        private readonly List<NidusMinion> activeMinions = new List<NidusMinion>();
        private readonly List<NidusAcidPool> activeAcidPools = new List<NidusAcidPool>();
        private readonly List<NidusRootSpikeHazard> activeRootHazards = new List<NidusRootSpikeHazard>();

        // Cooldown Timers
        private float lastMeleeTime = -99f;
        private float lastSummonTime = -99f;
        private float lastRootSpikeTime = -99f;
        private float lastAcidRainTime = -99f;
        private float lastBurrowTime = -99f;
        private float lastRootPrisonTime = -99f;
        private float lastToxicDetonationTime = -99f;
        private float lastActionTime = -99f;
        private bool pendingDesperation;
        private Coroutine cooldownCoroutine;
        private Coroutine deathRoutine;

        // Prebuilt States
        public NidusIdleState IdleState { get; private set; }
        public NidusChaseState ChaseState { get; private set; }
        public NidusMeleeState MeleeState { get; private set; }
        public NidusSummonState SummonState { get; private set; }
        public NidusRootSpikeWaveState RootSpikeState { get; private set; }
        public NidusAcidRainState AcidRainState { get; private set; }
        public NidusRootPrisonState RootPrisonState { get; private set; }
        public NidusToxicDetonationState ToxicDetonationState { get; private set; }
        public NidusBurrowState BurrowState { get; private set; }
        public NidusCooldownState CooldownState { get; private set; }
        public NidusDeadState DeadState { get; private set; }

        private void Awake()
        {
            Rigidbody2D = GetComponent<Rigidbody2D>();
            Animator = GetComponent<Animator>();
            Collider2D = GetComponent<Collider2D>();
            
            Rigidbody2D.freezeRotation = true;
            health = new HealthPool(maxHealth);

            if (spriteRenderers == null || spriteRenderers.Length == 0)
            {
                var allRenderers = GetComponentsInChildren<SpriteRenderer>();
                var filtered = new System.Collections.Generic.List<SpriteRenderer>();
                foreach (var sr in allRenderers)
                {
                    if (warningObject != null && sr.gameObject == warningObject) continue;
                    filtered.Add(sr);
                }
                spriteRenderers = filtered.ToArray();
            }

            if (warningObject != null) warningObject.SetActive(false);

            // Auto-detect ShaderController if not assigned
            if (shaderController == null)
                shaderController = GetComponentInChildren<NidusBossShaderController>();

            if (deathCutsceneController == null)
                deathCutsceneController = GetComponent<NidusCocoonDeathCutsceneController>();
            deathCutsceneController?.ConfigureLegacyReferences(deathCutsceneObject, destroyDelayIfNoNextScene);

            // Create all states
            IdleState = new NidusIdleState(this);
            ChaseState = new NidusChaseState(this);
            MeleeState = new NidusMeleeState(this);
            SummonState = new NidusSummonState(this);
            RootSpikeState = new NidusRootSpikeWaveState(this);
            AcidRainState = new NidusAcidRainState(this);
            RootPrisonState = new NidusRootPrisonState(this);
            ToxicDetonationState = new NidusToxicDetonationState(this);
            BurrowState = new NidusBurrowState(this);
            CooldownState = new NidusCooldownState(this);
            DeadState = new NidusDeadState(this);
        }

        private void Start()
        {
            if (uiEventDispatcher == null)
            {
                uiEventDispatcher = GetComponent<BossUIEventDispatcher>();
            }
            if (uiEventDispatcher != null)
            {
                uiEventDispatcher.Initialize("Nidus Cocoon", maxHealth);
            }

            FindPlayer();
            ApplyFacing(FacingDirection);
            ChangeState(IdleState);
        }

        private void Update()
        {
            if (IsDead) return;

            if (Target == null)
            {
                FindPlayer();
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Press F2 to instantly trigger Phase 2 for testing
            if (Input.GetKeyDown(KeyCode.F2))
            {
                ForcePhase2();
            }
#endif

            if (testForcePhase2)
            {
                testForcePhase2 = false;
                ForcePhase2();
            }

            CurrentState?.LogicUpdate();
        }

        private void FixedUpdate()
        {
            if (IsDead) return;
            CurrentState?.PhysicsUpdate();
        }

        public void ChangeState(NidusBossState newState)
        {
            if (CurrentState == DeadState || newState == null || CurrentState == newState) return;

            CurrentState?.Exit();
            CurrentState = newState;
            CurrentState.Enter();
        }

        private void FindPlayer()
        {
            if (PlayerRegistry.Current != null)
            {
                Target = PlayerRegistry.Current.transform;
                return;
            }
        }

        private bool hasEngaged = false;

        public bool HasTargetInDetectionRange()
        {
            if (Target == null) return false;
            
            if (hasEngaged) return true;

            if (Vector2.Distance(CenterPosition, Target.position) <= detectionRange)
            {
                hasEngaged = true;
                if (uiEventDispatcher != null) uiEventDispatcher.Engage();
                return true;
            }

            if (currentPhase == BossPhase.Phase2 || CurrentHealth < maxHealth)
            {
                hasEngaged = true;
                if (uiEventDispatcher != null) uiEventDispatcher.Engage();
                return true;
            }

            return false;
        }

        public bool HasTargetInMeleeRange()
        {
            if (Target == null) return false;
            return Vector2.Distance(CenterPosition, Target.position) <= attackRange;
        }

        public bool IsTargetWithinDetectionRange()
        {
            if (Target == null) return false;
            return Vector2.Distance(CenterPosition, Target.position) <= detectionRange;
        }

        public Vector3 PredictTargetPosition(float leadTime, float maxHorizontalLead)
        {
            if (Target == null)
                return CenterPosition;

            Vector3 predicted = Target.position;
            Rigidbody2D targetBody = Target.GetComponent<Rigidbody2D>();
            if (targetBody == null)
                targetBody = Target.GetComponentInParent<Rigidbody2D>();

            if (targetBody != null)
            {
                float maximumLead = Mathf.Max(0f, maxHorizontalLead);
                float offset = Mathf.Clamp(targetBody.linearVelocity.x * Mathf.Max(0f, leadTime), -maximumLead, maximumLead);
                predicted.x += offset;
            }

            return predicted;
        }

        public bool CanPerformAction() => Time.time >= lastActionTime + CurrentGlobalActionCooldown;

        public void MarkActionUsed(SkillCategory category, SkillId skill)
        {
            lastActionTime = Time.time;
            LastUsedSkillCategory = category;
            LastUsedSkill = skill;
        }

        public bool CanMelee()
        {
            return Time.time >= lastMeleeTime + meleeCooldown && !IsTransitioningPhases && CanPerformAction();
        }

        public void MarkMeleeUsed()
        {
            lastMeleeTime = Time.time;
            MarkActionUsed(SkillCategory.Melee, SkillId.Melee);
        }

        public bool CanSummon()
        {
            PruneOwnedCollections();
            return AvailableBroodSlots > 0
                && Time.time >= lastSummonTime + summonCooldown
                && !IsTransitioningPhases
                && CanPerformAction();
        }

        public void MarkSummonUsed()
        {
            lastSummonTime = Time.time;
            MarkActionUsed(SkillCategory.Summon, SkillId.ParasiteSeed);
        }

        public bool CanRootSpike()
        {
            return Time.time >= lastRootSpikeTime + rootSpikeCooldown
                && !IsTransitioningPhases
                && CanPerformAction();
        }

        public void MarkRootSpikeUsed()
        {
            lastRootSpikeTime = Time.time;
            MarkActionUsed(SkillCategory.Ranged, SkillId.RootSpike);
        }

        public bool CanAcidRain()
        {
            return currentPhase == BossPhase.Phase2
                && Time.time >= lastAcidRainTime + acidRainCooldown
                && !IsTransitioningPhases
                && CanPerformAction();
        }

        public void MarkAcidRainUsed()
        {
            lastAcidRainTime = Time.time;
            MarkActionUsed(SkillCategory.Hazard, SkillId.AcidRain);
        }

        public bool CanBurrow()
        {
            return currentPhase == BossPhase.Phase2
                && Time.time >= lastBurrowTime + burrowCooldown
                && !IsTransitioningPhases
                && CanPerformAction();
        }

        public void MarkBurrowUsed()
        {
            lastBurrowTime = Time.time;
            MarkActionUsed(SkillCategory.Movement, SkillId.Burrow);
        }

        public bool CanRootPrison()
        {
            return currentPhase == BossPhase.Phase2
                && Time.time >= lastRootPrisonTime + rootPrisonCooldown
                && !IsTransitioningPhases
                && CanPerformAction();
        }

        public void MarkRootPrisonUsed()
        {
            lastRootPrisonTime = Time.time;
            MarkActionUsed(SkillCategory.Control, SkillId.RootPrison);
        }

        public bool CanToxicDetonation()
        {
            return currentPhase == BossPhase.Phase2
                && LastUsedSkill != SkillId.RootPrison
                && ActiveAcidPoolCount >= toxicDetonationMinimumPools
                && Time.time >= lastToxicDetonationTime + toxicDetonationCooldown
                && !IsTransitioningPhases
                && CanPerformAction();
        }

        public void MarkToxicDetonationUsed()
        {
            lastToxicDetonationTime = Time.time;
            MarkActionUsed(SkillCategory.Hazard, SkillId.ToxicDetonation);
        }

        public void FacePosition(Vector2 position)
        {
            float deltaX = position.x - transform.position.x;
            if (Mathf.Approximately(deltaX, 0f)) return;

            float desiredDirection = Mathf.Sign(deltaX);
            if (!Mathf.Approximately(desiredDirection, FacingDirection))
            {
                FacingDirection = desiredDirection;
                ApplyFacing(FacingDirection);
            }
        }

        public void MoveForward(float direction)
        {
            FacingDirection = Mathf.Sign(direction == 0f ? FacingDirection : direction);
            ApplyFacing(FacingDirection);
            float speedMultiplier = Mathf.Clamp01(Mathf.Abs(direction));
            Rigidbody2D.linearVelocity = new Vector2(FacingDirection * moveSpeed * speedMultiplier, Rigidbody2D.linearVelocity.y);
        }

        public void StopHorizontalMovement()
        {
            Rigidbody2D.linearVelocity = new Vector2(0f, Rigidbody2D.linearVelocity.y);
        }

        public void StopMoving()
        {
            Rigidbody2D.linearVelocity = Vector2.zero;
        }

        public void EnterCooldownState()
        {
            StopMoving();
            HideWarning();
            TryActivateDesperationAtSafePoint();

            if (Animator != null)
            {
                Animator.SetBool("isWalk", false);
                Animator.SetBool("isIdle", false);
                Animator.speed = 1f;
            }

            IsCoolingDown = true;

            if (cooldownCoroutine != null)
                StopCoroutine(cooldownCoroutine);

            cooldownCoroutine = StartCoroutine(CooldownRoutine());
        }

        public void ExitCooldownState()
        {
            IsCoolingDown = false;
            SetCoreExposed(false);

            if (cooldownCoroutine != null)
            {
                StopCoroutine(cooldownCoroutine);
                cooldownCoroutine = null;
            }

            if (Animator != null)
                Animator.speed = 1f;
        }

        private IEnumerator CooldownRoutine()
        {
            float totalDuration = Mathf.Max(0f, CurrentComboCooldown);
            float elapsedTime = 0f;
            float exposeStart = Mathf.Min(coreExposeDelay, totalDuration);
            float exposeEnd = Mathf.Min(totalDuration, exposeStart + coreExposeDuration);
            float closeEnd = Mathf.Min(totalDuration, exposeEnd + coreCloseDuration);
            bool exposureStarted = false;
            bool exposureEnded = false;

            while (elapsedTime < totalDuration)
            {
                if (IsDead || CurrentState == DeadState)
                {
                    SetCoreExposed(false);
                    yield break;
                }

                StopMoving();

                if (facePlayerDuringCooldown && Target != null)
                    FacePosition(Target.position);

                if (!exposureStarted && elapsedTime >= exposeStart && exposeEnd > exposeStart)
                {
                    exposureStarted = true;
                    SetCoreExposed(true);
                }

                if (!exposureEnded && elapsedTime >= exposeEnd)
                {
                    exposureEnded = true;
                    SetCoreExposed(false);
                }

                if (elapsedTime >= closeEnd)
                    SetCoreExposed(false);

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            SetCoreExposed(false);
            IsCoolingDown = false;
            cooldownCoroutine = null;

            if (Animator != null)
                Animator.speed = 1f;

            EvaluateStateAfterCooldown();
        }

        private void SetCoreExposed(bool exposed)
        {
            if (IsCoreExposed == exposed)
                return;

            IsCoreExposed = exposed;
            shaderController?.SetCoreExposed(exposed);
        }

        private void TryActivateDesperationAtSafePoint()
        {
            if (IsDesperate || currentPhase != BossPhase.Phase2)
                return;

            float healthRatio = (float)CurrentHealth / Mathf.Max(1, maxHealth);
            if (!pendingDesperation && healthRatio > desperationThreshold)
                return;

            pendingDesperation = false;
            IsDesperate = true;
            shaderController?.SetDesperation(true);
            float pulseRadius = Collider2D != null ? Mathf.Max(2.2f, Collider2D.bounds.extents.x * 1.4f) : 2.2f;
            NidusOrganicSkillVFX.SpawnPulse(CenterPosition, pulseRadius, 0.65f,
                new Color(0.82f, 1.75f, 0.08f, 1f));
        }

        public void EvaluateStateAfterCooldown()
        {
            if (IsDead || CurrentState == DeadState)
                return;

            if (Target == null || !Target.gameObject.activeInHierarchy)
            {
                ChangeState(IdleState);
                return;
            }

            if (HasTargetInMeleeRange())
            {
                ChangeState(ChaseState);
            }
            else if (IsTargetWithinDetectionRange())
            {
                ChangeState(ChaseState);
            }
            else
            {
                ChangeState(IdleState);
            }
        }

        private void ApplyFacing(float direction)
        {
            Vector3 scale = transform.localScale;
            float visualDirection = direction * facingVisualMultiplier;
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(visualDirection);
            transform.localScale = scale;
        }

        public void TakeDamage(int damage)
        {
            if (IsInvulnerable || IsTransitioningPhases || IsDead) return;

            int effectiveDamage = IsCoreExposed && damage > 0
                ? Mathf.Max(1, Mathf.RoundToInt(damage * coreExposeDamageMultiplier))
                : damage;

            HealthChangeResult result = health.ApplyDamage(effectiveDamage);
            if (!result.WasApplied)
            {
                return;
            }

            if (uiEventDispatcher != null) uiEventDispatcher.UpdateHealth(CurrentHealth);

            Debug.Log($"Nidus Cocoon Boss took {result.DamageApplied} damage! Current health: {CurrentHealth}/{maxHealth}");

            global::DamagePopupSystem.ShowEnemy(CenterPosition + Vector3.up, result.DamageApplied);

            // Trigger hit flash shader effect
            shaderController?.TriggerHitFlash();

            if (result.KilledTarget)
            {
                Die();
                return;
            }

            if (currentPhase == BossPhase.Phase2 && !IsDesperate
                && CurrentHealth <= maxHealth * desperationThreshold)
            {
                pendingDesperation = true;
                if (IsCoolingDown)
                    TryActivateDesperationAtSafePoint();
            }

            // Phase 2 transition trigger
            if (currentPhase == BossPhase.Phase1 && CurrentHealth <= maxHealth * phase2Threshold)
            {
                StartCoroutine(TransitionToPhase2Routine());
            }
        }

        [ContextMenu("Force Phase 2")]
        public void ForcePhase2()
        {
            if (IsDead)
            {
                Debug.LogWarning("[NidusCocoonBoss] Cannot force Phase 2: Boss is dead.");
                return;
            }
            if (currentPhase == BossPhase.Phase2)
            {
                Debug.LogWarning("[NidusCocoonBoss] Boss is already in Phase 2.");
                return;
            }
            if (IsTransitioningPhases)
            {
                Debug.LogWarning("[NidusCocoonBoss] Boss is already transitioning to Phase 2.");
                return;
            }

            Debug.Log("[NidusCocoonBoss] Debug trigger: Forcing Phase 2 transition!");
            
            // Adjust health to phase 2 threshold if it is above
            int thresholdHealth = Mathf.FloorToInt(maxHealth * phase2Threshold);
            if (CurrentHealth > thresholdHealth)
            {
                health.SetCurrentHealth(thresholdHealth);
                if (uiEventDispatcher != null) uiEventDispatcher.UpdateHealth(CurrentHealth);
            }

            StartCoroutine(TransitionToPhase2Routine());
        }

        private IEnumerator TransitionToPhase2Routine()
        {
            IsTransitioningPhases = true;
            StopHorizontalMovement();
            IsInvulnerable = true;
            currentPhase = BossPhase.Phase2;
            
            if (uiEventDispatcher != null) uiEventDispatcher.ChangePhase(2);

            CleanupBroodForPhaseTransition();

            Debug.Log("Nidus Cocoon entering Phase 2 Transition!");

            if (phaseTransitionVFXPrefab != null)
            {
                GameObject vfx = Instantiate(phaseTransitionVFXPrefab, CenterPosition, Quaternion.identity);
                NidusPhaseTransitionVFX transitionVFX = vfx.GetComponentInChildren<NidusPhaseTransitionVFX>(true);
                if (transitionVFX != null)
                {
                    transitionVFX.Initialize(this, phaseTransitionDuration);
                    Destroy(vfx, transitionVFX.TotalDuration + 0.15f);
                }
                else
                {
                    Destroy(vfx, phaseTransitionDuration + 1f);
                }
            }

            shaderController?.StartPhaseTransitionPulse(phaseTransitionDuration);

            if (Animator != null)
            {
                Animator.SetTrigger("PhaseTransition");
            }

            // A shaped recoil keeps the cocoon readable while still selling the
            // shell rupture. The transform is restored before control returns.
            float elapsed = 0f;
            Vector3 baseWorldPosition = transform.position;
            while (elapsed < phaseTransitionDuration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, phaseTransitionDuration));
                float anticipation = Mathf.SmoothStep(0.012f, 0.055f, Mathf.Clamp01(normalized / 0.70f));
                float rupture = normalized > 0.70f
                    ? Mathf.Lerp(0.16f, 0f, Mathf.SmoothStep(0f, 1f, (normalized - 0.70f) / 0.30f))
                    : 0f;
                float shakeAmount = Mathf.Max(anticipation, rupture);
                float noiseX = (Mathf.PerlinNoise(Time.time * 32f, 1.7f) - 0.5f) * 2f;
                float noiseY = (Mathf.PerlinNoise(2.3f, Time.time * 37f) - 0.5f) * 2f;
                transform.position = baseWorldPosition + new Vector3(noiseX, noiseY, 0f) * shakeAmount;
                yield return null;
            }
            transform.position = baseWorldPosition;

            IsInvulnerable = false;
            IsTransitioningPhases = false;
            Debug.Log("Nidus Cocoon transitioned to Phase 2!");

            // Immediate burrow attack on phase 2 entry
            ChangeState(BurrowState);
        }

        public Vector3 ResolveGroundPoint(Vector3 desiredPosition, float castHeight = 8f)
        {
            float safeHeight = Mathf.Max(3f, castHeight);
            RaycastHit2D hit = Physics2D.Raycast(
                desiredPosition + Vector3.up * safeHeight,
                Vector2.down,
                safeHeight * 2.5f,
                environmentLayer);

            if (hit.collider != null)
                return new Vector3(hit.point.x, hit.point.y, desiredPosition.z);

            return desiredPosition;
        }

        public void RegisterSeed(NidusParasiteSeed seed)
        {
            if (seed != null && !activeSeeds.Contains(seed))
                activeSeeds.Add(seed);
        }

        public void UnregisterSeed(NidusParasiteSeed seed)
        {
            if (seed != null)
                activeSeeds.Remove(seed);
        }

        public void RegisterMinion(NidusMinion minion)
        {
            if (minion != null && !activeMinions.Contains(minion))
                activeMinions.Add(minion);
        }

        public void UnregisterMinion(NidusMinion minion)
        {
            if (minion != null)
                activeMinions.Remove(minion);
        }

        public void RegisterAcidPool(NidusAcidPool pool)
        {
            if (pool != null && !activeAcidPools.Contains(pool))
                activeAcidPools.Add(pool);
        }

        public void UnregisterAcidPool(NidusAcidPool pool)
        {
            if (pool != null)
                activeAcidPools.Remove(pool);
        }

        public void RegisterRootHazard(NidusRootSpikeHazard hazard)
        {
            if (hazard != null && !activeRootHazards.Contains(hazard))
                activeRootHazards.Add(hazard);
        }

        public void UnregisterRootHazard(NidusRootSpikeHazard hazard)
        {
            if (hazard != null)
                activeRootHazards.Remove(hazard);
        }

        public List<NidusAcidPool> GetBestDetonationPools(int maxCount)
        {
            PruneOwnedCollections();
            List<NidusAcidPool> selected = new List<NidusAcidPool>();
            for (int i = 0; i < activeAcidPools.Count; i++)
            {
                NidusAcidPool pool = activeAcidPools[i];
                if (pool != null && pool.IsAvailableForDetonation)
                    selected.Add(pool);
            }

            Vector3 targetPosition = Target != null ? Target.position : CenterPosition;
            selected.Sort((a, b) =>
                (((Vector2)a.transform.position - (Vector2)targetPosition).sqrMagnitude)
                    .CompareTo(((Vector2)b.transform.position - (Vector2)targetPosition).sqrMagnitude));

            if (selected.Count > maxCount)
                selected.RemoveRange(maxCount, selected.Count - maxCount);
            return selected;
        }

        private void PruneOwnedCollections()
        {
            activeSeeds.RemoveAll(x => x == null);
            activeMinions.RemoveAll(x => x == null);
            activeAcidPools.RemoveAll(x => x == null);
            activeRootHazards.RemoveAll(x => x == null);
        }

        private void CleanupBroodForPhaseTransition()
        {
            PruneOwnedCollections();

            NidusParasiteSeed[] seeds = activeSeeds.ToArray();
            foreach (NidusParasiteSeed seed in seeds)
                if (seed != null) seed.WitherForPhaseTransition(0.45f);

            NidusMinion[] minions = activeMinions.ToArray();
            foreach (NidusMinion minion in minions)
                if (minion != null) minion.DissolveForPhaseTransition(0.48f);
        }

        private void CleanupOwnedCombatObjects()
        {
            PruneOwnedCollections();

            NidusParasiteSeed[] seeds = activeSeeds.ToArray();
            foreach (NidusParasiteSeed seed in seeds)
                if (seed != null) seed.ForceCleanup();

            NidusMinion[] minions = activeMinions.ToArray();
            foreach (NidusMinion minion in minions)
                if (minion != null) minion.DissolveForPhaseTransition(0.28f);

            NidusAcidPool[] pools = activeAcidPools.ToArray();
            foreach (NidusAcidPool pool in pools)
                if (pool != null) pool.ForceCleanup();

            NidusRootSpikeHazard[] hazards = activeRootHazards.ToArray();
            foreach (NidusRootSpikeHazard hazard in hazards)
                if (hazard != null) hazard.ForceCleanup();

            activeSeeds.Clear();
            activeMinions.Clear();
            activeAcidPools.Clear();
            activeRootHazards.Clear();
        }

        public void ShowWarning()
        {
            if (warningObject != null) warningObject.SetActive(true);
        }

        public void HideWarning()
        {
            if (warningObject != null) warningObject.SetActive(false);
        }

        public IEnumerator PlaySkillWarning()
        {
            ShowWarning();
            yield return new WaitForSeconds(Mathf.Max(0.05f, chargeDelay));
            HideWarning();
        }

        private void Die()
        {
            IsDead = true;
            if (uiEventDispatcher != null) uiEventDispatcher.Die();
            SetCoreExposed(false);
            shaderController?.SetDesperation(false);
            ExitCooldownState();
            CleanupOwnedCombatObjects();
            StopAllCoroutines();
            deathRoutine = StartCoroutine(DeathSequenceRoutine());
        }

        private IEnumerator DeathSequenceRoutine()
        {
            StopMoving();
            HideWarning();

            if (Animator != null)
            {
                Animator.SetBool("isWalk", false);
                Animator.SetBool("isIdle", false);
                Animator.speed = 1f;
            }

            Vector3 originalPosition = transform.position;
            float elapsed = 0f;
            float duration = Mathf.Max(0f, deathShakeDuration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float pulse = Mathf.Sin(elapsed * Mathf.Max(0.01f, deathShakeFrequency));
                Vector3 shakeOffset = new Vector3(pulse * deathShakeAmplitude, 0f, 0f);
                transform.position = originalPosition + shakeOffset;
                StopMoving();
                yield return null;
            }

            transform.position = originalPosition;
            
            Vector3 deathSpawnPos = CenterPosition;
            
            ChangeState(DeadState);
            
            if (deathVFXPrefab != null)
            {
                Instantiate(deathVFXPrefab, deathSpawnPos, Quaternion.identity);
            }

            if (deathDropItemPrefab != null)
            {
                GameObject drop = Instantiate(deathDropItemPrefab, deathSpawnPos + deathDropOffset, Quaternion.identity);
                LaunchDeathDrop(drop);
            }

            deathCutsceneController?.PlayDeathCutscene(gameObject);
        }

        private void LaunchDeathDrop(GameObject drop)
        {
            if (drop == null)
            {
                return;
            }

            NidusDeathDropItem dropMarker = drop.GetComponent<NidusDeathDropItem>();
            if (dropMarker == null)
            {
                dropMarker = drop.AddComponent<NidusDeathDropItem>();
            }

            dropMarker.RegisterAsLatest();

            Rigidbody2D dropBody = drop.GetComponent<Rigidbody2D>();
            if (dropBody == null && addDropPhysicsIfMissing)
            {
                dropBody = drop.AddComponent<Rigidbody2D>();
            }

            if (dropBody == null)
            {
                return;
            }

            if (addDropPhysicsIfMissing && drop.GetComponent<Collider2D>() == null)
            {
                CircleCollider2D collider = drop.AddComponent<CircleCollider2D>();
                collider.radius = 0.25f;
                collider.isTrigger = false;
            }

            dropMarker.BeginHoverAtCurrentPosition();
        }

        public void SetRenderersAlpha(float alpha)
        {
            if (spriteRenderers == null) return;
            foreach (var r in spriteRenderers)
            {
                if (r != null)
                {
                    Color col = r.color;
                    col.a = alpha;
                    r.color = col;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(CenterPosition, detectionRange);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(CenterPosition, attackRange);

            Vector2 attackPoint = (Vector2)CenterPosition + Vector2.right * FacingDirection * 0.8f;
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(attackPoint, meleeRadius);
        }
    }
}
