using UnityEngine;

public sealed class BossAnimationAdapter
{
    private readonly MonoBehaviour animationBridge;
    private string currentAnimation;

    public BossAnimationAdapter(MonoBehaviour animationBridge)
    {
        this.animationBridge = animationBridge;
    }

    public string CurrentAnimation => currentAnimation;

    public void Play(string animationName, bool loop = false, bool restart = false)
    {
        if (animationBridge == null || string.IsNullOrEmpty(animationName))
        {
            return;
        }

        if (!restart && currentAnimation == animationName)
        {
            return;
        }

        currentAnimation = animationName;
        animationBridge.SendMessage("PlayAnimation", animationName, SendMessageOptions.DontRequireReceiver);
        animationBridge.SendMessage("SetLoop", loop, SendMessageOptions.DontRequireReceiver);
    }

    public void HoldFinalPose()
    {
        if (animationBridge == null)
        {
            return;
        }

        animationBridge.SendMessage("HoldFinalPose", null, SendMessageOptions.DontRequireReceiver);
    }

    public void Clear()
    {
        currentAnimation = null;
    }
}
