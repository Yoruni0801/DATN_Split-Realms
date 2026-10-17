using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Draws the lock and ripple state onto the clips themselves, since Timeline's checked menu state
/// (IMenuChecked) is internal to the package and cannot be implemented from project code.
/// </summary>
public static class TimelineClipLockDecorator
{
    private static Texture2D lockIcon;
    private static Texture2D rippleIcon;
    private static Texture2D groupIcon;
    private static Texture2D durationIcon;
    private static bool iconsLoaded;

    /// <summary>
    /// Adds the lock and ripple icons plus a tooltip to the clip options a ClipEditor is about to return.
    /// </summary>
    public static ClipDrawOptions Decorate(ClipDrawOptions options, TimelineClip clip)
    {
        bool locked = TimelineClipLockTool.IsClipLocked(clip);
        bool rippled = TimelineClipLockTool.IsClipRippled(clip);
        bool grouped = TimelineClipLockTool.IsClipGroupLocked(clip);
        bool durationLocked = TimelineClipLockTool.IsClipDurationLocked(clip);
        if (!locked && !rippled && !grouped && !durationLocked)
        {
            return options;
        }

        LoadIcons();

        List<Texture2D> icons = options.icons != null
            ? options.icons.Where(icon => icon != null).ToList()
            : new List<Texture2D>();

        if (grouped && groupIcon != null) icons.Add(groupIcon);
        if (durationLocked && durationIcon != null) icons.Add(durationIcon);
        if (locked && lockIcon != null) icons.Add(lockIcon);
        if (rippled && rippleIcon != null) icons.Add(rippleIcon);
        options.icons = icons;

        List<string> notes = new List<string>();
        if (grouped) notes.Add("Locked as group");
        if (durationLocked) notes.Add("Duration locked, dragging the start moves the whole clip");
        if (locked) notes.Add("Start and end locked to its paired clip");
        if (rippled) notes.Add("Pushes later clips when its end grows");

        string tooltip = string.Join("\n", notes);
        options.tooltip = string.IsNullOrEmpty(options.tooltip) ? tooltip : options.tooltip + "\n" + tooltip;

        return options;
    }

    private static void LoadIcons()
    {
        if (iconsLoaded)
        {
            return;
        }

        iconsLoaded = true;
        lockIcon = LoadIcon("AssemblyLock");
        rippleIcon = LoadIcon("Animation.NextKey");
        groupIcon = LoadIcon("LockIcon-On", "AssemblyLock", "Linked");
        durationIcon = LoadIcon("d_UnityEditor.AnimationWindow", "UnityEditor.AnimationWindow", "AssemblyLock");
    }

    /// <summary>Returns the first built in icon that resolves, so no image asset or .meta file is needed.</summary>
    private static Texture2D LoadIcon(params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Texture2D icon = EditorGUIUtility.IconContent(names[i]).image as Texture2D;
            if (icon != null)
            {
                return icon;
            }
        }

        return null;
    }
}

/// <summary>
/// Fallback clip editor so every clip without a more specific editor shows the lock and ripple state.
/// Timeline resolves clip editors by walking up the asset type hierarchy, so built-in clip types that ship
/// their own exact editor keep theirs and are not decorated.
/// </summary>
[CustomTimelineEditor(typeof(PlayableAsset))]
public class TimelineClipLockStateClipEditor : ClipEditor
{
    public override ClipDrawOptions GetClipOptions(TimelineClip clip)
    {
        return TimelineClipLockDecorator.Decorate(base.GetClipOptions(clip), clip);
    }
}
