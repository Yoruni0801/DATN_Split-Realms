public sealed class BossStateDriver
{
    public bool IsHardLocked(BossStateId state)
    {
        return state == BossStateId.Attack ||
               state == BossStateId.ComboAttack ||
               state == BossStateId.DashAttack ||
               state == BossStateId.ParryCounter ||
               state == BossStateId.JumpAttack ||
               state == BossStateId.AttackRecovery ||
               state == BossStateId.PostAttackPause ||
               state == BossStateId.Parry ||
               state == BossStateId.RangedPressure ||
               state == BossStateId.SwordThrow ||
               state == BossStateId.JumpSlam ||
               state == BossStateId.Evade ||
               state == BossStateId.JumpEvade ||
               state == BossStateId.Hurt ||
               state == BossStateId.Stun ||
               state == BossStateId.Dead;
    }

    public bool IsAttacking(BossStateId state)
    {
        return state == BossStateId.Attack ||
               state == BossStateId.ComboAttack ||
               state == BossStateId.DashAttack ||
               state == BossStateId.ParryCounter ||
               state == BossStateId.JumpAttack;
    }
}
