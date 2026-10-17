using UnityEngine;

namespace Quoc.Boss
{
    /// <summary>
    /// Interface cho tất cả entity có thể nhận damage trong game.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(int damage);
    }
}
