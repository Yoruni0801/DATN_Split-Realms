using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.35f, 0.55f, 0.95f)]
[TrackBindingType(typeof(Transform))]
[TrackClipType(typeof(TimelineAshwingPickupDropClip))]
[System.ComponentModel.DisplayName("TimelineTrack/Ashwing Pickup Drop")]
public sealed class TimelineAshwingPickupDropTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<TimelineAshwingPickupDropMixerBehaviour>.Create(graph, inputCount);
    }
}

public sealed class TimelineAshwingPickupDropMixerBehaviour : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            if (playable.GetInputWeight(i) <= 0.001f)
            {
                continue;
            }
            if (playable.GetInput(i).GetPlayableType() != typeof(TimelineAshwingPickupDropBehaviour)) continue;
            ScriptPlayable<TimelineAshwingPickupDropBehaviour> inputPlayable = (ScriptPlayable<TimelineAshwingPickupDropBehaviour>)playable.GetInput(i);
            inputPlayable.GetBehaviour().ProcessFrame(inputPlayable, info, playerData);
        }
    }
}
