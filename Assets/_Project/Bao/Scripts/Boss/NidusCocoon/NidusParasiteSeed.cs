using System.Collections;
using UnityEngine;

namespace Bao.Bosses
{
    /// <summary>
    /// Destructible parasite seed. If the player does not destroy it before the
    /// hatch timer completes it grows into a NidusMinion.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NidusParasiteSeed : MonoBehaviour, Bao.Enemies.IDamageable
    {
        [Header("Cocoon Visual")]
        [SerializeField] private Sprite cocoonSprite;
        [SerializeField, Min(0.05f)] private float visualRadius = 0.34f;
        [SerializeField, Min(0.05f)] private float cocoonScale = 1f;
        [SerializeField] private Color seedColor = Color.white;
        [SerializeField] private Color glowColor = new Color(0.45f, 1.35f, 0.08f, 0.95f);
        [SerializeField, Min(0f)] private float glowScale = 1.65f;
        [SerializeField, Range(0f, 1f)] private float glowAlpha = 0.28f;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 104;

        [Header("Cocoon Motion")]
        [SerializeField, Min(0f)] private float travelArcHeight = 1.25f;
        [SerializeField] private float travelSpinSpeed = 220f;
        [SerializeField, Min(0.05f)] private float hatchPulseMinScale = 0.92f;
        [SerializeField, Min(0.05f)] private float hatchPulseMaxScale = 1.03f;
        [SerializeField, Min(0f)] private float hatchUrgentPulseBoost = 0.11f;

        [Header("Cocoon Summon VFX")]
        [SerializeField, Min(0.05f)] private float landingBurstScale = 1.15f;
        [SerializeField, Min(0.05f)] private float landingBurstDuration = 0.32f;
        [SerializeField, Min(0.05f)] private float hatchBurstScale = 1.65f;
        [SerializeField, Min(0.05f)] private float hatchBurstDuration = 0.48f;
        [SerializeField, Range(0f, 360f)] private float burstRotation = 18f;
        [SerializeField] private Color burstColor = Color.white;

        private NidusCocoonBoss owner;
        private GameObject minionPrefab;
        private int maxHealth;
        private int currentHealth;
        private float hatchTime;
        private Vector3 groundTarget;
        private Vector3 spawnOrigin;
        private float travelDuration;
        private bool landed;
        private bool resolved;
        private Coroutine lifecycleRoutine;
        private CircleCollider2D hitCollider;
        private SpriteRenderer seedRenderer;
        private SpriteRenderer glowRenderer;
        private static Sprite cachedSeedSprite;

        public NidusCocoonBoss Owner => owner;
        public bool IsResolved => resolved;

        public static NidusParasiteSeed Spawn(
            GameObject seedPrefab,
            NidusCocoonBoss owner,
            GameObject minionPrefab,
            Vector3 spawnOrigin,
            Vector3 groundTarget,
            int health,
            float hatchTime,
            float travelDuration = 0.42f)
        {
            GameObject go;
            if (seedPrefab != null)
            {
                go = Object.Instantiate(seedPrefab, spawnOrigin, Quaternion.identity);
                go.SetActive(true);
            }
            else
            {
                go = new GameObject("Nidus_ParasiteSeed_Procedural");
                go.transform.position = spawnOrigin;
            }

            NidusParasiteSeed seed = go.GetComponent<NidusParasiteSeed>();
            if (seed == null)
                seed = go.AddComponent<NidusParasiteSeed>();

            seed.Initialize(owner, minionPrefab, spawnOrigin, groundTarget, health, hatchTime, travelDuration);
            return seed;
        }

        public void Initialize(
            NidusCocoonBoss owner,
            GameObject minionPrefab,
            Vector3 spawnOrigin,
            Vector3 groundTarget,
            int health,
            float hatchTime,
            float travelDuration)
        {
            this.owner = owner;
            this.minionPrefab = minionPrefab;
            this.spawnOrigin = spawnOrigin;
            this.groundTarget = groundTarget;
            this.maxHealth = Mathf.Max(1, health);
            this.currentHealth = this.maxHealth;
            this.hatchTime = Mathf.Max(0.6f, hatchTime);
            this.travelDuration = Mathf.Max(0.12f, travelDuration);
            landed = false;
            resolved = false;

            EnsureVisualAndCollider();
            hitCollider.enabled = false;
            owner?.RegisterSeed(this);

            if (lifecycleRoutine != null)
                StopCoroutine(lifecycleRoutine);
            lifecycleRoutine = StartCoroutine(LifecycleRoutine());
        }

        private void EnsureVisualAndCollider()
        {
            hitCollider = GetComponent<CircleCollider2D>();
            if (hitCollider == null)
                hitCollider = gameObject.AddComponent<CircleCollider2D>();
            hitCollider.isTrigger = true;
            hitCollider.radius = visualRadius * 1.15f;

            seedRenderer = GetComponent<SpriteRenderer>();
            if (seedRenderer == null)
                seedRenderer = gameObject.AddComponent<SpriteRenderer>();

            if (seedRenderer.sprite == null)
                seedRenderer.sprite = cocoonSprite != null ? cocoonSprite : GetSeedSprite();
            seedRenderer.color = seedColor;
            seedRenderer.sortingLayerName = sortingLayerName;
            seedRenderer.sortingOrder = sortingOrder;
            transform.localScale = Vector3.one * cocoonScale;

            Transform glow = transform.Find("SeedGlow");
            if (glow == null)
            {
                GameObject glowObject = new GameObject("SeedGlow");
                glowObject.transform.SetParent(transform, false);
                glow = glowObject.transform;
            }

            glowRenderer = glow.GetComponent<SpriteRenderer>();
            if (glowRenderer == null)
                glowRenderer = glow.gameObject.AddComponent<SpriteRenderer>();
            glowRenderer.sprite = seedRenderer.sprite;
            glowRenderer.color = new Color(glowColor.r, glowColor.g, glowColor.b, glowAlpha);
            glowRenderer.sortingLayerName = sortingLayerName;
            glowRenderer.sortingOrder = seedRenderer.sortingOrder - 1;
            glow.localScale = Vector3.one * glowScale;
        }

        private IEnumerator LifecycleRoutine()
        {
            float elapsed = 0f;
            while (elapsed < travelDuration && !resolved)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / travelDuration);
                float arc = Mathf.Sin(t * Mathf.PI) * travelArcHeight;
                transform.position = Vector3.Lerp(spawnOrigin, groundTarget, t) + Vector3.up * arc;
                transform.Rotate(0f, 0f, Time.deltaTime * travelSpinSpeed);
                yield return null;
            }

