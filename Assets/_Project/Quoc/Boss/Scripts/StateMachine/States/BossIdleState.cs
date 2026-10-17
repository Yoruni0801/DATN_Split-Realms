using UnityEngine;

namespace Quoc.Boss
{
    public class BossIdleState : BossState
    {
        private float idleTimer;
        private float idleDuration = 0.35f;

        public BossIdleState(BossController boss, BossStateMachine stateMachine) : base(boss, stateMachine) { }

        public override void Enter()
        {
            idleTimer = 0f;
            boss.Movement.StopMovement();
            boss.PlayIdleAnimation();
        }

        public override void UpdateState()
        {
            if (boss.IsDead)
            {
                stateMachine.ChangeState(boss.DeadState);
                return;
            }

            if (boss.CheckPendingPhaseTransition())
            {
                stateMachine.ChangeState(boss.PhaseTransitionState);
                return;
            }

            // KHÓA KHI ĐANG CHẠY CUTSCENE: không quay mặt tìm player, không đếm giờ đánh, dừng di chuyển
            if (!boss.IsAggroEnabled)
            {
                boss.Movement.StopMovement();
                idleTimer = 0f;
                return;
            }

            // Luôn đảm bảo có target Player khi đang trong Update
            if (boss.Player == null)
            {
                boss.ResolvePlayer(true);
            }

            boss.Movement.FacePlayer();
            idleTimer += Time.deltaTime;

            if (idleTimer >= idleDuration)
            {
                BossAttackData selectedAttack = boss.AttackSelector.SelectAttack(
                    boss.CurrentPhase,
                    boss.DistanceToPlayer()
                );

                if (selectedAttack != null)
                {
                    boss.SetCurrentAttack(selectedAttack);
                    stateMachine.ChangeState(boss.TelegraphState);
                }
                else
                {
                    // Khi Boss đang đứng nghỉ (Pause), giữ timer ở mức idleDuration để frame tiếp theo kiểm tra ngay khi hết pause
                    idleTimer = idleDuration;
                }
            }
        }
    }
}
