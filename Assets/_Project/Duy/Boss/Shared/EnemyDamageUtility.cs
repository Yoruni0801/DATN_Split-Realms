using UnityEngine;

public static class EnemyDamageUtility
{
    public static void ApplyDamage(IEnemyTarget enemy, float damage)
    {
        ApplyDamage(enemy, new DamageContext(damage));
    }

    public static void ApplyDamage(IEnemyTarget enemy, float damage, Transform attacker)
    {
        ApplyDamage(enemy, new DamageContext(damage, attacker));
    }

    public static void ApplyDamage(IEnemyTarget enemy, DamageContext context)
    {
        if (enemy == null)
        {
            return;
        }

        if (enemy is IDamageReceiver receiver)
        {
            receiver.TakeDamage(context);
            return;
        }

        enemy.TakeDamage(context.Damage);
    }
}
