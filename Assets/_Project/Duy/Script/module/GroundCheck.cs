using System;
using UnityEngine;

public enum GroundCheckMode
{
    Raycast,
    CircleCast,
    ColliderCast
}

[Serializable]
public struct GroundCheckConfig
{
    public Vector2 groundCheckOffset;
    public float checkRadius;
    public float rayLength;
    public LayerMask groundLayer;
    public GroundCheckMode checkMode;
    public bool ignoreTriggers;

    public static GroundCheckConfig Default => GroundCheck.DefaultConfig;
}

public readonly struct GroundCheckResult
{
    public readonly bool IsGrounded;
    public readonly RaycastHit2D Hit;
    public readonly Vector2 Origin;
    public readonly GroundCheckConfig Config;

    public GroundCheckResult(
        bool isGrounded,
        RaycastHit2D hit,
        Vector2 origin,
        GroundCheckConfig config)
    {
        IsGrounded = isGrounded;
        Hit = hit;
        Origin = origin;
        Config = config;
    }

    public static implicit operator bool(GroundCheckResult result)
    {
        return result.IsGrounded;
    }
}

public static class GroundCheck
{
    private const float MinRadius = 0.01f;
    private const float MinRayLength = 0.01f;
    private static readonly RaycastHit2D[] ColliderCastHits = new RaycastHit2D[16];

    public static GroundCheckConfig DefaultConfig = new GroundCheckConfig
    {
        groundCheckOffset = Vector2.zero,
        checkRadius = 0.12f,
        rayLength = 0.12f,
        groundLayer = 0,
        checkMode = GroundCheckMode.CircleCast,
        ignoreTriggers = true
    };

    public static bool IsGrounded(
        Transform actor,
        Collider2D actorCollider = null,
        GroundCheckConfig? overrideConfig = null)
    {
        return Check(actor, actorCollider, overrideConfig).IsGrounded;
    }

    public static bool IsGrounded(
        GameObject actor,
        GroundCheckConfig? overrideConfig = null)
    {
        if (actor == null)
        {
            return false;
        }

        return Check(actor.transform, actor.GetComponent<Collider2D>(), overrideConfig).IsGrounded;
    }

    public static GroundCheckResult Check(
        Transform actor,
        Collider2D actorCollider = null,
        GroundCheckConfig? overrideConfig = null)
    {
        return Check(actor, actorCollider, ResolveGroundDirection(actor), overrideConfig);
    }

    public static GroundCheckResult Check(
        Transform actor,
        Collider2D actorCollider,
        Vector2 groundDirection,
        GroundCheckConfig? overrideConfig = null)
    {
        GroundCheckConfig config = ResolveConfig(overrideConfig);
        if (actor == null)
        {
            return new GroundCheckResult(false, default, Vector2.zero, config);
        }

        groundDirection = NormalizeDirection(groundDirection);
        Vector2 origin = ResolveOrigin(actor, actorCollider, config.groundCheckOffset, groundDirection);
        RaycastHit2D hit = CastGround(origin, groundDirection, actorCollider, config);

        return new GroundCheckResult(hit.collider != null, hit, origin, config);
    }

    public static GroundCheckResult CheckSurface(
        Transform actor,
        Collider2D actorCollider,
        Vector2 castDirection,
        Vector2 expectedSurfaceNormal,
        float minNormalDot,
        GroundCheckConfig? overrideConfig = null)
    {
        GroundCheckConfig config = ResolveConfig(overrideConfig);
        if (actor == null)
        {
            return new GroundCheckResult(false, default, Vector2.zero, config);
        }

        castDirection = NormalizeDirection(castDirection);
        expectedSurfaceNormal = NormalizeDirection(expectedSurfaceNormal);
        Vector2 origin = ResolveOrigin(
            actor,
            actorCollider,
            config.groundCheckOffset,
            castDirection);
        RaycastHit2D hit = CastGround(
            origin,
            castDirection,
            actorCollider,
            config,
            expectedSurfaceNormal,
            Mathf.Clamp01(minNormalDot));

        return new GroundCheckResult(hit.collider != null, hit, origin, config);
    }

    public static GroundCheckResult CheckAt(
        Vector2 origin,
        Collider2D actorCollider = null,
        GroundCheckConfig? overrideConfig = null)
    {
        GroundCheckConfig config = ResolveConfig(overrideConfig);
        RaycastHit2D hit = CastGround(origin, Vector2.down, actorCollider, config);

        return new GroundCheckResult(hit.collider != null, hit, origin, config);
    }

