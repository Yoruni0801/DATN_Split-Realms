using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.25f, 0.8f, 0.55f)]
[TrackClipType(typeof(TimelinePlayerSpineMovePointAToBClip))]
[TrackBindingType(typeof(AnimationSpine))]
[System.ComponentModel.DisplayName("TimelineTrack/Player Spine Move Point A To B")]
public class TimelinePlayerSpineMovePointAToBTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<TimelinePlayerSpineMovePointAToBMixerBehaviour>.Create(graph, inputCount);
    }
}

public class TimelinePlayerSpineMovePointAToBMixerBehaviour : PlayableBehaviour
{
    private TimelinePlayerSpineMovePointAToBBehaviour activeClip;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        TimelinePlayerSpineMovePointAToBClipState nextClip = GetStrongestActiveClip(playable);
        if (nextClip.Behaviour == null)
        {
            ExitActiveClip();
            return;
        }

        AnimationSpine animationSpine = playerData as AnimationSpine;
        if (animationSpine == null)
        {
            ExitActiveClip();
            return;
        }

        if (activeClip != null && activeClip != nextClip.Behaviour)
        {
            activeClip.Exit();
        }

        activeClip = nextClip.Behaviour;
        activeClip.Enter(animationSpine);
        activeClip.Process(nextClip.LocalTime);
    }

    public override void OnGraphStop(Playable playable)
    {
        ExitActiveClip();
    }

    private TimelinePlayerSpineMovePointAToBClipState GetStrongestActiveClip(Playable playable)
    {
        TimelinePlayerSpineMovePointAToBClipState strongestClip =
            new TimelinePlayerSpineMovePointAToBClipState();
        float strongestWeight = 0f;

        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0.001f || weight <= strongestWeight)
            {
                continue;
            }

            ScriptPlayable<TimelinePlayerSpineMovePointAToBBehaviour> inputPlayable =
                (ScriptPlayable<TimelinePlayerSpineMovePointAToBBehaviour>)playable.GetInput(i);

            strongestClip = new TimelinePlayerSpineMovePointAToBClipState(
                inputPlayable.GetBehaviour(),
                GetLocalTime(inputPlayable));
            strongestWeight = weight;
        }

        return strongestClip;
    }

    private float GetLocalTime(Playable playable)
    {
        double time = playable.GetTime();
        if (double.IsInfinity(time) || double.IsNaN(time) || time < 0d)
        {
            return 0f;
        }

        return (float)time;
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

    private struct TimelinePlayerSpineMovePointAToBClipState
    {
        public TimelinePlayerSpineMovePointAToBBehaviour Behaviour;
        public float LocalTime;

        public TimelinePlayerSpineMovePointAToBClipState(
            TimelinePlayerSpineMovePointAToBBehaviour behaviour,
            float localTime)
        {
            Behaviour = behaviour;
            LocalTime = localTime;
        }
    }
}
