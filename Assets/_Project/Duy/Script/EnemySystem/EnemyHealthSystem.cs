using System;

namespace EnemySystem
{
    public sealed class EnemyHealthSystem
    {
        public EnemyHealthSystem(float maxHealth)
        {
            MaxHealth = Math.Max(0f, maxHealth);
            CurrentHealth = MaxHealth;
        }

        public float MaxHealth { get; }
        public float CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0f;

        public HealthChangeResult TakeDamage(float damage)
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
            CurrentHealth = Math.Max(0f, Math.Min(currentHealth, MaxHealth));
        }
    }
}
