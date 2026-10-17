using Spine.Unity;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum TimelineSkeletonGraphicExitMode
{
    KeepCurrent,
    ClearTrack,
    ResetToSetupPose
}

[System.Serializable]
public class TimelineSkeletonGraphicClip : PlayableAsset, ITimelineClipAsset
{
    public SkeletonDataAsset skeletonDataAsset;

    [SpineAnimation(dataField: "skeletonDataAsset")]
    public string animationName;
    public bool loop = true;
    public int trackIndex = 0;
    public float timeScale = 1f;
    public bool resetToSetupPose = false;
    public TimelineSkeletonGraphicExitMode exitMode = TimelineSkeletonGraphicExitMode.KeepCurrent;
    public bool restoreTimeScale = true;

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<TimelineSkeletonGraphicBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.animationName = animationName;
        behaviour.loop = loop;
        behaviour.trackIndex = Mathf.Max(0, trackIndex);
        behaviour.timeScale = Mathf.Max(0f, timeScale);
        behaviour.resetToSetupPose = resetToSetupPose;
        behaviour.exitMode = exitMode;
        behaviour.restoreTimeScale = restoreTimeScale;
        return playable;
    }
}
