using System;

public sealed class BossStateMachine
{
    private BossStateId state;
    private float stateTimer;

    public event Action<BossStateId, BossStateId> StateChanged;

    public BossStateId State => state;
    public float StateTimer => stateTimer;

    public void Reset(BossStateId initialState)
    {
        state = initialState;
        stateTimer = 0f;
    }

    public void Enter(BossStateId nextState, float duration = 0f)
    {
        if (state == nextState && stateTimer == duration)
        {
            return;
        }

        BossStateId previous = state;
        state = nextState;
        stateTimer = Math.Max(0f, duration);
        StateChanged?.Invoke(previous, nextState);
    }

    public void Tick(float deltaTime)
    {
        if (stateTimer <= 0f)
        {
            return;
        }

        stateTimer = Math.Max(0f, stateTimer - Math.Max(0f, deltaTime));
    }

    public bool HasTimerExpired => stateTimer <= 0f;
}
