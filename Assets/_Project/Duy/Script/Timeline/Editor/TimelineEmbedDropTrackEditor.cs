using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Turns a <see cref="TimelineEmbedDropTrack"/> into a one shot drop zone: as soon as a TimelineAsset lands on it,
/// the source Timeline is expanded into the master Timeline and the drop track removes itself.
/// </summary>
[CustomTimelineEditor(typeof(TimelineEmbedDropTrack))]
public class TimelineEmbedDropTrackEditor : TrackEditor
{
    private static readonly HashSet<TimelineEmbedDropTrack> PendingTracks = new HashSet<TimelineEmbedDropTrack>();

    public override TrackDrawOptions GetTrackOptions(TrackAsset track, Object binding)
    {
        TrackDrawOptions options = base.GetTrackOptions(track, binding);

        TimelineEmbedDropTrack dropTrack = track as TimelineEmbedDropTrack;
        if (dropTrack == null) return options;

        TimelineAsset source = binding as TimelineAsset;
        if (source == null) source = dropTrack.sourceTimeline;

        if (source == null)
        {
            options.errorText = "Drop a .playable Timeline here to expand it into this Timeline at the playhead.";
        }
        else
        {
            ScheduleEmbed(dropTrack, source);
        }

        return options;
    }

    public static void ScheduleEmbed(TimelineEmbedDropTrack dropTrack, TimelineAsset source)
    {
        if (dropTrack == null || source == null) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!PendingTracks.Add(dropTrack)) return;

        EditorApplication.delayCall += () =>
        {
            PendingTracks.Remove(dropTrack);
            Embed(dropTrack, source);
        };
    }

    private static void Embed(TimelineEmbedDropTrack dropTrack, TimelineAsset source)
    {
        if (dropTrack == null || source == null) return;

        TimelineAsset masterTimeline = dropTrack.timelineAsset;
        if (masterTimeline == null) return;

        PlayableDirector masterDirector = TimelineEditor.inspectedDirector;

        if (masterTimeline == source)
        {
            Debug.LogWarning("[TimelineEmbedTool] A Timeline cannot be embedded into itself.");
        }
        else
        {
            double importOffset = masterDirector != null ? masterDirector.time : 0.0;
            TimelineEmbedTool.EmbedTimeline(masterTimeline, masterDirector, source, importOffset);
        }

        TimelineEmbedTool.DeleteTrackTree(masterTimeline, masterDirector, dropTrack);
        TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved);
    }
}

[CustomEditor(typeof(TimelineEmbedDropTrack))]
public class TimelineEmbedDropTrackInspector : Editor
{
    public override void OnInspectorGUI()
    {
        TimelineEmbedDropTrack dropTrack = (TimelineEmbedDropTrack)target;

        EditorGUILayout.HelpBox("Drop a .playable Timeline onto this track to expand it into the open Timeline at the playhead. This track is removed once the embed is done.", MessageType.Info);

        EditorGUI.BeginChangeCheck();
        TimelineAsset source = (TimelineAsset)EditorGUILayout.ObjectField("Source Timeline", dropTrack.sourceTimeline, typeof(TimelineAsset), false);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(dropTrack, "Set Source Timeline");
            dropTrack.sourceTimeline = source;
            EditorUtility.SetDirty(dropTrack);
        }

        using (new EditorGUI.DisabledScope(dropTrack.sourceTimeline == null))
        {
            if (GUILayout.Button("Embed Into This Timeline", GUILayout.Height(26)))
            {
                TimelineEmbedDropTrackEditor.ScheduleEmbed(dropTrack, dropTrack.sourceTimeline);
            }
        }
    }
}
