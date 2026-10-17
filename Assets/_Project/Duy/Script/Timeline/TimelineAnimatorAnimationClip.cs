using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum TimelineAnimatorAnimationExitMode
{
    KeepCurrent,
    RestorePrevious
}

public class TimelineAnimatorAnimationClip : PlayableAsset, ITimelineClipAsset
{
    [Header("Animation")]
    public AnimationClip animationClip;
    public string stateName;
    public int layerIndex;
    public bool loop;

    [Header("Exit")]
    public TimelineAnimatorAnimationExitMode exitMode = TimelineAnimatorAnimationExitMode.KeepCurrent;

    public override double duration
    {
        get
        {
            if (animationClip != null && animationClip.length > 0f)
            {
                return animationClip.length;
            }

            return base.duration;
        }
    }

    public ClipCaps clipCaps
    {
        get
        {
            ClipCaps caps = ClipCaps.Blending | ClipCaps.Extrapolation | ClipCaps.SpeedMultiplier;
            if (animationClip != null && !animationClip.empty)
            {
                caps |= ClipCaps.ClipIn;
            }

            if (loop)
            {
                caps |= ClipCaps.Looping;
            }

            return caps;
        }
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<TimelineAnimatorAnimationBehaviour> playable =
            ScriptPlayable<TimelineAnimatorAnimationBehaviour>.Create(graph);

        TimelineAnimatorAnimationBehaviour behaviour = playable.GetBehaviour();
        behaviour.animationClip = animationClip;
        behaviour.stateName = stateName;
        behaviour.layerIndex = layerIndex;
        behaviour.loop = loop;
        behaviour.exitMode = exitMode;

        return playable;
    }
}
