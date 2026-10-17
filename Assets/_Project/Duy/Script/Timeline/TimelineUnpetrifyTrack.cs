using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.35f, 0.75f, 0.65f)]
[TrackClipType(typeof(TimelineUnpetrifyClip))]
[TrackBindingType(typeof(PetrifyController))]
[System.ComponentModel.DisplayName("TimelineTrack/Unpetrify")]
public class TimelineUnpetrifyTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        ScriptPlayable<TimelineUnpetrifyMixerBehaviour> mixer =
            ScriptPlayable<TimelineUnpetrifyMixerBehaviour>.Create(graph, inputCount);

        mixer.GetBehaviour().SetClipTimes(GetClips());
        return mixer;
    }
}

public class TimelineUnpetrifyMixerBehaviour : PlayableBehaviour
{
    private readonly List<double> clipStarts = new List<double>();
    private readonly List<double> clipEnds = new List<double>();

    private TimelineUnpetrifyBehaviour activeClip;

    public void SetClipTimes(IEnumerable<TimelineClip> clips)
    {
        clipStarts.Clear();
        clipEnds.Clear();

        if (clips == null) return;

        foreach (TimelineClip clip in clips)
        {
            clipStarts.Add(clip.start);
            clipEnds.Add(clip.end);
        }
    }

    public override void OnGraphStart(Playable playable)
    {
        TimelineUnpetrifyBehaviour.ResetBaselines();
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        TimelineSeekContext.Evaluate(playable, info);

        PetrifyController boundTarget = playerData as PetrifyController;

        if (TimelineSeekContext.IsSeeking)
        {
            ApplySeek(playable, boundTarget, TimelineSeekContext.DirectorTime);
            return;
        }

        TimelineUnpetrifyClipState nextClip = GetStrongestActiveClip(playable);

        if (nextClip.Behaviour == null ||
            (!Application.isPlaying && !nextClip.Behaviour.applyInEditMode))
        {
            ExitActiveClip();
            return;
        }

        if (nextClip.Behaviour.targetMode == TimelineUnpetrifyTargetMode.BoundTarget &&
            boundTarget == null &&
            nextClip.Behaviour.targetOverride == null)
        {
            ExitActiveClip();
            return;
        }

        if (activeClip != null && activeClip != nextClip.Behaviour)
        {
            activeClip.Exit();
        }

        activeClip = nextClip.Behaviour;
        activeClip.Enter(boundTarget);
        activeClip.Process(nextClip.NormalizedTime);
    }

    public override void OnGraphStop(Playable playable)
    {
        ExitActiveClip();
    }

    private void ApplySeek(Playable playable, PetrifyController boundTarget, double directorTime)
    {
        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount && i < clipEnds.Count; i++)
        {
            Playable input = playable.GetInput(i);
            if (!input.IsValid() || input.GetPlayableType() != typeof(TimelineUnpetrifyBehaviour))
            {
                continue;
            }

            TimelineUnpetrifyBehaviour behaviour = ((ScriptPlayable<TimelineUnpetrifyBehaviour>)input).GetBehaviour();
            double start = clipStarts[i];
            double end = clipEnds[i];
            double duration = end - start;

            TimelinePetrifySeekPhase phase;
            float normalizedTime;

            if (directorTime < start)
            {
                phase = TimelinePetrifySeekPhase.Before;
                normalizedTime = 0f;
            }
            else if (directorTime >= end)
            {
                phase = TimelinePetrifySeekPhase.After;
                normalizedTime = 1f;
            }
            else
            {
                phase = TimelinePetrifySeekPhase.Inside;
                normalizedTime = duration > 0d
                    ? Mathf.Clamp01((float)((directorTime - start) / duration))
                    : 1f;
            }

            behaviour.ApplySeekState(boundTarget, normalizedTime, phase);
        }

        activeClip = null;
    }

    private TimelineUnpetrifyClipState GetStrongestActiveClip(Playable playable)
    {
        TimelineUnpetrifyClipState strongestClip = new TimelineUnpetrifyClipState();
        float strongestWeight = 0f;

        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0.001f || weight <= strongestWeight)
            {
                continue;
            }
            if (playable.GetInput(i).GetPlayableType() != typeof(TimelineUnpetrifyBehaviour)) continue;
            ScriptPlayable<TimelineUnpetrifyBehaviour> inputPlayable = (ScriptPlayable<TimelineUnpetrifyBehaviour>)playable.GetInput(i);

            strongestClip = new TimelineUnpetrifyClipState(
                inputPlayable.GetBehaviour(),
                GetNormalizedTime(inputPlayable));
            strongestWeight = weight;
        }

        return strongestClip;
    }

    private float GetNormalizedTime(Playable playable)
    {
        double duration = playable.GetDuration();
        double time = playable.GetTime();
        if (double.IsInfinity(duration) || double.IsNaN(duration) || duration <= 0d)
        {
            return 1f;
        }

        return Mathf.Clamp01((float)(time / duration));
    }

    private void ExitActiveClip()
    {
        if (activeClip == null) return;
        activeClip.Exit();
        activeClip = null;
    }

    private struct TimelineUnpetrifyClipState
    {
        public TimelineUnpetrifyBehaviour Behaviour;
        public float NormalizedTime;

        public TimelineUnpetrifyClipState(
            TimelineUnpetrifyBehaviour behaviour,
            float normalizedTime)
        {
            Behaviour = behaviour;
            NormalizedTime = normalizedTime;
        }
    }
}
