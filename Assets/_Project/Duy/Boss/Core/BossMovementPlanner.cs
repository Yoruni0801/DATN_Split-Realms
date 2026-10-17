public readonly struct BossMovementIntent
{
    public BossMovementIntent(float horizontalVelocity, bool immediate)
    {
        HorizontalVelocity = horizontalVelocity;
        Immediate = immediate;
    }

    public float HorizontalVelocity { get; }
    public bool Immediate { get; }
}

public sealed class BossMovementPlanner
{
    public BossMovementIntent Stop()
    {
        return new BossMovementIntent(0f, true);
    }

    public BossMovementIntent Move(int direction, float speed, float phaseMultiplier, bool immediate)
    {
        return new BossMovementIntent(direction * speed * phaseMultiplier, immediate);
    }
}
