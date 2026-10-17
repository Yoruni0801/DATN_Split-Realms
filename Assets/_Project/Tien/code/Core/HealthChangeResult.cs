using System;

public readonly struct HealthChangeResult
{
    private HealthChangeResult(float previousHealth, float currentHealth, float maxHealth, bool wasApplied)
    {
        PreviousHealth = previousHealth;
        CurrentHealth = currentHealth;
        MaxHealth = maxHealth;
        WasApplied = wasApplied;
    }

    public float PreviousHealth { get; }
    public float CurrentHealth { get; }
    public float MaxHealth { get; }
    public bool WasApplied { get; }
    public float DamageApplied => Math.Max(0f, PreviousHealth - CurrentHealth);
    public bool KilledTarget => PreviousHealth > 0f && CurrentHealth <= 0f;

    public static HealthChangeResult NoChange(float currentHealth, float maxHealth)
    {
        return new HealthChangeResult(currentHealth, currentHealth, maxHealth, false);
    }

    public static HealthChangeResult FromDamage(float previousHealth, float currentHealth, float maxHealth)
    {
        return new HealthChangeResult(previousHealth, currentHealth, maxHealth, true);
    }
}
