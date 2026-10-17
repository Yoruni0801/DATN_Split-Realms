using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// Authoring-only drop zone. Dropping a .playable TimelineAsset onto the track list creates this track and
/// binds the asset to it; the editor tooling then expands the source Timeline into real tracks and deletes
/// this track again. It never takes part in playback.
/// </summary>
[TrackColor(0.85f, 0.6f, 0.2f)]
[TrackBindingType(typeof(TimelineAsset))]
[System.ComponentModel.DisplayName("TimelineTrack/Timeline Embed Drop")]
public class TimelineEmbedDropTrack : TrackAsset
{
    [Tooltip("Timeline to expand into the master Timeline. Set by drag and drop, or assigned manually in this inspector.")]
    public TimelineAsset sourceTimeline;

    public override bool CanCreateTrackMixer()
    {
        return false;
    }
}
