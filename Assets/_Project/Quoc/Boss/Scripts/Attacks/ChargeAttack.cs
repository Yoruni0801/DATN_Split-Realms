using System.Collections;
using UnityEngine;
using Spine.Unity;

namespace Quoc.Boss
{
    public class ChargeAttack : BossAttackBase
    {
        public override string AttackName => "Charge";

        [Header("Animation Settings")]
        [SpineAnimation, Tooltip("Animation Spine phát khi đang húc")]
        [SerializeField] private string chargeSpineAnimation = "Run";

        [Header("Charge Range Settings")]
        [SerializeField, Tooltip("Bật tùy chỉnh khoảng cách húc tối đa trực tiếp trong Inspector")]
        private bool overrideMaxDistance = true;

        [SerializeField, Tooltip("Khoảng cách tầm húc tối đa (mét)")]
        private float maxChargeDistance = 15f;

        [Header("Hitbox Settings")]
        [SerializeField, Tooltip("Bán kính va chạm gây damage khi lướt trúng Player")]
        private float chargeHitRadius = 1.6f;

        [SerializeField, Tooltip("LayerMask của Player")]
        private LayerMask playerLayer = ~0;

        [Header("Wall Detection")]
        [SerializeField, Tooltip("LayerMask tường cần phát hiện để dừng húc (chọn layer Ground/Wall trong Inspector)")]
        private LayerMask wallLayer;

        [SerializeField, Tooltip("Khoảng cách BoxCast kiểm tra tường phía trước Boss")]
        private float wallCheckDistance = 0.5f;

        [SerializeField, Tooltip("Nửa chiều cao BoxCast (nên bằng nửa chiều cao Boss, tránh quét trúng sàn dưới chân)")]
        private float wallCheckHalfHeight = 0.8f;

        [SerializeField, Tooltip("Dùng thêm Tag để phát hiện tường (ngoài wallLayer). Để trống nếu không cần")]
        private string wallTag = "Wall";

        [Header("Camera Shake")]
        [SerializeField, Tooltip("Cường độ rung camera Cinemachine khi húc")]
        private float shakeForce = 1.8f;

        [Header("Gizmos Settings")]
        [SerializeField, Tooltip("Luôn hiển thị Gizmos đòn húc trong Scene")]
        private bool alwaysShowGizmos = true;

        private bool hasHitPlayer = false;

        public override void Execute(BossController boss, BossAttackData data, System.Action onComplete)
        {
            base.Execute(boss, data, onComplete);
            hasHitPlayer = false;
            StartCoroutine(ChargeRoutine());
        }

        private IEnumerator ChargeRoutine()
        {
            boss.Movement.FacePlayer();
            float dirX = boss.Movement.IsFacingRight ? 1f : -1f;
            float timer = 0f;
            float duration = (attackData != null && attackData.chargeDuration > 0) ? attackData.chargeDuration : 1.2f;
            float speed = (attackData != null && attackData.chargeSpeed > 0) ? attackData.chargeSpeed : 16f;

            float maxDist = (overrideMaxDistance && maxChargeDistance > 0f) ? maxChargeDistance : 15f;
            float traveledDistance = 0f;

            string animToPlay = (boss != null && !string.IsNullOrEmpty(boss.ChargeAnimName)) ? boss.ChargeAnimName :
                                (!string.IsNullOrEmpty(chargeSpineAnimation) ? chargeSpineAnimation : 
                                (attackData != null ? attackData.spineAnimationName : "Run"));
            float animSpeed = (boss != null) ? boss.ChargeAnimSpeed : 1.0f;
            boss.PlaySpineAnimation(animToPlay, true, animSpeed);

            TriggerCameraShake(shakeForce, 1.0f);
            Debug.Log($"[ChargeAttack] Boss bắt đầu húc về hướng Player! DirX: {dirX}, Speed: {speed}, MaxDistance: {maxDist}m");

            while (timer < duration && traveledDistance < maxDist)
            {
                // === KIỂM TRA TƯỜNG TRƯỚC KHI DI CHUYỂN ===
                if (IsWallAhead(dirX))
                {
                    Debug.Log("[ChargeAttack] Phát hiện tường phía trước! Dừng húc.");
                    TriggerCameraShake(shakeForce * 1.2f, 1.0f);
                    break;
                }

                // Kiểm tra giới hạn Arena Bounds
                if (boss.Movement.IsAtBoundary(dirX) && traveledDistance > 1.0f)
                {
                    Debug.Log("[ChargeAttack] Boss chạm tường Arena!");
                    TriggerCameraShake(shakeForce * 1.2f, 1.0f);
                    break;
                }

                // === DI CHUYỂN ===
                Vector3 pos = boss.transform.position;
                float step = speed * Time.deltaTime;

                if (traveledDistance + step > maxDist)
                    step = maxDist - traveledDistance;

                float nextX = pos.x + dirX * step;
                float clampedX = boss.Movement.ClampToArenaBounds(nextX);
                float actualStep = Mathf.Abs(clampedX - pos.x);

                boss.transform.position = new Vector3(clampedX, pos.y, pos.z);
                traveledDistance += (actualStep > 0.001f ? actualStep : step);

                // Kiểm tra va chạm với Player
                if (!hasHitPlayer)
                {
                    Collider2D[] hits = Physics2D.OverlapCircleAll(boss.transform.position, chargeHitRadius, playerLayer);
                    foreach (var hit in hits)
                    {
                        if (hit.transform == boss.transform || hit.transform.IsChildOf(boss.transform) || hit.transform.root == boss.transform.root) continue;
                        if (DamageUtility.DealDamage(hit.gameObject, attackData != null ? attackData.damage : 25, boss.transform))
                        {
                            hasHitPlayer = true;
                            Debug.Log("[ChargeAttack] Boss húc trúng Player!");
                            TriggerCameraShake(shakeForce * 1.5f, 1.0f);
                            break;
                        }
                    }
                }

                timer += Time.deltaTime;
                yield return null;
            }

            boss.Movement.StopMovement();
            boss.PlayIdleAnimation();
            FinishAttack();
        }

