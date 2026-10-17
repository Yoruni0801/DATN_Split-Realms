using UnityEngine;

public enum ParryableAttackType
{
    Melee = 0,
    Projectile = 1
}

public enum ProjectileParryResponseMode
{
    Reflect = 0,
    Destroy = 1,
    ConvertToPlayer = 2
}

public readonly struct IncomingAttackData
{
    public IncomingAttackData(
        int damage,
        Transform sourceTransform,
        Vector2 hitPoint,
        Vector2 incomingDirection,
        ParryableAttackType attackType,
        bool canBeBlocked = true,
        bool isSlowAttack = false)
    {
        Damage = damage;
        SourceTransform = sourceTransform;
        HitPoint = hitPoint;
        IncomingDirection = incomingDirection;
        AttackType = attackType;
        CanBeBlocked = canBeBlocked;
        IsSlowAttack = isSlowAttack;
    }

    public int Damage { get; }
    public Transform SourceTransform { get; }
    public Vector2 HitPoint { get; }
    public Vector2 IncomingDirection { get; }
    public ParryableAttackType AttackType { get; }
    public bool CanBeBlocked { get; }
    public bool IsSlowAttack { get; }
}

public readonly struct ParryContext
{
    public ParryContext(
        Player player,
        Transform defenderTransform,
        Transform sourceTransform,
        Vector2 hitPoint,
        Vector2 incomingDirection,
        Vector2 defenderFacing,
        ParryableAttackType attackType,
        float defaultHitStopDuration)
    {
        Player = player;
        DefenderTransform = defenderTransform;
        SourceTransform = sourceTransform;
        HitPoint = hitPoint;
        IncomingDirection = incomingDirection;
        DefenderFacing = defenderFacing;
        AttackType = attackType;
        DefaultHitStopDuration = defaultHitStopDuration;
    }

    public Player Player { get; }
    public Transform DefenderTransform { get; }
    public Transform SourceTransform { get; }
    public Vector2 HitPoint { get; }
    public Vector2 IncomingDirection { get; }
    public Vector2 DefenderFacing { get; }
    public ParryableAttackType AttackType { get; }
    public float DefaultHitStopDuration { get; }
}

public readonly struct ParryResult
{
    public ParryResult(bool success, bool preventDamage, float hitStopDuration)
    {
        Success = success;
        PreventDamage = preventDamage;
        HitStopDuration = hitStopDuration;
    }

    public bool Success { get; }
    public bool PreventDamage { get; }
    public float HitStopDuration { get; }

    public static ParryResult Failed => new ParryResult(false, false, 0f);

    public static ParryResult Successful(float hitStopDuration, bool preventDamage = true)
        => new ParryResult(true, preventDamage, hitStopDuration);
}

public enum PlayerDefenseOutcome
{
    None = 0,
    PerfectParry = 1,
    Guard = 2
}

public readonly struct PlayerDefenseResult
{
    private PlayerDefenseResult(PlayerDefenseOutcome outcome, int damageToApply, ParryResult parryResult)
    {
        Outcome = outcome;
        DamageToApply = damageToApply;
        ParryResult = parryResult;
    }

    public PlayerDefenseOutcome Outcome { get; }
    public int DamageToApply { get; }
    public ParryResult ParryResult { get; }
    public bool PreventsDamage => Outcome == PlayerDefenseOutcome.PerfectParry && ParryResult.PreventDamage;

    public static PlayerDefenseResult None(int damage)
        => new PlayerDefenseResult(PlayerDefenseOutcome.None, damage, ParryResult.Failed);

    public static PlayerDefenseResult PerfectParry(ParryResult parryResult, int damageToApply)
        => new PlayerDefenseResult(PlayerDefenseOutcome.PerfectParry, damageToApply, parryResult);

    public static PlayerDefenseResult Guard(int damageToApply)
        => new PlayerDefenseResult(PlayerDefenseOutcome.Guard, damageToApply, ParryResult.Failed);
}

public enum IncomingAttackResolution
{
    Ignored = 0,
    Damaged = 1,
    Parried = 2,
    Guarded = 3
}
