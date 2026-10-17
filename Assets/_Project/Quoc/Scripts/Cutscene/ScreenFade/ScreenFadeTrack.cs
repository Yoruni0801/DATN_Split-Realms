using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;

[TrackClipType(typeof(ScreenFadeClip))]
[TrackBindingType(typeof(Image))]
public class ScreenFadeTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject owner, int inputCount)
    {
        return ScriptPlayable<ScreenFadeMixer>.Create(graph, inputCount);
    }
}
