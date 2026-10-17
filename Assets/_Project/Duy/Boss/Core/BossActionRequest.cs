public readonly struct BossActionRequest
{
    public static readonly BossActionRequest None = new BossActionRequest(BossActionType.None);

    public BossActionRequest(
        BossActionType type,
        int direction = 0,
        float duration = 0f,
        float speed = 0f,
        bool force = false)
    {
        Type = type;
        Direction = direction;
        Duration = duration;
        Speed = speed;
        Force = force;
    }

    public BossActionType Type { get; }
    public int Direction { get; }
    public float Duration { get; }
    public float Speed { get; }
    public bool Force { get; }
    public bool HasAction => Type != BossActionType.None;
}
