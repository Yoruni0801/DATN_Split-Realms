using UnityEngine;

namespace Bao.Enemies
{
    /// <summary>
    /// Improved frost slow VFX for Chubbloo.
    /// Natural glacier patch + orbiting ice shards + subtle slow badge.
    /// Existing API remains:
    /// ChubblooFrostImpactVfx.Spawn(target, hitPoint);
    /// ChubblooFrostImpactVfx.Spawn(target, hitPoint, slowDuration);
    /// </summary>
    public sealed class ChubblooFrostImpactVfx : MonoBehaviour
    {
        private const float DefaultDuration = 2f;
        private const int OrbitShardCount = 4;
        private const int SnowMoteCount = 8;
        private const int FootLockCount = 4;

        private static readonly Color IcyDepth = new Color(0.015f, 0.08f, 0.28f, 1f);
        private static readonly Color GlacierBlue = new Color(0.06f, 0.46f, 1f, 1f);
        private static readonly Color FrostCyan = new Color(0.28f, 0.96f, 1f, 1f);
        private static readonly Color IceWhite = new Color(0.93f, 1f, 1f, 1f);

        private struct LayeredSprite
        {
            public Transform root;
            public SpriteRenderer back;
            public SpriteRenderer front;
        }

        private static Material sharedMaterial;
        private static Sprite crystalSprite;
        private static Sprite snowflakeSprite;
        private static Sprite softCircleSprite;
        private static Sprite ringSprite;
        private static Sprite sparkleSprite;
        private static Sprite mistSprite;

        private readonly LayeredSprite[] orbitShards = new LayeredSprite[OrbitShardCount];
        private readonly LayeredSprite[] footLocks = new LayeredSprite[FootLockCount];
        private readonly SpriteRenderer[] snowMotes = new SpriteRenderer[SnowMoteCount];

        private SpriteRenderer impactBack;
        private SpriteRenderer impactFront;
        private SpriteRenderer groundPatchBack;
        private SpriteRenderer groundPatchFront;
        private SpriteRenderer auraBack;
        private SpriteRenderer auraFront;
        private SpriteRenderer badgeBack;
        private SpriteRenderer badgeCore;

        private Transform visualTarget;
        private Collider2D anchorCollider;
        private float elapsed;
        private float duration = DefaultDuration;
        private int sortingOrder = 10;
        private string sortingLayerName = "Default";
        private float groundOffset = -0.46f;
        private float badgeOffset = 1.12f;

        public static void Spawn(Transform target, Vector2 hitPoint)
        {
            Spawn(target, hitPoint, DefaultDuration);
        }

        public static void Spawn(Transform target, Vector2 hitPoint, float slowDuration)
        {
            Transform visual = ResolveVisualTarget(target);
            if (visual == null)
                return;

            ChubblooFrostImpactVfx existing = visual.GetComponentInChildren<ChubblooFrostImpactVfx>(true);
            if (existing != null)
            {
                existing.Configure(visual, slowDuration);
                return;
            }

            GameObject go = new GameObject("Chubbloo Frost Slow");
            go.transform.SetParent(visual, false);
            go.transform.localPosition = Vector3.zero;

            ChubblooFrostImpactVfx effect = go.AddComponent<ChubblooFrostImpactVfx>();
            effect.Configure(visual, slowDuration);
        }

        private static Transform ResolveVisualTarget(Transform target)
        {
            if (target == null)
                return null;

            PlayerHealth playerHealth = target.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
                return playerHealth.transform;

            Transform playerRoot = null;
            Transform visualRoot = null;

            for (Transform current = target; current != null; current = current.parent)
            {
                if (current.CompareTag("Player"))
                {
                    playerRoot = current;
                    break;
                }

                if (current.GetComponentInChildren<Renderer>(true) != null)
                    visualRoot = current;
            }

            return playerRoot != null ? playerRoot : visualRoot ?? target;
        }

