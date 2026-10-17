using UnityEngine;
using UnityEngine.Playables;

[System.Serializable]
public class TimelineInputClip : PlayableAsset
{
    public TimelineInputBehaviour template =
        new TimelineInputBehaviour();

    public override Playable CreatePlayable(
        PlayableGraph graph,
        GameObject owner)
    {
        return ScriptPlayable<TimelineInputBehaviour>.Create(
            graph,
            template
        );
    }
}