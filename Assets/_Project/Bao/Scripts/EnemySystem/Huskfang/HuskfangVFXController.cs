using UnityEngine;

namespace Bao.Enemies
{
    /// <summary>
    /// Visual-only presentation for Huskfang: living briar glow, drifting spores and a toxic
    /// charge burst. Combat state is observed only; no movement, damage or state logic is changed.
    /// </summary>
    [RequireComponent(typeof(HuskfangEnemy))]
    public class HuskfangVFXController : MonoBehaviour
    {
        [Header("Living Briar shader")]
        [SerializeField] private Shader briarShader;
        [SerializeField] private Color sapColor = new Color(0.42f, 1f, 0.16f, 1f);
        [SerializeField, Range(0f, 10f)] private float idleVenomLevel = 0.9f;
        [SerializeField, Range(0f, 10f)] private float attackVenomLevel = 5.5f;

        [Header("Spore palette")]
        [SerializeField] private Color mossSporeColor = new Color(0.55f, 0.93f, 0.16f, 0.55f);
        [SerializeField] private Color emberSapColor = new Color(1f, 0.45f, 0.08f, 0.76f);
        [SerializeField, Min(0.03f)] private float attackSporeInterval = 0.075f;
        [SerializeField, Min(0.05f)] private float idleSporeInterval = 0.26f;

        private static Sprite softSporeSprite;
        private static Sprite thornSparkSprite;

        private HuskfangEnemy huskfang;
        private SpriteRenderer[] renderers;
        private Material[] briarMaterials;
        private int highestSortingOrder;
        private string sortingLayerName;
        private bool wasAttacking;
        private float nextSporeTime;
        private float currentVenom;

        private void Awake()
        {
            huskfang = GetComponent<HuskfangEnemy>();
            renderers = huskfang.GetVisualSpriteRenderers();
            CacheSorting();
            EnsureParticleSprites();
            SetupBriarMaterials();
        }

        private void Update()
        {
            bool isAttacking = huskfang.StateMachine != null
                && huskfang.StateMachine.CurrentState is HuskfangRootAttackState;

            float idlePulse = idleVenomLevel + Mathf.Sin(Time.time * 2.35f) * 0.18f;
            float targetVenom = isAttacking ? attackVenomLevel : idlePulse;
            currentVenom = Mathf.MoveTowards(currentVenom, targetVenom, Time.deltaTime * (isAttacking ? 14f : 2.2f));
            ApplyBriarParameters();

            if (isAttacking && !wasAttacking)
            {
                SpawnChargeCrown();
            }
            wasAttacking = isAttacking;

            float interval = isAttacking ? attackSporeInterval : idleSporeInterval;
            if (Time.time >= nextSporeTime)
            {
                if (isAttacking || Random.value < 0.58f)
                {
                    SpawnBodySpore(isAttacking);
                }
                nextSporeTime = Time.time + interval * Random.Range(0.82f, 1.2f);
            }
        }

        private void OnDestroy()
        {
            if (briarMaterials == null)
            {
                return;
            }

            for (int i = 0; i < briarMaterials.Length; i++)
            {
                if (briarMaterials[i] != null)
                {
                    Destroy(briarMaterials[i]);
                }
            }
        }

        private void CacheSorting()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || (i > 0 && renderers[i].sortingOrder < highestSortingOrder))
                {
                    continue;
                }

