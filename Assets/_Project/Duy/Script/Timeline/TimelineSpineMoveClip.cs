using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum TimelineSpineMoveAxis
{
    Horizontal,
    XY
}

public enum TimelineSpineMoveTiming
{
    MoveSpeed,
    MoveDuration
}

public enum TimelineSpineFacingOverride
{
    None,
    FaceLeft,
    FaceRight
}

public enum TimelineSpineMoveBindingMode
{
    TrackBinding,
    CurrentPlayerWhenBindingMissing,
    CurrentPlayer
}

public class TimelineSpineMoveClip : PlayableAsset, ITimelineClipAsset
{
    public bool stopWhenReached = true;
    public bool keepStoppedOnExit;
    public bool faceMoveDirection = true;
    public bool invertFacing;
    public bool controlAnimation = true;
    public bool returnToIdleWhenStopped = true;
    public bool lockPlayerControl = true;
    public bool unlockPlayerControlOnExit = true;
    public bool applyGravity = true;
    public bool applyInEditMode;
    public TimelineSpineMoveBindingMode bindingMode = TimelineSpineMoveBindingMode.CurrentPlayerWhenBindingMissing;

    public ExposedReference<Transform> target;

    [Tooltip("Optional authored start of the move. Assign it to make editor scrubbing deterministic; leave it empty to start from the character's current position.")]
    public ExposedReference<Transform> startTarget;
    public Vector2 startOffset;

    public TimelineSpineMoveAxis moveAxis = TimelineSpineMoveAxis.Horizontal;
    public TimelineSpineMoveTiming moveTiming = TimelineSpineMoveTiming.MoveSpeed;
    public TimelineSpineFacingOverride facingOverride;
    public AnimationSpine.State moveState = AnimationSpine.State.Run;
    public AnimationSpine.State idleState = AnimationSpine.State.Idle;

    [Min(0f)] public float moveSpeed = 3f;
    [Min(0.01f)] public float moveDuration = 1f;
    public float stopDistance = 0.05f;
    public int animationTrackIndex;
    public Vector2 targetOffset;
    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<TimelineSpineMoveBehaviour> playable =
            ScriptPlayable<TimelineSpineMoveBehaviour>.Create(graph);

        TimelineSpineMoveBehaviour behaviour = playable.GetBehaviour();
        behaviour.target = target.Resolve(graph.GetResolver());
        behaviour.targetOffset = targetOffset;
        behaviour.startTarget = startTarget.Resolve(graph.GetResolver());
        behaviour.startOffset = startOffset;
        behaviour.stopDistance = Mathf.Max(0f, stopDistance);
        behaviour.moveAxis = moveAxis;
        behaviour.moveTiming = moveTiming;
        behaviour.moveSpeed = Mathf.Max(0f, moveSpeed);
        behaviour.moveDuration = Mathf.Max(0.01f, moveDuration);
        behaviour.stopWhenReached = stopWhenReached;
        behaviour.keepStoppedOnExit = keepStoppedOnExit;
        behaviour.faceMoveDirection = faceMoveDirection;
        behaviour.invertFacing = invertFacing;
        behaviour.facingOverride = facingOverride;
        behaviour.controlAnimation = controlAnimation;
        behaviour.moveState = moveState;
        behaviour.idleState = idleState;
        behaviour.returnToIdleWhenStopped = returnToIdleWhenStopped;
        behaviour.animationTrackIndex = Mathf.Max(0, animationTrackIndex);
        behaviour.lockPlayerControl = lockPlayerControl;
        behaviour.unlockPlayerControlOnExit = unlockPlayerControlOnExit;
        behaviour.applyGravity = applyGravity;
        behaviour.applyInEditMode = applyInEditMode;
        behaviour.bindingMode = bindingMode;

        return playable;
    }
}
