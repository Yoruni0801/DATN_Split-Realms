using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>Where the playhead sits relative to a petrify clip when the Timeline is seeked.</summary>
public enum TimelinePetrifySeekPhase
{
    Before,
    Inside,
    After
}

public class TimelinePetrifyBehaviour : PlayableBehaviour
{
    
    public bool includeInactiveTargets;
    public bool animateOverClip;
    public bool applyInEditMode;
    private bool clipActive;

    public float startAmount;
    public float endAmount;
    private float lastNormalizedTime;

    public TimelinePetrifyExitMode exitMode;
    private readonly List<TargetState> targets = new List<TargetState>();
    public TimelinePetrifyTargetMode targetMode;
    public PetrifyController targetOverride;
    private PetrifyController boundTarget;

    private readonly List<PetrifyController> seekTargets = new List<PetrifyController>();

    /// <summary>
    /// Petrify amount each target had when the Timeline graph started. Seeking resolves against this instead of
    /// the live scene value, which may already have been changed by the clip the user is scrubbing through.
    /// </summary>
    private static readonly Dictionary<PetrifyController, float> Baselines =
        new Dictionary<PetrifyController, float>();

    public static void ResetBaselines()
    {
        Baselines.Clear();
    }

    /// <summary>
    /// Deterministic evaluation used while the playhead is dragged: sets the petrify amount straight from the
    /// clip time, with no enter, exit or BeginPetrifying side effects.
    /// </summary>
    public void ApplySeekState(PetrifyController petrifyTarget, float normalizedTime, TimelinePetrifySeekPhase phase)
    {
        CollectSeekTargets(petrifyTarget);

        for (int i = 0; i < seekTargets.Count; i++)
        {
            PetrifyController target = seekTargets[i];
            if (target == null)
            {
                continue;
            }

            float baseline = CaptureBaseline(target);
            float amount;

            switch (phase)
            {
                case TimelinePetrifySeekPhase.Inside:
                    amount = GetAmountAtTime(normalizedTime);
                    break;
                case TimelinePetrifySeekPhase.After:
                    amount = ResolveExitAmount(baseline);
                    break;
                default:
                    amount = baseline;
                    break;
            }

            target.SetPetrifyAmount(amount);
        }

        // The normal path re-enters from scratch on the next playback frame.
        Clear();
    }

    private float ResolveExitAmount(float baseline)
    {
        if (exitMode == TimelinePetrifyExitMode.ClearPetrify)
        {
            return 0f;
        }

        if (ShouldKeepPetrified() || baseline >= 0.999f)
        {
            return 1f;
        }

        return exitMode == TimelinePetrifyExitMode.KeepEndAmount ? endAmount : baseline;
    }

