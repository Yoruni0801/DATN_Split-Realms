using UnityEngine;

namespace Bao.Enemies
{
    public class IcefinStunState : BaoEnemyState
    {
        private readonly IcefinEnemy icefin;
        private float stunTimer;
        private readonly float stunDuration;

        public IcefinStunState(BaoEnemyBase enemy, float duration = 1.2f) : base(enemy)
        {
            icefin = enemy as IcefinEnemy;
            stunDuration = duration;
        }

        public override void Enter()
        {
            stunTimer = 0f;

            // Dừng hoàn toàn chuyển động
            if (icefin.Rigidbody2D != null)
            {
                icefin.Rigidbody2D.linearVelocity = Vector2.zero;
            }

            // Tắt cảnh báo đòn đánh
            icefin.StopWarningRoutine();

            // Tắt animation tấn công, chuyển sang idle
            if (icefin.Animator != null)
            {
                icefin.Animator.SetBool("isAttack", false);
                icefin.Animator.SetBool("isFly", false);
                icefin.Animator.SetBool("isIdle", true);
            }

            // Kết thúc dash VFX
            icefin.VfxController?.TriggerDashEnd();

            // Bật hiệu ứng choáng trên đầu
            icefin.SetStunEffectActive(true);

            // Trigger Parry break VFX
            icefin.VfxController?.TriggerParryBreak();
        }

        public override void LogicUpdate()
        {
            stunTimer += Time.deltaTime;

            // Trong lúc choáng, giữ nguyên vận tốc 0
            if (icefin.Rigidbody2D != null)
            {
                icefin.Rigidbody2D.linearVelocity = Vector2.zero;
            }

            if (stunTimer >= stunDuration)
            {
                // Hết choáng: reset attack cooldown để không đánh ngay lập tức
                icefin.MarkAttackStarted();

                if (icefin.HasTargetInDetectionRange())
                {
                    icefin.SetStateToChase();
                }
                else
                {
                    icefin.SetStateToPatrol();
                }
            }
        }

        public override void PhysicsUpdate()
        {
            if (icefin.Rigidbody2D != null)
            {
                icefin.Rigidbody2D.linearVelocity = Vector2.zero;
            }
        }

        public override void Exit()
        {
            icefin.SetStunEffectActive(false);

            if (icefin.Animator != null)
            {
                icefin.Animator.SetBool("isIdle", false);
                icefin.Animator.SetBool("isFly", true);
            }
        }
    }
}
