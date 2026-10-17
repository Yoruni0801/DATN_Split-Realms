using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum TimelinePlayerSpineMoveAxis
{
    XOnly,
    XY
}

public enum TimelinePlayerSpineFacingOverride
{
    None,
    FaceLeft,
    FaceRight
}

public class TimelinePlayerSpineMovePointAToBClip : PlayableAsset, ITimelineClipAsset
{
    [Header("Points")]
    public ExposedReference<Transform> pointA;
    public ExposedReference<Transform> pointB;
    public bool snapToPointAOnEnter = true;

    [Header("Move")]
    public TimelinePlayerSpineMoveAxis moveAxis = TimelinePlayerSpineMoveAxis.XOnly;
    [Min(0f)] public float moveSpeed = 3f;
    [Min(0f)] public float stopDistance = 0.05f;

    [Header("Spine Animation")]
    public bool controlAnimation = true;
    public AnimationSpine.State moveState = AnimationSpine.State.Run;
    public AnimationSpine.State idleState = AnimationSpine.State.Idle;
    public string moveAnimationName;
    public bool loopMoveAnimation = true;
    public bool returnToIdleWhenStopped = true;
    public int animationTrackIndex;

    [Header("Player")]
    public ExposedReference<Player> player;
    public bool lockPlayerControl = true;
    public bool unlockPlayerControlOnExit = true;

    [Header("Facing")]
    public bool faceMoveDirection = true;
    public bool invertFacing = true;
    public TimelinePlayerSpineFacingOverride facingOverride;

    [Header("End")]
    public bool hideWhenReached;
    public bool showOnEnter = true;

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<TimelinePlayerSpineMovePointAToBBehaviour> playable =
            ScriptPlayable<TimelinePlayerSpineMovePointAToBBehaviour>.Create(graph);

        TimelinePlayerSpineMovePointAToBBehaviour behaviour = playable.GetBehaviour();
        behaviour.pointA = pointA.Resolve(graph.GetResolver());
        behaviour.pointB = pointB.Resolve(graph.GetResolver());
        behaviour.snapToPointAOnEnter = snapToPointAOnEnter;
        behaviour.moveAxis = moveAxis;
        behaviour.moveSpeed = Mathf.Max(0f, moveSpeed);
        behaviour.stopDistance = Mathf.Max(0f, stopDistance);
        behaviour.controlAnimation = controlAnimation;
        behaviour.moveState = moveState;
        behaviour.idleState = idleState;
        behaviour.moveAnimationName = moveAnimationName;
        behaviour.loopMoveAnimation = loopMoveAnimation;
        behaviour.returnToIdleWhenStopped = returnToIdleWhenStopped;
        behaviour.animationTrackIndex = Mathf.Max(0, animationTrackIndex);
        behaviour.player = player.Resolve(graph.GetResolver());
        behaviour.lockPlayerControl = lockPlayerControl;
        behaviour.unlockPlayerControlOnExit = unlockPlayerControlOnExit;
        behaviour.faceMoveDirection = faceMoveDirection;
        behaviour.invertFacing = invertFacing;
        behaviour.facingOverride = facingOverride;
        behaviour.hideWhenReached = hideWhenReached;
        behaviour.showOnEnter = showOnEnter;

        return playable;
    }
}
