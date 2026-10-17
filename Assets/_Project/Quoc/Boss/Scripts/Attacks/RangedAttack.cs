using System.Collections;
using UnityEngine;

namespace Quoc.Boss
{
    public class RangedAttack : BossAttackBase
    {
        public override string AttackName => "Ranged Attack";

        public override void Execute(BossController boss, BossAttackData data, System.Action onComplete)
        {
            base.Execute(boss, data, onComplete);
            StartCoroutine(RangedRoutine());
        }

        private IEnumerator RangedRoutine()
        {
            boss.Movement.StopMovement();
            boss.Movement.FacePlayer();

            string anim = (boss != null && !string.IsNullOrEmpty(boss.RangedAnimName)) ? boss.RangedAnimName :
                          (attackData != null ? attackData.spineAnimationName : "Skill");
            float animSpeed = (boss != null) ? boss.RangedAnimSpeed : 1.0f;
            boss.PlaySpineAnimation(anim, false, animSpeed);

            yield return new WaitForSeconds(0.2f);

            Transform p = boss.Player ?? boss.RawPlayer ?? boss.ResolvePlayer();
            if (boss.ProjectilePrefab != null && p != null)
            {
                Vector2 firePoint = boss.RangedPoint.position;
                Vector2 targetPos = p.position;
                Vector2 dir = (targetPos - firePoint).normalized;

                GameObject projObj = Instantiate(boss.ProjectilePrefab, firePoint, Quaternion.identity);
                BossProjectile proj = projObj.GetComponent<BossProjectile>();
                if (proj != null)
                {
                    float speed = attackData.projectileSpeed > 0 ? attackData.projectileSpeed : 12f;
                    proj.Initialize(dir, speed, attackData.damage, boss.transform);
                }
            }

            yield return new WaitForSeconds(0.3f);
            FinishAttack();
        }

        private void OnDrawGizmosSelected()
        {
            BossController b = boss != null ? boss : GetComponent<BossController>();
            Transform point = b != null && b.RangedPoint != null ? b.RangedPoint : transform;

            Gizmos.color = new Color(0f, 0.7f, 1f, 0.4f);
            Gizmos.DrawSphere(point.position, 0.4f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(point.position, 0.4f);

            if (b != null && b.Player != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(point.position, b.Player.position);
            }
        }
    }
}
