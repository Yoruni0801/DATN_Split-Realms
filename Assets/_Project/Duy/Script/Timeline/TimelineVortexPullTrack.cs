using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.72f, 0.28f, 0.92f)]
[TrackClipType(typeof(TimelineVortexPullClip))]
[TrackBindingType(typeof(Transform))]
[System.ComponentModel.DisplayName("TimelineTrack/Vortex Item Pull")]
public sealed class TimelineVortexPullTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<TimelineVortexPullMixerBehaviour>.Create(graph, inputCount);
    }
}

public sealed class TimelineVortexPullMixerBehaviour : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0.001f)
            {
                continue;
            }

            Playable input = playable.GetInput(i);
            if (!input.IsValid() || input.GetPlayableType() != typeof(TimelineVortexPullBehaviour))
            {
                continue;
            }

            ScriptPlayable<TimelineVortexPullBehaviour> inputPlayable =
                (ScriptPlayable<TimelineVortexPullBehaviour>)input;
            TimelineVortexPullBehaviour behaviour = inputPlayable.GetBehaviour();
            if (behaviour != null)
            {
                behaviour.ProcessFrame(inputPlayable, info, playerData);
            }
        }
    }
}
