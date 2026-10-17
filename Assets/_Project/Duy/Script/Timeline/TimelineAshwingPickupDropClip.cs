using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public sealed class TimelineAshwingPickupDropClip : PlayableAsset, ITimelineClipAsset
{
    public ExposedReference<Transform> ashwingOverride;
    public ExposedReference<Transform> itemTarget;
    public ExposedReference<Transform> carryPoint;
    public ExposedReference<Transform> flyAwayTarget;

    [Min(0f)] public float approachSpeed = 14f;
    [Min(0f)] public float escapeSpeed = 18f;
    [Min(0.1f)] public float flightSpeedMultiplier = 1.5f;
    [Min(0f)] public float flightAcceleration = 45f;
    [Min(0f)] public float arriveSlowdownDistance = 0.6f;
    public bool invertFacing = true;
    [Min(0f)] public float pickupDistance = 0.35f;
    [Range(0f, 1f)] public float forcePickupNormalizedTime = 0.45f;
    public bool requireReachItemBeforePickup = true;
    public Vector3 carryLocalOffset = new Vector3(0f, -0.25f, 0f);
    public bool flyToExactItemPosition = true;
    public Vector3 targetOffset = Vector3.zero;
    public Vector3 flyAwayTargetOffset;
    public Vector2 escapeDirection = new Vector2(1f, 1f);
    [Min(0f)] public float escapeDistance = 8f;
    public bool disableAshwingAtEnd;
    public bool releaseTimelineControlOnEnd = true;

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<TimelineAshwingPickupDropBehaviour> playable =
            ScriptPlayable<TimelineAshwingPickupDropBehaviour>.Create(graph);

        TimelineAshwingPickupDropBehaviour behaviour = playable.GetBehaviour();
        behaviour.ashwingOverride = ashwingOverride.Resolve(graph.GetResolver());
        behaviour.itemTarget = itemTarget.Resolve(graph.GetResolver());
        behaviour.carryPoint = carryPoint.Resolve(graph.GetResolver());
        behaviour.flyAwayTarget = flyAwayTarget.Resolve(graph.GetResolver());
        behaviour.approachSpeed = Mathf.Max(0f, approachSpeed);
        behaviour.escapeSpeed = Mathf.Max(0f, escapeSpeed);
        behaviour.flightSpeedMultiplier = Mathf.Max(0.1f, flightSpeedMultiplier);
        behaviour.flightAcceleration = Mathf.Max(0f, flightAcceleration);
        behaviour.arriveSlowdownDistance = Mathf.Max(0f, arriveSlowdownDistance);
        behaviour.invertFacing = invertFacing;
        behaviour.pickupDistance = Mathf.Max(0f, pickupDistance);
        behaviour.forcePickupNormalizedTime = Mathf.Clamp01(forcePickupNormalizedTime);
        behaviour.requireReachItemBeforePickup = requireReachItemBeforePickup;
        behaviour.carryLocalOffset = carryLocalOffset;
        behaviour.flyToExactItemPosition = flyToExactItemPosition;
        behaviour.targetOffset = targetOffset;
        behaviour.flyAwayTargetOffset = flyAwayTargetOffset;
        behaviour.escapeDirection = escapeDirection;
        behaviour.escapeDistance = Mathf.Max(0f, escapeDistance);
        behaviour.disableAshwingAtEnd = disableAshwingAtEnd;
        behaviour.releaseTimelineControlOnEnd = releaseTimelineControlOnEnd;
        return playable;
    }
}
