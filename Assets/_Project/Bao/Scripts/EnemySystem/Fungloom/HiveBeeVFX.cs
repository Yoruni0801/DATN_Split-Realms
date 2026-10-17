using UnityEngine;

namespace Bao.Enemies
{
    /// <summary>
    /// Cosmetic-only presentation for the Fungloom bee projectile. It intentionally does not
    /// change projectile movement, collision, damage, or Animator state.
    /// </summary>
    [RequireComponent(typeof(HiveBeeProjectile))]
    public class HiveBeeVFX : MonoBehaviour
    {
        [Header("Toxic pollen palette")]
        [SerializeField] private Color pollenColor = new Color(0.65f, 1f, 0.18f, 0.72f);
        [SerializeField] private Color glowColor = new Color(0.95f, 1f, 0.48f, 0.36f);
        [SerializeField] private Color sparkColor = new Color(1f, 0.82f, 0.25f, 0.9f);

        [Header("Trail")]
        [SerializeField, Min(0.04f)] private float trailInterval = 0.09f;
        [SerializeField, Min(0f)] private float glowPulseAmount = 0.13f;

        private static Sprite softPollenSprite;
        private static Sprite sparkleSprite;

        private Rigidbody2D body;
        private SpriteRenderer[] beeRenderers;
        private Color[] originalColors;
        private int sortingOrder;
        private string sortingLayerName;
        private bool isShuttingDown;
        private float nextTrailTime;
        private float visualTime;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            beeRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            originalColors = new Color[beeRenderers.Length];

            for (int i = 0; i < beeRenderers.Length; i++)
            {
                originalColors[i] = beeRenderers[i].color;
                if (beeRenderers[i].sortingOrder >= sortingOrder || i == 0)
                {
                    sortingOrder = beeRenderers[i].sortingOrder;
                    sortingLayerName = beeRenderers[i].sortingLayerName;
                }
            }

            EnsureSprites();
        }

        private void Update()
        {
            visualTime += Time.deltaTime;
            PulseBeeGlow();

            if (Time.time >= nextTrailTime)
            {
                SpawnPollenTrail();
                nextTrailTime = Time.time + trailInterval * Random.Range(0.82f, 1.18f);
            }
        }

        private void PulseBeeGlow()
        {
            float pulse = 1f + Mathf.Sin(visualTime * 12f) * glowPulseAmount;
            for (int i = 0; i < beeRenderers.Length; i++)
            {
                if (beeRenderers[i] == null)
                {
                    continue;
                }

                Color source = originalColors[i];
                beeRenderers[i].color = new Color(
                    Mathf.Clamp01(source.r * pulse + pollenColor.r * 0.05f),
                    Mathf.Clamp01(source.g * pulse + pollenColor.g * 0.08f),
                    Mathf.Clamp01(source.b * pulse + pollenColor.b * 0.02f),
                    source.a);
            }
        }

        private void SpawnPollenTrail()
        {
            Vector2 velocity = body != null ? body.linearVelocity : Vector2.zero;
            Vector2 travelDirection = velocity.sqrMagnitude > 0.01f ? velocity.normalized : Vector2.left;
            Vector3 position = transform.position - (Vector3)(travelDirection * 0.30f)
                + (Vector3)(Random.insideUnitCircle * 0.09f);

            CreateParticle(
                "Bee Toxic Pollen",
                softPollenSprite,
                position,
                pollenColor,
                Random.Range(0.16f, 0.26f),
                Random.Range(0.35f, 0.52f),
                -travelDirection * Random.Range(0.12f, 0.32f) + Vector2.up * Random.Range(0.05f, 0.20f),
                sortingOrder - 2,
                0.65f);

            if (Random.value < 0.38f)
            {
                CreateParticle(
                    "Bee Gold Spark",
                    sparkleSprite,
                    position + (Vector3)(Random.insideUnitCircle * 0.11f),
                    sparkColor,
                    Random.Range(0.06f, 0.11f),
                    Random.Range(0.20f, 0.32f),
                    -travelDirection * Random.Range(0.10f, 0.20f) + Random.insideUnitCircle * 0.12f,
                    sortingOrder - 1,
                    0.15f);
            }
        }

        private void OnDestroy()
        {
            if (isShuttingDown)
            {
                return;
            }

            isShuttingDown = true;
            SpawnDissipateBurst();
        }

        private void SpawnDissipateBurst()
        {
            for (int i = 0; i < 8; i++)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                CreateParticle(
                    "Bee Dissipate Pollen",
                    i % 3 == 0 ? sparkleSprite : softPollenSprite,
                    transform.position + (Vector3)(direction * Random.Range(0.03f, 0.15f)),
                    i % 3 == 0 ? sparkColor : Color.Lerp(pollenColor, glowColor, Random.value),
                    Random.Range(0.10f, 0.22f),
                    Random.Range(0.25f, 0.48f),
                    direction * Random.Range(0.65f, 1.35f) + Vector2.up * 0.18f,
                    sortingOrder + 1,
                    0.30f);
            }
        }

        private void CreateParticle(
            string particleName,
            Sprite sprite,
            Vector3 position,
            Color color,
            float size,
            float duration,
            Vector2 drift,
            int order,
            float expandAmount)
        {
            GameObject particle = new GameObject(particleName);
            particle.transform.SetParent(VFXContainerUtility.GetContainer(), true);
            particle.transform.position = position;
            particle.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            particle.transform.localScale = Vector3.one * size;

            SpriteRenderer renderer = particle.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = order;

            BeePuffAnimator animator = particle.AddComponent<BeePuffAnimator>();
            animator.Initialize(renderer, size, duration, drift, expandAmount);
        }

        private static void EnsureSprites()
        {
            if (softPollenSprite != null && sparkleSprite != null)
            {
                return;
            }

            const int size = 32;
            Texture2D pollenTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Texture2D sparkleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2(x - 15.5f, y - 15.5f) / 15.5f;
                    float softAlpha = Mathf.Clamp01(1f - point.magnitude);
                    softAlpha *= softAlpha;
                    pollenTexture.SetPixel(x, y, new Color(1f, 1f, 1f, softAlpha));

                    float diamond = Mathf.Clamp01(1f - (Mathf.Abs(point.x) + Mathf.Abs(point.y)) * 1.35f);
                    sparkleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, diamond));
                }
            }

            pollenTexture.Apply(false, true);
            sparkleTexture.Apply(false, true);
            softPollenSprite = Sprite.Create(pollenTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sparkleSprite = Sprite.Create(sparkleTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }

    public class BeePuffAnimator : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private float initialSize;
        private float duration;
        private float elapsed;
        private float expandAmount;
        private Vector2 drift;
        private Color startColor;

        public void Initialize(SpriteRenderer renderer, float size, float lifetime, Vector2 movement, float expansion)
        {
            spriteRenderer = renderer;
            initialSize = size;
            duration = lifetime;
            drift = movement;
            expandAmount = expansion;
            startColor = renderer.color;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position += (Vector3)(drift * Time.deltaTime);
            transform.Rotate(0f, 0f, 85f * Time.deltaTime);
            transform.localScale = Vector3.one * initialSize * Mathf.Lerp(1f, 1f + expandAmount, t);

            Color color = startColor;
            color.a *= (1f - t) * (1f - t);
            spriteRenderer.color = color;

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
