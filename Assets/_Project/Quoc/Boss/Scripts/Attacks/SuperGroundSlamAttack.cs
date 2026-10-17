using System.Collections;
using UnityEngine;

namespace Quoc.Boss
{
    public class SuperGroundSlamAttack : BossAttackBase
    {
        public override string AttackName => "Super Ground Slam";

        [Header("Settings")]
        [SerializeField, Tooltip("Bán kính tác động siêu giậm đất")]
        private float superSlamRadius = 3.5f;

        [SerializeField, Tooltip("LayerMask của Player")]
        private LayerMask playerLayer = ~0;

        [Header("Camera Shake")]
        [SerializeField, Tooltip("Cường độ rung camera khi đập đất")]
        private float shakeForce = 2.2f;

        public override void Execute(BossController boss, BossAttackData data, System.Action onComplete)
        {
            base.Execute(boss, data, onComplete);
            StartCoroutine(SuperSlamRoutine());
        }

        private IEnumerator SuperSlamRoutine()
        {
            boss.Movement.StopMovement();

            string anim = (boss != null && !string.IsNullOrEmpty(boss.SuperGroundSlamAnimName)) ? boss.SuperGroundSlamAnimName :
                          (attackData != null ? attackData.spineAnimationName : "Skill");
            float animSpeed = (boss != null) ? boss.SuperGroundSlamAnimSpeed : 1.0f;
            boss.PlaySpineAnimation(anim, false, animSpeed);

            yield return new WaitForSeconds(0.25f);

            TriggerCameraShake();

            Vector2 slamPoint = boss.GroundPoint.position;
            Collider2D[] hits = Physics2D.OverlapCircleAll(slamPoint, superSlamRadius, playerLayer);
            foreach (var hit in hits)
            {
                if (hit.transform == boss.transform || hit.transform.IsChildOf(boss.transform) || hit.transform.root == boss.transform.root) continue;
                DamageUtility.DealDamage(hit.gameObject, attackData.damage, boss.transform);
            }

            float speed = attackData.shockwaveSpeed > 0 ? attackData.shockwaveSpeed : 10f;

            SpawnShockwave(-1f, speed);
            SpawnShockwave(1f, speed);

            yield return new WaitForSeconds(0.15f);

            SpawnShockwave(-1f, speed * 1.3f);
            SpawnShockwave(1f, speed * 1.3f);

            yield return new WaitForSeconds(0.4f);
            FinishAttack();
        }

        private void SpawnShockwave(float dirX, float speed)
        {
            if (boss.ShockwavePrefab == null) return;

            GameObject shockwaveObj = Instantiate(boss.ShockwavePrefab, boss.GroundPoint.position, Quaternion.identity);
            BossShockwave shockwave = shockwaveObj.GetComponent<BossShockwave>();
            if (shockwave != null)
            {
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

            Gizmos.color = new Color(1f, 0.2f, 0f, 0.45f);
            Gizmos.DrawSphere(point.position, superSlamRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(point.position, superSlamRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(point.position, point.position + Vector3.left * 12f);
            Gizmos.DrawLine(point.position, point.position + Vector3.right * 12f);
        }
    }
}
