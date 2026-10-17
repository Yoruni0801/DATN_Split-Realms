using UnityEngine;

namespace Bao.Enemies
{
    public class EmberwormActiveState : BaoEnemyState
    {
        private readonly EmberwormEnemy worm;
        private float stateTimer;

        public EmberwormActiveState(BaoEnemyBase enemy) : base(enemy)
        {
            worm = enemy as EmberwormEnemy;
        }

        public override void Enter()
        {
            stateTimer = 0f;
            worm.transform.position = worm.EmergingPosition;
        }

        public override void LogicUpdate()
        {
            stateTimer += Time.deltaTime;
            
            if (stateTimer >= worm.ActiveTime)
            {
                worm.StateMachine.SetState(worm.RetractState);
            }
        }
    }
}
