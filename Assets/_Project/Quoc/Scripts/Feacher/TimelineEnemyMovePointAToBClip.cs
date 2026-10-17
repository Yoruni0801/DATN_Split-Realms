using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum TimelineEnemyMoveAxis
{
    XOnly,
    XY
}

public enum TimelineEnemyMoveTiming
{
    ClipDuration,
    MoveSpeed
}

public enum TimelineEnemyMoveFacingMode
{
    None,
    SpriteRendererFlipX,
    LocalScaleX
}

public enum TimelineEnemyMoveAnimationExitMode
{
    KeepCurrent,
    RestorePrevious
}

public class TimelineEnemyMovePointAToBClip : PlayableAsset, ITimelineClipAsset
{
    [Header("Points")]
    public ExposedReference<Transform> pointA;
    public ExposedReference<Transform> pointB;
    public bool snapToPointAOnEnter = true;

    [Header("Move")]
    public TimelineEnemyMoveAxis moveAxis = TimelineEnemyMoveAxis.XOnly;
    public TimelineEnemyMoveTiming moveTiming = TimelineEnemyMoveTiming.MoveSpeed;
    [Min(0f)] public float moveSpeed = 2f;

    [Header("End")]
    public bool hideWhenReached = true;
    public bool showOnEnter = true;

    [Header("Animation")]
    public AnimationClip moveAnimation;
    public string moveStateName;
    public int layerIndex;
    public bool loopAnimation = true;
    public TimelineEnemyMoveAnimationExitMode animationExitMode =
        TimelineEnemyMoveAnimationExitMode.KeepCurrent;

    [Header("Animator Parameter Optional")]
    public string speedParameterName;
    public float movingSpeedParameterValue = 1f;
    public float stoppedSpeedParameterValue = 0f;

    [Header("Facing")]
    public TimelineEnemyMoveFacingMode facingMode = TimelineEnemyMoveFacingMode.SpriteRendererFlipX;
    public bool defaultFaceRight = true;

    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.ClipIn | ClipCaps.SpeedMultiplier;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<TimelineEnemyMovePointAToBBehaviour> playable =
            ScriptPlayable<TimelineEnemyMovePointAToBBehaviour>.Create(graph);

        TimelineEnemyMovePointAToBBehaviour behaviour = playable.GetBehaviour();
        behaviour.pointA = pointA.Resolve(graph.GetResolver());
        behaviour.pointB = pointB.Resolve(graph.GetResolver());
        behaviour.snapToPointAOnEnter = snapToPointAOnEnter;
        behaviour.moveAxis = moveAxis;
        behaviour.moveTiming = moveTiming;
        behaviour.moveSpeed = Mathf.Max(0f, moveSpeed);
        behaviour.hideWhenReached = hideWhenReached;
        behaviour.showOnEnter = showOnEnter;
        behaviour.moveAnimation = moveAnimation;
        behaviour.moveStateName = moveStateName;
        behaviour.layerIndex = layerIndex;
        behaviour.loopAnimation = loopAnimation;
        behaviour.animationExitMode = animationExitMode;
        behaviour.speedParameterName = speedParameterName;
        behaviour.movingSpeedParameterValue = movingSpeedParameterValue;
        behaviour.stoppedSpeedParameterValue = stoppedSpeedParameterValue;
        behaviour.facingMode = facingMode;
        behaviour.defaultFaceRight = defaultFaceRight;

        return playable;
    }
}
