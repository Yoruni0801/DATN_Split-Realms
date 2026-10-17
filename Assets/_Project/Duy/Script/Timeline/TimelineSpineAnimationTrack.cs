using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.6f, 0.35f, 0.9f)]
[TrackClipType(typeof(TimelineSpineAnimationClip))]
[TrackBindingType(typeof(AnimationSpine))]
[System.ComponentModel.DisplayName("TimelineTrack/Spine Animation")]
public class TimelineSpineAnimationTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<TimelineSpineAnimationMixerBehaviour>.Create(graph, inputCount);
    }
}

public class TimelineSpineAnimationMixerBehaviour : PlayableBehaviour
{
    private TimelineSpineAnimationBehaviour activeClip;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        AnimationSpine animationSpine = playerData as AnimationSpine;
        if (animationSpine == null)
        {
            ExitActiveClip();
            return;
        }

        TimelineSpineAnimationBehaviour nextClip = GetStrongestActiveClip(playable);
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
        activeClip.Enter(animationSpine);
    }

    public override void OnGraphStop(Playable playable)
    {
        ExitActiveClip();
    }

    private TimelineSpineAnimationBehaviour GetStrongestActiveClip(Playable playable)
    {
        TimelineSpineAnimationBehaviour strongestClip = null;
        float strongestWeight = 0f;

        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0.001f || weight <= strongestWeight)
            {
                continue;
            }
            if (playable.GetInput(i).GetPlayableType() != typeof(TimelineSpineAnimationBehaviour)) continue;
            ScriptPlayable<TimelineSpineAnimationBehaviour> inputPlayable = (ScriptPlayable<TimelineSpineAnimationBehaviour>)playable.GetInput(i);

            strongestClip = inputPlayable.GetBehaviour();
            strongestWeight = weight;
        }

        return strongestClip;
    }

    private void ExitActiveClip()
    {
        if (activeClip == null)
        {
            return;
        }

        activeClip.Exit();
        activeClip = null;
    }
}
