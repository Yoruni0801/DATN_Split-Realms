using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.35f, 0.75f, 0.9f)]
[TrackClipType(typeof(TimelineSpineMoveClip))]
[TrackBindingType(typeof(AnimationSpine))]
[System.ComponentModel.DisplayName("TimelineTrack/Spine Move")]
public class TimelineSpineMoveTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        ScriptPlayable<TimelineSpineMoveMixerBehaviour> mixer =
            ScriptPlayable<TimelineSpineMoveMixerBehaviour>.Create(graph, inputCount);

        // The mixer needs the clip times to replay earlier clips after a seek; the playable graph does not
        // expose where a clip sits on the Timeline.
        mixer.GetBehaviour().SetClipTimes(GetClips());
        return mixer;
    }
}

public class TimelineSpineMoveMixerBehaviour : PlayableBehaviour
{
    private static readonly Dictionary<Transform, TimelineSpineMoveMixerBehaviour> FrameFallbackOwners =
        new Dictionary<Transform, TimelineSpineMoveMixerBehaviour>();
    private static int fallbackOwnerFrame = -1;
    private readonly List<double> clipStarts = new List<double>();
    private readonly List<double> clipEnds = new List<double>();

    private TimelineSpineMoveBehaviour activeClip;

    /// <summary>
    /// Timeline-start position per bound character. Keyed by transform so the player and every NPC driven by this
    /// track keep their own baseline instead of sharing one.
    /// </summary>
    private readonly Dictionary<Transform, Vector3> baselinePositions = new Dictionary<Transform, Vector3>();

    public void SetClipTimes(IEnumerable<TimelineClip> clips)
    {
        clipStarts.Clear();
        clipEnds.Clear();

        if (clips == null)
        {
            return;
        }

        foreach (TimelineClip clip in clips)
        {
            clipStarts.Add(clip.start);
            clipEnds.Add(clip.end);
        }
    }

    public override void OnGraphStart(Playable playable)
    {
        baselinePositions.Clear();
        TimelineSeekContext.Clear(playable);
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        TimelineSeekContext.Evaluate(playable, info);

        if (TimelineSeekContext.IsSeeking)
        {
            ProcessSeekFrame(playable, playerData, TimelineSeekContext.DirectorTime);
            return;
        }

        ProcessPlaybackFrame(playable, playerData);
    }

    private void ProcessPlaybackFrame(Playable playable, object playerData)
    {
        TimelineSpineMoveClipState nextClip = GetStrongestActiveClip(playable);
        if (nextClip.Behaviour == null ||
            (!Application.isPlaying && !nextClip.Behaviour.applyInEditMode))
        {
            ExitActiveClip();
            return;
        }

        AnimationSpine animationSpine = nextClip.Behaviour.ResolveAnimationSpine(playerData as AnimationSpine);
        if (animationSpine == null || !CanUseResolvedSpine(animationSpine, playerData, true))
        {
            ExitActiveClip();
            return;
        }

        CaptureBaseline(animationSpine.transform);

        if (activeClip != null && activeClip != nextClip.Behaviour)
        {
            activeClip.Exit();
        }

        activeClip = nextClip.Behaviour;
        activeClip.Enter(animationSpine);
        activeClip.Process(nextClip.LocalTime);
    }

    /// <summary>
    /// Rebuilds the bound character from its own baseline for any playhead time, including times where no clip is
    /// active. Before the first clip the character returns to its baseline, between or after clips it stays at the
    /// end of the last finished clip, and inside a clip it lands on that clip's in between position.
    /// The covering clip is found by timeline overlap rather than input weight, which is not reliable on the exact
    /// frame a seek is processed.
    /// </summary>
    private void ProcessSeekFrame(Playable playable, object playerData, double directorTime)
    {
        int coveringIndex = FindClipIndexAtTime(directorTime);
        TimelineSpineMoveBehaviour covering = coveringIndex >= 0 ? GetBehaviourAt(playable, coveringIndex) : null;
        AnimationSpine animationSpine = covering != null
            ? covering.ResolveAnimationSpine(playerData as AnimationSpine)
            : ResolveBoundSpine(playable, playerData);
        if (animationSpine == null || !CanUseResolvedSpine(animationSpine, playerData, covering != null))
        {
            ExitActiveClipForSeek();
            return;
        }

        Transform characterTransform = animationSpine.transform;
        CaptureBaseline(characterTransform);

        Vector3 position = RebuildToTime(playable, characterTransform, directorTime);
        ApplySeekPosition(characterTransform, position);

        if (covering == null)
        {
            ExitActiveClipForSeek();
            return;
        }

        if (activeClip != null && activeClip != covering)
        {
            ExitActiveClipForSeek();
        }

        activeClip = covering;

        // The seek frame only repositions: no control lock, no animation restart, no Rigidbody writes.
        activeClip.seekPreview = true;
        try
        {
            activeClip.ResetPath();
            activeClip.Enter(animationSpine);

            // Chains the covering clip from where the earlier clips left the character, so its local time maps to the
            // same position normal playback would produce, both now and on the frames after the seek.
            activeClip.PrimePath(position);
            activeClip.Process((float)(directorTime - clipStarts[coveringIndex]));
            ApplySeekPosition(characterTransform, characterTransform.position);
        }
        finally
        {
            activeClip.seekPreview = false;
        }
    }

    private static void ApplySeekPosition(Transform characterTransform, Vector3 position)
    {
        if (characterTransform == null)
        {
            return;
        }

        Rigidbody2D body = characterTransform.GetComponent<Rigidbody2D>();
        if (body != null && Application.isPlaying)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = (Vector2)position;
            characterTransform.position = position;
            return;
        }

