using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEditor.Timeline.Actions;
using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// What a locked pair keeps in sync. StartAndEnd is deliberately the zero value so pairs saved before this field
/// existed deserialize to the original combined behaviour.
/// </summary>
public enum TimelineClipLockMode
{
    StartAndEnd = 0,
    StartOnly = 1,
    EndOnly = 2
}

[Serializable]
public sealed class TimelineClipLockPair
{
    public UnityEngine.Object firstClipAsset;
    public UnityEngine.Object secondClipAsset;
    public TimelineClipLockMode lockMode;
    public double lastFirstStart;
    public double lastFirstEnd;
    public double lastSecondStart;
    public double lastSecondEnd;
}

[Serializable]
public sealed class TimelineClipRippleTarget
{
    public UnityEngine.Object clipAsset;
    public double lastStart;
    public double lastEnd;
}

[Serializable]
public sealed class TimelineClipGroupMember
{
    public UnityEngine.Object clipAsset;
    public double lastStart;
    public double lastEnd;
}

/// <summary>
/// Two or more clips of the same track that behave as one movable block, stored in timeline order.
/// </summary>
[Serializable]
public sealed class TimelineClipLockGroup
{
    public List<TimelineClipGroupMember> members = new List<TimelineClipGroupMember>();
}

/// <summary>A clip whose length is held fixed while it is edited.</summary>
[Serializable]
public sealed class TimelineClipDurationLock
{
    public UnityEngine.Object clipAsset;
    public double lockedDuration;
    public double lastStart;
    public double lastEnd;
}

public sealed class TimelineClipLockMetadata : ScriptableObject
{
    public List<TimelineClipLockPair> lockedPairs = new List<TimelineClipLockPair>();
    public List<TimelineClipRippleTarget> rippleTargets = new List<TimelineClipRippleTarget>();
    public List<TimelineClipLockGroup> lockedGroups = new List<TimelineClipLockGroup>();
    public List<TimelineClipDurationLock> durationLocks = new List<TimelineClipDurationLock>();
}

public static class TimelineClipLockTool
{
    private const string MetadataName = "Timeline Clip Lock Metadata";

    /// <summary>Mirrors MinClipDuration, which is internal to the Timeline package (one frame at 60 fps).</summary>
    private const double MinClipDuration = 1.0 / 60.0;

    private static bool isSyncing;

    /// <summary>
    /// Set right after an undo or redo. The next update rebaselines the stored positions from the restored
    /// Timeline instead of syncing, otherwise the watcher would immediately reapply what the undo just reverted.
    /// </summary>
    private static bool skipNextSync;

    private static TimelineAsset cachedTimeline;
    private static TimelineClipLockMetadata cachedMetadata;
    private static readonly HashSet<UnityEngine.Object> LockedClipAssets = new HashSet<UnityEngine.Object>();
    private static readonly HashSet<UnityEngine.Object> RippleClipAssets = new HashSet<UnityEngine.Object>();
    private static readonly HashSet<UnityEngine.Object> GroupedClipAssets = new HashSet<UnityEngine.Object>();
    private static readonly HashSet<UnityEngine.Object> DurationLockedClipAssets = new HashSet<UnityEngine.Object>();

    [InitializeOnLoadMethod]
    private static void Init()
    {
        EditorApplication.update -= UpdateInspectedTimeline;
        EditorApplication.update += UpdateInspectedTimeline;
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
    }

    private static void OnUndoRedoPerformed()
    {
        skipNextSync = true;
        InvalidateCache();
    }

    public static void InvalidateCache()
    {
        cachedTimeline = null;
        cachedMetadata = null;
    }

    /// <summary>True when the clip is part of a start/end locked pair in the Timeline currently open.</summary>
    public static bool IsClipLocked(TimelineClip clip)
    {
        return clip != null && clip.asset != null && LockedClipAssets.Contains(clip.asset);
    }

    /// <summary>True when the clip pushes the clips after it in the Timeline currently open.</summary>
    public static bool IsClipRippled(TimelineClip clip)
    {
        return clip != null && clip.asset != null && RippleClipAssets.Contains(clip.asset);
    }

    /// <summary>True when the clip belongs to a same track locked group in the Timeline currently open.</summary>
    public static bool IsClipGroupLocked(TimelineClip clip)
    {
        return clip != null && clip.asset != null && GroupedClipAssets.Contains(clip.asset);
    }

    /// <summary>True when the clip keeps a fixed length while it is edited.</summary>
    public static bool IsClipDurationLocked(TimelineClip clip)
    {
        return clip != null && clip.asset != null && DurationLockedClipAssets.Contains(clip.asset);
    }

    /// <summary>True when every selected clip already has its duration locked.</summary>
    public static bool AreAllDurationLocked(IList<TimelineClip> clips)
    {
        if (clips == null || clips.Count == 0)
        {
            return false;
        }

        bool anyValid = false;
        for (int i = 0; i < clips.Count; i++)
        {
            TimelineClip clip = clips[i];
            if (clip == null || clip.asset == null)
            {
                continue;
            }

            anyValid = true;
            if (!DurationLockedClipAssets.Contains(clip.asset))
            {
                return false;
            }
        }

        return anyValid;
    }

    /// <summary>Locks or unlocks the length of every selected clip.</summary>
    public static bool SetDurationLock(IList<TimelineClip> clips, bool enabled)
    {
        TimelineAsset timeline = TimelineEditor.inspectedAsset;
        if (timeline == null || clips == null || clips.Count == 0)
        {
            return false;
        }

        TimelineClipLockMetadata metadata = GetOrCreateMetadata(timeline);
        if (metadata == null)
        {
            return false;
        }

        Undo.RegisterCompleteObjectUndo(metadata, enabled ? "Lock Clip Duration" : "Unlock Clip Duration");

        bool changed = false;
        for (int i = 0; i < clips.Count; i++)
        {
            TimelineClip clip = clips[i];
            if (clip == null || clip.asset == null)
            {
                continue;
            }

            int existingIndex = FindDurationLockIndex(metadata, clip.asset);
            if (enabled && existingIndex < 0)
            {
                metadata.durationLocks.Add(new TimelineClipDurationLock
                {
                    clipAsset = clip.asset,
                    lockedDuration = Math.Max(clip.duration, MinClipDuration),
                    lastStart = clip.start,
                    lastEnd = clip.end
                });
                changed = true;
            }
            else if (!enabled && existingIndex >= 0)
            {
                metadata.durationLocks.RemoveAt(existingIndex);
                changed = true;
            }
        }

        if (!changed)
        {
            return false;
        }

        EditorUtility.SetDirty(metadata);
        EditorUtility.SetDirty(timeline);
        RebuildStateSets(metadata);
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
        return true;
    }

    private static int FindDurationLockIndex(TimelineClipLockMetadata metadata, UnityEngine.Object clipAsset)
    {
        for (int i = 0; i < metadata.durationLocks.Count; i++)
        {
            if (metadata.durationLocks[i].clipAsset == clipAsset)
            {
                return i;
            }
        }

        return -1;
    }

