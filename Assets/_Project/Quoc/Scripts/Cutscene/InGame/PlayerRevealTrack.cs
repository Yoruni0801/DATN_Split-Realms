using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackClipType(typeof(PlayerRevealClip))]
[TrackBindingType(typeof(Transform))]
public class PlayerRevealTrack : TrackAsset
{
    public override Playable CreateTrackMixer(
        PlayableGraph graph,
        GameObject owner,
        int inputCount)
    {
        return ScriptPlayable<PlayerRevealMixer>.Create(
            graph,
            inputCount
        );
    }
}