                highestSortingOrder = renderers[i].sortingOrder;
                sortingLayerName = renderers[i].sortingLayerName;
            }
        }

        private void SetupBriarMaterials()
        {
            if (briarShader == null)
            {
                briarShader = Shader.Find("Bao/HuskfangBriarShader");
            }
            if (briarShader == null)
            {
                Debug.LogWarning("Huskfang briar shader could not be found.", this);
                return;
            }

            briarMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                Material material = new Material(briarShader);
                material.SetColor("_SapColor", sapColor);
                material.SetFloat("_VenomLevel", 0f);
                renderers[i].material = material;
                briarMaterials[i] = material;
            }
        }

        private void ApplyBriarParameters()
        {
            if (briarMaterials == null)
            {
                return;
            }

            for (int i = 0; i < briarMaterials.Length; i++)
            {
                if (briarMaterials[i] != null)
                {
                    briarMaterials[i].SetFloat("_VenomLevel", currentVenom);
                    briarMaterials[i].SetColor("_SapColor", Color.Lerp(sapColor, emberSapColor, Mathf.Clamp01((currentVenom - 1f) / 7f)));
                }
            }
        }

        private void SpawnBodySpore(bool isAttacking)
        {
            Vector2 orbit = Random.insideUnitCircle;
            Vector3 position = transform.position + new Vector3(orbit.x * 0.92f, 1.15f + orbit.y * 0.75f, 0f);
            Color color = isAttacking && Random.value < 0.28f ? emberSapColor : mossSporeColor;
            Vector2 drift = new Vector2(orbit.x * 0.28f, Random.Range(0.25f, 0.65f));

            SpawnParticle(
                "Huskfang Spore",
                softSporeSprite,
                position,
                color,
                Random.Range(0.075f, 0.16f),
                Random.Range(0.55f, 1.05f),
                drift,
                highestSortingOrder + 1,
                Random.Range(0.45f, 0.95f));
        }

        private void SpawnChargeCrown()
        {
            for (int i = 0; i < 16; i++)
            {
                float angle = i / 16f * Mathf.PI * 2f + Random.Range(-0.09f, 0.09f);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector3 position = transform.position + new Vector3(direction.x * 0.76f, 1.22f + direction.y * 0.70f, 0f);

                SpawnParticle(
                    "Huskfang Thorn Spark",
                    i % 3 == 0 ? thornSparkSprite : softSporeSprite,
                    position,
                    i % 3 == 0 ? emberSapColor : mossSporeColor,
                    Random.Range(0.08f, 0.17f),
                    Random.Range(0.32f, 0.58f),
                    direction * Random.Range(0.7f, 1.35f) + Vector2.up * 0.15f,
                    highestSortingOrder + 2,
                    0.35f);
            }
        }

        private void SpawnParticle(string name, Sprite sprite, Vector3 position, Color color, float size, float lifetime, Vector2 drift, int sortingOrder, float expansion)
        {
            GameObject particle = new GameObject(name);
            particle.transform.SetParent(VFXContainerUtility.GetContainer(), true);
            particle.transform.position = position;
            particle.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            particle.transform.localScale = Vector3.one * size;

            SpriteRenderer renderer = particle.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;

            HuskfangSporeMotion motion = particle.AddComponent<HuskfangSporeMotion>();
            motion.Initialize(renderer, size, lifetime, drift, expansion);
        }

        private static void EnsureParticleSprites()
        {
            if (softSporeSprite != null && thornSparkSprite != null)
            {
                return;
            }

            const int textureSize = 32;
            Texture2D sporeTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            Texture2D sparkTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    Vector2 point = new Vector2(x - 15.5f, y - 15.5f) / 15.5f;
                    float softAlpha = Mathf.Clamp01(1f - point.magnitude);
                    sporeTexture.SetPixel(x, y, new Color(1f, 1f, 1f, softAlpha * softAlpha));

                    float thornAlpha = Mathf.Clamp01(1f - (Mathf.Abs(point.x) * 0.55f + Mathf.Abs(point.y) * 1.75f));
                    sparkTexture.SetPixel(x, y, new Color(1f, 1f, 1f, thornAlpha));
                }
            }

            sporeTexture.Apply(false, true);
            sparkTexture.Apply(false, true);
            softSporeSprite = Sprite.Create(sporeTexture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
            thornSparkSprite = Sprite.Create(sparkTexture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
        }
    }

    public class HuskfangSporeMotion : MonoBehaviour
    {
        private SpriteRenderer renderer;
        private float initialSize;
        private float lifetime;
        private float elapsed;
        private float expansion;
        private Vector2 drift;
        private Color startColor;

        public void Initialize(SpriteRenderer particleRenderer, float size, float duration, Vector2 velocity, float expandAmount)
        {
            renderer = particleRenderer;
            initialSize = size;
            lifetime = duration;
            drift = velocity;
            expansion = expandAmount;
            startColor = particleRenderer.color;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lifetime);
            transform.position += (Vector3)(drift * Time.deltaTime);
            transform.Rotate(0f, 0f, 75f * Time.deltaTime);
            transform.localScale = Vector3.one * initialSize * Mathf.Lerp(1f, 1f + expansion, t);

            Color color = startColor;
            color.a *= (1f - t) * (1f - t);
            renderer.color = color;
            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