    public static bool TogglePairLock(IList<TimelineClip> clips)
    {
        return TogglePairLock(clips, TimelineClipLockMode.StartAndEnd);
    }

    public static bool ToggleStartLock(IList<TimelineClip> clips)
    {
        return TogglePairLock(clips, TimelineClipLockMode.StartOnly);
    }

    public static bool ToggleEndLock(IList<TimelineClip> clips)
    {
        return TogglePairLock(clips, TimelineClipLockMode.EndOnly);
    }

    /// <summary>
    /// Adds or removes one lock of the given mode for the selected pair. Modes are stored independently, so a
    /// start lock and an end lock can coexist and be removed one at a time.
    /// </summary>
    public static bool TogglePairLock(IList<TimelineClip> clips, TimelineClipLockMode mode)
    {
        if (!CanPairLock(clips))
        {
            return false;
        }

        TimelineAsset timeline = TimelineEditor.inspectedAsset;
        TimelineClipLockMetadata metadata = GetOrCreateMetadata(timeline);
        if (metadata == null)
        {
            return false;
        }

        TimelineClip first = clips[0];
        TimelineClip second = clips[1];
        int existingIndex = FindPairIndex(metadata, first.asset, second.asset, mode);

        string undoName = GetUndoName(mode);
        UndoExtensions.RegisterCompleteTimeline(timeline, undoName);
        Undo.RegisterCompleteObjectUndo(metadata, undoName);

        if (existingIndex >= 0)
        {
            metadata.lockedPairs.RemoveAt(existingIndex);
        }
        else
        {
            AlignOnLock(first, second, mode);

            TimelineClipLockPair pair = BuildPair(first, second);
            pair.lockMode = mode;
            metadata.lockedPairs.Add(pair);
        }

        EditorUtility.SetDirty(metadata);
        EditorUtility.SetDirty(timeline);
        RebuildStateSets(metadata);
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
        return true;
    }

    public static bool SetRipple(IList<TimelineClip> clips, bool enabled)
    {
        TimelineAsset timeline = TimelineEditor.inspectedAsset;
        if (timeline == null || clips == null || clips.Count == 0)
        {
            return false;
        }

        TimelineClipLockMetadata metadata = GetOrCreateMetadata(timeline);
        if (metadata == null)
        {
            return false;
        }

        Undo.RegisterCompleteObjectUndo(metadata, enabled ? "Enable Clip Ripple" : "Disable Clip Ripple");

        bool changed = false;
        for (int i = 0; i < clips.Count; i++)
        {
            TimelineClip clip = clips[i];
            if (clip == null || clip.asset == null)
            {
                continue;
            }

            int existingIndex = FindRippleIndex(metadata, clip.asset);
            if (enabled && existingIndex < 0)
            {
                metadata.rippleTargets.Add(new TimelineClipRippleTarget
                {
                    clipAsset = clip.asset,
                    lastStart = clip.start,
                    lastEnd = clip.end
                });
                changed = true;
            }
            else if (!enabled && existingIndex >= 0)
            {
                metadata.rippleTargets.RemoveAt(existingIndex);
                changed = true;
            }
        }

        if (!changed)
        {
            return false;
        }

        EditorUtility.SetDirty(metadata);
        EditorUtility.SetDirty(timeline);
        RebuildStateSets(metadata);
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
        return true;
    }

