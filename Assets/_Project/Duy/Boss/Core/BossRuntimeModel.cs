public sealed class BossRuntimeModel
{
    public BossStateId State = BossStateId.Idle;
    public float CurrentHealth;
    public float StateTimer;
    public float DecisionTimer;
    public float AttackCooldownTimer;
    public float DashCooldownTimer;
    public float DashAttackCooldownTimer;
    public float ParryCooldownTimer;
    public float EvadeCooldownTimer;
    public float JumpCooldownTimer;
    public float HurtAnimationCooldownTimer;
    public float PoiseResetTimer;
    public float DefenseResetTimer;
    public float PendingThreatReactionTimer;
    public int PoiseHitCount;
    public int ConsecutiveParries;
    public int ConsecutiveDefenses;
    public int RecentRangedHits;
    public int FacingDirection = 1;
    public int MoveDirection = 1;
    public bool IsAggro;
    public bool HasPendingThreat;
    public BossThreatData PendingThreat;

    public bool IsAlive => State != BossStateId.Dead && CurrentHealth > 0f;
    public bool IsPhaseTwo(float maxHealth) => CurrentHealth <= maxHealth * 0.5f;

    public void Reset(float maxHealth)
    {
        State = BossStateId.Idle;
        CurrentHealth = maxHealth;
        StateTimer = 0f;
        DecisionTimer = 0f;
        AttackCooldownTimer = 0f;
        DashCooldownTimer = 0f;
        DashAttackCooldownTimer = 0f;
        ParryCooldownTimer = 0f;
        EvadeCooldownTimer = 0f;
        JumpCooldownTimer = 0f;
        HurtAnimationCooldownTimer = 0f;
        PoiseResetTimer = 0f;
        DefenseResetTimer = 0f;
        PendingThreatReactionTimer = 0f;
        PoiseHitCount = 0;
        ConsecutiveParries = 0;
        ConsecutiveDefenses = 0;
        RecentRangedHits = 0;
        FacingDirection = 1;
        MoveDirection = 1;
        IsAggro = false;
        HasPendingThreat = false;
    }
}
