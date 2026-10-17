using UnityEngine;

namespace Bao.Enemies
{
    public class EmberwormRetractState : BaoEnemyState
    {
        private readonly EmberwormEnemy worm;
        private float stateTimer;

        public EmberwormRetractState(BaoEnemyBase enemy) : base(enemy)
        {
            worm = enemy as EmberwormEnemy;
        }

        public override void Enter()
        {
            stateTimer = 0f;
        }

        public override void LogicUpdate()
        {
            stateTimer += Time.deltaTime;
            
            float progress = Mathf.Clamp01(stateTimer / worm.RetractTime);
            worm.transform.position = Vector3.Lerp(worm.EmergingPosition, worm.HiddenPosition, progress);

            if (stateTimer >= worm.RetractTime)
            {
                if (worm.IsLooping)
                {
                    worm.StateMachine.SetState(worm.IdleState);
                }
                else
                {
                    worm.enabled = false;
                }
            }
        }
    }
}
