using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public sealed class TimelineCameraPointBReturnClip : PlayableAsset, ITimelineClipAsset
{
    [Header("Point B")]
    public ExposedReference<Transform> pointB;

    [Header("Timing")]
    [Min(0f)] public float moveToBDuration = 1f;
    [Min(0f)] public float zoomDuration = 0.5f;
    [Min(0f)] public float holdDuration = 1f;
    [Min(0f)] public float returnDuration = 1f;

    [Header("Zoom At Point B")]
    [Min(0.01f)] public float targetOrthographicSize = 5f;
    [Range(1f, 179f)] public float targetFieldOfView = 45f;

    [Header("Curves")]
    public AnimationCurve moveCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public AnimationCurve zoomCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public AnimationCurve returnCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public ClipCaps clipCaps => ClipCaps.ClipIn | ClipCaps.SpeedMultiplier;

    public override double duration => Mathf.Max(
        0.01f,
        moveToBDuration + zoomDuration + holdDuration + returnDuration);

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<TimelineCameraPointBReturnBehaviour> playable =
            ScriptPlayable<TimelineCameraPointBReturnBehaviour>.Create(graph);
        TimelineCameraPointBReturnBehaviour behaviour = playable.GetBehaviour();

        behaviour.pointB = pointB.Resolve(graph.GetResolver());
        behaviour.moveToBDuration = Mathf.Max(0f, moveToBDuration);
        behaviour.zoomDuration = Mathf.Max(0f, zoomDuration);
        behaviour.holdDuration = Mathf.Max(0f, holdDuration);
        behaviour.returnDuration = Mathf.Max(0f, returnDuration);
        behaviour.targetOrthographicSize = Mathf.Max(0.01f, targetOrthographicSize);
        behaviour.targetFieldOfView = Mathf.Clamp(targetFieldOfView, 1f, 179f);
        behaviour.moveCurve = moveCurve;
        behaviour.zoomCurve = zoomCurve;
        behaviour.returnCurve = returnCurve;

        return playable;
    }
}
