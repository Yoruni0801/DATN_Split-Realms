using System.Collections;
using UnityEngine;

namespace Bao.Bosses.QuyVuongPhase4
{
    /// <summary>
    /// SKILL TẦM GẦN: Liệt Địa Ma Trảo (Phase 4 - Hỏa Nham Ma)
    /// Combo 2 nhịp cận chiến:
    /// - Nhịp 1: Molten Skull Dash (Biến thân Đầu Lâu Hỏa Ma húc tới tấp nập, dừng khi đụng Player/Ground/Wall)
    /// - Nhịp 2: Eruption Uppercut (Vuốt móc lửa tạo Lốc Xoáy Dung Nham bùng lên ngay dưới chân Người Chơi)
    /// </summary>
    public class Phase4TectonicRendingSkill : QuyVuongPhase4SkillBase, IParryable
    {
        private bool dashWasParried;

        [Header("=== NHỊP 1: Molten Skull Dash ===")]
        [SerializeField] private float dashSpeed = 22f;
        [SerializeField] private int dashDamage = 18;
        [SerializeField] private float dashKnockback = 5f;
        [Tooltip("Kích thước / Tỷ lệ scale của Đầu Lâu Hỏa Ma khi biến thân húc tới")]
        [SerializeField] private Vector3 skullScale = new Vector3(4.0f, 4.0f, 1f);
        [Tooltip("Độ cao lệch Y tính từ chân Boss lên giữa thân Boss (giúp Đầu Lâu xuất hiện đúng ngay giữa tâm Boss)")]
        [SerializeField] private float skullCenterYOffset = 2.0f;

        [Header("=== DASH COLLISION & STOP SETTINGS ===")]
        [Tooltip("Danh sách các Tag khi húc trúng sẽ dừng lại và biến hình về Boss cũ")]
        [SerializeField] private string[] dashStopTags = new string[] { "Player", "Ground", "Wall" };
        [Tooltip("Bán kính phát hiện va chạm của Đầu Lâu Hỏa Ma")]
        [SerializeField] private float dashHitboxRadius = 2.2f;
        [Tooltip("Quãng đường húc tối đa (nếu không đụng tag cản nào)")]
        [SerializeField] private float maxDashDistance = 50f;
        [Tooltip("Thời gian húc tối đa phòng kẹt")]
        [SerializeField] private float maxDashTimeout = 3.0f;
        [SerializeField] private LayerMask stopCollisionMask = ~0;

        [Header("=== NHỊP 2: Eruption Uppercut ===")]
        [SerializeField] private float uppercutWindupTime = 0.4f;
        [SerializeField] private int uppercutDamage = 20;
        [SerializeField] private GameObject eruptionVFXPrefab;

        [Header("VFX & SFX")]
        [SerializeField] private GameObject dashTrailVFXPrefab;
        [SerializeField] private AudioClip dashSFX;
        [SerializeField] private AudioClip uppercutSFX;

        [Header("Camera Shake Settings")]
        [SerializeField] private float dashShakeIntensity = 0.3f;
        [SerializeField] private float shakeDuration = 0.3f;

        protected override void Awake()
        {
            base.Awake();
            skillName = "Phase 4 - Tectonic Rending (Skull Dash & Uppercut)";
        }

        public override IEnumerator ExecuteSkill(Transform playerTransform)
        {
            if (playerTransform == null) yield break;

            DebugLog("=== BẮT ĐẦU COMBO MELEE (PHASE 4) ===");
            ResolveReferences();
            FlipTowardsPlayer(playerTransform);
            dashWasParried = false;

            // NHỊP 1: Molten Trench Dash (Biến thân đầu lâu húc tới)
            yield return StartCoroutine(Phase1_MoltenTrenchDash(playerTransform));

            // Parry thành công cú dash sẽ hủy nhịp 2: không spawn lốc xoáy và không có damage tiếp theo.
            if (!dashWasParried)
            {
                yield return new WaitForSeconds(0.15f);
                yield return StartCoroutine(Phase2_EruptionUppercut(playerTransform));
            }

            DebugLog("=== KẾT THÚC COMBO MELEE (PHASE 4) ===");
        }

