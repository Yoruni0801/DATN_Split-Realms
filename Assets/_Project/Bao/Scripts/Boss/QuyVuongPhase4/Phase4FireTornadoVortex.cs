using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bao.Bosses.QuyVuongPhase4
{
    /// <summary>
    /// Hiệu ứng Lốc Xoáy Lửa (Nhịp 2 Skill Melee Quỷ Vương Phase 4).
    /// Khi bùng lên dưới chân Người Chơi:
    /// 1. Tự động hút người chơi vào tâm đáy lốc xoáy (Inward Suction).
    /// 2. Cuốn và nâng bổng người chơi xoay tròn theo trục xoắn ốc lên đỉnh lốc xoáy (Spiral Lift).
    /// 3. Gây sát thương liên tục (Damage Ticks) kèm rung lắc camera.
    /// 4. Hất tung người chơi văng ra khi lốc xoáy tan biến ở đỉnh.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Phase4FireTornadoVortex : MonoBehaviour
    {
        [Header("=== VORTEX SUCTION & LIFT CONFIG ===")]
        [Tooltip("Bán kính hút người chơi vào tâm lốc xoáy")]
        [SerializeField] private float suctionRadius = 3.5f;

        [Tooltip("Tốc độ hút dồn vào tâm xoáy")]
        [SerializeField] private float pullSpeed = 12f;

        [Tooltip("Độ cao tối đa cuốn người chơi lên (đỉnh lốc xoáy)")]
        [SerializeField] private float maxLiftHeight = 4.2f;

        [Tooltip("Tốc độ nâng người chơi bay lên đỉnh")]
        [SerializeField] private float liftSpeed = 3.6f;

        [Tooltip("Tốc độ góc xoay tròn quanh tâm lốc xoáy (độ/giây)")]
        [SerializeField] private float spiralAngularSpeed = 720f;

        [Tooltip("Bán kính quỹ đạo xoay tròn quanh trục lốc")]
        [SerializeField] private float orbitRadius = 0.55f;

        [Tooltip("Lực hất văng người chơi khi lốc xoáy tan biến ở đỉnh")]
        [SerializeField] private Vector2 releaseLaunchVelocity = new Vector2(3.5f, 6.0f);

        [Header("=== DAMAGE SETTINGS ===")]
        [SerializeField] private int damagePerTick = 6;
        [SerializeField] private float damageInterval = 0.25f;
        [SerializeField] private LayerMask targetLayer = ~0;

        [Header("=== CAMERA FEEDBACK ===")]
        [SerializeField] private float shakeIntensity = 0.2f;

        private class CapturedPlayerState
        {
            public Transform transform;
            public Rigidbody2D rb;
            public Player player;
            public float currentHeight;
            public float currentAngle;
            public float originalGravity;
            public bool isCaptured;
        }

        private readonly List<CapturedPlayerState> capturedPlayers = new List<CapturedPlayerState>();
        private float damageTimer = 0f;
        private Vector3 vortexBaseCenter;
        private bool isReleasing = false;

        private void Start()
        {
            vortexBaseCenter = transform.position;
        }

        private void Update()
        {
            if (isReleasing) return;

            DetectAndCapturePlayers();
            UpdateCapturedMotion(Time.deltaTime);
            UpdateDamageTicks(Time.deltaTime);
        }

        private void DetectAndCapturePlayers()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(vortexBaseCenter, suctionRadius, targetLayer);
            foreach (var hit in hits)
            {
                if (hit == null || !hit.CompareTag("Player")) continue;

                Transform rootTransform = hit.transform.root;
                if (IsAlreadyCaptured(rootTransform)) continue;

                var rb = hit.attachedRigidbody != null ? hit.attachedRigidbody : rootTransform.GetComponent<Rigidbody2D>();
                var player = rootTransform.GetComponent<Player>() ?? hit.GetComponentInParent<Player>();

                Vector2 offset = (Vector2)rootTransform.position - (Vector2)vortexBaseCenter;
                float startAngle = Mathf.Atan2(offset.y, offset.x);

                var captured = new CapturedPlayerState
                {
                    transform = rootTransform,
                    rb = rb,
                    player = player,
                    currentHeight = Mathf.Max(0f, rootTransform.position.y - vortexBaseCenter.y),
                    currentAngle = startAngle,
                    originalGravity = rb != null ? rb.gravityScale : 1f,
                    isCaptured = true
                };

                // Tạm thời vô hiệu hóa trọng lực khi bị lốc xoáy cuốn
                if (rb != null)
                {
                    rb.gravityScale = 0f;
                    rb.linearVelocity = Vector2.zero;
                }

                capturedPlayers.Add(captured);
            }
        }

        private bool IsAlreadyCaptured(Transform t)
        {
            for (int i = 0; i < capturedPlayers.Count; i++)
            {
                if (capturedPlayers[i].transform == t) return true;
            }
            return false;
        }

        private void UpdateCapturedMotion(float dt)
        {
            for (int i = capturedPlayers.Count - 1; i >= 0; i--)
            {
                var state = capturedPlayers[i];
                if (state.transform == null)
                {
                    capturedPlayers.RemoveAt(i);
                    continue;
                }

                // 1. Cập nhật góc xoay tròn
                state.currentAngle += spiralAngularSpeed * Mathf.Deg2Rad * dt;

                // 2. Cập nhật độ cao nâng bổng lên đỉnh
                state.currentHeight = Mathf.MoveTowards(state.currentHeight, maxLiftHeight, liftSpeed * dt);

                // 3. Tính toán vị trí xoắn ốc 2D
                float dynamicRadius = Mathf.Lerp(orbitRadius * 1.5f, orbitRadius * 0.7f, state.currentHeight / Mathf.Max(0.1f, maxLiftHeight));
                float offsetX = Mathf.Cos(state.currentAngle) * dynamicRadius;
                Vector3 targetPos = vortexBaseCenter + new Vector3(offsetX, state.currentHeight, 0f);

                // 4. Di chuyển mượt mà Player vào quỹ đạo xoáy
                if (state.rb != null)
                {
                    Vector2 toTarget = (Vector2)targetPos - state.rb.position;
                    state.rb.linearVelocity = toTarget * pullSpeed;
                }
                else
                {
                    state.transform.position = Vector3.Lerp(state.transform.position, targetPos, dt * pullSpeed);
                }
            }
        }

        private void UpdateDamageTicks(float dt)
        {
            if (capturedPlayers.Count == 0) return;

            damageTimer -= dt;
            if (damageTimer <= 0f)
            {
                damageTimer = damageInterval;
                for (int i = 0; i < capturedPlayers.Count; i++)
                {
                    var state = capturedPlayers[i];
                    if (state.transform != null)
                    {
                        QuyVuongPhase4SkillBase.ApplyPlayerDamage(state.transform.gameObject, damagePerTick, 0f);
                    }
                }
            }
        }

        /// <summary>
        /// Giải phóng người chơi khi lốc xoáy tan biến.
        /// </summary>
        private void ReleaseAllCapturedPlayers()
        {
            if (isReleasing) return;
            isReleasing = true;

            for (int i = 0; i < capturedPlayers.Count; i++)
            {
                var state = capturedPlayers[i];
                if (state == null || state.transform == null) continue;

                if (state.rb != null)
                {
                    state.rb.gravityScale = state.originalGravity > 0 ? state.originalGravity : 1f;

                    // Hất tung theo hướng xoay hiện tại văng ra ngoài từ đỉnh lốc
                    float launchDirX = Mathf.Cos(state.currentAngle) >= 0 ? 1f : -1f;
                    Vector2 launchVel = new Vector2(launchDirX * releaseLaunchVelocity.x, releaseLaunchVelocity.y);

                    if (state.player != null)
                    {
                        state.player.ApplyExternalLaunch(launchVel, 0.25f);
                    }
                    else
                    {
                        state.rb.linearVelocity = launchVel;
                    }
                }
            }

            capturedPlayers.Clear();
        }

        private void OnDestroy()
        {
            ReleaseAllCapturedPlayers();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, suctionRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * maxLiftHeight);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * maxLiftHeight, orbitRadius);
        }
    }
}
