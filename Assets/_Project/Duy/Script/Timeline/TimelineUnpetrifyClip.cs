using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class TimelineUnpetrifyClip : PlayableAsset, ITimelineClipAsset
{
    public ExposedReference<PetrifyController> targetOverride;

    public bool includeInactiveTargets;
    public bool applyInEditMode = true;
    public bool animateOverClip = true;

    [Range(0f, 1f)] public float startAmount = 1f;
    [Range(0f, 1f)] public float endAmount = 0f;

    public TimelineUnpetrifyExitMode exitMode = TimelineUnpetrifyExitMode.ClearPetrify;
    public TimelineUnpetrifyTargetMode targetMode = TimelineUnpetrifyTargetMode.BoundTarget;

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<TimelineUnpetrifyBehaviour> playable =
            ScriptPlayable<TimelineUnpetrifyBehaviour>.Create(graph);

        TimelineUnpetrifyBehaviour behaviour = playable.GetBehaviour();
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
