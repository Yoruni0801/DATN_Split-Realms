using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.25f, 0.45f, 0.85f)]
[TrackClipType(typeof(TimelineLoadSceneClip))]
[System.ComponentModel.DisplayName("TimelineTrack/Load Scene")]
public class TimelineLoadSceneTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<TimelineLoadSceneMixerBehaviour>.Create(graph, inputCount);
    }
}

public class TimelineLoadSceneMixerBehaviour : PlayableBehaviour
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

            if (playable.GetInput(i).GetPlayableType() != typeof(TimelineLoadSceneBehaviour)) continue;
            ScriptPlayable<TimelineLoadSceneBehaviour> inputPlayable =
                (ScriptPlayable<TimelineLoadSceneBehaviour>)playable.GetInput(i);
            inputPlayable.GetBehaviour().ProcessFrame(inputPlayable, info, playerData);
        }
    }
}
