using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct PlayerWaterDetectionSettings
{
    public float waterCheckRadius;
    public float enterSwimmingDepth;
    public float exitSwimmingDepth;
    public float groundCheckRadius;
    public float ledgeProbeDistance;
    public float ledgeLowRayHeight;
    public float ledgeHighRayHeight;
    public float maxExitStepHeight;
    public float landingForwardOffset;
    public float landingRayHeight;
    public float landingRayDepth;
    public Vector2 landingClearanceSize;
    public float landingSkin;
    public bool drawDebug;

    public static PlayerWaterDetectionSettings Default => new PlayerWaterDetectionSettings
    {
        waterCheckRadius = 0.12f,
        enterSwimmingDepth = 0.15f,
        exitSwimmingDepth = 0.03f,
        groundCheckRadius = 0.2f,
        ledgeProbeDistance = 0.65f,
        ledgeLowRayHeight = 0.35f,
        ledgeHighRayHeight = 1.35f,
        maxExitStepHeight = 1.15f,
        landingForwardOffset = 0.45f,
        landingRayHeight = 1.2f,
        landingRayDepth = 1.8f,
        landingClearanceSize = new Vector2(1.2f, 2.4f),
        landingSkin = 0.03f,
        drawDebug = true
    };
}

public struct PlayerWaterSnapshot
{
    public bool InWaterZone;
    public bool HeadInWater;
    public bool BodyInWater;
    public bool FeetInWater;
    public bool BodyDeepEnoughToEnterSwimming;
    public bool BodyDeepEnoughToStaySwimming;
    public bool Grounded;
    public bool CanExitWater;
    public bool LedgeLowBlocked;
    public bool LedgeHighBlocked;
    public bool LandingGrounded;
    public bool LandingClear;
    public float WaterSurfaceY;
    public Vector2 HeadPoint;
    public Vector2 BodyPoint;
    public Vector2 FeetPoint;
    public Vector2 GroundPoint;
    public Vector2 LowRayOrigin;
    public Vector2 HighRayOrigin;
    public Vector2 LandingRayOrigin;
    public Vector2 LandingClearanceCenter;
    public Vector2 ExitFeetPosition;
    public Vector2 ExitTargetPosition;
}

public sealed class PlayerWaterDetector
{
    private readonly Transform _body;
    private readonly Collider2D _bodyCollider;
    private readonly Transform _headWaterCheck;
    private readonly Transform _bodyWaterCheck;
    private readonly Transform _feetWaterCheck;
    private readonly LayerMask _waterLayer;
    private readonly LayerMask _groundLayer;
    private readonly PlayerWaterDetectionSettings _settings;
    private readonly HashSet<Collider2D> _waterZones = new HashSet<Collider2D>();

    private readonly Collider2D[] _overlapBuffer = new Collider2D[8];

    public int WaterZoneCount => _waterZones.Count;

    public PlayerWaterDetector(
        Transform body,
        Collider2D bodyCollider,
        Transform headWaterCheck,
        Transform bodyWaterCheck,
        Transform feetWaterCheck,
        LayerMask waterLayer,
        LayerMask groundLayer,
        PlayerWaterDetectionSettings settings)
    {
        _body = body;
        _bodyCollider = bodyCollider;
        _headWaterCheck = headWaterCheck;
        _bodyWaterCheck = bodyWaterCheck;
        _feetWaterCheck = feetWaterCheck;
        _waterLayer = waterLayer;
        _groundLayer = groundLayer;
        _settings = settings;
    }

    public bool IsWaterCollider(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        int waterMask = _waterLayer.value;
        return waterMask != 0 && (waterMask & (1 << other.gameObject.layer)) != 0;
    }

    public void AddWaterZone(Collider2D waterCollider)
    {
        if (waterCollider != null)
        {
            _waterZones.Add(waterCollider);
        }
    }

    public void RemoveWaterZone(Collider2D waterCollider)
    {
        if (waterCollider != null)
        {
            _waterZones.Remove(waterCollider);
        }
    }

    public void ClearWaterZones()
    {
        _waterZones.Clear();
    }

