using UnityEngine;

namespace Bao.Enemies
{
    public class FungloomShootState : BaoEnemyState
    {
        private readonly FungloomEnemy fungloom;
        private enum Phase { Anticipation, Attack }
        private Phase currentPhase;
        private float phaseTimer;
        private bool hasShot;

        public FungloomShootState(BaoEnemyBase enemy) : base(enemy)
        {
            fungloom = enemy as FungloomEnemy;
        }

        public override void Enter()
        {
            fungloom.StopHorizontalMovement();
            fungloom.MarkAttackStarted();
            
            StartPhase(Phase.Anticipation);
        }

        private void StartPhase(Phase phase)
        {
            currentPhase = phase;
            phaseTimer = 0f;

            if (phase == Phase.Anticipation)
            {
                if (fungloom.Target != null)
                {
                    fungloom.FacePosition(fungloom.Target.position);
                }
                fungloom.ShowWarningNow(); // Bật dấu chấm than
            }
            else if (phase == Phase.Attack)
            {
                fungloom.StopWarningRoutine(); // Tắt dấu chấm than
                hasShot = false;
                
                if (fungloom.Animator != null)
                {
                    fungloom.Animator.SetTrigger("Shoot"); // Chơi animation bắn
                }
            }
        }

        public override void LogicUpdate()
        {
            phaseTimer += Time.deltaTime;

            switch (currentPhase)
            {
                case Phase.Anticipation:
                    if (fungloom.Target != null)
                    {
                        fungloom.FacePosition(fungloom.Target.position);
                    }

                    if (phaseTimer >= fungloom.AnticipationDuration)
                    {
                        StartPhase(Phase.Attack);
                    }
                    break;

                case Phase.Attack:
                    if (!hasShot && phaseTimer >= fungloom.ShootDelay)
                    {
                        fungloom.SpawnBee();
                        hasShot = true;
                    }

                    float requiredDuration = Mathf.Max(fungloom.AttackStateDuration, fungloom.ShootDelay + 0.1f);
                    if (phaseTimer >= requiredDuration)
                    {
                        if (fungloom.HasTargetInDetectionRange())
                            fungloom.SetStateToChase();
                        else
                            fungloom.SetStateToPatrol();
                    }
                    break;
            }
        }
        
        public override void Exit()
        {
            base.Exit();
            fungloom.StopWarningRoutine();
        }
    }
}
