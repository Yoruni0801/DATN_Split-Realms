using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace EnemySystem
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class StandaloneFlyingShooterEnemy : MonoBehaviour, IParryable
    {
        private enum AttackType
        {
            RangedSnipe,     // Bắn tỉa 1 lông vũ ngắm thẳng người chơi
            TripleSpread,    // Bắn chùm 3 lông vũ tỏa quạt
            HorizontalDash   // Đứng gồng tại chỗ rồi lao thẳng theo trục ngang qua Player
        }

        [Header("Health & UI")]
        [SerializeField] private int maxHealth = 30;
        [SerializeField] private GameObject deathVFXPrefab;

        [Header("Drop On Death (Vật phẩm lơ lửng khi chim chết)")]
        [SerializeField] private GameObject childDropObject;
        [SerializeField] private bool floatInAir = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onDiedEvent;
        public event Action OnDied;

        [Header("Movement & Navigation")]
        [SerializeField] private float patrolSpeed = 2.5f;
        [SerializeField] private float chaseSpeed = 3.8f;
        [SerializeField] private float retreatSpeed = 4.5f;
        [SerializeField] private float minSafeDistance = 3.5f;
        [SerializeField] private float patrolDistance = 8f;
        [SerializeField] private bool invertFacing = true;

        [Header("Combat General Settings")]
        [SerializeField] private float detectionRange = 12f;
        [SerializeField] private float attackRange = 7.5f;
        [SerializeField] private float minAttackCooldown = 1.4f;
        [SerializeField] private float maxAttackCooldown = 2.0f;
        [SerializeField] private float telegraphDelay = 0.4f;

        [Header("Skill 1: Horizontal Dash (Lao ngang - Parry được)")]
        [SerializeField] private float dashSpeed = 16f;
        [SerializeField] private int dashDamage = 15;
        [SerializeField] private int stunDamageOnParried = 5;

        [Header("Skill 2 & 3: Projectile Attacks (Parry được đạn)")]
        [SerializeField] private float projectileSpeed = 14f;
        [SerializeField] private float projectileSpawnRotation = 0f;
        [SerializeField] private float projectileLifetime = 3.5f;
        [SerializeField] private int projectileDamage = 10;
        [SerializeField] private GameObject featherProjectilePrefab;
        [SerializeField] private Transform shootPoint;
        [SerializeField] private LayerMask playerLayer;

        [Header("Warning Indicator")]
        [SerializeField] private GameObject warningObject;

        [Header("Timeline Control")]
        [SerializeField] private bool waitTimelineAggro = false;

        [Header("Animation Parameters")]
        private Animator animator;
        private string walkBool = "isWalk";
        private string idleBool = "isIdle";
        private string attackBool = "isAttack";
        private string attackTrigger = "Attack";
        private string hurtTrigger = "Hurt";
        private string dieTrigger = "Die";

        private Rigidbody2D rb;
        private Collider2D col;
        private Vector2 spawnPosition;
        private float fixedFlightY;
        private int currentHealth;
        private bool isDead;
        private bool isAttacking;
        private bool isDashing;
        private bool isAggroEnabled = true;
        private int currentFacingDirection = 1;
        private float currentHorizontalVelocity;
        private float nextAttackTime = -999f;
        private AttackType lastAttackType = AttackType.HorizontalDash;
        private Transform currentTarget;
        private CancellationTokenSource combatCts;
        private float targetSearchTimer;
        private Bao.Bosses.Ashwing.AshwingBossCombat bossCombat;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsDead => isDead;
        public bool IsAggroEnabled => isAggroEnabled;
        public Transform ShootPoint => shootPoint;
        public bool HasBossCombat => bossCombat != null;

        private void Awake()
        {
            bossCombat = GetComponent<Bao.Bosses.Ashwing.AshwingBossCombat>();
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.linearVelocity = Vector2.zero;

            currentHealth = maxHealth;
            // A bound intro owns aggro, independently of Awake execution order.
            isAggroEnabled = !waitTimelineAggro && GetComponent<Duy.Timeline.TimelineEnemyAggroController>() == null;

            if (warningObject != null)
            {
                warningObject.SetActive(false);
            }
        }

        private void Start()
        {
            spawnPosition = transform.position;
            fixedFlightY = transform.position.y;
            combatCts = new CancellationTokenSource();
            ApplyFacing(currentFacingDirection);
        }

        private void Update()
        {
            if (isDead) return;
            if (bossCombat != null) return;

#if UNITY_EDITOR
            // Phím tắt Debug: Bấm phím 0 hoặc Numpad0 để diệt chim lập tức
            if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0))
            {
                Die();
                return;
            }
