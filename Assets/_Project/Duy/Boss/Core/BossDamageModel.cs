public enum BossDamageOutcome
{
    Ignored,
    Damaged,
    Parried,
    Interrupted,
    Died
}

public readonly struct BossDamageInput
{
    public BossDamageInput(
        BossStateId state,
        float damage,
        float currentHealth,
        bool isPhaseTwo,
        float distanceToTarget,
        int poiseHitCount,
        float hurtAnimationCooldownTimer,
        bool isStunned)
    {
        State = state;
        Damage = damage;
        CurrentHealth = currentHealth;
        IsPhaseTwo = isPhaseTwo;
        DistanceToTarget = distanceToTarget;
        PoiseHitCount = poiseHitCount;
        HurtAnimationCooldownTimer = hurtAnimationCooldownTimer;
        IsStunned = isStunned;
    }

    public BossStateId State { get; }
    public float Damage { get; }
    public float CurrentHealth { get; }
    public bool IsPhaseTwo { get; }
    public float DistanceToTarget { get; }
    public int PoiseHitCount { get; }
    public float HurtAnimationCooldownTimer { get; }
    public bool IsStunned { get; }
}

public readonly struct BossDamageResult
{
    public BossDamageResult(
        BossDamageOutcome outcome,
        float appliedDamage,
        float newHealth,
        int poiseHitCount,
        bool countedAsRangedHit)
    {
        Outcome = outcome;
        AppliedDamage = appliedDamage;
        NewHealth = newHealth;
        PoiseHitCount = poiseHitCount;
        CountedAsRangedHit = countedAsRangedHit;
    }

    public BossDamageOutcome Outcome { get; }
    public float AppliedDamage { get; }
    public float NewHealth { get; }
    public int PoiseHitCount { get; }
    public bool CountedAsRangedHit { get; }
}

public sealed class BossDamageModel
{
    private readonly BossConfig config;

    public BossDamageModel(BossConfig config)
    {
        this.config = config;
    }

    public BossDamageResult ApplyDamage(BossDamageInput input)
    {
        if (input.State == BossStateId.Dead || input.Damage <= 0f)
        {
            return new BossDamageResult(BossDamageOutcome.Ignored, 0f, input.CurrentHealth, input.PoiseHitCount, false);
        }

        bool countedAsRangedHit = input.DistanceToTarget > config.AttackRange;
        if (input.State == BossStateId.Parry)
        {
            return new BossDamageResult(BossDamageOutcome.Parried, 0f, input.CurrentHealth, input.PoiseHitCount, countedAsRangedHit);
        }

        float damage = HasAttackArmor(input.State)
            ? input.Damage * config.AttackingDamageTakenMultiplier
            : input.Damage;
        float newHealth = Max(0f, input.CurrentHealth - damage);
        if (newHealth <= 0f)
        {
            return new BossDamageResult(BossDamageOutcome.Died, damage, newHealth, input.PoiseHitCount, countedAsRangedHit);
        }

        int poiseHitCount = input.PoiseHitCount + 1;
        bool protectedAction = input.State == BossStateId.DashAttack || input.State == BossStateId.ParryCounter;
        int requiredPoise = Max(1, config.PoiseHitsBeforeInterrupt + (input.IsPhaseTwo ? config.PhaseTwoPoiseBonus : 0));
        bool canInterrupt = !protectedAction &&
            input.HurtAnimationCooldownTimer <= 0f &&
            poiseHitCount >= requiredPoise;

        return new BossDamageResult(
            canInterrupt && !input.IsStunned ? BossDamageOutcome.Interrupted : BossDamageOutcome.Damaged,
            damage,
            newHealth,
            canInterrupt && !input.IsStunned ? 0 : poiseHitCount,
            countedAsRangedHit);
    }

    private static bool HasAttackArmor(BossStateId state)
    {
        return state == BossStateId.Attack ||
               state == BossStateId.ComboAttack ||
               state == BossStateId.DashAttack ||
               state == BossStateId.ParryCounter;
    }

    private static float Max(float a, float b) => a > b ? a : b;
    private static int Max(int a, int b) => a > b ? a : b;
}
