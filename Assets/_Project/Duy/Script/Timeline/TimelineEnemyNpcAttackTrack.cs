using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.75f, 0.2f, 0.15f)]
[TrackBindingType(typeof(Transform))]
[TrackClipType(typeof(TimelineEnemyNpcAttackClip))]
[System.ComponentModel.DisplayName("TimelineTrack/Enemy NPC Attack")]
public class TimelineEnemyNpcAttackTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<TimelineEnemyNpcAttackMixerBehaviour>.Create(graph, inputCount);
    }
}

public class TimelineEnemyNpcAttackMixerBehaviour : PlayableBehaviour
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
            if (playable.GetInput(i).GetPlayableType() != typeof(TimelineEnemyNpcAttackBehaviour)) continue;
            ScriptPlayable<TimelineEnemyNpcAttackBehaviour> inputPlayable = (ScriptPlayable<TimelineEnemyNpcAttackBehaviour>)playable.GetInput(i);
            inputPlayable.GetBehaviour().ProcessFrame(inputPlayable, info, playerData);
        }
    }
}



