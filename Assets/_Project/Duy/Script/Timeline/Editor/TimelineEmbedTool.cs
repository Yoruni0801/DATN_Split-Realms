using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEditor.Timeline.Actions;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Expands a source TimelineAsset into the currently open master Timeline as real, directly editable tracks
/// grouped under "Embedded - {source name}", instead of referencing it through a nested PlayableDirector.
/// </summary>
public static class TimelineEmbedTool
{
    public const string GroupPrefix = "Embedded - ";

    [MenuItem("Assets/Embed Selected Timeline Here", false, 80)]
    [MenuItem("Tools/Duy/Timeline/Embed Selected Timeline Here", false, 100)]
    public static void EmbedSelectedTimelineAsset()
    {
        TimelineAsset sourceAsset = Selection.activeObject as TimelineAsset;
        if (sourceAsset == null)
        {
            EditorUtility.DisplayDialog("Embed Timeline", "Select a .playable TimelineAsset in the Project window first.", "OK");
            return;
        }

        TimelineAsset masterTimeline = TimelineEditor.inspectedAsset;
        if (masterTimeline == null)
        {
            EditorUtility.DisplayDialog("Embed Timeline", "No master Timeline is currently open in the Timeline window.", "OK");
            return;
        }

        PlayableDirector masterDirector = TimelineEditor.inspectedDirector;
        double importOffset = masterDirector != null ? masterDirector.time : 0.0;
        EmbedTimeline(masterTimeline, masterDirector, sourceAsset, importOffset);
    }

    [MenuItem("Assets/Embed Selected Timeline Here", true)]
    [MenuItem("Tools/Duy/Timeline/Embed Selected Timeline Here", true)]
    public static bool ValidateEmbedSelectedTimelineAsset()
    {
        return Selection.activeObject is TimelineAsset && TimelineEditor.inspectedAsset != null;
    }

