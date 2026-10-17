using UnityEditor.Timeline;
using UnityEngine.Timeline;

[CustomTimelineEditor(typeof(TimelineActionClip))]
public class TimelineClipTimelineEditor : ClipEditor
{
    public override ClipDrawOptions GetClipOptions(TimelineClip clip)
    {
        return TimelineClipLockDecorator.Decorate(base.GetClipOptions(clip), clip);
    }

    public override void OnClipChanged(TimelineClip clip)
    {
        var asset = clip.asset as TimelineActionClip;
        if (asset == null)
            return;

        switch (asset.clipType)
        {
            case TimelineClipType.SpineMove:       clip.displayName = "Move";    break;
            case TimelineClipType.SpineAnimation:   clip.displayName = "Anim";    break;
            case TimelineClipType.CameraFollow:     clip.displayName = "Camera";  break;
            case TimelineClipType.LoadScene:         clip.displayName = "Scene";   break;
            case TimelineClipType.Petrify:           clip.displayName = "Petrify"; break;
            case TimelineClipType.Unpetrify:         clip.displayName = "Unpetrify"; break;
            case TimelineClipType.EnemyNpcAttack:   clip.displayName = "Attack";  break;
            case TimelineClipType.AshwingPickupDrop: clip.displayName = "Ashwing"; break;
        }
    }
}
