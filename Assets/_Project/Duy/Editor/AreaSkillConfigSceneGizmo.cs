using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AreaSkillConfigSceneGizmo
{
    private static readonly Color SpawnColor = new Color(0.2f, 0.8f, 1f, 1f);
    private static readonly Color PathColor = new Color(1f, 0.75f, 0.1f, 1f);
    private static readonly Color AreaColor = new Color(0.1f, 0.9f, 0.45f, 1f);
    private static readonly Color FillColor = new Color(0.1f, 0.9f, 0.45f, 0.12f);

    static AreaSkillConfigSceneGizmo()
    {
        SceneView.duringSceneGui -= DrawSelectedAreaSkillConfig;
        SceneView.duringSceneGui += DrawSelectedAreaSkillConfig;
    }

    private static void DrawSelectedAreaSkillConfig(SceneView sceneView)
    {
        AreaSkillConfig config = Selection.activeObject as AreaSkillConfig;
        if (config == null)
        {
            return;
        }

        Player player = Object.FindAnyObjectByType<Player>();
        Vector3 origin = player != null ? player.transform.position : Vector3.zero;
        float facing = player != null && player.FacingDirectionSign < 0f ? -1f : 1f;

        if (config.DeliveryMode == AreaSkillDeliveryMode.JumpApexSpreadShot)
        {
            DrawApexSpreadPreview(config, origin, facing);
            return;
        }

        Vector3 spawn = ResolveSpawnPoint(config, origin, facing);
        Vector3 areaCenter = ResolveAreaCenter(config, origin, spawn, facing);

        DrawSpawn(origin, spawn);
        DrawPath(config, spawn, areaCenter, facing);
        DrawArea(areaCenter, Mathf.Max(0.01f, config.Radius));

        Handles.Label(
            areaCenter + Vector3.up * (config.Radius + 0.25f),
            $"Area Skill Preview\nMode: {config.DeliveryMode}\nRadius: {config.Radius:0.##}");
    }

    private static Vector3 ResolveSpawnPoint(AreaSkillConfig config, Vector3 origin, float facing)
    {
        if (config.DeliveryMode == AreaSkillDeliveryMode.InstantArea)
        {
            return origin;
        }

        Vector2 offset = config.ProjectileSpawnOffset;
        return origin + new Vector3(offset.x * facing, offset.y, 0f);
    }

    private static Vector3 ResolveAreaCenter(AreaSkillConfig config, Vector3 origin, Vector3 spawn, float facing)
    {
        switch (config.DeliveryMode)
        {
            case AreaSkillDeliveryMode.ArcProjectile:
            {
                float previewTime = Mathf.Min(Mathf.Max(0.05f, config.ProjectileLifetime), 1f);
                Vector2 velocity = new Vector2(Mathf.Abs(config.ArcLaunchVelocity.x) * facing, config.ArcLaunchVelocity.y);
                Vector2 gravity = Physics2D.gravity * Mathf.Max(0f, config.ArcGravityScale);
                Vector2 displacement = velocity * previewTime + 0.5f * gravity * previewTime * previewTime;
                return spawn + (Vector3)displacement;
            }

            default:
                Vector2 offset = config.Offset;
                return origin + new Vector3(offset.x * facing, offset.y, 0f);
        }
    }

    private static void DrawSpawn(Vector3 origin, Vector3 spawn)
    {
        Handles.color = SpawnColor;
        Handles.DrawWireDisc(origin, Vector3.forward, 0.2f);
        Handles.DrawSolidDisc(spawn, Vector3.forward, 0.08f);
        Handles.DrawLine(origin, spawn);
        Handles.Label(spawn + Vector3.up * 0.2f, "Spawn");
    }

    private static void DrawPath(AreaSkillConfig config, Vector3 spawn, Vector3 areaCenter, float facing)
    {
        Vector3 path = areaCenter - spawn;
        if (path.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Handles.color = PathColor;
        if (config.DeliveryMode == AreaSkillDeliveryMode.ArcProjectile)
        {
            DrawArcPath(config, spawn, facing);
            return;
        }

        Handles.DrawLine(spawn, areaCenter);
        Handles.ConeHandleCap(
            0,
            areaCenter,
            Quaternion.LookRotation(Vector3.forward, path.normalized),
            0.25f,
            EventType.Repaint);
    }

    private static void DrawArcPath(AreaSkillConfig config, Vector3 spawn, float facing)
    {
        float previewTime = Mathf.Min(Mathf.Max(0.05f, config.ProjectileLifetime), 1f);
        Vector2 velocity = new Vector2(Mathf.Abs(config.ArcLaunchVelocity.x) * facing, config.ArcLaunchVelocity.y);
        Vector2 gravity = Physics2D.gravity * Mathf.Max(0f, config.ArcGravityScale);
        Vector3 previous = spawn;

        for (int i = 1; i <= 16; i++)
        {
            float t = previewTime * i / 16f;
            Vector2 displacement = velocity * t + 0.5f * gravity * t * t;
            Vector3 next = spawn + (Vector3)displacement;
            Handles.DrawLine(previous, next);
            previous = next;
        }
    }

    private static void DrawArea(Vector3 center, float radius)
    {
        Handles.color = FillColor;
        Handles.DrawSolidDisc(center, Vector3.forward, radius);
        Handles.color = AreaColor;
        Handles.DrawWireDisc(center, Vector3.forward, radius);
        Handles.DrawLine(center + Vector3.left * radius, center + Vector3.right * radius);
        Handles.DrawLine(center + Vector3.down * radius, center + Vector3.up * radius);
    }

    private static void DrawApexSpreadPreview(AreaSkillConfig config, Vector3 origin, float facing)
    {
        Vector3 shotOrigin = origin + Vector3.up * Mathf.Max(0.5f, config.JumpImpulse * 0.35f);
        shotOrigin += new Vector3(config.ProjectileSpawnOffset.x * facing, config.ProjectileSpawnOffset.y, 0f);
        int projectileCount = Mathf.Max(1, config.ProjectileCount);
        float totalSpread = Mathf.Max(0f, config.SpreadAngle);
        float startAngleOffset = projectileCount > 1 ? -0.5f * totalSpread : 0f;
        float angleStep = projectileCount > 1 ? totalSpread / (projectileCount - 1) : 0f;
        float centerAngle = facing > 0f
            ? -config.CenterShotAngleDown
            : 180f + config.CenterShotAngleDown;
        float previewDistance = Mathf.Max(1f, config.SpreadShotSpeed * Mathf.Min(config.ProjectileLifetime, 1f));

        Handles.color = SpawnColor;
        Handles.DrawWireDisc(origin, Vector3.forward, 0.2f);
        Handles.DrawLine(origin, shotOrigin);
        Handles.DrawSolidDisc(shotOrigin, Vector3.forward, 0.1f);
        Handles.Label(shotOrigin + Vector3.up * 0.25f, "Apex Shot Origin");

        for (int i = 0; i < projectileCount; i++)
        {
            float angle = centerAngle + startAngleOffset + angleStep * i;
            Vector2 direction = DirectionFromAngle(angle);
            Vector3 laneSpawn = shotOrigin + (Vector3)(GetPerpendicular(direction) * GetVisualSpawnOffset(config, i, projectileCount));
            Vector3 previewEnd = laneSpawn + (Vector3)(direction * previewDistance);

            Handles.color = PathColor;
            Handles.DrawLine(laneSpawn, previewEnd);
            Handles.ConeHandleCap(
                0,
                previewEnd,
                Quaternion.LookRotation(Vector3.forward, direction),
                0.22f,
                EventType.Repaint);

            Handles.color = SpawnColor;
            Handles.DrawSolidDisc(laneSpawn, Vector3.forward, 0.06f);
            Handles.Label(laneSpawn + Vector3.up * 0.16f, $"P{i + 1}");
            DrawArea(previewEnd, Mathf.Max(0.01f, config.Radius));
        }

        Handles.Label(
            shotOrigin + Vector3.up * 0.7f,
            $"Area Skill Preview\nMode: {config.DeliveryMode}\nProjectiles: {projectileCount}\nSpread: {totalSpread:0.##}");
    }

    private static float GetVisualSpawnOffset(AreaSkillConfig config, int index, int count)
    {
        if (count <= 1)
        {
            return 0f;
        }

        return (index - (count - 1) * 0.5f) * Mathf.Max(0f, config.ProjectileSpacing);
    }

    private static Vector2 DirectionFromAngle(float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
    }

    private static Vector2 GetPerpendicular(Vector2 direction)
    {
        return new Vector2(-direction.y, direction.x).normalized;
    }
}