    public PlayerWaterSnapshot Sample(float facingSign)
    {
        PurgeInvalidWaterZones();

        Bounds bounds = _bodyCollider != null
            ? _bodyCollider.bounds
            : new Bounds(_body.position, Vector3.one);

        PlayerWaterSnapshot snapshot = new PlayerWaterSnapshot
        {
            HeadPoint = ResolvePoint(_headWaterCheck, new Vector2(bounds.center.x, bounds.max.y)),
            BodyPoint = ResolvePoint(_bodyWaterCheck, bounds.center),
            FeetPoint = ResolvePoint(_feetWaterCheck, new Vector2(bounds.center.x, bounds.min.y)),
            GroundPoint = new Vector2(bounds.center.x, bounds.min.y)
        };

        float surfaceY = float.NegativeInfinity;
        bool hasWaterSurface = TryGetWaterAtPoint(snapshot.HeadPoint, ref surfaceY, out snapshot.HeadInWater);
        hasWaterSurface |= TryGetWaterAtPoint(snapshot.BodyPoint, ref surfaceY, out snapshot.BodyInWater);
        hasWaterSurface |= TryGetWaterAtPoint(snapshot.FeetPoint, ref surfaceY, out snapshot.FeetInWater);
        hasWaterSurface |= TryGetTrackedWaterAtPoint(snapshot.HeadPoint, ref surfaceY, ref snapshot.HeadInWater);
        hasWaterSurface |= TryGetTrackedWaterAtPoint(snapshot.BodyPoint, ref surfaceY, ref snapshot.BodyInWater);
        hasWaterSurface |= TryGetTrackedWaterAtPoint(snapshot.FeetPoint, ref surfaceY, ref snapshot.FeetInWater);
        hasWaterSurface |= TryGetWaterSurfaceFromZones(ref surfaceY);

        snapshot.InWaterZone = _waterZones.Count > 0;
        snapshot.WaterSurfaceY = hasWaterSurface ? surfaceY : 0f;
        snapshot.Grounded = GroundCheck.IsGrounded(
            _body,
            _bodyCollider,
            CreateGroundCheckConfig(bounds, snapshot.GroundPoint));

        if (hasWaterSurface)
        {
            float bodyDepth = surfaceY - snapshot.BodyPoint.y;
            snapshot.BodyDeepEnoughToEnterSwimming = snapshot.BodyInWater
                && bodyDepth >= _settings.enterSwimmingDepth;
            snapshot.BodyDeepEnoughToStaySwimming = snapshot.BodyInWater
                && bodyDepth >= _settings.exitSwimmingDepth;
        }

        CheckLedge(ref snapshot, bounds, facingSign);
        DrawDebug(snapshot, facingSign);
        return snapshot;
    }

    public static void DrawGizmos(
        Transform body,
        Collider2D bodyCollider,
        Transform headWaterCheck,
        Transform bodyWaterCheck,
        Transform feetWaterCheck,
        PlayerWaterDetectionSettings settings,
        PlayerWaterSnapshot snapshot,
        bool hasSnapshot)
    {
        if (body == null)
        {
            return;
        }

        Bounds bounds = bodyCollider != null
            ? bodyCollider.bounds
            : new Bounds(body.position, Vector3.one);

        Vector2 head = headWaterCheck != null ? (Vector2)headWaterCheck.position : new Vector2(bounds.center.x, bounds.max.y);
        Vector2 center = bodyWaterCheck != null ? (Vector2)bodyWaterCheck.position : bounds.center;
        Vector2 feet = feetWaterCheck != null ? (Vector2)feetWaterCheck.position : new Vector2(bounds.center.x, bounds.min.y);
        Vector2 ground = new Vector2(bounds.center.x, bounds.min.y);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(head, settings.waterCheckRadius);
        Gizmos.DrawWireSphere(center, settings.waterCheckRadius);
        Gizmos.DrawWireSphere(feet, settings.waterCheckRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(ground, settings.groundCheckRadius);

        if (!hasSnapshot)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(snapshot.LowRayOrigin, snapshot.LowRayOrigin + Vector2.right * settings.ledgeProbeDistance);
        Gizmos.DrawLine(snapshot.HighRayOrigin, snapshot.HighRayOrigin + Vector2.right * settings.ledgeProbeDistance);

        Gizmos.color = snapshot.LandingClear ? Color.green : Color.red;
        Gizmos.DrawWireCube(snapshot.LandingClearanceCenter, settings.landingClearanceSize);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(snapshot.ExitFeetPosition, 0.08f);
        Gizmos.DrawWireSphere(snapshot.ExitTargetPosition, 0.08f);
    }

    private Vector2 ResolvePoint(Transform point, Vector2 fallback)
    {
        return point != null ? (Vector2)point.position : fallback;
    }

    private GroundCheckConfig CreateGroundCheckConfig(Bounds bounds, Vector2 groundPoint)
    {
        return GroundCheck.Override(
            _groundLayer,
            _settings.groundCheckRadius,
            _settings.groundCheckRadius,
            groundPoint - new Vector2(bounds.center.x, bounds.min.y),
            GroundCheckMode.CircleCast);
    }

    private bool TryGetWaterAtPoint(Vector2 point, ref float surfaceY, out bool inWater)
    {
        inWater = false;
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(_waterLayer);
        filter.useTriggers = true;
        int count = Physics2D.OverlapCircle(
            point,
            Mathf.Max(0.01f, _settings.waterCheckRadius),
            filter,
            _overlapBuffer);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = _overlapBuffer[i];
            if (hit == null || !IsWaterCollider(hit))
            {
                continue;
            }

            inWater = true;
            surfaceY = Mathf.Max(surfaceY, hit.bounds.max.y);
        }

        return inWater;
    }

