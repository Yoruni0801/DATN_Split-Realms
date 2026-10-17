using System.Collections;
using UnityEngine;
using Spine.Unity;

namespace Quoc.Boss
{
    public class DoubleChargeAttack : BossAttackBase
    {
        public override string AttackName => "Double Charge";

        [Header("Animation Settings")]
        [SpineAnimation, Tooltip("Animation Spine khi húc lao tới")]
        [SerializeField] private string chargeSpineAnimation = "Run";

        [Header("Charge Range Settings")]
        [SerializeField, Tooltip("Bật tùy chỉnh khoảng cách húc tối đa trực tiếp trong Inspector")]
        private bool overrideMaxDistance = true;

        [SerializeField, Tooltip("Khoảng cách tầm húc tối đa cho mỗi lượt (mét)")]
        private float maxChargeDistance = 15f;

        [Header("Hitbox Settings")]
        [SerializeField, Tooltip("Bán kính va chạm khi lướt")]
        private float chargeHitRadius = 1.6f;

        [SerializeField, Tooltip("LayerMask của Player")]
        private LayerMask playerLayer = ~0;

        [Header("Wall Detection")]
        [SerializeField, Tooltip("LayerMask tường cần phát hiện để dừng húc (chọn layer Ground/Wall trong Inspector)")]
        private LayerMask wallLayer;

        [SerializeField, Tooltip("Khoảng cách BoxCast kiểm tra tường phía trước Boss")]
        private float wallCheckDistance = 0.5f;

        [SerializeField, Tooltip("Nửa chiều cao BoxCast (tránh quét trúng sàn dưới chân)")]
        private float wallCheckHalfHeight = 0.8f;

        [SerializeField, Tooltip("Dùng thêm Tag để phát hiện tường. Để trống nếu không cần")]
        private string wallTag = "Wall";

        [Header("Camera Shake")]
        [SerializeField, Tooltip("Cường độ rung camera Cinemachine khi húc")]
        private float shakeForce = 2.0f;

        [Header("Gizmos Settings")]
        [SerializeField, Tooltip("Luôn hiển thị Gizmos đòn húc kép trong Scene")]
        private bool alwaysShowGizmos = true;

        public override void Execute(BossController boss, BossAttackData data, System.Action onComplete)
        {
            base.Execute(boss, data, onComplete);
            StartCoroutine(DoubleChargeRoutine());
        }

        private IEnumerator DoubleChargeRoutine()
        {
            yield return PerformSingleCharge();

            boss.Movement.StopMovement();
            boss.PlayIdleAnimation();
            yield return new WaitForSeconds(0.25f);

            boss.Movement.FacePlayer();
            yield return PerformSingleCharge();

            boss.Movement.StopMovement();
            boss.PlayIdleAnimation();
            FinishAttack();
        }

