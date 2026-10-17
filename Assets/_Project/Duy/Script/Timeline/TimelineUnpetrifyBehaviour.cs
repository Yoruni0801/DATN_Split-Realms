using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public enum TimelineUnpetrifyTargetMode
{
    BoundTarget,
    AllTargets
}

public enum TimelineUnpetrifyExitMode
{
    ClearPetrify,
    KeepEndAmount,
    RestorePrevious
}

public class TimelineUnpetrifyBehaviour : PlayableBehaviour
{
    public bool includeInactiveTargets;
    public bool animateOverClip = true;
    public bool applyInEditMode = true;
    private bool clipActive;

    public float startAmount = 1f;
    public float endAmount = 0f;
    private float lastNormalizedTime;

    public TimelineUnpetrifyExitMode exitMode = TimelineUnpetrifyExitMode.ClearPetrify;
    private readonly List<TargetState> targets = new List<TargetState>();
    public TimelineUnpetrifyTargetMode targetMode = TimelineUnpetrifyTargetMode.BoundTarget;
    public PetrifyController targetOverride;
    private PetrifyController boundTarget;

    private readonly List<PetrifyController> seekTargets = new List<PetrifyController>();

    private static readonly Dictionary<PetrifyController, float> Baselines =
        new Dictionary<PetrifyController, float>();

    public static void ResetBaselines()
    {
        Baselines.Clear();
    }

    public void ApplySeekState(PetrifyController petrifyTarget, float normalizedTime, TimelinePetrifySeekPhase phase)
    {
        CollectSeekTargets(petrifyTarget);

        for (int i = 0; i < seekTargets.Count; i++)
        {
            PetrifyController target = seekTargets[i];
            if (target == null) continue;

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

        Clear();
    }

    private float ResolveExitAmount(float baseline)
    {
        switch (exitMode)
        {
            case TimelineUnpetrifyExitMode.ClearPetrify:
                return 0f;
            case TimelineUnpetrifyExitMode.KeepEndAmount:
                return endAmount;
            default:
                return baseline;
        }
    }

    private void CollectSeekTargets(PetrifyController petrifyTarget)
    {
        seekTargets.Clear();

        if (targetMode == TimelineUnpetrifyTargetMode.AllTargets)
        {
            PetrifyController[] petrifyTargets = Object.FindObjectsByType<PetrifyController>(
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
        if (target == null) return 0f;

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
    }

    public void Process(float normalizedTime)
    {
        if (!clipActive) return;

        lastNormalizedTime = Mathf.Clamp01(normalizedTime);
        float amount = GetAmountAtTime(lastNormalizedTime);

        for (int i = 0; i < targets.Count; i++)
        {
            PetrifyController target = targets[i].Target;
            if (target != null)
            {
                target.SetPetrifyAmount(amount);
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
            if (target == null) continue;

            float exitAmount = ResolveExitAmount(targetState.PreviousAmount);
            target.SetPetrifyAmount(exitAmount);
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

        if (targetMode == TimelineUnpetrifyTargetMode.AllTargets)
        {
            PetrifyController[] petrifyTargets = Object.FindObjectsByType<PetrifyController>(
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
        if (target == null || ContainsTarget(target)) return;

        CaptureBaseline(target);
        targets.Add(new TargetState(target, target.PetrifyAmount));
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
            if (targets[i].Target == target) return true;
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
