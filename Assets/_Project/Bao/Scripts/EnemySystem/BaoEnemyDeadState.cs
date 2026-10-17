namespace Bao.Enemies
{
    public class BaoEnemyDeadState : BaoEnemyState
    {
        public BaoEnemyDeadState(BaoEnemyBase enemy) : base(enemy)
        {
        }

        public override void Enter()
        {
            Enemy.StopHorizontalMovement();
            Enemy.SetFlyingAnimation(false);
            Enemy.SetLocomotionAnimation(false);
        }

        public override void PhysicsUpdate()
        {
            Enemy.StopHorizontalMovement();
        }
    }
}