using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.8f, 0.45f, 0.2f)]
[TrackClipType(typeof(TimelineEnemyMovePointAToBClip))]
[TrackBindingType(typeof(Transform))]
[System.ComponentModel.DisplayName("TimelineTrack/Enemy Move Point A To B")]
public class TimelineEnemyMovePointAToBTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<TimelineEnemyMovePointAToBMixerBehaviour>.Create(graph, inputCount);
    }
}

public class TimelineEnemyMovePointAToBMixerBehaviour : PlayableBehaviour
{
    private TimelineEnemyMovePointAToBBehaviour activeClip;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        TimelineEnemyMovePointAToBClipState nextClip = GetStrongestActiveClip(playable);
        if (nextClip.Behaviour == null)
        {
            ExitActiveClip();
            return;
        }

        Transform enemy = playerData as Transform;
        if (enemy == null)
        {
            ExitActiveClip();
            return;
        }

        if (activeClip != null && activeClip != nextClip.Behaviour)
        {
            activeClip.Exit();
        }

        activeClip = nextClip.Behaviour;
        activeClip.Enter(enemy);
        activeClip.Process(nextClip.LocalTime, nextClip.Duration);
    }

    public override void OnGraphStop(Playable playable)
    {
        ExitActiveClip();
    }

    private TimelineEnemyMovePointAToBClipState GetStrongestActiveClip(Playable playable)
    {
        TimelineEnemyMovePointAToBClipState strongestClip =
            new TimelineEnemyMovePointAToBClipState();
        float strongestWeight = 0f;

        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0.001f || weight <= strongestWeight)
            {
                continue;
            }

            Playable input = playable.GetInput(i);
            ScriptPlayable<TimelineEnemyMovePointAToBBehaviour> inputPlayable =
                (ScriptPlayable<TimelineEnemyMovePointAToBBehaviour>)input;

            strongestClip = new TimelineEnemyMovePointAToBClipState(
                inputPlayable.GetBehaviour(),
                GetLocalTime(inputPlayable),
                GetDuration(inputPlayable));
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

    private float GetDuration(Playable playable)
    {
        double duration = playable.GetDuration();
        if (double.IsInfinity(duration) || double.IsNaN(duration) || duration <= 0d)
        {
            return 1f;
        }

        return (float)duration;
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

    private struct TimelineEnemyMovePointAToBClipState
    {
        public TimelineEnemyMovePointAToBBehaviour Behaviour;
        public float LocalTime;
        public float Duration;

        public TimelineEnemyMovePointAToBClipState(
            TimelineEnemyMovePointAToBBehaviour behaviour,
            float localTime,
            float duration)
        {
            Behaviour = behaviour;
            LocalTime = localTime;
            Duration = duration;
        }
    }
}
