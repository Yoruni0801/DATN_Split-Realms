using System;

public sealed class HealthPool
{
    public HealthPool(float maxHealth)
    {
        MaxHealth = Math.Max(0f, maxHealth);
        CurrentHealth = MaxHealth;
    }

    public float MaxHealth { get; }
    public float CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0f;

    public float TakeDamage(float damage)
    {
        return ApplyDamage(damage).CurrentHealth;
    }

    public HealthChangeResult ApplyDamage(float damage)
    {
        float previousHealth = CurrentHealth;
        float sanitizedDamage = Math.Max(0f, damage);

        if (IsDead || sanitizedDamage <= 0f)
        {
            return HealthChangeResult.NoChange(previousHealth, MaxHealth);
        }

        CurrentHealth = Math.Max(0f, CurrentHealth - sanitizedDamage);
        return HealthChangeResult.FromDamage(previousHealth, CurrentHealth, MaxHealth);
    }

    public void Reset()
    {
        CurrentHealth = MaxHealth;
    }

    public void SetCurrentHealth(float currentHealth)
    {
        CurrentHealth = Clamp(currentHealth, 0f, MaxHealth);
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }
}