    private void CollectSeekTargets(PetrifyController petrifyTarget)
    {
        seekTargets.Clear();

        if (targetMode == TimelinePetrifyTargetMode.AllTargets)
        {
            PetrifyController[] petrifyTargets =
                Object.FindObjectsByType<PetrifyController>(
                    includeInactiveTargets ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            for (int i = 0; i < petrifyTargets.Length; i++)
            {
                if (petrifyTargets[i] != null && !seekTargets.Contains(petrifyTargets[i]))
                {
                    seekTargets.Add(petrifyTargets[i]);
                }
            }

            return;
        }

        PetrifyController resolved = targetOverride != null ? targetOverride : petrifyTarget;
        if (resolved != null)
        {
            seekTargets.Add(resolved);
        }
    }

    private static float CaptureBaseline(PetrifyController target)
    {
        if (target == null)
        {
            return 0f;
        }

        if (!Baselines.TryGetValue(target, out float amount))
        {
            amount = target.PetrifyAmount;
            Baselines[target] = amount;
        }

        return amount;
    }


    public void Enter(PetrifyController petrifyTarget)
    {
        if (clipActive && boundTarget == petrifyTarget)
        {
            return;
        }

        Exit();

        boundTarget = petrifyTarget;
        clipActive = true;
        lastNormalizedTime = 0f;
        CollectTargets();
        BeginPetrifyingTargets();
    }

    public void Process(float normalizedTime)
    {
        if (!clipActive)
        {
            return;
        }

        lastNormalizedTime = Mathf.Clamp01(normalizedTime);
        float amount = GetAmountAtTime(lastNormalizedTime);
        if (ShouldKeepPetrified())
        {
            amount = Mathf.Max(amount, PetrifyController.PetrifyStartedAmount);
        }

        for (int i = 0; i < targets.Count; i++)
        {
            TargetState targetState = targets[i];
            PetrifyController target = targetState.Target;
            if (target != null)
            {
                target.SetPetrifyAmount(IsPermanentlyPetrified(targetState) ? 1f : amount);
            }
        }
    }

    public void Exit()
    {
        if (!clipActive)
        {
            Clear();
            return;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            TargetState targetState = targets[i];
            PetrifyController target = targetState.Target;
            if (target == null)
            {
                continue;
            }

            if (exitMode == TimelinePetrifyExitMode.ClearPetrify)
            {
                target.SetPetrifyAmount(0f);
                continue;
            }

            if (ShouldKeepPetrified() || IsPermanentlyPetrified(targetState))
            {
                target.SetPetrifyAmount(1f);
                continue;
            }

            switch (exitMode)
            {
                case TimelinePetrifyExitMode.KeepEndAmount:
                    target.SetPetrifyAmount(endAmount);
                    break;
                default:
                    target.SetPetrifyAmount(targetState.PreviousAmount);
                    break;
            }
        }

        Clear();
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        Exit();
    }

    private void CollectTargets()
    {
        targets.Clear();

        if (targetMode == TimelinePetrifyTargetMode.AllTargets)
        {
            PetrifyController[] petrifyTargets =
                Object.FindObjectsByType<PetrifyController>(
                    includeInactiveTargets ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            for (int i = 0; i < petrifyTargets.Length; i++)
            {
                AddTarget(petrifyTargets[i]);
            }

            return;
        }

        if (targetOverride != null)
        {
            AddTarget(targetOverride);
            return;
        }

        AddTarget(boundTarget);
    }

    private void AddTarget(PetrifyController target)
    {
        if (target == null || ContainsTarget(target))
        {
            return;
        }

        // Records the graph-start value too, so a later seek has a baseline that predates this clip.
        CaptureBaseline(target);
        targets.Add(new TargetState(target, target.PetrifyAmount));
    }

    private void BeginPetrifyingTargets()
    {
        if (!ShouldKeepPetrified())
        {
            return;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            PetrifyController target = targets[i].Target;
            if (target != null && !IsPermanentlyPetrified(targets[i]))
            {
                target.BeginPetrifying();
            }
        }
    }

    private bool ShouldKeepPetrified()
    {
        return endAmount >= 0.999f;
    }

    private bool IsPermanentlyPetrified(TargetState targetState)
    {
        return targetState.PreviousAmount >= 0.999f
            || (targetState.Target != null && targetState.Target.IsPetrified);
    }

    private float GetAmountAtTime(float normalizedTime)
    {
        return animateOverClip
            ? Mathf.Lerp(startAmount, endAmount, Mathf.Clamp01(normalizedTime))
            : endAmount;
    }

    private bool ContainsTarget(PetrifyController target)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i].Target == target)
            {
                return true;
            }
        }

        return false;
    }

    private void Clear()
    {
        targets.Clear();
        boundTarget = null;
        clipActive = false;
        lastNormalizedTime = 0f;
    }

    private struct TargetState
    {
        public PetrifyController Target;
        public float PreviousAmount;

        public TargetState(PetrifyController target, float previousAmount)
        {
            Target = target;
            PreviousAmount = previousAmount;
        }
    }
}
