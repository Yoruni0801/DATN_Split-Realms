using Spine.Unity;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.85f, 0.4f, 0.6f)]
[TrackClipType(typeof(TimelineSkeletonGraphicClip))]
[TrackBindingType(typeof(SkeletonGraphic))]
[System.ComponentModel.DisplayName("TimelineTrack/SkeletonGraphic (UI Spine)")]
public class TimelineSkeletonGraphicTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<TimelineSkeletonGraphicMixerBehaviour>.Create(graph, inputCount);
    }
}

public class TimelineSkeletonGraphicMixerBehaviour : PlayableBehaviour
{
    private TimelineSkeletonGraphicBehaviour activeClip;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        SkeletonGraphic skeletonGraphic = playerData as SkeletonGraphic;
        if (skeletonGraphic == null)
        {
            ExitActiveClip();
            return;
        }

        TimelineSkeletonGraphicBehaviour nextClip = GetStrongestActiveClip(playable);
        if (nextClip == null)
        {
            ExitActiveClip();
            return;
        }

        if (activeClip != null && activeClip != nextClip)
        {
            activeClip.Exit();
        }

        activeClip = nextClip;
        activeClip.Enter(skeletonGraphic);
    }

    public override void OnGraphStop(Playable playable)
    {
        ExitActiveClip();
    }

    private TimelineSkeletonGraphicBehaviour GetStrongestActiveClip(Playable playable)
    {
        TimelineSkeletonGraphicBehaviour strongestClip = null;
        float strongestWeight = 0f;

        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0.001f || weight <= strongestWeight)
                continue;

            if (playable.GetInput(i).GetPlayableType() != typeof(TimelineSkeletonGraphicBehaviour))
                continue;

            ScriptPlayable<TimelineSkeletonGraphicBehaviour> inputPlayable =
                (ScriptPlayable<TimelineSkeletonGraphicBehaviour>)playable.GetInput(i);

            strongestClip = inputPlayable.GetBehaviour();
            strongestWeight = weight;
        }

        return strongestClip;
    }

    private void ExitActiveClip()
    {
        if (activeClip == null) return;
        activeClip.Exit();
        activeClip = null;
    }
}
