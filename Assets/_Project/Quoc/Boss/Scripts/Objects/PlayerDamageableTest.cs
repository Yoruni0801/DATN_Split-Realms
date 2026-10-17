using UnityEngine;

namespace Quoc.Boss
{
    /// <summary>
    /// Component mẫu gắn vào Player GameObject để nhận damage thử nghiệm
    /// </summary>
    public class PlayerDamageableTest : MonoBehaviour, IDamageable
    {
        [Header("Player Health Stats")]
        [SerializeField] private int currentHealth = 100;
        [SerializeField] private int maxHealth = 100;

        public void TakeDamage(int damage)
        {
            currentHealth = Mathf.Max(0, currentHealth - damage);
            Debug.Log($"[PlayerDamageableTest] Player nhận {damage} damage! HP còn lại: {currentHealth}/{maxHealth}");
            if (currentHealth <= 0)
            {
                Debug.Log("[PlayerDamageableTest] Player đã bị hạ gục!");
            }
        }
    }
}
