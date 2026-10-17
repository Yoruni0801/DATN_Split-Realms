using UnityEngine;

namespace Bao.Bosses
{
    /// <summary>
    /// Simple projectile component for rocks thrown by Lithos Monolith.
    /// Moves in a direction, deals damage on contact, and auto-destroys.
    /// </summary>
    public class LithosRockProjectile : MonoBehaviour
    {
        private Vector2 direction;
        private float speed;
        private int damage;
        private float lifetime;
        private float spawnTime;
        private bool initialized;

        // Spin effect
        private float spinSpeed;

        public void Initialize(Vector2 dir, float spd, int dmg, float life)
        {
            direction = dir.normalized;
            speed = spd;
            damage = dmg;
            lifetime = life;
            spawnTime = Time.time;
            initialized = true;
            spinSpeed = Random.Range(200f, 500f) * (Random.value > 0.5f ? 1f : -1f);

            // Set rotation to face direction
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void Update()
        {
            if (!initialized) return;

            // Move
            transform.position += (Vector3)(direction * speed * Time.deltaTime);

            // Spin for visual flair
            transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

            // Auto-destroy
            if (Time.time - spawnTime > lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!initialized) return;

            // Don't hit the boss itself
            LithosMonolithBoss boss = other.GetComponent<LithosMonolithBoss>();
            if (boss != null) return;

            // Check for player damage
            Bao.Enemies.IDamageable damageable = other.GetComponent<Bao.Enemies.IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage);
            }

            // Check if we hit environment
            if (other.gameObject.layer == LayerMask.NameToLayer("Environment") ||
                other.gameObject.layer == LayerMask.NameToLayer("Ground"))
            {
                // Spawn impact VFX
                SpawnImpactVFX();
                Destroy(gameObject);
            }
        }

        private void SpawnImpactVFX()
        {
            // Simple procedural impact
            GameObject impact = new GameObject("RockImpact");
            impact.transform.position = transform.position;

            // Create a few debris sprites
            for (int i = 0; i < 4; i++)
            {
                GameObject debris = new GameObject($"Debris_{i}");
                debris.transform.SetParent(impact.transform);
                debris.transform.position = transform.position;

                SpriteRenderer sr = debris.AddComponent<SpriteRenderer>();
                sr.sprite = CreateSmallSquareSprite();
                sr.color = new Color(0.6f, 0.5f, 0.35f, 1f);
                sr.sortingOrder = 90;
                debris.transform.localScale = Vector3.one * Random.Range(0.1f, 0.3f);

                Rigidbody2D rb = debris.AddComponent<Rigidbody2D>();
                rb.gravityScale = 3f;
                float angle = Random.Range(30f, 150f) * Mathf.Deg2Rad;
                rb.linearVelocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(3f, 7f);
            }

            Destroy(impact, 1.5f);
        }

        private static Sprite _cachedSquare;
        private static Sprite CreateSmallSquareSprite()
        {
            if (_cachedSquare != null) return _cachedSquare;

            int size = 4;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = Color.white;
            tex.SetPixels(colors);
            tex.Apply();
            _cachedSquare = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _cachedSquare;
        }
    }
}
