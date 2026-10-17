using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class TimelineNpcDamageEvent : UnityEvent<int, Transform>
{
}

public class TimelineNpcDamageReceiver : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 100;
    [SerializeField] private bool debugLogDamage = true;
    [SerializeField] private bool destroyOnDeath;
    [SerializeField] private UnityEvent onDeath;
    [SerializeField] private TimelineNpcDamageEvent onDamaged;

    private int currentHealth;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead { get; private set; }

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        ApplyDamage(damage, null);
    }

    public void TakeDamage(float damage)
    {
        ApplyDamage(Mathf.RoundToInt(damage), null);
    }

    public void TakeDamage(int damage, Transform attacker)
    {
        ApplyDamage(damage, attacker);
    }

    public void TakeDamage(float damage, Transform attacker)
    {
        ApplyDamage(Mathf.RoundToInt(damage), attacker);
    }

    public void TakeDamage(NpcTimelineDamageContext context)
    {
        ApplyDamage(context.Damage, context.Attacker);
    }

    private void ApplyDamage(int damage, Transform attacker)
    {
        if (IsDead || damage <= 0)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - damage);
        if (debugLogDamage)
        {
            Debug.Log($"[{nameof(TimelineNpcDamageReceiver)}] {name} took {damage} damage from {(attacker != null ? attacker.name : "Timeline")}. HP: {currentHealth}/{maxHealth}", this);
        }

        onDamaged?.Invoke(damage, attacker);

        if (currentHealth > 0)
        {
            return;
        }

        IsDead = true;
        onDeath?.Invoke();

        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
    }
}
