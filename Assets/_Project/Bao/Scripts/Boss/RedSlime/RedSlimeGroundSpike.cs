using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Bao.Bosses
{
    /// <summary>
    /// Component gai trồi lên từ lòng đất (Gai_RedSlime) tạo cảm giác cực kỳ uy lực & nguy hiểm:
    /// 1. Giai đoạn Teaser (Ground Rumbling): Rung lắc mặt đất, khói bụi trào lên (0.10s).
    /// 2. Giai đoạn Emerge (Impale Burst): Phóng vút từ dưới lòng đất đâm xuyên lên với tốc độ cực đại (Overshoot) + Hất tung đối thủ.
    /// 3. Giai đoạn Active: Gai đứng vững đỏ rực đe dọa.
    /// 4. Giai đoạn Retract: Rút nhanh dứt khoát xuống đất.
    /// </summary>
    public class RedSlimeGroundSpike : MonoBehaviour
    {
        [Header("Damage & Timing")]
        [SerializeField] private int damage = 16;
        [SerializeField] private float lifetime = 1.6f;
        [SerializeField] private float warningDuration = 0.10f;
        [SerializeField] private float emergeDuration = 0.08f;
        [SerializeField] private float retractDuration = 0.18f;

        [Header("Impale Physics")]
        [SerializeField] private float emergeDepth = 3.5f;
        [SerializeField] private float knockbackForce = 10f;

        private bool hasDealtDamage = false;
        private HashSet<int> sharedHitTracker;
        private SpriteRenderer spriteRenderer;
        private Collider2D spikeCollider;
        private Vector3 finalPosition;
        private Vector3 undergroundPosition;
        private Vector3 originalScale;

        private void Awake()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            spikeCollider = GetComponent<Collider2D>();
            if (spikeCollider == null) spikeCollider = GetComponentInChildren<Collider2D>();
            if (spikeCollider != null) spikeCollider.isTrigger = true;
        }

        public void Initialize(int spikeDamage, float spikeLifetime, HashSet<int> hitTracker = null)
        {
            damage = spikeDamage;
            lifetime = Mathf.Max(1.0f, spikeLifetime);
            hasDealtDamage = false;
            sharedHitTracker = hitTracker;

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            finalPosition = transform.position;
            // Di chuyển thẳng đứng xuống dưới lòng đất theo trục Global Y
            undergroundPosition = finalPosition + Vector3.down * emergeDepth;
            originalScale = transform.localScale;

            // Bắt đầu từ dưới đất
            transform.position = undergroundPosition;

            StartCoroutine(ViolentSpikeRoutine());
        }

        private IEnumerator ViolentSpikeRoutine()
        {
            // ── GIAI ĐOẠN 1: CẢNH BÁO RUNG LẮC MẶT ĐẤT (Ground Tremor) ──
            float elapsed = 0f;
            SpawnGroundDebris(finalPosition, 4);

            while (elapsed < warningDuration)
            {
                elapsed += Time.deltaTime;
                transform.position = undergroundPosition + (Vector3)(Random.insideUnitCircle * 0.04f);
                yield return null;
            }

            // ── GIAI ĐOẠN 2: PHÓNG VÚT LÊN ĐÂM XUYÊN CỰC MẠNH (Violent Emerge) ──
            elapsed = 0f;
            SpawnGroundDebris(finalPosition, 8);
            TriggerLocalShake(0.12f, 0.10f);
            TriggerSpikeSFX();

            if (spikeCollider != null) spikeCollider.enabled = true;

            while (elapsed < emergeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / emergeDuration);

                // Back Out Easing (Phóng vút nhô lên dứt khoát)
                float c1 = 1.70158f;
                float c3 = c1 + 1f;
                float easeBack = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);

                transform.position = Vector3.LerpUnclamped(undergroundPosition, finalPosition, easeBack);

                float stretchY = Mathf.Lerp(1.2f, 1.0f, t);
                transform.localScale = new Vector3(originalScale.x, originalScale.y * stretchY, originalScale.z);

                yield return null;
            }

            transform.position = finalPosition;
            transform.localScale = originalScale;

            if (spriteRenderer != null)
            {
                StartCoroutine(SpikeGlowPulse());
            }

            // ── GIAI ĐOẠN 3: DUY TRÌ GÂY SÁT THƯƠNG ĐỨNG VỮNG (Active Stand) ──
            float activeStayTime = Mathf.Max(0.3f, lifetime - warningDuration - emergeDuration - retractDuration);
            yield return new WaitForSeconds(activeStayTime);

            // ── GIAI ĐOẠN 4: RÚT NHANH XUỐNG LÒNG ĐẤT (Retract) ──
            elapsed = 0f;
            Color initColor = spriteRenderer != null ? spriteRenderer.color : Color.white;

            while (elapsed < retractDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / retractDuration);
                float easeIn = t * t;

                transform.position = Vector3.Lerp(finalPosition, undergroundPosition, easeIn);

                if (spriteRenderer != null)
                {
                    Color c = initColor;
                    c.a = Mathf.Lerp(initColor.a, 0f, easeIn);
                    spriteRenderer.color = c;
                }

                yield return null;
            }

            Destroy(gameObject);
        }

        private IEnumerator SpikeGlowPulse()
        {
            Color origColor = spriteRenderer.color;
            Color threatColor = new Color(3.0f, 2.0f, 0.2f, 1f); // Radiant Solar Amber Threat Pulse
            spriteRenderer.color = threatColor;

            float pulseTime = 0.15f;
            float elapsed = 0f;
            while (elapsed < pulseTime)
            {
                elapsed += Time.deltaTime;
                spriteRenderer.color = Color.Lerp(threatColor, origColor, elapsed / pulseTime);
                yield return null;
            }
            spriteRenderer.color = origColor;
        }

        private void SpawnGroundDebris(Vector3 groundPos, int count)
        {
            GameObject debrisRoot = new GameObject("SpikeDebris");
            debrisRoot.transform.position = groundPos;

            List<SpriteRenderer> debris = new List<SpriteRenderer>();
            List<Vector2> vels = new List<Vector2>();

            for (int i = 0; i < count; i++)
            {
                GameObject d = new GameObject($"deb_{i}");
                d.transform.SetParent(debrisRoot.transform);
                d.transform.position = groundPos;

                SpriteRenderer sr = d.AddComponent<SpriteRenderer>();
                sr.sprite = CreateDustSprite();
                sr.color = new Color(1.0f, 0.75f, 0.15f, 0.9f); // Amber Gold Dust Particles
                sr.sortingOrder = 90;
                d.transform.localScale = Vector3.one * Random.Range(0.12f, 0.3f);

                float angle = Random.Range(30f, 150f);
                float speed = Random.Range(3f, 7f);
                Vector2 vel = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad) * speed, Mathf.Sin(angle * Mathf.Deg2Rad) * speed);

                debris.Add(sr);
                vels.Add(vel);
            }

            StartCoroutine(AnimateDebris(debrisRoot, debris, vels, 0.35f));
        }

        private IEnumerator AnimateDebris(GameObject root, List<SpriteRenderer> debris, List<Vector2> vels, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                for (int i = 0; i < debris.Count; i++)
                {
                    if (debris[i] == null) continue;
                    vels[i] += Vector2.down * 18f * Time.deltaTime;
                    debris[i].transform.position += (Vector3)(vels[i] * Time.deltaTime);

                    Color c = debris[i].color;
                    c.a = Mathf.Lerp(0.9f, 0f, t);
                    debris[i].color = c;
                }
                yield return null;
            }

            if (root != null) Destroy(root);
        }

        private void TriggerLocalShake(float duration, float intensity)
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            RedSlimeBoss boss = FindFirstObjectByType<RedSlimeBoss>();
            if (boss != null && boss.ShaderController != null)
            {
                boss.ShaderController.TriggerScreenShake(duration, intensity);
            }
        }

        private void TriggerSpikeSFX()
        {
            RedSlimeBoss boss = FindFirstObjectByType<RedSlimeBoss>();
            if (boss != null && boss.AudioController != null)
            {
                boss.AudioController.PlaySpikeEruptSound(finalPosition);
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            TryDealDamage(collision);
        }

        private void OnTriggerStay2D(Collider2D collision)
        {
            TryDealDamage(collision);
        }

        private void TryDealDamage(Collider2D collision)
        {
            if (hasDealtDamage) return;

            PlayerHealth playerHealth = collision.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null || collision.CompareTag("Player"))
            {
                GameObject rootGo = collision.transform.root.gameObject;
                int id = rootGo.GetInstanceID();

                if (sharedHitTracker != null && sharedHitTracker.Contains(id))
                {
                    return;
                }

                IncomingAttackResolution resolution = PlayerHitResolver.TryApplyHitToPlayer(
                    collision,
                    damage,
                    ParryableAttackType.Melee,
                    EnemyParryResponder.Ensure(this),
                    transform,
                    Vector2.up);

                if (resolution != IncomingAttackResolution.Ignored)
                {
                    hasDealtDamage = true;
                    if (sharedHitTracker != null)
                    {
                        sharedHitTracker.Add(id);
                    }

                    // Hất tung lên không trung
                    Rigidbody2D playerRb = collision.GetComponent<Rigidbody2D>();
                    if (playerRb == null) playerRb = collision.GetComponentInParent<Rigidbody2D>();
                    if (playerRb != null)
                    {
                        Vector2 knockDir = (transform.up + Vector3.up * 0.8f).normalized;
                        playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x * 0.3f, 0f);
                        playerRb.AddForce(knockDir * knockbackForce, ForceMode2D.Impulse);
                    }
                }
            }
        }

        private static Sprite _cachedDust;
        private static Sprite CreateDustSprite()
        {
            if (_cachedDust != null) return _cachedDust;

            int size = 16;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            float radius = size / 2f;
            Vector2 center = new Vector2(radius, radius);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= radius)
                    {
                        float a = 1f - (dist / radius);
                        colors[y * size + x] = new Color(1f, 1f, 1f, a);
                    }
                    else
                    {
                        colors[y * size + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            _cachedDust = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _cachedDust;
        }
    }
}
