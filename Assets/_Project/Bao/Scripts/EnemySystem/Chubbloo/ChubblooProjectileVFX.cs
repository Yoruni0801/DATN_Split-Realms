using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Bao.Enemies
{
    [RequireComponent(typeof(ChubblooProjectile))]
    public class ChubblooProjectileVFX : MonoBehaviour
    {
        [SerializeField] private Shader projectileShader;
        [SerializeField] private Color icyDepthColor = new Color(0.04f, 0.15f, 0.42f, 1f);
        [SerializeField] private Color glacierColor = new Color(0.12f, 0.74f, 1.0f, 1f);
        [SerializeField] private Color frostColor = new Color(0.42f, 0.97f, 1.0f, 1f);
        [SerializeField] private Color snowColor = new Color(0.93f, 1f, 1f, 1f);

        private Rigidbody2D rb;
        private Material spriteMaterial;
        private Material additiveMaterial;
        private Material projectileMaterial;
        private Sprite softCircleSprite;
        private Sprite ringSprite;
        private Sprite shardSprite;
        private Sprite mistSprite;
        private Sprite sparkleSprite;
        private readonly List<Object> runtimeAssets = new List<Object>();

        private Transform root;
        private SpriteRenderer halo;
        private SpriteRenderer body;
        private SpriteRenderer core;

        private Vector2 direction = Vector2.right;
        private float speed;
        private float spawnTime;
        private Coroutine trailRoutine;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (projectileShader == null)
                projectileShader = Shader.Find("Bao/ChubblooProjectileIceShader");

            BuildRuntimeAssets();
            CreateProjectileVisual();
        }

        private void OnDestroy()
        {
            if (trailRoutine != null)
                StopCoroutine(trailRoutine);

            if (projectileMaterial != null)
                Destroy(projectileMaterial);

            foreach (Object asset in runtimeAssets)
            {
                if (asset != null)
                    Destroy(asset);
            }
        }

        private void Update()
        {
            if (rb != null && rb.linearVelocity.sqrMagnitude > 0.001f)
            {
                direction = rb.linearVelocity.normalized;
                speed = rb.linearVelocity.magnitude;
            }

            UpdateProjectileVisual();
        }

        public void OnLaunched(Vector2 dir, float launchedSpeed)
        {
            direction = dir.normalized;
            speed = launchedSpeed;
            spawnTime = Time.time;

            if (trailRoutine != null)
                StopCoroutine(trailRoutine);

            trailRoutine = StartCoroutine(TrailLoop());
        }

        public void TriggerImpact(Vector2 point, bool hitPlayer)
        {
            SpawnRing(point, hitPlayer ? 0.72f : 0.6f, hitPlayer ? frostColor : glacierColor, 0.2f, true, 15);
            SpawnRing(point, hitPlayer ? 0.92f : 0.8f, glacierColor, 0.24f, false, 14);

            int shardCount = hitPlayer ? 10 : 6;
            int mistCount = hitPlayer ? 8 : 5;
            int sparkCount = hitPlayer ? 6 : 3;

            for (int i = 0; i < mistCount; i++)
                SpawnImpactMist(point, hitPlayer);

            for (int i = 0; i < shardCount; i++)
                SpawnImpactShard(point, hitPlayer);

            for (int i = 0; i < sparkCount; i++)
                SpawnImpactSpark(point, hitPlayer);
        }

        private void CreateProjectileVisual()
        {
            root = new GameObject("ChubblooProjectileVisual").transform;
            root.SetParent(transform, false);

            halo = CreateSpriteRenderer("Halo", root, CreateSoftCircleSprite(64), true, 3);
            body = CreateSpriteRenderer("Body", root, CreateSoftCircleSprite(64), false, 4);
            core = CreateSpriteRenderer("Core", root, CreateSoftCircleSprite(64), true, 5);

            softCircleSprite = halo.sprite;
            ringSprite = CreateRingSprite(96);
            shardSprite = CreateShardSprite(48, 72);
            mistSprite = CreateMistSprite(96);
            sparkleSprite = CreateSparkleSprite(64);

            runtimeAssets.Add(ringSprite);
            runtimeAssets.Add(shardSprite);
            runtimeAssets.Add(mistSprite);
            runtimeAssets.Add(sparkleSprite);

            runtimeAssets.Add(ringSprite.texture);
            runtimeAssets.Add(shardSprite.texture);
            runtimeAssets.Add(mistSprite.texture);
            runtimeAssets.Add(sparkleSprite.texture);

            if (projectileShader != null)
            {
                projectileMaterial = new Material(projectileShader);
                projectileMaterial.SetColor("_IceDepthColor", icyDepthColor);
                projectileMaterial.SetColor("_GlacierColor", glacierColor);
                projectileMaterial.SetColor("_FrostColor", frostColor);
                projectileMaterial.SetColor("_SnowColor", snowColor);
                body.sharedMaterial = projectileMaterial;
            }

            halo.color = new Color(glacierColor.r, glacierColor.g, glacierColor.b, 0.28f);
            core.color = new Color(snowColor.r, snowColor.g, snowColor.b, 0.9f);
        }

        private void UpdateProjectileVisual()
        {
            if (root == null || body == null || core == null || halo == null)
                return;

            float time = Time.time - spawnTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 11f);
            float stretch = Mathf.Lerp(1f, 1.22f, Mathf.Clamp01(speed / 10f));

            root.right = direction;

            halo.transform.localScale = new Vector3(0.26f * stretch, 0.20f, 1f) * Mathf.Lerp(0.95f, 1.08f, pulse);
            body.transform.localScale = new Vector3(0.18f * stretch, 0.16f, 1f);
            core.transform.localScale = new Vector3(0.08f * stretch, 0.08f, 1f) * Mathf.Lerp(0.9f, 1.1f, pulse);

            if (projectileMaterial != null)
            {
                projectileMaterial.SetFloat("_Pulse", pulse);
                projectileMaterial.SetFloat("_Speed01", Mathf.Clamp01(speed / 10f));
                projectileMaterial.SetVector("_Direction", new Vector4(direction.x, direction.y, 0f, 0f));
            }
        }

        private IEnumerator TrailLoop()
        {
            while (true)
            {
                SpawnTrailMist();
                if (Random.value < 0.75f)
                    SpawnTrailSpark();
                if (Random.value < 0.35f)
                    SpawnTrailShard();

                yield return new WaitForSeconds(0.045f);
            }
        }

        private void SpawnTrailMist()
        {
            GameObject go = CreateSpriteVFX("TrailMist", mistSprite, glacierColor, false, 1);
            go.transform.position = transform.position - (Vector3)direction * 0.05f;
            go.transform.localScale = new Vector3(Random.Range(0.06f, 0.12f), Random.Range(0.04f, 0.08f), 1f);
            Vector3 velocity = -(Vector3)direction * Random.Range(0.35f, 1.0f) + new Vector3(0f, Random.Range(-0.08f, 0.08f), 0f);
            StartCoroutine(AnimateMist(go, velocity, Random.Range(0.18f, 0.28f)));
        }

        private void SpawnTrailSpark()
        {
            GameObject go = CreateSpriteVFX("TrailSpark", sparkleSprite, snowColor, true, 2);
            go.transform.position = transform.position - (Vector3)direction * 0.03f;
            go.transform.localScale = Vector3.one * Random.Range(0.018f, 0.035f);
            Vector3 velocity = -(Vector3)direction * Random.Range(0.45f, 1.3f) + (Vector3)Random.insideUnitCircle * 0.15f;
            StartCoroutine(AnimateSpark(go, velocity, Random.Range(0.10f, 0.18f)));
        }

        private void SpawnTrailShard()
        {
            GameObject go = CreateSpriteVFX("TrailShard", shardSprite, frostColor, true, 3);
            go.transform.position = transform.position - (Vector3)direction * 0.02f;
            go.transform.localScale = new Vector3(Random.Range(0.025f, 0.04f), Random.Range(0.05f, 0.08f), 1f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            Vector3 velocity = -(Vector3)direction * Random.Range(0.4f, 1.2f) + (Vector3)Random.insideUnitCircle * 0.25f;
            StartCoroutine(AnimateShard(go, velocity, Random.Range(0.12f, 0.22f)));
        }

        private void SpawnImpactMist(Vector2 point, bool hitPlayer)
        {
            GameObject go = CreateSpriteVFX("ImpactMist", mistSprite, glacierColor, false, 13);
            go.transform.position = point;
            go.transform.localScale = new Vector3(Random.Range(0.08f, 0.16f), Random.Range(0.06f, 0.12f), 1f);
            Vector3 velocity = (Vector3)Random.insideUnitCircle * Random.Range(0.4f, hitPlayer ? 1.2f : 0.8f);
            velocity.y += Random.Range(0.05f, 0.4f);
            StartCoroutine(AnimateMist(go, velocity, Random.Range(0.22f, 0.36f)));
        }

        private void SpawnImpactShard(Vector2 point, bool hitPlayer)
        {
            GameObject go = CreateSpriteVFX("ImpactShard", shardSprite, Random.value < 0.5f ? frostColor : snowColor, true, 14);
            go.transform.position = point;
            go.transform.localScale = new Vector3(Random.Range(0.03f, 0.06f), Random.Range(0.07f, 0.12f), 1f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            Vector3 velocity = (Vector3)Random.insideUnitCircle.normalized * Random.Range(hitPlayer ? 0.8f : 0.45f, hitPlayer ? 1.8f : 1.0f);
            velocity.y = Mathf.Abs(velocity.y) + 0.08f;
            StartCoroutine(AnimateShard(go, velocity, Random.Range(0.16f, 0.3f)));
        }

        private void SpawnImpactSpark(Vector2 point, bool hitPlayer)
        {
            GameObject go = CreateSpriteVFX("ImpactSpark", sparkleSprite, snowColor, true, 15);
            go.transform.position = point;
            go.transform.localScale = Vector3.one * Random.Range(0.02f, 0.04f);
            Vector3 velocity = (Vector3)Random.insideUnitCircle.normalized * Random.Range(hitPlayer ? 0.8f : 0.4f, hitPlayer ? 1.7f : 0.9f);
            StartCoroutine(AnimateSpark(go, velocity, Random.Range(0.08f, 0.18f)));
        }

        private void SpawnRing(Vector2 center, float radius, Color color, float duration, bool bright, int sortingOrder)
        {
            GameObject go = CreateSpriteVFX("ImpactRing", ringSprite, color, bright, sortingOrder);
            go.transform.position = center;
            go.transform.localScale = Vector3.one * 0.08f;
            StartCoroutine(AnimateRing(go, color, radius, duration));
        }

        private IEnumerator AnimateRing(GameObject go, Color color, float radius, float duration)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 startScale = Vector3.one * 0.08f;
            Vector3 endScale = Vector3.one * radius * 2f;
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                go.transform.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);

                if (sr != null)
                {
                    Color c = color;
                    c.a = Mathf.Pow(1f - t, 1.5f);
                    sr.color = c;
                }

                yield return null;
            }

            if (go != null) Destroy(go);
        }

        private IEnumerator AnimateMist(GameObject go, Vector3 velocity, float duration)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 startScale = go.transform.localScale;
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                velocity *= Mathf.Exp(-3.0f * Time.deltaTime);
                go.transform.position += velocity * Time.deltaTime;
                go.transform.localScale = startScale * Mathf.Lerp(1f, 2.1f, t);

                if (sr != null)
                {
                    Color c = glacierColor;
                    c.a = Mathf.Lerp(0.24f, 0f, t);
                    sr.color = c;
                }

                yield return null;
            }

            if (go != null) Destroy(go);
        }

        private IEnumerator AnimateSpark(GameObject go, Vector3 velocity, float duration)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 startScale = go.transform.localScale;
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                velocity *= Mathf.Exp(-4f * Time.deltaTime);
                go.transform.position += velocity * Time.deltaTime;
                go.transform.localScale = startScale * Mathf.Lerp(1.15f, 0f, t);

                if (sr != null)
                {
                    Color c = snowColor;
                    c.a = 1f - t;
                    sr.color = c;
                }

                yield return null;
            }

            if (go != null) Destroy(go);
        }

        private IEnumerator AnimateShard(GameObject go, Vector3 velocity, float duration)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 startScale = go.transform.localScale;
            float rot = Random.Range(-720f, 720f);
            float elapsed = 0f;

            while (go != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                velocity.y -= 2.4f * Time.deltaTime;
                go.transform.position += velocity * Time.deltaTime;
                go.transform.Rotate(0f, 0f, rot * Time.deltaTime);
                go.transform.localScale = startScale * Mathf.Lerp(1f, 0.35f, t);

                if (sr != null && t > 0.45f)
                {
                    Color c = sr.color;
                    c.a = 1f - Mathf.InverseLerp(0.45f, 1f, t);
                    sr.color = c;
                }

                yield return null;
            }

            if (go != null) Destroy(go);
        }

        private Material CreateSpriteMaterial(bool additive)
        {
            Shader shader = Shader.Find("Sprites/Default");
            Material mat = shader != null ? new Material(shader) : null;
            if (mat != null)
                runtimeAssets.Add(mat);
            return mat;
        }

        private GameObject CreateSpriteVFX(string name, Sprite sprite, Color color, bool bright, int sortingOrder)
        {
            GameObject go = new GameObject(name);
            Transform parent = VFXContainerUtility.GetContainer();
            if (parent != null)
                go.transform.SetParent(parent, true);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            sr.sharedMaterial = bright ? additiveMaterial : spriteMaterial;
            return go;
        }

        private SpriteRenderer CreateSpriteRenderer(string name, Transform parent, Sprite sprite, bool bright, int sortingOrder)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            sr.sharedMaterial = bright ? additiveMaterial : spriteMaterial;
            return sr;
        }

        private void BuildRuntimeAssets()
        {
            spriteMaterial = CreateSpriteMaterial(false);
            additiveMaterial = CreateSpriteMaterial(true);
        }

        private static Sprite CreateSoftCircleSprite(int size)
        {
            Texture2D tex = NewTexture(size, size);
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxRadius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxRadius;
                    float alpha = Mathf.Clamp01(1f - d);
                    alpha = alpha * alpha * (3f - 2f * alpha);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateRingSprite(int size)
        {
            Texture2D tex = NewTexture(size, size);
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.38f;
            float thickness = size * 0.05f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    float edgeDistance = Mathf.Abs(d - radius);
                    float alpha = Mathf.Clamp01(1f - edgeDistance / thickness);
                    alpha = alpha * alpha;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateShardSprite(int width, int height)
        {
            Texture2D tex = NewTexture(width, height);
            for (int y = 0; y < height; y++)
            {
                float ny = (y / (float)(height - 1)) * 2f - 1f;
                float halfWidth = Mathf.Lerp(0f, 0.95f, 1f - Mathf.Abs(ny));

                for (int x = 0; x < width; x++)
                {
                    float nx = Mathf.Abs((x / (float)(width - 1)) * 2f - 1f);
                    float body = Mathf.Clamp01((halfWidth - nx) * 8f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, body));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.42f), height);
        }

        private static Sprite CreateMistSprite(int size)
        {
            Texture2D tex = NewTexture(size, size);
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = (new Vector2(x, y) - center) / center.x;
                    float dist = p.magnitude;
                    float alpha = Mathf.Clamp01(1f - dist * 1.2f);
                    alpha = Mathf.Pow(alpha, 1.8f) * (0.72f + 0.28f * Mathf.PerlinNoise((x + 8f) * 0.09f, (y + 12f) * 0.09f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateSparkleSprite(int size)
        {
            Texture2D tex = NewTexture(size, size);
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y) - center;
                    float nx = Mathf.Abs(p.x) / center.x;
                    float ny = Mathf.Abs(p.y) / center.y;
                    float horizontal = Mathf.Clamp01(1f - (nx * 0.62f + ny * 8f));
                    float vertical = Mathf.Clamp01(1f - (ny * 0.62f + nx * 8f));
                    float alpha = Mathf.Max(horizontal, vertical);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Texture2D NewTexture(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Chubbloo_Projectile_Runtime"
            };

            Color clear = new Color(1f, 1f, 1f, 0f);
            Color[] pixels = new Color[width * height];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;

            tex.SetPixels(pixels);
            return tex;
        }
    }
}