    /// <summary>
    /// Copies every track of <paramref name="sourceAsset"/> into <paramref name="masterTimeline"/> under a single
    /// group track, shifted by <paramref name="importOffset"/>.
    /// </summary>
    public static GroupTrack EmbedTimeline(TimelineAsset masterTimeline, PlayableDirector masterDirector, TimelineAsset sourceAsset, double importOffset, PlayableDirector sourceDirector = null)
    {
        if (masterTimeline == null || sourceAsset == null) return null;

        if (masterTimeline == sourceAsset)
        {
            Debug.LogWarning("[TimelineEmbedTool] A Timeline cannot be embedded into itself.");
            return null;
        }

        Undo.RegisterCompleteObjectUndo(masterTimeline, "Embed Timeline");
        if (masterDirector != null)
        {
            Undo.RegisterCompleteObjectUndo(masterDirector, "Embed Timeline");
        }

        GroupTrack groupTrack = masterTimeline.CreateTrack<GroupTrack>(null, GroupPrefix + sourceAsset.name);

        TimelineEmbedMarker metaMarker = groupTrack.CreateMarker(typeof(TimelineEmbedMarker), importOffset) as TimelineEmbedMarker;
        if (metaMarker != null)
        {
            metaMarker.isSkipCheckpoint = false;
            metaMarker.checkpointId = string.Empty;
            metaMarker.sourceAssetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sourceAsset));
            metaMarker.sourceAssetName = sourceAsset.name;
            metaMarker.importOffset = importOffset;
        }

        CopyTracksRecursive(masterTimeline, masterDirector, sourceAsset.GetRootTracks(), groupTrack, importOffset, sourceDirector);

        EditorUtility.SetDirty(masterTimeline);
        if (masterDirector != null) EditorUtility.SetDirty(masterDirector);

        TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved | RefreshReason.ContentsModified);
        Debug.Log($"[TimelineEmbedTool] Embedded '{sourceAsset.name}' into '{masterTimeline.name}' at {importOffset:F2}s.", masterTimeline);
        return groupTrack;
    }

    /// <summary>
    /// Rebuilds one embedded group from its original source Timeline, keeping the offset it was imported with.
    /// </summary>
    public static void ReimportEmbeddedTimeline(TimelineAsset masterTimeline, PlayableDirector masterDirector, GroupTrack groupTrack)
    {
        if (masterTimeline == null || groupTrack == null) return;

        TimelineEmbedMarker marker = FindEmbedMarker(groupTrack);
        if (marker == null || string.IsNullOrEmpty(marker.sourceAssetGuid))
        {
            EditorUtility.DisplayDialog("Reimport Timeline", "This group has no embed metadata, so there is no source Timeline to reimport from.", "OK");
            return;
        }

        string assetPath = AssetDatabase.GUIDToAssetPath(marker.sourceAssetGuid);
        TimelineAsset sourceAsset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(assetPath);
        if (sourceAsset == null)
        {
            EditorUtility.DisplayDialog("Reimport Timeline", $"Source Timeline '{marker.sourceAssetName}' could not be found.", "OK");
            return;
        }

        double importOffset = marker.importOffset;

        Undo.RegisterCompleteObjectUndo(masterTimeline, "Reimport Embedded Timeline");
        DeleteTrackTree(masterTimeline, masterDirector, groupTrack);

        EmbedTimeline(masterTimeline, masterDirector, sourceAsset, importOffset);
    }

    public static TimelineEmbedMarker FindEmbedMarker(TrackAsset track)
    {
        if (track == null) return null;
        foreach (IMarker marker in track.GetMarkers())
        {
            TimelineEmbedMarker embedMarker = marker as TimelineEmbedMarker;
            if (embedMarker != null) return embedMarker;
        }
        return null;
    }

    /// <summary>
    /// Deletes a track and its subtracks, destroying the marker sub assets that DeleteTrack leaves behind.
    /// </summary>
    public static void DeleteTrackTree(TimelineAsset masterTimeline, PlayableDirector masterDirector, TrackAsset track)
    {
        if (masterTimeline == null || track == null) return;

        DestroyMarkersRecursive(masterDirector, track);
        masterTimeline.DeleteTrack(track);
        EditorUtility.SetDirty(masterTimeline);
    }

    private static void DestroyMarkersRecursive(PlayableDirector masterDirector, TrackAsset track)
    {
        foreach (TrackAsset child in track.GetChildTracks())
        {
            DestroyMarkersRecursive(masterDirector, child);
        }

        if (masterDirector != null)
        {
            masterDirector.ClearGenericBinding(track);
        }

        List<ScriptableObject> markerObjects = new List<ScriptableObject>();
        foreach (IMarker marker in track.GetMarkers())
        {
            ScriptableObject markerObject = marker as ScriptableObject;
            if (markerObject != null) markerObjects.Add(markerObject);
        }

        foreach (ScriptableObject markerObject in markerObjects)
        {
            track.DeleteMarker(markerObject as IMarker);
            UnityEngine.Object.DestroyImmediate(markerObject, true);
        }
    }

    private static void CopyTracksRecursive(TimelineAsset masterTimeline, PlayableDirector masterDirector, IEnumerable<TrackAsset> sourceTracks, TrackAsset parentTrack, double importOffset, PlayableDirector sourceDirector)
    {
        foreach (TrackAsset sourceTrack in sourceTracks)
        {
            if (sourceTrack == null) continue;

            // The timeline-level marker track cannot be parented, so its markers go into a SignalTrack inside the group.
            if (sourceTrack is MarkerTrack)
            {
                CopyTimelineMarkers(masterTimeline, sourceTrack, parentTrack, importOffset);
                continue;
            }

            if (sourceTrack is GroupTrack sourceGroup)
            {
                GroupTrack newGroup = masterTimeline.CreateTrack<GroupTrack>(parentTrack, sourceGroup.name);
                newGroup.muted = sourceGroup.muted;
                newGroup.locked = sourceGroup.locked;
                CopyTracksRecursive(masterTimeline, masterDirector, sourceGroup.GetChildTracks(), newGroup, importOffset, sourceDirector);
                continue;
            }

            TrackAsset newTrack = CreateTrackSafe(masterTimeline, sourceTrack.GetType(), parentTrack, sourceTrack.name);
            if (newTrack == null) continue;

            // Carries track type specific settings such as AnimationTrack offsets or custom track fields.
            EditorUtility.CopySerialized(sourceTrack, newTrack);
            newTrack.name = sourceTrack.name;
            SanitizeCopiedTrack(masterTimeline, newTrack, parentTrack);
            newTrack.muted = sourceTrack.muted;
            newTrack.locked = sourceTrack.locked;

            foreach (TimelineClip sourceClip in sourceTrack.GetClips())
            {
                CopyClip(masterTimeline, newTrack, sourceClip, importOffset, masterDirector, sourceDirector);
            }

            foreach (IMarker sourceMarker in sourceTrack.GetMarkers())
            {
                CopyMarker(newTrack, sourceMarker, importOffset);
            }

            bool bound = false;
            if (masterDirector != null && sourceDirector != null)
            {
                UnityEngine.Object binding = sourceDirector.GetGenericBinding(sourceTrack);
                if (binding != null)
                {
                    masterDirector.SetGenericBinding(newTrack, binding);
                    bound = true;
                }
            }

            if (!bound)
            {
                BindAudioTrackFallback(masterDirector, newTrack);
            }

            // Subtracks such as AnimationTrack override tracks.
            CopyTracksRecursive(masterTimeline, masterDirector, sourceTrack.GetChildTracks(), newTrack, importOffset, sourceDirector);
        }
    }

    /// <summary>
    /// Dragging in a .playable gives no source PlayableDirector, so a copied AudioTrack would arrive without an
    /// AudioSource binding and stay silent. Only AudioTrack gets this fallback: every other track type points at
    /// scene specific objects that cannot be guessed.
    /// </summary>
    private static void BindAudioTrackFallback(PlayableDirector masterDirector, TrackAsset newTrack)
    {
        if (masterDirector == null || !(newTrack is AudioTrack))
        {
            return;
        }

        GameObject host = masterDirector.gameObject;
        AudioSource audioSource = host.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            Undo.RegisterCompleteObjectUndo(host, "Embed Timeline Audio Source");
            audioSource = Undo.AddComponent<AudioSource>(host);
            audioSource.playOnAwake = false;
            Debug.Log($"[TimelineEmbedTool] Added an AudioSource to '{host.name}' for embedded audio track '{newTrack.name}'.", host);
        }

        masterDirector.SetGenericBinding(newTrack, audioSource);
    }

    private static TrackAsset CreateTrackSafe(TimelineAsset masterTimeline, Type trackType, TrackAsset parentTrack, string trackName)
    {
        try
        {
            return masterTimeline.CreateTrack(trackType, parentTrack, trackName);
        }
        catch (InvalidOperationException e)
        {
            Debug.LogWarning($"[TimelineEmbedTool] Skipped track '{trackName}' ({trackType.Name}): {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// CopySerialized also copies the structural fields of the source track: its parent, its clip, marker and
    /// child lists, and its recorded animation. Left as is, the copy would point back into the source Timeline,
    /// so those fields are reset and the contents are cloned one by one instead.
    /// </summary>
    private static void SanitizeCopiedTrack(TimelineAsset masterTimeline, TrackAsset track, TrackAsset parentTrack)
    {
        SerializedObject serializedTrack = new SerializedObject(track);

        SerializedProperty parent = serializedTrack.FindProperty("m_Parent");
        if (parent != null)
        {
            parent.objectReferenceValue = parentTrack != null ? (UnityEngine.Object)parentTrack : masterTimeline;
        }

        ClearArrayProperty(serializedTrack, "m_Clips");
        ClearArrayProperty(serializedTrack, "m_Markers.m_Objects");
        ClearArrayProperty(serializedTrack, "m_Children");

        // Recorded animation clips live inside the source asset and must not be shared.
        SerializedProperty curves = serializedTrack.FindProperty("m_Curves");
        if (curves != null) curves.objectReferenceValue = null;
        SerializedProperty infiniteClip = serializedTrack.FindProperty("m_InfiniteClip");
        if (infiniteClip != null) infiniteClip.objectReferenceValue = null;

        serializedTrack.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ClearArrayProperty(SerializedObject serializedTrack, string propertyPath)
    {
        SerializedProperty property = serializedTrack.FindProperty(propertyPath);
        if (property != null && property.isArray) property.ClearArray();
    }

    private static void CopyTimelineMarkers(TimelineAsset masterTimeline, TrackAsset sourceMarkerTrack, TrackAsset parentTrack, double importOffset)
    {
        bool hasMarkers = false;
        foreach (IMarker unused in sourceMarkerTrack.GetMarkers())
        {
            hasMarkers = true;
            break;
        }
        if (!hasMarkers) return;

        TrackAsset markerHost = masterTimeline.CreateTrack<SignalTrack>(parentTrack, "Timeline Markers");
        foreach (IMarker sourceMarker in sourceMarkerTrack.GetMarkers())
        {
            CopyMarker(markerHost, sourceMarker, importOffset);
        }
    }

    private static void CopyClip(TimelineAsset masterTimeline, TrackAsset targetTrack, TimelineClip sourceClip, double importOffset, PlayableDirector masterDirector, PlayableDirector sourceDirector)
    {
        if (masterTimeline == null || targetTrack == null || sourceClip == null) return;

        TimelineClip newClip = targetTrack.CreateDefaultClip();
        if (newClip == null) return;

        ScriptableObject sourceClipAsset = sourceClip.asset as ScriptableObject;
        if (sourceClipAsset != null)
        {
            if (newClip.asset != null && newClip.asset.GetType() != sourceClipAsset.GetType())
            {
                UnityEngine.Object defaultAsset = newClip.asset;
                newClip.asset = null;
                UnityEngine.Object.DestroyImmediate(defaultAsset, true);
            }

            if (newClip.asset == null)
            {
                ScriptableObject clonedAsset = UnityEngine.Object.Instantiate(sourceClipAsset);
                AssetDatabase.AddObjectToAsset(clonedAsset, masterTimeline);
                clonedAsset.hideFlags |= HideFlags.HideInHierarchy;
                newClip.asset = clonedAsset;
            }
            else
            {
                EditorUtility.CopySerialized(sourceClipAsset, newClip.asset);
            }

            newClip.asset.name = sourceClipAsset.name;
            RemapExposedReferences(sourceClipAsset, newClip.asset as ScriptableObject, sourceDirector, masterDirector);
        }

        newClip.start = sourceClip.start + importOffset;
        newClip.duration = sourceClip.duration;
        newClip.clipIn = sourceClip.clipIn;
        newClip.timeScale = sourceClip.timeScale;
        newClip.displayName = sourceClip.displayName;
        newClip.easeInDuration = sourceClip.easeInDuration;
        newClip.easeOutDuration = sourceClip.easeOutDuration;
        newClip.blendInDuration = sourceClip.blendInDuration;
        newClip.blendOutDuration = sourceClip.blendOutDuration;
        newClip.blendInCurveMode = sourceClip.blendInCurveMode;
        newClip.blendOutCurveMode = sourceClip.blendOutCurveMode;
    }

    private static void CopyMarker(TrackAsset targetTrack, IMarker sourceMarker, double importOffset)
    {
        ScriptableObject sourceMarkerObject = sourceMarker as ScriptableObject;
        if (targetTrack == null || sourceMarkerObject == null) return;
        if (sourceMarker is TimelineEmbedMarker) return;

        Type markerType = sourceMarkerObject.GetType();
        if (typeof(INotification).IsAssignableFrom(markerType) && !targetTrack.supportsNotifications)
        {
            Debug.LogWarning($"[TimelineEmbedTool] Marker '{sourceMarkerObject.name}' was skipped: track '{targetTrack.name}' does not support notifications.");
            return;
        }

        double newTime = sourceMarker.time + importOffset;
        IMarker newMarker = targetTrack.CreateMarker(markerType, newTime);
        ScriptableObject newMarkerObject = newMarker as ScriptableObject;
        if (newMarkerObject == null) return;

        string createdName = newMarkerObject.name;
        HideFlags createdFlags = newMarkerObject.hideFlags;
        EditorUtility.CopySerialized(sourceMarkerObject, newMarkerObject);
        newMarkerObject.name = createdName;
        newMarkerObject.hideFlags = createdFlags;
        newMarker.time = newTime;
    }

    /// <summary>
    /// ExposedReferences resolve through the director that owns them, so a copied clip only keeps its scene
    /// references when the source director is known.
    /// </summary>
    private static void RemapExposedReferences(ScriptableObject sourceAsset, ScriptableObject targetAsset, PlayableDirector sourceDirector, PlayableDirector masterDirector)
    {
        if (sourceAsset == null || targetAsset == null || sourceDirector == null || masterDirector == null) return;

        SerializedObject sourceSerialized = new SerializedObject(sourceAsset, sourceDirector);
        SerializedObject targetSerialized = new SerializedObject(targetAsset, masterDirector);

        SerializedProperty iterator = sourceSerialized.GetIterator();
        while (iterator.NextVisible(true))
        {
            if (iterator.propertyType != SerializedPropertyType.ExposedReference) continue;

            SerializedProperty targetProperty = targetSerialized.FindProperty(iterator.propertyPath);
            if (targetProperty == null) continue;

            targetProperty.exposedReferenceValue = iterator.exposedReferenceValue;
        }

        targetSerialized.ApplyModifiedPropertiesWithoutUndo();
    }
}

/// <summary>
/// Right click context menu entry on an embedded group track in the Timeline window.
/// </summary>
[MenuEntry("Reimport Embedded Timeline", 9000)]
public class ReimportEmbeddedTimelineAction : TrackAction
{
    public override ActionValidity Validate(IEnumerable<TrackAsset> tracks)
    {
        foreach (TrackAsset track in tracks)
        {
            if (track is GroupTrack && TimelineEmbedTool.FindEmbedMarker(track) != null)
            {
                return ActionValidity.Valid;
            }
        }
        return ActionValidity.NotApplicable;
    }

    public override bool Execute(IEnumerable<TrackAsset> tracks)
    {
        TimelineAsset masterTimeline = TimelineEditor.inspectedAsset;
        PlayableDirector masterDirector = TimelineEditor.inspectedDirector;
        if (masterTimeline == null) return false;

        List<GroupTrack> targets = new List<GroupTrack>();
        foreach (TrackAsset track in tracks)
        {
            if (track is GroupTrack groupTrack && TimelineEmbedTool.FindEmbedMarker(groupTrack) != null)
            {
                targets.Add(groupTrack);
            }
        }

        foreach (GroupTrack groupTrack in targets)
        {
            TimelineEmbedTool.ReimportEmbeddedTimeline(masterTimeline, masterDirector, groupTrack);
        }

        return targets.Count > 0;
    }
}
