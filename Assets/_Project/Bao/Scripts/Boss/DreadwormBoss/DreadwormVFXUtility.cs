using UnityEngine;
using UnityEngine.Rendering;

namespace Bao.Bosses
{
    public static class DreadwormVFXUtility
    {
        public const int SortingBaseOrder = 80;

        public static readonly Color FrostCyan = new Color(0.18f, 1.25f, 1.55f, 1f);
        public static readonly Color CrystalWhite = new Color(1.45f, 1.75f, 1.8f, 1f);
        public static readonly Color StormBlue = new Color(0.12f, 0.35f, 1.8f, 1f);
        public static readonly Color DeepTeal = new Color(0.02f, 0.2f, 0.22f, 0.82f);
        public static readonly Color GroundDust = new Color(0.18f, 0.24f, 0.25f, 0.82f);

        // --- Living Arctic & Glacial Jade Palette (Spine Eruption & Glacial Magic) ---
        public static readonly Color GlacialJadeCore = new Color(0.25f, 1.25f, 1.15f, 1f);       // Translucent mint jade core (HDR calibrated)
        public static readonly Color GlacialApexDiamond = new Color(0.95f, 1.45f, 1.75f, 1f);    // Crisp diamond apex highlight (HDR calibrated)
        public static readonly Color GlacialDeepNavy = new Color(0.04f, 0.10f, 0.22f, 1f);       // Deep abyssal navy shadow
        public static readonly Color BasaltRootDark = new Color(0.08f, 0.12f, 0.18f, 1.0f);      // Dark obsidian mineral footing
        public static readonly Color CryoMistCyan = new Color(0.25f, 0.85f, 1.15f, 0.65f);       // Dense ground cryo mist

        // --- Ruby & Burnt Crimson Palette (Skill 3 & Subterranean Attacks) ---
        public static readonly Color BloodRubyCore = new Color(2.15f, 0.24f, 0.38f, 1f);      // Intense HDR Crimson
        public static readonly Color MoltenAmber = new Color(2.05f, 0.72f, 0.15f, 1f);       // Hot glowing ember orange
        public static readonly Color SeismicAmber = new Color(2.35f, 0.88f, 0.18f, 1f);      // Ultra bright seismic amber
        public static readonly Color PlasmaElectricCyan = new Color(0.45f, 1.85f, 2.35f, 1f); // Supercharged Plasma Arc Cyan
        public static readonly Color GlintFrostApex = new Color(1.85f, 2.25f, 2.55f, 1f);    // Super bright Glint Frost White
        public static readonly Color BurntTerracotta = new Color(0.72f, 0.20f, 0.14f, 0.88f);  // Boss belly earthy tone
        public static readonly Color DeepMaroonAlpha = new Color(0.45f, 0.06f, 0.09f, 0.75f);  // Dark ground telegraph fill
        public static readonly Color RubyHighlight = new Color(1.85f, 1.45f, 1.35f, 1f);      // Bright crystal apex highlight
        public static readonly Color EruptionDust = new Color(0.38f, 0.18f, 0.14f, 0.85f);     // Terracotta earth & dust

        private static Texture2D softDiscTexture;
        private static Texture2D ringTexture;
        private static Texture2D shardTexture;
        private static Texture2D streakTexture;
        private static Texture2D chevronTexture;

        private static Sprite softDiscSprite;
        private static Sprite ringSprite;
        private static Sprite shardSprite;
        private static Sprite chevronSprite;

        private static Sprite spikeTier1Sprite;
        private static Sprite spikeTier2Sprite;
        private static Sprite spikeTier3Sprite;
        private static Sprite abyssalFissureSprite;

        public static Sprite SpikeTier1Sprite
        {
            get
            {
                if (spikeTier1Sprite == null)
                    spikeTier1Sprite = Resources.Load<Sprite>("Textures/VFX/Spikes/Spike_Glacial_Tier1");
                return spikeTier1Sprite != null ? spikeTier1Sprite : ShardSprite;
            }
        }

        public static Sprite SpikeTier2Sprite
        {
            get
            {
                if (spikeTier2Sprite == null)
                    spikeTier2Sprite = Resources.Load<Sprite>("Textures/VFX/Spikes/Spike_Glacial_Tier2");
                return spikeTier2Sprite != null ? spikeTier2Sprite : ShardSprite;
            }
        }