    /// <summary>At least two clips, all on the same track, all with an asset.</summary>
    public static bool CanGroupLock(IList<TimelineClip> clips)
    {
        if (TimelineEditor.inspectedAsset == null || clips == null || clips.Count < 2)
        {
            return false;
        }

        TrackAsset track = null;
        for (int i = 0; i < clips.Count; i++)
        {
            TimelineClip clip = clips[i];
            if (clip == null || clip.asset == null)
            {
                return false;
            }

            TrackAsset clipTrack = clip.GetParentTrack();
            if (clipTrack == null)
            {
                return false;
            }

            if (track == null)
            {
                track = clipTrack;
            }
            else if (track != clipTrack)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>True when every selected clip already belongs to the same locked group.</summary>
    public static bool IsGroupLocked(IList<TimelineClip> clips)
    {
        return FindGroupIndex(clips) >= 0;
    }

    public static bool LockClipGroup(IList<TimelineClip> clips)
    {
        if (!CanGroupLock(clips) || IsGroupLocked(clips))
        {
            return false;
        }

        TimelineAsset timeline = TimelineEditor.inspectedAsset;
        TimelineClipLockMetadata metadata = GetOrCreateMetadata(timeline);
        if (metadata == null)
        {
            return false;
        }

        Undo.RegisterCompleteObjectUndo(metadata, "Lock Clip Group");

        List<TimelineClip> ordered = clips.Where(c => c != null && c.asset != null).OrderBy(c => c.start).ToList();
        TimelineClipLockGroup group = new TimelineClipLockGroup();
        for (int i = 0; i < ordered.Count; i++)
        {
            group.members.Add(new TimelineClipGroupMember
            {
                clipAsset = ordered[i].asset,
                lastStart = ordered[i].start,
                lastEnd = ordered[i].end
            });
        }

        metadata.lockedGroups.Add(group);

        EditorUtility.SetDirty(metadata);
        EditorUtility.SetDirty(timeline);
        RebuildStateSets(metadata);
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
        return true;
    }

    public static bool UnlockClipGroup(IList<TimelineClip> clips)
    {
        int index = FindGroupIndex(clips);
        if (index < 0)
        {
            return false;
        }

        TimelineAsset timeline = TimelineEditor.inspectedAsset;
        TimelineClipLockMetadata metadata = GetCachedMetadata(timeline);
        if (metadata == null)
        {
            return false;
        }

        Undo.RegisterCompleteObjectUndo(metadata, "Unlock Clip Group");
        metadata.lockedGroups.RemoveAt(index);

        EditorUtility.SetDirty(metadata);
        EditorUtility.SetDirty(timeline);
        RebuildStateSets(metadata);
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
        return true;
    }

    /// <summary>Index of the group that contains every selected clip, or -1.</summary>
    private static int FindGroupIndex(IList<TimelineClip> clips)
    {
        if (clips == null || clips.Count == 0)
        {
            return -1;
        }

        TimelineClipLockMetadata metadata = GetCachedMetadata(TimelineEditor.inspectedAsset);
        if (metadata == null)
        {
            return -1;
        }

        for (int g = 0; g < metadata.lockedGroups.Count; g++)
        {
            TimelineClipLockGroup group = metadata.lockedGroups[g];

            bool containsAll = true;
            for (int i = 0; i < clips.Count; i++)
            {
                TimelineClip clip = clips[i];
                if (clip == null || clip.asset == null || !GroupContains(group, clip.asset))
                {
                    containsAll = false;
                    break;
                }
            }

            if (containsAll)
            {
                return g;
            }
        }

        return -1;
    }

    private static bool GroupContains(TimelineClipLockGroup group, UnityEngine.Object clipAsset)
    {
        for (int i = 0; i < group.members.Count; i++)
        {
            if (group.members[i].clipAsset == clipAsset)
            {
                return true;
            }
        }

        return false;
    }

    public static bool CanPairLock(IList<TimelineClip> clips)
    {
        return TimelineEditor.inspectedAsset != null
            && clips != null
            && clips.Count == 2
            && clips[0] != null
            && clips[1] != null
            && clips[0].asset != null
            && clips[1].asset != null
            && clips[0].GetParentTrack() != clips[1].GetParentTrack();
    }

    /// <summary>True when the two selected clips already have a combined start and end lock.</summary>
    public static bool IsPairLocked(IList<TimelineClip> clips)
    {
        return IsPairLocked(clips, TimelineClipLockMode.StartAndEnd);
    }

    /// <summary>True when the two selected clips already have a lock of exactly this mode.</summary>
    public static bool IsPairLocked(IList<TimelineClip> clips, TimelineClipLockMode mode)
    {
        if (!CanPairLock(clips))
        {
            return false;
        }

        TimelineClipLockMetadata metadata = GetCachedMetadata(TimelineEditor.inspectedAsset);
        return metadata != null && FindPairIndex(metadata, clips[0].asset, clips[1].asset, mode) >= 0;
    }

    /// <summary>
    /// A combined lock already syncs both edges, so the separate start and end entries hide while it exists.
    /// </summary>
    public static bool CanUseSeparateLock(IList<TimelineClip> clips)
    {
        return CanPairLock(clips) && !IsPairLocked(clips, TimelineClipLockMode.StartAndEnd);
    }

    /// <summary>True when every selected clip that can ripple already has ripple enabled.</summary>
    public static bool AreAllRippled(IList<TimelineClip> clips)
    {
        if (clips == null || clips.Count == 0)
        {
            return false;
        }

        bool anyValid = false;
        for (int i = 0; i < clips.Count; i++)
        {
            TimelineClip clip = clips[i];
            if (clip == null || clip.asset == null)
            {
                continue;
            }

            anyValid = true;
            if (!RippleClipAssets.Contains(clip.asset))
            {
                return false;
            }
        }

        return anyValid;
    }

    // ---------------------------------------------------------------------------------------------------------
    // Track level entry points
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>
    /// True when the selection is a usable track context. Group tracks are accepted: their clips are gathered
    /// recursively from the child tracks.
    /// </summary>
    public static bool CanUseTracks(IList<TrackAsset> tracks)
    {
        if (TimelineEditor.inspectedAsset == null || tracks == null || tracks.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < tracks.Count; i++)
        {
            if (tracks[i] == null)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Enables or disables ripple on every clip of the given tracks.</summary>
    public static bool SetRippleOnTracks(IList<TrackAsset> tracks, bool enabled)
    {
        return SetRipple(GetClipsOnTracks(tracks), enabled);
    }

    /// <summary>True when every clip of the given tracks already ripples.</summary>
    public static bool AreTracksFullyRippled(IList<TrackAsset> tracks)
    {
        return AreAllRippled(GetClipsOnTracks(tracks));
    }

    public static bool TracksHaveClips(IList<TrackAsset> tracks)
    {
        return GetClipsOnTracks(tracks).Count > 0;
    }

    public static bool CanTrackPairLock(IList<TrackAsset> tracks)
    {
        return CanUseTracks(tracks) && tracks.Count == 2 && tracks[0] != tracks[1];
    }

    public static bool HasUnlockedMatchingPairs(IList<TrackAsset> tracks)
    {
        return CanTrackPairLock(tracks) && GetMatchingUnlockedPairs(tracks).Count > 0;
    }

    public static bool HasLockedPairsBetweenTracks(IList<TrackAsset> tracks)
    {
        return CanTrackPairLock(tracks) && GetLockedPairIndicesBetweenTracks(tracks).Count > 0;
    }

    /// <summary>
    /// Locks the clip pairs of two tracks whose start and end already match within one half frame, so nothing
    /// is moved and no unclear pairing is created.
    /// </summary>
    public static bool LockMatchingClipEnds(IList<TrackAsset> tracks)
    {
        if (!CanTrackPairLock(tracks))
        {
            return false;
        }

        List<KeyValuePair<TimelineClip, TimelineClip>> matches = GetMatchingUnlockedPairs(tracks);
        if (matches.Count == 0)
        {
            Debug.LogWarning("[TimelineClipLockTool] No clips with matching start and end were found between the two selected tracks, so nothing was locked.");
            return false;
        }

        TimelineAsset timeline = TimelineEditor.inspectedAsset;
        TimelineClipLockMetadata metadata = GetOrCreateMetadata(timeline);
        if (metadata == null)
        {
            return false;
        }

        Undo.RegisterCompleteObjectUndo(metadata, "Lock Matching Clip Ends");

        foreach (KeyValuePair<TimelineClip, TimelineClip> match in matches)
        {
            AddPair(metadata, match.Key, match.Value);
        }

        EditorUtility.SetDirty(metadata);
        EditorUtility.SetDirty(timeline);
        RebuildStateSets(metadata);
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
        Debug.Log($"[TimelineClipLockTool] Locked {matches.Count} matching clip pair(s) between '{tracks[0].name}' and '{tracks[1].name}'.", timeline);
        return true;
    }

    /// <summary>Removes every lock whose two clips sit on the two selected tracks.</summary>
    public static bool UnlockClipsBetweenTracks(IList<TrackAsset> tracks)
    {
        if (!CanTrackPairLock(tracks))
        {
            return false;
        }

        List<int> indices = GetLockedPairIndicesBetweenTracks(tracks);
        if (indices.Count == 0)
        {
            return false;
        }

        TimelineAsset timeline = TimelineEditor.inspectedAsset;
        TimelineClipLockMetadata metadata = GetCachedMetadata(timeline);
        if (metadata == null)
        {
            return false;
        }

        Undo.RegisterCompleteObjectUndo(metadata, "Unlock Matching Clip Ends");

        indices.Sort();
        for (int i = indices.Count - 1; i >= 0; i--)
        {
            metadata.lockedPairs.RemoveAt(indices[i]);
        }

        EditorUtility.SetDirty(metadata);
        EditorUtility.SetDirty(timeline);
        RebuildStateSets(metadata);
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
        return true;
    }

    private static IList<TimelineClip> GetClipsOnTracks(IList<TrackAsset> tracks)
    {
        List<TimelineClip> clips = new List<TimelineClip>();
        if (!CanUseTracks(tracks))
        {
            return clips;
        }

        for (int i = 0; i < tracks.Count; i++)
        {
            CollectClips(tracks[i], clips);
        }

        return clips;
    }

    private static List<TimelineClip> GetTrackClips(TrackAsset track)
    {
        List<TimelineClip> clips = new List<TimelineClip>();
        CollectClips(track, clips);
        return clips;
    }

    private static void CollectClips(TrackAsset track, List<TimelineClip> clips)
    {
        if (track == null)
        {
            return;
        }

        foreach (TimelineClip clip in track.GetClips())
        {
            if (clip != null && clip.asset != null)
            {
                clips.Add(clip);
            }
        }

        foreach (TrackAsset child in track.GetChildTracks())
        {
            CollectClips(child, clips);
        }
    }

    private static List<KeyValuePair<TimelineClip, TimelineClip>> GetMatchingUnlockedPairs(IList<TrackAsset> tracks)
    {
        List<KeyValuePair<TimelineClip, TimelineClip>> matches = new List<KeyValuePair<TimelineClip, TimelineClip>>();

        TimelineAsset timeline = TimelineEditor.inspectedAsset;
        TimelineClipLockMetadata metadata = GetCachedMetadata(timeline);
        double tolerance = GetFrameTolerance(timeline);

        List<TimelineClip> secondClips = GetTrackClips(tracks[1]);

        foreach (TimelineClip first in GetTrackClips(tracks[0]))
        {
            for (int i = 0; i < secondClips.Count; i++)
            {
                TimelineClip second = secondClips[i];
                if (Math.Abs(first.start - second.start) > tolerance || Math.Abs(first.end - second.end) > tolerance)
                {
                    continue;
                }

                if (metadata == null || FindPairIndex(metadata, first.asset, second.asset) < 0)
                {
                    matches.Add(new KeyValuePair<TimelineClip, TimelineClip>(first, second));
                }

                secondClips.RemoveAt(i);
                break;
            }
        }

        return matches;
    }

    private static List<int> GetLockedPairIndicesBetweenTracks(IList<TrackAsset> tracks)
    {
        List<int> indices = new List<int>();

        TimelineClipLockMetadata metadata = GetCachedMetadata(TimelineEditor.inspectedAsset);
        if (metadata == null)
        {
            return indices;
        }

        HashSet<UnityEngine.Object> firstAssets = GetTrackClipAssets(tracks[0]);
        HashSet<UnityEngine.Object> secondAssets = GetTrackClipAssets(tracks[1]);

        for (int i = 0; i < metadata.lockedPairs.Count; i++)
        {
            TimelineClipLockPair pair = metadata.lockedPairs[i];
            bool sameOrder = firstAssets.Contains(pair.firstClipAsset) && secondAssets.Contains(pair.secondClipAsset);
            bool reverseOrder = secondAssets.Contains(pair.firstClipAsset) && firstAssets.Contains(pair.secondClipAsset);
            if (sameOrder || reverseOrder)
            {
                indices.Add(i);
            }
        }

        return indices;
    }

    private static HashSet<UnityEngine.Object> GetTrackClipAssets(TrackAsset track)
    {
        HashSet<UnityEngine.Object> assets = new HashSet<UnityEngine.Object>();
        foreach (TimelineClip clip in GetTrackClips(track))
        {
            assets.Add(clip.asset);
        }
        return assets;
    }

    private static double GetFrameTolerance(TimelineAsset timeline)
    {
        double frameRate = timeline != null ? timeline.editorSettings.frameRate : 60.0;
        if (frameRate <= 0.0)
        {
            frameRate = 60.0;
        }

        return 0.5 / frameRate;
    }

    private static void AddPair(TimelineClipLockMetadata metadata, TimelineClip first, TimelineClip second)
    {
        TimelineClipLockPair pair = BuildPair(first, second);
        pair.lockMode = TimelineClipLockMode.StartAndEnd;
        metadata.lockedPairs.Add(pair);
    }

    private static void UpdateInspectedTimeline()
    {
        if (isSyncing || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        TimelineAsset timeline = TimelineEditor.inspectedAsset;
        if (timeline == null)
        {
            LockedClipAssets.Clear();
            RippleClipAssets.Clear();
            GroupedClipAssets.Clear();
            DurationLockedClipAssets.Clear();
            return;
        }

        TimelineClipLockMetadata metadata = GetCachedMetadata(timeline);
        RebuildStateSets(metadata);

        if (metadata == null)
        {
            skipNextSync = false;
            return;
        }

        if (skipNextSync)
        {
            skipNextSync = false;
            RebaselineMetadata(timeline, metadata);
            return;
        }

        Dictionary<UnityEngine.Object, TimelineClip> clipsByAsset = GetClipsByAsset(timeline);
        bool changed = false;
        bool undoRegistered = false;

        // Captured before any undo registration so the sync can be folded back into the edit that triggered it,
        // which makes a single Ctrl+Z revert the drag together with the clips it pushed.
        int undoGroup = Undo.GetCurrentGroup();

        isSyncing = true;
        try
        {
            // Duration correction first so the later passes see the clip at its intended length, then groups so a
            // grouped clip moves as one block before ripple pushes unrelated later clips.
            changed |= ApplyDurationLocks(timeline, metadata, clipsByAsset, ref undoRegistered);
            changed |= ApplyLockedGroups(timeline, metadata, clipsByAsset, ref undoRegistered);
            changed |= ApplyLockedPairs(timeline, metadata, clipsByAsset, ref undoRegistered);
            changed |= ApplyRippleTargets(timeline, metadata, clipsByAsset, ref undoRegistered);
        }
        finally
        {
            isSyncing = false;
        }

        if (changed)
        {
            if (undoRegistered)
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            // Clips this pass moved are not user edits, so their baselines are refreshed here; without this a
            // rippled clip would look edited on the next update and start a cascade of further ripples.
            RebaselineMetadata(timeline, metadata);

            EditorUtility.SetDirty(metadata);
            EditorUtility.SetDirty(timeline);
            TimelineEditor.Refresh(RefreshReason.ContentsModified);
        }
    }

    /// <summary>
    /// Holds a locked clip at its stored length. Dragging the start therefore carries the end along instead of
    /// shrinking the clip, and dragging the end snaps the length back.
    /// </summary>
    private static bool ApplyDurationLocks(
        TimelineAsset timeline,
        TimelineClipLockMetadata metadata,
        Dictionary<UnityEngine.Object, TimelineClip> clipsByAsset,
        ref bool undoRegistered)
    {
        bool changed = false;

        for (int i = metadata.durationLocks.Count - 1; i >= 0; i--)
        {
            TimelineClipDurationLock durationLock = metadata.durationLocks[i];
            TimelineClip clip = ResolveClip(clipsByAsset, durationLock.clipAsset);

            if (clip == null)
            {
                RegisterSyncUndo(timeline, metadata, ref undoRegistered);
                metadata.durationLocks.RemoveAt(i);
                changed = true;
                continue;
            }

            double target = Math.Max(durationLock.lockedDuration, MinClipDuration);
            if (Math.Abs(clip.duration - target) <= 0.000001)
            {
                durationLock.lastStart = clip.start;
                durationLock.lastEnd = clip.end;
                continue;
            }

            RegisterSyncUndo(timeline, metadata, ref undoRegistered);
            clip.duration = target;
            durationLock.lastStart = clip.start;
            durationLock.lastEnd = clip.end;
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Moves the clips of a same track group so the block keeps its shape. A start move shifts the whole group,
    /// an end resize shifts the clips after the edited one, and a start resize shifts the clips before it.
    /// Only the clip the user edited changes duration.
    /// </summary>
    private static bool ApplyLockedGroups(
        TimelineAsset timeline,
        TimelineClipLockMetadata metadata,
        Dictionary<UnityEngine.Object, TimelineClip> clipsByAsset,
        ref bool undoRegistered)
    {
        bool changed = false;

        for (int g = metadata.lockedGroups.Count - 1; g >= 0; g--)
        {
            TimelineClipLockGroup group = metadata.lockedGroups[g];

            List<TimelineClip> clips = new List<TimelineClip>(group.members.Count);
            bool broken = false;
            for (int i = 0; i < group.members.Count; i++)
            {
                TimelineClip clip = ResolveClip(clipsByAsset, group.members[i].clipAsset);
                if (clip == null)
                {
                    broken = true;
                    break;
                }

                clips.Add(clip);
            }

            if (broken || clips.Count < 2)
            {
                RegisterSyncUndo(timeline, metadata, ref undoRegistered);
                metadata.lockedGroups.RemoveAt(g);
                changed = true;
                continue;
            }

            int editedIndex = -1;
            double startDelta = 0d;
            double endDelta = 0d;

            for (int i = 0; i < clips.Count; i++)
            {
                double clipStartDelta = clips[i].start - group.members[i].lastStart;
                double clipEndDelta = clips[i].end - group.members[i].lastEnd;
                if (Math.Abs(clipStartDelta) > 0.000001 || Math.Abs(clipEndDelta) > 0.000001)
                {
                    editedIndex = i;
                    startDelta = clipStartDelta;
                    endDelta = clipEndDelta;
                    break;
                }
            }

            if (editedIndex < 0)
            {
                continue;
            }

            bool movedWholeClip = Math.Abs(startDelta) > 0.000001
                && Math.Abs(endDelta - startDelta) <= 0.000001;

            if (movedWholeClip)
            {
                RegisterSyncUndo(timeline, metadata, ref undoRegistered);
                for (int i = 0; i < clips.Count; i++)
                {
                    if (i != editedIndex)
                    {
                        clips[i].start += startDelta;
                    }
                }

                changed = true;
                continue;
            }

            if (Math.Abs(endDelta) > 0.000001)
            {
                // End resize: everything after the edited clip follows, so the gaps stay the same.
                RegisterSyncUndo(timeline, metadata, ref undoRegistered);
                for (int i = editedIndex + 1; i < clips.Count; i++)
                {
                    clips[i].start += endDelta;
                }

                changed = true;
            }

            if (Math.Abs(startDelta) > 0.000001)
            {
                // Start resize: everything before the edited clip follows.
                RegisterSyncUndo(timeline, metadata, ref undoRegistered);
                for (int i = 0; i < editedIndex; i++)
                {
                    clips[i].start += startDelta;
                }

                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Copies the current clip positions into the metadata without touching the Timeline, so the watcher treats
    /// the restored state as the new baseline instead of a user edit that needs syncing.
    /// </summary>
    private static void RebaselineMetadata(TimelineAsset timeline, TimelineClipLockMetadata metadata)
    {
        Dictionary<UnityEngine.Object, TimelineClip> clipsByAsset = GetClipsByAsset(timeline);

        foreach (TimelineClipLockGroup group in metadata.lockedGroups)
        {
            foreach (TimelineClipGroupMember member in group.members)
            {
                TimelineClip clip = ResolveClip(clipsByAsset, member.clipAsset);
                if (clip != null)
                {
                    member.lastStart = clip.start;
                    member.lastEnd = clip.end;
                }
            }
        }

        foreach (TimelineClipDurationLock durationLock in metadata.durationLocks)
        {
            TimelineClip clip = ResolveClip(clipsByAsset, durationLock.clipAsset);
            if (clip != null)
            {
                durationLock.lastStart = clip.start;
                durationLock.lastEnd = clip.end;
            }
        }

        foreach (TimelineClipLockPair pair in metadata.lockedPairs)
        {
            TimelineClip first = ResolveClip(clipsByAsset, pair.firstClipAsset);
            if (first != null)
            {
                pair.lastFirstStart = first.start;
                pair.lastFirstEnd = first.end;
            }

            TimelineClip second = ResolveClip(clipsByAsset, pair.secondClipAsset);
            if (second != null)
            {
                pair.lastSecondStart = second.start;
                pair.lastSecondEnd = second.end;
            }
        }

        foreach (TimelineClipRippleTarget target in metadata.rippleTargets)
        {
            TimelineClip clip = ResolveClip(clipsByAsset, target.clipAsset);
            if (clip != null)
            {
                target.lastStart = clip.start;
                target.lastEnd = clip.end;
            }
        }

        EditorUtility.SetDirty(metadata);
    }

    private static bool ApplyLockedPairs(
        TimelineAsset timeline,
        TimelineClipLockMetadata metadata,
        Dictionary<UnityEngine.Object, TimelineClip> clipsByAsset,
        ref bool undoRegistered)
    {
        bool changed = false;
        for (int i = metadata.lockedPairs.Count - 1; i >= 0; i--)
        {
            TimelineClipLockPair pair = metadata.lockedPairs[i];
            TimelineClip first = ResolveClip(clipsByAsset, pair.firstClipAsset);
            TimelineClip second = ResolveClip(clipsByAsset, pair.secondClipAsset);

            if (first == null || second == null)
            {
                RegisterSyncUndo(timeline, metadata, ref undoRegistered);
                metadata.lockedPairs.RemoveAt(i);
                changed = true;
                continue;
            }

            // Only the edge this lock covers counts as a change, so resizing the end of a start locked clip
            // leaves its partner alone.
            bool firstChanged = HasRelevantChange(pair.lockMode, first, pair.lastFirstStart, pair.lastFirstEnd);
            bool secondChanged = HasRelevantChange(pair.lockMode, second, pair.lastSecondStart, pair.lastSecondEnd);

            if (firstChanged || secondChanged)
            {
                // When both moved in the same pass the first clip stays the reference, as before.
                TimelineClip source = firstChanged ? first : second;
                TimelineClip follower = firstChanged ? second : first;

                RegisterSyncUndo(timeline, metadata, ref undoRegistered);
                ApplyLock(follower, source, pair.lockMode);
                changed = true;
            }

            pair.lastFirstStart = first.start;
            pair.lastFirstEnd = first.end;
            pair.lastSecondStart = second.start;
            pair.lastSecondEnd = second.end;
        }

        return changed;
    }

    private static bool ApplyRippleTargets(
        TimelineAsset timeline,
        TimelineClipLockMetadata metadata,
        Dictionary<UnityEngine.Object, TimelineClip> clipsByAsset,
        ref bool undoRegistered)
    {
        bool changed = false;
        for (int i = metadata.rippleTargets.Count - 1; i >= 0; i--)
        {
            TimelineClipRippleTarget target = metadata.rippleTargets[i];
            TimelineClip clip = ResolveClip(clipsByAsset, target.clipAsset);

            if (clip == null)
            {
                RegisterSyncUndo(timeline, metadata, ref undoRegistered);
                metadata.rippleTargets.RemoveAt(i);
                changed = true;
                continue;
            }

            double endDelta = clip.end - target.lastEnd;
            if (Math.Abs(endDelta) > 0.000001)
            {
                RegisterSyncUndo(timeline, metadata, ref undoRegistered);
                MoveLaterClips(timeline, clip, endDelta);
                changed = true;
            }

            target.lastStart = clip.start;
            target.lastEnd = clip.end;
        }

        return changed;
    }

    private static void RegisterSyncUndo(TimelineAsset timeline, TimelineClipLockMetadata metadata, ref bool registered)
    {
        if (registered)
        {
            return;
        }

        // The moved clips live on the tracks, not on the TimelineAsset, so the whole timeline has to be recorded.
        UndoExtensions.RegisterCompleteTimeline(timeline, "Sync Timeline Clips");
        Undo.RegisterCompleteObjectUndo(metadata, "Sync Timeline Clips");
        registered = true;
    }

    /// <summary>
    /// Shifts every clip that starts at or after the source clip's previous end by the signed delta, so growing
    /// the source pushes them forward and shrinking it pulls them back by the same amount. Only start times move,
    /// durations are left alone.
    /// </summary>
    private static void MoveLaterClips(TimelineAsset timeline, TimelineClip sourceClip, double delta)
    {
        double previousEnd = sourceClip.end - delta;
        List<TimelineClip> movingClips = new List<TimelineClip>();
        double earliestStart = double.MaxValue;

        foreach (TrackAsset track in GetAllTracks(timeline))
        {
            foreach (TimelineClip clip in track.GetClips())
            {
                if (clip == sourceClip || clip.asset == null)
                {
                    continue;
                }

                if (clip.start < previousEnd - 0.000001)
                {
                    continue;
                }

                movingClips.Add(clip);
                earliestStart = Math.Min(earliestStart, clip.start);
            }
        }

        if (movingClips.Count == 0)
        {
            return;
        }

        // Pulling clips back must never move one before the start of the Timeline.
        if (delta < 0 && earliestStart + delta < 0)
        {
            delta = -earliestStart;
        }

        if (Math.Abs(delta) < 0.000001)
        {
            return;
        }

        for (int i = 0; i < movingClips.Count; i++)
        {
            movingClips[i].start += delta;
        }
    }

    private static void SyncClipStartAndEnd(TimelineClip clip, double start, double end)
    {
        double duration = Math.Max(end - start, MinClipDuration);
        clip.start = start;
        clip.duration = duration;
    }

    private static bool HasRelevantChange(TimelineClipLockMode mode, TimelineClip clip, double lastStart, double lastEnd)
    {
        switch (mode)
        {
            case TimelineClipLockMode.StartOnly:
                return Math.Abs(clip.start - lastStart) > 0.000001;
            case TimelineClipLockMode.EndOnly:
                return Math.Abs(clip.end - lastEnd) > 0.000001;
            default:
                return Math.Abs(clip.start - lastStart) > 0.000001
                    || Math.Abs(clip.end - lastEnd) > 0.000001;
        }
    }

    private static void ApplyLock(TimelineClip follower, TimelineClip source, TimelineClipLockMode mode)
    {
        switch (mode)
        {
            case TimelineClipLockMode.StartOnly:
                // Moves the clip, keeping its length.
                follower.start = source.start;
                break;
            case TimelineClipLockMode.EndOnly:
                // Resizes the clip, keeping where it begins.
                follower.duration = Math.Max(source.end - follower.start, MinClipDuration);
                break;
            default:
                SyncClipStartAndEnd(follower, source.start, source.end);
                break;
        }
    }

    private static void AlignOnLock(TimelineClip first, TimelineClip second, TimelineClipLockMode mode)
    {
        ApplyLock(second, first, mode);
    }

    private static TimelineClipLockPair BuildPair(TimelineClip first, TimelineClip second)
    {
        return new TimelineClipLockPair
        {
            firstClipAsset = first.asset,
            secondClipAsset = second.asset,
            lastFirstStart = first.start,
            lastFirstEnd = first.end,
            lastSecondStart = second.start,
            lastSecondEnd = second.end
        };
    }

    private static string GetUndoName(TimelineClipLockMode mode)
    {
        switch (mode)
        {
            case TimelineClipLockMode.StartOnly:
                return "Toggle Clip Start Lock";
            case TimelineClipLockMode.EndOnly:
                return "Toggle Clip End Lock";
            default:
                return "Toggle Clip Start End Lock";
        }
    }

    private static TimelineClipLockMetadata GetOrCreateMetadata(TimelineAsset timeline)
    {
        if (timeline == null)
        {
            return null;
        }

        TimelineClipLockMetadata metadata = FindMetadata(timeline);
        if (metadata != null)
        {
            return metadata;
        }

        metadata = ScriptableObject.CreateInstance<TimelineClipLockMetadata>();
        metadata.name = MetadataName;
        metadata.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
        AssetDatabase.AddObjectToAsset(metadata, timeline);
        EditorUtility.SetDirty(timeline);
        InvalidateCache();
        return metadata;
    }

    /// <summary>
    /// FindMetadata hits the AssetDatabase, which is far too slow for the per repaint state queries, so the
    /// lookup is cached until the open Timeline changes or a toggle invalidates it.
    /// </summary>
    private static TimelineClipLockMetadata GetCachedMetadata(TimelineAsset timeline)
    {
        if (timeline != cachedTimeline || cachedMetadata == null)
        {
            cachedTimeline = timeline;
            cachedMetadata = FindMetadata(timeline);
        }

        return cachedMetadata;
    }

    private static void RebuildStateSets(TimelineClipLockMetadata metadata)
    {
        LockedClipAssets.Clear();
        RippleClipAssets.Clear();
        GroupedClipAssets.Clear();
        DurationLockedClipAssets.Clear();

        if (metadata == null)
        {
            return;
        }

        foreach (TimelineClipDurationLock durationLock in metadata.durationLocks)
        {
            if (durationLock.clipAsset != null) DurationLockedClipAssets.Add(durationLock.clipAsset);
        }

        foreach (TimelineClipLockGroup group in metadata.lockedGroups)
        {
            foreach (TimelineClipGroupMember member in group.members)
            {
                if (member.clipAsset != null) GroupedClipAssets.Add(member.clipAsset);
            }
        }

        foreach (TimelineClipLockPair pair in metadata.lockedPairs)
        {
            if (pair.firstClipAsset != null) LockedClipAssets.Add(pair.firstClipAsset);
            if (pair.secondClipAsset != null) LockedClipAssets.Add(pair.secondClipAsset);
        }

        foreach (TimelineClipRippleTarget target in metadata.rippleTargets)
        {
            if (target.clipAsset != null) RippleClipAssets.Add(target.clipAsset);
        }
    }

    private static TimelineClipLockMetadata FindMetadata(TimelineAsset timeline)
    {
        if (timeline == null)
        {
            return null;
        }

        string path = AssetDatabase.GetAssetPath(timeline);
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            TimelineClipLockMetadata metadata = assets[i] as TimelineClipLockMetadata;
            if (metadata != null)
            {
                return metadata;
            }
        }

        return null;
    }

    private static Dictionary<UnityEngine.Object, TimelineClip> GetClipsByAsset(TimelineAsset timeline)
    {
        Dictionary<UnityEngine.Object, TimelineClip> clips = new Dictionary<UnityEngine.Object, TimelineClip>();
        foreach (TrackAsset track in GetAllTracks(timeline))
        {
            foreach (TimelineClip clip in track.GetClips())
            {
                if (clip.asset != null && !clips.ContainsKey(clip.asset))
                {
                    clips.Add(clip.asset, clip);
                }
            }
        }

        return clips;
    }

    private static IEnumerable<TrackAsset> GetAllTracks(TimelineAsset timeline)
    {
        foreach (TrackAsset track in timeline.GetRootTracks())
        {
            foreach (TrackAsset child in FlattenTrack(track))
            {
                yield return child;
            }
        }
    }

    private static IEnumerable<TrackAsset> FlattenTrack(TrackAsset track)
    {
        yield return track;
        foreach (TrackAsset child in track.GetChildTracks())
        {
            foreach (TrackAsset nested in FlattenTrack(child))
            {
                yield return nested;
            }
        }
    }

    private static TimelineClip ResolveClip(Dictionary<UnityEngine.Object, TimelineClip> clipsByAsset, UnityEngine.Object asset)
    {
        if (asset == null)
        {
            return null;
        }

        TimelineClip clip;
        return clipsByAsset.TryGetValue(asset, out clip) ? clip : null;
    }

    /// <summary>Finds a lock between the two clips regardless of its mode.</summary>
    private static int FindPairIndex(TimelineClipLockMetadata metadata, UnityEngine.Object firstAsset, UnityEngine.Object secondAsset)
    {
        return FindPairIndex(metadata, firstAsset, secondAsset, null);
    }

    private static int FindPairIndex(TimelineClipLockMetadata metadata, UnityEngine.Object firstAsset, UnityEngine.Object secondAsset, TimelineClipLockMode? mode)
    {
        for (int i = 0; i < metadata.lockedPairs.Count; i++)
        {
            TimelineClipLockPair pair = metadata.lockedPairs[i];
            if (mode.HasValue && pair.lockMode != mode.Value)
            {
                continue;
            }

            bool sameOrder = pair.firstClipAsset == firstAsset && pair.secondClipAsset == secondAsset;
            bool reverseOrder = pair.firstClipAsset == secondAsset && pair.secondClipAsset == firstAsset;
            if (sameOrder || reverseOrder)
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindRippleIndex(TimelineClipLockMetadata metadata, UnityEngine.Object clipAsset)
    {
        for (int i = 0; i < metadata.rippleTargets.Count; i++)
        {
            if (metadata.rippleTargets[i].clipAsset == clipAsset)
            {
                return i;
            }
        }

        return -1;
    }
}

// Timeline's checked menu state goes through the internal IMenuChecked interface, which project code cannot
// implement, so each toggle is split into two entries and only the one matching the current state is shown.

[MenuEntry("Lock Start & End Together", MenuPriority.TimelineActionSection.delete + 10)]
public sealed class TimelineLockClipStartEndAction : ClipAction
{
    public override ActionValidity Validate(IEnumerable<TimelineClip> clips)
    {
        IList<TimelineClip> selected = clips.ToList();
        return TimelineClipLockTool.CanPairLock(selected) && !TimelineClipLockTool.IsPairLocked(selected)
            ? ActionValidity.Valid
            : ActionValidity.NotApplicable;
    }

    public override bool Execute(IEnumerable<TimelineClip> clips)
    {
        return TimelineClipLockTool.TogglePairLock(clips.ToList());
    }
}

[MenuEntry("Unlock Start & End", MenuPriority.TimelineActionSection.delete + 10)]
public sealed class TimelineUnlockClipStartEndAction : ClipAction
{
    public override ActionValidity Validate(IEnumerable<TimelineClip> clips)
    {
        return TimelineClipLockTool.IsPairLocked(clips.ToList())
            ? ActionValidity.Valid
            : ActionValidity.NotApplicable;
    }

    public override bool Execute(IEnumerable<TimelineClip> clips)
    {
        return TimelineClipLockTool.TogglePairLock(clips.ToList());
    }
}

[MenuEntry("Lock Starts Together", MenuPriority.TimelineActionSection.delete + 11)]
public sealed class TimelineLockClipStartsAction : ClipAction
{
    public override ActionValidity Validate(IEnumerable<TimelineClip> clips)
    {
        IList<TimelineClip> selected = clips.ToList();
        return TimelineClipLockTool.CanUseSeparateLock(selected)
            && !TimelineClipLockTool.IsPairLocked(selected, TimelineClipLockMode.StartOnly)
            ? ActionValidity.Valid
            : ActionValidity.NotApplicable;
    }

    public override bool Execute(IEnumerable<TimelineClip> clips)
    {
        return TimelineClipLockTool.ToggleStartLock(clips.ToList());
    }
}

[MenuEntry("Unlock Starts", MenuPriority.TimelineActionSection.delete + 12)]
public sealed class TimelineUnlockClipStartsAction : ClipAction
{
    public override ActionValidity Validate(IEnumerable<TimelineClip> clips)
    {
        return TimelineClipLockTool.IsPairLocked(clips.ToList(), TimelineClipLockMode.StartOnly)
            ? ActionValidity.Valid
            : ActionValidity.NotApplicable;
    }

    public override bool Execute(IEnumerable<TimelineClip> clips)
    {
        return TimelineClipLockTool.ToggleStartLock(clips.ToList());
    }
}

[MenuEntry("Lock Ends Together", MenuPriority.TimelineActionSection.delete + 13)]
public sealed class TimelineLockClipEndsTogetherAction : ClipAction
{
    public override ActionValidity Validate(IEnumerable<TimelineClip> clips)
    {
        IList<TimelineClip> selected = clips.ToList();
        return TimelineClipLockTool.CanUseSeparateLock(selected)
            && !TimelineClipLockTool.IsPairLocked(selected, TimelineClipLockMode.EndOnly)
            ? ActionValidity.Valid
            : ActionValidity.NotApplicable;
    }

    public override bool Execute(IEnumerable<TimelineClip> clips)
    {
        return TimelineClipLockTool.ToggleEndLock(clips.ToList());
    }
}

[MenuEntry("Unlock Ends", MenuPriority.TimelineActionSection.delete + 14)]
public sealed class TimelineUnlockClipEndsAction : ClipAction
{
    public override ActionValidity Validate(IEnumerable<TimelineClip> clips)
    {
        return TimelineClipLockTool.IsPairLocked(clips.ToList(), TimelineClipLockMode.EndOnly)
            ? ActionValidity.Valid
            : ActionValidity.NotApplicable;
    }

    public override bool Execute(IEnumerable<TimelineClip> clips)
    {
        return TimelineClipLockTool.ToggleEndLock(clips.ToList());
    }
}

[MenuEntry("Lock Clips As One Group", MenuPriority.TimelineActionSection.delete + 15)]
public sealed class TimelineLockClipGroupAction : ClipAction
{
    public override ActionValidity Validate(IEnumerable<TimelineClip> clips)
    {
        IList<TimelineClip> selected = clips.ToList();
        return TimelineClipLockTool.CanGroupLock(selected) && !TimelineClipLockTool.IsGroupLocked(selected)
            ? ActionValidity.Valid
            : ActionValidity.NotApplicable;
    }

    public override bool Execute(IEnumerable<TimelineClip> clips)
    {
        return TimelineClipLockTool.LockClipGroup(clips.ToList());
    }
}

[MenuEntry("Unlock Clip Group", MenuPriority.TimelineActionSection.delete + 16)]
public sealed class TimelineUnlockClipGroupAction : ClipAction
{
    public override ActionValidity Validate(IEnumerable<TimelineClip> clips)
    {
        return TimelineClipLockTool.IsGroupLocked(clips.ToList())
            ? ActionValidity.Valid
            : ActionValidity.NotApplicable;
    }

    public override bool Execute(IEnumerable<TimelineClip> clips)
    {
        return TimelineClipLockTool.UnlockClipGroup(clips.ToList());
    }
}

[MenuEntry("Lock Clip Duration", MenuPriority.TimelineActionSection.delete + 17)]
public sealed class TimelineLockClipDurationAction : ClipAction
{
    public override ActionValidity Validate(IEnumerable<TimelineClip> clips)
    {
        IList<TimelineClip> selected = clips.ToList();
        return TimelineEditor.inspectedAsset != null
            && selected.Any(c => c != null && c.asset != null)
            && !TimelineClipLockTool.AreAllDurationLocked(selected)
            ? ActionValidity.Valid
            : ActionValidity.NotApplicable;
    }

    public override bool Execute(IEnumerable<TimelineClip> clips)
    {
        return TimelineClipLockTool.SetDurationLock(clips.ToList(), true);
    }
}

[MenuEntry("Unlock Clip Duration", MenuPriority.TimelineActionSection.delete + 18)]
public sealed class TimelineUnlockClipDurationAction : ClipAction
{
    public override ActionValidity Validate(IEnumerable<TimelineClip> clips)
    {
        return TimelineEditor.inspectedAsset != null && TimelineClipLockTool.AreAllDurationLocked(clips.ToList())
            ? ActionValidity.Valid
            : ActionValidity.NotApplicable;
    }

    public override bool Execute(IEnumerable<TimelineClip> clips)
    {
        return TimelineClipLockTool.SetDurationLock(clips.ToList(), false);
    }
}

[MenuEntry("Ripple Later Clips", MenuPriority.TimelineActionSection.delete + 20)]
public sealed class TimelineEnableRippleAfterClipAction : ClipAction
{
    public override ActionValidity Validate(IEnumerable<TimelineClip> clips)
    {
        IList<TimelineClip> selected = clips.ToList();
        return TimelineEditor.inspectedAsset != null
            && selected.Any(c => c != null && c.asset != null)
            && !TimelineClipLockTool.AreAllRippled(selected)
            ? ActionValidity.Valid
            : ActionValidity.NotApplicable;
    }

    public override bool Execute(IEnumerable<TimelineClip> clips)
    {
        return TimelineClipLockTool.SetRipple(clips.ToList(), true);
    }
}

[MenuEntry("Stop Rippling Later Clips", MenuPriority.TimelineActionSection.delete + 20)]
public sealed class TimelineDisableRippleAfterClipAction : ClipAction
{
    public override ActionValidity Validate(IEnumerable<TimelineClip> clips)
    {
        return TimelineEditor.inspectedAsset != null && TimelineClipLockTool.AreAllRippled(clips.ToList())
            ? ActionValidity.Valid
            : ActionValidity.NotApplicable;
    }

    public override bool Execute(IEnumerable<TimelineClip> clips)
    {
        return TimelineClipLockTool.SetRipple(clips.ToList(), false);
    }
}

// Track entries stay visible for any recognized track context and fall back to Invalid, which greys the item out,
// so the user always sees them instead of the menu silently dropping them.

[MenuEntry("Lock Matching Clips", MenuPriority.TimelineActionSection.delete + 30)]
public sealed class TimelineLockMatchingClipEndsAction : TrackAction
{
    public override ActionValidity Validate(IEnumerable<TrackAsset> tracks)
    {
        IList<TrackAsset> selected = tracks.ToList();
        if (!TimelineClipLockTool.CanUseTracks(selected))
        {
            return ActionValidity.NotApplicable;
        }

        return TimelineClipLockTool.HasUnlockedMatchingPairs(selected) ? ActionValidity.Valid : ActionValidity.Invalid;
    }

    public override bool Execute(IEnumerable<TrackAsset> tracks)
    {
        return TimelineClipLockTool.LockMatchingClipEnds(tracks.ToList());
    }
}

[MenuEntry("Unlock Matching Clips", MenuPriority.TimelineActionSection.delete + 31)]
public sealed class TimelineUnlockMatchingClipEndsAction : TrackAction
{
    public override ActionValidity Validate(IEnumerable<TrackAsset> tracks)
    {
        IList<TrackAsset> selected = tracks.ToList();
        if (!TimelineClipLockTool.CanUseTracks(selected))
        {
            return ActionValidity.NotApplicable;
        }

        return TimelineClipLockTool.HasLockedPairsBetweenTracks(selected) ? ActionValidity.Valid : ActionValidity.Invalid;
    }

    public override bool Execute(IEnumerable<TrackAsset> tracks)
    {
        return TimelineClipLockTool.UnlockClipsBetweenTracks(tracks.ToList());
    }
}

[MenuEntry("Ripple Clips On Track", MenuPriority.TimelineActionSection.delete + 32)]
public sealed class TimelineEnableTrackRippleAction : TrackAction
{
    public override ActionValidity Validate(IEnumerable<TrackAsset> tracks)
    {
        IList<TrackAsset> selected = tracks.ToList();
        if (!TimelineClipLockTool.CanUseTracks(selected))
        {
            return ActionValidity.NotApplicable;
        }

        return TimelineClipLockTool.TracksHaveClips(selected) && !TimelineClipLockTool.AreTracksFullyRippled(selected)
            ? ActionValidity.Valid
            : ActionValidity.Invalid;
    }

    public override bool Execute(IEnumerable<TrackAsset> tracks)
    {
        return TimelineClipLockTool.SetRippleOnTracks(tracks.ToList(), true);
    }
}

[MenuEntry("Stop Ripple On Track", MenuPriority.TimelineActionSection.delete + 33)]
public sealed class TimelineDisableTrackRippleAction : TrackAction
{
    public override ActionValidity Validate(IEnumerable<TrackAsset> tracks)
    {
        IList<TrackAsset> selected = tracks.ToList();
        if (!TimelineClipLockTool.CanUseTracks(selected))
        {
            return ActionValidity.NotApplicable;
        }

        return TimelineClipLockTool.AreTracksFullyRippled(selected) ? ActionValidity.Valid : ActionValidity.Invalid;
    }

    public override bool Execute(IEnumerable<TrackAsset> tracks)
    {
        return TimelineClipLockTool.SetRippleOnTracks(tracks.ToList(), false);
    }
}