        private bool IsWallAhead(float dirX)
        {
            // 1. Dùng kiểm tra của BossMovement trước
            if (boss != null && boss.Movement != null && boss.Movement.IsWalled(dirX))
                return true;

            // 2. Bắn BoxCast ngang tầm thân Boss để phát hiện tường theo LayerMask hoặc Tag
            Vector2 rayOrigin = (Vector2)boss.transform.position + Vector2.up * wallCheckHalfHeight;
            Vector2 boxSize = new Vector2(0.1f, wallCheckHalfHeight * 1.5f);

            // Bắn không giới hạn LayerMask nếu wallLayer == 0 để bắt Tag "Wall" / "Ground"
            int mask = wallLayer.value != 0 ? wallLayer.value : ~0;
            RaycastHit2D hit = Physics2D.BoxCast(rayOrigin, boxSize, 0f, new Vector2(dirX, 0f), wallCheckDistance, mask);

            if (hit.collider != null && hit.transform != boss.transform && !hit.collider.isTrigger)
            {
                // Kiểm tra theo Layer
                if (wallLayer.value != 0 && (wallLayer.value & (1 << hit.collider.gameObject.layer)) != 0)
                    return true;

                // Kiểm tra theo Tag
                if (hit.collider.CompareTag("Wall") || hit.collider.CompareTag("Ground"))
                    return true;

                if (!string.IsNullOrEmpty(wallTag) && hit.collider.CompareTag(wallTag))
                    return true;
            }

            return false;
        }

        private void TriggerCameraShake(float force = -1f, float duration = 1.0f)
        {
            float f = force > 0f ? force : shakeForce;
            BossCameraShake shake = boss.GetComponent<BossCameraShake>();
            if (shake == null)
            {
                shake = boss.gameObject.AddComponent<BossCameraShake>();
            }
            shake.TriggerShake(f, duration);
        }

        private void OnDrawGizmos()
        {
            if (alwaysShowGizmos)
            {
                DrawChargeGizmos(false);
            }
        }

        private void OnDrawGizmosSelected()
        {
            DrawChargeGizmos(true);
        }

        private void DrawChargeGizmos(bool isSelected)
        {
            BossController b = boss != null ? boss : GetComponent<BossController>();
            float dir = 1f;

            if (b != null && b.Movement != null)
            {
                dir = b.Movement.IsFacingRight ? 1f : -1f;
            }
            else if (transform.localScale.x < 0)
            {
                dir = -1f;
            }

            float totalDistance = (overrideMaxDistance && maxChargeDistance > 0f) ? maxChargeDistance : 15f;

            Vector3 startPos = transform.position;
            Vector3 targetPos = startPos + new Vector3(dir * totalDistance, 0, 0);

            if (b != null && b.Movement != null)
            {
                targetPos.x = b.Movement.ClampToArenaBounds(targetPos.x);
            }

            Gizmos.color = isSelected ? new Color(1f, 0f, 0.4f, 0.45f) : new Color(1f, 0f, 0.4f, 0.2f);
            Gizmos.DrawSphere(startPos, chargeHitRadius);
            Gizmos.color = isSelected ? Color.red : new Color(1f, 0.2f, 0.2f, 0.7f);
            Gizmos.DrawWireSphere(startPos, chargeHitRadius);

            Gizmos.color = isSelected ? Color.yellow : new Color(1f, 0.9f, 0.2f, 0.6f);
            Gizmos.DrawLine(startPos, targetPos);
            Gizmos.DrawLine(startPos + Vector3.up * chargeHitRadius, targetPos + Vector3.up * chargeHitRadius);
            Gizmos.DrawLine(startPos + Vector3.down * chargeHitRadius, targetPos + Vector3.down * chargeHitRadius);

            Gizmos.color = isSelected ? Color.magenta : new Color(0.9f, 0f, 0.9f, 0.6f);
            Gizmos.DrawWireSphere(targetPos, chargeHitRadius);
            Gizmos.DrawSphere(targetPos, 0.3f);
        }
    }
}