    public static GroundCheckConfig Override(
        LayerMask groundLayer,
        float checkRadius = -1f,
        float rayLength = -1f,
        Vector2? groundCheckOffset = null,
        GroundCheckMode? checkMode = null)
    {
        GroundCheckConfig config = ResolveConfig(null);
        if (groundLayer.value != 0)
        {
            config.groundLayer = groundLayer;
        }

        if (checkRadius >= 0f)
        {
            config.checkRadius = checkRadius;
        }

        if (rayLength >= 0f)
        {
            config.rayLength = rayLength;
        }

        if (groundCheckOffset.HasValue)
        {
            config.groundCheckOffset = groundCheckOffset.Value;
        }

        if (checkMode.HasValue)
        {
            config.checkMode = checkMode.Value;
        }

        return config;
    }

    public static Vector2 GetCheckOrigin(
        Transform actor,
        Collider2D actorCollider = null,
        GroundCheckConfig? overrideConfig = null)
    {
        GroundCheckConfig config = ResolveConfig(overrideConfig);
        return actor == null
            ? Vector2.zero
            : ResolveOrigin(actor, actorCollider, config.groundCheckOffset, ResolveGroundDirection(actor));
    }

    public static void DrawGizmos(
        Transform actor,
        Collider2D actorCollider = null,
        GroundCheckConfig? overrideConfig = null)
    {
        DrawGizmos(actor, actorCollider, ResolveGroundDirection(actor), overrideConfig);
    }

    public static void DrawGizmos(
        Transform actor,
        Collider2D actorCollider,
        Vector2 groundDirection,
        GroundCheckConfig? overrideConfig = null)
    {
        if (actor == null)
        {
            return;
        }

        GroundCheckConfig config = ResolveConfig(overrideConfig);
        groundDirection = NormalizeDirection(groundDirection);
        Vector2 origin = ResolveOrigin(actor, actorCollider, config.groundCheckOffset, groundDirection);

        Gizmos.color = Color.green;
        if (config.checkMode == GroundCheckMode.Raycast
            || config.checkMode == GroundCheckMode.ColliderCast)
        {
            Gizmos.DrawLine(origin, origin + groundDirection * Mathf.Max(MinRayLength, config.rayLength));
            return;
        }

        Gizmos.DrawWireSphere(origin, Mathf.Max(MinRadius, config.checkRadius));
        Gizmos.DrawLine(origin, origin + groundDirection * Mathf.Max(MinRayLength, config.rayLength));
        Gizmos.DrawWireSphere(
            origin + groundDirection * Mathf.Max(MinRayLength, config.rayLength),
            Mathf.Max(MinRadius, config.checkRadius));
    }

    private static GroundCheckConfig ResolveConfig(GroundCheckConfig? overrideConfig)
    {
        GroundCheckConfig config = overrideConfig ?? DefaultConfig;
        if (config.groundLayer.value == 0)
        {
            config.groundLayer = ResolveDefaultGroundLayer();
        }

        config.checkRadius = Mathf.Max(MinRadius, config.checkRadius);
        config.rayLength = Mathf.Max(MinRayLength, config.rayLength);
        return config;
    }

    private static LayerMask ResolveDefaultGroundLayer()
    {
        int mask = LayerMask.GetMask("Ground", "Platform", "Default");
        return mask != 0 ? mask : Physics2D.DefaultRaycastLayers;
    }

    private static Vector2 ResolveOrigin(
        Transform actor,
        Collider2D actorCollider,
        Vector2 offset,
        Vector2 groundDirection)
    {
        Vector2 rotatedOffset = actor != null ? (Vector2)actor.TransformVector(offset) : offset;
        if (actorCollider != null)
        {
            Bounds bounds = actorCollider.bounds;
            Vector2 extents = bounds.extents;
            float distanceToEdge =
                Mathf.Abs(groundDirection.x) * extents.x
                + Mathf.Abs(groundDirection.y) * extents.y;

            return (Vector2)bounds.center + (groundDirection * distanceToEdge) + rotatedOffset;
        }

        return (Vector2)actor.position + rotatedOffset;
    }