        private void Awake()
        {
            EnsureSharedAssets();

            CreateImpactGlows();
            CreateGroundPatch();
            CreateBodyAura();
            CreateBadge();
            CreateOrbitShards();
            CreateFootLocks();
            CreateSnowMotes();

            ApplySorting();
            Restart();
        }

        private void Configure(Transform target, float slowDuration)
        {
            visualTarget = target;
            anchorCollider = ResolveAnchorCollider(visualTarget);
            duration = Mathf.Max(0.35f, slowDuration);
            AlignToTarget(visualTarget);
            SetSorting(visualTarget);
            Restart();
        }

        private void Restart()
        {
            elapsed = 0f;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (visualTarget != null)
                AlignToTarget(visualTarget);

            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float fadeOut = 1f - Smooth01(Mathf.InverseLerp(0.72f, 1f, t));
            float inAmount = EaseOutBack(Mathf.Clamp01(t / 0.16f));

            UpdateImpact(t);
            UpdateGroundPatch(fadeOut, inAmount);
            UpdateAura(fadeOut, inAmount);
            UpdateBadge(fadeOut, inAmount);
            UpdateOrbitShards(t, fadeOut, inAmount);
            UpdateFootLocks(t, fadeOut, inAmount);
            UpdateSnowMotes(fadeOut);

            if (elapsed >= duration)
                Destroy(gameObject);
        }

        private static Collider2D ResolveAnchorCollider(Transform target)
        {
            if (target == null)
                return null;

            PlayerHealth playerHealth = target.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                Collider2D col = playerHealth.GetComponent<Collider2D>();
                if (col != null && col.enabled && !col.isTrigger)
                    return col;
            }

            Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D col = colliders[i];
                if (col != null && col.enabled && !col.isTrigger)
                    return col;
            }

