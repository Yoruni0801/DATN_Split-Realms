using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Quoc.PuzzleQuest;
using Random = UnityEngine.Random;

namespace Bao.Bosses
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(Collider2D))]
    public class LithosMonolithBoss : MonoBehaviour, Bao.Enemies.IDamageable
    {
        public event Action OnBossDied;
        public static event Action OnGlobalLithosBossDied;

        // ══════════════════════════════════════════════════
        //  SERIALIZED FIELDS
        // ══════════════════════════════════════════════════

        [Header("Boss - Health & Phase")]
        [SerializeField] private int maxHealth = 500;
        [SerializeField] private int currentHealth;
        [Range(0f, 1f)]
        [SerializeField] private float phase2Threshold = 0.5f;
        [SerializeField] private BossPhase currentPhase = BossPhase.Phase1;
        [SerializeField] private GameObject phaseTransitionVFXPrefab;
        [SerializeField] private float phaseTransitionDuration = 2.5f;

        [Header("Boss - Detection (Stationary Boss)")]
        [SerializeField] private float detectionRange = 18f;
        [SerializeField] private float attackRange = 14f;
        [SerializeField] private LayerMask environmentLayer;
        [SerializeField] private LayerMask playerLayer;

        [Header("Boss - AI Behavior & Pacing")]
        [SerializeField] private float globalActionCooldown = 2.0f;

        [Header("Combo Cooldown")]
        [SerializeField, Min(0f)] private float comboCooldown = 2f;
        [SerializeField] private bool facePlayerDuringCooldown = true;

        [Header("Boss - Rock Throw (Phase 1 & 2)")]
        [SerializeField] private int rockThrowDamage = 15;
        [SerializeField] private float rockThrowCooldown = 3.0f;
        [SerializeField] private float rockThrowSpeed = 12f;
        [SerializeField] private float rockThrowAnimDuration = 1.2f;
        [SerializeField] private float rockThrowDelay = 0.4f;
        [SerializeField] private GameObject[] rockProjectiles;

        [Header("Boss - Orbit AOE (Phase 2 Only)")]
        [SerializeField] private int orbitDamage = 10;
        [SerializeField] private float orbitCooldown = 12f;
        [SerializeField] private float orbitRadius = 5f;
        [SerializeField] private float orbitSpeed = 180f;
        [SerializeField] private float orbitDuration = 5f;
        [SerializeField] private float orbitAnimDuration = 6f;
        [SerializeField] private float orbitDamageInterval = 0.5f;
        [SerializeField] private float orbitDamageRadius = 1.5f;

        [Header("Boss - Rock Barrage (Phase 2 Only)")]
        [SerializeField] private int barrageDamage = 12;
        [SerializeField] private float barrageCooldown = 10f;
        [SerializeField] private float barrageSpeed = 15f;
        [SerializeField] private float barrageAnimDuration = 3f;
        [SerializeField] private float barrageDelay = 0.6f;
        [SerializeField] private int barrageCount = 6;
        [SerializeField] private float barrageInterval = 0.3f;

        [Header("Boss - Orbit Stone References")]
        [SerializeField] private Transform[] orbitStones;

        [Header("Boss - Rune Effects (Phase 2 Visuals)")]
        [SerializeField] private GameObject[] runeEffects;

        [Header("Boss - Debug & Visuals")]
        [SerializeField] private SpriteRenderer[] spriteRenderers;
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private bool testForcePhase2 = false;
        [SerializeField] private LithosMonolithShaderController shaderController;

        // ══════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════

        public int MaxHealth => maxHealth;
        public int CurrentHealth => currentHealth;
        public BossPhase CurrentPhase => currentPhase;
        public float DetectionRange => detectionRange;
        public float AttackRange => attackRange;
        public LayerMask EnvironmentLayer => environmentLayer;
        public LayerMask PlayerLayer => playerLayer;
        public float GlobalActionCooldown => globalActionCooldown;
        public float ComboCooldown => comboCooldown;
        public bool FacePlayerDuringCooldown => facePlayerDuringCooldown;

        // Rock Throw
        public int RockThrowDamage => rockThrowDamage;
        public float RockThrowCooldown => rockThrowCooldown;
        public float RockThrowSpeed => rockThrowSpeed;
        public float RockThrowAnimDuration => rockThrowAnimDuration;
        public float RockThrowDelay => rockThrowDelay;
        public GameObject[] RockProjectiles => rockProjectiles;

        // Orbit AOE
        public int OrbitDamage => orbitDamage;
        public float OrbitCooldown => orbitCooldown;
        public float OrbitRadius => orbitRadius;
        public float OrbitSpeed => orbitSpeed;
        public float OrbitDuration => orbitDuration;
        public float OrbitAnimDuration => orbitAnimDuration;
        public float OrbitDamageInterval => orbitDamageInterval;
        public float OrbitDamageRadius => orbitDamageRadius;

        // Rock Barrage
        public int BarrageDamage => barrageDamage;
        public float BarrageCooldown => barrageCooldown;
        public float BarrageSpeed => barrageSpeed;
        public float BarrageAnimDuration => barrageAnimDuration;
        public float BarrageDelay => barrageDelay;
        public int BarrageCount => barrageCount;
        public float BarrageInterval => barrageInterval;

        // Orbit Stones
        public Transform[] OrbitStones => orbitStones;
        public GameObject[] RuneEffects => runeEffects;

        // References
        public Rigidbody2D Rigidbody2D { get; private set; }
        public Animator Animator { get; private set; }
        public Collider2D Collider2D { get; private set; }
        public Transform Target { get; private set; }
        public SpriteRenderer[] SpriteRenderers => spriteRenderers;
        public LithosMonolithShaderController ShaderController => shaderController;

        public bool IsInvulnerable { get; set; } = false;
        public bool IsTransitioningPhases { get; private set; } = false;
        public bool IsDead { get; private set; } = false;
        public bool IsCoolingDown { get; private set; } = false;

        public Vector3 CenterPosition => Collider2D != null ? Collider2D.bounds.center : transform.position;

        // ══════════════════════════════════════════════════
        //  STATE MACHINE
        // ══════════════════════════════════════════════════

        public LithosMonolithBossState CurrentState { get; private set; }

        public enum SkillCategory { None, Ranged, AOE }
        public SkillCategory LastUsedSkillCategory { get; private set; } = SkillCategory.None;

        // Cooldown Timers
        private float lastRockThrowTime = -99f;
        private float lastOrbitTime = -99f;
        private float lastBarrageTime = -99f;
        private float lastActionTime = -99f;
        private Coroutine cooldownCoroutine;
        private bool hasEngaged = false;

        // Original positions for orbit stones (to reset after orbit)
        private Vector3[] orbitStoneOriginalLocalPositions;

        // Prebuilt States
        public LithosIdleState IdleState { get; private set; }
        public LithosRockThrowState RockThrowState { get; private set; }
        public LithosOrbitAOEState OrbitAOEState { get; private set; }
        public LithosRockBarrageState RockBarrageState { get; private set; }
        public LithosCooldownState CooldownState { get; private set; }
        public LithosDeadState DeadState { get; private set; }

        // ══════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════

        private void Awake()
        {
            Rigidbody2D = GetComponent<Rigidbody2D>();
            Animator = GetComponent<Animator>();
            Collider2D = GetComponent<Collider2D>();

            // Stationary boss: freeze all
            Rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            currentHealth = maxHealth;

            if (spriteRenderers == null || spriteRenderers.Length == 0)
                spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

            if (shaderController == null)
                shaderController = GetComponentInChildren<LithosMonolithShaderController>();

            // Save original orbit stone positions
            if (orbitStones != null && orbitStones.Length > 0)
            {
                orbitStoneOriginalLocalPositions = new Vector3[orbitStones.Length];
                for (int i = 0; i < orbitStones.Length; i++)
                {
                    if (orbitStones[i] != null)
                        orbitStoneOriginalLocalPositions[i] = orbitStones[i].localPosition;
                }
            }

            // Hide rune effects in Phase 1
            SetRuneEffectsActive(false);

            // Create all states
            IdleState = new LithosIdleState(this);
            RockThrowState = new LithosRockThrowState(this);
            OrbitAOEState = new LithosOrbitAOEState(this);
            RockBarrageState = new LithosRockBarrageState(this);
            CooldownState = new LithosCooldownState(this);
            DeadState = new LithosDeadState(this);
        }

        private void OnEnable()
        {
            PuzzleQuestController.OnPuzzleBossDefeated += HandlePuzzleBossDefeated;
            if (PuzzleQuestController.IsPuzzleBossDefeated && !IsDead)
            {
                Die();
            }
        }

        private void OnDisable()
        {
            PuzzleQuestController.OnPuzzleBossDefeated -= HandlePuzzleBossDefeated;
        }

        private void HandlePuzzleBossDefeated()
        {
            if (!IsDead)
            {
                Die();
            }
        }

        private void Start()
        {
            if (PuzzleQuestController.IsPuzzleBossDefeated)
            {
                Die();
                return;
            }

            FindPlayer();
            ChangeState(IdleState);
        }

        private void Update()
        {
            if (IsDead) return;

            if (Target == null)
                FindPlayer();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Input.GetKeyDown(KeyCode.F3))
                ForcePhase2();
            if (Input.GetKeyDown(KeyCode.F4))
                DebugTakeDamage();
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

        // ══════════════════════════════════════════════════
        //  STATE MACHINE METHODS
        // ══════════════════════════════════════════════════

        public void ChangeState(LithosMonolithBossState newState)
        {
            if ((IsDead && newState != DeadState) || newState == null || CurrentState == newState) return;

            string oldStateName = CurrentState != null ? CurrentState.GetType().Name : "None";
            Debug.Log($"[LithosMonolith] Changing state from {oldStateName} to {newState.GetType().Name}");

            CurrentState?.Exit();
            CurrentState = newState;
            CurrentState.Enter();
        }

        // ══════════════════════════════════════════════════
        //  DETECTION
        // ══════════════════════════════════════════════════

        private void FindPlayer()
        {
            if (PlayerRegistry.Current != null)
            {
                Target = PlayerRegistry.Current.transform;
                return;
            }
        }

        public bool HasTargetInDetectionRange()
        {
            if (Target == null) return false;

            if (hasEngaged) return true;

            if (Vector2.Distance(CenterPosition, Target.position) <= detectionRange)
            {
                hasEngaged = true;
                return true;
            }

            if (currentPhase == BossPhase.Phase2 || currentHealth < maxHealth)
            {
                hasEngaged = true;
                return true;
            }

            return false;
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

        /// <summary>Returns normalized direction toward player.</summary>
        public Vector2 GetDirectionToTarget()
        {
            if (Target == null) return Vector2.right;
            return ((Vector2)Target.position - (Vector2)CenterPosition).normalized;
        }

        // ══════════════════════════════════════════════════
        //  PACING & SKILL MEMORY
        // ══════════════════════════════════════════════════

        public bool CanPerformAction() => Time.time >= lastActionTime + globalActionCooldown;

        public void MarkActionUsed(SkillCategory category)
        {
            lastActionTime = Time.time;
            LastUsedSkillCategory = category;
        }

        public void DelayActionBy(float extraTime)
        {
            lastActionTime = Mathf.Max(lastActionTime, Time.time) + extraTime;
        }

        public void ForceReadyToAct()
        {
            lastActionTime = Time.time - globalActionCooldown;
        }

        public void SetTarget(Transform t)
        {
            if (t != null) Target = t;
        }

        public void ForceAttack()
        {
            if (IsDead || IsTransitioningPhases) return;

            // Ensure cooldown timers are fresh so CanRockThrow() passes
            ForceReadyToAct();
            MarkRockThrowUsed();

            ChangeState(IdleState);
            ChangeState(RockThrowState);
        }

        // ══════════════════════════════════════════════════
        //  SKILL COOLDOWNS
        // ══════════════════════════════════════════════════

        public bool CanRockThrow()
        {
            return Time.time >= lastRockThrowTime + rockThrowCooldown
                && !IsTransitioningPhases && CanPerformAction();
        }
        public void MarkRockThrowUsed()
        {
            lastRockThrowTime = Time.time;
            MarkActionUsed(SkillCategory.Ranged);
        }

        public bool CanOrbit()
        {
            return currentPhase == BossPhase.Phase2
                && Time.time >= lastOrbitTime + orbitCooldown
                && !IsTransitioningPhases && CanPerformAction();
        }
        public void MarkOrbitUsed()
        {
            lastOrbitTime = Time.time;
            MarkActionUsed(SkillCategory.AOE);
        }

        public bool CanBarrage()
        {
            return currentPhase == BossPhase.Phase2
                && Time.time >= lastBarrageTime + barrageCooldown
                && !IsTransitioningPhases && CanPerformAction();
        }
        public void MarkBarrageUsed()
        {
            lastBarrageTime = Time.time;
            MarkActionUsed(SkillCategory.Ranged);
        }

        // ══════════════════════════════════════════════════
        //  RUNE EFFECTS MANAGEMENT
        // ══════════════════════════════════════════════════

        public void SetRuneEffectsActive(bool active)
        {
            if (runeEffects == null) return;
            foreach (var r in runeEffects)
            {
                if (r != null) r.SetActive(active);
            }
        }

        // ══════════════════════════════════════════════════
        //  ORBIT STONE MANAGEMENT
        // ══════════════════════════════════════════════════

        public void ResetOrbitStonePositions()
        {
            if (orbitStones == null || orbitStoneOriginalLocalPositions == null) return;
            for (int i = 0; i < orbitStones.Length; i++)
            {
                if (orbitStones[i] != null)
                    orbitStones[i].localPosition = orbitStoneOriginalLocalPositions[i];
            }
        }

        public void StopMoving()
        {
            if (Rigidbody2D != null)
            {
                Rigidbody2D.linearVelocity = Vector2.zero;
                Rigidbody2D.angularVelocity = 0f;
            }
        }

        public void FacePosition(Vector2 position)
        {
            // Lithos is a stationary monolith; keep this hook for shared cooldown behavior.
        }

        public void EnterCooldownState()
        {
            StopMoving();

            if (Animator != null)
            {
                Animator.SetBool("isIdle", false);
                Animator.SetBool("isIdlePhase2", false);
                Animator.SetBool("isOrbit", false);
                Animator.speed = 0f;
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

            if (HasTargetInAttackRange() || IsTargetWithinDetectionRange())
            {
                ChangeState(IdleState);
            }
            else
            {
                ChangeState(IdleState);
            }
        }

        // ══════════════════════════════════════════════════
        //  RENDERER ALPHA
        // ══════════════════════════════════════════════════

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

        // ══════════════════════════════════════════════════
        //  DAMAGE & PHASE TRANSITION
        // ══════════════════════════════════════════════════

        public void TakeDamage(int damage)
        {
            if (IsInvulnerable || IsTransitioningPhases || IsDead) return;

            currentHealth -= damage;
            Debug.Log($"Lithos Monolith took {damage} damage! HP: {currentHealth}/{maxHealth}");

            global::DamagePopupSystem.ShowEnemy(CenterPosition + Vector3.up, damage);
            shaderController?.TriggerHitFlash();

            if (currentHealth <= 0)
            {
                Die();
                return;
            }

            if (currentPhase == BossPhase.Phase1 && currentHealth <= maxHealth * phase2Threshold)
            {
                StartCoroutine(TransitionToPhase2Routine());
            }
        }

        [ContextMenu("Debug - Take 50 Damage")]
        public void DebugTakeDamage()
        {
            TakeDamage(50);
        }

        [ContextMenu("Force Phase 2")]
        public void ForcePhase2()
        {
            if (IsDead || currentPhase == BossPhase.Phase2 || IsTransitioningPhases) return;

            Debug.Log("[LithosMonolith] Forcing Phase 2!");
            int thresholdHealth = Mathf.FloorToInt(maxHealth * phase2Threshold);
            if (currentHealth > thresholdHealth)
                currentHealth = thresholdHealth;

            StartCoroutine(TransitionToPhase2Routine());
        }

        private IEnumerator TransitionToPhase2Routine()
        {
            IsTransitioningPhases = true;
            IsInvulnerable = true;
            currentPhase = BossPhase.Phase2;

            Debug.Log("[LithosMonolith] Phase 2 Transition started!");

            if (Animator != null)
                Animator.SetTrigger("PhaseTransition");

            // Dramatic shake
            float elapsed = 0f;
            Vector3 basePos = transform.position;
            while (elapsed < phaseTransitionDuration * 0.6f)
            {
                elapsed += Time.deltaTime;
                float shakeIntensity = Mathf.Lerp(0.01f, 0.08f, elapsed / (phaseTransitionDuration * 0.6f));
                transform.position = basePos + (Vector3)(Random.insideUnitCircle * shakeIntensity);
                yield return null;
            }
            transform.position = basePos;

            // Reveal rune effects with staggered activation
            if (runeEffects != null)
            {
                for (int i = 0; i < runeEffects.Length; i++)
                {
                    if (runeEffects[i] != null)
                    {
                        runeEffects[i].SetActive(true);
                        shaderController?.SpawnRuneRevealVFX(runeEffects[i].transform.position);
                    }
                    yield return new WaitForSeconds(0.2f);
                }
            }

            // VFX burst
            if (phaseTransitionVFXPrefab != null)
            {
                GameObject vfx = Instantiate(phaseTransitionVFXPrefab, CenterPosition, Quaternion.identity);
                Destroy(vfx, phaseTransitionDuration + 1f);
            }

            shaderController?.TriggerScreenShake(0.4f, 0.15f);

            yield return new WaitForSeconds(phaseTransitionDuration * 0.4f);

            IsTransitioningPhases = false;
            IsInvulnerable = false;

            Debug.Log("[LithosMonolith] Phase 2 active! Runes revealed!");

            // Begin Phase 2 with orbit AOE
            ChangeState(OrbitAOEState);
        }

        public void Die()
        {
            if (IsDead) return;
            IsDead = true;
            ExitCooldownState();
            StopMoving();
            ChangeState(DeadState);
            OnBossDied?.Invoke();
            OnGlobalLithosBossDied?.Invoke();
        }

        // ══════════════════════════════════════════════════
        //  GIZMOS
        // ══════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            // Detection range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(CenterPosition, detectionRange);

            // Attack range
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(CenterPosition, attackRange);

            // Orbit radius
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireSphere(CenterPosition, orbitRadius);

            // Orbit damage radius (sample at 4 points)
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f * Mathf.Deg2Rad;
                Vector3 pos = CenterPosition + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * orbitRadius;
                Gizmos.DrawWireSphere(pos, orbitDamageRadius);
            }
        }
    }
}
