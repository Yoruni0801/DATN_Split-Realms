using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.25f, 0.55f, 0.95f)]
[TrackClipType(typeof(TimelineAnimatorAnimationClip))]
[TrackBindingType(typeof(Animator))]
[System.ComponentModel.DisplayName("TimelineTrack/Animator Animation")]
public class TimelineAnimatorAnimationTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<TimelineAnimatorAnimationMixerBehaviour>.Create(graph, inputCount);
    }
}

public class TimelineAnimatorAnimationMixerBehaviour : PlayableBehaviour
{
    private TimelineAnimatorAnimationBehaviour activeClip;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        Animator animator = playerData as Animator;
        if (animator == null)
        {
            ExitActiveClip();
            return;
        }

        ScriptPlayable<TimelineAnimatorAnimationBehaviour> nextPlayable = GetStrongestActivePlayable(playable);
        if (!nextPlayable.IsValid())
        {
            ExitActiveClip();
            return;
        }

        TimelineAnimatorAnimationBehaviour nextClip = nextPlayable.GetBehaviour();
        if (activeClip != null && activeClip != nextClip)
        {
            activeClip.Exit();
        }

        activeClip = nextClip;
        activeClip.Evaluate(animator, nextPlayable);
    }

    public override void OnGraphStop(Playable playable)
    {
        ExitActiveClip();
    }

    private ScriptPlayable<TimelineAnimatorAnimationBehaviour> GetStrongestActivePlayable(Playable playable)
    {
        ScriptPlayable<TimelineAnimatorAnimationBehaviour> strongestPlayable =
            default(ScriptPlayable<TimelineAnimatorAnimationBehaviour>);
        float strongestWeight = 0f;

        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0.001f || weight <= strongestWeight)
            {
                continue;
            }

            if (playable.GetInput(i).GetPlayableType() != typeof(TimelineAnimatorAnimationBehaviour)) continue;
            strongestPlayable = (ScriptPlayable<TimelineAnimatorAnimationBehaviour>)playable.GetInput(i);
            strongestWeight = weight;
        }

        return strongestPlayable;
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