            return null;
        }

        private void AlignToTarget(Transform target)
        {
            if (anchorCollider == null || !anchorCollider.enabled || anchorCollider.isTrigger)
                anchorCollider = ResolveAnchorCollider(target);

            Bounds bounds = default;
            bool hasBounds = anchorCollider != null;

            if (hasBounds)
            {
                bounds = anchorCollider.bounds;
            }
            else
            {
                Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer r = renderers[i];
                    if (r == null || !r.enabled || r.transform.IsChildOf(transform) || r.bounds.size.sqrMagnitude <= 0.0001f)
                        continue;

                    if (!hasBounds)
                    {
                        bounds = r.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(r.bounds);
                    }
                }
            }

            if (!hasBounds)
            {
                transform.localPosition = Vector3.zero;
                transform.localScale = Vector3.one;
                groundOffset = -0.46f;
                badgeOffset = 1.12f;
                return;
            }

            transform.position = bounds.center;
            float scale = Mathf.Clamp(Mathf.Max(bounds.size.x / 1.52f, bounds.size.y / 2.08f), 0.82f, 1.35f);
            Vector3 targetScale = target.lossyScale;
            transform.localScale = new Vector3(
                scale / Mathf.Max(0.01f, Mathf.Abs(targetScale.x)),
                scale / Mathf.Max(0.01f, Mathf.Abs(targetScale.y)),
                1f);

            float inverseScale = 1f / scale;
            groundOffset = -bounds.extents.y * 0.94f * inverseScale;
            badgeOffset = bounds.extents.y * 0.97f * inverseScale;

            if (groundPatchBack != null)
                groundPatchBack.transform.parent.localPosition = new Vector3(0f, groundOffset, 0f);

            if (badgeBack != null)
                badgeBack.transform.parent.localPosition = new Vector3(0f, badgeOffset, 0f);
        }

        private void SetSorting(Transform target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            int bestOrder = int.MinValue;
            string bestLayer = "Default";

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.enabled || r.transform.IsChildOf(transform) || r.bounds.size.sqrMagnitude <= 0.0001f)
                    continue;

                if (r.sortingOrder > bestOrder)
                {
                    bestOrder = r.sortingOrder;
                    bestLayer = r.sortingLayerName;
                }
            }

            sortingOrder = bestOrder == int.MinValue ? 10 : bestOrder;
            sortingLayerName = bestLayer;
            ApplySorting();
        }

        private void ApplySorting()
        {
            SetSort(impactBack, -3);
            SetSort(impactFront, -2);
            SetSort(groundPatchBack, -3);
            SetSort(groundPatchFront, -2);
            SetSort(auraBack, -2);
            SetSort(auraFront, -1);

            for (int i = 0; i < footLocks.Length; i++)
            {
                SetSort(footLocks[i].back, 0);
                SetSort(footLocks[i].front, 1);
            }

            for (int i = 0; i < orbitShards.Length; i++)
            {
                SetSort(orbitShards[i].back, 1);
                SetSort(orbitShards[i].front, 2);
            }

            for (int i = 0; i < snowMotes.Length; i++)
                SetSort(snowMotes[i], 2);

            SetSort(badgeBack, 3);
            SetSort(badgeCore, 4);
        }

        private void SetSort(SpriteRenderer sr, int offset)
        {
            if (sr == null)
                return;

            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder + offset;
        }

        private void CreateImpactGlows()
        {
            impactBack = CreateSpriteRenderer("Impact Back", transform, softCircleSprite);
            impactFront = CreateSpriteRenderer("Impact Front", transform, softCircleSprite);
        }

        private void CreateGroundPatch()
        {
            Transform root = CreateChild("Ground Frost Patch", transform);
            root.localPosition = new Vector3(0f, groundOffset, 0f);

            groundPatchBack = CreateSpriteRenderer("Patch Back", root, mistSprite);
            groundPatchBack.transform.localScale = new Vector3(1.22f, 0.48f, 1f);

            groundPatchFront = CreateSpriteRenderer("Patch Front", root, ringSprite);
            groundPatchFront.transform.localScale = new Vector3(1.02f, 0.34f, 1f);
        }

        private void CreateBodyAura()
        {
            Transform root = CreateChild("Body Frost Aura", transform);

            auraBack = CreateSpriteRenderer("Aura Back", root, softCircleSprite);
            auraBack.transform.localScale = new Vector3(0.96f, 1.26f, 1f);

            auraFront = CreateSpriteRenderer("Aura Front", root, softCircleSprite);
            auraFront.transform.localScale = new Vector3(0.82f, 1.08f, 1f);
        }

        private void CreateBadge()
        {
            Transform root = CreateChild("Slow Badge", transform);
            root.localPosition = new Vector3(0f, badgeOffset, 0f);

            badgeBack = CreateSpriteRenderer("Badge Back", root, softCircleSprite);
            badgeBack.transform.localScale = Vector3.one * 0.42f;

            badgeCore = CreateSpriteRenderer("Badge Core", root, snowflakeSprite);
            badgeCore.transform.localScale = Vector3.one * 0.28f;
        }

        private void CreateOrbitShards()
        {
            for (int i = 0; i < OrbitShardCount; i++)
            {
                Transform root = CreateChild("Orbit Shard", transform);
                SpriteRenderer back = CreateSpriteRenderer("Shard Back", root, crystalSprite);
                SpriteRenderer front = CreateSpriteRenderer("Shard Front", root, crystalSprite);
                back.transform.localScale = Vector3.one * 1.24f;

                orbitShards[i] = new LayeredSprite
                {
                    root = root,
                    back = back,
                    front = front
                };
            }
        }

        private void CreateFootLocks()
        {
            float[] xPositions = { -0.42f, -0.16f, 0.16f, 0.42f };
            float[] rotations = { 18f, 7f, -7f, -18f };

            for (int i = 0; i < FootLockCount; i++)
            {
                Transform root = CreateChild("Foot Frost Spike", transform);
                root.localPosition = new Vector3(xPositions[i], -0.40f, 0f);
                root.localRotation = Quaternion.Euler(0f, 0f, rotations[i]);

                SpriteRenderer back = CreateSpriteRenderer("Spike Back", root, crystalSprite);
                SpriteRenderer front = CreateSpriteRenderer("Spike Front", root, crystalSprite);
                back.transform.localScale = new Vector3(0.24f, 0.62f, 1f);
                front.transform.localScale = new Vector3(0.16f, 0.50f, 1f);

                footLocks[i] = new LayeredSprite
                {
                    root = root,
                    back = back,
                    front = front
                };
            }
        }

        private void CreateSnowMotes()
        {
            for (int i = 0; i < SnowMoteCount; i++)
            {
                Transform root = CreateChild("Snow Mote", transform);
                SpriteRenderer mote = CreateSpriteRenderer("Mote", root, i % 3 == 0 ? sparkleSprite : softCircleSprite);
                snowMotes[i] = mote;
            }
        }

        private void UpdateImpact(float t)
        {
            float hitT = Mathf.Clamp01(t / 0.14f);
            float alpha = 1f - Smooth01(hitT);
            float overshoot = EaseOutBack(hitT);

            impactBack.transform.localScale = Vector3.one * Mathf.Lerp(0.14f, 0.72f, overshoot);
            impactFront.transform.localScale = Vector3.one * Mathf.Lerp(0.08f, 0.54f, overshoot);

            impactBack.color = WithAlpha(IcyDepth, alpha * 0.34f);
            impactFront.color = WithAlpha(FrostCyan, alpha * 0.48f);
        }

        private void UpdateGroundPatch(float fadeOut, float inAmount)
        {
            Transform root = groundPatchBack.transform.parent;
            float pulse = 1f + Mathf.Sin(elapsed * 6.5f) * 0.028f;
            root.localScale = Vector3.one * inAmount * pulse;
            root.localRotation = Quaternion.Euler(0f, 0f, elapsed * 8f);

            groundPatchBack.color = WithAlpha(IcyDepth, fadeOut * 0.36f);
            groundPatchFront.color = WithAlpha(FrostCyan, fadeOut * (0.34f + Mathf.Sin(elapsed * 7.5f) * 0.05f));
        }

        private void UpdateAura(float fadeOut, float inAmount)
        {
            Transform root = auraBack.transform.parent;
            root.localScale = Vector3.one * inAmount;
            root.localRotation = Quaternion.Euler(0f, 0f, -elapsed * 13f);

            float breathe = 0.5f + 0.5f * Mathf.Sin(elapsed * 5.3f);
            auraBack.color = WithAlpha(IcyDepth, fadeOut * (0.11f + breathe * 0.05f));
            auraFront.color = WithAlpha(GlacierBlue, fadeOut * (0.08f + breathe * 0.06f));
        }

        private void UpdateBadge(float fadeOut, float inAmount)
        {
            Transform root = badgeBack.transform.parent;
            root.localPosition = new Vector3(0f, badgeOffset + Mathf.Sin(elapsed * 6.2f) * 0.032f, 0f);
            root.localScale = Vector3.one * inAmount * (1f + Mathf.Sin(elapsed * 7.4f) * 0.02f);

            badgeBack.transform.localRotation = Quaternion.Euler(0f, 0f, elapsed * 28f);
            badgeCore.transform.localRotation = Quaternion.Euler(0f, 0f, -elapsed * 64f);

            badgeBack.color = WithAlpha(IcyDepth, fadeOut * 0.48f);
            badgeCore.color = WithAlpha(IceWhite, fadeOut * 0.82f);
        }

        private void UpdateOrbitShards(float t, float fadeOut, float inAmount)
        {
            float radius = Mathf.Lerp(0.14f, 0.46f, Smooth01(Mathf.Clamp01(t / 0.16f)));

            for (int i = 0; i < orbitShards.Length; i++)
            {
                LayeredSprite shard = orbitShards[i];
                float angle = elapsed * 152f + i * (360f / OrbitShardCount);
                float radians = angle * Mathf.Deg2Rad;
                float depth = (Mathf.Sin(radians) + 1f) * 0.5f;

                shard.root.localPosition = new Vector3(
                    Mathf.Cos(radians) * radius,
                    Mathf.Sin(radians) * radius * 0.52f + 0.08f,
                    0f);

                shard.root.localRotation = Quaternion.Euler(0f, 0f, -angle * 1.35f);
                shard.root.localScale = Vector3.one * inAmount * Mathf.Lerp(0.11f, 0.18f, depth);

                shard.back.color = WithAlpha(IcyDepth, fadeOut * 0.56f);
                shard.front.color = WithAlpha(i % 2 == 0 ? FrostCyan : IceWhite, fadeOut * (0.64f + Mathf.Sin(elapsed * 9f + i) * 0.12f));
            }
        }

        private void UpdateFootLocks(float t, float fadeOut, float inAmount)
        {
            for (int i = 0; i < footLocks.Length; i++)
            {
                LayeredSprite spike = footLocks[i];
                float delay = i * 0.028f;
                float spikeIn = EaseOutBack(Mathf.Clamp01((t - delay) / 0.14f));
                spike.root.localScale = new Vector3(1f, spikeIn * (1f + Mathf.Sin(elapsed * 5.5f + i) * 0.025f), 1f);

                spike.back.color = WithAlpha(IcyDepth, fadeOut * 0.65f);
                spike.front.color = WithAlpha(i % 2 == 0 ? FrostCyan : GlacierBlue, fadeOut * 0.74f);
            }
        }

        private void UpdateSnowMotes(float fadeOut)
        {
            for (int i = 0; i < snowMotes.Length; i++)
            {
                SpriteRenderer mote = snowMotes[i];
                float speed = 0.24f + (i % 4) * 0.05f;
                float phase = Mathf.Repeat(elapsed * speed + i * 0.133f, 1f);
                float side = Mathf.Sin(i * 12.9898f) * 0.58f;
                float drift = Mathf.Sin(elapsed * (1.7f + i * 0.08f) + i) * 0.11f;

                mote.transform.localPosition = new Vector3(side * 0.74f + drift, -0.28f + phase * 1.12f, 0f);
                mote.transform.localRotation = Quaternion.Euler(0f, 0f, elapsed * (44f + i * 7f));
                mote.transform.localScale = Vector3.one * Mathf.Lerp(0.022f, 0.05f, (i % 5) / 4f);

                float alpha = Mathf.Sin(phase * Mathf.PI) * fadeOut * (i % 4 == 0 ? 0.56f : 0.42f);
                mote.color = WithAlpha(i % 4 == 0 ? GlacierBlue : IceWhite, alpha);
            }
        }

        private static Transform CreateChild(string name, Transform parent)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static SpriteRenderer CreateSpriteRenderer(string name, Transform parent, Sprite sprite)
        {
            Transform child = CreateChild(name, parent);
            SpriteRenderer sr = child.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = sharedMaterial;
            return sr;
        }

        private static void EnsureSharedAssets()
        {
            if (sharedMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                sharedMaterial = shader != null ? new Material(shader) : null;
            }

            if (crystalSprite == null)
                crystalSprite = CreateSprite(CreateCrystalTexture(), "Chubbloo Crystal Sprite");

            if (snowflakeSprite == null)
                snowflakeSprite = CreateSprite(CreateSnowflakeTexture(), "Chubbloo Snowflake Sprite");

            if (softCircleSprite == null)
                softCircleSprite = CreateSprite(CreateSoftCircleTexture(), "Chubbloo Soft Circle Sprite");

            if (ringSprite == null)
                ringSprite = CreateSprite(CreateRingTexture(), "Chubbloo Ring Sprite");

            if (sparkleSprite == null)
                sparkleSprite = CreateSprite(CreateSparkleTexture(), "Chubbloo Sparkle Sprite");

            if (mistSprite == null)
                mistSprite = CreateSprite(CreateMistTexture(), "Chubbloo Mist Sprite");
        }

        private static Sprite CreateSprite(Texture2D tex, string name)
        {
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            sprite.name = name;
            return sprite;
        }

        private static Texture2D CreateCrystalTexture()
        {
            const int size = 64;
            Texture2D tex = NewTexture(size, "Chubbloo Crystal Texture");
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y) - center;
                    float nx = Mathf.Abs(p.x) / center.x;
                    float ny = Mathf.Abs(p.y) / center.y;

                    float body = 1f - (nx * 1.75f + ny * 0.92f);
                    float tip = 1f - (nx * 1.05f + ny * 1.35f);
                    float alpha = Mathf.Clamp01(Mathf.Max(body, tip));
                    alpha = Smooth01(alpha);

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return tex;
        }

        private static Texture2D CreateSnowflakeTexture()
        {
            const int size = 96;
            Texture2D tex = NewTexture(size, "Chubbloo Snowflake Texture");
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y) - center;
                    float dist = p.magnitude / center.x;
                    float angle = Mathf.Atan2(p.y, p.x);

                    float armDistance = Mathf.Abs(Mathf.Sin(angle * 3f));
                    float arms = Mathf.Clamp01(1f - armDistance * 18f) * Mathf.Clamp01(1.05f - dist);
                    float core = Mathf.Clamp01(1f - dist * 4.2f);
                    float branches = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sin(angle * 6f)) * 11f)
                                     * Mathf.Clamp01(1f - Mathf.Abs(dist - 0.48f) * 8f) * 0.55f;

                    float alpha = Mathf.Clamp01(arms + core + branches);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return tex;
        }

        private static Texture2D CreateSoftCircleTexture()
        {
            const int size = 96;
            Texture2D tex = NewTexture(size, "Chubbloo Soft Circle Texture");
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = (new Vector2(x, y) - center).magnitude / center.x;
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = alpha * alpha * (3f - 2f * alpha);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return tex;
        }

        private static Texture2D CreateRingTexture()
        {
            const int size = 128;
            Texture2D tex = NewTexture(size, "Chubbloo Ring Texture");
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = (new Vector2(x, y) - center).magnitude / center.x;
                    float alpha = 1f - Mathf.Clamp01(Mathf.Abs(dist - 0.72f) / 0.06f);
                    alpha = alpha * alpha;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return tex;
        }

        private static Texture2D CreateSparkleTexture()
        {
            const int size = 64;
            Texture2D tex = NewTexture(size, "Chubbloo Sparkle Texture");
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y) - center;
                    float nx = Mathf.Abs(p.x) / center.x;
                    float ny = Mathf.Abs(p.y) / center.y;
                    float horizontal = Mathf.Clamp01(1f - (nx * 0.65f + ny * 8f));
                    float vertical = Mathf.Clamp01(1f - (ny * 0.65f + nx * 8f));
                    float alpha = Mathf.Max(horizontal, vertical);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return tex;
        }

        private static Texture2D CreateMistTexture()
        {
            const int size = 96;
            Texture2D tex = NewTexture(size, "Chubbloo Mist Texture");
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = (new Vector2(x, y) - center) / center.x;
                    float dist = p.magnitude;
                    float alpha = Mathf.Clamp01(1f - dist * 1.2f);
                    alpha = Mathf.Pow(alpha, 1.8f) * (0.72f + 0.28f * Mathf.PerlinNoise((x + 11f) * 0.09f, (y + 5f) * 0.09f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return tex;
        }

        private static Texture2D NewTexture(int size, string name)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = name
            };
            return tex;
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static float EaseOutBack(float value)
        {
            value = Mathf.Clamp01(value);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float p = value - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }
}
