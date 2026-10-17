public readonly struct BossDecisionConfig
{
    public BossDecisionConfig(
        float attackRange,
        float tooCloseDistance,
        float mediumDistance,
        float farDistance,
        float dashChance,
        float defenseChance)
    {
        AttackRange = attackRange;
        TooCloseDistance = tooCloseDistance;
        MediumDistance = mediumDistance;
        FarDistance = farDistance;
        DashChance = dashChance;
        DefenseChance = defenseChance;
    }

    public float AttackRange { get; }
    public float TooCloseDistance { get; }
    public float MediumDistance { get; }
    public float FarDistance { get; }
    public float DashChance { get; }
    public float DefenseChance { get; }
}
