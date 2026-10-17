using UnityEngine;

namespace Bao.Enemies
{
    /// <summary>
    /// Runtime presentation for Ashwing's single Cinder Razor projectile.
    /// The damage/lifetime component on the projectile prefab remains untouched.
    /// </summary>
    public class AshwingFeatherVFX : MonoBehaviour
    {
        [Header("Cinder Razor Colors")]
        [SerializeField] private Color coreColor = new Color(1f, 0.82f, 0.30f, 1f);
        [SerializeField] private Color fireColor = new Color(1f, 0.28f, 0.03f, 1f);
        [SerializeField] private Color sparkColor = new Color(1f, 0.72f, 0.12f, 1f);

        [Header("Feather Motion")]
        [SerializeField] private float afterimageInterval = 0.045f;
        [SerializeField] private float sparkInterval = 0.055f;

        [Header("Trail")]
        [SerializeField] private float trailTime = 0.16f;
        [SerializeField] private float trailStartWidth = 0.09f;

        private Rigidbody2D rb;
        private SpriteRenderer sourceRenderer;
        private TrailRenderer trail;
        private float afterimageTimer;
        private float sparkTimer;
        private bool impactPlayed;
        private float spawnedAt;

        private void Awake()
        {
            AshwingVFXRuntime.EnsureAssets();
            rb = GetComponent<Rigidbody2D>();
            sourceRenderer = GetComponent<SpriteRenderer>();
            spawnedAt = Time.time;
        }

        private void Start()
        {
            CreateRazorTrail();
        }

        private void Update()
        {
            afterimageTimer += Time.deltaTime;
            if (afterimageTimer >= afterimageInterval)
            {
                afterimageTimer = 0f;
                SpawnAfterimage();
            }

            sparkTimer += Time.deltaTime;
            if (sparkTimer >= sparkInterval)
            {
                sparkTimer = 0f;
                SpawnTrailEmber();
            }
        }

        private void CreateRazorTrail()
        {
            trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = trailTime;
            trail.startWidth = trailStartWidth;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.03f;
            trail.textureMode = LineTextureMode.Stretch;
            trail.alignment = LineAlignment.View;
            trail.numCapVertices = 2;
            trail.numCornerVertices = 2;
            trail.sharedMaterial = AshwingVFXRuntime.SpriteMaterial;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(coreColor, 0.22f),
                    new GradientColorKey(fireColor, 0.65f),
                    new GradientColorKey(new Color(0.35f, 0.02f, 0f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.72f, 0.35f),
                    new GradientAlphaKey(0f, 1f)
                });
            trail.colorGradient = gradient;

            if (sourceRenderer != null)
            {
                trail.sortingLayerID = sourceRenderer.sortingLayerID;
                trail.sortingOrder = sourceRenderer.sortingOrder;
            }
        }

        private void SpawnAfterimage()
        {
            if (sourceRenderer == null || sourceRenderer.sprite == null)
                return;

            GameObject ghost = new GameObject("CinderRazorAfterimage");
            ghost.transform.SetParent(VFXContainerUtility.GetContainer());
            ghost.transform.position = transform.position;
            ghost.transform.rotation = transform.rotation;
            ghost.transform.localScale = transform.lossyScale;

            SpriteRenderer ghostRenderer = ghost.AddComponent<SpriteRenderer>();
            ghostRenderer.sprite = sourceRenderer.sprite;
            ghostRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
            ghostRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            ghostRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
            ghostRenderer.color = new Color(fireColor.r, fireColor.g, fireColor.b, 0.34f);

            Vector3 drift = Vector3.zero;
            if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f)
            {
                drift = -(Vector3)rb.linearVelocity.normalized * 0.65f;
            }