    private bool TryGetWaterSurfaceFromZones(ref float surfaceY)
    {
        bool found = false;
        foreach (Collider2D waterZone in _waterZones)
        {
            if (waterZone == null)
            {
                continue;
            }

            surfaceY = Mathf.Max(surfaceY, waterZone.bounds.max.y);
            found = true;
        }

        return found;
    }

    private bool TryGetTrackedWaterAtPoint(Vector2 point, ref float surfaceY, ref bool inWater)
    {
        bool found = false;
        foreach (Collider2D waterZone in _waterZones)
        {
            if (waterZone == null)
            {
                continue;
            }

            if (!waterZone.bounds.Contains(point))
            {
                continue;
            }

            inWater = true;
            surfaceY = Mathf.Max(surfaceY, waterZone.bounds.max.y);
            found = true;
        }

        return found;
    }

    private void CheckLedge(ref PlayerWaterSnapshot snapshot, Bounds bounds, float facingSign)
    {
        float directionSign = Mathf.Sign(facingSign);
        if (Mathf.Approximately(directionSign, 0f))
        {
            directionSign = 1f;
        }

        Vector2 direction = new Vector2(directionSign, 0f);
        snapshot.LowRayOrigin = new Vector2(bounds.center.x, bounds.min.y + _settings.ledgeLowRayHeight);
        snapshot.HighRayOrigin = new Vector2(bounds.center.x, bounds.min.y + _settings.ledgeHighRayHeight);

        RaycastHit2D lowHit = Physics2D.Raycast(
            snapshot.LowRayOrigin,
            direction,
            _settings.ledgeProbeDistance,
            _groundLayer);
        RaycastHit2D highHit = Physics2D.Raycast(
            snapshot.HighRayOrigin,
            direction,
            _settings.ledgeProbeDistance,
            _groundLayer);

        snapshot.LedgeLowBlocked = lowHit.collider != null;
        snapshot.LedgeHighBlocked = highHit.collider != null;
        if (!snapshot.LedgeLowBlocked || snapshot.LedgeHighBlocked)
        {
            return;
        }

        float landingX = lowHit.point.x + directionSign * _settings.landingForwardOffset;
        snapshot.LandingRayOrigin = new Vector2(
            landingX,
            bounds.min.y + _settings.maxExitStepHeight + _settings.landingRayHeight);

        RaycastHit2D groundHit = Physics2D.Raycast(
            snapshot.LandingRayOrigin,
            Vector2.down,
            _settings.landingRayDepth,
            _groundLayer);

        snapshot.LandingGrounded = groundHit.collider != null;
        if (!snapshot.LandingGrounded)
        {
            return;
        }

        float stepHeight = groundHit.point.y - bounds.min.y;
        if (stepHeight < -0.05f || stepHeight > _settings.maxExitStepHeight)
        {
            return;
        }

        Vector2 clearanceSize = _settings.landingClearanceSize;
        if (clearanceSize.x <= 0f || clearanceSize.y <= 0f)
        {
            clearanceSize = bounds.size;
        }

        snapshot.ExitFeetPosition = new Vector2(landingX, groundHit.point.y + _settings.landingSkin);
        Vector2 feetDelta = snapshot.ExitFeetPosition - new Vector2(bounds.center.x, bounds.min.y);
        snapshot.ExitTargetPosition = (Vector2)_body.position + feetDelta;
        snapshot.LandingClearanceCenter = new Vector2(
            landingX,
            groundHit.point.y + _settings.landingSkin + clearanceSize.y * 0.5f);

        snapshot.LandingClear = Physics2D.OverlapBox(
            snapshot.LandingClearanceCenter,
            clearanceSize,
            0f,
            _groundLayer) == null;
        snapshot.CanExitWater = snapshot.LandingClear;
    }

    private void DrawDebug(PlayerWaterSnapshot snapshot, float facingSign)
    {
        if (!_settings.drawDebug)
        {
            return;
        }

        float directionSign = Mathf.Sign(facingSign);
        if (Mathf.Approximately(directionSign, 0f))
        {
            directionSign = 1f;
        }

        Vector3 direction = new Vector3(directionSign, 0f, 0f);
        Debug.DrawRay(snapshot.LowRayOrigin, direction * _settings.ledgeProbeDistance,
            snapshot.LedgeLowBlocked ? Color.green : Color.red);
        Debug.DrawRay(snapshot.HighRayOrigin, direction * _settings.ledgeProbeDistance,
            snapshot.LedgeHighBlocked ? Color.red : Color.green);
        Debug.DrawRay(snapshot.LandingRayOrigin, Vector3.down * _settings.landingRayDepth,
            snapshot.LandingGrounded ? Color.green : Color.red);
        Debug.DrawLine(snapshot.HeadPoint, snapshot.BodyPoint, Color.cyan);
        Debug.DrawLine(snapshot.BodyPoint, snapshot.FeetPoint, Color.cyan);
        Debug.DrawLine(snapshot.FeetPoint, snapshot.GroundPoint, Color.green);
    }

    private void PurgeInvalidWaterZones()
    {
        _waterZones.RemoveWhere(collider => collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy);
    }
}
