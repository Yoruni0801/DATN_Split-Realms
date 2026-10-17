public enum BossCommandType
{
    None,
    EnterState,
    PlayAnimation,
    StartAttackHitbox,
    StopAttackHitbox,
    SpawnShockwave,
    StopHorizontal,
    SetHorizontalIntent,
    Jump,
    FaceDirection,
    Die
}

public readonly struct BossCommand
{
    public static readonly BossCommand None = new BossCommand(BossCommandType.None);

    public BossCommand(
        BossCommandType type,
        BossStateId state = BossStateId.Idle,
        BossAnimationId animation = BossAnimationId.None,
        int direction = 0,
        float value = 0f,
        float duration = 0f,
        bool restart = false)
    {
        Type = type;
        State = state;
        Animation = animation;
        Direction = direction;
        Value = value;
        Duration = duration;
        Restart = restart;
    }

    public BossCommandType Type { get; }
    public BossStateId State { get; }
    public BossAnimationId Animation { get; }
    public int Direction { get; }
    public float Value { get; }
    public float Duration { get; }
    public bool Restart { get; }
}