            AshwingTransientSpriteVFX fx = ghost.AddComponent<AshwingTransientSpriteVFX>();
            fx.Initialize(
                ghostRenderer,
                0.14f,
                drift,
                0f,
                new Color(fireColor.r * 0.45f, 0f, 0f, 0f),
                ghost.transform.localScale * 0.72f);
        }

        private void SpawnTrailEmber()
        {
            Vector3 backwards = Vector3.left;
            if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f)
            {
                backwards = -(Vector3)rb.linearVelocity.normalized;
            }

            Vector3 position = transform.position + (Vector3)(Random.insideUnitCircle * 0.16f);
            Vector3 velocity = backwards * Random.Range(1.8f, 3.4f)
                             + Vector3.up * Random.Range(0.35f, 1.25f)
                             + Vector3.right * Random.Range(-0.35f, 0.35f);

            SpawnEmber(position, velocity, Random.Range(0.10f, 0.18f), Random.Range(0.22f, 0.38f));
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (impactPlayed || Time.time - spawnedAt < 0.06f)
                return;

            impactPlayed = true;
            Vector2 direction = rb != null && rb.linearVelocity.sqrMagnitude > 0.01f
                ? rb.linearVelocity.normalized
                : (Vector2)transform.right;

            SpawnImpact(transform.position, direction);
        }

        private void SpawnImpact(Vector3 position, Vector2 direction)
        {
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // White-hot core flash.
            SpawnTransientSprite(
                "CinderRazorImpactFlash",
                AshwingVFXRuntime.CircleSprite,
                position,
                Quaternion.identity,
                Vector3.one * 0.25f,
                new Color(1f, 0.94f, 0.62f, 0.95f),
                0.12f,
                Vector3.zero,
                0f,
                new Color(1f, 0.18f, 0f, 0f),
                Vector3.one * 1.15f,
                240);

            // Two compact crossing razor slashes instead of a boss-sized explosion.
            for (int i = 0; i < 2; i++)
            {
                float angle = baseAngle + (i == 0 ? 48f : -48f);
                SpawnTransientSprite(
                    "CinderRazorImpactSlash",
                    AshwingVFXRuntime.StreakSprite,
                    position,
                    Quaternion.Euler(0f, 0f, angle),
                    new Vector3(1.15f, 0.16f, 1f),
                    i == 0 ? coreColor : fireColor,
                    0.18f,
                    Vector3.zero,
                    i == 0 ? 80f : -80f,
                    new Color(fireColor.r, 0f, 0f, 0f),
                    new Vector3(1.5f, 0.04f, 1f),
                    239);
            }

            for (int i = 0; i < 9; i++)
            {
                Vector2 burstDir = Random.insideUnitCircle.normalized;
                if (burstDir.sqrMagnitude < 0.01f)
                    burstDir = Vector2.up;

                SpawnEmber(
                    position + (Vector3)(burstDir * Random.Range(0.02f, 0.12f)),
                    (Vector3)burstDir * Random.Range(1.4f, 3.8f) + Vector3.up * 0.45f,
                    Random.Range(0.10f, 0.22f),
                    Random.Range(0.22f, 0.48f));
            }
        }

        private void SpawnEmber(Vector3 position, Vector3 velocity, float scale, float lifetime)
        {
            Color color = Random.value > 0.38f ? sparkColor : fireColor;
            SpawnTransientSprite(
                "CinderRazorEmber",
                AshwingVFXRuntime.CircleSprite,
                position,
                Quaternion.identity,
                Vector3.one * scale,
                color,
                lifetime,
                velocity,
                Random.Range(-220f, 220f),
                new Color(fireColor.r * 0.5f, 0f, 0f, 0f),
                Vector3.zero,
                235);
        }

        private static void SpawnTransientSprite(
            string objectName,
            Sprite sprite,
            Vector3 position,
            Quaternion rotation,
            Vector3 startScale,
            Color startColor,
            float lifetime,
            Vector3 velocity,
            float angularSpeed,
            Color endColor,
            Vector3 endScale,
            int sortingOrder)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(VFXContainerUtility.GetContainer());
            go.transform.position = position;
            go.transform.rotation = rotation;
            go.transform.localScale = startScale;

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = AshwingVFXRuntime.SpriteMaterial;
            renderer.color = startColor;
            renderer.sortingOrder = sortingOrder;

            AshwingTransientSpriteVFX fx = go.AddComponent<AshwingTransientSpriteVFX>();
            fx.Initialize(renderer, lifetime, velocity, angularSpeed, endColor, endScale);
        }
    }

    /// <summary>
    /// Lightweight detached VFX animator. Effects keep playing even when the
    /// projectile that spawned them is destroyed on impact.
    /// </summary>
    public class AshwingTransientSpriteVFX : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private float lifetime;
        private float elapsed;
        private Vector3 velocity;
        private float angularSpeed;
        private Color startColor;
        private Color endColor;
        private Vector3 startScale;
        private Vector3 endScale;

        public void Initialize(
            SpriteRenderer renderer,
            float duration,
            Vector3 moveVelocity,
            float rotationSpeed,
            Color targetColor,
            Vector3 targetScale)
        {
            spriteRenderer = renderer;
            lifetime = Mathf.Max(0.01f, duration);
            velocity = moveVelocity;
            angularSpeed = rotationSpeed;
            startColor = renderer != null ? renderer.color : Color.white;
            endColor = targetColor;
            startScale = transform.localScale;
            endScale = targetScale;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lifetime);
            float eased = 1f - Mathf.Pow(1f - t, 2f);

            transform.position += velocity * Time.deltaTime;
            transform.Rotate(0f, 0f, angularSpeed * Time.deltaTime);
            transform.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);

            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.Lerp(startColor, endColor, t);
            }

            if (elapsed >= lifetime)
            {
                Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// Shared procedural sprites/material so each feather and ember does not
    /// allocate a new texture/material every frame.
    /// </summary>
    public static class AshwingVFXRuntime
    {
        public static Sprite CircleSprite { get; private set; }
        public static Sprite StreakSprite { get; private set; }
        public static Material SpriteMaterial { get; private set; }

        private static bool initialized;

        public static void EnsureAssets()
        {
            if (initialized)
                return;

            initialized = true;
            CircleSprite = CreateCircleSprite();
            StreakSprite = CreateStreakSprite();

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                SpriteMaterial = new Material(shader)
                {
                    name = "Ashwing_RuntimeVFXMaterial",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
        }

        private static Sprite CreateCircleSprite()
        {
            const int size = 16;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Ashwing_RuntimeCircle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = center.x;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha *= alpha;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
            sprite.name = "Ashwing_RuntimeCircleSprite";
            return sprite;
        }

        private static Sprite CreateStreakSprite()
        {
            const int width = 48;
            const int height = 8;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Ashwing_RuntimeStreak",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            float halfHeight = (height - 1) * 0.5f;
            for (int y = 0; y < height; y++)
            {
                float vertical = 1f - Mathf.Abs(y - halfHeight) / Mathf.Max(1f, halfHeight);
                for (int x = 0; x < width; x++)
                {
                    float horizontal = Mathf.Sin((x / (float)(width - 1)) * Mathf.PI);
                    float alpha = Mathf.Clamp01(vertical * horizontal);
                    alpha *= alpha;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 24f);
            sprite.name = "Ashwing_RuntimeStreakSprite";
            return sprite;
        }
    }
}
