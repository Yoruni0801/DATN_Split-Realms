using Spine.Unity;
using UnityEngine;

public class Boss15Anim : MonoBehaviour
{
    [SerializeField] private SkeletonAnimation skeleton;

    private void Awake()
    {
        if (skeleton == null)
            skeleton = GetComponent<SkeletonAnimation>();
    }

    private void Start()
    {
        PlayIdle();
    }

    public void PlayIdle()
    {
        skeleton.AnimationState.SetAnimation(0, "Idle", true);
    }

    public void PlayRun()
    {
        skeleton.AnimationState.SetAnimation(0, "Run", true);
    }

    public void PlayAttack()
    {
        skeleton.AnimationState.SetAnimation(0, "Attack", false);
    }

    public void PlaySkill()
    {
        skeleton.AnimationState.SetAnimation(0, "Skill", false);
    }

    public void PlayDeath()
    {
        skeleton.AnimationState.SetAnimation(0, "Death", false);
    }
}