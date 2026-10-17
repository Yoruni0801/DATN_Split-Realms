using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Serialization;

public enum TimelineSpineAnimationExitMode
{
    KeepCurrent,
    RestorePrevious,
    ClearTrack,
    StopAll
}

public class TimelineSpineAnimationClip : PlayableAsset, ITimelineClipAsset
{
    public AnimationSpine.State state = AnimationSpine.State.Idle;
    public TimelineSpineAnimationExitMode exitMode = TimelineSpineAnimationExitMode.KeepCurrent;
    public TimelineSpineFacingOverride facingOverride;

    public int trackIndex;
    public float mixDuration = 0.1f;
    public float timeScale = 1f;

    public bool resetToSetupPoseBeforePlay;
    public bool restoreTimeScale = true;
    public bool loopAnimation = true;
    public bool reverseAnimation;

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<TimelineSpineAnimationBehaviour> playable =
            ScriptPlayable<TimelineSpineAnimationBehaviour>.Create(graph);

        TimelineSpineAnimationBehaviour behaviour = playable.GetBehaviour();
        behaviour.state = state;
        behaviour.loopAnimation = loopAnimation;
        behaviour.trackIndex = Mathf.Max(0, trackIndex);
        behaviour.mixDuration = Mathf.Max(0f, mixDuration);
        behaviour.timeScale = Mathf.Max(0f, timeScale);
        behaviour.resetToSetupPoseBeforePlay = resetToSetupPoseBeforePlay;
        behaviour.exitMode = exitMode;
        behaviour.restoreTimeScale = restoreTimeScale;
        behaviour.reverseAnimation = reverseAnimation;
        behaviour.facingOverride = facingOverride;

        return playable;
    }
}
