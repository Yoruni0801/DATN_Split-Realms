namespace Bao.Enemies
{
    public class BaoEnemyStateMachine
    {
        public BaoEnemyState CurrentState { get; private set; }

        public string CurrentStateName => CurrentState == null ? "None" : CurrentState.GetType().Name;

        public void SetState(BaoEnemyState nextState)
        {
            if (nextState == null || nextState == CurrentState)
            {
                return;
            }

            CurrentState?.Exit();
            CurrentState = nextState;
            CurrentState.Enter();
        }

        public void LogicUpdate()
        {
            CurrentState?.LogicUpdate();
        }

        public void PhysicsUpdate()
        {
            CurrentState?.PhysicsUpdate();
        }

        public void LateUpdate()
        {
            CurrentState?.LateUpdate();
        }
    }
}