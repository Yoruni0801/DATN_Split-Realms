using System;
using UnityEngine;

namespace Quoc.QuyVuong.Phase1
{
    public class QuyVuongPhase1Arrow : MonoBehaviour
    {
        [Header("Arrow Settings")]
        [SerializeField] private int damage = 15;
        [SerializeField] private float speed = 14f;
        [SerializeField] private float lifetime = 5f;

        [Header("Visual & Particle Effects")]
        [SerializeField] private GameObject hitVfxPrefab;
        [SerializeField] private ParticleSystem trailParticleSystem;
        [SerializeField] private TrailRenderer trailRenderer;
        [SerializeField] private ParticleSystem spawnParticleSystem;

        private Vector2 moveDirection = Vector2.down;
        private Transform ownerTransform;
        private bool isSpawned = false;
        private GameObject customImpactVfx;

        public void Initialize(Vector2 direction, float customSpeed, int customDamage, Transform owner, GameObject impactVfx = null)
        {
            moveDirection = direction.normalized;
            speed = customSpeed;
            damage = customDamage;
            ownerTransform = owner;
            customImpactVfx = impactVfx;
            isSpawned = true;

            // Rotate arrow towards movement direction
            float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

            if (spawnParticleSystem != null)
            {
                spawnParticleSystem.Play();
            }
            if (trailParticleSystem != null)
            {
                trailParticleSystem.Play();
            }
            if (trailRenderer == null)
            {
                trailRenderer = GetComponent<TrailRenderer>() ?? GetComponentInChildren<TrailRenderer>();
            }
            if (trailRenderer != null)
            {
                trailRenderer.Clear();
                trailRenderer.enabled = true;
            }

            Destroy(gameObject, lifetime);
        }

        private void Update()
        {
            if (!isSpawned) return;

            float moveDist = speed * Time.deltaTime;

            // Quét Raycast2D lọc collider (chỉ nhận collider cứng, không phải Trigger, không phải của Boss)
            RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, moveDirection, moveDist + 0.2f);
            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;
                if (ownerTransform != null && hit.collider.transform.root == ownerTransform.root) continue;
                if (PlayerHitResolver.IsThreatSensorCollider(hit.collider)) continue;

                // Hit Player
                if (hit.collider.CompareTag("Player") || hit.collider.GetComponentInParent<PlayerHealth>() != null)
                {
                    PlayerHitResolver.TryApplyHitToPlayer(
                        hit.collider,
                        damage,
                        ParryableAttackType.Projectile,
                        ownerTransform != null ? ownerTransform : transform,
                        moveDirection,
                        true
                    );

                    SpawnHitEffect();
                    HideAndDestroy();
                    return;
                }

                // Hit Ground (không phải trigger)
                if (!hit.collider.isTrigger)
                {
                    bool isGround = ((1 << hit.collider.gameObject.layer) & PlayerLayerConfig.EnvironmentLayer) != 0
                                 || hit.collider.CompareTag("Ground")
                                 || hit.collider.gameObject.layer == 3
                                 || hit.collider.gameObject.name.ToLower().Contains("ground");

                    if (isGround)
                    {
                        SpawnHitEffect();
                        HideAndDestroy();
                        return;
                    }
                }
            }

            transform.Translate(moveDirection * moveDist, Space.World);
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
                HideAndDestroy();
                return;
            }

            // Chỉ nhận Ground nếu collider KHÔNG phải trigger (để tránh dính vào trigger zone của enemy/boss)
            if (collision.isTrigger) return;

            // Bắt tất cả các loại Ground: Layer nằm trong EnvironmentLayer HOẶC Tag là Ground HOẶC Layer Index 3 ("Ground")
            bool isGround = ((1 << collision.gameObject.layer) & PlayerLayerConfig.EnvironmentLayer) != 0
                         || collision.CompareTag("Ground")
                         || collision.gameObject.layer == 3
                         || collision.gameObject.name.ToLower().Contains("ground");

            if (isGround)
            {
                SpawnHitEffect();
                HideAndDestroy();
            }
        }

        private void HideAndDestroy()
        {
            isSpawned = false;
            if (TryGetComponent<SpriteRenderer>(out var sr))
            {
                sr.enabled = false;
            }
            if (TryGetComponent<Collider2D>(out var col))
            {
                col.enabled = false;
            }
            Destroy(gameObject, 0.5f);
        }

        private void SpawnHitEffect()
        {
            GameObject fxToSpawn = customImpactVfx != null ? customImpactVfx : hitVfxPrefab;
            if (fxToSpawn != null)
            {
                Instantiate(fxToSpawn, transform.position, Quaternion.identity);
            }
            if (trailParticleSystem != null)
            {
                trailParticleSystem.Stop();
            }
            if (trailRenderer != null)
            {
                trailRenderer.enabled = false;
            }
        }
    }
}