        private IEnumerator Phase1_MoltenTrenchDash(Transform playerTransform)
        {
            DebugLog("Nhịp 1: Molten Trench Dash - Biến thân Đầu Lâu Hỏa Ma");

            // Telegraph
            SetBossColor(new Color(1f, 0.5f, 0.2f));
            yield return new WaitForSeconds(0.25f);
            SetBossColor(Color.white);

            Vector2 dirToPlayer = GetDirectionToPlayer(playerTransform);
            // Húc thẳng theo trục ngang về phía player
            float dirX = dirToPlayer.x >= 0 ? 1f : -1f;
            Vector2 dashDirection = new Vector2(dirX, 0f);

            // Biến thân: Ẩn thân xác Boss và sinh Đầu Lâu Hỏa Ma khổng lồ tại vị trí tâm giữa thân Boss
            SetBossBodyActive(false);

            GameObject trailVFX = null;
            Phase4FlameSkullAura skullAura = null;
            if (dashTrailVFXPrefab != null)
            {
                // Spawn tại tâm giữa thân Boss (offset Y từ chân)
                Vector3 spawnPos = transform.position + Vector3.up * skullCenterYOffset;
                trailVFX = Instantiate(dashTrailVFXPrefab, spawnPos, Quaternion.identity);
                skullAura = trailVFX.GetComponent<Phase4FlameSkullAura>();
                if (skullAura == null)
                {
                    skullAura = trailVFX.AddComponent<Phase4FlameSkullAura>();
                }
                skullAura.Initialize(dashDirection, skullScale);
            }

            if (dashSFX != null)
            {
                AudioSource.PlayClipAtPoint(dashSFX, transform.position);
            }

            float traveledDistance = 0f;
            float elapsed = 0f;
            bool hitPlayer = false;
            bool hitObstacle = false;
            bool wasParried = false;

            // Húc liên tục cho đến khi đụng Player hoặc đụng Tag cản (Ground/Wall...)
            while (traveledDistance < maxDashDistance && elapsed < maxDashTimeout)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                float moveStep = dashSpeed * dt;

                Vector3 currentPos = transform.position;
                Vector3 nextPos = currentPos + (Vector3)(dashDirection * moveStep);
                Vector3 checkCenterPos = nextPos + Vector3.up * skullCenterYOffset;

                // 1. Kiểm tra va chạm với các Stop Tags (Player, Ground,...) tại vị trí tâm đầu lâu tiếp theo
                bool shouldStop = CheckDashObstacleAndPlayerHit(checkCenterPos, dashDamage, dashKnockback, out hitPlayer, out hitObstacle, out wasParried);

                transform.position = nextPos;
                traveledDistance += moveStep;

                if (trailVFX != null)
                {
                    trailVFX.transform.position = transform.position + Vector3.up * skullCenterYOffset;
                }

                if (shouldStop)
                {
                    dashWasParried = wasParried;
                    DebugLog($"[Dash] Đã đụng {(hitPlayer ? "Player" : "Vật cản Ground/Wall")} -> Dừng húc và khôi phục Boss!");
                    break;
                }

                yield return null;
            }

            // Kết thúc / Đụng người chơi / Đụng Ground: Kích nổ tan biến đầu lâu lửa & Hiện hình lại boss như cũ
            if (trailVFX != null)
            {
                if (skullAura != null)
                {
                    skullAura.TriggerImpactBurstAndFade(0.22f);
                }
                else
                {
                    Destroy(trailVFX, 0.25f);
                }
            }

            // Boss hiện hình lại như cũ
            SetBossBodyActive(true);
            FlipTowardsPlayer(playerTransform);
            PlayMoveAnimation();

