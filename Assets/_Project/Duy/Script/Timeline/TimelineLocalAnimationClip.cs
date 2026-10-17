using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class TimelineLocalAnimationClip : PlayableAsset, ITimelineClipAsset, IPropertyPreview
{
    [Header("Animation")]
    public AnimationClip animationClip;
    public bool applyFootIK = true;

    public override double duration
    {
        get
        {
            if (animationClip == null || animationClip.length <= 0f)
            {
                return base.duration;
            }

            return animationClip.length;
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

            if (ShouldLoop())
            {
                caps |= ClipCaps.Looping;
            }

            return caps;
        }
    }

    public override IEnumerable<PlayableBinding> outputs
    {
        get { yield return AnimationPlayableBinding.Create(name, this); }
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        if (animationClip == null || animationClip.legacy)
        {
            return Playable.Null;
        }

        AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, animationClip);
        playable.SetApplyFootIK(applyFootIK);
        return playable;
    }

    public void GatherProperties(PlayableDirector director, IPropertyCollector driver)
    {
        if (animationClip != null)
        {
            driver.AddFromClip(animationClip);
        }
    }

    private bool ShouldLoop()
    {
        if (animationClip == null)
        {
            return false;
        }

        return animationClip.isLooping;
    }
}
