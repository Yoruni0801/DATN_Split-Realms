using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace Bao.Bosses
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(Collider2D))]
    [RequireComponent(typeof(DreadwormDeathCutsceneController))]
    public class DreadwormBoss : MonoBehaviour, Bao.Enemies.IDamageable
    {
        [Header("Boss - Health & Phase")]
        [SerializeField] private int maxHealth = 300;
        [Range(0f, 1f)]
        [SerializeField] private float phase2Threshold = 0.5f;
        [SerializeField] private BossPhase currentPhase = BossPhase.Phase1;
        [SerializeField] private GameObject phaseTransitionVFXPrefab;
        [SerializeField] private GameObject deathVFXPrefab;
        [SerializeField] private float phaseTransitionDuration = 1.5f;

        [Header("Boss - Death Scene Transition")]
        [Tooltip("Điền tên scene/map sẽ load sau khi giết boss. Để trống nếu không muốn chuyển scene.")]
        [SerializeField] private string nextSceneName;
        [SerializeField] private bool useAdvancedLoadingScene = true;
        [SerializeField] private float destroyDelayIfNoNextScene = 3f;

        [Header("Boss - Death Cutscene")]
        [SerializeField] private DreadwormDeathCutsceneController deathCutsceneController;
        [SerializeField, HideInInspector] private GameObject deathCutsceneObject;

        [Header("Boss - Warning")]
        [Tooltip("Kéo object chứa dấu chấm than vào đây")]
        [SerializeField] private GameObject warningObject;

        [Header("Boss - Movement & Detection")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float chaseSpeedMultiplier = 2f;
        [SerializeField] private float detectionRange = 26f;
        [SerializeField] private float attackRange = 7.5f;

        [Header("Boss - Slam (Phase 1 & 2)")]
        [SerializeField] private GameObject slamVFXPrefab;
        [SerializeField] private Vector2 slamVFXOffset = new Vector2(1.5f, 0f);
        [SerializeField] private int slamDamage = 20;
        [SerializeField] private float slamCooldown = 1.2f;
        [SerializeField] private float slamRadius = 3.6f;
        [SerializeField] private float slamDelay = 0.28f;
        [SerializeField] private float slamDuration = 0.52f;
        [SerializeField] private float heavySlamDelay = 0.38f;
        [SerializeField] private float heavySlamRadius = 4.5f;

        [Header("Boss - AI Behavior & Pacing")]
        [SerializeField] private float globalActionCooldown = 0.25f; // Fast action pacing for high-tempo combat
        [SerializeField] private float comboEndRecovery = 0.6f; // Brief punish window for player after a full combo
        [SerializeField] private float optimalSpacingDistance = 3.2f; // Distance boss tries to maintain when spacing
        [SerializeField] private float backingSpeedMultiplier = 0.85f;
        [SerializeField] private float parryMicroStaggerDuration = 0.45f; // Boss dazed duration when player parries

        [Header("Combo Cooldown")]
        [SerializeField, Min(0f)] private float comboCooldown = 0.5f;
        [SerializeField] private bool facePlayerDuringCooldown = true;

        [Header("Boss - Phase 1 Skills")]
        [SerializeField] private float chargeDelay = 0.32f;
        [SerializeField] private float chargeCooldown = 4f;
        [SerializeField] private float chargeSpeed = 34f;
        [SerializeField] private int chargeDamage = 16;
        [SerializeField] private float chargeMaxDuration = 0.55f;
        [SerializeField] private float chargeRange = 16f;
        [SerializeField] private float chargeMinStartDistance = 2.2f;
        [SerializeField] private Vector2 chargeHitboxPadding = new Vector2(0.7f, 0.4f);

        [Header("Boss - Ice Arrows (Phase 2 Skill 1)")]
        [SerializeField] private float iceArrowCooldown = 4f;
        [SerializeField] private GameObject iceArrowPrefab;
        [SerializeField] private int iceArrowCount = 3;
        [SerializeField] private float iceArrowAnimDuration = 0.8f;
        [SerializeField] private float iceArrowChargeTime = 0.45f;
        [SerializeField] private float iceArrowShootInterval = 0.14f;
        [SerializeField] private float iceArrowSpeed = 14f;
        [SerializeField] private int iceArrowDamage = 15;
        [SerializeField] private Vector2 iceArrowSpread = new Vector2(-0.5f, 1.2f); // Distance between arrows when charging (X is curve back, Y is vertical)
        [SerializeField] private Vector2 iceArrowSpawnOffset = new Vector2(-1.5f, 2f); // Offset base for arrow stacking

        [Header("Boss - Electro Spheres (Phase 2 Skill 2)")]
        [SerializeField] private float summonSphereCooldown = 6.5f;
        [SerializeField] private GameObject electroSpherePrefab;
        [SerializeField] private Vector2 summonSphereSpawnOffset = new Vector2(1.5f, 4.5f); // Đỉnh đầu băng
        [SerializeField] private float sweepYOffset = 0.5f; // Vị trí mặt đất để càn quét
        [SerializeField] private float summonSphereChargeTime = 0.65f; // Thời gian gồng
        [SerializeField] private float summonSphereAnimDuration = 1.0f;
        [SerializeField] private float summonSphereSpeed = 4.5f;
        [SerializeField] private int summonSphereDamage = 28;

        [Header("Boss - Crystal Spine Eruption (Phase 2 Skill 3)")]
        [SerializeField] private float spineEruptionCooldown = 5.5f;
        [SerializeField] private int spineEruptionDamage = 18;
        [SerializeField, Range(3, 10)] private int spineEruptionSpikeCount = 6;
        [SerializeField, Min(0.15f)] private float spineEruptionWindup = 0.45f;
        [SerializeField, Min(0.04f)] private float spineEruptionInterval = 0.09f;
        [SerializeField, Min(0.5f)] private float spineEruptionSpacing = 1.65f;
        [SerializeField, Min(0.25f)] private float spineEruptionHitRadius = 0.95f;
        [SerializeField] private Vector2 spineEruptionStartOffset = new Vector2(1.6f, 0.06f);
        [SerializeField, Min(0.05f)] private float spineEruptionRecovery = 0.22f;

        [Header("Boss - Frozen Sanctuary & Glacial Barrage (Phase 2 Skill 4)")]
        [SerializeField] private float frozenSanctuaryCooldown = 11f;
        [SerializeField, Min(0.2f)] private float frozenSanctuaryWindup = 0.8f;
        [SerializeField] private float frozenSanctuaryDuration = 4.2f;
        [SerializeField] private int iceShellMaxHealth = 90;
        [SerializeField, Range(0f, 0.5f)] private float sanctuaryDamageMultiplier = 0.15f; // takes 15% damage (85% reduction)
        [SerializeField] private int glacialBarrageCount = 10;
        [SerializeField] private float glacialBarrageInterval = 0.35f;
        [SerializeField] private float icicleTelegraphDuration = 0.65f;
        [SerializeField] private int icicleDamage = 22;
        [SerializeField] private float icicleTargetPredictionTime = 0.4f;
        [SerializeField] private float icicleRandomArenaRadius = 11f;
        [SerializeField] private float bossStaggerDuration = 2.0f;
        [SerializeField] private Sprite[] icicleSprites;

        [Header("Boss - Crystal Bone Anchors")]
        [Tooltip("Optional. Order these from head/back crystal toward the tail. If empty, the boss auto-finds child transforms whose names contain Crystal/Spike/Thorn/Ice.")]
        [SerializeField] private Transform[] crystalBones;
        [SerializeField] private bool autoFindCrystalBones = true;
        [SerializeField] private string[] crystalBoneNameHints = { "crystal", "spike", "thorn", "ice" };

        [Header("Boss - Debug & Visuals")]
        [SerializeField] private SpriteRenderer[] spriteRenderers;
        [SerializeField] private float facingVisualMultiplier = -1f;
        [SerializeField] private bool testForcePhase2 = false;
        private HealthPool health;

        [Header("Boss - UI")]
        [SerializeField] private BossUIEventDispatcher uiEventDispatcher;
        // [SerializeField] private Bao.Enemies.WorldSpaceHealthBar healthBar;

        public int MaxHealth => maxHealth;
        public int CurrentHealth => health != null ? Mathf.RoundToInt(health.CurrentHealth) : maxHealth;
        public BossPhase CurrentPhase => currentPhase;
        public float MoveSpeed => moveSpeed;
        public float ChaseSpeedMultiplier => chaseSpeedMultiplier;
        
        public Rigidbody2D Rigidbody2D { get; private set; }
        public Animator Animator { get; private set; }
        public Collider2D Collider2D { get; private set; }
        public Bao.Enemies.DreadwormVFXController VFXController { get; private set; }
        public DreadwormBossAudioController AudioController { get; private set; }
        public Transform Target { get; private set; }
        public SpriteRenderer[] SpriteRenderers => spriteRenderers;

        public GameObject SlamVFXPrefab => slamVFXPrefab;
        public Vector2 SlamVFXOffset => slamVFXOffset;
        public int SlamDamage => slamDamage;
        public float SlamCooldown => slamCooldown;
        public float SlamRadius => slamRadius;
        public float SlamDelay => slamDelay;
        public float SlamDuration => slamDuration;
        public float HeavySlamDelay => heavySlamDelay;
        public float HeavySlamRadius => heavySlamRadius;

        public float ChargeDelay => chargeDelay;
        public float ChargeCooldown => chargeCooldown;
        public float ChargeSpeed => chargeSpeed;
        public int ChargeDamage => chargeDamage;
        public float ChargeMaxDuration => chargeMaxDuration;
        public float ChargeRange => chargeRange;
        public float ChargeMinStartDistance => chargeMinStartDistance;
        public float ChargeTravelDistance => chargeSpeed * chargeMaxDuration;
        public Vector2 ChargeHitboxPadding => chargeHitboxPadding;

        public float GlobalActionCooldown => globalActionCooldown;
        public float ComboEndRecovery => comboEndRecovery;
        public float OptimalSpacingDistance => optimalSpacingDistance;
        public float BackingSpeedMultiplier => backingSpeedMultiplier;
        public float ParryMicroStaggerDuration => parryMicroStaggerDuration;
        public float ComboCooldown => comboCooldown;
        public bool FacePlayerDuringCooldown => facePlayerDuringCooldown;

        public float IceArrowCooldown => iceArrowCooldown;
        public GameObject IceArrowPrefab => iceArrowPrefab;
        public int IceArrowCount => iceArrowCount;
        public float IceArrowAnimDuration => iceArrowAnimDuration;
        public float IceArrowChargeTime => iceArrowChargeTime;
        public float IceArrowShootInterval => iceArrowShootInterval;
        public float IceArrowSpeed => iceArrowSpeed;
        public int IceArrowDamage => iceArrowDamage;
        public Vector2 IceArrowSpread => iceArrowSpread;
        public Vector2 IceArrowSpawnOffset => iceArrowSpawnOffset;

        public float SummonSphereCooldown => summonSphereCooldown;
        public GameObject ElectroSpherePrefab => electroSpherePrefab;
        public Vector2 SummonSphereSpawnOffset => summonSphereSpawnOffset;
        public float SweepYOffset => sweepYOffset;
        public float SummonSphereChargeTime => summonSphereChargeTime;
        public float SummonSphereAnimDuration => summonSphereAnimDuration;
        public float SummonSphereSpeed => summonSphereSpeed;
        public int SummonSphereDamage => summonSphereDamage;

        public float SpineEruptionCooldown => spineEruptionCooldown;
        public int SpineEruptionDamage => spineEruptionDamage;
        public int SpineEruptionSpikeCount => Mathf.Max(1, spineEruptionSpikeCount);
        public float SpineEruptionWindup => spineEruptionWindup;
        public float SpineEruptionInterval => spineEruptionInterval;
        public float SpineEruptionSpacing => spineEruptionSpacing;
        public float SpineEruptionHitRadius => spineEruptionHitRadius;
        public float SpineEruptionRecovery => spineEruptionRecovery;
        public float SpineEruptionReach => Mathf.Abs(spineEruptionStartOffset.x) +
                                           Mathf.Max(0, SpineEruptionSpikeCount - 1) * spineEruptionSpacing +
                                           spineEruptionHitRadius;

        public float FrozenSanctuaryCooldown => frozenSanctuaryCooldown;
        public float FrozenSanctuaryWindup => frozenSanctuaryWindup;
        public float FrozenSanctuaryDuration => frozenSanctuaryDuration;
        public int IceShellMaxHealth => iceShellMaxHealth;
        public float SanctuaryDamageMultiplier => sanctuaryDamageMultiplier;
        public int GlacialBarrageCount => glacialBarrageCount;
        public float GlacialBarrageInterval => glacialBarrageInterval;
        public float IcicleTelegraphDuration => icicleTelegraphDuration;
        public int IcicleDamage => icicleDamage;
        public float IcicleTargetPredictionTime => icicleTargetPredictionTime;
        public float IcicleRandomArenaRadius => icicleRandomArenaRadius;
        public float BossStaggerDuration => bossStaggerDuration;
        public Sprite[] IcicleSprites => icicleSprites;

        public int CrystalBoneCount => crystalBones != null ? crystalBones.Length : 0;

        public bool IsInvulnerable { get; set; } = false;
        public bool IsTransitioningPhases { get; private set; } = false;
        public bool IsDead { get; private set; } = false;
        public bool IsCoolingDown { get; private set; } = false;
        public float FacingDirection { get; private set; } = -1f;

        public Vector3 CenterPosition => Collider2D != null ? Collider2D.bounds.center : transform.position;

        public Vector3 GetIceArrowSpawnPosition(int index)
        {
            Bounds bounds = Collider2D != null
                ? Collider2D.bounds
                : new Bounds(transform.position, new Vector3(6f, 2f, 0f));
            return CalculateIceArrowSpawnPosition(bounds, FacingDirection, index);
        }

        public Vector3 GetSphereSpawnPosition()
        {
            return CalculateSphereSpawnPosition(FacingDirection);
        }


        public Vector3 GetSpineEruptionPoint(int index)
        {
            Bounds bounds = Collider2D != null
                ? Collider2D.bounds
                : new Bounds(transform.position, new Vector3(6f, 2f, 0f));

            float facing = Mathf.Approximately(FacingDirection, 0f) ? 1f : Mathf.Sign(FacingDirection);
            int clampedIndex = Mathf.Clamp(index, 0, SpineEruptionSpikeCount - 1);
            float startDistance = bounds.extents.x + Mathf.Abs(spineEruptionStartOffset.x);
            float distance = startDistance + clampedIndex * spineEruptionSpacing;
            float groundY = bounds.min.y + spineEruptionStartOffset.y;

            return new Vector3(
                bounds.center.x + facing * distance,
                groundY,
                transform.position.z);
        }

        public Transform GetCrystalBoneAnchor(int sequenceIndex)
        {
            if (crystalBones == null || crystalBones.Length == 0)
                return null;

            int sourceCount = Mathf.Max(1, SpineEruptionSpikeCount);
            float normalized = sourceCount <= 1 ? 0f : Mathf.Clamp01(sequenceIndex / (sourceCount - 1f));
            int boneIndex = Mathf.RoundToInt(normalized * (crystalBones.Length - 1));
            return crystalBones[Mathf.Clamp(boneIndex, 0, crystalBones.Length - 1)];
        }

        public DreadwormBossState CurrentState { get; private set; }

        public enum SkillCategory { None, Melee, Ranged, Movement, Area }

        private float lastSlamTime = -99f;
        private float lastChargeTime = -99f;
        private float lastIceArrowTime = -99f;
        private float lastSummonSphereTime = -99f;
        private float lastSpineEruptionTime = -99f;
        private float lastFrozenSanctuaryTime = -99f;
        private float lastActionTime = -99f;
        private Coroutine cooldownCoroutine;

        public SkillCategory LastUsedSkillCategory { get; private set; } = SkillCategory.None;

        public DreadwormIdleState IdleState { get; private set; }
        public DreadwormChaseState ChaseState { get; private set; }
        public DreadwormSlamState SlamState { get; private set; }
        public DreadwormChargeState ChargeState { get; private set; }
        public DreadwormIceArrowState IceArrowState { get; private set; }
        public DreadwormSummonSphereState SummonSphereState { get; private set; }
        public DreadwormSpineEruptionState SpineEruptionState { get; private set; }
        public DreadwormFrozenSanctuaryState FrozenSanctuaryState { get; private set; }
        public DreadwormStaggerState StaggerState { get; private set; }
        public DreadwormCooldownState CooldownState { get; private set; }
        public DreadwormDeadState DeadState { get; private set; }

        public void TriggerParryStagger()
        {
            if (IsDead || IsTransitioningPhases) return;
            AudioController?.PlayParriedStagger();
            ChangeState(StaggerState);
        }

        private void Awake()
        {
            Rigidbody2D = GetComponent<Rigidbody2D>();
            Animator = GetComponent<Animator>();
            Collider2D = GetComponent<Collider2D>();
            VFXController = GetComponent<Bao.Enemies.DreadwormVFXController>();
            AudioController = GetComponent<DreadwormBossAudioController>();

            Rigidbody2D.freezeRotation = true;
            health = new HealthPool(maxHealth);

            // Always re-fetch and filter in case the array was saved in the Inspector
            var allRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            var validRenderers = new List<SpriteRenderer>();
            foreach (var r in allRenderers)
            {
                if (warningObject != null && r.transform.IsChildOf(warningObject.transform))
                    continue;
                validRenderers.Add(r);
            }
            spriteRenderers = validRenderers.ToArray();
            ResolveCrystalBones();

            IdleState = new DreadwormIdleState(this);
            ChaseState = new DreadwormChaseState(this);
            SlamState = new DreadwormSlamState(this);
            ChargeState = new DreadwormChargeState(this);
            IceArrowState = new DreadwormIceArrowState(this);
            SummonSphereState = new DreadwormSummonSphereState(this);
            SpineEruptionState = new DreadwormSpineEruptionState(this);
            FrozenSanctuaryState = new DreadwormFrozenSanctuaryState(this);
            StaggerState = new DreadwormStaggerState(this);
            CooldownState = new DreadwormCooldownState(this);
            DeadState = new DreadwormDeadState(this);

            if (warningObject != null)
            {
                warningObject.SetActive(false);
            }

            // if (healthBar != null)
            // {
            //     healthBar.gameObject.SetActive(false);
            // }

            if (deathCutsceneController == null)
                deathCutsceneController = GetComponent<DreadwormDeathCutsceneController>();
            deathCutsceneController?.ConfigureLegacyReferences(deathCutsceneObject, destroyDelayIfNoNextScene);
        }

        private void Start()
        {
            if (uiEventDispatcher == null) uiEventDispatcher = GetComponent<BossUIEventDispatcher>();
            if (uiEventDispatcher != null) uiEventDispatcher.Initialize("Dreadworm", maxHealth);

            FindPlayer();
            ApplyFacing(FacingDirection);
            ChangeState(IdleState);
        }

        private void Update()
        {
            if (IsDead) return;

            if (Target == null) FindPlayer();

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

        public void ChangeState(DreadwormBossState newState)
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
            
            // Once the boss spots the player, it never drops aggro
            if (hasEngaged) return true;

            if (Vector2.Distance(CenterPosition, Target.position) <= detectionRange)
            {
                hasEngaged = true;
                if (uiEventDispatcher != null) uiEventDispatcher.Engage();
                AudioController?.PlayEngageRoar();
                return true;
            }
            
            // If we are in phase 2 or hit, we should also engage immediately
            if (currentPhase == BossPhase.Phase2 || CurrentHealth < maxHealth)
            {
                hasEngaged = true;
                if (uiEventDispatcher != null) uiEventDispatcher.Engage();
                AudioController?.PlayEngageRoar();
                return true;
            }

            return false;
        }

        public void ShowWarning()
        {
            if (warningObject != null) warningObject.SetActive(true);
        }

        public void HideWarning()
        {
            if (warningObject != null) warningObject.SetActive(false);
        }

        public bool HasTargetInAttackRange()
        {
            if (Target == null) return false;
            return Vector2.Distance(CenterPosition, Target.position) <= attackRange;
        }

        public bool IsTargetWithinDetectionRange()
        {
            if (Target == null) return false;
            return Vector2.Distance(CenterPosition, Target.position) <= detectionRange;
        }

        public bool CanPerformAction() => Time.time >= lastActionTime + globalActionCooldown;
        public void MarkActionUsed(SkillCategory category) 
        { 
            lastActionTime = Time.time; 
            LastUsedSkillCategory = category;
        }

        public bool CanSlam() => Time.time >= lastSlamTime + slamCooldown && !IsTransitioningPhases && CanPerformAction();
        public void MarkSlamUsed() { lastSlamTime = Time.time; MarkActionUsed(SkillCategory.Melee); }

        public bool CanCharge() => Time.time >= lastChargeTime + chargeCooldown && !IsTransitioningPhases && CanPerformAction();
        public void MarkChargeUsed() { lastChargeTime = Time.time; MarkActionUsed(SkillCategory.Movement); }

        public bool CanIceArrow() => currentPhase == BossPhase.Phase2 && Time.time >= lastIceArrowTime + iceArrowCooldown && !IsTransitioningPhases && CanPerformAction();
        public void MarkIceArrowUsed() { lastIceArrowTime = Time.time; MarkActionUsed(SkillCategory.Ranged); }

        public bool CanSummonSphere() => currentPhase == BossPhase.Phase2 && Time.time >= lastSummonSphereTime + summonSphereCooldown && !IsTransitioningPhases && CanPerformAction();
        public void MarkSummonSphereUsed() { lastSummonSphereTime = Time.time; MarkActionUsed(SkillCategory.Ranged); }


        public bool CanSpineEruption() => currentPhase == BossPhase.Phase2 && Time.time >= lastSpineEruptionTime + spineEruptionCooldown && !IsTransitioningPhases && CanPerformAction();
        public void MarkSpineEruptionUsed() { lastSpineEruptionTime = Time.time; MarkActionUsed(SkillCategory.Area); }

        public bool CanFrozenSanctuary() => currentPhase == BossPhase.Phase2 && Time.time >= lastFrozenSanctuaryTime + frozenSanctuaryCooldown && !IsTransitioningPhases && CanPerformAction();
        public void MarkFrozenSanctuaryUsed() { lastFrozenSanctuaryTime = Time.time; MarkActionUsed(SkillCategory.Area); }

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

        public void MoveForward(float speedMult)
        {
            Rigidbody2D.linearVelocity = new Vector2(FacingDirection * moveSpeed * speedMult, Rigidbody2D.linearVelocity.y);
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

            if (Animator != null)
            {
                Animator.SetBool("isWalk", false);
                Animator.SetBool("isIdle", true);
                Animator.speed = 0.62f;
            }

            IsCoolingDown = true;

            if (cooldownCoroutine != null)
            {
                StopCoroutine(cooldownCoroutine);
            }

            cooldownCoroutine = StartCoroutine(CooldownRoutine());
        }

        public void ExitCooldownState()
        {
            IsCoolingDown = false;

            if (cooldownCoroutine != null)
            {
                StopCoroutine(cooldownCoroutine);
                cooldownCoroutine = null;
            }

            if (Animator != null)
            {
                Animator.speed = 1f;
            }
        }

        private IEnumerator CooldownRoutine()
        {
            float elapsedTime = 0f;

            while (elapsedTime < comboCooldown)
            {
                if (IsDead || CurrentState == DeadState)
                {
                    yield break;
                }

                StopMoving();

                if (facePlayerDuringCooldown && Target != null)
                {
                    FacePosition(Target.position);
                }

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            IsCoolingDown = false;
            cooldownCoroutine = null;

            if (Animator != null)
            {
                Animator.speed = 1f;
            }

            EvaluateStateAfterCooldown();
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

            if (HasTargetInAttackRange())
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

        private void ResolveCrystalBones()
        {
            if (!autoFindCrystalBones || (crystalBones != null && crystalBones.Length > 0))
                return;

            Transform[] allTransforms = GetComponentsInChildren<Transform>(true);
            List<Transform> found = new List<Transform>();

            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform candidate = allTransforms[i];
                if (candidate == null || candidate == transform)
                    continue;

                string lowerName = candidate.name.ToLowerInvariant();
                bool matches = false;
                if (crystalBoneNameHints != null)
                {
                    for (int h = 0; h < crystalBoneNameHints.Length; h++)
                    {
                        string hint = crystalBoneNameHints[h];
                        if (!string.IsNullOrWhiteSpace(hint) && lowerName.Contains(hint.Trim().ToLowerInvariant()))
                        {
                            matches = true;
                            break;
                        }
                    }
                }

                if (matches)
                    found.Add(candidate);
            }

            // Inspector order is preferred. Auto-found anchors are sorted by local X only as a safe fallback.
            found.Sort((a, b) => a.localPosition.x.CompareTo(b.localPosition.x));
            crystalBones = found.ToArray();
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

            int effectiveDamage = damage;
            if (CurrentState is DreadwormFrozenSanctuaryState sanctuaryState)
            {
                // Heavy damage reduction during Ice Cocoon state (takes only ~15% damage)
                effectiveDamage = Mathf.Max(1, Mathf.RoundToInt(damage * sanctuaryDamageMultiplier));
                // Normal direct attacks also slowly chip away at the Ice Shell
                sanctuaryState.ApplyDirectAttackShellDamage(damage);
            }

            HealthChangeResult result = health.ApplyDamage(effectiveDamage);
            if (!result.WasApplied)
            {
                return;
            }

            Debug.Log($"Dreadworm Boss took {effectiveDamage} damage! Current health: {CurrentHealth}/{maxHealth}");
            if (uiEventDispatcher != null) uiEventDispatcher.UpdateHealth(CurrentHealth);

            global::DamagePopupSystem.ShowEnemy(CenterPosition + Vector3.up, result.DamageApplied);
            AudioController?.PlayHurt();
            if (VFXController != null)
                VFXController.TriggerHitFlash();
            else
                StartCoroutine(HitFlashRoutine());

            if (result.KilledTarget)
            {
                Die();
                return;
            }

            if (currentPhase == BossPhase.Phase1 && CurrentHealth <= maxHealth * phase2Threshold)
            {
                StartCoroutine(TransitionToPhase2Routine());
            }
        }

        private IEnumerator HitFlashRoutine()
        {
            foreach (var r in spriteRenderers) if (r != null) r.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            foreach (var r in spriteRenderers) if (r != null) r.color = Color.white;
        }

        [ContextMenu("Force Phase 2")]
        public void ForcePhase2()
        {
            if (IsDead || currentPhase == BossPhase.Phase2 || IsTransitioningPhases) return;
            health.SetCurrentHealth(Mathf.FloorToInt(maxHealth * phase2Threshold));
            if (uiEventDispatcher != null) uiEventDispatcher.UpdateHealth(CurrentHealth);
            StartCoroutine(TransitionToPhase2Routine());
        }

        [ContextMenu("Take 50 Damage (Debug)")]
        public void DebugTakeDamage()
        {
            TakeDamage(50);
        }



        public void CreateChargeGhost()
        {
            VFXController?.SpawnChargeAfterimage();
        }

        private IEnumerator TransitionToPhase2Routine()
        {
            IsTransitioningPhases = true;
            StopHorizontalMovement();
            IsInvulnerable = true;
            currentPhase = BossPhase.Phase2;
            if (uiEventDispatcher != null) uiEventDispatcher.ChangePhase(2);
            AudioController?.PlayPhaseTransition();
            VFXController?.BeginPhaseTransition(phaseTransitionDuration);

            if (phaseTransitionVFXPrefab != null)
            {
                GameObject vfx = Instantiate(phaseTransitionVFXPrefab, CenterPosition, Quaternion.identity);
                DreadwormPhaseTransitionVFX phaseVFX = vfx.GetComponent<DreadwormPhaseTransitionVFX>();
                if (phaseVFX != null)
                    phaseVFX.Initialize(this, phaseTransitionDuration);
                Destroy(vfx, phaseTransitionDuration + 1.2f);
            }

            if (Animator != null)
            {
                Animator.SetBool("isWalk", false);
                Animator.SetBool("isIdle", true);
            }

            yield return new WaitForSeconds(phaseTransitionDuration);

            IsInvulnerable = false;
            IsTransitioningPhases = false;
        }

        private void Die()
        {
            IsDead = true;
            if (uiEventDispatcher != null) uiEventDispatcher.Die();
            AudioController?.PlayDeath();
            ExitCooldownState();
            StopMoving();

            Vector3 deathSpawnPos = CenterPosition;

            ChangeState(DeadState);

            if (deathVFXPrefab != null)
            {
                Instantiate(deathVFXPrefab, deathSpawnPos, Quaternion.identity);
            }

            deathCutsceneController?.PlayDeathCutscene(gameObject);

            LoadNextSceneOrDestroy();
        }

        private void LoadNextSceneOrDestroy()
        {
            if (!string.IsNullOrWhiteSpace(nextSceneName))
            {
                string sceneName = nextSceneName.Trim();
                if (useAdvancedLoadingScene)
                {
                    Tien.AdvancedLoading.Core.SceneLoader.Load(sceneName);
                }
                else
                {
                    SceneManager.LoadScene(sceneName);
                }

                return;
            }

            Destroy(gameObject, Mathf.Max(0f, destroyDelayIfNoNextScene));
        }

        private Vector3 CalculateIceArrowSpawnPosition(Bounds bounds, float direction, int index)
        {
            float facing = Mathf.Approximately(direction, 0f) ? 1f : Mathf.Sign(direction);
            float horizontalOffset = Mathf.Clamp(
                Mathf.Abs(iceArrowSpawnOffset.x),
                bounds.extents.x * 0.18f,
                bounds.extents.x * 0.48f);
            float verticalOffset = Mathf.Clamp(
                Mathf.Abs(iceArrowSpawnOffset.y),
                bounds.extents.y + 0.75f,
                bounds.extents.y + 1.6f);

            int count = Mathf.Max(1, iceArrowCount);
            float centeredIndex = Mathf.Clamp(index, 0, count - 1) - (count - 1f) * 0.5f;
            float verticalStep = Mathf.Clamp(Mathf.Abs(iceArrowSpread.y), 0.82f, 1.15f);
            float arcBackStep = Mathf.Clamp(Mathf.Abs(iceArrowSpread.x), 0.38f, 0.68f);

            Vector3 origin = bounds.center +
                             Vector3.right * facing * horizontalOffset +
                             Vector3.up * verticalOffset;
            return origin +
                   Vector3.up * centeredIndex * verticalStep -
                   Vector3.right * facing * Mathf.Abs(centeredIndex) * arcBackStep;
        }

        private Vector3 CalculateSphereSpawnPosition(float direction)
        {
            float facing = Mathf.Approximately(direction, 0f) ? 1f : Mathf.Sign(direction);

            return transform.position +
                   Vector3.right * facing * summonSphereSpawnOffset.x +
                   Vector3.up * summonSphereSpawnOffset.y;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = transform.position;
            var col = GetComponent<Collider2D>();
            if (col != null)
            {
                center = col.bounds.center;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(center, detectionRange);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(center, attackRange);

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(center, chargeRange);

            float direction = Application.isPlaying ? FacingDirection : Mathf.Sign(transform.localScale.x * facingVisualMultiplier);
            if (direction == 0) direction = 1f;
            Vector3 attackPoint = center + Vector3.right * direction * 1.5f;
            Gizmos.color = new Color(1f, 0.5f, 0f); // Orange
            Gizmos.DrawWireSphere(attackPoint, slamRadius);

            // Draw VFX Spawn Point
            Vector3 vfxSpawnPos = center + new Vector3(slamVFXOffset.x * direction, slamVFXOffset.y, 0f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(vfxSpawnPos, new Vector3(0.5f, 0.5f, 0.5f));

            // Draw Electro Sphere Head Spawn Point
            Bounds previewBounds = col != null
                ? col.bounds
                : new Bounds(transform.position, new Vector3(6f, 2f, 0f));
            Vector3 headSpawnPos = CalculateSphereSpawnPosition(direction);
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(headSpawnPos, 0.5f);

            // Draw the complete clamped ice-arrow formation.
            Gizmos.color = Color.white;
            for (int i = 0; i < Mathf.Max(1, iceArrowCount); i++)
            {
                Vector3 iceArrowPos = CalculateIceArrowSpawnPosition(previewBounds, direction, i);
                Gizmos.DrawWireCube(iceArrowPos, new Vector3(0.4f, 0.4f, 0.4f));
            }

            // Draw Ground Sweep Target
            Vector3 groundTargetPos = new Vector3(headSpawnPos.x, transform.position.y + sweepYOffset, transform.position.z);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(groundTargetPos, 0.3f);
            Gizmos.DrawLine(headSpawnPos, groundTargetPos);


            // Draw Crystal Spine Eruption chain.
            Gizmos.color = new Color(0.25f, 0.95f, 1f, 0.9f);
            float facing = Mathf.Approximately(direction, 0f) ? 1f : Mathf.Sign(direction);
            float startDistance = previewBounds.extents.x + Mathf.Abs(spineEruptionStartOffset.x);
            float eruptionGroundY = previewBounds.min.y + spineEruptionStartOffset.y;
            for (int i = 0; i < Mathf.Max(1, spineEruptionSpikeCount); i++)
            {
                Vector3 point = new Vector3(
                    previewBounds.center.x + facing * (startDistance + i * spineEruptionSpacing),
                    eruptionGroundY,
                    transform.position.z);
                Gizmos.DrawWireSphere(point, spineEruptionHitRadius);
                if (i > 0)
                {
                    Vector3 previous = point - Vector3.right * facing * spineEruptionSpacing;
                    Gizmos.DrawLine(previous, point);
                }
            }
        }
    }
}
