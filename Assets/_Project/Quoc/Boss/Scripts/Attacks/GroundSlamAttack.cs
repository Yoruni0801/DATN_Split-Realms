using System.Collections;
using UnityEngine;

namespace Quoc.Boss
{
    public class GroundSlamAttack : BossAttackBase
    {
        public override string AttackName => "Ground Slam";

        [Header("Settings")]
        [SerializeField, Tooltip("Bán kính nổ giậm đất tại vị trí chân")]
        private float slamImpactRadius = 2.5f;

        [SerializeField, Tooltip("LayerMask của Player")]
        private LayerMask playerLayer = ~0;

        [Header("Camera Shake")]
        [SerializeField, Tooltip("Cường độ rung camera khi đập đất")]
        private float shakeForce = 1.5f;

        public override void Execute(BossController boss, BossAttackData data, System.Action onComplete)
        {
            base.Execute(boss, data, onComplete);
            StartCoroutine(SlamRoutine());
        }

        private IEnumerator SlamRoutine()
        {
            boss.Movement.StopMovement();

            string anim = (boss != null && !string.IsNullOrEmpty(boss.GroundSlamAnimName)) ? boss.GroundSlamAnimName :
                          (attackData != null ? attackData.spineAnimationName : "Skill");
            float animSpeed = (boss != null) ? boss.GroundSlamAnimSpeed : 1.0f;
            boss.PlaySpineAnimation(anim, false, animSpeed);

            yield return new WaitForSeconds(0.2f);

            TriggerCameraShake();

            Vector2 slamPoint = boss.GroundPoint.position;
            Collider2D[] hits = Physics2D.OverlapCircleAll(slamPoint, slamImpactRadius, playerLayer);
            foreach (var hit in hits)
            {
                if (hit.transform == boss.transform || hit.transform.IsChildOf(boss.transform) || hit.transform.root == boss.transform.root) continue;
                DamageUtility.DealDamage(hit.gameObject, attackData.damage, boss.transform);
            }

            SpawnShockwave(-1f);
            SpawnShockwave(1f);

            yield return new WaitForSeconds(0.4f);
            FinishAttack();
        }

        private void SpawnShockwave(float dirX)
        {
            if (boss.ShockwavePrefab == null) return;

            GameObject shockwaveObj = Instantiate(boss.ShockwavePrefab, boss.GroundPoint.position, Quaternion.identity);
            BossShockwave shockwave = shockwaveObj.GetComponent<BossShockwave>();
            if (shockwave != null)
            {
                float speed = attackData.shockwaveSpeed > 0 ? attackData.shockwaveSpeed : 8f;
                shockwave.Initialize(dirX, speed, attackData.damage, boss.transform);
            }
        }

        private void TriggerCameraShake()
        {
            BossCameraShake shake = boss.GetComponent<BossCameraShake>();
            if (shake == null)
            {
                shake = boss.gameObject.AddComponent<BossCameraShake>();
            }
            shake.TriggerShake(shakeForce);
        }

        private void OnDrawGizmosSelected()
        {
            BossController b = boss != null ? boss : GetComponent<BossController>();
            Transform point = b != null && b.GroundPoint != null ? b.GroundPoint : transform;

            Gizmos.color = new Color(0.9f, 0.5f, 0f, 0.35f);
            Gizmos.DrawSphere(point.position, slamImpactRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(point.position, slamImpactRadius);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(point.position, point.position + Vector3.left * 8f);
            Gizmos.DrawLine(point.position, point.position + Vector3.right * 8f);
        }
    }
}
