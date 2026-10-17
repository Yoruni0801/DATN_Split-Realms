using UnityEngine;
using UnityEngine.Playables;

public class TimelineAnimatorAnimationBehaviour : PlayableBehaviour
{
    public AnimationClip animationClip;
    public string stateName;
    public int layerIndex;
    public bool loop;
    public TimelineAnimatorAnimationExitMode exitMode;

    private Animator animator;
    private bool clipActive;
    private int stateHash;
    private int previousStateHash;
    private float previousStateTime;

    public void Evaluate(Animator boundAnimator, Playable playable)
    {
        if (boundAnimator == null || !boundAnimator.gameObject.activeInHierarchy || boundAnimator.runtimeAnimatorController == null)
        {
            Exit();
            return;
        }

        if (animator != boundAnimator)
        {
            animator = boundAnimator;
            clipActive = false;
        }

        if (!clipActive)
        {
            Begin();
        }

        PlayAtTimelineTime(playable);
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        Exit();
    }

    public void Exit()
    {
        if (!clipActive || animator == null)
        {
            return;
        }

        if (exitMode == TimelineAnimatorAnimationExitMode.RestorePrevious && previousStateHash != 0)
        {
            if (animator.gameObject.activeInHierarchy && animator.runtimeAnimatorController != null)
            {
                animator.Play(previousStateHash, ResolvedLayerIndex, previousStateTime);
                animator.Update(0f);
            }
        }

        clipActive = false;
    }

    private void Begin()
    {
        string resolvedStateName = ResolveStateName();
        stateHash = string.IsNullOrEmpty(resolvedStateName) ? 0 : Animator.StringToHash(resolvedStateName);

        if (animator.runtimeAnimatorController != null && animator.gameObject.activeInHierarchy && animator.isActiveAndEnabled)
        {
            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(ResolvedLayerIndex);
            previousStateHash = currentState.fullPathHash;
            previousStateTime = currentState.normalizedTime;
        }
        else
        {
            previousStateHash = 0;
            previousStateTime = 0f;
        }

        clipActive = true;
    }

    private void PlayAtTimelineTime(Playable playable)
    {
        if (stateHash == 0 || animator == null || !animator.gameObject.activeInHierarchy || animator.runtimeAnimatorController == null)
        {
            return;
        }

        float length = animationClip != null && animationClip.length > 0f ? animationClip.length : 1f;
        float normalizedTime = (float)(playable.GetTime() / length);
        if (loop)
        {
            normalizedTime = Mathf.Repeat(normalizedTime, 1f);
        }

        animator.Play(stateHash, ResolvedLayerIndex, normalizedTime);
        animator.Update(0f);
    }

    private string ResolveStateName()
    {
        if (!string.IsNullOrEmpty(stateName))
        {
            return stateName;
        }

        return animationClip != null ? animationClip.name : string.Empty;
    }

    private int ResolvedLayerIndex
    {
        get
        {
            if (animator == null || animator.layerCount <= 0)
            {
                return 0;
            }

            return Mathf.Clamp(layerIndex, 0, animator.layerCount - 1);
        }
    }
}
