using UnityEngine;

namespace Quoc.Boss
{
    /// <summary>
    /// Quản lý trạng thái hiện tại của Boss
    /// </summary>
    public class BossStateMachine
    {
        public BossState CurrentState { get; private set; }

        public void Initialize(BossState startingState)
        {
            CurrentState = startingState;
            CurrentState?.Enter();
        }

        public void ChangeState(BossState newState)
        {
            if (newState == null || newState == CurrentState) return;

            CurrentState?.Exit();
            CurrentState = newState;
            CurrentState.Enter();
        }

        public void Update()
        {
            CurrentState?.UpdateState();
        }

        public void FixedUpdate()
        {
            CurrentState?.FixedUpdateState();
        }
    }
}
