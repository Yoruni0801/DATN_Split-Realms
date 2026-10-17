public static class SceneGateTravelState
{
    public static string PendingSpawnGateId { get; private set; }

    public static bool HasPendingSpawnGate => !string.IsNullOrEmpty(PendingSpawnGateId);

    public static void SetNextSpawnGate(string gateId)
    {
        PendingSpawnGateId = gateId;
    }

    public static bool MatchesPendingSpawnGate(string gateId)
    {
        return !string.IsNullOrEmpty(gateId)
            && !string.IsNullOrEmpty(PendingSpawnGateId)
            && PendingSpawnGateId == gateId;
    }

    public static void ClearPendingSpawnGate()
    {
        PendingSpawnGateId = null;
    }

    public static bool ConsumeIfMatches(string gateId)
    {
        if (!MatchesPendingSpawnGate(gateId))
        {
            return false;
        }

        ClearPendingSpawnGate();
        return true;
    }
}
