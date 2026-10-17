using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Tells Duy Timeline behaviours whether the current frame is normal playback or a manual seek, so dragging the
/// playhead in the Timeline window can reposition deterministic state without firing one shot gameplay side
/// effects such as damage, scene loads or pickups.
/// Every behaviour calls <see cref="Evaluate"/> at the top of its ProcessFrame; the result is computed once per
/// frame per director and stays valid for the rest of that frame.
/// Only meaningful in Play Mode: edit mode preview is left exactly as it was.
/// </summary>
public static class TimelineSeekContext
{
    /// <summary>Extra time a single frame may advance before the jump counts as a seek.</summary>
    private const double SeekSlack = 0.05;

    private static readonly Dictionary<PlayableDirector, double> LastDirectorTimes =
        new Dictionary<PlayableDirector, double>();
    private static readonly Dictionary<PlayableDirector, int> ExplicitSeekDepths =
        new Dictionary<PlayableDirector, int>();

    private static PlayableDirector evaluatedDirector;
    private static int evaluatedFrame = -1;

    public static bool IsSeeking { get; private set; }
    public static double DirectorTime { get; private set; }
    public static double PreviousDirectorTime { get; private set; }

    public static void Evaluate(Playable playable, FrameData info)
    {
        if (!Application.isPlaying)
        {
            IsSeeking = false;
            return;
        }

        PlayableDirector director = ResolveDirector(playable);
        if (director == null)
        {
            IsSeeking = false;
            return;
        }

        if (evaluatedFrame == Time.frameCount && evaluatedDirector == director)
        {
            return;
        }

        double time = director.time;
        bool hasPrevious = LastDirectorTimes.TryGetValue(director, out double previous);

        double delta = time - previous;
        double allowed = Math.Max(0.0, info.deltaTime * Mathf.Max(1f, info.effectiveSpeed)) + SeekSlack;

        IsSeeking = IsExplicitlySeeking(director)
            || (hasPrevious && (info.seekOccurred || delta < -0.0001 || delta > allowed));
        PreviousDirectorTime = hasPrevious ? previous : time;
        DirectorTime = time;

        LastDirectorTimes[director] = time;
        evaluatedDirector = director;
        evaluatedFrame = Time.frameCount;
    }

    /// <summary>Drops the tracked time for this graph, so a restarted Timeline does not read as a jump.</summary>
    public static void Clear(Playable playable)
    {
        IsSeeking = false;
        evaluatedDirector = null;
        evaluatedFrame = -1;

        PlayableDirector director = ResolveDirector(playable);
        if (director != null)
        {
            LastDirectorTimes.Remove(director);
        }

        PruneDestroyedDirectors();
    }

    public static void BeginExplicitSeek(PlayableDirector director)
    {
        if (director == null)
        {
            return;
        }

        ExplicitSeekDepths.TryGetValue(director, out int depth);
        ExplicitSeekDepths[director] = depth + 1;
        IsSeeking = true;
    }

    public static void EndExplicitSeek(PlayableDirector director)
    {
        if (director == null || !ExplicitSeekDepths.TryGetValue(director, out int depth))
        {
            return;
        }

        if (depth <= 1)
        {
            ExplicitSeekDepths.Remove(director);
        }
        else
        {
            ExplicitSeekDepths[director] = depth - 1;
        }

        IsSeeking = false;
        evaluatedDirector = null;
        evaluatedFrame = -1;
    }

    public static bool IsExplicitlySeeking(PlayableDirector director)
    {
        return director != null
            && ExplicitSeekDepths.TryGetValue(director, out int depth)
            && depth > 0;
    }

    private static PlayableDirector ResolveDirector(Playable playable)
    {
        if (!playable.IsValid())
        {
            return null;
        }

        PlayableGraph graph = playable.GetGraph();
        return graph.IsValid() ? graph.GetResolver() as PlayableDirector : null;
    }

    private static void PruneDestroyedDirectors()
    {
        List<PlayableDirector> destroyed = null;
        foreach (KeyValuePair<PlayableDirector, double> entry in LastDirectorTimes)
        {
            if (entry.Key == null)
            {
                destroyed ??= new List<PlayableDirector>();
                destroyed.Add(entry.Key);
            }
        }

        if (destroyed == null)
        {
            return;
        }

        for (int i = 0; i < destroyed.Count; i++)
        {
            LastDirectorTimes.Remove(destroyed[i]);
            ExplicitSeekDepths.Remove(destroyed[i]);
        }
    }
}
