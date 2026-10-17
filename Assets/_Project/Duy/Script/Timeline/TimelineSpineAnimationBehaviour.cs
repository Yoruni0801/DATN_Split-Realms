using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Playables;

public class TimelineSpineAnimationBehaviour : PlayableBehaviour
{
    public AnimationSpine.State state;
    public bool loopAnimation;
    public int trackIndex;
    public float mixDuration;
    public float timeScale;
    public bool resetToSetupPoseBeforePlay;
    public TimelineSpineAnimationExitMode exitMode;
    public bool restoreTimeScale;
    public bool reverseAnimation;
    public TimelineSpineFacingOverride facingOverride;

    private AnimationSpine animationSpine;
    private SkeletonAnimation skeletonAnimation;
    private PetrifyController petrifyController;
    private Transform animatedTransform;
    private Player player;
    private bool clipActive;
    private AnimationSpine.State previousState;
    private float previousTimeScale = 1f;

    public void Enter(AnimationSpine boundAnimationSpine)
    {
        if (boundAnimationSpine == null)
        {
            return;
        }

        if (animationSpine != boundAnimationSpine)
        {
            animationSpine = boundAnimationSpine;
            skeletonAnimation = animationSpine.SkeletonAnimation;
            petrifyController = animationSpine.GetComponent<PetrifyController>();
            animatedTransform = animationSpine.transform;
            player = animationSpine.GetComponent<Player>();
            clipActive = false;
        }

        if (IsPetrifying())
        {
            if (skeletonAnimation != null)
            {
                skeletonAnimation.timeScale = 0f;
            }

            return;
        }

        if (!clipActive)
        {
            PlayClipAnimation();
        }
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        Exit();
    }

    public void Exit()
    {
        if (!clipActive || animationSpine == null)
        {
            return;
        }

        if (!IsPetrifying())
        {
            ApplyExitMode();
        }

        clipActive = false;
    }

    private void PlayClipAnimation()
    {
        if (animationSpine == null)
        {
            return;
        }

        if (IsPetrifying())
        {
            return;
        }

        skeletonAnimation = animationSpine.SkeletonAnimation;
        previousState = animationSpine.CurrentState;

        if (skeletonAnimation != null)
        {
            previousTimeScale = skeletonAnimation.timeScale;
        }

        animationSpine.SetDefaultMix(mixDuration);
        animationSpine.SetTimeScale(timeScale);
        ApplyFacingOverride();

        if (resetToSetupPoseBeforePlay && animationSpine.Skeleton != null)
        {
            animationSpine.Skeleton.SetToSetupPose();
        }

        TrackEntry entry = animationSpine.Play(state, loopAnimation, trackIndex);
        if (entry != null)
        {
            entry.Reverse = reverseAnimation;
        }

        clipActive = true;
    }

    private bool IsPetrifying()
    {
        return petrifyController != null && petrifyController.IsPetrifying;
    }

    private void ApplyExitMode()
    {
        if (restoreTimeScale)
        {
            animationSpine.SetTimeScale(previousTimeScale);
        }

        switch (exitMode)
        {
            case TimelineSpineAnimationExitMode.RestorePrevious:
                RestorePreviousAnimation();
                break;
            case TimelineSpineAnimationExitMode.ClearTrack:
                animationSpine.StopAnimation(trackIndex);
                break;
            case TimelineSpineAnimationExitMode.StopAll:
                animationSpine.StopAllAnimations();
                break;
        }
    }

    private void ApplyFacingOverride()
    {
        switch (facingOverride)
        {
            case TimelineSpineFacingOverride.FaceLeft:
                ApplyFacingSign(-1f);
                break;
            case TimelineSpineFacingOverride.FaceRight:
                ApplyFacingSign(1f);
                break;
        }
    }

    private void ApplyFacingSign(float sign)
    {
        if (animatedTransform == null || Mathf.Abs(sign) <= 0.001f)
        {
            return;
        }

        Vector3 scale = animatedTransform.localScale;
        if (player != null)
        {
            scale.x = Mathf.Abs(scale.x) * (sign > 0f ? -1f : 1f);
        }
        else
        {
            scale.x = Mathf.Abs(scale.x) * sign;
        }

        animatedTransform.localScale = scale;
    }

    private void RestorePreviousAnimation()
    {
        if (previousState != AnimationSpine.State.None
            && animationSpine.HasStateAnimation(previousState))
        {
            TrackEntry entry = animationSpine.Play(previousState, true, trackIndex);
            if (entry != null)
            {
                entry.Reverse = false;
            }

            return;
        }
    }
}
