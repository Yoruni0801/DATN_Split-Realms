using Spine.Unity;
using UnityEngine;
using UnityEngine.Playables;

public class TimelineSkeletonGraphicBehaviour : PlayableBehaviour
{
    public string animationName;
    public bool loop;
    public int trackIndex;
    public float timeScale;
    public bool resetToSetupPose;
    public TimelineSkeletonGraphicExitMode exitMode;
    public bool restoreTimeScale;

    private SkeletonGraphic skeletonGraphic;
    private bool clipActive;
    private float previousTimeScale = 1f;

    public void Enter(SkeletonGraphic boundGraphic)
    {
        if (boundGraphic == null) return;

        if (skeletonGraphic != boundGraphic)
        {
            skeletonGraphic = boundGraphic;
            clipActive = false;
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
        if (!clipActive || skeletonGraphic == null) return;

        if (restoreTimeScale)
        {
            skeletonGraphic.timeScale = previousTimeScale;
        }

        switch (exitMode)
        {
            case TimelineSkeletonGraphicExitMode.ClearTrack:
                if (skeletonGraphic.AnimationState != null)
                {
                    skeletonGraphic.AnimationState.ClearTrack(trackIndex);
                }
                break;
            case TimelineSkeletonGraphicExitMode.ResetToSetupPose:
                if (skeletonGraphic.AnimationState != null)
                {
                    skeletonGraphic.AnimationState.ClearTrack(trackIndex);
                }
                if (skeletonGraphic.Skeleton != null)
                {
                    skeletonGraphic.Skeleton.SetToSetupPose();
                }
                break;
        }

        clipActive = false;
    }

    private void PlayClipAnimation()
    {
        if (skeletonGraphic == null || !skeletonGraphic.IsValid) return;

        previousTimeScale = skeletonGraphic.timeScale;
        skeletonGraphic.timeScale = timeScale;

        if (resetToSetupPose && skeletonGraphic.Skeleton != null)
        {
            skeletonGraphic.Skeleton.SetToSetupPose();
        }

        if (!string.IsNullOrEmpty(animationName))
        {
            skeletonGraphic.AnimationState?.SetAnimation(trackIndex, animationName, loop);
        }

        clipActive = true;
    }
}
