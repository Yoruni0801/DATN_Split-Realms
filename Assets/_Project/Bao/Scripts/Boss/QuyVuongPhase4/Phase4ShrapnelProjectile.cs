using UnityEngine;

namespace Bao.Bosses.QuyVuongPhase4
{
    public class Phase4ShrapnelProjectile : MonoBehaviour
    {
        private Vector2 direction;
        private int damage;
        private float speed = 14f;
        private float lifetime = 3.5f;
        private bool isDead = false;
        private float spawnTime;

        public void Initialize(Vector2 dir, int dmg, float customSpeed = 14f)
        {
            direction = dir.normalized;
            damage = dmg;
            speed = customSpeed;
            spawnTime = Time.time;

            // Xoay đầu nhọn của Sprite theo đúng hướng bắn tỏa ra
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

            Invoke(nameof(OnLifetimeExpired), lifetime);
        }

        private void Update()
        {
            if (isDead) return;
            transform.position += (Vector3)(direction * speed * Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isDead) return;

            // Tránh va chạm ngay trong 0.05s đầu khi vừa spawn tại vị trí tảng đá
            if (Time.time - spawnTime < 0.05f) return;

            if (other.CompareTag("Player"))
            {
                QuyVuongPhase4SkillBase.ApplyPlayerDamage(other.gameObject, damage, 1f);
                OnHitOrExpire();
            }
            else if (other.CompareTag("Ground") || other.gameObject.layer == LayerMask.NameToLayer("Ground"))
            {
                OnHitOrExpire();
            }
        }

        private void OnLifetimeExpired()
        {
            if (!isDead) OnHitOrExpire();
        }

        private void OnHitOrExpire()
        {
            if (isDead) return;
            isDead = true;

            // 1. Tắt Collider để không nhận/gây thêm va chạm
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            // 2. Ẩn Sprite đầu đạn và quầng sáng gắn liền
            var spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
            foreach (var sr in spriteRenderers)
            {
                if (sr != null) sr.enabled = false;
            }

            // 3. Ngừng sinh vệt dải ruy-băng TrailRenderer
            var trails = GetComponentsInChildren<TrailRenderer>();
            foreach (var tr in trails)
            {
                if (tr != null) tr.emitting = false;
            }

            // 4. Ngừng phát hạt mới trên toàn bộ Particle Systems để hạt cũ tan dần ngoài World Space
            var particleSystems = GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particleSystems)
            {
                if (ps != null)
                {
                    var em = ps.emission;
                    em.enabled = false;
                }
            }

            // 5. Trì hoãn hủy đối tượng 0.45s để các dải trail và hạt world-space tàn dần tự nhiên
            Destroy(gameObject, 0.45f);
        }
    }
}
