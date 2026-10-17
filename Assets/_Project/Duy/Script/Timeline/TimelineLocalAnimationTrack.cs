using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackClipType(typeof(TimelineLocalAnimationClip))]
[TrackBindingType(typeof(Animator))]
[System.ComponentModel.DisplayName("TimelineTrack/Local Animation")]
public class TimelineLocalAnimationTrack : TrackAsset
{
    public TimelineClip CreateClip(AnimationClip animationClip)
    {
        if (animationClip == null)
        {
            return null;
        }

        TimelineClip timelineClip = CreateClip<TimelineLocalAnimationClip>();
        TimelineLocalAnimationClip localClip = timelineClip.asset as TimelineLocalAnimationClip;
        if (localClip != null)
        {
            localClip.animationClip = animationClip;
        }

        timelineClip.displayName = animationClip.name;
        timelineClip.duration = animationClip.length;
        return timelineClip;
    }

    public override IEnumerable<PlayableBinding> outputs
    {
        get { yield return AnimationPlayableBinding.Create(name, this); }
    }

    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return AnimationMixerPlayable.Create(graph, inputCount);
    }

    protected override void OnCreateClip(TimelineClip clip)
    {
        TimelineLocalAnimationClip localClip = clip.asset as TimelineLocalAnimationClip;
        if (localClip != null && localClip.animationClip != null)
        {
            clip.displayName = localClip.animationClip.name;
            clip.duration = localClip.animationClip.length;
        }
    }

    public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
    {
        foreach (TimelineClip timelineClip in GetClips())
        {
            TimelineLocalAnimationClip localClip = timelineClip.asset as TimelineLocalAnimationClip;
            if (localClip != null)
            {
                localClip.GatherProperties(director, driver);
            }
        }
    }
}
