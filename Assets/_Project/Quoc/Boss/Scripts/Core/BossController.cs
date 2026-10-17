using System;
using System.Collections.Generic;
using UnityEngine;
using Spine.Unity;
using Spine;

namespace Quoc.Boss
{
    [RequireComponent(typeof(BossMovement))]
    [RequireComponent(typeof(BossAttackSelector))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class BossController : MonoBehaviour
    {
        [Header("Cutscene & Aggro Control")]
        [SerializeField, Tooltip("Bật/tắt khả năng tìm mục tiêu và tấn công của Boss (sẽ bị khóa/tắt khi cutscene đang chạy)")]
        private bool isAggroEnabled = true;

        [Header("Health Component (Optional)")]
        [SerializeField, Tooltip("Component BossHealth tùy chọn (nếu không gán tự động tìm hoặc để trống)")]
        private BossHealth health;

        [Header("Target & Bounds")]
        [SerializeField, Tooltip("Transform của Player")]
        private Transform player;

        [SerializeField, Tooltip("Điểm giới hạn lề trái arena")]
        private Transform leftArenaBound;

        [SerializeField, Tooltip("Điểm giới hạn lề phải arena")]
        private Transform rightArenaBound;

        [Header("Phase Model Swapping")]
        [SerializeField, Tooltip("Mảng các GameObject chứa Spine của Boss ứng với từng Phase (Index 0 = Phase 1, Index 1 = Phase 2)")]
        private GameObject[] phaseSpineModels;

        [Header("Spine Animation System (General)")]
        [SerializeField, Tooltip("Component SkeletonAnimation của Spine (nếu để trống tự động tìm)")]
        private SkeletonAnimation skeletonAnimation;
        [Header("Basic Spine Animations")]
        [SpineAnimation(dataField = "skeletonAnimation"), Tooltip("Spine Animation khi đứng chờ (Idle)")]
        [SerializeField] private string idleAnimName = "Idle";
        [Range(0.1f, 5.0f), Tooltip("Tốc độ animation Idle (1.0 = bình thường, 0.5 = chậm một nửa, 2.0 = nhanh gấp đôi)")]
        [SerializeField] private float idleAnimSpeed = 1.0f;

        [SpineAnimation(dataField = "skeletonAnimation"), Tooltip("Spine Animation khi chạy tiếp cận (Run / Walk)")]
        [SerializeField] private string moveAnimName = "Run";
        [Range(0.1f, 5.0f), Tooltip("Tốc độ animation Di chuyển (Move)")]
        [SerializeField] private float moveAnimSpeed = 1.0f;

        [SpineAnimation(dataField = "skeletonAnimation"), Tooltip("Spine Animation khi gồng báo hiệu (Telegraph)")]
        [SerializeField] private string telegraphAnimName = "Skill";
        [Range(0.1f, 5.0f), Tooltip("Tốc độ animation Telegraph")]
        [SerializeField] private float telegraphAnimSpeed = 1.0f;

        [SpineAnimation(dataField = "skeletonAnimation"), Tooltip("Spine Animation khi thu chiêu (Recovery)")]
        [SerializeField] private string recoveryAnimName = "Idle";
        [Range(0.1f, 5.0f), Tooltip("Tốc độ animation Recovery")]
        [SerializeField] private float recoveryAnimSpeed = 1.0f;

        [SpineAnimation(dataField = "skeletonAnimation"), Tooltip("Spine Animation khi chuyển Phase")]
        [SerializeField] private string phaseTransitionAnimName = "Skill";
        [Range(0.1f, 5.0f), Tooltip("Tốc độ animation Chuyển Phase")]
        [SerializeField] private float phaseTransitionAnimSpeed = 1.0f;

        [SpineAnimation(dataField = "skeletonAnimation"), Tooltip("Spine Animation khi chết")]
        [SerializeField] private string dieAnimName = "Death";
        [Range(0.1f, 5.0f), Tooltip("Tốc độ animation Chết (Die)")]
        [SerializeField] private float dieAnimSpeed = 1.0f;

        [Header("Skill Animations Configuration (Chỉnh Animation & Tốc độ cho từng chiêu)")]
        [SpineAnimation(dataField = "skeletonAnimation"), Tooltip("Spine Animation cho đòn Nhảy Giậm Đất (Jump Slam)")]
        [SerializeField] private string jumpSlamAnimName = "Skill";
        [Range(0.1f, 5.0f), Tooltip("Tốc độ animation Nhảy Giậm Đất (Jump Slam)")]
        [SerializeField] private float jumpSlamAnimSpeed = 1.0f;

        [SpineAnimation(dataField = "skeletonAnimation"), Tooltip("Spine Animation cho Cú Chém 1 (Triple Slash - Bước 1)")]
        [SerializeField] private string slash1AnimName = "Attack";
        [Range(0.1f, 5.0f), Tooltip("Tốc độ animation Cú Chém 1")]
        [SerializeField] private float slash1AnimSpeed = 1.0f;

        [SpineAnimation(dataField = "skeletonAnimation"), Tooltip("Spine Animation cho Cú Chém 2 (Triple Slash - Bước 2)")]
        [SerializeField] private string slash2AnimName = "Skill";
        [Range(0.1f, 5.0f), Tooltip("Tốc độ animation Cú Chém 2")]
        [SerializeField] private float slash2AnimSpeed = 1.0f;

        [SpineAnimation(dataField = "skeletonAnimation"), Tooltip("Spine Animation cho Cú Chém 3 (Triple Slash - Bước 3)")]
        [SerializeField] private string slash3AnimName = "Attack";
        [Range(0.1f, 5.0f), Tooltip("Tốc độ animation Cú Chém 3")]
        [SerializeField] private float slash3AnimSpeed = 1.0f;

        [SpineAnimation(dataField = "skeletonAnimation"), Tooltip("Spine Animation cho đòn Chém Đơn (Single Slash)")]
        [SerializeField] private string singleSlashAnimName = "Attack";
        [Range(0.1f, 5.0f), Tooltip("Tốc độ animation Chém Đơn (Single Slash)")]
        [SerializeField] private float singleSlashAnimSpeed = 1.0f;

        [SpineAnimation(dataField = "skeletonAnimation"), Tooltip("Spine Animation cho đòn Húc Lao Tới (Charge)")]
        [SerializeField] private string chargeAnimName = "Run";
        [Range(0.1f, 5.0f), Tooltip("Tốc độ animation Húc Lao Tới (Charge)")]
        [SerializeField] private float chargeAnimSpeed = 1.0f;

        [SpineAnimation(dataField = "skeletonAnimation"), Tooltip("Spine Animation cho đòn Giậm Đất (Ground Slam)")]
        [SerializeField] private string groundSlamAnimName = "Skill";
        [Range(0.1f, 5.0f), Tooltip("Tốc độ animation Giậm Đất (Ground Slam)")]
        [SerializeField] private float groundSlamAnimSpeed = 1.0f;

        [SpineAnimation(dataField = "skeletonAnimation"), Tooltip("Spine Animation cho đòn Siêu Giậm Đất (Super Ground Slam)")]
        [SerializeField] private string superGroundSlamAnimName = "Skill";
        [Range(0.1f, 5.0f), Tooltip("Tốc độ animation Siêu Giậm Đất (Super Ground Slam)")]
        [SerializeField] private float superGroundSlamAnimSpeed = 1.0f;

        [SpineAnimation(dataField = "skeletonAnimation"), Tooltip("Spine Animation cho đòn Bắn Đạn Tầm Xa (Ranged Attack)")]
        [SerializeField] private string rangedAnimName = "Skill";
        [Range(0.1f, 5.0f), Tooltip("Tốc độ animation Bắn Đạn Tầm Xa (Ranged Attack)")]
        [SerializeField] private float rangedAnimSpeed = 1.0f;

        [Header("Unity Animator Fallback")]
        [SerializeField, Tooltip("Animator của Boss (dành cho game 2D thông thường nếu không dùng Spine)")]
        private Animator animator;

        [SerializeField, Tooltip("SpriteRenderer của Boss")]
        private SpriteRenderer spriteRenderer;

        [SerializeField, Tooltip("Collider của Boss")]
        private Collider2D bossCollider;

        [SerializeField, Tooltip("GameObject hiệu ứng khi Telegraph")]
        private GameObject telegraphEffect;

        [SerializeField, Tooltip("GameObject hiệu ứng khi Chuyển Phase")]
        private GameObject phaseTransitionEffect;

        [Header("Attack Spawn Points")]
        [SerializeField, Tooltip("Điểm xuất phát đòn chém cận chiến")]
        private Transform meleePoint;

        [SerializeField, Tooltip("Điểm bắn đạn tầm xa")]
        private Transform rangedPoint;

        [SerializeField, Tooltip("Điểm giậm đất (chân boss)")]
        private Transform groundPoint;

        [Header("Prefabs")]
        [SerializeField, Tooltip("Prefab sóng thần / Shockwave khi giậm đất")]
        private GameObject shockwavePrefab;

        [SerializeField, Tooltip("Prefab đạn bắn tầm xa")]
        private GameObject projectilePrefab;

        public bool IsAggroEnabled => isAggroEnabled;
        public bool IsCutsceneLocked => !isAggroEnabled;
        public Transform Player => isAggroEnabled ? ResolvePlayer() : null;
        public Transform RawPlayer => ResolvePlayer();
        public SkeletonAnimation SkeletonAnimation => skeletonAnimation;
        public Animator Animator => animator;
        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public BossHealth Health => health;
        public bool IsDead => health != null && health.IsDead;
        public BossPhase CurrentPhase => health != null ? health.CurrentPhase : BossPhase.Phase1;
        public BossMovement Movement { get; private set; }
        public BossAttackSelector AttackSelector { get; private set; }
        public BossStateMachine StateMachine { get; private set; }

        public string JumpSlamAnimName => jumpSlamAnimName;
        public string Slash1AnimName => slash1AnimName;
        public string Slash2AnimName => slash2AnimName;
        public string Slash3AnimName => slash3AnimName;
        public string SingleSlashAnimName => singleSlashAnimName;
        public string ChargeAnimName => chargeAnimName;
        public string GroundSlamAnimName => groundSlamAnimName;
        public string SuperGroundSlamAnimName => superGroundSlamAnimName;
        public string RangedAnimName => rangedAnimName;

        public float IdleAnimSpeed => idleAnimSpeed > 0f ? idleAnimSpeed : 1.0f;
        public float MoveAnimSpeed => moveAnimSpeed > 0f ? moveAnimSpeed : 1.0f;
        public float TelegraphAnimSpeed => telegraphAnimSpeed > 0f ? telegraphAnimSpeed : 1.0f;
        public float RecoveryAnimSpeed => recoveryAnimSpeed > 0f ? recoveryAnimSpeed : 1.0f;
        public float PhaseTransitionAnimSpeed => phaseTransitionAnimSpeed > 0f ? phaseTransitionAnimSpeed : 1.0f;
        public float DieAnimSpeed => dieAnimSpeed > 0f ? dieAnimSpeed : 1.0f;

        public float JumpSlamAnimSpeed => jumpSlamAnimSpeed > 0f ? jumpSlamAnimSpeed : 1.0f;
        public float Slash1AnimSpeed => slash1AnimSpeed > 0f ? slash1AnimSpeed : 1.0f;
        public float Slash2AnimSpeed => slash2AnimSpeed > 0f ? slash2AnimSpeed : 1.0f;
        public float Slash3AnimSpeed => slash3AnimSpeed > 0f ? slash3AnimSpeed : 1.0f;
        public float SingleSlashAnimSpeed => singleSlashAnimSpeed > 0f ? singleSlashAnimSpeed : 1.0f;
        public float ChargeAnimSpeed => chargeAnimSpeed > 0f ? chargeAnimSpeed : 1.0f;
        public float GroundSlamAnimSpeed => groundSlamAnimSpeed > 0f ? groundSlamAnimSpeed : 1.0f;
        public float SuperGroundSlamAnimSpeed => superGroundSlamAnimSpeed > 0f ? superGroundSlamAnimSpeed : 1.0f;
        public float RangedAnimSpeed => rangedAnimSpeed > 0f ? rangedAnimSpeed : 1.0f;

        public Transform MeleePoint => meleePoint != null ? meleePoint : transform;
        public Transform RangedPoint => rangedPoint != null ? rangedPoint : transform;
        public Transform GroundPoint => groundPoint != null ? groundPoint : transform;

        public GameObject ShockwavePrefab => shockwavePrefab;
        public GameObject ProjectilePrefab => projectilePrefab;
        public GameObject TelegraphEffect => telegraphEffect;
        public GameObject PhaseTransitionEffect => phaseTransitionEffect;

        public BossAttackData CurrentAttackData { get; private set; }
        private BossAttackBase currentAttackComponent;
        private Dictionary<string, BossAttackBase> attackMap = new Dictionary<string, BossAttackBase>();

        public BossIdleState IdleState { get; private set; }
        public BossMoveState MoveState { get; private set; }
        public BossTelegraphState TelegraphState { get; private set; }
        public BossAttackState AttackState { get; private set; }
        public BossRecoveryState RecoveryState { get; private set; }
        public BossPhaseTransitionState PhaseTransitionState { get; private set; }
        public BossDeadState DeadState { get; private set; }

        private void Awake()
        {
            if (health == null) health = GetComponent<BossHealth>();
            if (health == null) health = GetComponentInChildren<BossHealth>();

            Movement = GetComponent<BossMovement>();
            AttackSelector = GetComponent<BossAttackSelector>();

            if (AttackSelector != null)
            {
                AttackSelector.UpdateAttackAnimations(jumpSlamAnimName, slash1AnimName, chargeAnimName);
            }

            if (skeletonAnimation == null) skeletonAnimation = GetComponent<SkeletonAnimation>();
            if (skeletonAnimation == null) skeletonAnimation = GetComponentInChildren<SkeletonAnimation>();

            if (animator == null) animator = GetComponent<Animator>();
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (bossCollider == null) bossCollider = GetComponent<Collider2D>();

            ResolvePlayer();

            Movement.Initialize(this);
            Movement.SetupBounds(leftArenaBound, rightArenaBound);

            RegisterAttackComponents();
            UpdatePhaseModel();
            InitializeStateMachine();
        }

        private void OnEnable()
        {
            isAggroEnabled = true;
            ResolvePlayer(true);

            if (Movement == null) Movement = GetComponent<BossMovement>();
            if (Movement != null)
            {
                Movement.Initialize(this);
                Movement.SetupBounds(leftArenaBound, rightArenaBound);
                Movement.StopMovement();
            }

            if (AttackSelector == null) AttackSelector = GetComponent<BossAttackSelector>();
            if (AttackSelector != null)
            {
                AttackSelector.UpdateAttackAnimations(jumpSlamAnimName, slash1AnimName, chargeAnimName);
                AttackSelector.EnsureDefaultAttacksIfEmpty();
                AttackSelector.ResetFlow();
            }

            // Dọn sạch toàn bộ các đòn đánh cũ đang dở dang / dừng coroutine
            CleanupAllAttacks();

            if (StateMachine == null)
            {
                RegisterAttackComponents();
                InitializeStateMachine();
            }

            // LUÔN RESET STATE VỀ IDLE STATE KHI SCRIPT ĐƯỢC BẬT LẠI
            if (StateMachine != null && IdleState != null && !IsDead)
            {
                StateMachine.CurrentState?.Exit();
                StateMachine.Initialize(IdleState);
            }

            PlayIdleAnimation();
        }

        private void OnDisable()
        {
            CleanupAllAttacks();
            if (Movement != null)
            {
                Movement.StopMovement();
            }
        }

        public void CleanupAllAttacks()
        {
            CurrentAttackData = null;
            currentAttackComponent = null;

            BossAttackBase[] attacks = GetComponents<BossAttackBase>();
            for (int i = 0; i < attacks.Length; i++)
            {
                if (attacks[i] != null)
                {
                    attacks[i].Cleanup();
                }
            }

            if (telegraphEffect != null)
            {
                telegraphEffect.SetActive(false);
            }
        }

        private void OnValidate()
        {
            if (AttackSelector == null) AttackSelector = GetComponent<BossAttackSelector>();
            if (AttackSelector != null)
            {
                AttackSelector.UpdateAttackAnimations(jumpSlamAnimName, slash1AnimName, chargeAnimName);
            }
        }

        public Transform ResolvePlayer(bool forceRefresh = false)
        {
            if (!forceRefresh && player != null && player.gameObject != null)
            {
                return player;
            }

            // 1. Tìm theo Component Player
            Player p = FindFirstObjectByType<Player>(FindObjectsInactive.Include);
            if (p != null)
            {
                player = p.transform;
                return player;
            }

            // 2. Tìm theo PlayerHealth
            PlayerHealth ph = FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if (ph != null)
            {
                player = ph.transform;
                return player;
            }

            // 3. Tìm theo Tag Player
            try
            {
                GameObject pObj = GameObject.FindGameObjectWithTag("Player");
                if (pObj != null)
                {
                    player = pObj.transform;
                    return player;
                }
            }
            catch { }

            // 4. Tìm theo tên GameObject
            GameObject namedP = GameObject.Find("Player");
            if (namedP != null)
            {
                player = namedP.transform;
                return player;
            }

            return null;
        }

        private void Start()
        {
            ResolvePlayer(true);
            if (StateMachine != null && IdleState != null)
            {
                StateMachine.Initialize(IdleState);
            }
        }

        private void Update()
        {
            // Luôn kiểm tra và tìm target Player mỗi frame (phòng trường hợp Player vừa bật lại sau cutscene)
            if (player == null || !player.gameObject.activeInHierarchy)
            {
                ResolvePlayer(true);
            }

            if (StateMachine != null)
            {
                StateMachine.Update();
            }
        }

        private void FixedUpdate()
        {
            if (StateMachine != null)
            {
                StateMachine.FixedUpdate();
            }
        }

        private void RegisterAttackComponents()
        {
            EnsureAttackComponent<JumpSlamAttack>();
            EnsureAttackComponent<SlashAttack>();
            EnsureAttackComponent<TripleSlashAttack>();
            EnsureAttackComponent<ChargeAttack>();
            EnsureAttackComponent<GroundSlamAttack>();
            EnsureAttackComponent<DoubleChargeAttack>();
            EnsureAttackComponent<SuperGroundSlamAttack>();
            EnsureAttackComponent<RangedAttack>();

            attackMap.Clear();
            BossAttackBase[] attacks = GetComponents<BossAttackBase>();
            foreach (var attack in attacks)
            {
                if (!string.IsNullOrEmpty(attack.AttackName) && !attackMap.ContainsKey(attack.AttackName))
                {
                    attackMap.Add(attack.AttackName, attack);
                }
            }
        }

        private void EnsureAttackComponent<T>() where T : BossAttackBase
        {
            if (GetComponent<T>() == null)
            {
                gameObject.AddComponent<T>();
            }
        }

        private void InitializeStateMachine()
        {
            StateMachine = new BossStateMachine();

            IdleState = new BossIdleState(this, StateMachine);
            MoveState = new BossMoveState(this, StateMachine);
            TelegraphState = new BossTelegraphState(this, StateMachine);
            AttackState = new BossAttackState(this, StateMachine);
            RecoveryState = new BossRecoveryState(this, StateMachine);
            PhaseTransitionState = new BossPhaseTransitionState(this, StateMachine);
            DeadState = new BossDeadState(this, StateMachine);
        }

        public float DistanceToPlayer()
        {
            Transform p = Player;
            if (p == null) return float.MaxValue;
            return Mathf.Abs(p.position.x - transform.position.x);
        }

        public void EnableAggro()
        {
            SetCutsceneLocked(false);
        }

        public void DisableAggro()
        {
            SetCutsceneLocked(true);
        }

        public void SetCutsceneLocked(bool isLocked)
        {
            isAggroEnabled = !isLocked;

            if (isLocked)
            {
                // Khóa Attack & Target:
                // 1. Hủy bỏ đòn đánh hiện tại (nếu đang đánh)
                CleanupCurrentAttack();
                CurrentAttackData = null;
                currentAttackComponent = null;

                // 2. Tắt hiệu ứng Telegraph (nếu có)
                if (telegraphEffect != null)
                {
                    telegraphEffect.SetActive(false);
                }

                // 3. Dừng di chuyển
                if (Movement != null)
                {
                    Movement.StopMovement();
                }

                // 4. Reset luồng combo/attack về bước ban đầu
                if (AttackSelector != null)
                {
                    AttackSelector.ResetFlow();
                }

                // 5. Đưa Boss về trạng thái Idle nếu đang không chết
                if (StateMachine != null && IdleState != null && !IsDead)
                {
                    StateMachine.ChangeState(IdleState);
                }

                // 6. Phát animation đứng chờ (Idle)
                PlayIdleAnimation();
            }
            else
            {
                // Mở khóa: Boss sẵn sàng chiến đấu trở lại
                if (AttackSelector != null)
                {
                    AttackSelector.ResetFlow();
                }
            }
        }

        public void SetCurrentAttack(BossAttackData attackData)
        {
            if (!isAggroEnabled)
            {
                CurrentAttackData = null;
                currentAttackComponent = null;
                return;
            }

            CurrentAttackData = attackData;
            currentAttackComponent = null;

            if (attackData != null && attackMap.TryGetValue(attackData.attackName, out var comp))
            {
                currentAttackComponent = comp;
            }
        }

        public void ExecuteCurrentAttack(Action onComplete)
        {
            if (!isAggroEnabled)
            {
                onComplete?.Invoke();
                return;
            }

            if (currentAttackComponent != null && CurrentAttackData != null)
            {
                currentAttackComponent.Execute(this, CurrentAttackData, onComplete);
            }
            else
            {
                Debug.LogWarning($"[BossController] Không tìm thấy script attack nào cho '{CurrentAttackData?.attackName}' trên Boss GameObject!");
                onComplete?.Invoke();
            }
        }

        public void UpdateCurrentAttack()
        {
            currentAttackComponent?.UpdateAttack();
        }

        public void FixedUpdateCurrentAttack()
        {
            currentAttackComponent?.FixedUpdateAttack();
        }

        public void CleanupCurrentAttack()
        {
            currentAttackComponent?.Cleanup();
        }

        public void SetInvulnerable(bool invulnerable)
        {
            if (health != null)
            {
                health.SetInvulnerable(invulnerable);
            }
        }

        public bool CheckPendingPhaseTransition()
        {
            return health != null && health.HasPendingPhaseTransition;
        }

        public void PerformPhaseTransition()
        {
            if (health != null)
            {
                health.ConsumePhaseTransition();
            }
            UpdatePhaseModel();
            Debug.Log($"[BossController] Chuyển phase thành công! Phase hiện tại: {CurrentPhase}");
        }

        public void UpdatePhaseModel()
        {
            if (phaseSpineModels == null || phaseSpineModels.Length == 0) return;

            int phaseIndex = (int)CurrentPhase; // Phase1 = 0, Phase2 = 1, Phase3 = 2...
            for (int i = 0; i < phaseSpineModels.Length; i++)
            {
                if (phaseSpineModels[i] != null)
                {
                    bool isActive = (i == phaseIndex);
                    phaseSpineModels[i].SetActive(isActive);
                    if (isActive)
                    {
                        // Cập nhật lại SkeletonAnimation reference sang model mới đang active
                        skeletonAnimation = phaseSpineModels[i].GetComponent<SkeletonAnimation>();
                        if (skeletonAnimation == null)
                        {
                            skeletonAnimation = phaseSpineModels[i].GetComponentInChildren<SkeletonAnimation>();
                        }
                    }
                }
            }
        }

        public void DisablePhysics()
        {
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.isKinematic = true;
            }
            if (bossCollider != null)
            {
                bossCollider.enabled = false;
            }
        }

        public TrackEntry PlaySpineAnimation(string animName, bool loop = false, float timeScale = 1.0f, int trackIndex = 0)
        {
            if (skeletonAnimation == null || string.IsNullOrEmpty(animName)) return null;
            if (skeletonAnimation.AnimationState == null) return null;

            skeletonAnimation.AnimationState.ClearTrack(trackIndex);
            skeletonAnimation.Skeleton?.SetToSetupPose();
            TrackEntry entry = skeletonAnimation.AnimationState.SetAnimation(trackIndex, animName, loop);
            if (entry != null)
            {
                entry.TimeScale = timeScale > 0f ? timeScale : 1.0f;
            }
            return entry;
        }

        public void PlayIdleAnimation()
        {
            PlaySpineAnimation(idleAnimName, true, IdleAnimSpeed);
            SetAnimatorTrigger("Idle");
            SetAnimatorBool("IsMoving", false);
        }

        public void PlayMoveAnimation()
        {
            PlaySpineAnimation(moveAnimName, true, MoveAnimSpeed);
            SetAnimatorBool("IsMoving", true);
        }

        public void PlayTelegraphAnimation()
        {
            PlaySpineAnimation(telegraphAnimName, false, TelegraphAnimSpeed);
            SetAnimatorTrigger("Telegraph");
        }

        public void PlayAttackAnimation()
        {
            if (CurrentAttackData != null)
            {
                if (!string.IsNullOrEmpty(CurrentAttackData.spineAnimationName))
                {
                    PlaySpineAnimation(CurrentAttackData.spineAnimationName, CurrentAttackData.loopSpineAnimation);
                }
                if (!string.IsNullOrEmpty(CurrentAttackData.animatorTrigger))
                {
                    SetAnimatorTrigger(CurrentAttackData.animatorTrigger);
                }
            }
        }

        public void PlayRecoveryAnimation()
        {
            PlaySpineAnimation(recoveryAnimName, false, RecoveryAnimSpeed);
            SetAnimatorTrigger("Recovery");
        }

        public void PlayPhaseTransitionAnimation()
        {
            PlaySpineAnimation(phaseTransitionAnimName, false, PhaseTransitionAnimSpeed);
            SetAnimatorTrigger("PhaseTransition");
        }

        public void PlayDeathAnimation()
        {
            PlaySpineAnimation(dieAnimName, false, DieAnimSpeed);
            SetAnimatorTrigger("Die");
        }

        public void SetAnimatorTrigger(string triggerName)
        {
            if (animator != null && !string.IsNullOrEmpty(triggerName))
            {
                animator.SetTrigger(triggerName);
            }
        }

        public void SetAnimatorBool(string boolName, bool value)
        {
            if (animator != null && !string.IsNullOrEmpty(boolName))
            {
                animator.SetBool(boolName, value);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (leftArenaBound != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(leftArenaBound.position + Vector3.up * 5f, leftArenaBound.position + Vector3.down * 5f);
            }

            if (rightArenaBound != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(rightArenaBound.position + Vector3.up * 5f, rightArenaBound.position + Vector3.down * 5f);
            }

            if (meleePoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(meleePoint.position, 0.25f);
            }
            if (rangedPoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(rangedPoint.position, 0.25f);
            }
            if (groundPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(groundPoint.position, 0.25f);
            }

            Transform p = RawPlayer;
            if (p != null)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawLine(transform.position, p.position);
            }
        }
    }
}
