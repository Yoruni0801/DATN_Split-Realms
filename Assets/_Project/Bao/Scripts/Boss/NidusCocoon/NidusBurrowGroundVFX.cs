using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bao.Bosses
{
    public enum NidusBurrowVFXMode
    {
        Burrow,
        Trail,
        Warning,
        Emerge
    }

    [DisallowMultipleComponent]
    public sealed class NidusBurrowGroundVFX : MonoBehaviour
    {
        private static readonly Color SoilDark = new Color(0.16f, 0.055f, 0.018f, 1f);
        private static readonly Color SoilWarm = new Color(0.52f, 0.18f, 0.035f, 1f);
        private static readonly Color RootBrown = new Color(0.24f, 0.07f, 0.025f, 1f);
        private static readonly Color AcidGreen = new Color(0.15f, 1.25f, 0.018f, 1f);
        private static readonly Color AcidYellow = new Color(0.72f, 1.55f, 0.06f, 1f);
        private const int SortingBaseOrder = 80;

        private readonly List<Object> generatedResources = new List<Object>();
        private readonly List<ParticleSystem> particleSystems = new List<ParticleSystem>();
        private readonly List<LineRenderer> crackBases = new List<LineRenderer>();
        private readonly List<LineRenderer> crackGlows = new List<LineRenderer>();

        private NidusBurrowVFXMode mode;
        private float radius;
        private float duration;
        private string vfxSortingLayerName;

        private Texture2D softTexture;
        private Texture2D ringTexture;
        private Texture2D shardTexture;
        private Sprite softSprite;
        private Sprite ringSprite;

        private Material soilSoftMaterial;
        private Material soilRingMaterial;
        private Material rootMaterial;
        private Material acidSoftMaterial;
        private Material acidRingMaterial;
        private Material acidBrightMaterial;
        private Material shardMaterial;

        private SpriteRenderer crater;
        private SpriteRenderer soilRing;
        private SpriteRenderer toxicRing;
        private SpriteRenderer pulseRing;

        public float TotalDuration => duration;

        public static NidusBurrowGroundVFX Spawn(
            Vector3 groundPosition,
            NidusBurrowVFXMode effectMode,
            float effectRadius,
            float durationOverride = -1f)
        {
            GameObject root = new GameObject($"Nidus_{effectMode}_GroundVFX");
            root.transform.position = groundPosition;
            NidusBurrowGroundVFX effect = root.AddComponent<NidusBurrowGroundVFX>();
            effect.Initialize(effectMode, effectRadius, durationOverride);
            return effect;
        }

        public void Initialize(
            NidusBurrowVFXMode effectMode,
            float effectRadius,
            float durationOverride = -1f)
        {
            mode = effectMode;
            radius = Mathf.Max(0.35f, effectRadius);
            duration = durationOverride > 0f ? durationOverride : GetDefaultDuration(effectMode);
            vfxSortingLayerName = GetTopSortingLayerName();
            ConfigureRootSorting();

            CreateResources();
            BuildGroundLayers();
            BuildCracks();
            BuildParticles();
            StartParticles();
            StartCoroutine(Animate());
        }

        public void SetAccentColor(Color accentColor)
        {
            SetMaterialTint(acidSoftMaterial, accentColor);
            SetMaterialTint(acidRingMaterial, accentColor);

            Color brightColor = Color.Lerp(
                accentColor,
                new Color(1.25f, 1.25f, 0.18f, accentColor.a),
                0.32f);
            SetMaterialTint(acidBrightMaterial, brightColor);
        }

        private static float GetDefaultDuration(NidusBurrowVFXMode effectMode)
        {
            switch (effectMode)
            {
                case NidusBurrowVFXMode.Burrow:
                    return 1.35f;
                case NidusBurrowVFXMode.Trail:
                    return 0.72f;
                case NidusBurrowVFXMode.Warning:
                    return 1.2f;
                default:
                    return 1.65f;
            }
        }

        private void CreateResources()
        {
            softTexture = CreateSoftDiscTexture(64);
            ringTexture = CreateOrganicRingTexture(128);
            shardTexture = CreateSoilShardTexture(64);
            softSprite = CreateSprite(softTexture, "NidusBurrow_SoftSprite");
            ringSprite = CreateSprite(ringTexture, "NidusBurrow_RingSprite");

            Shader shader = Shader.Find("Bao/NidusPhaseTransitionVFX");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            soilSoftMaterial = CreateMaterial(shader, softTexture, SoilDark, false, "NidusBurrow_SoilSoft");
            soilRingMaterial = CreateMaterial(shader, ringTexture, SoilWarm, false, "NidusBurrow_SoilRing");
            rootMaterial = CreateMaterial(shader, softTexture, RootBrown, false, "NidusBurrow_Root");
            acidSoftMaterial = CreateMaterial(shader, softTexture, AcidGreen, true, "NidusBurrow_AcidSoft");
            acidRingMaterial = CreateMaterial(shader, ringTexture, AcidGreen, true, "NidusBurrow_AcidRing");
            acidBrightMaterial = CreateMaterial(shader, softTexture, AcidYellow, true, "NidusBurrow_AcidBright");
            shardMaterial = CreateMaterial(shader, shardTexture, SoilWarm, false, "NidusBurrow_Shards");
        }

        private void BuildGroundLayers()
        {
            crater = CreateLayer("Crater", softSprite, soilSoftMaterial, 0);
            soilRing = CreateLayer("SoilRing", ringSprite, soilRingMaterial, 2);
            toxicRing = CreateLayer("ToxicRootRing", ringSprite, acidRingMaterial, 4);
            pulseRing = CreateLayer("PressurePulse", ringSprite, acidRingMaterial, 6);
        }

        private void BuildCracks()
        {
            int crackCount = mode == NidusBurrowVFXMode.Trail ? 4 : 8;
            if (mode == NidusBurrowVFXMode.Emerge)
                crackCount = 10;

            for (int i = 0; i < crackCount; i++)
            {
                float angle = (Mathf.PI * 2f * i / crackCount) + Mathf.Sin(i * 2.71f) * 0.18f;
                float length = radius * Mathf.Lerp(0.68f, 1.08f, Hash01(i * 17.31f));
                Vector3[] points = BuildCrackPoints(angle, length, i);

                LineRenderer baseLine = CreateLine(
                    $"Crack_{i:00}_Soil",
                    rootMaterial,
                    5,
                    Mathf.Max(0.035f, radius * 0.028f),
                    Mathf.Max(0.012f, radius * 0.009f));
                LineRenderer glowLine = CreateLine(
                    $"Crack_{i:00}_Glow",
                    acidSoftMaterial,
                    7,
                    Mathf.Max(0.016f, radius * 0.010f),
                    0f);

                baseLine.positionCount = points.Length;
                glowLine.positionCount = points.Length;
                baseLine.SetPositions(points);
                glowLine.SetPositions(points);
                crackBases.Add(baseLine);
                crackGlows.Add(glowLine);
            }
        }

        private Vector3[] BuildCrackPoints(float angle, float length, int index)
        {
            Vector3[] points = new Vector3[5];
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.18f);
            Vector2 tangent = new Vector2(-direction.y, direction.x).normalized;

            for (int p = 0; p < points.Length; p++)
            {
                float t = p / (points.Length - 1f);
                float wobble = Mathf.Sin(index * 3.17f + p * 4.31f) * length * 0.055f * t;
                Vector2 point = direction * (length * Mathf.Lerp(0.10f, 1f, t)) + tangent * wobble;
                points[p] = new Vector3(point.x, point.y, 0f);
            }

            return points;
        }

        private void BuildParticles()
        {
            BuildSoilClods();
            BuildDust();
            BuildToxicSpores();

            if (mode != NidusBurrowVFXMode.Warning)
                BuildRootSplinters();
        }

        private void BuildSoilClods()
        {
            if (mode == NidusBurrowVFXMode.Warning)
                return;

            ParticleSystem ps = CreateParticleSystem("SoilClods", shardMaterial, 16);
            particleSystems.Add(ps);

            var main = ps.main;
            main.duration = Mathf.Max(0.15f, duration * 0.42f);
            main.startLifetime = mode == NidusBurrowVFXMode.Emerge
                ? new ParticleSystem.MinMaxCurve(0.72f, 1.35f)
                : new ParticleSystem.MinMaxCurve(0.38f, 0.86f);
            main.startSpeed = mode == NidusBurrowVFXMode.Burrow
                ? new ParticleSystem.MinMaxCurve(-radius * 1.3f, -radius * 0.55f)
                : mode == NidusBurrowVFXMode.Emerge
                    ? new ParticleSystem.MinMaxCurve(radius * 1.15f, radius * 2.35f)
                    : new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            main.startSize = mode == NidusBurrowVFXMode.Emerge
                ? new ParticleSystem.MinMaxCurve(0.16f, 0.48f)
                : new ParticleSystem.MinMaxCurve(0.10f, 0.30f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = mode == NidusBurrowVFXMode.Emerge ? 2.35f : 1.25f;
            main.startColor = Color.white;
            main.maxParticles = 90;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            int minimum = mode == NidusBurrowVFXMode.Emerge ? 30 : mode == NidusBurrowVFXMode.Burrow ? 18 : 5;
            int maximum = mode == NidusBurrowVFXMode.Emerge ? 42 : mode == NidusBurrowVFXMode.Burrow ? 25 : 8;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)minimum, (short)maximum)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * (mode == NidusBurrowVFXMode.Trail ? 0.42f : 0.72f);
            shape.radiusThickness = 0.42f;
            shape.scale = new Vector3(1f, 0.18f, 1f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = mode == NidusBurrowVFXMode.Emerge
                ? new ParticleSystem.MinMaxCurve(-0.18f, 0.18f)
                : new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            velocity.y = mode == NidusBurrowVFXMode.Emerge
                ? new ParticleSystem.MinMaxCurve(2.6f, 5.8f)
                : new ParticleSystem.MinMaxCurve(-1.1f, 0.35f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-5f, 5f);

            ConfigureFadeAndShrink(ps, 0.02f, 0.30f, 0.76f);
        }

        private void BuildDust()
        {
            ParticleSystem ps = CreateParticleSystem("SoilDust", soilSoftMaterial, 12);
            particleSystems.Add(ps);

            var main = ps.main;
            main.duration = Mathf.Max(0.18f, duration * 0.75f);
            main.startLifetime = mode == NidusBurrowVFXMode.Warning
                ? new ParticleSystem.MinMaxCurve(0.45f, 0.85f)
                : new ParticleSystem.MinMaxCurve(0.68f, 1.35f);
            main.startSpeed = mode == NidusBurrowVFXMode.Emerge
                ? new ParticleSystem.MinMaxCurve(1.4f, 3.8f)
                : new ParticleSystem.MinMaxCurve(0.25f, 1.25f);
            main.startSize = mode == NidusBurrowVFXMode.Trail
                ? new ParticleSystem.MinMaxCurve(0.28f, 0.72f)
                : new ParticleSystem.MinMaxCurve(0.52f, 1.45f);
            main.startColor = new Color(1f, 1f, 1f, mode == NidusBurrowVFXMode.Warning ? 0.32f : 0.58f);
            main.gravityModifier = -0.06f;
            main.maxParticles = 100;

            var emission = ps.emission;
            if (mode == NidusBurrowVFXMode.Warning)
            {
                emission.rateOverTime = 9f;
            }
            else
            {
                emission.rateOverTime = 0f;
                int minimum = mode == NidusBurrowVFXMode.Emerge ? 24 : mode == NidusBurrowVFXMode.Burrow ? 16 : 4;
                int maximum = mode == NidusBurrowVFXMode.Emerge ? 34 : mode == NidusBurrowVFXMode.Burrow ? 22 : 7;
                emission.SetBursts(new[]
                {
                    new ParticleSystem.Burst(0f, (short)minimum, (short)maximum)
                });
            }

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * 0.72f;
            shape.radiusThickness = 0.72f;
            shape.scale = new Vector3(1f, 0.13f, 1f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);
            velocity.y = mode == NidusBurrowVFXMode.Emerge
                ? new ParticleSystem.MinMaxCurve(0.55f, 1.8f)
                : new ParticleSystem.MinMaxCurve(0.10f, 0.65f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.34f;
            noise.frequency = 0.75f;
            noise.scrollSpeed = 0.45f;

            ConfigureDustFade(ps);
        }

        private void BuildToxicSpores()
        {
            ParticleSystem ps = CreateParticleSystem("ToxicSpores", acidBrightMaterial, 20);
            particleSystems.Add(ps);

            var main = ps.main;
            main.duration = Mathf.Max(0.18f, duration * 0.82f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1.15f);
            main.startSpeed = mode == NidusBurrowVFXMode.Burrow
                ? new ParticleSystem.MinMaxCurve(-radius * 0.75f, -0.35f)
                : mode == NidusBurrowVFXMode.Emerge
                    ? new ParticleSystem.MinMaxCurve(1.3f, 4.2f)
                    : new ParticleSystem.MinMaxCurve(0.15f, 0.75f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.14f);
            main.startColor = Color.white;
            main.gravityModifier = -0.08f;
            main.maxParticles = 130;

            var emission = ps.emission;
            if (mode == NidusBurrowVFXMode.Warning)
            {
                emission.rateOverTime = 16f;
            }
            else
            {
                emission.rateOverTime = 0f;
                int minimum = mode == NidusBurrowVFXMode.Emerge ? 28 : mode == NidusBurrowVFXMode.Burrow ? 20 : 5;
                int maximum = mode == NidusBurrowVFXMode.Emerge ? 42 : mode == NidusBurrowVFXMode.Burrow ? 30 : 9;
                emission.SetBursts(new[]
                {
                    new ParticleSystem.Burst(0f, (short)minimum, (short)maximum)
                });
            }

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * 0.72f;
            shape.radiusThickness = 0.65f;
            shape.scale = new Vector3(1f, 0.16f, 1f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.10f, 0.10f);
            velocity.y = mode == NidusBurrowVFXMode.Emerge
                ? new ParticleSystem.MinMaxCurve(0.45f, 1.9f)
                : new ParticleSystem.MinMaxCurve(0.08f, 0.55f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            // Use the same Constant mode on every orbital axis to satisfy Unity's
            // ParticleSystem validation while this module is assembled at runtime.
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(0f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.28f;
            noise.frequency = 1.15f;
            noise.scrollSpeed = 0.9f;

            ConfigureFadeAndShrink(ps, 0.05f, 0.20f, 0.74f);
        }

        private void BuildRootSplinters()
        {
            ParticleSystem ps = CreateParticleSystem("RootSplinters", shardMaterial, 18);
            particleSystems.Add(ps);

            var main = ps.main;
            main.duration = Mathf.Max(0.12f, duration * 0.30f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.42f, 0.92f);
            main.startSpeed = mode == NidusBurrowVFXMode.Emerge
                ? new ParticleSystem.MinMaxCurve(2.8f, 6.8f)
                : new ParticleSystem.MinMaxCurve(-2.8f, -0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.24f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.72f, 0.42f, 0.20f, 0.92f);
            main.gravityModifier = mode == NidusBurrowVFXMode.Emerge ? 1.75f : 0.85f;
            main.maxParticles = 55;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            int minimum = mode == NidusBurrowVFXMode.Emerge ? 14 : mode == NidusBurrowVFXMode.Burrow ? 9 : 3;
            int maximum = mode == NidusBurrowVFXMode.Emerge ? 22 : mode == NidusBurrowVFXMode.Burrow ? 14 : 5;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)minimum, (short)maximum)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * 0.58f;
            shape.radiusThickness = 0.55f;
            shape.scale = new Vector3(1f, 0.14f, 1f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.16f, 0.16f);
            velocity.y = mode == NidusBurrowVFXMode.Emerge
                ? new ParticleSystem.MinMaxCurve(1.8f, 4.6f)
                : new ParticleSystem.MinMaxCurve(-0.8f, 0.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-7f, 7f);

            ConfigureFadeAndShrink(ps, 0.02f, 0.32f, 0.76f);
        }

        private IEnumerator Animate()
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));

                switch (mode)
                {
                    case NidusBurrowVFXMode.Burrow:
                        UpdateBurrow(t, elapsed);
                        break;
                    case NidusBurrowVFXMode.Trail:
                        UpdateTrail(t);
                        break;
                    case NidusBurrowVFXMode.Warning:
                        UpdateWarning(t, elapsed);
                        break;
                    case NidusBurrowVFXMode.Emerge:
                        UpdateEmerge(t);
                        break;
                }

                yield return null;
            }

            Destroy(gameObject);
        }

        private void UpdateBurrow(float t, float elapsed)
        {
            float eased = Smooth01(t);
            float fade = 1f - Smooth01(Mathf.InverseLerp(0.58f, 1f, t));
            float contraction = Mathf.Lerp(1f, 0.28f, eased);
            float pulse = 0.82f + Mathf.Pow(0.5f + 0.5f * Mathf.Sin(elapsed * 18f), 5f) * 0.18f;

            SetLayer(crater,
                new Vector3(radius * Mathf.Lerp(1.25f, 1.95f, eased), radius * Mathf.Lerp(0.20f, 0.34f, eased), 1f),
                Mathf.Lerp(0.18f, 0.48f, eased) * fade);
            SetLayer(soilRing,
                new Vector3(radius * 2.05f * contraction, radius * 0.40f * contraction, 1f),
                0.58f * fade);
            SetLayer(toxicRing,
                new Vector3(radius * 1.76f * contraction * pulse, radius * 0.31f * contraction * pulse, 1f),
                (0.24f + eased * 0.32f) * fade);

            float ripple = Mathf.Repeat(t * 2.2f, 1f);
            SetLayer(pulseRing,
                new Vector3(radius * Mathf.Lerp(1.55f, 0.34f, ripple), radius * Mathf.Lerp(0.27f, 0.07f, ripple), 1f),
                (1f - ripple) * 0.30f * fade);
            SetCrackAlpha(Mathf.Lerp(0.82f, 0f, eased), Mathf.Lerp(0.45f, 0f, eased));
        }

        private void UpdateTrail(float t)
        {
            float fade = 1f - Smooth01(t);
            float expansion = EaseOut(t);

            SetLayer(crater,
                new Vector3(radius * Mathf.Lerp(0.72f, 1.55f, expansion), radius * Mathf.Lerp(0.12f, 0.24f, expansion), 1f),
                0.30f * fade);
            SetLayer(soilRing,
                new Vector3(radius * Mathf.Lerp(0.65f, 1.72f, expansion), radius * Mathf.Lerp(0.11f, 0.29f, expansion), 1f),
                0.42f * fade);
            SetLayer(toxicRing,
                new Vector3(radius * Mathf.Lerp(0.42f, 1.38f, expansion), radius * Mathf.Lerp(0.08f, 0.22f, expansion), 1f),
                0.34f * fade);
            HideLayer(pulseRing);
            SetCrackAlpha(0.34f * fade, 0.18f * fade);
        }

        private void UpdateWarning(float t, float elapsed)
        {
            float appear = Smooth01(Mathf.Clamp01(t / 0.16f));
            float urgency = t * t;
            float breathing = 0.94f + Mathf.Sin(elapsed * Mathf.Lerp(7f, 18f, urgency)) * 0.06f;

            SetLayer(crater,
                new Vector3(radius * 1.82f, radius * 0.38f, 1f),
                appear * Mathf.Lerp(0.24f, 0.42f, urgency));
            SetLayer(soilRing,
                new Vector3(radius * 2.04f * breathing, radius * 0.43f * breathing, 1f),
                appear * 0.64f);
            SetLayer(toxicRing,
                new Vector3(radius * 1.76f * breathing, radius * 0.35f * breathing, 1f),
                appear * Mathf.Lerp(0.34f, 0.72f, urgency));

            float ripple = Mathf.Repeat(t * Mathf.Lerp(2.2f, 5.2f, urgency), 1f);
            SetLayer(pulseRing,
                new Vector3(radius * Mathf.Lerp(1.72f, 0.34f, ripple), radius * Mathf.Lerp(0.34f, 0.07f, ripple), 1f),
                appear * (1f - ripple) * Mathf.Lerp(0.28f, 0.66f, urgency));
            SetCrackAlpha(appear * Mathf.Lerp(0.35f, 0.88f, urgency), appear * Mathf.Lerp(0.16f, 0.68f, urgency));
        }

        private void UpdateEmerge(float t)
        {
            float expansion = EaseOut(Mathf.Clamp01(t / 0.62f));
            float fade = 1f - Smooth01(Mathf.InverseLerp(0.34f, 1f, t));

            SetLayer(crater,
                new Vector3(radius * Mathf.Lerp(0.75f, 2.15f, expansion), radius * Mathf.Lerp(0.14f, 0.42f, expansion), 1f),
                Mathf.Lerp(0.50f, 0f, Smooth01(t)));
            SetLayer(soilRing,
                new Vector3(radius * Mathf.Lerp(0.46f, 2.48f, expansion), radius * Mathf.Lerp(0.10f, 0.52f, expansion), 1f),
                0.72f * fade);
            SetLayer(toxicRing,
                new Vector3(radius * Mathf.Lerp(0.32f, 2.74f, expansion), radius * Mathf.Lerp(0.07f, 0.54f, expansion), 1f),
                0.58f * fade);

            float delayedT = Mathf.Clamp01((t - 0.08f) / 0.72f);
            SetLayer(pulseRing,
                new Vector3(radius * Mathf.Lerp(0.24f, 2.15f, EaseOut(delayedT)), radius * Mathf.Lerp(0.05f, 0.42f, EaseOut(delayedT)), 1f),
                (1f - Smooth01(delayedT)) * 0.48f);
            SetCrackAlpha(0.82f * fade, 0.72f * fade);
        }

        private void ConfigureRootSorting()
        {
            SortingGroup sortingGroup = gameObject.GetComponent<SortingGroup>();
            if (sortingGroup == null)
                sortingGroup = gameObject.AddComponent<SortingGroup>();

            sortingGroup.sortingLayerName = vfxSortingLayerName;
            sortingGroup.sortingOrder = SortingBaseOrder;
        }

        private ParticleSystem CreateParticleSystem(string objectName, Material material, int sortingOrder)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.material = material;
            renderer.sortingLayerName = vfxSortingLayerName;
            renderer.sortingOrder = SortingBaseOrder + sortingOrder;
            renderer.maskInteraction = SpriteMaskInteraction.None;
            return ps;
        }

        private SpriteRenderer CreateLayer(string objectName, Sprite sprite, Material material, int sortingOrder)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingLayerName = vfxSortingLayerName;
            renderer.sortingOrder = SortingBaseOrder + sortingOrder;
            renderer.color = new Color(1f, 1f, 1f, 0f);
            return renderer;
        }

        private LineRenderer CreateLine(
            string objectName,
            Material material,
            int sortingOrder,
            float startWidth,
            float endWidth)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sharedMaterial = material;
            line.sortingLayerName = vfxSortingLayerName;
            line.sortingOrder = SortingBaseOrder + sortingOrder;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.startWidth = startWidth;
            line.endWidth = endWidth;
            line.startColor = new Color(1f, 1f, 1f, 0f);
            line.endColor = new Color(1f, 1f, 1f, 0f);
            return line;
        }

        private static string GetTopSortingLayerName()
        {
            SortingLayer[] layers = SortingLayer.layers;
            if (layers == null || layers.Length == 0)
                return "Default";

            SortingLayer topLayer = layers[0];
            for (int i = 1; i < layers.Length; i++)
            {
                if (layers[i].value > topLayer.value)
                    topLayer = layers[i];
            }

            return topLayer.name;
        }

        private void StartParticles()
        {
            for (int i = 0; i < particleSystems.Count; i++)
            {
                if (particleSystems[i] != null)
                    particleSystems[i].Play();
            }
        }

        private static void ConfigureFadeAndShrink(
            ParticleSystem ps,
            float fadeInEnd,
            float holdEnd,
            float shrinkStart)
        {
            var color = ps.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, Mathf.Max(0.01f, fadeInEnd)),
                    new GradientAlphaKey(1f, Mathf.Max(fadeInEnd, holdEnd)),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.12f, 1f),
                new Keyframe(Mathf.Clamp01(shrinkStart), 0.82f),
                new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static void ConfigureDustFade(ParticleSystem ps)
        {
            var color = ps.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.82f, 0.72f, 0.58f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.72f, 0.16f),
                    new GradientAlphaKey(0.48f, 0.52f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.28f),
                    new Keyframe(0.28f, 1f),
                    new Keyframe(1f, 1.45f)));
        }

        private void SetLayer(SpriteRenderer renderer, Vector3 scale, float alpha)
        {
            if (renderer == null)
                return;

            renderer.transform.localScale = scale;
            renderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        }

        private static void HideLayer(SpriteRenderer renderer)
        {
            if (renderer != null)
                renderer.color = new Color(1f, 1f, 1f, 0f);
        }

        private void SetCrackAlpha(float baseAlpha, float glowAlpha)
        {
            Color baseColor = new Color(1f, 1f, 1f, Mathf.Clamp01(baseAlpha));
            Color glowColor = new Color(1f, 1f, 1f, Mathf.Clamp01(glowAlpha));

            for (int i = 0; i < crackBases.Count; i++)
            {
                if (crackBases[i] != null)
                {
                    crackBases[i].startColor = baseColor;
                    crackBases[i].endColor = new Color(1f, 1f, 1f, baseColor.a * 0.12f);
                }

                if (crackGlows[i] != null)
                {
                    crackGlows[i].startColor = glowColor;
                    crackGlows[i].endColor = new Color(1f, 1f, 1f, glowColor.a * 0.04f);
                }
            }
        }

        private Material CreateMaterial(
            Shader shader,
            Texture texture,
            Color tint,
            bool additive,
            string materialName)
        {
            Material material = new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.DontSave
            };

            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_TintColor"))
                material.SetColor("_TintColor", tint);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);

            generatedResources.Add(material);
            return material;
        }

        private static void SetMaterialTint(Material material, Color tint)
        {
            if (material != null && material.HasProperty("_TintColor"))
                material.SetColor("_TintColor", tint);
        }

        private Texture2D CreateSoftDiscTexture(int size)
        {
            Texture2D texture = CreateTexture(size, "NidusBurrow_SoftDisc");
            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxRadius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float radial = Vector2.Distance(new Vector2(x, y), center) / maxRadius;
                    float alpha = 1f - Smooth01(Mathf.InverseLerp(0.12f, 1f, radial));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha * alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private Texture2D CreateOrganicRingTexture(int size)
        {
            Texture2D texture = CreateTexture(size, "NidusBurrow_OrganicRing");
            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxRadius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 delta = new Vector2(x, y) - center;
                    float radial = delta.magnitude / maxRadius;
                    float angle = Mathf.Atan2(delta.y, delta.x);
                    float warp = Mathf.Sin(angle * 7f + 0.6f) * 0.026f
                        + Mathf.Sin(angle * 13f - 1.4f) * 0.014f
                        + Mathf.Sin(angle * 3f + 2.2f) * 0.010f;
                    float ringDistance = Mathf.Abs(radial - (0.76f + warp));
                    float alpha = 1f - Smooth01(Mathf.InverseLerp(0.004f, 0.040f, ringDistance));
                    alpha *= 1f - Smooth01(Mathf.InverseLerp(0.94f, 1f, radial));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private Texture2D CreateSoilShardTexture(int size)
        {
            Texture2D texture = CreateTexture(size, "NidusBurrow_SoilShard");
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                float v = y / (size - 1f);
                for (int x = 0; x < size; x++)
                {
                    float u = x / (size - 1f);
                    float centeredX = Mathf.Abs(u - 0.5f);
                    float width = Mathf.Lerp(0.08f, 0.44f, Mathf.Sin(v * Mathf.PI));
                    float edge = width - centeredX;
                    float alpha = Smooth01(Mathf.InverseLerp(0f, 0.035f, edge));
                    float notch = Mathf.Sin((u * 5.3f + v * 7.1f) * Mathf.PI) * 0.04f;
                    alpha *= v > 0.04f + notch && v < 0.96f - notch ? 1f : 0f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private Texture2D CreateTexture(int size, string textureName)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = textureName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            generatedResources.Add(texture);
            return texture;
        }

        private Sprite CreateSprite(Texture2D texture, string spriteName)
        {
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                texture.width);
            sprite.name = spriteName;
            sprite.hideFlags = HideFlags.DontSave;
            generatedResources.Add(sprite);
            return sprite;
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static float EaseOut(float value)
        {
            value = Mathf.Clamp01(value);
            return 1f - Mathf.Pow(1f - value, 3f);
        }

        private static float Hash01(float value)
        {
            return Mathf.Repeat(Mathf.Sin(value * 12.9898f) * 43758.5453f, 1f);
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            for (int i = 0; i < generatedResources.Count; i++)
            {
                if (generatedResources[i] != null)
                    Destroy(generatedResources[i]);
            }
            generatedResources.Clear();
        }
    }
}
