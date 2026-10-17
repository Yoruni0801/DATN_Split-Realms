using UnityEngine;

namespace EnemySystem
{
    public interface IDamageable
    {
        void TakeDamage(int damage);
        void Die();
    }
}
