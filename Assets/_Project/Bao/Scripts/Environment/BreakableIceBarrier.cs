using System.Collections;
using Bao.Enemies;
using UnityEngine;

namespace Bao.Environment
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class BreakableIceBarrier : MonoBehaviour, IDamageable
    {
        [Header("Durability")]
        [SerializeField, Min(1)] private int hitsToBreak = 2;

        [Header("Visual References")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer[] iceRenderers;

        [Header("Hit Reaction")]
        [SerializeField] private Color damageTint = new(0.35f, 0.82f, 1f, 1f);
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField, Range(0f, 1f)] private float damagedTintStrength = 0.62f;
        [SerializeField, Min(0.02f)] private float hitReactionDuration = 0.2f;
        [SerializeField, Min(0f)] private float hitShakeMagnitude = 0.075f;
        [SerializeField] private Vector2 hitPunchScale = new(1.15f, 0.86f);
        [SerializeField, Min(1)] private int hitChipCount = 10;
        [SerializeField, Min(0f)] private float hitChipSpeed = 3.2f;
        [SerializeField] private Color hitChipColor = new(0.62f, 0.94f, 1f, 1f);
        [SerializeField] private AudioClip hitClip;
        [SerializeField, Range(0f, 1f)] private float hitVolume = 0.7f;
        [SerializeField, Min(0f)] private float hitCameraShake = 0.025f;

        [Header("Break VFX")]
        [SerializeField, Min(1)] private int shardCount = 34;
        [SerializeField, Min(0.1f)] private float shardLifetime = 0.75f;
        [SerializeField, Min(0f)] private float shardSpeed = 6.5f;
        [SerializeField, Min(0.01f)] private float shardSize = 0.16f;
        [SerializeField] private Color shardColor = new(0.42f, 0.9f, 1f, 1f);
        [SerializeField, Min(1)] private int sparkleCount = 24;
        [SerializeField, Min(1)] private int mistCount = 12;
        [SerializeField] private Color sparkleColor = new(0.78f, 0.98f, 1f, 1f);
        [SerializeField] private Color mistColor = new(0.45f, 0.86f, 1f, 0.32f);
        [SerializeField, Min(0.02f)] private float breakVisualDuration = 0.14f;
        [SerializeField] private AudioClip breakClip;
        [SerializeField, Range(0f, 1f)] private float breakVolume = 1f;
        [SerializeField, Min(0f)] private float breakCameraShakeDuration = 0.18f;
        [SerializeField, Min(0f)] private float breakCameraShakeMagnitude = 0.09f;

        private static Material diamondMaterial;
        private static Material softMaterial;
        private static Material ringMaterial;
        private static Sprite crackSprite;

        private int remainingHits;
        private bool isBroken;
        private Color[] originalColors;
        private Vector3 originalVisualPosition;
        private Vector3 originalVisualScale;
        private Coroutine reactionRoutine;
        private SpriteRenderer crackRenderer;

        private void Awake()
        {
            remainingHits = Mathf.Max(1, hitsToBreak);
            CacheVisuals();
            ApplyColor(0f);
        }

        private void OnValidate()
        {
            hitsToBreak = Mathf.Max(1, hitsToBreak);
            hitChipCount = Mathf.Max(1, hitChipCount);
            shardCount = Mathf.Max(1, shardCount);
            sparkleCount = Mathf.Max(1, sparkleCount);
            mistCount = Mathf.Max(1, mistCount);
        }

        public void TakeDamage(int damage)
        {
            if (isBroken || damage <= 0)
            {
                return;
            }

            remainingHits = Mathf.Max(0, remainingHits - 1);
            if (remainingHits == 0)
            {
                Break();
            }
            else
            {
                ShowCracks();
                PlayHitFeedback();
            }
        }

        private void CacheVisuals()
        {
            if (iceRenderers == null || iceRenderers.Length == 0)
            {
                iceRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            }

            if (visualRoot == null && iceRenderers.Length > 0 && iceRenderers[0] != null)
            {
                visualRoot = iceRenderers[0].transform;
            }

            visualRoot = visualRoot != null ? visualRoot : transform;
            originalVisualPosition = visualRoot.localPosition;
            originalVisualScale = visualRoot.localScale;
            originalColors = new Color[iceRenderers.Length];
            for (int index = 0; index < iceRenderers.Length; index++)
            {
                if (iceRenderers[index] != null)
                {
                    originalColors[index] = iceRenderers[index].color;
                }
            }
        }

        private void PlayHitFeedback()
        {
            if (reactionRoutine != null)
            {
                StopCoroutine(reactionRoutine);
            }

            ResetVisual();
            reactionRoutine = StartCoroutine(HitReactionRoutine());
            SpawnHitVfx(BarrierBounds);
            PlayClip(hitClip, hitVolume);

            if (hitCameraShake > 0f && global::CameraController.Instance != null)
            {
                global::CameraController.Instance.ShakeCamera(hitReactionDuration, hitCameraShake);
            }
        }

        private IEnumerator HitReactionRoutine()
        {
            float elapsed = 0f;
            while (elapsed < hitReactionDuration)
            {
                float normalized = Mathf.Clamp01(elapsed / hitReactionDuration);
                float punch = Mathf.Sin(normalized * Mathf.PI) * (1f - normalized * 0.55f);
                Vector2 shake = Random.insideUnitCircle * hitShakeMagnitude * (1f - normalized);
                visualRoot.localPosition = originalVisualPosition + (Vector3)shake;
                visualRoot.localScale = Vector3.Scale(originalVisualScale, new Vector3(
                    Mathf.Lerp(1f, hitPunchScale.x, punch),
                    Mathf.Lerp(1f, hitPunchScale.y, punch),
                    1f));
                ApplyColor(Mathf.Pow(1f - normalized, 2f));
                elapsed += Time.deltaTime;
                yield return null;
            }

            ResetVisual();
            ApplyColor(0f);
            reactionRoutine = null;
        }

        private void ApplyColor(float flash)
        {
            float progress = 1f - remainingHits / (float)Mathf.Max(1, hitsToBreak);
            for (int index = 0; index < iceRenderers.Length; index++)
            {
                SpriteRenderer renderer = iceRenderers[index];
                if (renderer == null)
                {
                    continue;
                }

                Color damaged = Color.Lerp(originalColors[index], damageTint, progress * damagedTintStrength);
                renderer.color = Color.Lerp(damaged, hitFlashColor, Mathf.Clamp01(flash));
            }
        }

        private void Break()
        {
            isBroken = true;
            Bounds effectBounds = BarrierBounds;
            foreach (Collider2D barrierCollider in GetComponentsInChildren<Collider2D>(true))
            {
                barrierCollider.enabled = false;
            }

            if (reactionRoutine != null)
            {
                StopCoroutine(reactionRoutine);
                reactionRoutine = null;
            }

            ResetVisual();
            SpawnBreakVfx(effectBounds);
            PlayClip(breakClip, breakVolume);

            if (breakCameraShakeMagnitude > 0f && global::CameraController.Instance != null)
            {
                global::CameraController.Instance.ShakeCamera(breakCameraShakeDuration, breakCameraShakeMagnitude);
            }

            StartCoroutine(BreakVisualRoutine());
        }

        private IEnumerator BreakVisualRoutine()
        {
            float elapsed = 0f;
            while (elapsed < breakVisualDuration)
            {
                float normalized = Mathf.Clamp01(elapsed / breakVisualDuration);
                visualRoot.localScale = originalVisualScale * (1f + Mathf.Sin(normalized * Mathf.PI) * 0.22f);
                visualRoot.localPosition = originalVisualPosition + (Vector3)(Random.insideUnitCircle * 0.1f * (1f - normalized));

                foreach (SpriteRenderer renderer in iceRenderers)
                {
                    if (renderer == null)
                    {
                        continue;
                    }

                    Color color = Color.Lerp(hitFlashColor, shardColor, normalized);
                    color.a = 1f - normalized;
                    renderer.color = color;
                }

                if (crackRenderer != null)
                {
                    Color crackColor = crackRenderer.color;
                    crackColor.a = 1f - normalized;
                    crackRenderer.color = crackColor;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            Destroy(gameObject);
        }

        private void ShowCracks()
        {
            if (crackRenderer == null)
            {
                GameObject crackObject = new("Ice Cracks");
                crackObject.transform.SetParent(visualRoot, false);
                crackObject.layer = visualRoot.gameObject.layer;
                crackRenderer = crackObject.AddComponent<SpriteRenderer>();
                crackRenderer.sprite = CrackSprite;
                crackRenderer.sortingLayerID = SortingLayerId;
                crackRenderer.sortingOrder = SortingOrder + 1;
                crackRenderer.color = new Color(0.28f, 0.82f, 1f, 0.92f);
            }

            crackRenderer.enabled = true;
        }

        private void SpawnHitVfx(Bounds bounds)
        {
            GameObject root = CreateEffectRoot("Ice Hit", bounds.center);
            ParticleSystem chips = CreateSystem(root.transform, "Chips", SortingOrder + 2, DiamondMaterial);
            ConfigureBurst(chips, hitChipCount, 0.24f, 0.42f, hitChipSpeed * 0.65f, hitChipSpeed, 0.055f, 0.13f, hitChipColor, 0.25f, bounds, true);
            ParticleSystem glints = CreateSystem(root.transform, "Glints", SortingOrder + 4, DiamondMaterial);
            ConfigureBurst(glints, Mathf.Max(5, hitChipCount / 2), 0.22f, 0.4f, 1.5f, 3.2f, 0.035f, 0.075f, sparkleColor, 0f, bounds, false);
            ApplyLifetimeCurves(glints, false);
            ParticleSystem ring = CreateSystem(root.transform, "Hit Ring", SortingOrder + 3, RingMaterial);
            ConfigureShockwave(ring, 0.25f, 3.4f, 0.65f);
            ParticleSystem glow = CreateSystem(root.transform, "Hit Glow", SortingOrder + 1, SoftMaterial);
            ConfigureFlash(glow, 0.24f, 0.48f, 3.2f, new Color(hitChipColor.r, hitChipColor.g, hitChipColor.b, 0.5f));
            Destroy(root, 0.7f);
        }

        private void SpawnBreakVfx(Bounds bounds)
        {
            GameObject root = CreateEffectRoot("Ice Barrier Break", bounds.center);
            ParticleSystem shards = CreateSystem(root.transform, "Shards", SortingOrder + 3, DiamondMaterial);
            ConfigureBurst(shards, shardCount, shardLifetime * 0.65f, shardLifetime, shardSpeed * 0.55f, shardSpeed, shardSize * 0.55f, shardSize * 1.3f, shardColor, 0.72f, bounds, true);

            ParticleSystem sparkles = CreateSystem(root.transform, "Sparkles", SortingOrder + 5, DiamondMaterial);
            ConfigureBurst(sparkles, sparkleCount, 0.28f, 0.62f, 2.4f, 5.2f, 0.035f, 0.1f, sparkleColor, 0f, bounds, false);
            ApplyLifetimeCurves(sparkles, false);

            ParticleSystem mist = CreateSystem(root.transform, "Frost Mist", SortingOrder + 1, SoftMaterial);
            ConfigureBurst(mist, mistCount, 0.5f, 0.95f, 0.25f, 1.05f, 0.28f, 0.72f, mistColor, -0.08f, bounds, false);
            ApplyLifetimeCurves(mist, true);

            ParticleSystem shockwave = CreateSystem(root.transform, "Shockwave", SortingOrder + 4, RingMaterial);
            ConfigureShockwave(shockwave, 0.38f, 6.5f, 0.8f);
            ParticleSystem flash = CreateSystem(root.transform, "Core Flash", SortingOrder + 2, SoftMaterial);
            ConfigureFlash(flash, 0.3f, 0.85f, 4.2f, new Color(sparkleColor.r, sparkleColor.g, sparkleColor.b, 0.72f));
            Destroy(root, Mathf.Max(shardLifetime, 0.95f) + 0.45f);
        }

        private ParticleSystem CreateSystem(Transform parent, string effectName, int order, Material material)
        {
            GameObject child = new(effectName);
            child.transform.SetParent(parent, false);
            ParticleSystem particles = child.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.duration = 0.1f;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 256;

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.material = material;
            renderer.sortingLayerID = SortingLayerId;
            renderer.sortingOrder = order;
            return particles;
        }

        private static void ConfigureBurst(ParticleSystem particles, int count, float minLife, float maxLife, float minSpeed, float maxSpeed, float minSize, float maxSize, Color color, float gravity, Bounds bounds, bool stretch)
        {
            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.65f));
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.gravityModifier = gravity;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 1, short.MaxValue)) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(bounds.size.x * 0.72f, bounds.size.y * 0.72f, 0.05f);
            shape.randomDirectionAmount = 0.72f;

            ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-7f, 7f);

            if (stretch)
            {
                ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 1.55f;
                renderer.velocityScale = 0.22f;
            }

            particles.Play(true);
        }

        private static void ApplyLifetimeCurves(ParticleSystem particles, bool grow)
        {
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = FadeGradient();
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, grow
                ? new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.35f, 1f), new Keyframe(1f, 1.65f))
                : new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.18f, 1.25f), new Keyframe(1f, 0.7f)));
        }

        private static void ConfigureFlash(ParticleSystem particles, float lifetime, float startSize, float finalScale, Color color)
        {
            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = startSize;
            main.startColor = color;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, finalScale)));
            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            fade.color = FadeGradient();
            particles.Play(true);
        }

        private void ConfigureShockwave(ParticleSystem particles, float lifetime, float finalScale, float alpha)
        {
            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = 0.5f;
            main.startColor = new Color(sparkleColor.r, sparkleColor.g, sparkleColor.b, alpha);
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(1f, finalScale)));
            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            fade.color = FadeGradient();
            particles.Play(true);
        }

        private static ParticleSystem.MinMaxGradient FadeGradient()
        {
            Gradient gradient = new();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.45f), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(gradient);
        }

        private static GameObject CreateEffectRoot(string effectName, Vector3 position)
        {
            GameObject root = new(effectName);
            root.transform.position = position;
            return root;
        }

        private void ResetVisual()
        {
            visualRoot.localPosition = originalVisualPosition;
            visualRoot.localScale = originalVisualScale;
        }

        private void PlayClip(AudioClip clip, float volume)
        {
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, transform.position, volume);
            }
        }

        private Bounds BarrierBounds
        {
            get
            {
                Collider2D barrierCollider = GetComponent<Collider2D>();
                return barrierCollider != null ? barrierCollider.bounds : new Bounds(transform.position, Vector3.one * 2f);
            }
        }

        private int SortingOrder => iceRenderers.Length > 0 && iceRenderers[0] != null ? iceRenderers[0].sortingOrder : 0;
        private int SortingLayerId => iceRenderers.Length > 0 && iceRenderers[0] != null ? iceRenderers[0].sortingLayerID : 0;
        private static Material DiamondMaterial => diamondMaterial != null ? diamondMaterial : diamondMaterial = CreateMaterial(TextureStyle.Diamond);
        private static Material SoftMaterial => softMaterial != null ? softMaterial : softMaterial = CreateMaterial(TextureStyle.SoftCircle);
        private static Material RingMaterial => ringMaterial != null ? ringMaterial : ringMaterial = CreateMaterial(TextureStyle.Ring);

        private enum TextureStyle
        {
            Diamond,
            SoftCircle,
            Ring
        }

        private static Material CreateMaterial(TextureStyle style)
        {
            Material material = new(Shader.Find("Sprites/Default"))
            {
                name = $"Ice Barrier {style}",
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = CreateTexture(style)
            };
            return material;
        }

        private static Sprite CrackSprite
        {
            get
            {
                if (crackSprite == null)
                {
                    Texture2D texture = new(96, 96, TextureFormat.RGBA32, false)
                    {
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp,
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    Color[] clear = new Color[96 * 96];
                    texture.SetPixels(clear);
                    Vector2 center = new(48f, 48f);
                    DrawCrack(texture, center, new Vector2(8f, 84f));
                    DrawCrack(texture, center, new Vector2(87f, 12f));
                    DrawCrack(texture, center, new Vector2(88f, 82f));
                    DrawCrack(texture, center, new Vector2(12f, 19f));
                    DrawCrack(texture, new Vector2(32f, 58f), new Vector2(15f, 72f));
                    DrawCrack(texture, new Vector2(65f, 33f), new Vector2(76f, 48f));
                    texture.Apply(false, true);
                    crackSprite = Sprite.Create(texture, new Rect(0f, 0f, 96f, 96f), new Vector2(0.5f, 0.5f), 32f);
                    crackSprite.hideFlags = HideFlags.HideAndDontSave;
                }

                return crackSprite;
            }
        }

        private static void DrawCrack(Texture2D texture, Vector2 from, Vector2 to)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(from, to));
            for (int index = 0; index <= steps; index++)
            {
                float t = index / (float)Mathf.Max(1, steps);
                Vector2 point = Vector2.Lerp(from, to, t);
                int x = Mathf.RoundToInt(point.x);
                int y = Mathf.RoundToInt(point.y);
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    for (int offsetY = -1; offsetY <= 1; offsetY++)
                    {
                        if (x + offsetX >= 0 && x + offsetX < 96 && y + offsetY >= 0 && y + offsetY < 96)
                        {
                            float alpha = offsetX == 0 && offsetY == 0 ? 0.95f : 0.35f;
                            texture.SetPixel(x + offsetX, y + offsetY, new Color(1f, 1f, 1f, alpha));
                        }
                    }
                }
            }
        }

        private static Texture2D CreateTexture(TextureStyle style)
        {
            const int size = 32;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    float radius = Mathf.Sqrt(nx * nx + ny * ny);
                    float alpha = style == TextureStyle.Diamond
                        ? Mathf.Clamp01((1f - Mathf.Abs(nx) - Mathf.Abs(ny)) * 7f)
                        : style == TextureStyle.Ring
                            ? Mathf.Clamp01(1f - Mathf.Abs(radius - 0.68f) / 0.12f) * Mathf.Clamp01((1f - radius) * 6f)
                            : Mathf.Pow(Mathf.Clamp01(1f - radius), 2.2f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, true);
            return texture;
        }
    }
}
