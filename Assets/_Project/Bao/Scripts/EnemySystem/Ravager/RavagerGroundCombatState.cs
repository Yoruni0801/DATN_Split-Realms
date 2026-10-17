using UnityEngine;

namespace Bao.Enemies
{
    public class RavagerGroundCombatState : BaoEnemyState
    {
        private RavagerEnemy ravager;
        private float stateTimer;
        private float groundCombatDuration = 2f;

        public RavagerGroundCombatState(BaoEnemyBase enemy) : base(enemy)
        {
            ravager = enemy as RavagerEnemy;
        }

        public override void Enter()
        {
            stateTimer = 0f;
            ravager.SetFlyingAnimation(false);
            ravager.SetLocomotionAnimation(true);
        }

        public override void LogicUpdate()
        {
            if (!ravager.HasTargetInDetectionRange())
            {
                ravager.SetStateToPatrol();
                return;
            }

            stateTimer += Time.deltaTime;

            if (ravager.HasTargetInAttackRange())
            {
                ravager.StopHorizontalMovement();
                ravager.SetLocomotionAnimation(false);

                if (Random.Range(0, 100) < ravager.DodgeChancePercent)
                {
                    ravager.SetStateToDodge();
                }
                else
                {
                    ravager.SetStateToAttack();
                }
                return;
            }

            // Hết phase ground thì roll tỉ lệ để bay, không còn luân chuyển theo timer cố định
            if (stateTimer >= groundCombatDuration)
            {
                if (Random.Range(0, 100) < ravager.flyChancePercent)
                {
                    ravager.SetStateToFly();
                }
                else
                {
                    stateTimer = 0f;
                }
            }
        }

        public override void PhysicsUpdate()
        {
            if (ravager.Target == null)
            {
                ravager.StopHorizontalMovement();
                ravager.SetLocomotionAnimation(false);
                return;
            }

            if (!ravager.IsWithinBoundary())
            {
                ravager.StopHorizontalMovement();
                ravager.SetLocomotionAnimation(false);
                return;
            }

            if (ravager.HasTargetInAttackRange())
            {
                ravager.StopHorizontalMovement();
                ravager.SetLocomotionAnimation(false);
                return;
            }

            float direction = ravager.Target.position.x >= ravager.transform.position.x ? 1f : -1f;
            ravager.SetLocomotionAnimation(true);
            ravager.MoveForward(direction);
        }
    }
}
