using System;

public readonly struct BossCombatBrainInput
{
    public BossCombatBrainInput(
        bool hasTarget,
        bool isAggro,
        bool isHardLocked,
        float decisionTimer,
        float stateTimer,
        float distance,
        float attackRange,
        int directionToTarget,
        bool canDash,
        bool canAttack,
        float dashChance)
    {
        HasTarget = hasTarget;
        IsAggro = isAggro;
        IsHardLocked = isHardLocked;
        DecisionTimer = decisionTimer;
        StateTimer = stateTimer;
        Distance = distance;
        AttackRange = attackRange;
        DirectionToTarget = directionToTarget;
        CanDash = canDash;
        CanAttack = canAttack;
        DashChance = dashChance;
    }

    public bool HasTarget { get; }
    public bool IsAggro { get; }
    public bool IsHardLocked { get; }
    public float DecisionTimer { get; }
    public float StateTimer { get; }
    public float Distance { get; }
    public float AttackRange { get; }
    public int DirectionToTarget { get; }
    public bool CanDash { get; }
    public bool CanAttack { get; }
    public float DashChance { get; }
}

public sealed class BossCombatBrain
{
    private readonly Random random;

    public BossCombatBrain(int seed = 0)
    {
        random = seed == 0 ? new Random() : new Random(seed);
    }

    public BossActionRequest DecideOpeningAction(BossCombatBrainInput input)
    {
        if (input.IsHardLocked || (input.DecisionTimer > 0f && input.StateTimer > 0f))
        {
            return BossActionRequest.None;
        }

        if (!input.HasTarget)
        {
            return new BossActionRequest(BossActionType.Idle);
        }

        if (input.IsAggro && input.Distance > input.AttackRange)
        {
            if (input.CanDash && Roll(input.DashChance))
            {
                return new BossActionRequest(BossActionType.GapCloseDash, input.DirectionToTarget);
            }

            return new BossActionRequest(BossActionType.Chase, input.DirectionToTarget);
        }

        if (input.Distance <= input.AttackRange && input.CanAttack)
        {
            return new BossActionRequest(BossActionType.ComboAttack, input.DirectionToTarget);
        }

        return BossActionRequest.None;
    }

    private bool Roll(float chance)
    {
        if (chance <= 0f)
        {
            return false;
        }

        if (chance >= 1f)
        {
            return true;
        }

        return random.NextDouble() < chance;
    }
}