            if (resolved) yield break;

            transform.position = groundTarget;
            transform.rotation = Quaternion.identity;
            landed = true;
            hitCollider.enabled = true;
            NidusOrganicSkillVFX.SpawnCocoonBurst(
                transform.position,
                seedRenderer != null ? seedRenderer.sprite : cocoonSprite,
                cocoonScale * landingBurstScale,
                landingBurstDuration,
                burstRotation,
                burstColor,
                sortingLayerName,
                sortingOrder - 2);
            GameObject pulse = NidusOrganicSkillVFX.SpawnPulse(transform.position, 0.75f, hatchTime, glowColor);

            elapsed = 0f;
            while (elapsed < hatchTime && !resolved)
            {
                if (owner == null || owner.IsDead)
                {
                    WitherForPhaseTransition(0.28f);
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / hatchTime);
                float urgency = t * t;
                float pulseSpeed = Mathf.Lerp(4.5f, 15f, urgency);
                float pulseAmplitude = Mathf.Lerp(
                    Mathf.Abs(hatchPulseMaxScale - hatchPulseMinScale) * 0.5f,
                    hatchUrgentPulseBoost,
                    urgency);
                float pulseCenter = (hatchPulseMinScale + hatchPulseMaxScale) * 0.5f;
                float pulseAmount = pulseCenter + Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude;
                transform.localScale = Vector3.one * cocoonScale * pulseAmount;

                if (glowRenderer != null)
                {
                    Color c = Color.Lerp(glowColor, new Color(0.82f, 1.65f, 0.08f, 1f), urgency);
                    c.a = Mathf.Lerp(0.20f, 0.72f, urgency);
                    glowRenderer.color = c;
                }

                yield return null;
            }

