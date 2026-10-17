using UnityEngine;

public sealed class SkillCastContext
{
    public SkillCastContext(
        Player owner,
        Transform body,
        Transform castPoint,
        LayerMask targetLayer,
        ICharacterScaleProvider scaleProvider = null,
        float holdDuration = 0f)
    {
        Owner = owner;
        Body = body;
        CastPoint = castPoint;
        TargetLayer = targetLayer;
        ScaleProvider = scaleProvider;
        HoldDuration = Mathf.Max(0f, holdDuration);
    }

    public Player Owner { get; }
    public Transform Body { get; }
    public Transform CastPoint { get; }
    public LayerMask TargetLayer { get; }
    public ICharacterScaleProvider ScaleProvider { get; }
    public float HoldDuration { get; }
    public float FacingDirection => Owner != null ? Owner.FacingDirectionSign : 1f;
    public Vector3 Origin => CastPoint != null ? CastPoint.position : Body.position;
    public float CurrentScaleMultiplier => ScaleProvider?.CurrentScaleMultiplier ?? 1f;

    public Vector3 ScaleOffset(Vector3 baseOffset)
    {
        return ScaleProvider != null ? ScaleProvider.ScaleOffset(baseOffset) : baseOffset;
    }

    public float ScaleDistance(float baseDistance)
    {
        return ScaleProvider != null ? ScaleProvider.ScaleDistance(baseDistance) : baseDistance;
    }

    public Vector3 GetManualSpawnPosition(Vector3 baseOffset, int facingDirection)
    {
        Vector3 offset = ScaleOffset(baseOffset);
        offset.x *= facingDirection >= 0 ? 1f : -1f;
        return Origin + offset;
    }

    public SkillCastContext WithHoldDuration(float holdDuration)
    {
        return new SkillCastContext(Owner, Body, CastPoint, TargetLayer, ScaleProvider, holdDuration);
    }
}