        public static Sprite SpikeTier3Sprite
        {
            get
            {
                if (spikeTier3Sprite == null)
                    spikeTier3Sprite = Resources.Load<Sprite>("Textures/VFX/Spikes/Spike_Glacial_Tier3");
                return spikeTier3Sprite != null ? spikeTier3Sprite : ShardSprite;
            }
        }

        public static Sprite AbyssalFissureSprite
        {
            get
            {
                if (abyssalFissureSprite == null)
                    abyssalFissureSprite = Resources.Load<Sprite>("Textures/VFX/Spikes/Decal_Abyssal_Fissure");
                return abyssalFissureSprite != null ? abyssalFissureSprite : RingSprite;
            }
        }

        public static Texture2D SoftDiscTexture
        {
            get
            {
                EnsureResources();
                return softDiscTexture;
            }
        }

        public static Texture2D RingTexture
        {
            get
            {
                EnsureResources();
                return ringTexture;
            }
        }

        public static Texture2D ShardTexture
        {
            get
            {
                EnsureResources();
                return shardTexture;
            }
        }

        public static Texture2D StreakTexture
        {
            get
            {
                EnsureResources();
                return streakTexture;
            }
        }

        public static Sprite SoftDiscSprite
        {
            get
            {
                EnsureResources();
                return softDiscSprite;
            }
        }

        public static Sprite RingSprite
        {
            get
            {
                EnsureResources();
                return ringSprite;
            }
        }

        public static Sprite ShardSprite
        {
            get
            {
                EnsureResources();
                return shardSprite;
            }
        }

        public static Sprite ChevronSprite
        {
            get
            {
                EnsureResources();
                return chevronSprite;
            }
        }

        public static Material CreateMaterial(
            Color tint,
            bool additive,
            Texture texture = null,
            string materialName = "Dreadworm_VFX")
        {
            EnsureResources();

            Shader shader = Shader.Find("Bao/DreadwormVFX");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            Material material = new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.DontSave
            };

            Texture selectedTexture = texture != null ? texture : softDiscTexture;
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", selectedTexture);
            if (material.HasProperty("_TintColor"))
                material.SetColor("_TintColor", tint);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            if (material.HasProperty("_PulseStrength"))
                material.SetFloat("_PulseStrength", additive ? 0.22f : 0.05f);

            return material;
        }

        public static Material CreateFrozenCocoonMaterial(string materialName = "Dreadworm_FrozenCocoon")
        {
            Shader shader = Shader.Find("Bao/DreadwormFrozenCocoonShader");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            Material material = new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.DontSave
            };

