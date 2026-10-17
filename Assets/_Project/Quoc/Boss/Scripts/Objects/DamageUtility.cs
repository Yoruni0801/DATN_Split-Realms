using UnityEngine;

namespace Quoc.Boss
{
    public static class DamageUtility
    {
        public static bool DealDamage(GameObject target, int damage, Transform attacker = null)
        {
            if (target == null) return false;

            // Bỏ qua nếu mục tiêu chính là người tấn công hoặc con/root của người tấn công (chặn tự gây sát thương)
            if (attacker != null)
            {
                if (target.transform == attacker || target.transform.IsChildOf(attacker.root) || target.transform.root == attacker.root)
                {
                    return false;
                }
            }

            // 1. Check Quoc.Boss.IDamageable
            IDamageable bossDmg = target.GetComponentInParent<IDamageable>();
            if (bossDmg != null)
            {
                bossDmg.TakeDamage(damage);
                return true;
            }

            // 2. Check Bao.Enemies.IDamageable (used by PlayerHealth.cs)
            Bao.Enemies.IDamageable baoDmg = target.GetComponentInParent<Bao.Enemies.IDamageable>();
            if (baoDmg != null)
            {
                baoDmg.TakeDamage(damage);
                return true;
            }

            // 3. Check EnemySystem.IDamageable
            EnemySystem.IDamageable enemyDmg = target.GetComponentInParent<EnemySystem.IDamageable>();
            if (enemyDmg != null)
            {
                enemyDmg.TakeDamage(damage);
                return true;
            }

            // 4. SendMessage Fallback
            target.SendMessageUpwards("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
            return true;
        }
    }
}
