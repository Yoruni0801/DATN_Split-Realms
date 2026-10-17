using System;

public sealed class BossDecisionEngine
{
    private readonly Random random;

    public BossDecisionEngine(int seed = 0)
    {
        random = seed == 0 ? new Random() : new Random(seed);
    }

    public BossActionRequest Decide(
        BossRuntimeState runtime,
        BossSenseSnapshot sense,
        BossDecisionConfig config,
        bool canAttack,
        bool canDash,
        bool canDefend)
    {
        if (runtime == null || !runtime.IsAlive)
        {
            return new BossActionRequest(BossActionType.Die);
        }

        if (IsHardLocked(runtime.State))
        {
            return BossActionRequest.None;
        }

        if (sense.HasIncomingThreat && canDefend && Roll(config.DefenseChance))
        {
            return sense.IsGrounded
                ? new BossActionRequest(BossActionType.Evade, -sense.DirectionToTarget)
                : BossActionRequest.None;
        }

        if (!sense.HasTarget)
        {
            return new BossActionRequest(BossActionType.Idle);
        }

        if (runtime.IsAggro && sense.TargetDistance > config.AttackRange)
        {
            if (canDash && Roll(config.DashChance))
            {
                return new BossActionRequest(BossActionType.GapCloseDash, sense.DirectionToTarget);
            }

            return new BossActionRequest(BossActionType.Chase, sense.DirectionToTarget);
        }

        if (sense.TargetDistance <= config.AttackRange && canAttack)
        {
            return new BossActionRequest(BossActionType.Attack, sense.DirectionToTarget);
        }

        if (sense.TargetDistance <= config.TooCloseDistance)
        {
            return new BossActionRequest(BossActionType.Retreat, -sense.DirectionToTarget);
        }

        if (sense.TargetDistance <= config.MediumDistance)
        {
            if (canDash && Roll(config.DashChance))
            {
                return new BossActionRequest(BossActionType.Dash, sense.DirectionToTarget);
            }

            return new BossActionRequest(BossActionType.Approach, sense.DirectionToTarget);
        }

        return new BossActionRequest(
            sense.TargetDistance >= config.FarDistance ? BossActionType.Chase : BossActionType.Approach,
            sense.DirectionToTarget);
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

    private static bool IsHardLocked(BossStateId state)
    {
        return state == BossStateId.Dead ||
               state == BossStateId.Hurt ||
               state == BossStateId.Stun ||
               state == BossStateId.Attack ||
               state == BossStateId.ComboAttack ||
               state == BossStateId.DashAttack ||
               state == BossStateId.JumpAttack ||
               state == BossStateId.ParryCounter ||
               state == BossStateId.AttackRecovery ||
               state == BossStateId.PostAttackPause ||
               state == BossStateId.Parry ||
               state == BossStateId.RangedPressure ||
               state == BossStateId.SwordThrow ||
               state == BossStateId.JumpSlam ||
               state == BossStateId.Evade ||
               state == BossStateId.JumpEvade;
    }
}
