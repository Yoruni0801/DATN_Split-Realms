using System.ComponentModel;
using UnityEngine.Timeline;

/// <summary>
/// Metadata stored on an embedded group track so the group can be reimported from its source TimelineAsset.
/// This type lives in the runtime assembly on purpose: it is serialized inside .playable assets,
/// so an editor-only type would fail to resolve in builds.
/// </summary>
[DisplayName("Timeline/Checkpoint")]
public class TimelineEmbedMarker : Marker
{
    public bool isSkipCheckpoint = true;
    public bool enableBlackTransition = true;
    public string checkpointId;
    public string checkpointLabel;

    public string sourceAssetGuid;
    public string sourceAssetName;
    public double importOffset;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (isSkipCheckpoint && string.IsNullOrWhiteSpace(checkpointId))
        {
            checkpointId = $"checkpoint_{System.Guid.NewGuid():N}";
        }
    }
#endif
}
