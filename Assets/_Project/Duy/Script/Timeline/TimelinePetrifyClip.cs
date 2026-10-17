using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum TimelinePetrifyTargetMode
{
    BoundTarget,
    AllTargets
}

public enum TimelinePetrifyExitMode
{
    RestorePrevious,
    KeepEndAmount,
    ClearPetrify
}

public class TimelinePetrifyClip : PlayableAsset, ITimelineClipAsset
{
    public ExposedReference<PetrifyController> targetOverride;

    public bool includeInactiveTargets;
    public bool applyInEditMode;
    public bool animateOverClip = true;

    private float startAmount = 0f;
    private float endAmount = 1f;

    public TimelinePetrifyExitMode exitMode = TimelinePetrifyExitMode.KeepEndAmount;
    public TimelinePetrifyTargetMode targetMode = TimelinePetrifyTargetMode.BoundTarget;


    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<TimelinePetrifyBehaviour> playable =
            ScriptPlayable<TimelinePetrifyBehaviour>.Create(graph);

        TimelinePetrifyBehaviour behaviour = playable.GetBehaviour();
        behaviour.targetMode = targetMode;
        behaviour.targetOverride = targetOverride.Resolve(graph.GetResolver());
        behaviour.includeInactiveTargets = includeInactiveTargets;
        behaviour.startAmount = Mathf.Clamp01(startAmount);
        behaviour.endAmount = Mathf.Clamp01(endAmount);
        behaviour.animateOverClip = animateOverClip;
        behaviour.exitMode = exitMode;
        behaviour.applyInEditMode = applyInEditMode;

        return playable;
    }
}