        characterTransform.position = position;
    }

    private void CaptureBaseline(Transform characterTransform)
    {
        if (characterTransform != null && !baselinePositions.ContainsKey(characterTransform))
        {
            baselinePositions.Add(characterTransform, characterTransform.position);
        }
    }

    /// <summary>
    /// Resolves the character even when no clip has weight, so a seek between clips still has something to place.
    /// </summary>
    private AnimationSpine ResolveBoundSpine(Playable playable, object playerData)
    {
        AnimationSpine bound = playerData as AnimationSpine;

        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            TimelineSpineMoveBehaviour behaviour = GetBehaviourAt(playable, i);
            if (behaviour == null)
            {
                continue;
            }

            AnimationSpine resolved = behaviour.ResolveAnimationSpine(bound);
            if (resolved != null)
            {
                return resolved;
            }
        }

        return bound;
    }

    private bool CanUseResolvedSpine(AnimationSpine animationSpine, object playerData, bool hasActiveClip)
    {
        if (animationSpine == null)
        {
            return false;
        }

        if (playerData is AnimationSpine)
        {
            return true;
        }

        if (animationSpine.GetComponent<Player>() == null)
        {
            return true;
        }

        if (!hasActiveClip)
        {
            return false;
        }

        if (fallbackOwnerFrame != Time.frameCount)
        {
            FrameFallbackOwners.Clear();
            fallbackOwnerFrame = Time.frameCount;
        }

        Transform key = animationSpine.transform;
        if (FrameFallbackOwners.TryGetValue(key, out TimelineSpineMoveMixerBehaviour owner))
        {
            return owner == this;
        }

        FrameFallbackOwners.Add(key, this);
        return true;
    }
    /// <summary>Last clip whose timeline range contains the playhead, or -1 when the playhead sits between clips.</summary>
    private int FindClipIndexAtTime(double directorTime)
    {
        int found = -1;
        for (int i = 0; i < clipStarts.Count; i++)
        {
            if (directorTime >= clipStarts[i] - 0.0001 && directorTime < clipEnds[i])
            {
                found = i;
            }
        }

        return found;
    }

    public override void OnGraphStop(Playable playable)
    {
        ExitActiveClip();
        baselinePositions.Clear();
        TimelineSeekContext.Clear(playable);
    }

    /// <summary>
    /// Replays the way continuous playback would have produced the position: from this character's own
    /// timeline-start baseline through every clip that has already finished at the new playhead time. Returns the
    /// position the character should have when the currently active clip begins.
    /// </summary>
    private Vector3 RebuildToTime(Playable playable, Transform characterTransform, double directorTime)
    {
        Vector3 position = baselinePositions.TryGetValue(characterTransform, out Vector3 baseline)
            ? baseline
            : characterTransform.position;

        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount && i < clipEnds.Count; i++)
        {
            if (clipEnds[i] > directorTime + 0.0001)
            {
                continue;
            }

            TimelineSpineMoveBehaviour behaviour = GetBehaviourAt(playable, i);
            if (behaviour == null)
            {
                continue;
            }

            behaviour.ResetPath();
            position = behaviour.EvaluatePositionAt(position, (float)(clipEnds[i] - clipStarts[i]));
        }

        return position;
    }

    private static TimelineSpineMoveBehaviour GetBehaviourAt(Playable playable, int index)
    {
        Playable input = playable.GetInput(index);
        if (!input.IsValid() || input.GetPlayableType() != typeof(TimelineSpineMoveBehaviour))
        {
            return null;
        }

        return ((ScriptPlayable<TimelineSpineMoveBehaviour>)input).GetBehaviour();
    }

    private TimelineSpineMoveClipState GetStrongestActiveClip(Playable playable)
    {
        TimelineSpineMoveClipState strongestClip = new TimelineSpineMoveClipState();
        float strongestWeight = 0f;

        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0.001f || weight <= strongestWeight)
            {
                continue;
            }
            if (playable.GetInput(i).GetPlayableType() != typeof(TimelineSpineMoveBehaviour)) continue;
            ScriptPlayable<TimelineSpineMoveBehaviour> inputPlayable = (ScriptPlayable<TimelineSpineMoveBehaviour>)playable.GetInput(i);

            strongestClip = new TimelineSpineMoveClipState(
                inputPlayable.GetBehaviour(),
                GetLocalTime(inputPlayable));
            strongestWeight = weight;
        }

        return strongestClip;
    }

    private float GetLocalTime(Playable playable)
    {
        double time = playable.GetTime();
        if (double.IsInfinity(time) || double.IsNaN(time) || time < 0d)
        {
            return 0f;
        }

        return (float)time;
    }

    private void ExitActiveClip()
    {
        if (activeClip == null)
        {
            return;
        }

        activeClip.Exit();
        activeClip = null;
    }

    /// <summary>Leaves the clip without unlocking player control, stopping animation or writing to the Rigidbody.</summary>
    private void ExitActiveClipForSeek()
    {
        if (activeClip == null)
        {
            return;
        }

        activeClip.seekPreview = true;
        activeClip.Exit();
        activeClip.seekPreview = false;
        activeClip = null;
    }

    private struct TimelineSpineMoveClipState
    {
        public TimelineSpineMoveBehaviour Behaviour;
        public float LocalTime;

        public TimelineSpineMoveClipState(
            TimelineSpineMoveBehaviour behaviour,
            float localTime)
        {
            Behaviour = behaviour;
            LocalTime = localTime;
        }
    }
}





