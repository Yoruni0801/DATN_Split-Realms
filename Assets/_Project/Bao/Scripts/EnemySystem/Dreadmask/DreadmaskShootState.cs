using UnityEngine;

namespace Bao.Enemies
{
    public class DreadmaskShootState : BaoEnemyState
    {
        private readonly DreadmaskEnemy dreadmask;
        private float startTime;
        private bool hasShot;

        public DreadmaskShootState(BaoEnemyBase enemy) : base(enemy)
        {
            dreadmask = enemy as DreadmaskEnemy;
        }

        public override void Enter()
        {
            startTime = Time.time;
            hasShot = false;
            
            dreadmask.StopHorizontalMovement();
            
            if (dreadmask.Target != null)
            {
                dreadmask.FacePosition(dreadmask.Target.position);
            }
            
            dreadmask.MarkAttackStarted();
            
            if (dreadmask.Animator != null)
            {
                dreadmask.Animator.SetTrigger("Attack"); // Bắn đá
            }
        }

        public override void LogicUpdate()
        {
            float elapsed = Time.time - startTime;
            
            if (!hasShot && elapsed > dreadmask.ShootDelay)
            {
                dreadmask.SpawnRock();
                hasShot = true;
            }
            
            // Wait for both the visual attack duration and the shooting delay to complete
            float requiredDuration = Mathf.Max(dreadmask.AttackStateDuration, dreadmask.ShootDelay + 0.1f);
            if (elapsed > requiredDuration)
            {
                if (dreadmask.HasTargetInDetectionRange())
                    dreadmask.SetStateToChase();
                else
                    dreadmask.SetStateToPatrol();
            }
        }
    }
}
