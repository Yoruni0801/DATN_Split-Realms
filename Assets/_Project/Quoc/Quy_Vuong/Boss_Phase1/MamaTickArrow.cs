using System;
using UnityEngine;

namespace Quoc.QuyVuong.Boss_Phase1
{
    public class MamaTickArrow : MonoBehaviour
    {
        [Header("Arrow Settings")]
        [SerializeField] private int damage = 15;
        [SerializeField] private float speed = 12f;
        [SerializeField] private float lifetime = 5f;

        [Header("Visual & Particle Effects")]
        [SerializeField] private GameObject hitVfxPrefab;
        [SerializeField] private ParticleSystem trailParticleSystem;
        [SerializeField] private ParticleSystem spawnParticleSystem;

        private Vector2 moveDirection = Vector2.down;
        private Transform ownerTransform;
        private bool isSpawned = false;

        public void Initialize(Vector2 direction, float customSpeed, int customDamage, Transform owner)
        {
            moveDirection = direction.normalized;
            speed = customSpeed;
            damage = customDamage;
            ownerTransform = owner;
            isSpawned = true;

            // Rotate arrow towards movement direction
            float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

            // Play spawn and trail particles if assigned
            if (spawnParticleSystem != null)
            {
                spawnParticleSystem.Play();
            }
            if (trailParticleSystem != null)
            {
                trailParticleSystem.Play();
            }

            Destroy(gameObject, lifetime);
        }

        private void Update()
        {
            if (!isSpawned) return;
            transform.Translate(moveDirection * speed * Time.deltaTime, Space.World);
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!isSpawned) return;

            // Ignore boss colliders
            if (ownerTransform != null && collision.transform.root == ownerTransform.root) return;

            // Ignore threat sensors
            if (PlayerHitResolver.IsThreatSensorCollider(collision)) return;

            // Check if hit player
            if (collision.CompareTag("Player") || collision.GetComponentInParent<PlayerHealth>() != null)
            {
                PlayerHitResolver.TryApplyHitToPlayer(
                    collision,
                    damage,
                    ParryableAttackType.Projectile,
                    ownerTransform != null ? ownerTransform : transform,
                    moveDirection,
                    true
                );

                SpawnHitEffect();
                Destroy(gameObject);
                return;
            }

            // Hit ground/obstacle environment
            if (((1 << collision.gameObject.layer) & PlayerLayerConfig.EnvironmentLayer) != 0)
            {
                SpawnHitEffect();
                Destroy(gameObject);
            }
        }

        private void SpawnHitEffect()
        {
            if (hitVfxPrefab != null)
            {
                Instantiate(hitVfxPrefab, transform.position, Quaternion.identity);
            }
            if (trailParticleSystem != null)
            {
                trailParticleSystem.Stop();
            }
        }
    }
}
