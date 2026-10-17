public sealed class BossRuntimeState
{
    public BossStateId State { get; set; }
    public float CurrentHealth { get; set; }
    public float MaxHealth { get; set; }
    public int FacingDirection { get; set; } = 1;
    public int MoveDirection { get; set; }
    public bool IsPhaseTwo { get; set; }
    public bool IsAggro { get; set; }
    public bool HasTarget { get; set; }
    public bool IsAlive => CurrentHealth > 0f && State != BossStateId.Dead;

    public void Reset(float maxHealth)
    {
        MaxHealth = maxHealth;
        CurrentHealth = maxHealth;
        State = BossStateId.Idle;
        FacingDirection = 1;
        MoveDirection = 0;
        IsPhaseTwo = false;
        IsAggro = false;
        HasTarget = false;
    }
}
