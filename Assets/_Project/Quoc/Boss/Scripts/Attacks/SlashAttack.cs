using System.Collections;
using UnityEngine;

namespace Quoc.Boss
{
    public class SlashAttack : BossAttackBase
    {
        public override string AttackName => "Slash";

        [Header("Hitbox Settings")]
        [SerializeField, Tooltip("Bán kính chém gây sát thương")]
        private float attackRadius = 1.8f;

        [SerializeField, Tooltip("LayerMask của Player")]
        private LayerMask playerLayer = ~0;

        public override void Execute(BossController boss, BossAttackData data, System.Action onComplete)
        {
            base.Execute(boss, data, onComplete);
            StartCoroutine(SlashRoutine());
        }

        private IEnumerator SlashRoutine()
        {
            boss.Movement.StopMovement();
            boss.Movement.FacePlayer();

            string anim = (boss != null && !string.IsNullOrEmpty(boss.SingleSlashAnimName)) ? boss.SingleSlashAnimName :
                          (attackData != null ? attackData.spineAnimationName : "Attack");
            float animSpeed = (boss != null) ? boss.SingleSlashAnimSpeed : 1.0f;
            boss.PlaySpineAnimation(anim, false, animSpeed);

            yield return new WaitForSeconds(0.15f);

            CheckHit();

            yield return new WaitForSeconds(0.35f);
            FinishAttack();
        }

        private void CheckHit()
        {
            Vector2 hitPoint = boss.MeleePoint.position;
            Collider2D[] hits = Physics2D.OverlapCircleAll(hitPoint, attackRadius, playerLayer);

            foreach (var hit in hits)
            {
                if (hit.transform == boss.transform || hit.transform.IsChildOf(boss.transform) || hit.transform.root == boss.transform.root) continue;
                DamageUtility.DealDamage(hit.gameObject, attackData.damage, boss.transform);
            }
        }

        private void OnDrawGizmosSelected()
        {
            BossController b = boss != null ? boss : GetComponent<BossController>();
            Transform point = b != null && b.MeleePoint != null ? b.MeleePoint : transform;

            Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
            Gizmos.DrawSphere(point.position, attackRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(point.position, attackRadius);
        }
    }
}
