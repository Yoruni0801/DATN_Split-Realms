public sealed class ComboTracker
{
    private static readonly AnimationSpine.State[] NormalAttackStates =
    {
        AnimationSpine.State.NormalAttack1,
        AnimationSpine.State.NormalAttack2,
        AnimationSpine.State.NormalAttack3
    };

    private int step;
    private float lastAttackTime;

    public int Step => step;

    public void RegisterAttack(float time)
    {
        lastAttackTime = time;
    }

    public bool ShouldReset(float time, float resetWindow, bool isAttacking)
    {
        return !isAttacking && time - lastAttackTime > resetWindow;
    }

    public void Reset()
    {
        step = 0;
    }

    public bool TryGetNextAnimation(int normalDamage, out AnimationSpine.State animationState, out int damage)
    {
        animationState = AnimationSpine.State.None;
        damage = normalDamage;

        if (NormalAttackStates.Length == 0)
        {
            return false;
        }

        if (step >= NormalAttackStates.Length)
        {
            step = 0;
        }

        animationState = NormalAttackStates[step];
        step++;
        return animationState != AnimationSpine.State.None;
    }
}
