using UnityEngine;

namespace Bao.Enemies
{
    public class HuskfangRootAttackState : BaoEnemyState
    {
        private readonly HuskfangEnemy huskfang;
        private enum Phase { Anticipation, Attack }
        private const float AnticipationDuration = 0.4f;
        private const float RootWarningDuration = 0.5f;
        private Phase currentPhase;
        private float phaseTimer;
        private bool hasSummoned;

        public HuskfangRootAttackState(BaoEnemyBase enemy) : base(enemy)
        {
            huskfang = enemy as HuskfangEnemy;
        }

        public override void Enter()
        {
            huskfang.StopHorizontalMovement();
            huskfang.SetLocomotionAnimation(false);
            huskfang.MarkAttackStarted();
            
            StartPhase(Phase.Anticipation);
        }

        private void StartPhase(Phase phase)
        {
            currentPhase = phase;
            phaseTimer = 0f;

            if (phase == Phase.Anticipation)
            {
                if (huskfang.Target != null)
                {
                    huskfang.FacePosition(huskfang.Target.position);
                }
                huskfang.ShowWarningNow();
            }
            else if (phase == Phase.Attack)
            {
                huskfang.StopWarningRoutine();
                hasSummoned = false;
                
                if (huskfang.Animator != null)
                {
                    huskfang.Animator.SetTrigger("Stomp"); // Dậm chân
                }
            }
        }

        public override void LogicUpdate()
        {
            phaseTimer += Time.deltaTime;

            switch (currentPhase)
            {
                case Phase.Anticipation:
                    if (huskfang.Target != null)
                    {
                        huskfang.FacePosition(huskfang.Target.position);
                    }

                    if (phaseTimer >= AnticipationDuration)
                    {
                        StartPhase(Phase.Attack);
                    }
                    break;

                case Phase.Attack:
                    if (!hasSummoned && phaseTimer >= huskfang.AttackDelay)
                    {
                        huskfang.SummonRoots();
                        hasSummoned = true;
                    }

                    float requiredDuration = Mathf.Max(huskfang.AttackStateDuration, huskfang.AttackDelay + RootWarningDuration + 0.1f);
                    if (phaseTimer >= requiredDuration)
                    {
                        if (huskfang.HasTargetInDetectionRange())
                            huskfang.SetStateToChase();
                        else
                            huskfang.SetStateToPatrol();
                    }
                    break;
            }
        }
        
        public override void Exit()
        {
            base.Exit();
            huskfang.StopWarningRoutine();
        }
    }
}