        private IEnumerator PerformSingleCharge()
        {
            float dirX = boss.Movement.IsFacingRight ? 1f : -1f;
            float timer = 0f;
            float duration = (attackData != null && attackData.chargeDuration > 0) ? attackData.chargeDuration : 1.2f;
            float speed = (attackData != null && attackData.chargeSpeed > 0) ? attackData.chargeSpeed : 17f;
            float maxDist = (overrideMaxDistance && maxChargeDistance > 0f) ? maxChargeDistance : 15f;
            float traveledDistance = 0f;
            bool hitThisCharge = false;

            string animToPlay = (boss != null && !string.IsNullOrEmpty(boss.ChargeAnimName)) ? boss.ChargeAnimName :
                                (!string.IsNullOrEmpty(chargeSpineAnimation) ? chargeSpineAnimation : 
                                (attackData != null ? attackData.spineAnimationName : "Run"));
            float animSpeed = (boss != null) ? boss.ChargeAnimSpeed : 1.0f;
            boss.PlaySpineAnimation(animToPlay, true, animSpeed);

            TriggerCameraShake(shakeForce, 1.0f);
            Debug.Log($"[DoubleChargeAttack] Boss húc lượt! DirX: {dirX}, Speed: {speed}, MaxDist: {maxDist}m");

            while (timer < duration && traveledDistance < maxDist)
            {
                // === KIỂM TRA TƯỜNG TRƯỚC KHI DI CHUYỂN ===
                if (IsWallAhead(dirX))
                {
                    Debug.Log("[DoubleChargeAttack] Phát hiện tường phía trước! Dừng húc.");
                    TriggerCameraShake(shakeForce * 1.2f, 1.0f);
                    break;
                }

                // Kiểm tra va chạm giới hạn Arena
                if (boss.Movement.IsAtBoundary(dirX) && traveledDistance > 1.0f)
                {
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

                if (!hitThisCharge)
                {
                    Collider2D[] hits = Physics2D.OverlapCircleAll(boss.transform.position, chargeHitRadius, playerLayer);
                    foreach (var hit in hits)
                    {
                        if (hit.transform == boss.transform || hit.transform.IsChildOf(boss.transform) || hit.transform.root == boss.transform.root) continue;
                        if (DamageUtility.DealDamage(hit.gameObject, attackData != null ? attackData.damage : 30, boss.transform))
                        {
                            hitThisCharge = true;
                            TriggerCameraShake(shakeForce * 1.5f, 1.0f);
                            break;
                        }
                    }
                }

                timer += Time.deltaTime;
                yield return null;
            }
        }

        private bool IsWallAhead(float dirX)
        {
            if (boss != null && boss.Movement != null && boss.Movement.IsWalled(dirX))
                return true;

            Vector2 rayOrigin = (Vector2)boss.transform.position + Vector2.up * wallCheckHalfHeight;
            Vector2 boxSize = new Vector2(0.1f, wallCheckHalfHeight * 1.5f);

            int mask = wallLayer.value != 0 ? wallLayer.value : ~0;
            RaycastHit2D hit = Physics2D.BoxCast(rayOrigin, boxSize, 0f, new Vector2(dirX, 0f), wallCheckDistance, mask);

            if (hit.collider != null && hit.transform != boss.transform && !hit.collider.isTrigger)
            {
                if (wallLayer.value != 0 && (wallLayer.value & (1 << hit.collider.gameObject.layer)) != 0)
                    return true;

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
                DrawDoubleChargeGizmos(false);
            }
        }

        private void OnDrawGizmosSelected()
        {
            DrawDoubleChargeGizmos(true);
        }

        private void DrawDoubleChargeGizmos(bool isSelected)
        {
            BossController b = boss != null ? boss : GetComponent<BossController>();
            float dir1 = 1f;

            if (b != null && b.Movement != null)
            {
                dir1 = b.Movement.IsFacingRight ? 1f : -1f;
            }
            else if (transform.localScale.x < 0)
            {
                dir1 = -1f;
            }

            float dist = (overrideMaxDistance && maxChargeDistance > 0f) ? maxChargeDistance : 15f;

            Vector3 startPos = transform.position;
            Vector3 pos1 = startPos + new Vector3(dir1 * dist, 0, 0);
            if (b != null && b.Movement != null) pos1.x = b.Movement.ClampToArenaBounds(pos1.x);

            Vector3 pos2 = pos1 + new Vector3(-dir1 * dist, 0, 0);
            if (b != null && b.Movement != null) pos2.x = b.Movement.ClampToArenaBounds(pos2.x);

            Gizmos.color = isSelected ? Color.yellow : new Color(1f, 0.9f, 0.2f, 0.7f);
            Gizmos.DrawLine(startPos, pos1);
            Gizmos.DrawWireSphere(pos1, chargeHitRadius);

            Gizmos.color = isSelected ? Color.magenta : new Color(1f, 0.8f, 0.8f, 0.7f);
            Gizmos.DrawLine(pos1, pos2);
            Gizmos.DrawWireSphere(pos2, chargeHitRadius);

            Gizmos.color = isSelected ? Color.red : new Color(1f, 0.3f, 0.3f, 0.2f);
            Gizmos.DrawSphere(startPos, chargeHitRadius);
            Gizmos.DrawWireSphere(startPos, chargeHitRadius);
        }
    }
}
