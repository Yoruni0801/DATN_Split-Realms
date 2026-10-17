using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bao.Bosses
{
    /// <summary>
    /// Lightweight procedural VFX helpers for Nidus skill telegraphs and impacts.
    /// Intentionally avoids spawning a full NidusBurrowGroundVFX for every root spike.
    /// </summary>
    public static class NidusOrganicSkillVFX
    {
        private static Material lineMaterial;
        private static Material glowMaterial;
        private static Material particleMaterial;
        private static Material acidDetonationMaterial;
        private static Sprite rootThornSprite;
        private static Sprite acidBlobSprite;

        private static readonly Color RootBrown = new Color(0.24f, 0.07f, 0.025f, 0.92f);
        private static readonly Color AcidGreen = new Color(0.16f, 1.25f, 0.025f, 0.95f);
        private static readonly Color AcidYellow = new Color(0.78f, 1.65f, 0.08f, 1f);
        private static readonly Color CoreTeal = new Color(0.20f, 1.55f, 0.85f, 1f);

        internal static string SortingLayerName
        {
            get
            {
                SortingLayer[] layers = SortingLayer.layers;
                if (layers == null || layers.Length == 0) return "Default";
                SortingLayer top = layers[0];
                for (int i = 1; i < layers.Length; i++)
                    if (layers[i].value > top.value) top = layers[i];
                return top.name;
            }
        }

        private static Material GetLineMaterial(bool glow)
        {
            if (glow && glowMaterial != null) return glowMaterial;
            if (!glow && lineMaterial != null) return lineMaterial;

            // This VFX runs in URP; using the legacy sprite shader can render as
            // Unity's error-magenta material when the active renderer changes.
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material mat = new Material(shader)
            {
                hideFlags = HideFlags.DontSave,
                name = glow ? "NidusOrganic_Glow" : "NidusOrganic_Root"
            };
            mat.color = glow ? AcidGreen : RootBrown;
            if (glow) glowMaterial = mat;
            else lineMaterial = mat;
            return mat;
        }

        private static Material GetParticleMaterial()
        {
            if (particleMaterial != null) return particleMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            particleMaterial = new Material(shader)
            {
                hideFlags = HideFlags.DontSave,
                name = "NidusOrganic_Particles"
            };
            return particleMaterial;
        }

        private static Material GetAcidDetonationMaterial()
        {
            if (acidDetonationMaterial != null) return acidDetonationMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            acidDetonationMaterial = new Material(shader)
            {
                hideFlags = HideFlags.DontSave,
                name = "NidusOrganic_AcidDetonation"
            };

            Texture texture = GetAcidBlobSprite().texture;
            if (acidDetonationMaterial.HasProperty("_BaseMap"))
                acidDetonationMaterial.SetTexture("_BaseMap", texture);
            if (acidDetonationMaterial.HasProperty("_MainTex"))
                acidDetonationMaterial.SetTexture("_MainTex", texture);
            return acidDetonationMaterial;
        }

        private static Sprite GetAcidBlobSprite()
        {
            if (acidBlobSprite != null) return acidBlobSprite;

            const int size = 96;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "NidusOrganic_AcidBlobTexture",
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2(x / (size - 1f) - 0.5f, y / (size - 1f) - 0.5f);
                    float angle = Mathf.Atan2(point.y, point.x);
                    float distance = new Vector2(point.x, point.y * 1.08f).magnitude;
                    float edge = 0.38f + Mathf.Sin(angle * 5f + 0.7f) * 0.012f + Mathf.Sin(angle * 9f) * 0.006f;
                    float edgeFade = 1f - Mathf.SmoothStep(edge - 0.075f, edge + 0.035f, distance);
                    float innerGlow = 0.9f + 0.1f * Mathf.SmoothStep(edge, 0f, distance);
                    float alpha = Mathf.Clamp01(edgeFade * innerGlow);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            acidBlobSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            acidBlobSprite.name = "NidusOrganic_AcidBlobSprite";
            acidBlobSprite.hideFlags = HideFlags.DontSave;
            return acidBlobSprite;
        }

        internal static Sprite GetRootThornSprite()
        {
            if (rootThornSprite != null) return rootThornSprite;

            Texture2D texture = Resources.Load<Texture2D>("Nidus_RootThorn");
            if (texture == null)
            {
                Debug.LogWarning("Nidus root-thorn sprite is missing from Assets/Resources/Nidus_RootThorn.png.");
                return null;
            }

            rootThornSprite = CreateRootThornSprite(texture);
            rootThornSprite.name = "Nidus_RootThorn_RuntimeSprite";
            rootThornSprite.hideFlags = HideFlags.DontSave;
            return rootThornSprite;
        }

        internal static Sprite CreateRootThornSprite(Texture2D texture)
        {
            return texture == null ? null : Sprite.Create(
                texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0f), 420f);
        }

        public static GameObject SpawnRootTelegraph(Vector3 position, float radius, float duration, bool large = false)
        {
            GameObject root = new GameObject("Nidus_RootTelegraph");
            root.transform.position = position;

            NidusRootTelegraphVisual visual = root.AddComponent<NidusRootTelegraphVisual>();
            visual.Initialize(
                Mathf.Max(0.2f, radius),
                Mathf.Max(0.12f, duration),
                large,
                GetLineMaterial(false),
                GetLineMaterial(true),
                RootBrown,
                AcidGreen,
                AcidYellow);
            return root;
        }

        public static GameObject SpawnRootBurst(Vector3 position, float radius, float height, float duration, bool large = false,
            NidusRootSpikeVisualSettings visualSettings = null)
        {
            GameObject root = new GameObject("Nidus_RootBurst");
            root.transform.position = position;

            NidusRootBurstVisual visual = root.AddComponent<NidusRootBurstVisual>();
            visual.Initialize(
                Mathf.Max(0.2f, radius),
                Mathf.Max(0.8f, height),
                Mathf.Max(0.2f, duration),
                large,
                GetLineMaterial(false),
                GetLineMaterial(true),
                GetParticleMaterial(),
                RootBrown,
                AcidGreen,
                AcidYellow,
                visualSettings);
            return root;
        }

        public static GameObject SpawnPulse(Vector3 position, float radius, float duration, Color? color = null)
        {
            GameObject root = new GameObject("Nidus_OrganicPulse");
            root.transform.position = position;
            NidusPulseVisual pulse = root.AddComponent<NidusPulseVisual>();
            pulse.Initialize(
                Mathf.Max(0.2f, radius),
                Mathf.Max(0.12f, duration),
                GetLineMaterial(true),
                color ?? AcidGreen);
            return root;
        }

        public static GameObject SpawnCocoonBurst(Vector3 position, Sprite sprite, float scale, float duration, float rotation, Color color,
            string sortingLayerName, int sortingOrder)
        {
            if (sprite == null)
                return null;

            GameObject root = new GameObject("Nidus_CocoonSpriteBurst");
            root.transform.position = position;
            NidusCocoonSpriteBurstVisual visual = root.AddComponent<NidusCocoonSpriteBurstVisual>();
            visual.Initialize(sprite, Mathf.Max(0.05f, scale), Mathf.Max(0.05f, duration), rotation, color, sortingLayerName, sortingOrder);
            return root;
        }

        public static GameObject SpawnAcidDetonation(Vector3 position, float radius)
        {
            GameObject root = new GameObject("Nidus_AcidDetonationVFX");
            root.transform.position = position;

            Sprite blob = GetAcidBlobSprite();

            SpawnPulse(position, radius * 1.32f, 0.34f, new Color(0.74f, 1f, 0.18f, 0.82f));
            NidusAcidDetonationVisual visual = root.AddComponent<NidusAcidDetonationVisual>();
            visual.Initialize(blob, radius, SortingLayerName);
            return root;
        }

        private static ParticleSystem CreateDetonationParticleSystem(GameObject root, string name, Material material, int sortingOrder)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(root.transform, false);

            ParticleSystem system = child.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.material = material;
            renderer.sortingLayerName = SortingLayerName;
            renderer.sortingOrder = sortingOrder;
            return system;
        }

        private static void ConfigureAcidGlobules(ParticleSystem system, float radius)
        {
            var main = system.main;
            main.duration = 0.14f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.62f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 1.7f, radius * 4.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.12f, radius * 0.32f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startColor = new ParticleSystem.MinMaxGradient(AcidGreen, AcidYellow);
            main.gravityModifier = -0.08f;
            main.maxParticles = 56;

            var emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18, 30) });

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * 0.2f;
            shape.radiusThickness = 1f;

            var sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.28f),
                new Keyframe(0.16f, 1f),
                new Keyframe(0.72f, 0.82f),
                new Keyframe(1f, 0.08f)));

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(AcidYellow, 0.18f),
                    new GradientColorKey(AcidGreen, 0.78f),
                    new GradientColorKey(new Color(0.12f, 0.42f, 0.04f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.07f),
                    new GradientAlphaKey(0.74f, 0.66f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-2.6f, 2.6f);

            var trails = system.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.PerParticle;
            trails.ratio = 0.75f;
            trails.lifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            trails.dieWithParticles = true;
            trails.inheritParticleColor = true;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(0.42f);

            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.22f;
            renderer.lengthScale = 1.3f;
            renderer.trailMaterial = renderer.material;
            system.Play();
        }

        private static void ConfigureAcidMist(ParticleSystem system, float radius)
        {
            var main = system.main;
            main.duration = 0.24f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.52f, 0.92f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.24f, radius * 0.72f);
            main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.2f, radius * 0.52f);
            main.startColor = new Color(0.36f, 0.92f, 0.1f, 0.6f);
            main.gravityModifier = -0.16f;
            main.maxParticles = 36;

            var emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.04f, 10, 18) });

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * 0.55f;
            shape.radiusThickness = 0.65f;

            var noise = system.noise;
            noise.enabled = true;
            noise.strength = radius * 0.32f;
            noise.frequency = 0.75f;
            noise.scrollSpeed = 0.5f;
            noise.damping = true;

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.75f, 1f, 0.2f), 0f),
                    new GradientColorKey(new Color(0.16f, 0.7f, 0.08f), 0.7f),
                    new GradientColorKey(new Color(0.06f, 0.23f, 0.05f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.48f, 0.14f),
                    new GradientAlphaKey(0.2f, 0.74f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.36f),
                new Keyframe(0.34f, 1f),
                new Keyframe(1f, 1.35f)));

            system.Play();
        }

        public static GameObject SpawnAbsorbSporeStream(Vector3 from, Vector3 to, float duration)
        {
            GameObject root = new GameObject("Nidus_AbsorbSpores");
            root.transform.position = from;
            NidusSporeStreamVisual stream = root.AddComponent<NidusSporeStreamVisual>();
            stream.Initialize(to, Mathf.Max(0.2f, duration), GetParticleMaterial(), AcidGreen);
            return root;
        }

        /// <summary>
        /// Rich "living core" effect that plays while the boss exposes its weak point.
        /// Layers a pulsing bio-luminescent core, an implosion of toxic spores that spiral
        /// inward, drifting embers and a soft aura glow so the moment reads clearly as the
        /// boss channelling its organic energy back into itself.
        /// </summary>
        public static GameObject SpawnCoreAbsorbBloom(Vector3 center, float radius, float duration)
        {
            GameObject root = new GameObject("Nidus_CoreAbsorbBloom");
            root.transform.position = center;
            NidusCoreAbsorbVisual visual = root.AddComponent<NidusCoreAbsorbVisual>();
            visual.Initialize(
                Mathf.Max(0.6f, radius),
                Mathf.Max(0.25f, duration),
                GetParticleMaterial(),
                GetLineMaterial(true),
                CoreTeal,
                AcidGreen,
                AcidYellow);
            return root;
        }
    }

    internal sealed class NidusRootTelegraphVisual : MonoBehaviour
    {
        private readonly List<LineRenderer> lines = new List<LineRenderer>();
        private float duration;
        private float elapsed;
        private float radius;
        private Color rootColor;
        private Color glowColor;
        private Color finalColor;

        public void Initialize(float radius, float duration, bool large, Material rootMaterial, Material glowMaterial,
            Color rootColor, Color glowColor, Color finalColor)
        {
            this.radius = radius;
            this.duration = duration;
            this.rootColor = rootColor;
            this.glowColor = glowColor;
            this.finalColor = finalColor;

            int branchCount = large ? 4 : 3;
            for (int i = 0; i < branchCount; i++)
            {
                float lane = branchCount == 1 ? 0f : (i / (branchCount - 1f) - 0.5f);
                Vector3[] points = BuildPoints(radius, lane, i);
                lines.Add(CreateLine("RootBase", rootMaterial, points, Mathf.Max(0.035f, radius * 0.08f), rootColor, 92));
                lines.Add(CreateLine("RootGlow", glowMaterial, points, Mathf.Max(0.012f, radius * 0.028f), glowColor, 93));
            }

            NidusOrganicSkillVFX.SpawnPulse(transform.position, radius * (large ? 1.2f : 0.95f), duration * 0.92f, glowColor);
            Destroy(gameObject, duration + 0.05f);
        }

        private static Vector3[] BuildPoints(float radius, float lane, int seed)
        {
            Vector3[] points = new Vector3[6];
            for (int i = 0; i < points.Length; i++)
            {
                float t = i / (points.Length - 1f);
                float x = Mathf.Lerp(-radius, radius, t);
                float y = lane * radius * 0.12f + Mathf.Sin(seed * 2.17f + t * 10.2f) * radius * 0.06f;
                points[i] = new Vector3(x, y, 0f);
            }
            return points;
        }

        private LineRenderer CreateLine(string name, Material material, Vector3[] points, float width, Color color, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = points.Length;
            lr.SetPositions(points);
            lr.sharedMaterial = material;
            lr.startWidth = width;
            lr.endWidth = width * 0.15f;
            lr.startColor = color;
            lr.endColor = new Color(color.r, color.g, color.b, color.a * 0.18f);
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.sortingLayerName = NidusOrganicSkillVFX.SortingLayerName;
            lr.sortingOrder = order;
            return lr;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float urgency = t * t;
            Color currentGlow = Color.Lerp(glowColor, finalColor, Mathf.InverseLerp(0.72f, 1f, t));
            float pulse = 0.72f + Mathf.Sin(elapsed * Mathf.Lerp(8f, 22f, urgency)) * 0.28f;

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i] == null) continue;
                bool glow = (i % 2) == 1;
                Color c = glow ? currentGlow : rootColor;
                float alpha = glow ? Mathf.Lerp(0.28f, 0.95f, urgency) * pulse : Mathf.Lerp(0.35f, 0.88f, urgency);
                c.a *= alpha;
                lines[i].startColor = c;
                lines[i].endColor = new Color(c.r, c.g, c.b, c.a * 0.08f);
            }
        }
    }

    internal sealed class NidusRootBurstVisual : MonoBehaviour
    {
        private readonly List<SpriteRenderer> roots = new List<SpriteRenderer>();
        private float duration;
        private float elapsed;
        private float height;
        private float emergeDuration;

        public void Initialize(float radius, float height, float duration, bool large, Material rootMaterial, Material glowMaterial,
            Material particleMaterial, Color rootColor, Color glowColor, Color tipColor,
            NidusRootSpikeVisualSettings visualSettings)
        {
            this.duration = duration;
            this.height = height * (visualSettings != null ? Mathf.Max(0.1f, visualSettings.heightMultiplier) : 1f);
            emergeDuration = visualSettings != null ? Mathf.Max(0.04f, visualSettings.emergeDuration) : 0.16f;

            // Standard spikes are one strong illustrated thorn. Root-prison uses a compact trio.
            int rootCount = visualSettings != null
                ? Mathf.Max(1, large ? visualSettings.prisonRootCount : visualSettings.normalRootCount)
                : (large ? 3 : 1);
            Sprite thornSprite = visualSettings != null && visualSettings.rootSprite != null
                ? visualSettings.rootSprite
                : visualSettings != null && visualSettings.rootTexture != null
                    ? NidusOrganicSkillVFX.CreateRootThornSprite(visualSettings.rootTexture)
                    : NidusOrganicSkillVFX.GetRootThornSprite();
            if (thornSprite == null)
            {
                Destroy(gameObject);
                return;
            }

            for (int r = 0; r < rootCount; r++)
            {
                float side = rootCount == 1 ? 0f : (r / (rootCount - 1f) - 0.5f);
                roots.Add(CreateRootSprite(thornSprite, side, radius, this.height, r, rootColor, visualSettings));
            }

            transform.localScale = new Vector3(1f, 0.04f, 1f);
            Destroy(gameObject, duration + 0.25f);
        }

        private SpriteRenderer CreateRootSprite(Sprite sprite, float side, float radius, float height, int index, Color tint,
            NidusRootSpikeVisualSettings visualSettings)
        {
            GameObject go = new GameObject("NidusRootThornSprite");
            go.transform.SetParent(transform, false);
            Vector2 offset = visualSettings != null ? visualSettings.spawnOffset : Vector2.zero;
            go.transform.localPosition = new Vector3(offset.x + side * radius * 0.82f, offset.y, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, visualSettings != null ? visualSettings.rotation : 0f);
            float targetWidth = Mathf.Max(0.55f, radius * (index == 1 ? 1.15f : 0.90f));
            targetWidth *= visualSettings != null ? Mathf.Max(0.1f, visualSettings.widthMultiplier) : 1f;
            go.transform.localScale = new Vector3(
                targetWidth / Mathf.Max(0.01f, sprite.bounds.size.x),
                height / Mathf.Max(0.01f, sprite.bounds.size.y), 1f);

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = visualSettings != null ? visualSettings.tint : tint;
            renderer.flipX = index % 2 == 1;
            renderer.sortingLayerName = NidusOrganicSkillVFX.SortingLayerName;
            renderer.sortingOrder = 106 + index;
            return renderer;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / emergeDuration), 3f);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, t));
            transform.localScale = new Vector3(1f, Mathf.Lerp(0.04f, 1f, grow), 1f);

            foreach (SpriteRenderer sprite in roots)
            {
                if (sprite == null) continue;
                Color c = sprite.color;
                c.a = fade;
                sprite.color = c;
            }
        }
    }

    internal sealed class NidusCocoonSpriteBurstVisual : MonoBehaviour
    {
        private SpriteRenderer renderer;
        private float duration;
        private float elapsed;
        private float scale;
        private float rotation;
        private Color color;

        public void Initialize(Sprite sprite, float scale, float duration, float rotation, Color color, string sortingLayerName, int sortingOrder)
        {
            this.scale = scale;
            this.duration = duration;
            this.rotation = rotation;
            this.color = color;

            renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;
            transform.localScale = Vector3.one * scale * 0.72f;
            Destroy(gameObject, duration + 0.05f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float ease = 1f - Mathf.Pow(1f - t, 3f);
            transform.localScale = Vector3.one * Mathf.Lerp(scale * 0.72f, scale * 1.24f, ease);
            transform.Rotate(0f, 0f, rotation * Time.deltaTime);
            Color c = color;
            c.a *= 1f - Mathf.SmoothStep(0f, 1f, t);
            if (renderer != null)
                renderer.color = c;
        }
    }

    internal sealed class NidusAcidDetonationVisual : MonoBehaviour
    {
        private sealed class Droplet
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector3 Start;
            public Vector3 End;
            public float ArcHeight;
            public float RotationSpeed;
            public float Scale;
            public Color Color;
        }

        private readonly List<Droplet> droplets = new List<Droplet>();
        private readonly List<Droplet> mist = new List<Droplet>();
        private readonly List<SpriteRenderer> coreLayers = new List<SpriteRenderer>();
        private readonly List<float> coreScales = new List<float>();
        private float duration;
        private float elapsed;

        public void Initialize(Sprite blob, float radius, string sortingLayerName)
        {
            duration = 0.78f;
            CreateCoreLayer(blob, sortingLayerName, 123, radius * 0.82f, new Color(0.92f, 1f, 0.28f, 0.92f), 14f);
            CreateCoreLayer(blob, sortingLayerName, 122, radius * 1.12f, new Color(0.25f, 0.92f, 0.09f, 0.7f), -11f);
            CreateCoreLayer(blob, sortingLayerName, 121, radius * 1.42f, new Color(0.1f, 0.5f, 0.05f, 0.32f), 7f);

            int dropletCount = Mathf.Clamp(Mathf.RoundToInt(radius * 7f), 11, 18);
            for (int i = 0; i < dropletCount; i++)
            {
                float angle = Mathf.PI * 2f * i / dropletCount + Random.Range(-0.18f, 0.18f);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.62f).normalized;
                float travel = radius * Random.Range(0.7f, 1.48f);
                float scale = radius * Random.Range(0.1f, 0.21f);
                Color color = Color.Lerp(
                    new Color(0.95f, 1f, 0.32f, 0.95f),
                    new Color(0.12f, 0.72f, 0.08f, 0.85f),
                    Random.value);
                Droplet droplet = CreateDroplet(
                    "AcidDroplet_" + i,
                    blob,
                    sortingLayerName,
                    124,
                    direction * radius * 0.08f,
                    direction * travel,
                    radius * Random.Range(0.12f, 0.34f),
                    scale,
                    color,
                    Random.Range(-240f, 240f));
                droplet.Transform.SetParent(transform, false);
                droplets.Add(droplet);
            }

            for (int i = 0; i < 5; i++)
            {
                Vector2 offset = Random.insideUnitCircle * radius * 0.32f;
                Droplet vapor = CreateDroplet(
                    "AcidVapor_" + i,
                    blob,
                    sortingLayerName,
                    118,
                    offset,
                    offset + Vector2.up * radius * Random.Range(0.65f, 1.15f),
                    0f,
                    radius * Random.Range(0.28f, 0.52f),
                    new Color(0.28f, 0.74f, 0.11f, Random.Range(0.14f, 0.24f)),
                    Random.Range(-40f, 40f));
                vapor.Transform.SetParent(transform, false);
                mist.Add(vapor);
            }

            Destroy(gameObject, duration + 0.06f);
        }

        private void CreateCoreLayer(Sprite sprite, string sortingLayerName, int sortingOrder, float scale, Color color, float rotationSpeed)
        {
            GameObject layer = new GameObject("AcidBloomCore");
            layer.transform.SetParent(transform, false);
            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;
            layer.transform.localScale = Vector3.one * scale * 0.25f;
            coreLayers.Add(renderer);
            coreScales.Add(scale);
            layer.AddComponent<NidusAcidSpin>().Initialize(rotationSpeed);
        }

        private static Droplet CreateDroplet(
            string name,
            Sprite sprite,
            string sortingLayerName,
            int sortingOrder,
            Vector3 start,
            Vector3 end,
            float arcHeight,
            float scale,
            Color color,
            float rotationSpeed)
        {
            GameObject drop = new GameObject(name);
            SpriteRenderer renderer = drop.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;
            drop.transform.localScale = Vector3.one * scale * 0.15f;
            return new Droplet
            {
                Transform = drop.transform,
                Renderer = renderer,
                Start = start,
                End = end,
                ArcHeight = arcHeight,
                RotationSpeed = rotationSpeed,
                Scale = scale,
                Color = color
            };
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float burstT = 1f - Mathf.Pow(1f - Mathf.Min(1f, t * 2.6f), 3f);

            for (int i = 0; i < coreLayers.Count; i++)
            {
                SpriteRenderer layer = coreLayers[i];
                if (layer == null) continue;
                float layerT = Mathf.Clamp01(t * (1.25f + i * 0.18f));
                layer.transform.localScale = Vector3.one * Mathf.Lerp(coreScales[i] * 0.25f, coreScales[i] * 1.35f, burstT);
                Color color = layer.color;
                color.a = Mathf.Lerp(color.a, 0f, Mathf.SmoothStep(0.18f, 1f, layerT));
                layer.color = color;
            }

            UpdateDropletGroup(droplets, t, 1f, 0.12f);
            UpdateDropletGroup(mist, t, 0.72f, 0.48f);
        }

        private static void UpdateDropletGroup(List<Droplet> group, float t, float lifetimeMultiplier, float fadeStart)
        {
            float localT = Mathf.Clamp01(t / lifetimeMultiplier);
            float travelT = 1f - Mathf.Pow(1f - localT, 3f);
            for (int i = 0; i < group.Count; i++)
            {
                Droplet droplet = group[i];
                if (droplet.Renderer == null) continue;

                droplet.Transform.localPosition = Vector3.Lerp(droplet.Start, droplet.End, travelT) +
                    Vector3.up * droplet.ArcHeight * 4f * travelT * (1f - travelT);
                droplet.Transform.Rotate(0f, 0f, droplet.RotationSpeed * Time.deltaTime);
                float pop = Mathf.Sin(Mathf.Clamp01(localT / 0.25f) * Mathf.PI * 0.5f);
                droplet.Transform.localScale = Vector3.one * droplet.Scale * Mathf.Lerp(0.2f, 1f, pop);

                Color color = droplet.Color;
                color.a *= 1f - Mathf.SmoothStep(fadeStart, 1f, localT);
                droplet.Renderer.color = color;
            }
        }
    }

    internal sealed class NidusAcidSpin : MonoBehaviour
    {
        private float rotationSpeed;

        public void Initialize(float speed)
        {
            rotationSpeed = speed;
        }

        private void Update()
        {
            transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
        }
    }

    internal sealed class NidusPulseVisual : MonoBehaviour
    {
        private LineRenderer ring;
        private float radius;
        private float duration;
        private float elapsed;
        private Color color;

        public void Initialize(float radius, float duration, Material material, Color color)
        {
            this.radius = radius;
            this.duration = duration;
            this.color = color;

            ring = gameObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.sharedMaterial = material;
            ring.loop = true;
            ring.positionCount = 40;
            ring.startWidth = Mathf.Max(0.025f, radius * 0.035f);
            ring.endWidth = ring.startWidth;
            ring.sortingLayerName = NidusOrganicSkillVFX.SortingLayerName;
            ring.sortingOrder = 100;

            for (int i = 0; i < ring.positionCount; i++)
            {
                float a = Mathf.PI * 2f * i / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.22f, 0f));
            }

            Destroy(gameObject, duration + 0.05f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float scale = Mathf.Lerp(radius * 0.45f, radius * 1.25f, 1f - Mathf.Pow(1f - t, 3f));
            transform.localScale = Vector3.one * scale;
            Color c = color;
            c.a *= (1f - Mathf.SmoothStep(0f, 1f, t)) * 0.75f;
            ring.startColor = c;
            ring.endColor = c;
        }
    }

    internal sealed class NidusCoreAbsorbVisual : MonoBehaviour
    {
        private readonly List<ParticleSystem> systems = new List<ParticleSystem>();
        private readonly List<float> baseEmissionRates = new List<float>();
        private readonly List<LineRenderer> rings = new List<LineRenderer>();
        private readonly List<float> ringPhases = new List<float>();

        private float duration;
        private float elapsed;
        private float radius;
        private Color coreColor;
        private Color midColor;
        private Color rimColor;

        public void Initialize(float radius, float duration, Material particleMaterial, Material ringMaterial,
            Color coreColor, Color midColor, Color rimColor)
        {
            this.radius = radius;
            this.duration = duration;
            this.coreColor = coreColor;
            this.midColor = midColor;
            this.rimColor = rimColor;

            BuildImplodingSpores(particleMaterial);
            BuildCoreGlow(particleMaterial);
            BuildRisingEmbers(particleMaterial);
            BuildBreathingRings(ringMaterial);

            Destroy(gameObject, duration + 1.1f);
        }

        // Spores sucked inward: high count, spiralling via orbital velocity, colour shifting
        // from rim yellow to core teal as they reach the middle.
        private void BuildImplodingSpores(Material material)
        {
            ParticleSystem ps = CreateSystem("ImplodingSpores", material, 122);
            var main = ps.main;
            main.duration = duration;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.42f, 0.72f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.05f, radius * 0.13f);
            main.startColor = new ParticleSystem.MinMaxGradient(midColor, rimColor);
            main.maxParticles = 260;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 130f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Donut;
            shape.radius = radius * 1.05f;
            shape.donutRadius = radius * 0.28f;
            shape.radiusThickness = 1f;
            shape.arc = 360f;
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;

            // Pull toward the core plus a tangential swirl -> spiral implosion.
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            // Orbital X/Y/Z have to share one curve mode in Unity's particle module.
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(0f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(2.25f);
            velocity.radial = new ParticleSystem.MinMaxCurve(-radius * 2.4f, -radius * 1.3f);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.25f),
                new Keyframe(0.35f, 1f),
                new Keyframe(1f, 0.05f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(rimColor, 0f),
                    new GradientColorKey(midColor, 0.45f),
                    new GradientColorKey(coreColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.95f, 0.22f),
                    new GradientAlphaKey(0.85f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = radius * 0.35f;
            noise.frequency = 1.6f;
            noise.scrollSpeed = 0.7f;
            noise.damping = true;

            var trails = ps.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.PerParticle;
            trails.ratio = 0.45f;
            trails.lifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            trails.dieWithParticles = true;
            trails.sizeAffectsWidth = true;
            trails.inheritParticleColor = true;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(0.55f);

            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.trailMaterial = material;

            ps.Play();
        }

        // Dense bright heart of the effect: small, fast pulsing particles hugging the centre.
        private void BuildCoreGlow(Material material)
        {
            ParticleSystem ps = CreateSystem("CoreGlow", material, 124);
            var main = ps.main;
            main.duration = duration;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.1f, radius * 0.32f);
            main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.16f, radius * 0.34f);
            main.startColor = new ParticleSystem.MinMaxGradient(coreColor, midColor);
            main.maxParticles = 90;
            main.gravityModifier = -0.02f;

            var emission = ps.emission;
            emission.rateOverTime = 48f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius * 0.22f;
            shape.radiusThickness = 0.4f;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.45f),
                new Keyframe(0.4f, 1f),
                new Keyframe(1f, 0.2f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(coreColor, 0.35f),
                    new GradientColorKey(midColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.7f, 0.25f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            ps.Play();
        }

        // Slow, sparse motes lifting off the shell: sells the "organic, breathing" feel.
        private void BuildRisingEmbers(Material material)
        {
            ParticleSystem ps = CreateSystem("SporeEmbers", material, 121);
            var main = ps.main;
            main.duration = duration;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.15f, radius * 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.03f, radius * 0.07f);
            main.startColor = new ParticleSystem.MinMaxGradient(midColor, coreColor);
            main.maxParticles = 70;
            main.gravityModifier = -0.12f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 26f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * 0.75f;
            shape.radiusThickness = 1f;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = radius * 0.5f;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.35f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(midColor, 0f), new GradientColorKey(coreColor, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.55f, 0.3f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            ps.Play();
        }

        // Two flattened rings breathing in/out of phase, echoing the pulse language
        // already used by root telegraphs so the whole kit feels consistent.
        private void BuildBreathingRings(Material material)
        {
            for (int i = 0; i < 2; i++)
            {
                GameObject go = new GameObject(i == 0 ? "CoreRingInner" : "CoreRingOuter");
                go.transform.SetParent(transform, false);

                LineRenderer ring = go.AddComponent<LineRenderer>();
                ring.useWorldSpace = false;
                ring.sharedMaterial = material;
                ring.loop = true;
                ring.positionCount = 48;
                ring.startWidth = Mathf.Max(0.02f, radius * (i == 0 ? 0.045f : 0.028f));
                ring.endWidth = ring.startWidth;
                ring.sortingLayerName = NidusOrganicSkillVFX.SortingLayerName;
                ring.sortingOrder = 119 + i;

                for (int p = 0; p < ring.positionCount; p++)
                {
                    float a = Mathf.PI * 2f * p / ring.positionCount;
                    ring.SetPosition(p, new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.35f, 0f));
                }

                rings.Add(ring);
                ringPhases.Add(i * Mathf.PI * 0.6f);
            }
        }

        private ParticleSystem CreateSystem(string name, Material material, int sortingOrder)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = material;
            renderer.sortingLayerName = NidusOrganicSkillVFX.SortingLayerName;
            renderer.sortingOrder = sortingOrder;

            systems.Add(ps);
            baseEmissionRates.Add(ps.emission.rateOverTime.constant);
            return ps;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));

            // Ramp in fast, hold, then fade out so it never pops off screen.
            float envelope = Mathf.Min(
                Mathf.InverseLerp(0f, 0.18f, t),
                1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, t)));

            float breathe = 0.86f + Mathf.Sin(elapsed * 7.5f) * 0.14f;

            for (int i = 0; i < rings.Count; i++)
            {
                LineRenderer ring = rings[i];
                if (ring == null) continue;

                float phase = ringPhases[i];
                float wobble = 0.82f + Mathf.Sin(elapsed * 5.2f + phase) * 0.18f;
                float scale = radius * (i == 0 ? 0.52f : 0.86f) * wobble;
                ring.transform.localScale = new Vector3(scale, scale, 1f);
                ring.transform.localRotation = Quaternion.Euler(0f, 0f, elapsed * (i == 0 ? 38f : -24f));

                Color c = Color.Lerp(coreColor, i == 0 ? midColor : rimColor, 0.35f);
                c.a = envelope * breathe * (i == 0 ? 0.65f : 0.4f);
                ring.startColor = c;
                ring.endColor = c;
            }

            // Fade emission with the envelope instead of hard-stopping the systems,
            // so already-spawned spores still finish their journey into the core.
            for (int i = 0; i < systems.Count; i++)
            {
                ParticleSystem ps = systems[i];
                if (ps == null) continue;

                var emission = ps.emission;
                emission.rateOverTimeMultiplier = baseEmissionRates[i] * envelope;

                if (t >= 0.99f && ps.isEmitting)
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    internal sealed class NidusSporeStreamVisual : MonoBehaviour
    {
        private Vector3 target;
        private float duration;
        private float elapsed;
        private ParticleSystem ps;

        public void Initialize(Vector3 target, float duration, Material material, Color color)
        {
            this.target = target;
            this.duration = duration;

            ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = duration;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
            main.startColor = color;
            main.maxParticles = 80;
            var emission = ps.emission;
            emission.rateOverTime = 36f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.35f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.24f;
            ParticleSystemRenderer renderer = GetComponent<ParticleSystemRenderer>();
            renderer.material = material;
            renderer.sortingLayerName = NidusOrganicSkillVFX.SortingLayerName;
            renderer.sortingOrder = 118;
            ps.Play();
            Destroy(gameObject, duration + 0.65f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * Mathf.Lerp(2f, 7f, t));
        }
    }
}
