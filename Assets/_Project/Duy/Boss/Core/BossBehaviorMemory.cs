public sealed class BossBehaviorMemory
{
    private bool wasTargetGrounded = true;
    private float previousTargetVelocityX;
    private float previousTargetVelocityY;

    public int RecentJumpCount { get; private set; }
    public int RecentDashCount { get; private set; }
    public int RecentPassThroughCount { get; private set; }
    public float SampleTimer { get; private set; }

    public void Tick(
        float deltaTime,
        bool targetGrounded,
        float targetVelocityX,
        float targetVelocityY,
        float dashSpeedThreshold,
        float sampleWindow)
    {
        SampleTimer += Max(0f, deltaTime);

        if (wasTargetGrounded && !targetGrounded && targetVelocityY > 0.1f)
        {
            RecentJumpCount++;
        }

        if (Abs(targetVelocityX) >= dashSpeedThreshold &&
            Abs(previousTargetVelocityX) < dashSpeedThreshold)
        {
            RecentDashCount++;
        }

        if (Sign(targetVelocityX) != Sign(previousTargetVelocityX) &&
            Abs(targetVelocityX) >= dashSpeedThreshold)
        {
            RecentPassThroughCount++;
        }

        if (SampleTimer >= sampleWindow)
        {
            RecentJumpCount = 0;
            RecentDashCount = 0;
            RecentPassThroughCount = 0;
            SampleTimer = 0f;
        }

        wasTargetGrounded = targetGrounded;
        previousTargetVelocityX = targetVelocityX;
        previousTargetVelocityY = targetVelocityY;
    }

    public void Reset()
    {
        wasTargetGrounded = true;
        previousTargetVelocityX = 0f;
        previousTargetVelocityY = 0f;
        RecentJumpCount = 0;
        RecentDashCount = 0;
        RecentPassThroughCount = 0;
        SampleTimer = 0f;
    }

    private static float Abs(float value) => value < 0f ? -value : value;
    private static float Max(float a, float b) => a > b ? a : b;
    private static int Sign(float value) => value > 0f ? 1 : value < 0f ? -1 : 0;
}