    private static Vector2 ResolveGroundDirection(Transform actor)
    {
        if (actor == null)
        {
            return Vector2.down;
        }

        Rigidbody2D actorRigidbody = actor.GetComponent<Rigidbody2D>();
        if (actorRigidbody != null && actorRigidbody.gravityScale < 0f)
        {
            return Vector2.up;
        }

        Vector2 direction = -actor.up;
        return NormalizeDirection(direction);
    }

    private static Vector2 NormalizeDirection(Vector2 direction)
    {
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
    }

    private static RaycastHit2D CastGround(
        Vector2 origin,
        Vector2 direction,
        Collider2D actorCollider,
        GroundCheckConfig config,
        Vector2 expectedSurfaceNormal = default,
        float minNormalDot = -1f)
    {
        switch (config.checkMode)
        {
            case GroundCheckMode.Raycast:
                return RaycastGround(
                    origin,
                    direction,
                    actorCollider,
                    config,
                    expectedSurfaceNormal,
                    minNormalDot);

            case GroundCheckMode.ColliderCast:
                return ColliderCastGround(
                    origin,
                    direction,
                    actorCollider,
                    config,
                    expectedSurfaceNormal,
                    minNormalDot);

            default:
                return CircleCastGround(
                    origin,
                    direction,
                    actorCollider,
                    config,
                    expectedSurfaceNormal,
                    minNormalDot);
        }
    }

    private static RaycastHit2D RaycastGround(
        Vector2 origin,
        Vector2 direction,
        Collider2D actorCollider,
        GroundCheckConfig config,
        Vector2 expectedSurfaceNormal = default,
        float minNormalDot = -1f)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(
            origin,
            direction,
            config.rayLength,
            config.groundLayer);
        return FindValidHit(
            hits,
            actorCollider,
            config.ignoreTriggers,
            expectedSurfaceNormal: expectedSurfaceNormal,
            minNormalDot: minNormalDot);
    }

    private static RaycastHit2D CircleCastGround(
        Vector2 origin,
        Vector2 direction,
        Collider2D actorCollider,
        GroundCheckConfig config,
        Vector2 expectedSurfaceNormal = default,
        float minNormalDot = -1f)
    {
        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            origin,
            config.checkRadius,
            direction,
            config.rayLength,
            config.groundLayer);
        return FindValidHit(
            hits,
            actorCollider,
            config.ignoreTriggers,
            expectedSurfaceNormal: expectedSurfaceNormal,
            minNormalDot: minNormalDot);
    }

    private static RaycastHit2D ColliderCastGround(
        Vector2 origin,
        Vector2 direction,
        Collider2D actorCollider,
        GroundCheckConfig config,
        Vector2 expectedSurfaceNormal = default,
        float minNormalDot = -1f)
    {
        if (actorCollider == null)
        {
            return RaycastGround(
                origin,
                direction,
                null,
                config,
                expectedSurfaceNormal,
                minNormalDot);
        }

        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = !config.ignoreTriggers
        };
        filter.SetLayerMask(config.groundLayer);

        int hitCount = actorCollider.Cast(
            direction,
            filter,
            ColliderCastHits,
            config.rayLength);
        return FindValidHit(
            ColliderCastHits,
            actorCollider,
            config.ignoreTriggers,
            hitCount,
            expectedSurfaceNormal,
            minNormalDot);
    }

    private static RaycastHit2D FindValidHit(
        RaycastHit2D[] hits,
        Collider2D actorCollider,
        bool ignoreTriggers,
        int hitCount = -1,
        Vector2 expectedSurfaceNormal = default,
        float minNormalDot = -1f)
    {
        int count = hitCount >= 0 ? Mathf.Min(hitCount, hits.Length) : hits.Length;
        bool filterByNormal = expectedSurfaceNormal.sqrMagnitude > 0.0001f;
        if (filterByNormal)
        {
            expectedSurfaceNormal.Normalize();
        }

        RaycastHit2D nearestHit = default;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null)
            {
                continue;
            }

            if (ignoreTriggers && hitCollider.isTrigger)
            {
                continue;
            }

            if (actorCollider != null && hitCollider.transform.root == actorCollider.transform.root)
            {
                continue;
            }

            if (filterByNormal
                && Vector2.Dot(hits[i].normal, expectedSurfaceNormal) < minNormalDot)
            {
                continue;
            }

            if (hits[i].distance < nearestDistance)
            {
                nearestHit = hits[i];
                nearestDistance = hits[i].distance;
            }
        }

        return nearestHit;
    }
}
