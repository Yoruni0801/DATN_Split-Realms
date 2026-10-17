public readonly struct BossSenseSnapshot
{
    public BossSenseSnapshot(
        bool hasTarget,
        float targetDistance,
        float targetHorizontalDistance,
        float targetHeightDelta,
        int directionToTarget,
        bool isGrounded,
        bool hasGroundAhead,
        bool hasWallAhead,
        bool hasIncomingThreat,
        float incomingThreatVelocityX,
        float incomingThreatVelocityY,
        float estimatedThreatTime)
    {
        HasTarget = hasTarget;
        TargetDistance = targetDistance;
        TargetHorizontalDistance = targetHorizontalDistance;
        TargetHeightDelta = targetHeightDelta;
        DirectionToTarget = directionToTarget;
        IsGrounded = isGrounded;
        HasGroundAhead = hasGroundAhead;
        HasWallAhead = hasWallAhead;
        HasIncomingThreat = hasIncomingThreat;
        IncomingThreatVelocityX = incomingThreatVelocityX;
        IncomingThreatVelocityY = incomingThreatVelocityY;
        EstimatedThreatTime = estimatedThreatTime;
    }

    public bool HasTarget { get; }
    public float TargetDistance { get; }
    public float TargetHorizontalDistance { get; }
    public float TargetHeightDelta { get; }
    public int DirectionToTarget { get; }
    public bool IsGrounded { get; }
    public bool HasGroundAhead { get; }
    public bool HasWallAhead { get; }
    public bool HasIncomingThreat { get; }
    public float IncomingThreatVelocityX { get; }
    public float IncomingThreatVelocityY { get; }
    public float EstimatedThreatTime { get; }
}