            if (pulse != null) Destroy(pulse);
            if (!resolved)
                Hatch();
        }

        public void TakeDamage(int damage)
        {
            if (resolved || !landed || damage <= 0)
                return;

            currentHealth = Mathf.Max(0, currentHealth - damage);
            global::DamagePopupSystem.ShowEnemy(transform.position + Vector3.up * 0.5f, damage);

            if (seedRenderer != null)
                StartCoroutine(HitFlashRoutine());

            if (currentHealth <= 0)
                StartCoroutine(WitherRoutine(0.34f, true));
        }

        private IEnumerator HitFlashRoutine()
        {
            Color original = seedRenderer != null ? seedRenderer.color : seedColor;
            if (seedRenderer != null)
                seedRenderer.color = new Color(1f, 1f, 0.65f, 1f);
            yield return new WaitForSeconds(0.08f);
            if (seedRenderer != null && !resolved)
                seedRenderer.color = original;
        }

        private void Hatch()
        {
            if (resolved) return;
            resolved = true;
            hitCollider.enabled = false;

            owner?.UnregisterSeed(this);

            if (minionPrefab != null)
            {
                GameObject minionObject = Instantiate(minionPrefab, groundTarget, Quaternion.identity);
                NidusMinion minion = minionObject.GetComponent<NidusMinion>();
                if (minion != null)
                    minion.InitializeOwner(owner);
            }

            NidusOrganicSkillVFX.SpawnCocoonBurst(
                groundTarget,
                seedRenderer != null ? seedRenderer.sprite : cocoonSprite,
                cocoonScale * hatchBurstScale,
                hatchBurstDuration,
                burstRotation,
                burstColor,
                sortingLayerName,
                sortingOrder + 1);
            NidusOrganicSkillVFX.SpawnPulse(groundTarget, 1.2f, 0.45f, glowColor);
            Destroy(gameObject);
        }

        public void WitherForPhaseTransition(float duration = 0.45f)
        {
            if (resolved) return;
            if (lifecycleRoutine != null)
            {
                StopCoroutine(lifecycleRoutine);
                lifecycleRoutine = null;
            }
            StartCoroutine(WitherRoutine(duration, false));
        }

        public void ForceCleanup()
        {
            if (resolved)
            {
                if (gameObject != null) Destroy(gameObject);
                return;
            }

            resolved = true;
            owner?.UnregisterSeed(this);
            owner = null;
            if (gameObject != null) Destroy(gameObject);
        }

        private IEnumerator WitherRoutine(float duration, bool destroyedByPlayer)
        {
            if (resolved) yield break;
            resolved = true;
            if (hitCollider != null) hitCollider.enabled = false;
            owner?.UnregisterSeed(this);

            if (destroyedByPlayer)
                NidusOrganicSkillVFX.SpawnPulse(transform.position, 0.8f, 0.30f, new Color(0.48f, 0.75f, 0.20f, 1f));

            Vector3 startScale = transform.localScale;
            Color startColor = seedRenderer != null ? seedRenderer.color : Color.white;
            Color startGlow = glowRenderer != null ? glowRenderer.color : Color.white;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                transform.localScale = startScale * Mathf.Lerp(1f, 0.12f, t);
                if (seedRenderer != null)
                {
                    Color c = startColor;
                    c.a = 1f - t;
                    seedRenderer.color = c;
                }
                if (glowRenderer != null)
                {
                    Color c = startGlow;
                    c.a *= 1f - t;
                    glowRenderer.color = c;
                }
                yield return null;
            }

            owner = null;
            Destroy(gameObject);
        }

        private static Sprite GetSeedSprite()
        {
            if (cachedSeedSprite != null)
                return cachedSeedSprite;

            const int size = 48;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Nidus_SeedSprite_Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 delta = new Vector2(x, y) - center;
                    float nx = delta.x / (size * 0.33f);
                    float ny = delta.y / (size * 0.43f);
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    float edge = Mathf.Clamp01((1f - d) * 12f);
                    float vein = Mathf.Abs(Mathf.Sin((delta.x * 0.28f) + (delta.y * 0.15f))) < 0.12f ? 0.22f : 0f;
                    pixels[y * size + x] = new Color(1f, 1f - vein, 1f - vein * 0.4f, edge);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);

            cachedSeedSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.45f), size);
            cachedSeedSprite.name = "Nidus_SeedSprite";
            cachedSeedSprite.hideFlags = HideFlags.DontSave;
            return cachedSeedSprite;
        }

        private void OnDestroy()
        {
            owner?.UnregisterSeed(this);
        }
    }
}