            TriggerCameraShake(dashShakeIntensity * (hitPlayer ? 1.5f : 1.2f), shakeDuration);
            if (hitPlayer)
            {
                yield return StartCoroutine(Hitstop(0.06f));
            }
            yield return new WaitForSeconds(0.12f);
            FlipTowardsPlayer(playerTransform);
        }

        private IEnumerator Phase2_EruptionUppercut(Transform playerTransform)
        {
            DebugLog("Nhịp 2: Eruption Uppercut");

            FlipTowardsPlayer(playerTransform);
            PlayAttackAnimation();
            yield return new WaitForSeconds(uppercutWindupTime);

            // Vị trí spawn lốc xoáy: Bùng lên ngay tại vị trí chân của Người Chơi
            Vector3 eruptPos = playerTransform != null ? playerTransform.position : (MeleePoint != null ? MeleePoint.position : transform.position);
            if (eruptionVFXPrefab != null)
            {
                Instantiate(eruptionVFXPrefab, eruptPos, Quaternion.identity);
            }

            if (uppercutSFX != null)
            {
                AudioSource.PlayClipAtPoint(uppercutSFX, eruptPos);
            }

            CheckUppercutHit(uppercutDamage);

            yield return new WaitForSeconds(0.3f);
            FlipTowardsPlayer(playerTransform);
        }

        private bool CheckDashObstacleAndPlayerHit(Vector3 checkPosition, int damage, float knockback, out bool hitPlayer, out bool hitObstacle, out bool wasParried)
        {
            hitPlayer = false;
            hitObstacle = false;
            wasParried = false;

            Collider2D[] hits = Physics2D.OverlapCircleAll(checkPosition, dashHitboxRadius, stopCollisionMask);
            foreach (var hit in hits)
            {
                if (hit == null || hit.transform.root == transform.root) continue;

                // Kiểm tra va chạm với các Tag trong danh sách tuỳ chỉnh (Player, Ground, Wall, ...)
                if (dashStopTags != null)
                {
                    for (int i = 0; i < dashStopTags.Length; i++)
                    {
                        string targetTag = dashStopTags[i];
                        if (!string.IsNullOrEmpty(targetTag) && hit.CompareTag(targetTag))
                        {
                            if (hit.CompareTag("Player"))
                            {
                                hitPlayer = true;
                                IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                                    hit,
                                    damage,
                                    ParryableAttackType.Melee,
                                    this,
                                    transform,
                                    new Vector2(transform.localScale.x >= 0f ? 1f : -1f, 0f),
                                    canBeBlocked: true);
                                wasParried = resolution == IncomingAttackResolution.Parried;
                            }
                            else
                            {
                                hitObstacle = true;
                            }
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Skull Dash là đòn có thể parry. Skill tự làm nguồn parry để tránh generic responder
        /// pause chính coroutine này trước khi nó kịp hủy nhịp lốc xoáy.
        /// </summary>
        public ParryResult OnParried(ParryContext context)
        {
            dashWasParried = true;
            DebugLog("Skull Dash đã bị parry: hủy Eruption Uppercut.");
            return ParryResult.Successful(context.DefaultHitStopDuration, preventDamage: true);
        }

        private void CheckUppercutHit(int damage)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(MeleePoint.position, 2.5f);
            foreach (var hit in hits)
            {
                if (hit != null && hit.transform.root != transform.root && hit.CompareTag("Player"))
                {
                    ApplyPlayerDamage(hit.gameObject, damage, 4f);
                }
            }
        }

        public void ClearActiveSkillObjects()
        {
            StopAllCoroutines();
            SetBossBodyActive(true);
            SetBossColor(Color.white);

            var flameAuras = FindObjectsByType<Phase4FlameSkullAura>(FindObjectsSortMode.None);
            foreach (var aura in flameAuras) if (aura != null) Destroy(aura.gameObject);

            var vortexes = FindObjectsByType<Phase4FireTornadoVortex>(FindObjectsSortMode.None);
            foreach (var v in vortexes) if (v != null) Destroy(v.gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * skullCenterYOffset, dashHitboxRadius);
        }
    }
}
