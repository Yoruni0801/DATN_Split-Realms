using System.Collections;
using UnityEngine;
using Spine.Unity;

namespace Quoc.Boss
{
    public class JumpSlamAttack : BossAttackBase
    {
        public override string AttackName => "Jump Slam";

        [Header("Animation Settings")]
        [SpineAnimation, Tooltip("Animation Spine khi nhảy giậm đất")]
        [SerializeField] private string jumpSpineAnimation = "Skill";

        [Header("Jump Settings")]
        [SerializeField, Tooltip("Độ cao cú nhảy (mét)")]
        private float jumpHeight = 4.0f;

        [SerializeField, Tooltip("Thời gian thực hiện cú nhảy (giây)")]
        private float jumpDuration = 0.75f;

        [Header("Impact Settings")]
        [SerializeField, Tooltip("Bán kính nổ giậm đất gây sát thương")]
        private float impactRadius = 3.0f;

        [SerializeField, Tooltip("Particle hiệu ứng khi giậm xuống đất")]
        private GameObject slamParticlePrefab;

        [SerializeField, Tooltip("LayerMask của Player")]
        private LayerMask playerLayer = ~0;

        [Header("Camera Shake")]
        [SerializeField, Tooltip("Lực rung camera Cinemachine khi giậm đất")]
        private float shakeForce = 2.0f;

        public override void Execute(BossController boss, BossAttackData data, System.Action onComplete)
        {
            base.Execute(boss, data, onComplete);
            StartCoroutine(JumpSlamRoutine());
        }

        private IEnumerator JumpSlamRoutine()
        {
            boss.Movement.StopMovement();
            boss.Movement.FacePlayer();

            Vector3 startPos = boss.transform.position;
            Vector3 targetPos = startPos;

            Transform targetPlayer = boss.Player ?? boss.RawPlayer ?? boss.ResolvePlayer();
            if (targetPlayer != null)
            {
                targetPos = targetPlayer.position;
                targetPos.y = startPos.y; // Giữ nguyên độ cao mặt đất
                targetPos.x = boss.Movement.ClampToArenaBounds(targetPos.x);
            }

            string anim = (boss != null && !string.IsNullOrEmpty(boss.JumpSlamAnimName)) ? boss.JumpSlamAnimName :
                          (!string.IsNullOrEmpty(jumpSpineAnimation) ? jumpSpineAnimation : 
                          (attackData != null ? attackData.spineAnimationName : "Skill"));
            float animSpeed = (boss != null) ? boss.JumpSlamAnimSpeed : 1.0f;
            boss.PlaySpineAnimation(anim, false, animSpeed);

            float elapsed = 0f;
            while (elapsed < jumpDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / jumpDuration);

                // Quỹ đạo nhảy hình Parabol
                float currentX = Mathf.Lerp(startPos.x, targetPos.x, t);
                float arcY = Mathf.Sin(t * Mathf.PI) * jumpHeight;
                float currentY = Mathf.Lerp(startPos.y, targetPos.y, t) + arcY;

                boss.transform.position = new Vector3(currentX, currentY, startPos.z);
                yield return null;
            }

            boss.transform.position = targetPos;

            // Xử lý khi chạm đất: Spawn Particle + Damage + Camera Shake
            SpawnSlamParticle(targetPos);
            TriggerCameraShake();
            CheckImpactDamage(targetPos);

            yield return new WaitForSeconds(0.4f);
            FinishAttack();
        }

        private void SpawnSlamParticle(Vector3 pos)
        {
            if (slamParticlePrefab != null)
            {
                GameObject vfx = Instantiate(slamParticlePrefab, pos, Quaternion.identity);
                Destroy(vfx, 2.5f);
            }
            else if (boss.ShockwavePrefab != null)
            {
                GameObject vfx = Instantiate(boss.ShockwavePrefab, pos, Quaternion.identity);
                BossShockwave shock = vfx.GetComponent<BossShockwave>();
                if (shock != null)
                {
                    shock.Initialize(1f, 0f, attackData != null ? attackData.damage : 30, boss.transform);
                }
                Destroy(vfx, 2.5f);
            }
        }

        private void CheckImpactDamage(Vector3 pos)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(pos, impactRadius, playerLayer);
            foreach (var hit in hits)
            {
                if (hit.transform == boss.transform || hit.transform.IsChildOf(boss.transform) || hit.transform.root == boss.transform.root) continue;
                DamageUtility.DealDamage(hit.gameObject, attackData != null ? attackData.damage : 30, boss.transform);
            }
        }

        private void TriggerCameraShake()
        {
            BossCameraShake shake = boss.GetComponent<BossCameraShake>();
            if (shake == null)
            {
                shake = boss.gameObject.AddComponent<BossCameraShake>();
            }
            shake.TriggerShake(shakeForce, 1.0f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            Gizmos.DrawSphere(transform.position, impactRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, impactRadius);
        }
    }
}
