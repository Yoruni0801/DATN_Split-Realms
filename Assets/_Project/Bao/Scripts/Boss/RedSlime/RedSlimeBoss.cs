using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Bao.Bosses
{
    public enum SlimeForm
    {
        SlimeA, // Đỏ (Lớn)
        SlimeB, // Xanh (Vừa - tách làm 2)
        SlimeC  // Vàng (Nhỏ - tách làm 4)
    }

    [RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(Collider2D))]
    public class RedSlimeBoss : MonoBehaviour, Bao.Enemies.IDamageable, IEnemyTarget
    {
        // ══════════════════════════════════════════════════
        //  SERIALIZED FIELDS
        // ══════════════════════════════════════════════════

        [Header("Slime Forms & Visual Scale (Kích thước 3 biến thể)")]
        [SerializeField] private SlimeForm currentForm = SlimeForm.SlimeA;
        [SerializeField] private GameObject slimeAObject;
        [SerializeField] private GameObject slimeBObject;
        [SerializeField] private GameObject slimeCObject;
        [SerializeField] private GameObject splitPrefab;
        [Tooltip("Scale của Slime Đỏ (Form A - Lớn)")]
        [SerializeField] private float formScaleA = 1.0f;
        [Tooltip("Scale của Slime Xanh (Form B - Vừa)")]
        [SerializeField] private float formScaleB = 0.7f;
        [Tooltip("Scale của Slime Vàng (Form C - Nhỏ)")]
        [SerializeField] private float formScaleC = 0.5f;
        [SerializeField] private float splitInvulnerableDuration = 0.6f;

        [Header("Split Pop Distance (Khoảng cách nảy tách xa 2 bên)")]
        [Tooltip("Khoảng cách nảy bung ra 2 bên khi Đỏ (A) tách thành 2 Xanh (B)")]
        [SerializeField] private float splitDistanceFormB = 5.5f;
        [Tooltip("Khoảng cách nảy bung ra 2 bên khi Xanh (B) tách thành 2 Vàng (C)")]
        [SerializeField] private float splitDistanceFormC = 7.5f;
        [Tooltip("Độ cao nhảy vòng cung khi tách")]
        [SerializeField] private float splitPopJumpHeight = 2.5f;
        [Tooltip("Thời gian thực hiện cú nhảy bung tách")]
        [SerializeField] private float splitPopDuration = 0.45f;

        [Header("Flocking Separation (Khoảng cách né nhau)")]
        [Tooltip("Khoảng cách cự ly tối thiểu giữa các Slime khi đuổi theo Player")]
        [SerializeField] private float separationDistance = 2.5f;
        [Tooltip("Lực đẩy dãn cách giữa các Slime")]
        [SerializeField] private float separationWeight = 1.6f;

        [Header("Slime Merge Mechanic")]
        [SerializeField] private bool enableMerge = true;
        [Tooltip("Thời gian chờ (giây) trước khi Slime con tự tìm nhau để gộp lại")]
        [SerializeField] private float timeBeforeMerge = 6.0f;
        [Tooltip("Khoảng cách chạm nhau để gộp thành công")]
        [SerializeField] private float mergeDistance = 1.2f;
        [Tooltip("Tốc độ di chuyển lại gần nhau khi gộp")]
        [SerializeField] private float mergeSpeedMultiplier = 1.6f;

        [Header("Boss - Health")]
        [SerializeField] private int maxHealth = 350;
        [SerializeField] private int currentHealth;
        [SerializeField] private BossPhase currentPhase = BossPhase.Phase1;

        [Header("Boss - Cutscene Settings")]
        [Tooltip("Kéo PlayableDirector (Cutscene Timeline) vào đây để phát khi Boss chết")]
        [SerializeField] private UnityEngine.Playables.PlayableDirector deathCutsceneDirector;
        [Tooltip("Thời gian delay trước khi phát cutscene (giây)")]
        [SerializeField] private float cutsceneDelay = 0f;

        [Header("Boss - UI & Events")]
        [Tooltip("Prefab UI Canvas thanh máu của Boss (Assets/_Project/Tien/Boss/Red Slime.prefab)")]
        [SerializeField] private GameObject uiPrefab;
        [SerializeField] private BossUIEventDispatcher uiEventDispatcher;

        [Header("Boss - Speed Per Form (Càng tách tốc độ càng cao)")]
        [Tooltip("Tốc độ di chuyển của Slime Đỏ (Form A - Lớn)")]
        [SerializeField] private float moveSpeedFormA = 2.0f;
        [Tooltip("Tốc độ di chuyển của Slime Xanh (Form B - Vừa)")]
        [SerializeField] private float moveSpeedFormB = 3.2f;
        [Tooltip("Tốc độ di chuyển của Slime Vàng (Form C - Nhỏ)")]
        [SerializeField] private float moveSpeedFormC = 4.6f;
        [SerializeField] private float detectionRange = 12f;
        [SerializeField] private float attackRange = 2.0f;
        [SerializeField] private LayerMask environmentLayer;

        [Header("Boss - AI Behavior & Pacing")]
        [SerializeField] private float globalActionCooldown = 1.8f;
        [SerializeField] private float optimalSpacingDistance = 3.5f;
        [SerializeField] private float backingSpeedMultiplier = 0.4f;

        [Header("Boss - Melee Attack")]
        [SerializeField] private int meleeDamage = 18;
        [SerializeField] private float meleeCooldown = 2.5f;
        [SerializeField] private float meleeRadius = 1.8f;
        [SerializeField] private float meleeDelay = 0.35f;
        [SerializeField] private float meleeAnimDuration = 0.9f;
        [SerializeField] private Vector2 meleeVFXOffset = new Vector2(1.2f, 0f);

        [Header("Boss - Ground Slam & Spike Wave")]
        [Tooltip("Delay in seconds until the boss hits the ground in the animation")]
        [SerializeField] private float slamJumpDuration = 0.5f;
        [SerializeField] private float slamCooldown = 7f;
        [SerializeField] private int slamDamage = 25;
        [SerializeField] private float slamAOERadius = 2.5f;
        [SerializeField] private Vector2 slamVFXOffset = new Vector2(0f, 0f);
        [SerializeField] private float slamAnimDuration = 1.5f;
        [Tooltip("Prefab for each individual spike (Gai_RedSlime)")]
        [SerializeField] private GameObject spikePrefab;
        [SerializeField] private int spikeCountPerSide = 5;
        [SerializeField] private float spikeSpacing = 1.6f;
        [SerializeField] private float spikeStartOffset = 2.2f;
        [SerializeField] private float spikeSpawnDelay = 0.06f;
        [SerializeField] private int spikeDamage = 16;
        [SerializeField] private float spikeLifetime = 1.6f;
        [Tooltip("Angle to slant spikes outward (e.g. 15 for slanted)")]
        [SerializeField] private float spikeAngle = 15f;
        [Tooltip("Flip spike sprite on X axis to make it point outward")]
        [SerializeField] private bool flipSpikeX = false;

        [Header("Boss - Visual & Renderers")]
        [SerializeField] private SpriteRenderer[] spriteRenderers;
        [Tooltip("Set to -1 if sprite natively faces Left, or 1 if sprite natively faces Right")]
        [SerializeField] private float facingVisualMultiplier = -1f;
        public float FacingVisualMultiplier => facingVisualMultiplier;

        [Header("Slime B - Hunter Dash Attack (Kỹ năng Lướt Săn Mồi Slime B)")]
        [SerializeField] private float dashBCooldown = 3.5f;
        [SerializeField] private float dashBSpeed = 16f;
        [SerializeField] private float dashBDuration = 0.38f;
        [SerializeField] private float dashBWindupTime = 0.28f;
        [SerializeField] private int dashBDamage = 22;
        [SerializeField] private float dashBRadius = 1.4f;

        [Header("Slime B - Tri-Burst Slime Elements (Kỹ năng Bắn Đạn Thạch Slime B)")]
        [SerializeField] private float shootBCooldown = 4.0f;
        [SerializeField] private float shootBSpeed = 14f;
        [SerializeField] private int shootBDamage = 16;
        [SerializeField] private float shootBSpreadAngle = 15f;
        [SerializeField] private float shootBWindupDuration = 0.667f;
        [SerializeField] private float shootBAnimDuration = 1.5f;
        [SerializeField] private Vector2 shootBMouthOffset = new Vector2(0.8f, 0.35f);
        [SerializeField] private Sprite shootBProjectileSprite;

        [Header("Slime B - Dual Pincer Cross-Dash (Kỹ năng Phối Hợp Song Sát Slime B)")]
        [SerializeField] private float dualCrossDashCooldown = 7.0f;
        [SerializeField] private float dualCrossDashSpeed = 20.0f;
        [SerializeField] private float dualCrossDashFlankDist = 5.2f;
        [SerializeField] private float dualCrossDashWindup = 0.55f;
        [SerializeField] private float dualCrossDashDuration = 0.30f;
        [SerializeField] private int dualCrossDashDamage = 32;
        [SerializeField] private float dualCrossDashRadius = 1.6f;
        [SerializeField] private float dualCrossDashStunDuration = 1.35f;

        [Header("Slime C - Pinball Popcorn & Laser Matrix (Kỹ năng Slime C)")]
        [SerializeField] private float pinballCooldown = 3.8f;
        [SerializeField] private float pinballSpeed = 13.5f;
        [SerializeField] private int pinballBounces = 2;
        [SerializeField] private int pinballDamage = 16;
        [SerializeField] private float pinballRadius = 1.1f;
        [SerializeField] private float laserCooldown = 5.5f;
        [SerializeField] private float laserMaxRange = 7.5f;
        [SerializeField] private float laserDuration = 1.0f;
        [SerializeField] private int laserDamage = 14;
        [SerializeField] private float laserWidth = 0.32f;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        [Header("Shader Controller Ref")]
        [SerializeField] private RedSlimeShaderController shaderController;

        // ══════════════════════════════════════════════════
        //  STATE MACHINE & REFERENCES
        // ══════════════════════════════════════════════════

        public RedSlimeIdleState IdleState { get; private set; }
        public RedSlimeChaseState ChaseState { get; private set; }
        public RedSlimeMeleeState MeleeState { get; private set; }
        public RedSlimeGroundSlamState GroundSlamState { get; private set; }
        public RedSlimeDeadState DeadState { get; private set; }
        public RedSlimeMergeState MergeState { get; private set; }
        public RedSlimeBDashState BDashState { get; private set; }
        public RedSlimeBShootState BShootState { get; private set; }
        public RedSlimeBDualPincerCrossDashState BDualCrossDashState { get; private set; }
        public RedSlimeCPinballState CPinballState { get; private set; }
        public RedSlimeCLaserMatrixState CLaserMatrixState { get; private set; }

        public RedSlimeBossState CurrentState { get; private set; }

        public Rigidbody2D Rigidbody2D { get; private set; }
        public Animator Animator { get; private set; }
        public Collider2D Collider2D { get; private set; }
        public Transform Target { get; private set; }
        public RedSlimeShaderController ShaderController => shaderController;
        public RedSlimeBossAudioController AudioController { get; private set; }

        // ══════════════════════════════════════════════════
        //  PROPERTIES & GETTERS
        // ══════════════════════════════════════════════════

        public SlimeForm CurrentForm => currentForm;
        public float MergeDistance => mergeDistance;
        public float MergeSpeedMultiplier => mergeSpeedMultiplier;
        public RedSlimeBoss MergePartner { get; set; }
        public bool IsMerging { get; set; } = false;
        public bool IsPopping { get; set; } = false;
        public float SeparationDistance => separationDistance;
        public float SeparationWeight => separationWeight;
        public float SplitDistanceFormB => splitDistanceFormB;
        public float SplitDistanceFormC => splitDistanceFormC;
        public float SplitPopJumpHeight => splitPopJumpHeight;
        public float SplitPopDuration => splitPopDuration;
        public int MaxHealth => maxHealth;
        public int CurrentHealth => currentHealth;
        float IEnemyTarget.CurrentHealth => currentHealth;
        Transform IEnemyTarget.Transform => transform;
        public bool IsAlive => !IsDead;
        public BossPhase CurrentPhase => currentPhase;

        public float MoveSpeed
        {
            get
            {
                switch (currentForm)
                {
                    case SlimeForm.SlimeB:
                        return moveSpeedFormB;
                    case SlimeForm.SlimeC:
                        return moveSpeedFormC;
                    case SlimeForm.SlimeA:
                    default:
                        return moveSpeedFormA;
                }
            }
        }

        public LayerMask EnvironmentLayer => environmentLayer;

        public int MeleeDamage => meleeDamage;
        public float MeleeRadius => meleeRadius;
        public float MeleeDelay => meleeDelay;
        public float MeleeAnimDuration => meleeAnimDuration;
        public Vector2 MeleeVFXOffset => meleeVFXOffset;

        public float SlamJumpDuration => slamJumpDuration;
        public int SlamDamage => slamDamage;
        public float SlamAOERadius => slamAOERadius;
        public Vector2 SlamVFXOffset => slamVFXOffset;
        public float SlamAnimDuration => slamAnimDuration;
        public GameObject SpikePrefab => spikePrefab;
        public int SpikeCountPerSide => spikeCountPerSide;
        public float SpikeSpacing => spikeSpacing;
        public float SpikeStartOffset => spikeStartOffset;
        public float SpikeSpawnDelay => spikeSpawnDelay;
        public int SpikeDamage => spikeDamage;
        public float SpikeLifetime => spikeLifetime;
        public float SpikeAngle => spikeAngle;
        public bool FlipSpikeX => flipSpikeX;

        public float OptimalSpacingDistance => optimalSpacingDistance;
        public float BackingSpeedMultiplier => backingSpeedMultiplier;

        public float DashBCooldown => dashBCooldown;
        public float DashBSpeed => dashBSpeed;
        public float DashBDuration => dashBDuration;
        public float DashBWindupTime => dashBWindupTime;
        public int DashBDamage => dashBDamage;
        public float DashBRadius => dashBRadius;

        public float ShootBCooldown => shootBCooldown;
        public float ShootBSpeed => shootBSpeed;
        public int ShootBDamage => shootBDamage;
        public float ShootBSpreadAngle => shootBSpreadAngle;
        public float ShootBWindupDuration => shootBWindupDuration;
        public float ShootBAnimDuration => shootBAnimDuration;
        public Vector2 ShootBMouthOffset => shootBMouthOffset;
        public Sprite ShootBProjectileSprite => shootBProjectileSprite;

        public float DualCrossDashCooldown => dualCrossDashCooldown;
        public float DualCrossDashSpeed => dualCrossDashSpeed;
        public float DualCrossDashFlankDist => dualCrossDashFlankDist;
        public float DualCrossDashWindup => dualCrossDashWindup;
        public float DualCrossDashDuration => dualCrossDashDuration;
        public int DualCrossDashDamage => dualCrossDashDamage;
        public float DualCrossDashRadius => dualCrossDashRadius;
        public float DualCrossDashStunDuration => dualCrossDashStunDuration;

        public float PinballCooldown => pinballCooldown;
        public float PinballSpeed => pinballSpeed;
        public int PinballBounces => pinballBounces;
        public int PinballDamage => pinballDamage;
        public float PinballRadius => pinballRadius;

        public float LaserCooldown => laserCooldown;
        public float LaserMaxRange => laserMaxRange;
        public float LaserDuration => laserDuration;
        public int LaserDamage => laserDamage;
        public float LaserWidth => laserWidth;

        public bool IsDead { get; private set; } = false;
        public bool IsInvulnerable { get; set; } = false;
        public bool IsTransitioningPhases { get; set; } = false;
        public float FacingDirection { get; private set; } = 1f;

        public Vector3 CenterPosition
        {
            get
            {
                if (Collider2D != null) return Collider2D.bounds.center;
                return transform.position;
            }
        }

        public Vector3 GroundPosition
        {
            get
            {
                if (Collider2D != null)
                {
                    return new Vector3(Collider2D.bounds.center.x, Collider2D.bounds.min.y, Collider2D.bounds.center.z);
                }
                return transform.position;
            }
        }

        /// <summary>
        /// Lấy độ lớn scale tuyệt đối cho từng biến thể Form (A, B, C) theo Inspector
        /// </summary>
        public float GetFormScaleMagnitude(SlimeForm form)
        {
            switch (form)
            {
                case SlimeForm.SlimeA: return Mathf.Max(0.1f, formScaleA);
                case SlimeForm.SlimeB: return Mathf.Max(0.1f, formScaleB);
                case SlimeForm.SlimeC: return Mathf.Max(0.1f, formScaleC);
                default: return 1.0f;
            }
        }

        public float CurrentFormScaleMagnitude => GetFormScaleMagnitude(currentForm);

        /// <summary>
        /// Tính toán localScale chuẩn xác tuyệt đối cho Slime dựa trên Form hiện tại và hướng quay mặt
        /// </summary>
        public Vector3 GetTargetLocalScale(float overrideFacingSign = 0f)
        {
            float sign = overrideFacingSign != 0f ? Mathf.Sign(overrideFacingSign) : FacingDirection;
            float scaleMag = CurrentFormScaleMagnitude;
            return new Vector3(
                sign * scaleMag * facingVisualMultiplier,
                scaleMag,
                scaleMag
            );
        }

        // Cooldown Timers
        private float lastMeleeTime = -99f;
        private float lastSlamTime = -99f;
        private float lastDashBTime = -99f;
        private float lastShootBTime = -99f;
        private float lastDualCrossDashTime = -99f;
        private float lastPinballCTime = -99f;
        private float lastLaserCTime = -99f;
        private float lastActionTime = -99f;
        private float lastSplitOrFormChangeTime = -99f;

        // Categories for Anti-Spam
        public enum SkillCategory { None, Melee, AOE, Dash, Shoot, Bounce, Laser }
        public SkillCategory LastUsedSkillCategory { get; set; } = SkillCategory.None;

        [Header("Boss UI Multi-Layer Settings")]
        [Tooltip("Bật/Tắt chế độ hiển thị máu nhiều tầng (x4, x3, x2, x1). Tích chọn nếu muốn có thanh máu x cho con quái này, bỏ tích để hiển thị 1 thanh máu đơn bình thường.")]
        [SerializeField] private bool useMultiLayerHealthBar = true;
        [Tooltip("Số lượng tầng máu khi bật Multi-Layer (Mặc định: 4 tầng).")]
        [SerializeField, Range(1, 10)] private int multiLayerCount = 4;

        // Events
        public event System.Action<int, int> OnHealthChanged;
        public event System.Action OnBossDied;

        public BossUIEventDispatcher UIEventDispatcher => uiEventDispatcher;
        public GameObject UIPrefab => uiPrefab;

        public static BossUIEventDispatcher SharedUIDispatcher { get; private set; }
        public static readonly List<RedSlimeBoss> ActiveSlimes = new List<RedSlimeBoss>();
        private static int initialSwarmMaxHealth = 50;

        public static int GetTotalSwarmHealth()
        {
            int total = 0;
            for (int i = 0; i < ActiveSlimes.Count; i++)
            {
                if (ActiveSlimes[i] != null && !ActiveSlimes[i].IsDead)
                {
                    total += ActiveSlimes[i].CurrentHealth;
                }
            }
            return Mathf.Clamp(total, 0, GetTotalSwarmMaxHealth());
        }

        public static int GetTotalSwarmMaxHealth()
        {
            return Mathf.Max(1, initialSwarmMaxHealth);
        }

        public int GetMaxHealthForForm(SlimeForm form)
        {
            int baseMax = GetTotalSwarmMaxHealth();
            if (form == SlimeForm.SlimeA) return baseMax;
            if (form == SlimeForm.SlimeB) return Mathf.Max(1, baseMax / 2);
            if (form == SlimeForm.SlimeC) return Mathf.Max(1, baseMax / 4);
            return baseMax;
        }

        public static void NotifySwarmHealthChanged()
        {
            if (SharedUIDispatcher != null)
            {
                int current = GetTotalSwarmHealth();
                int max = GetTotalSwarmMaxHealth();
                SharedUIDispatcher.UpdateHealth(current, max);
            }
        }

        public static void NotifySwarmEngaged()
        {
            if (SharedUIDispatcher != null && !SharedUIDispatcher.IsEngaged)
            {
                SharedUIDispatcher.Engage();
            }
        }

        private void OnEnable()
        {
            if (!ActiveSlimes.Contains(this))
            {
                ActiveSlimes.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveSlimes.Remove(this);
        }

        private void OnValidate()
        {
            // Các con sinh ra bằng Instantiate cần giữ collider/Rigidbody sạch,
            // không mang theo vận tốc hoặc trạng thái collider từ thời điểm tách.
            if (splitPopDuration < 0.05f) splitPopDuration = 0.05f;
            if (formScaleA <= 0f) formScaleA = 1f;
            if (formScaleB <= 0f) formScaleB = 0.7f;
            if (formScaleC <= 0f) formScaleC = 0.5f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SharedUIDispatcher = null;
            ActiveSlimes.Clear();
            initialSwarmMaxHealth = 50;
        }

        private void OnDestroy()
        {
            ActiveSlimes.Remove(this);
            if (ActiveSlimes.Count == 0 || (ActiveSlimes.Count == 1 && ActiveSlimes[0] == this))
            {
                SharedUIDispatcher = null;
            }
        }

        // ══════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════

        private void Awake()
        {
            Rigidbody2D = GetComponent<Rigidbody2D>();
            Animator = GetComponent<Animator>();
            Collider2D = GetComponent<Collider2D>();

            Rigidbody2D.freezeRotation = true;
            Rigidbody2D.linearVelocity = Vector2.zero;
            Rigidbody2D.angularVelocity = 0f;
            currentHealth = maxHealth;

            if (spriteRenderers == null || spriteRenderers.Length == 0)
            {
                spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
            }

            if (shaderController == null)
            {
                shaderController = GetComponentInChildren<RedSlimeShaderController>();
                if (shaderController == null) shaderController = GetComponent<RedSlimeShaderController>();
            }

            AudioController = GetComponent<RedSlimeBossAudioController>();
            if (AudioController == null) AudioController = GetComponentInChildren<RedSlimeBossAudioController>();

            // Create all states
            IdleState = new RedSlimeIdleState(this);
            ChaseState = new RedSlimeChaseState(this);
            MeleeState = new RedSlimeMeleeState(this);
            GroundSlamState = new RedSlimeGroundSlamState(this);
            DeadState = new RedSlimeDeadState(this);
            MergeState = new RedSlimeMergeState(this);
            BDashState = new RedSlimeBDashState(this);
            BShootState = new RedSlimeBShootState(this);
            BDualCrossDashState = new RedSlimeBDualPincerCrossDashState(this);
            CPinballState = new RedSlimeCPinballState(this);
            CLaserMatrixState = new RedSlimeCLaserMatrixState(this);

            InitForms();

            if (uiEventDispatcher == null)
            {
                uiEventDispatcher = GetComponent<BossUIEventDispatcher>();
                if (uiEventDispatcher == null)
                {
                    uiEventDispatcher = gameObject.AddComponent<BossUIEventDispatcher>();
                }
            }

            if (uiEventDispatcher != null)
            {
                uiEventDispatcher.ConfigureMultiLayer(useMultiLayerHealthBar, multiLayerCount);
            }

            // Thiết lập Dispatcher chung nếu đây là Boss gốc hoặc con đầu tiên
            if (SharedUIDispatcher == null && uiEventDispatcher != null)
            {
                SharedUIDispatcher = uiEventDispatcher;
                initialSwarmMaxHealth = maxHealth;
                if (uiPrefab != null)
                {
                    SharedUIDispatcher.SetUIPrefab(uiPrefab);
                }
                SharedUIDispatcher.Initialize("Red Slime", initialSwarmMaxHealth);
            }
        }

        private RedSlimeElementHitbox[] slimeBElementHitboxes;

        private void InitForms()
        {
            if (slimeAObject == null)
            {
                var t = transform.Find("Slime A");
                if (t != null) slimeAObject = t.gameObject;
            }
            if (slimeBObject == null)
            {
                var t = transform.Find("Slime B");
                if (t != null) slimeBObject = t.gameObject;
            }
            if (slimeCObject == null)
            {
                var t = transform.Find("Slime C");
                if (t != null) slimeCObject = t.gameObject;
            }

            // Tự động khởi tạo RedSlimeElementHitbox cho 3 GameObject con Slime elements 1, 2, 3 của Slime B
            if (slimeBObject != null)
            {
                List<RedSlimeElementHitbox> hitboxes = new List<RedSlimeElementHitbox>();
                string[] elementNames = new string[] { "Slime elements 1", "Slime elements 2", "Slime elements 3" };
                foreach (var elName in elementNames)
                {
                    Transform elT = slimeBObject.transform.Find(elName);
                    if (elT != null)
                    {
                        var hb = elT.GetComponent<RedSlimeElementHitbox>();
                        if (hb == null)
                        {
                            hb = elT.gameObject.AddComponent<RedSlimeElementHitbox>();
                        }
                        hb.Initialize(this);
                        hitboxes.Add(hb);
                    }
                }
                slimeBElementHitboxes = hitboxes.ToArray();
            }

            ApplyCurrentFormVisual();
        }

        /// <summary>
        /// Reset bộ đếm va chạm của 3 viên đạn Slime elements khi bắt đầu bắn
        /// </summary>
        public void ResetSlimeBElements()
        {
            if (slimeBElementHitboxes != null)
            {
                foreach (var hb in slimeBElementHitboxes)
                {
                    if (hb != null) hb.ResetHitTracker();
                }
            }
        }

        public void ApplyCurrentFormVisual()
        {
            if (slimeAObject != null) slimeAObject.SetActive(currentForm == SlimeForm.SlimeA);
            if (slimeBObject != null) slimeBObject.SetActive(currentForm == SlimeForm.SlimeB);
            if (slimeCObject != null) slimeCObject.SetActive(currentForm == SlimeForm.SlimeC);

            // Cập nhật scale tuyệt đối chuẩn xác theo Form hiện tại
            transform.localScale = GetTargetLocalScale();

            shaderController?.RefreshMaterials();
        }

        private void Start()
        {
            ResetMergeTimer();
            FindPlayerTarget();
            ChangeState(IdleState);
        }

        private void Update()
        {
            if (IsDead) return;

            // Failsafe chống lọt sàn/rơi khỏi map
            if (transform.position.y < -25f)
            {
                if (Target != null)
                {
                    transform.position = Target.position + Vector3.up * 1.5f;
                }
                else
                {
                    transform.position = new Vector3(transform.position.x, 0f, transform.position.z);
                }
                if (Rigidbody2D != null) Rigidbody2D.linearVelocity = Vector2.zero;
            }

            if (Target == null)
            {
                FindPlayerTarget();
            }

            CurrentState?.LogicUpdate();
        }

        private void FixedUpdate()
        {
            if (IsDead) return;
            CurrentState?.PhysicsUpdate();
        }

        // ══════════════════════════════════════════════════
        //  STATE MACHINE CONTROL
        // ══════════════════════════════════════════════════

        public void ChangeState(RedSlimeBossState newState)
        {
            if (newState == null || newState == CurrentState) return;

            // Tự động thông báo UI khi chuyển sang trạng thái chiến đấu (ChaseState, MeleeState, BDashState, etc.)
            if (!(newState is RedSlimeIdleState) && !(newState is RedSlimeDeadState))
            {
                NotifySwarmEngaged();
            }

            CurrentState?.Exit();
            CurrentState = newState;
            CurrentState.Enter();
        }

        // ══════════════════════════════════════════════════
        //  TARGET DETECTION & TARGETING
        // ══════════════════════════════════════════════════

        private void FindPlayerTarget()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Target = player.transform;
            }
        }

        public bool HasTargetInDetectionRange()
        {
            if (Target == null) return false;
            return Vector2.Distance(CenterPosition, Target.position) <= detectionRange;
        }

        public bool HasTargetInAttackRange()
        {
            if (Target == null) return false;
            return Vector2.Distance(CenterPosition, Target.position) <= attackRange;
        }

        // ══════════════════════════════════════════════════
        //  MOVEMENT & FACING
        // ══════════════════════════════════════════════════

        public void MoveHorizontal(float direction, float speed)
        {
            if (Rigidbody2D == null) return;
            Rigidbody2D.linearVelocity = new Vector2(direction * speed, Rigidbody2D.linearVelocity.y);
            FaceDirection(direction);
        }

        public void StopHorizontalMovement()
        {
            if (Rigidbody2D == null) return;
            Rigidbody2D.linearVelocity = new Vector2(0f, Rigidbody2D.linearVelocity.y);
        }

        public void FacePosition(Vector3 worldPos)
        {
            float dir = (worldPos.x >= transform.position.x) ? 1f : -1f;
            FaceDirection(dir);
        }

        public void FaceDirection(float direction)
        {
            if (Mathf.Abs(direction) < 0.01f) return;
            FacingDirection = Mathf.Sign(direction);

            // Cập nhật scale chuẩn xác tuyệt đối mà không phụ thuộc vào scale cũ
            transform.localScale = GetTargetLocalScale(FacingDirection);
        }

        // ══════════════════════════════════════════════════
        //  SKILL USABILITY & COOLDOWNS
        // ══════════════════════════════════════════════════

        public bool CanPerformAction()
        {
            return (Time.time - lastActionTime) >= globalActionCooldown;
        }

        public void RecordActionUsed(SkillCategory category)
        {
            lastActionTime = Time.time;
            LastUsedSkillCategory = category;
        }

        public bool CanMelee() => (Time.time - lastMeleeTime) >= meleeCooldown;
        public void MarkMeleeUsed() { lastMeleeTime = Time.time; RecordActionUsed(SkillCategory.Melee); }

        public bool CanSlam() => (Time.time - lastSlamTime) >= slamCooldown;
        public void MarkSlamUsed() { lastSlamTime = Time.time; RecordActionUsed(SkillCategory.AOE); }

        public bool CanDashB() => (Time.time - lastDashBTime) >= dashBCooldown;
        public void MarkDashBUsed() { lastDashBTime = Time.time; RecordActionUsed(SkillCategory.Dash); }

        public bool CanShootB() => (Time.time - lastShootBTime) >= shootBCooldown;
        public void MarkShootBUsed() { lastShootBTime = Time.time; RecordActionUsed(SkillCategory.Shoot); }

        public bool CanDualCrossDash()
        {
            if (currentForm != SlimeForm.SlimeB) return false;
            if (IsMerging || IsInvulnerable || IsDead || IsPopping) return false;
            return (Time.time - lastDualCrossDashTime) >= dualCrossDashCooldown;
        }

        public void MarkDualCrossDashUsed()
        {
            lastDualCrossDashTime = Time.time;
            RecordActionUsed(SkillCategory.Dash);
        }

        /// <summary>
        /// Tìm một Slime B đồng đội khả dụng đang ở trạng thái rảnh rỗi để phối hợp song sát
        /// </summary>
        public RedSlimeBoss FindDualCrossDashPartner()
        {
            for (int i = 0; i < ActiveSlimes.Count; i++)
            {
                var other = ActiveSlimes[i];
                if (other == null || other == this || other.IsDead || other.IsInvulnerable || other.IsMerging || other.IsPopping)
                    continue;

                if (other.CurrentForm == SlimeForm.SlimeB && (other.CurrentState is RedSlimeChaseState || other.CurrentState is RedSlimeIdleState))
                {
                    return other;
                }
            }
            return null;
        }

        public bool CanPinballC() => (Time.time - lastPinballCTime) >= pinballCooldown;
        public void MarkPinballCUsed() { lastPinballCTime = Time.time; RecordActionUsed(SkillCategory.Bounce); }

        public bool CanLaserC() => (Time.time - lastLaserCTime) >= laserCooldown;
        public void MarkLaserCUsed() { lastLaserCTime = Time.time; RecordActionUsed(SkillCategory.Laser); }

        public void ResetMergeTimer() => lastSplitOrFormChangeTime = Time.time;

        // ══════════════════════════════════════════════════
        //  DAMAGE & COMBAT (IDamageable, IEnemyTarget)
        // ══════════════════════════════════════════════════

        public void TakeDamage(float damage)
        {
            TakeDamage(Mathf.RoundToInt(damage));
        }

        public void TakeDamage(int damage)
        {
            if (IsInvulnerable || IsTransitioningPhases || IsDead) return;

            // Đảm bảo UI Boss đã được kích hoạt khi bị tấn công
            NotifySwarmEngaged();

            int remainingHealth = Mathf.Max(0, currentHealth - damage);

            // Hiện số sát thương bay lên (Popup Damage)
            Vector3 popupPosition = CenterPosition + Vector3.up * 1.0f;
            DamagePopupSystem.ShowEnemy(popupPosition, damage);

            shaderController?.TriggerHitFlash();
            AudioController?.PlayHurtSound();

            if (Animator != null)
            {
                Animator.SetTrigger("DamageTrig");
            }

            currentHealth = remainingHealth;

            // Cơ chế phân chia máu khi tách:
            // SlimeA (Đỏ) -> tách thành 2 SlimeB (Xanh), máu còn lại chia đôi
            // SlimeB (Xanh) -> tách thành 2 SlimeC (Vàng), máu còn lại của con B đó chia đôi
            // SlimeC (Vàng) -> chết
            if (currentForm == SlimeForm.SlimeA)
            {
                SplitToNextForm(SlimeForm.SlimeB, remainingHealth);
                return;
            }
            else if (currentForm == SlimeForm.SlimeB)
            {
                SplitToNextForm(SlimeForm.SlimeC, remainingHealth);
                return;
            }
            else
            {
                // Slime C bị đánh thì nhận sát thương trực tiếp
                currentHealth = remainingHealth;
                OnHealthChanged?.Invoke(currentHealth, maxHealth);
                NotifySwarmHealthChanged();

                if (currentHealth <= 0)
                {
                    Die();
                }
            }
        }

        private void SplitToNextForm(SlimeForm nextForm, int remainingHealthBeforeSplit)
        {
            StartCoroutine(SplitRoutine(nextForm, remainingHealthBeforeSplit));
        }

        private IEnumerator SplitRoutine(SlimeForm nextForm, int remainingHealthBeforeSplit)
        {
            IsInvulnerable = true;
            IsPopping = true;
            StopHorizontalMovement();

            SlimeForm prevForm = currentForm;
            Vector3 splitPosition = CenterPosition;

            // Máu Max thì cố định theo Form
            int splitMaxSelf = GetMaxHealthForForm(nextForm);
            int splitMaxClone = splitMaxSelf;

            // Máu Hiện Tại thì giữ nguyên tỷ lệ phần trăm từ lúc bị chém
            float healthRatio = (float)remainingHealthBeforeSplit / (float)maxHealth;
            int splitHpSelf = Mathf.CeilToInt(splitMaxSelf * healthRatio);
            int splitHpClone = Mathf.Max(1, Mathf.FloorToInt(splitMaxClone * healthRatio));

            // VFX phải phát đúng một lần tại tâm ban đầu, trước khi hai con thay đổi form/di chuyển.
            shaderController?.SpawnSplitVFX(prevForm, nextForm, splitPosition);
            shaderController?.TriggerScreenShake(0.22f, 0.18f);
            AudioController?.PlaySplitSound();

            // 1. Chuyển bản thân thành nextForm với nửa máu, reset toàn bộ visual/physics cũ.
            currentForm = nextForm;
            currentHealth = splitHpSelf;
            maxHealth = splitMaxSelf;
            ApplyCurrentFormVisual();
            ResetRuntimeAfterFormChange();
            ResetMergeTimer();

            // 2. Clone từ prefab độc lập nếu có; tránh kế thừa state machine/animation đang chạy
            // từ GameObject gốc trong trường hợp splitPrefab chưa được gán.
            GameObject clonePrefab = splitPrefab != null ? splitPrefab : gameObject;
            GameObject clone = Instantiate(clonePrefab, transform.position, transform.rotation);
            RedSlimeBoss cloneBoss = clone.GetComponent<RedSlimeBoss>();

            if (cloneBoss != null)
            {
                cloneBoss.InitializeAsForm(nextForm, splitHpClone, splitMaxClone);
            }
            else
            {
                Debug.LogError("[RedSlimeBoss] splitPrefab cần chứa RedSlimeBoss để tạo Slime tách đúng.", clone);
                Destroy(clone);
            }

            NotifySwarmHealthChanged();

            // 3. Mỗi con bay về một phía với cùng nhịp; chỉ con clone hợp lệ mới được bật nhánh bên phải.
            float horizontalDist = nextForm == SlimeForm.SlimeB ? splitDistanceFormB : splitDistanceFormC;
            StartCoroutine(ParabolicPopArcRoutine(-1f, horizontalDist));
            if (cloneBoss != null)
            {
                cloneBoss.StartCoroutine(cloneBoss.ParabolicPopArcRoutine(1f, horizontalDist));
            }

            yield return new WaitForSeconds(splitInvulnerableDuration);
            IsInvulnerable = false;
        }

        private void ResetRuntimeAfterFormChange()
        {
            // Không dùng StopAllCoroutines ở đây: hàm này được gọi ngay trong SplitRoutine,
            // StopAllCoroutines sẽ tự hủy chính routine tách trước khi clone hoàn thành.
            StopHorizontalMovement();

            if (Rigidbody2D != null)
            {
                Rigidbody2D.linearVelocity = Vector2.zero;
                Rigidbody2D.angularVelocity = 0f;
            }

            if (Animator != null)
            {
                Animator.Rebind();
                Animator.Update(0f);
            }

            ResetSlimeBElements();
            shaderController?.RefreshMaterials();
        }

        public IEnumerator ParabolicPopArcRoutine(float direction, float horizontalDist)
        {
            IsPopping = true;
            StopHorizontalMovement();

            if (Rigidbody2D != null)
            {
                Rigidbody2D.linearVelocity = Vector2.zero;
                // Nhảy hình vòng cung bằng xung lực vật lý thực tế:
                // Tự động tương tác với trọng lực và sàn/tường mà không bao giờ xuyên qua nền đất
                float vx = (direction * horizontalDist) / splitPopDuration;
                float vy = Mathf.Max(6f, (4f * splitPopJumpHeight) / splitPopDuration);
                Rigidbody2D.linearVelocity = new Vector2(vx, vy);
            }

            yield return new WaitForSeconds(splitPopDuration);

            StopHorizontalMovement();
            IsPopping = false;

            // Hiệu ứng nảy đàn hồi (Squash & Stretch) khi tiếp đất
            shaderController?.TriggerSquashAndStretch(0.3f, 0.25f);
        }

        /// <summary>
        /// Animation Event được gọi từ clip Slime B Attack.anim tại frame 0.667s (AE_SpitShoot)
        /// </summary>
        public void AE_SpitShoot()
        {
            if (CurrentState is RedSlimeBShootState shootState)
            {
                shootState.OnAnimationSpitEvent();
            }
        }

        public bool CanStartMerge()
        {
            if (!enableMerge) return false;
            if (currentForm == SlimeForm.SlimeA) return false; // Form A không cần gộp
            if (IsMerging || IsInvulnerable || IsDead || IsPopping) return false;

            return (Time.time - lastSplitOrFormChangeTime) >= timeBeforeMerge;
        }

        public RedSlimeBoss FindNearestSameFormPartner()
        {
            RedSlimeBoss nearest = null;
            float minDistance = float.MaxValue;

            for (int i = 0; i < ActiveSlimes.Count; i++)
            {
                var other = ActiveSlimes[i];
                if (other == null || other == this || other.IsDead || other.IsInvulnerable || other.IsMerging || other.IsPopping)
                    continue;

                if (other.CurrentForm == this.currentForm)
                {
                    float dist = Vector2.Distance(CenterPosition, other.CenterPosition);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        nearest = other;
                    }
                }
            }

            return nearest;
        }

        public void MergeWith(RedSlimeBoss partner)
        {
            if (IsMerging || partner == null || partner.IsMerging || partner.IsDead) return;

            // Xác định con giữ lại (Master) và con bị tiêu biến (Slave) dựa trên InstanceID để đồng bộ 2 phía
            if (this.GetInstanceID() > partner.GetInstanceID())
            {
                StartCoroutine(PerformMergeMasterRoutine(partner));
            }
        }

        private IEnumerator PerformMergeMasterRoutine(RedSlimeBoss partner)
        {
            IsMerging = true;
            partner.IsMerging = true;

            IsInvulnerable = true;
            partner.IsInvulnerable = true;

            StopHorizontalMovement();
            partner.StopHorizontalMovement();

            // Xác định form lớn hơn sau khi gộp: C -> B, B -> A
            SlimeForm prevForm = currentForm;
            SlimeForm mergedForm = (currentForm == SlimeForm.SlimeC) ? SlimeForm.SlimeB : SlimeForm.SlimeA;

            // Spawn VFX nổ gộp đặc trưng tại trung điểm 2 con
            Vector3 midPoint = (CenterPosition + partner.CenterPosition) * 0.5f;
            transform.position = midPoint;
            shaderController?.SpawnMergeVFX(prevForm, mergedForm, midPoint);
            shaderController?.TriggerScreenShake(0.3f, 0.25f);
            AudioController?.PlayMergeImpactSound();

            // Cơ chế cộng dồn máu khi gộp
            int combinedHealth = this.currentHealth + partner.CurrentHealth;
            // Max Health thì lấy cố định theo Form mới
            int combinedMaxHealth = GetMaxHealthForForm(mergedForm);

            // Chuyển bản thân thành mergedForm với lượng máu cộng dồn
            currentForm = mergedForm;
            currentHealth = combinedHealth;
            maxHealth = combinedMaxHealth;
            ApplyCurrentFormVisual();
            ResetMergeTimer();

            // Bảo toàn SharedUIDispatcher nếu con partner đang nắm giữ
            if (partner != null && SharedUIDispatcher == partner.uiEventDispatcher)
            {
                SharedUIDispatcher = this.uiEventDispatcher;
            }

            // Tiêu hủy con đối tác và xóa khỏi danh sách ngay lập tức để tránh tính đúp máu
            partner.currentHealth = 0;
            ActiveSlimes.Remove(partner);
            Destroy(partner.gameObject);

            NotifySwarmHealthChanged();

            // Nảy đàn hồi giãn nở khi hấp thụ thành công Slime mới
            shaderController?.TriggerSquashAndStretch(0.4f, 0.35f);

            yield return new WaitForSeconds(splitInvulnerableDuration);
            IsInvulnerable = false;
            IsMerging = false;

            ChangeState(ChaseState);
        }

        public void InitializeAsForm(SlimeForm form, int startingHealth = -1, int startingMaxHealth = -1)
        {
            currentForm = form;
            if (startingMaxHealth > 0)
            {
                maxHealth = startingMaxHealth;
            }
            if (startingHealth > 0)
            {
                currentHealth = startingHealth;
            }
            else
            {
                currentHealth = maxHealth;
            }

            IsInvulnerable = true;
            IsDead = false;
            IsMerging = false;
            IsPopping = true;
            MergePartner = null;

            if (uiEventDispatcher == null)
            {
                uiEventDispatcher = GetComponent<BossUIEventDispatcher>();
            }
            if (uiEventDispatcher != null)
            {
                uiEventDispatcher.SetUIPrefab(null);
            }

            // Vô hiệu hóa Canvas UI trên con clone để không bị nhân bản thêm thanh máu đè lên màn hình
            Canvas childCanvas = GetComponentInChildren<Canvas>(true);
            if (childCanvas != null)
            {
                childCanvas.gameObject.SetActive(false);
            }

            // Clone được Instantiate giữa một animation đang chạy, vì vậy bắt buộc trả
            // Animator, Rigidbody và VFX về trạng thái sạch trước khi hiện form mới.
            if (Rigidbody2D != null)
            {
                Rigidbody2D.linearVelocity = Vector2.zero;
                Rigidbody2D.angularVelocity = 0f;
            }

            if (Animator != null)
            {
                Animator.Rebind();
                Animator.Update(0f);
            }

            InitForms();
            ResetSlimeBElements();
            shaderController?.RefreshMaterials();
            ResetMergeTimer();

            StartCoroutine(TemporaryInvulnerability(splitInvulnerableDuration));
        }

        private IEnumerator TemporaryInvulnerability(float duration)
        {
            IsInvulnerable = true;
            yield return new WaitForSeconds(duration);
            IsInvulnerable = false;
        }

        private void Die()
        {
            IsDead = true;
            StopHorizontalMovement();
            AudioController?.PlayDeathSound();
            OnBossDied?.Invoke();

            NotifySwarmHealthChanged();

            // Nếu tất cả Slime trong bầy đều đã chết thì gọi UI kết thúc
            int remainingAliveCount = 0;
            for (int i = 0; i < ActiveSlimes.Count; i++)
            {
                if (ActiveSlimes[i] != null && ActiveSlimes[i] != this && !ActiveSlimes[i].IsDead)
                {
                    remainingAliveCount++;
                }
            }

            if (remainingAliveCount == 0)
            {
                if (SharedUIDispatcher != null)
                {
                    SharedUIDispatcher.Die();
                }
                PlayDeathCutscene();
            }

            ChangeState(DeadState);
        }

        private void PlayDeathCutscene()
        {
            if (deathCutsceneDirector == null) return;

            if (cutsceneDelay > 0f)
            {
                StartCoroutine(PlayCutsceneRoutine());
            }
            else
            {
                TriggerTimeline();
            }
        }

        private IEnumerator PlayCutsceneRoutine()
        {
            yield return new WaitForSeconds(cutsceneDelay);
            TriggerTimeline();
        }

        private void TriggerTimeline()
        {
            if (deathCutsceneDirector == null) return;
            if (!deathCutsceneDirector.gameObject.activeSelf)
            {
                deathCutsceneDirector.gameObject.SetActive(true);
            }
            deathCutsceneDirector.Stop();
            deathCutsceneDirector.Play();
        }

        // IEnemyTarget Interface Implementations
        public void ApplyStun(float duration) { }
        public void FlyToTarget(Vector3 targetPosition, float holdTime) { }
        public void ApplyVelocity(Vector2 velocity, bool forceDynamicBody) { }

        // ══════════════════════════════════════════════════
        //  GIZMOS
        // ══════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(CenterPosition, detectionRange);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(CenterPosition, attackRange);

            Vector2 attackPoint = (Vector2)CenterPosition + Vector2.right * FacingDirection * meleeVFXOffset.x;
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(attackPoint, meleeRadius);

            Vector2 slamPoint = (Vector2)GroundPosition + new Vector2(slamVFXOffset.x * FacingDirection, slamVFXOffset.y);
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
            Gizmos.DrawWireSphere(slamPoint, slamAOERadius);

            // Vẽ các điểm spawn dự kiến của Spike Wave từ slamPoint
            if (spikePrefab != null)
            {
                Gizmos.color = Color.red;
                for (int i = 0; i < spikeCountPerSide; i++)
                {
                    float offset = spikeStartOffset + (i * spikeSpacing);
                    Gizmos.DrawWireCube(slamPoint + new Vector2(offset, 0f), new Vector3(0.5f, 1f, 0f));
                    Gizmos.DrawWireCube(slamPoint + new Vector2(-offset, 0f), new Vector3(0.5f, 1f, 0f));
                }
            }

            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawWireSphere(CenterPosition, optimalSpacingDistance);
        }
    }
}
