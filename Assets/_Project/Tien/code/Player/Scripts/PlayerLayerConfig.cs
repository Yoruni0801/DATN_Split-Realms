using UnityEngine;

public static class PlayerLayerConfig
{
    public static LayerMask GroundLayer => ResolveMask(FallbackGroundLayer, "Ground");
    public static LayerMask WaterLayer => ResolveMask(FallbackWaterLayer, "Water");
    public static LayerMask AcidLayer => ResolveMask(0, "Axit", "Acid");
    public static LayerMask EnemyLayer => ResolveMask(FallbackEnemyLayer, "Enemy");
    public static LayerMask AttackTargetLayer => ResolveMask(FallbackAttackTargetLayer, "Enemy", "Boss");
    public static LayerMask EnvironmentLayer => ResolveMask(FallbackEnvironmentLayer, "Default", "Ground", "Platform", "Wall", "Obstacle");
    public static LayerMask WorldAObstacleLayer => ResolveMask(FallbackGroundLayer, "Obstacle_WorldA", "Ground");
    public static LayerMask WorldBObstacleLayer => ResolveMask(FallbackGroundLayer, "Obstacle_WorldB", "Ground");

    private const int FallbackGroundLayer = 1 << 3;
    private const int FallbackWaterLayer = 1 << 4;
    private const int FallbackEnemyLayer = 1 << 7;
    private const int FallbackAttackTargetLayer = (1 << 7) | (1 << 11);
    private const int FallbackEnvironmentLayer = (1 << 0) | (1 << 3);

    private static LayerMask ResolveMask(int fallback, params string[] layerNames)
    {
        int mask = LayerMask.GetMask(layerNames);
        return mask != 0 ? mask : fallback;
    }
}
