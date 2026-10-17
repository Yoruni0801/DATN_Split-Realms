using System;
using UnityEngine;

namespace Quoc.QuyVuong.Boss_Phase1
{
    public class WaterBeamHazard : MonoBehaviour
    {
        [Header("Beam Hazard Settings")]
        [SerializeField] private int damage = 20;
        [SerializeField] private float duration = 2.5f;
        [SerializeField] private float damageInterval = 0.4f;

        private Transform ownerTransform;
        private float lastDamageTime = 0f;
        private bool isInitialized = false;

        public void Initialize(int customDamage, float customDuration, Transform owner)
        {
            damage = customDamage;
            duration = customDuration;
            ownerTransform = owner;
            isInitialized = true;

            Destroy(gameObject, duration);
        }

        private void OnTriggerStay2D(Collider2D collision)
        {
            if (!isInitialized) return;

            // Ignore boss colliders
            if (ownerTransform != null && collision.transform.root == ownerTransform.root) return;

            // Ignore threat sensors
            if (PlayerHitResolver.IsThreatSensorCollider(collision)) return;

            // Apply damage on interval to player
            if (Time.time >= lastDamageTime + damageInterval)
            {
                if (collision.CompareTag("Player") || collision.GetComponentInParent<PlayerHealth>() != null)
                {
                    PlayerHitResolver.TryApplyHitToPlayer(
                        collision,
                        damage,
                        ParryableAttackType.Projectile,
                        ownerTransform != null ? ownerTransform : transform,
                        Vector2.down,
                        true
                    );
                    lastDamageTime = Time.time;
                }
            }
        }
    }
}
