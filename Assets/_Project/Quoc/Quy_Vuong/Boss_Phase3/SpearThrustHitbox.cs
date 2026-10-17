using UnityEngine;

namespace Quoc.QuyVuong.Boss_Phase3
{
    /// <summary>
    /// Spear Thrust Hitbox cho Skill 2 của Boss Phase 3.
    /// Dùng để đâm nhanh trúng player trong lane, có thể kết hợp làm hitbox trực tiếp hoặc projectile đâm.
    /// </summary>
    public class SpearThrustHitbox : MonoBehaviour
    {
        [Header("Damage Settings")]
        [SerializeField] private int damage = 30;

        [Header("VFX & Particle (Expose Inspector)")]
        [SerializeField] private ParticleSystem thrustVfxParticle;
        [SerializeField] private GameObject hitVfxPrefab;

        private bool hasHitPlayer = false;

        public void Initialize(int damageAmount)
        {
            damage = damageAmount;
            hasHitPlayer = false;
            if (thrustVfxParticle != null)
            {
                thrustVfxParticle.Play();
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            HandleCollision(collision);
        }

        private void HandleCollision(Collider2D collider)
        {
            if (hasHitPlayer || collider == null) return;
            if (PlayerHitResolver.IsThreatSensorCollider(collider)) return;

            if (collider.CompareTag("Player") || collider.GetComponentInParent<PlayerHealth>() != null)
            {
                hasHitPlayer = true;

                Vector2 knockbackDir = transform.right;

                PlayerHitResolver.TryApplyHitToPlayer(
                    collider,
                    damage,
                    ParryableAttackType.Melee,
                    transform,
                    knockbackDir
                );

                if (hitVfxPrefab != null)
                {
                    Instantiate(hitVfxPrefab, transform.position, Quaternion.identity);
                }
            }
        }
    }
}