            return material;
        }

        public static void ConfigureRenderer(Renderer renderer, int orderOffset)
        {
            if (renderer == null)
                return;

            SortingLayer[] layers = SortingLayer.layers;
            if (layers != null && layers.Length > 0)
                renderer.sortingLayerID = layers[layers.Length - 1].id;
            renderer.sortingOrder = SortingBaseOrder + orderOffset;
        }

        public static void ConfigureSortingGroup(GameObject root, int orderOffset)
        {
            if (root == null)
                return;

            SortingGroup group = root.GetComponent<SortingGroup>();
            if (group == null)
                group = root.AddComponent<SortingGroup>();

            SortingLayer[] layers = SortingLayer.layers;
            if (layers != null && layers.Length > 0)
                group.sortingLayerID = layers[layers.Length - 1].id;
            group.sortingOrder = SortingBaseOrder + orderOffset;
        }

        public static ParticleSystem CreateParticleSystem(
            GameObject root,
            string objectName,
            Material material,
            int orderOffset)
        {
            GameObject particleObject = new GameObject(objectName);
            particleObject.transform.SetParent(root.transform, false);

            ParticleSystem particleSystem = particleObject.AddComponent<ParticleSystem>();
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            ConfigureRenderer(renderer, orderOffset);
            return particleSystem;
        }

        public static float EaseOutCubic(float value)
        {
            float t = Mathf.Clamp01(value);
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        public static float EaseInOut(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        private static void EnsureResources()
        {
            if (softDiscTexture != null &&
                ringTexture != null &&
                shardTexture != null &&
                streakTexture != null &&
                chevronTexture != null)
            {
                return;
            }

            softDiscTexture = CreateSoftDiscTexture(64);
            ringTexture = CreateRingTexture(128);
            shardTexture = CreateShardTexture(64);
            streakTexture = CreateStreakTexture(64, 16);
            chevronTexture = CreateChevronTexture(64, 48);

            softDiscSprite = CreateSprite(softDiscTexture, "Dreadworm_SoftDisc");
            ringSprite = CreateSprite(ringTexture, "Dreadworm_Ring");
            shardSprite = CreateSprite(shardTexture, "Dreadworm_Shard");
            chevronSprite = CreateSprite(chevronTexture, "Dreadworm_Chevron");
        }

        private static Texture2D CreateSoftDiscTexture(int size)
        {
            Texture2D texture = CreateTexture(size, size, "Dreadworm_SoftDiscTexture");
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 1.65f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            FinishTexture(texture);
            return texture;
        }

        private static Texture2D CreateRingTexture(int size)
        {
            Texture2D texture = CreateTexture(size, size, "Dreadworm_RingTexture");
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.385f;
            float thickness = size * 0.055f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 delta = new Vector2(x, y) - center;
                    float angle = Mathf.Atan2(delta.y, delta.x);
                    float irregularity = Mathf.Sin(angle * 7f) * size * 0.008f +
                                         Mathf.Sin(angle * 13f + 0.8f) * size * 0.004f;
                    float ringDistance = Mathf.Abs(delta.magnitude - radius - irregularity);
                    float alpha = Mathf.SmoothStep(1f, 0f, ringDistance / thickness);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            FinishTexture(texture);
            return texture;
        }

        private static Texture2D CreateShardTexture(int size)
        {
            Texture2D texture = CreateTexture(size, size, "Dreadworm_ShardTexture");

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (size - 1f);
                    float v = y / (size - 1f);
                    float halfWidth = Mathf.Lerp(0.04f, 0.48f, v) * (1f - v * 0.72f);
                    float distanceFromCenter = Mathf.Abs(u - 0.5f);
                    float edge = Mathf.Clamp01((halfWidth - distanceFromCenter) * size * 0.55f);
                    float tipFade =
                        SmoothThreshold(0f, 0.08f, v) *
                        (1f - SmoothThreshold(0.72f, 1f, v));
                    float alpha = edge * tipFade;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            FinishTexture(texture);
            return texture;
        }

        private static Texture2D CreateStreakTexture(int width, int height)
        {
            Texture2D texture = CreateTexture(width, height, "Dreadworm_StreakTexture");

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = x / (width - 1f);
                    float v = y / (height - 1f);
                    float longitudinal =
                        Mathf.Pow(1f - u, 0.55f) *
                        SmoothThreshold(0f, 0.10f, u);
                    float lateral = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v - 0.5f) * 2f), 2f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, longitudinal * lateral));
                }
            }

            FinishTexture(texture);
            return texture;
        }

        private static Texture2D CreateChevronTexture(int width, int height)
        {
            Texture2D texture = CreateTexture(width, height, "Dreadworm_ChevronTexture");

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = x / (width - 1f);
                    float v = y / (height - 1f);
                    float centerLine = 0.5f + Mathf.Abs(v - 0.5f) * 0.72f;
                    float distance = Mathf.Abs(u - centerLine);
                    float alpha = 1f - SmoothThreshold(0.025f, 0.10f, distance);
                    alpha *=
                        SmoothThreshold(0f, 0.12f, u) *
                        (1f - SmoothThreshold(0.82f, 1f, u));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            FinishTexture(texture);
            return texture;
        }

        private static Texture2D CreateTexture(int width, int height, string textureName)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = textureName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
        }

        private static void FinishTexture(Texture2D texture)
        {
            texture.Apply(false, true);
        }

        private static Sprite CreateSprite(Texture2D texture, string spriteName)
        {
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                texture.width);
            sprite.name = spriteName;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private static float SmoothThreshold(float minimum, float maximum, float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(minimum, maximum, value));
        }
    }
}