#endif

            // KHÓA CHẶT ĐỘ CAO Y - TUYỆT ĐỐI KHÔNG BAO GIỜ BAY LÊN TRỜI HAY LỆCH TRỤC Y
            Vector3 pos = transform.position;
            pos.y = fixedFlightY;
            transform.position = pos;

            UpdateTarget();

            if (isAggroEnabled && CanStartAttack())
            {
                _ = ExecuteRandomDistinctSkillAsync(combatCts.Token);
                return;
            }

            if (!isAttacking)
            {
                if (isAggroEnabled && currentTarget != null && HasTargetInDetection())
                {
                    HandleCombatNavigation();
                }
                else
                {
                    PatrolHorizontal();
                }
            }
        }

        private void FixedUpdate()
        {
            if (bossCombat != null) return;
            if (isDead || isAttacking)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            rb.linearVelocity = new Vector2(currentHorizontalVelocity, 0f);
        }

        private void UpdateTarget()
        {
            if (PlayerRegistry.Current != null)
            {
                currentTarget = PlayerRegistry.Current.transform;
                return;
            }

            targetSearchTimer -= Time.deltaTime;
            if (targetSearchTimer <= 0f)
            {
                targetSearchTimer = 0.25f; // Quét định kỳ mỗi 0.25s thay vì mỗi frame
                Collider2D hit = Physics2D.OverlapCircle(transform.position, detectionRange, playerLayer);
                currentTarget = hit != null ? hit.transform : null;
            }
        }

        private bool HasTargetInDetection()
        {
            if (currentTarget == null) return false;
            float sqrDist = ((Vector2)currentTarget.position - (Vector2)transform.position).sqrMagnitude;
            return sqrDist <= detectionRange * detectionRange;
        }

        private bool CanStartAttack()
        {
            if (isAttacking || currentTarget == null) return false;
            if (Time.time < nextAttackTime) return false;

            float sqrDist = ((Vector2)currentTarget.position - (Vector2)transform.position).sqrMagnitude;
            return sqrDist <= attackRange * attackRange;
        }

        private void HandleCombatNavigation()
        {
            float dx = currentTarget.position.x - transform.position.x;
            float absDistance = Mathf.Abs(dx);
            int dirToPlayer = dx >= 0 ? 1 : -1;

            currentFacingDirection = dirToPlayer;
            ApplyFacing(currentFacingDirection);

            // NẾU NGƯỜI CHƠI ÁP SÁT QUÁ GẦN: LÙI RA ĐỂ THẢ DIỀU
            if (absDistance < minSafeDistance)
            {
                currentHorizontalVelocity = -dirToPlayer * retreatSpeed;
                SetAnimationBool(walkBool, true);
                SetAnimationBool(idleBool, false);
                return;
            }

            // NẾU TRONG TẦM BẮN LÝ TƯỞNG: DỪNG LẠI CHUẨN BỊ XẢ CHIÊU
            if (absDistance <= attackRange * 0.85f)
            {
                currentHorizontalVelocity = 0f;
                SetAnimationBool(walkBool, false);
                SetAnimationBool(idleBool, true);
                return;
            }

            // NẾU Ở XA: BAY TIẾP CẬN THEO TRỤC NGANG
            currentHorizontalVelocity = dirToPlayer * chaseSpeed;
            SetAnimationBool(walkBool, true);
            SetAnimationBool(idleBool, false);
        }

        private void PatrolHorizontal()
        {
            float halfDist = patrolDistance * 0.5f;
            float currentX = transform.position.x;

            if (currentX >= spawnPosition.x + halfDist)
            {
                currentFacingDirection = -1;
            }
            else if (currentX <= spawnPosition.x - halfDist)
            {
                currentFacingDirection = 1;
            }

            currentHorizontalVelocity = currentFacingDirection * patrolSpeed;
            ApplyFacing(currentFacingDirection);
            SetAnimationBool(walkBool, true);
            SetAnimationBool(idleBool, false);
        }

        private async Task ExecuteRandomDistinctSkillAsync(CancellationToken token)
        {
            isAttacking = true;
            nextAttackTime = Time.time + UnityEngine.Random.Range(minAttackCooldown, maxAttackCooldown);
            currentHorizontalVelocity = 0f;
            rb.linearVelocity = Vector2.zero;

            SetAnimationBool(walkBool, false);
            SetAnimationBool(idleBool, true);

            AttackType chosenAttack = (AttackType)UnityEngine.Random.Range(0, 3);
            if (chosenAttack == lastAttackType)
            {
                chosenAttack = (AttackType)(((int)chosenAttack + 1) % 3);
            }
            lastAttackType = chosenAttack;

            switch (chosenAttack)
            {
                case AttackType.HorizontalDash:
                    await ExecuteHorizontalDashAttackAsync(token);
                    break;
                case AttackType.TripleSpread:
                    await ExecuteTripleSpreadAttackAsync(token);
                    break;
                case AttackType.RangedSnipe:
                default:
                    await ExecuteRangedSnipeAttackAsync(token);
                    break;
            }

            CleanUpAttackState();
        }

        // ==================== CHIÊU 1: BẮN TỈA TẦM XA (RANGED SNIPE - PARRY ĐƯỢC ĐẠN) ====================
        private async Task ExecuteRangedSnipeAttackAsync(CancellationToken token)
        {
            if (currentTarget != null)
            {
                int dirToTarget = currentTarget.position.x >= transform.position.x ? 1 : -1;
                ApplyFacing(dirToTarget);
            }

            ShowWarning(true);
            TriggerAnimation(attackTrigger);
            SetAnimationBool(attackBool, true);

            try
            {
                await Task.Delay((int)(telegraphDelay * 1000f), token);
            }
            catch { return; }

            ShowWarning(false);
            if (isDead) return;

            SpawnFeatherInDirection(GetDirectionToPlayer());

            try
            {
                await Task.Delay(300, token);
            }
            catch { }
        }

        // ==================== CHIÊU 2: BẮN CHÙM 3 LÔNG VŨ TỎA QUẠT (TRIPLE SPREAD) ====================
        private async Task ExecuteTripleSpreadAttackAsync(CancellationToken token)
        {
            if (currentTarget != null)
            {
                int dirToTarget = currentTarget.position.x >= transform.position.x ? 1 : -1;
                ApplyFacing(dirToTarget);
            }

            ShowWarning(true);
            TriggerAnimation(attackTrigger);
            SetAnimationBool(attackBool, true);

            try
            {
                await Task.Delay((int)(telegraphDelay * 1000f), token);
            }
            catch { return; }

            ShowWarning(false);
            if (isDead) return;

            Vector2 baseDir = GetDirectionToPlayer();
            float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

            // Bắn 3 hướng tỏa quạt (-15 độ, 0 độ, +15 độ)
            float[] angles = new float[] { -15f, 0f, 15f };
            for (int i = 0; i < angles.Length; i++)
            {
                float finalAngle = (baseAngle + angles[i]) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(finalAngle), Mathf.Sin(finalAngle));
                SpawnFeatherInDirection(dir);
            }

            try
            {
                await Task.Delay(350, token);
            }
            catch { }
        }

        // ==================== CHIÊU 3: ĐỨNG YÊN GỒNG RỒI LAO NGANG QUA PLAYER (HORIZONTAL DASH - PARRY ĐƯỢC) ====================
        private async Task ExecuteHorizontalDashAttackAsync(CancellationToken token)
        {
            if (currentTarget == null) return;

            int dirToTarget = currentTarget.position.x >= transform.position.x ? 1 : -1;
            ApplyFacing(dirToTarget);

            // Giai đoạn 1: Đứng yên tại chỗ gồng đòn
            ShowWarning(true);
            try
            {
                await Task.Delay((int)(telegraphDelay * 1000f), token);
            }
            catch { return; }

            ShowWarning(false);
            if (isDead) return;

            TriggerAnimation(attackTrigger);
            SetAnimationBool(attackBool, true);
            isDashing = true;

            // Giai đoạn 2: Lao ngang trên đúng trục fixedFlightY qua vị trí Player
            float startX = transform.position.x;
            float targetX = currentTarget.position.x + (dirToTarget * 3.5f);
            float totalDistance = Mathf.Abs(targetX - startX);
            float dashDuration = Mathf.Max(0.15f, totalDistance / dashSpeed);
            float elapsed = 0f;
            bool hasDealtDamage = false;

            while (elapsed < dashDuration && isDashing)
            {
                if (isDead || token.IsCancellationRequested)
                {
                    isDashing = false;
                    return;
                }

                elapsed += Time.deltaTime;
                float t = elapsed / dashDuration;
                float currentX = Mathf.Lerp(startX, targetX, t);

                transform.position = new Vector3(currentX, fixedFlightY, transform.position.z);

                // Check va chạm trúng người chơi (Có hỗ trợ PARRY)
                if (!hasDealtDamage)
                {
                    Collider2D hitPlayer = Physics2D.OverlapCircle(transform.position, 1.2f, playerLayer);
                    if (hitPlayer != null)
                    {
                        IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                            hitPlayer,
                            dashDamage,
                            ParryableAttackType.Melee,
                            this,
                            transform,
                            new Vector2(dirToTarget, 0f));

                        hasDealtDamage = true;

                        if (resolution == IncomingAttackResolution.Parried)
                        {
                            isDashing = false;
                            break;
                        }
                    }
                }

                await Task.Yield();
            }

            isDashing = false;

            try
            {
                await Task.Delay(200, token);
            }
            catch { }
        }

        // Khi người chơi Parry trúng cú lao ngang của chim:
        public ParryResult OnParried(ParryContext context)
        {
            if (bossCombat != null) return bossCombat.HandleParry(context);
            isDashing = false;

            // Chim bị khựng lại, dính sát thương phản đòn và giật lùi theo trục X
            TakeDamage(stunDamageOnParried);
            TriggerAnimation(hurtTrigger);

            float recoilDirX = context.IncomingDirection.x > 0 ? -1f : 1f;
            transform.position += new Vector3(recoilDirX * 1.5f, 0f, 0f);

            return ParryResult.Successful(0.15f, true);
        }

        private Vector2 GetDirectionToPlayer()
        {
            Transform spawnPointTransform = shootPoint != null ? shootPoint : transform;
            Vector2 shotDirection = currentFacingDirection * Vector2.right;

            if (currentTarget != null)
            {
                Vector2 toTarget = (Vector2)currentTarget.position - (Vector2)spawnPointTransform.position;
                if (toTarget.sqrMagnitude > 0.001f)
                {
                    shotDirection = toTarget.normalized;
                }
            }

            return shotDirection;
        }

        private void SpawnFeatherInDirection(Vector2 direction)
        {
            if (featherProjectilePrefab == null) return;

            Transform spawnPointTransform = shootPoint != null ? shootPoint : transform;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + projectileSpawnRotation;
            GameObject proj = Instantiate(featherProjectilePrefab, spawnPointTransform.position, Quaternion.Euler(0f, 0f, angle));
            if (proj.TryGetComponent<Bao.Enemies.RavagerProjectile>(out var feather))
                feather.Launch(direction, projectileSpeed, projectileDamage);

            // Tự động hủy đạn sau thời gian bay để triệt tiêu memory leak và giải phóng VFX
            Destroy(proj, projectileLifetime);

            if (proj.TryGetComponent<Rigidbody2D>(out var projRb))
            {
                projRb.gravityScale = 0f;
                projRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                projRb.linearVelocity = direction * projectileSpeed;
            }

            // Gắn adapter Parryable vào đạn để người chơi chém phản đòn dội ngược lại trúng chim
            var adapter = ParryableProjectileAdapter.Ensure(proj.transform);
            if (adapter != null)
            {
                adapter.TrackMotion(direction * projectileSpeed);
            }
        }

        private void ShowWarning(bool show)
        {
            if (warningObject != null)
            {
                warningObject.SetActive(show);
            }
        }

        private void CleanUpAttackState()
        {
            ShowWarning(false);
            SetAnimationBool(attackBool, false);
            isAttacking = false;
            isDashing = false;
        }

        private void ApplyFacing(int direction)
        {
            if (direction == 0) return;
            currentFacingDirection = direction > 0 ? 1 : -1;
            Vector3 scale = transform.localScale;
            float multiplier = invertFacing ? -1f : 1f;
            scale.x = Mathf.Abs(scale.x) * currentFacingDirection * multiplier;
            transform.localScale = scale;
        }

        private void SetAnimationBool(string paramName, bool value)
        {
            if (animator == null || string.IsNullOrEmpty(paramName)) return;
            foreach (var parameter in animator.parameters)
            {
                if (parameter.name != paramName) continue;
                if (parameter.type == AnimatorControllerParameterType.Bool) animator.SetBool(paramName, value);
                else if (parameter.type == AnimatorControllerParameterType.Trigger && value) animator.SetTrigger(paramName);
                return;
            }
        }

        private void TriggerAnimation(string paramName)
        {
            if (animator == null || string.IsNullOrEmpty(paramName)) return;
            foreach (var parameter in animator.parameters)
                if (parameter.name == paramName && parameter.type == AnimatorControllerParameterType.Trigger)
                    animator.SetTrigger(paramName);
        }

        public void EnableAggro()
        {
            isAggroEnabled = true;
        }

        public void DisableAggro()
        {
            isAggroEnabled = false;
            if (bossCombat != null) bossCombat.SuspendCombat();
        }

        public void TakeDamage(int damage)
        {
            if (isDead) return;

            if (damage <= 0) return;
            currentHealth = Mathf.Max(0, currentHealth - damage);
            if (bossCombat != null) bossCombat.OnDamaged();

            TriggerAnimation(hurtTrigger);

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        public void TakeDamage(float damage)
        {
            TakeDamage(Mathf.RoundToInt(damage));
        }

        public void Die()
        {
            if (isDead) return;
            isDead = true;
            if (bossCombat != null) bossCombat.OnBossDeath();

            combatCts?.Cancel();
            rb.linearVelocity = Vector2.zero;
            if (col != null) col.enabled = false;

            ShowWarning(false);

            if (deathVFXPrefab != null && bossCombat == null)
            {
                Instantiate(deathVFXPrefab, transform.position, Quaternion.identity);
            }

            ReleaseChildDropObject();

            try
            {
                onDiedEvent?.Invoke();
                OnDied?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StandaloneFlyingShooterEnemy] Error in OnDied callback: {ex.Message}");
            }

            TriggerAnimation(dieTrigger);
            Destroy(gameObject, bossCombat != null ? 1.8f : 1.2f);
        }

        private void ReleaseChildDropObject()
        {
            if (childDropObject == null) return;

            childDropObject.transform.SetParent(null);
            childDropObject.SetActive(true);

            if (floatInAir)
            {
                if (childDropObject.TryGetComponent<Rigidbody2D>(out var dropRb))
                {
                    dropRb.gravityScale = 0f;
                    dropRb.linearVelocity = Vector2.zero;
                }
            }

            // Kích hoạt drop cho CrystalPickupItem hoặc ProximityItemPickup (quét cả trong children và root)
            CrystalPickupItem crystalPickup = childDropObject.GetComponentInChildren<CrystalPickupItem>(true);
            if (crystalPickup != null)
            {
                crystalPickup.ActivateDrop();
            }

            Duy.Interaction.ProximityItemPickup proximityPickup = childDropObject.GetComponentInChildren<Duy.Interaction.ProximityItemPickup>(true);
            if (proximityPickup != null)
            {
                proximityPickup.ActivateDrop();
            }
        }

        private void OnDestroy()
        {
            combatCts?.Cancel();
            combatCts?.Dispose();
        }

        private void OnDrawGizmosSelected()
        {
            Vector2 origin = Application.isPlaying ? spawnPosition : (Vector2)transform.position;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin + Vector2.left * (patrolDistance * 0.5f), origin + Vector2.right * (patrolDistance * 0.5f));

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, minSafeDistance);
        }
    }
}
