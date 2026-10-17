using UnityEngine;

namespace Bao.Enemies
{
    public class MoonflutterExplodeState : BaoEnemyState
    {
        private readonly MoonflutterEnemy moonflutter;
        private float startTime;
        private bool hasExploded;

        public MoonflutterExplodeState(BaoEnemyBase enemy) : base(enemy)
        {
            moonflutter = enemy as MoonflutterEnemy;
        }

        public override void Enter()
        {
            Enemy.StartWarningRoutine();
            startTime = Time.time;
            hasExploded = false;
            
            moonflutter.StopHorizontalMovement();
            
            moonflutter.MarkAttackStarted();
            
            if (moonflutter.Animator != null)
            {
                moonflutter.PlayAttackAnimation(); // Triggers "isAttack" / "Attack" based on animator parameters
                moonflutter.Animator.SetTrigger("Swell"); // Phồng to
                moonflutter.Animator.SetBool("isSwelling", true);
            }
        }

        public override void LogicUpdate()
        {
            float elapsed = Time.time - startTime;
            
            if (!hasExploded && elapsed > moonflutter.SwellDuration)
            {
                moonflutter.Detonate();
                hasExploded = true;
            }
        }

        public override void Exit()
        {
            Enemy.StopWarningRoutine();
            if (moonflutter.Animator != null)
            {
                moonflutter.Animator.SetBool("isSwelling", false);
            }
        }
    }
}
