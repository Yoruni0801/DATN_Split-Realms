using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bao.Bosses
{
    /// <summary>
    /// Multi-stage phase transition for Nidus Cocoon.
    ///
    /// The effect is intentionally generated at runtime so it does not depend on
    /// placeholder particle textures. It is built around the boss silhouette:
    /// roots feed the cocoon, the core compresses, then the shell erupts into
    /// toxic light, spores and organic fragments.
    /// </summary>
    public class NidusPhaseTransitionVFX : MonoBehaviour
    {
        [Header("Timeline")]
        [SerializeField, Min(1f)] private float totalDuration = 2.35f;
        [SerializeField, Range(0.45f, 0.9f)] private float burstTimeNormalized = 0.70f;
        [SerializeField, Min(3f)] private float defaultEffectRadius = 4.2f;

        [Header("Palette")]
        [SerializeField] private Color acidGreen = new Color(0.18f, 1.15f, 0.018f, 1f);
        [SerializeField] private Color acidYellow = new Color(0.62f, 1.55f, 0.075f, 1f);
        [SerializeField] private Color rootBrown = new Color(0.16f, 0.045f, 0.012f, 0.82f);
        [SerializeField] private Color deepGreen = new Color(0.035f, 0.22f, 0.012f, 0.72f);

        private readonly List<Object> generatedAssets = new List<Object>();
        private readonly List<ParticleSystem> particleSystems = new List<ParticleSystem>();

        private NidusCocoonBoss owner;
        private bool initialized;
        private bool playing;
        private float burstTime;
        private float effectRadius;
        private float groundY;

        private Texture2D softTexture;
        private Texture2D ringTexture;
        private Texture2D shardTexture;
        private Sprite softSprite;
        private Sprite ringSprite;
        private Sprite shardSprite;

        private Material additiveSoftMaterial;
        private Material additiveBrightMaterial;
        private Material additiveRingMaterial;
        private Material additiveBrightRingMaterial;
        private Material additiveSporeMaterial;
        private Material additiveShellMaterial;
        private Material alphaMistMaterial;
        private Material rootMaterial;
        private Material rootGlowMaterial;
        private Material alphaRootShardMaterial;

        private SpriteRenderer coreGlow;
        private SpriteRenderer cocoonHalo;
        private SpriteRenderer impactFlash;
        private SpriteRenderer chargeRing;
        private SpriteRenderer pulseRingA;
        private SpriteRenderer pulseRingB;
        private SpriteRenderer pulseRingC;
        private SpriteRenderer shockwaveA;
        private SpriteRenderer shockwaveB;
        private SpriteRenderer groundShockwave;

        private LineRenderer[] rootLines;
        private LineRenderer[] rootGlowLines;
        private Vector3[][] rootPaths;

        /// <summary>
        /// Lifetime used by the boss when scheduling cleanup.
        /// </summary>
        public float TotalDuration => totalDuration;

        private void Awake()
        {
            CreateTexturesAndMaterials();
        }

        private void Start()
        {
            if (!initialized)
                Play();
        }

        /// <summary>
        /// Called immediately after instantiation by NidusCocoonBoss.
        /// </summary>
        public void Initialize(NidusCocoonBoss boss, float gameplayDuration)
        {
            owner = boss;
            initialized = true;

            float safeGameplayDuration = Mathf.Max(0.8f, gameplayDuration);
            burstTime = Mathf.Clamp(safeGameplayDuration * burstTimeNormalized, 0.72f, 1.18f);
            totalDuration = Mathf.Max(totalDuration, safeGameplayDuration + 0.75f);

            if (owner != null && owner.Collider2D != null)
            {
                Bounds bounds = owner.Collider2D.bounds;
                effectRadius = Mathf.Max(defaultEffectRadius, Mathf.Max(bounds.extents.x * 1.7f, bounds.extents.y * 1.15f));
                groundY = bounds.min.y - transform.position.y + 0.10f;
            }
            else
            {
                effectRadius = defaultEffectRadius;
                groundY = -2.8f;
            }

            Play();
        }

        private void Play()
        {
            if (playing)
                return;

            playing = true;
            if (burstTime <= 0f)
                burstTime = totalDuration * burstTimeNormalized;
            if (effectRadius <= 0f)
                effectRadius = defaultEffectRadius;

            BuildEffect();
            StartAllParticles();
            StartCoroutine(AnimateEffect());
        }

        private void CreateTexturesAndMaterials()
        {
            softTexture = CreateSoftDiscTexture(64, "NidusPhase_SoftDisc");
            ringTexture = CreateRingTexture(128, "NidusPhase_OrganicRing");
            shardTexture = CreateShardTexture(64, "NidusPhase_ShellShard");

            softSprite = CreateSprite(softTexture, "NidusPhase_SoftDiscSprite");
            ringSprite = CreateSprite(ringTexture, "NidusPhase_OrganicRingSprite");
            shardSprite = CreateSprite(shardTexture, "NidusPhase_ShellShardSprite");

            Shader shader = Shader.Find("Bao/NidusPhaseTransitionVFX");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            additiveSoftMaterial = CreateMaterial(shader, softTexture, acidGreen, true, "NidusPhase_AdditiveSoft");
            additiveBrightMaterial = CreateMaterial(shader, softTexture, acidYellow, true, "NidusPhase_AdditiveBright");
            additiveRingMaterial = CreateMaterial(shader, ringTexture, acidGreen, true, "NidusPhase_AdditiveRing");
            additiveBrightRingMaterial = CreateMaterial(shader, ringTexture, acidYellow, true, "NidusPhase_AdditiveBrightRing");
            additiveSporeMaterial = CreateMaterial(shader, shardTexture, acidGreen, true, "NidusPhase_AdditiveSpore");
            additiveShellMaterial = CreateMaterial(shader, shardTexture, acidYellow, true, "NidusPhase_AdditiveShell");
            alphaMistMaterial = CreateMaterial(shader, softTexture, deepGreen, false, "NidusPhase_AlphaMist");
            rootMaterial = CreateMaterial(shader, softTexture, rootBrown, false, "NidusPhase_Root");
            rootGlowMaterial = CreateMaterial(shader, softTexture, acidGreen, true, "NidusPhase_RootGlow");
            alphaRootShardMaterial = CreateMaterial(shader, shardTexture, rootBrown, false, "NidusPhase_RootShard");
        }

        private void BuildEffect()
        {
            BuildSpriteLayers();
            BuildRootTendrils();
            BuildParticleLayers();
        }

        private void BuildSpriteLayers()
        {
            coreGlow = CreateSpriteLayer("CoreGlow", softSprite, additiveBrightMaterial, 12);
            cocoonHalo = CreateSpriteLayer("CocoonHalo", softSprite, additiveSoftMaterial, 8);
            impactFlash = CreateSpriteLayer("ImpactFlash", softSprite, additiveBrightMaterial, 16);
            chargeRing = CreateSpriteLayer("ChargeRing", ringSprite, additiveRingMaterial, 7);
            pulseRingA = CreateSpriteLayer("ChargePulse_A", ringSprite, additiveRingMaterial, 9);
            pulseRingB = CreateSpriteLayer("ChargePulse_B", ringSprite, additiveRingMaterial, 9);
            pulseRingC = CreateSpriteLayer("ChargePulse_C", ringSprite, additiveRingMaterial, 9);
            shockwaveA = CreateSpriteLayer("Shockwave_A", ringSprite, additiveBrightRingMaterial, 15);
            shockwaveB = CreateSpriteLayer("Shockwave_B", ringSprite, additiveRingMaterial, 14);
            groundShockwave = CreateSpriteLayer("GroundShockwave", ringSprite, additiveBrightRingMaterial, 6);
            chargeRing.transform.localPosition = new Vector3(0f, groundY, 0f);
            groundShockwave.transform.localPosition = new Vector3(0f, groundY, 0f);

            HideSprite(coreGlow);
            HideSprite(cocoonHalo);
            HideSprite(impactFlash);
            HideSprite(chargeRing);
            HideSprite(pulseRingA);
            HideSprite(pulseRingB);
            HideSprite(pulseRingC);
            HideSprite(shockwaveA);
            HideSprite(shockwaveB);
            HideSprite(groundShockwave);
        }

        private void BuildRootTendrils()
        {
            const int rootCount = 8;
            rootLines = new LineRenderer[rootCount];
            rootGlowLines = new LineRenderer[rootCount];
            rootPaths = new Vector3[rootCount][];

            float rootRadius = effectRadius * 1.18f;
            for (int i = 0; i < rootCount; i++)
            {
                float normalized = i / (float)rootCount;
                float angle = Mathf.Lerp(-Mathf.PI * 0.92f, Mathf.PI * 0.92f, normalized);
                float side = Mathf.Sign(Mathf.Cos(angle));
                float outerX = Mathf.Cos(angle) * rootRadius;
                float outerY = groundY + Mathf.Sin(angle) * 0.24f;

                Vector3 p0 = new Vector3(outerX, outerY, 0f);
                Vector3 p1 = new Vector3(outerX * 0.78f, groundY + 0.10f + (i % 3) * 0.10f, 0f);
                Vector3 p2 = new Vector3(side * effectRadius * (0.34f + (i % 2) * 0.08f), -effectRadius * 0.22f, 0f);
                Vector3 p3 = new Vector3(Mathf.Sin(i * 2.7f) * 0.42f, -effectRadius * 0.04f + (i % 2) * 0.18f, 0f);

                Vector3[] path = new Vector3[18];
                for (int point = 0; point < path.Length; point++)
                {
                    float t = point / (float)(path.Length - 1);
                    Vector3 curve = CubicBezier(p0, p1, p2, p3, t);
                    float wave = Mathf.Sin(t * Mathf.PI * (2.2f + i * 0.15f) + i * 1.7f) * (0.06f + t * 0.09f);
                    curve.y += wave;
                    path[point] = curve;
                }

                rootPaths[i] = path;
                rootLines[i] = CreateLine("Root_" + i, rootMaterial, -3, 0.16f, 0.035f);
                rootGlowLines[i] = CreateLine("RootGlow_" + i, rootGlowMaterial, -2, 0.045f, 0.012f);
            }
        }

        private void BuildParticleLayers()
        {
            BuildInwardSpores();
            BuildOrbitingWisps();
            BuildGroundMist();
            BuildShellShards();
            BuildRootDebris();
            BuildImpactStreaks();
            BuildAfterglowSpores();
        }

        private void BuildInwardSpores()
        {
            ParticleSystem ps = CreateParticleSystem("InwardSpores", additiveSporeMaterial, 10);
            particleSystems.Add(ps);

            var main = ps.main;
            main.duration = burstTime;
            main.startLifetime = new ParticleSystem.MinMaxCurve(burstTime * 0.62f, burstTime * 0.98f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(-effectRadius * 1.45f, -effectRadius * 0.88f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.20f);
            main.startColor = Color.white;
            main.maxParticles = 280;

            var emission = ps.emission;
            emission.rateOverTime = 92f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = effectRadius * 1.28f;
            shape.radiusThickness = 0.16f;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.42f;
            noise.frequency = 1.6f;
            noise.scrollSpeed = 0.8f;

            SetFadeAndShrink(ps, 0.08f, 0.20f, 0.78f);

            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.Velocity;
        }

        private void BuildOrbitingWisps()
        {
            ParticleSystem ps = CreateParticleSystem("OrbitingWisps", additiveBrightMaterial, 11);
            particleSystems.Add(ps);

            var main = ps.main;
            main.duration = burstTime + 0.12f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 1.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(-2.8f, -1.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.30f);
            main.startColor = Color.white;
            main.maxParticles = 90;

            var emission = ps.emission;
            emission.rateOverTime = 24f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = effectRadius * 0.72f;
            shape.radiusThickness = 0.28f;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            // Unity requires all three orbital axes to use the same curve mode.
            // Keep them as constants: assigning a TwoConstants curve to only one axis
            // first triggers "Particle Orbital Velocity curves must all be in the same mode".
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(0f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(4.4f);
            velocity.radial = new ParticleSystem.MinMaxCurve(-0.25f, 0.35f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.28f;
            noise.frequency = 1.2f;
            noise.scrollSpeed = 1.4f;

            SetFadeAndShrink(ps, 0.05f, 0.24f, 0.72f);
        }

        private void BuildGroundMist()
        {
            ParticleSystem ps = CreateParticleSystem("GroundMist", alphaMistMaterial, 5);
            ps.transform.localPosition = new Vector3(0f, groundY, 0f);
            particleSystems.Add(ps);

            var main = ps.main;
            main.duration = 0.9f;
            main.startDelay = burstTime * 0.82f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.65f, 1.55f);
            main.gravityModifier = -0.08f;
            main.startColor = new Color(1f, 1f, 1f, 0.55f);
            main.maxParticles = 70;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24, 34) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = effectRadius * 0.42f;
            shape.radiusThickness = 0.8f;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.55f, 0.55f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.25f, 1.15f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.55f;
            noise.frequency = 0.75f;
            noise.scrollSpeed = 0.5f;

            SetFadeAndShrink(ps, 0.05f, 0.28f, 0.68f);
        }

        private void BuildShellShards()
        {
            ParticleSystem ps = CreateParticleSystem("ShellShards", additiveShellMaterial, 15);
            particleSystems.Add(ps);

            var main = ps.main;
            main.duration = 0.30f;
            main.startDelay = burstTime;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.58f, 1.05f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5.0f, 9.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.50f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.82f;
            main.startColor = Color.white;
            main.maxParticles = 70;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28, 38) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = effectRadius * 0.18f;
            shape.radiusThickness = 1f;

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-5.5f, 5.5f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.radial = new ParticleSystem.MinMaxCurve(0.4f, 1.0f);

            SetFadeAndShrink(ps, 0.02f, 0.32f, 0.75f);
        }

        private void BuildRootDebris()
        {
            ParticleSystem ps = CreateParticleSystem("RootDebris", alphaRootShardMaterial, 6);
            particleSystems.Add(ps);

            var main = ps.main;
            main.duration = 0.24f;
            main.startDelay = burstTime + 0.015f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 7.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.34f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 1.25f;
            main.startColor = new Color(1f, 1f, 1f, 0.75f);
            main.maxParticles = 50;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14, 22) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = effectRadius * 0.26f;
            shape.radiusThickness = 1f;

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

            SetFadeAndShrink(ps, 0.02f, 0.30f, 0.78f);
        }

        private void BuildImpactStreaks()
        {
            ParticleSystem ps = CreateParticleSystem("ImpactStreaks", additiveBrightMaterial, 16);
            particleSystems.Add(ps);

            var main = ps.main;
            main.duration = 0.18f;
            main.startDelay = Mathf.Max(0f, burstTime - 0.01f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(12f, 20f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            main.maxParticles = 45;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20, 30) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = effectRadius * 0.10f;
            shape.radiusThickness = 1f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.7f;
            renderer.velocityScale = 0.16f;

            SetFadeAndShrink(ps, 0f, 0.30f, 1f);
        }

        private void BuildAfterglowSpores()
        {
            ParticleSystem ps = CreateParticleSystem("AfterglowSpores", additiveSoftMaterial, 13);
            particleSystems.Add(ps);

            var main = ps.main;
            main.duration = 0.25f;
            main.startDelay = burstTime + 0.12f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.85f, 1.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 4.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
            main.gravityModifier = -0.10f;
            main.startColor = Color.white;
            main.maxParticles = 110;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 34, 52) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = effectRadius * 0.25f;
            shape.radiusThickness = 1f;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.15f, 1.10f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.42f;
            noise.frequency = 0.9f;
            noise.scrollSpeed = 0.7f;

            SetFadeAndShrink(ps, 0.04f, 0.22f, 0.72f);
        }

        private IEnumerator AnimateEffect()
        {
            float elapsed = 0f;
            while (elapsed < totalDuration)
            {
                float delta = Time.deltaTime;
                elapsed += delta;
                UpdateVisualTimeline(elapsed);
                yield return null;
            }

            HideSprite(coreGlow);
            HideSprite(cocoonHalo);
            HideSprite(impactFlash);
            HideSprite(chargeRing);
            HideSprite(pulseRingA);
            HideSprite(pulseRingB);
            HideSprite(pulseRingC);
            HideSprite(shockwaveA);
            HideSprite(shockwaveB);
            HideSprite(groundShockwave);
            Destroy(gameObject);
        }

        private void UpdateVisualTimeline(float elapsed)
        {
            float charge = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, burstTime));
            float easedCharge = Smooth01(charge);
            float pulse = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(elapsed * Mathf.Lerp(8f, 24f, easedCharge)), 7f);

            UpdateRoots(easedCharge, pulse);

            if (elapsed < burstTime)
            {
                float coreScale = Mathf.Lerp(0.45f, 1.15f, easedCharge) + pulse * 0.28f;
                SetSprite(coreGlow, Vector3.one * (effectRadius * 0.34f * coreScale), 0.16f + easedCharge * 0.48f + pulse * 0.14f);
                SetSprite(cocoonHalo,
                    new Vector3(effectRadius * (0.72f + easedCharge * 0.16f), effectRadius * (1.02f + easedCharge * 0.16f), 1f),
                    0.07f + easedCharge * 0.18f + pulse * 0.05f);

                SetSprite(chargeRing,
                    new Vector3(effectRadius * (0.62f + easedCharge * 0.18f), effectRadius * (0.17f + easedCharge * 0.06f), 1f),
                    0.14f + easedCharge * 0.22f);
                UpdateChargePulses(elapsed, easedCharge);

                HideSprite(impactFlash);
                HideSprite(shockwaveA);
                HideSprite(shockwaveB);
                HideSprite(groundShockwave);
            }
            else
            {
                float afterBurst = elapsed - burstTime;
                float flashT = Mathf.Clamp01(afterBurst / 0.12f);
                float shockT = Mathf.Clamp01(afterBurst / 0.68f);
                float groundT = Mathf.Clamp01(Mathf.Max(0f, afterBurst - 0.04f) / 0.88f);

                float flashAlpha = 1f - Smooth01(flashT);
                SetSprite(impactFlash,
                    Vector3.one * Mathf.Lerp(effectRadius * 0.16f, effectRadius * 0.76f, EaseOut(flashT)),
                    flashAlpha * 0.30f);

                SetSprite(coreGlow,
                    Vector3.one * Mathf.Lerp(effectRadius * 0.46f, effectRadius * 0.26f, EaseOut(Mathf.Clamp01(afterBurst / 0.65f))),
                    Mathf.Lerp(0.19f, 0f, Smooth01(Mathf.Clamp01(afterBurst / 0.72f))));
                HideSprite(cocoonHalo);
                HideSprite(chargeRing);
                HideSprite(pulseRingA);
                HideSprite(pulseRingB);
                HideSprite(pulseRingC);

                SetSprite(shockwaveA,
                    new Vector3(effectRadius * Mathf.Lerp(0.62f, 2.55f, EaseOut(shockT)),
                        effectRadius * Mathf.Lerp(0.30f, 1.62f, EaseOut(shockT)), 1f),
                    (1f - Smooth01(shockT)) * 0.48f);
                SetSprite(shockwaveB,
                    new Vector3(effectRadius * Mathf.Lerp(0.38f, 2.12f, EaseOut(Mathf.Clamp01(Mathf.Max(0f, afterBurst - 0.09f) / 0.74f))),
                        effectRadius * Mathf.Lerp(0.22f, 1.36f, EaseOut(Mathf.Clamp01(Mathf.Max(0f, afterBurst - 0.09f) / 0.74f))), 1f),
                    (1f - Smooth01(Mathf.Clamp01(Mathf.Max(0f, afterBurst - 0.09f) / 0.74f))) * 0.38f);
                SetSprite(groundShockwave,
                    new Vector3(effectRadius * Mathf.Lerp(0.52f, 2.85f, EaseOut(groundT)),
                        effectRadius * Mathf.Lerp(0.12f, 0.40f, EaseOut(groundT)), 1f),
                    (1f - Smooth01(groundT)) * 0.42f);

                UpdateRoots(Mathf.Lerp(1f, 0f, Smooth01(Mathf.Clamp01(afterBurst / 0.55f))), 1f - Smooth01(Mathf.Clamp01(afterBurst / 0.55f)));
            }
        }

        private void UpdateChargePulses(float elapsed, float charge)
        {
            UpdatePulseRing(pulseRingA, elapsed, 0.00f, charge, 0.22f);
            UpdatePulseRing(pulseRingB, elapsed, 0.14f, charge, 0.18f);
            UpdatePulseRing(pulseRingC, elapsed, 0.28f, charge, 0.14f);
        }

        private void UpdatePulseRing(SpriteRenderer ring, float elapsed, float offset, float charge, float alpha)
        {
            float pulseDuration = 0.52f;
            float normalized = Mathf.Repeat(elapsed + offset, pulseDuration) / pulseDuration;
            float ringScale = Mathf.Lerp(0.72f, 1.60f, EaseOut(normalized));
            float ringAlpha = (1f - Smooth01(normalized)) * charge * alpha;
            SetSprite(ring,
                new Vector3(effectRadius * ringScale, effectRadius * ringScale * 0.62f, 1f),
                ringAlpha);
        }

        private void UpdateRoots(float reveal, float pulse)
        {
            if (rootPaths == null)
                return;

            for (int i = 0; i < rootPaths.Length; i++)
            {
                float pathReveal = Mathf.Clamp01(reveal * 1.14f - (i % 3) * 0.035f);
                int visiblePoints = Mathf.Clamp(Mathf.CeilToInt(pathReveal * rootPaths[i].Length), 0, rootPaths[i].Length);
                SetLinePath(rootLines[i], rootPaths[i], visiblePoints);
                SetLinePath(rootGlowLines[i], rootPaths[i], Mathf.Clamp(visiblePoints - 1, 0, rootPaths[i].Length));

                float alpha = Mathf.Clamp01(pathReveal) * (0.55f + pulse * 0.45f);
                rootLines[i].startColor = WithAlpha(rootBrown, alpha);
                rootLines[i].endColor = WithAlpha(rootBrown, alpha * 0.15f);
                rootGlowLines[i].startColor = WithAlpha(acidGreen, alpha * 0.82f);
                rootGlowLines[i].endColor = WithAlpha(acidGreen, alpha * 0.06f);
            }
        }

        private void SetLinePath(LineRenderer line, Vector3[] path, int visiblePoints)
        {
            if (line == null || visiblePoints < 2)
            {
                if (line != null)
                    line.positionCount = 0;
                return;
            }

            line.positionCount = visiblePoints;
            for (int i = 0; i < visiblePoints; i++)
                line.SetPosition(i, path[i]);
        }

        private ParticleSystem CreateParticleSystem(string name, Material material, int sortingOrder)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.simulationSpeed = 1f;

            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortingOrder = sortingOrder;
            renderer.material = material;
            renderer.maskInteraction = SpriteMaskInteraction.None;
            return ps;
        }

        private void StartAllParticles()
        {
            for (int i = 0; i < particleSystems.Count; i++)
                if (particleSystems[i] != null)
                    particleSystems[i].Play();
        }

        private SpriteRenderer CreateSpriteLayer(string name, Sprite sprite, Material material, int sortingOrder)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            renderer.color = new Color(1f, 1f, 1f, 0f);
            return renderer;
        }

        private LineRenderer CreateLine(string name, Material material, int sortingOrder, float startWidth, float endWidth)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCornerVertices = 4;
            line.numCapVertices = 3;
            line.startWidth = startWidth;
            line.endWidth = endWidth;
            line.sharedMaterial = material;
            line.sortingOrder = sortingOrder;
            line.positionCount = 0;
            return line;
        }

        private static void SetFadeAndShrink(ParticleSystem ps, float fadeIn, float hold, float fadeOut)
        {
            var color = ps.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, Mathf.Clamp01(fadeIn + hold)),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, Mathf.Clamp01(fadeIn)),
                    new GradientAlphaKey(1f, Mathf.Clamp01(fadeIn + hold)),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.35f),
                    new Keyframe(0.16f, 1f),
                    new Keyframe(0.70f, 0.82f),
                    new Keyframe(1f, 0.05f)));
        }

        private static Texture2D CreateSoftDiscTexture(int size, string name)
        {
            Texture2D texture = CreateTexture(size, size, name);
            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float radius = center;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / radius;
                    float dy = (y - center) / radius;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = 1f - SmoothRange(0.10f, 1f, distance);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha * alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateRingTexture(int size, string name)
        {
            Texture2D texture = CreateTexture(size, size, name);
            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float radius = center;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / radius;
                    float dy = (y - center) / radius;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);
                    float organicRadius = 0.66f
                        + Mathf.Sin(angle * 5f + 0.7f) * 0.025f
                        + Mathf.Sin(angle * 9f - 1.4f) * 0.018f;
                    float ringDistance = Mathf.Abs(distance - organicRadius);
                    float alpha = 1f - SmoothRange(0.004f, 0.038f, ringDistance);
                    alpha *= Mathf.Clamp01(1f - Mathf.Max(0f, distance - 0.96f) * 20f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha * 0.95f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateShardTexture(int size, string name)
        {
            Texture2D texture = CreateTexture(size, size, name);
            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float half = center;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - center) / half;
                    float ny = (y - center) / half;
                    float width = Mathf.Lerp(0.05f, 0.80f, Mathf.Clamp01((ny + 1f) * 0.5f));
                    float inside = Mathf.Clamp01((ny + 1f) * 0.5f) * Mathf.Clamp01((1f - ny) * 0.65f);
                    float alpha = (Mathf.Abs(nx) < width ? inside : 0f);
                    float edge = SmoothRange(0f, 0.16f, width - Mathf.Abs(nx));
                    alpha *= edge;

                    // A narrow transparent cut gives the shard a leaf/shell silhouette.
                    float vein = Mathf.Abs(nx - ny * 0.08f);
                    alpha *= 0.75f + 0.25f * Mathf.Clamp01(vein * 12f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateTexture(int width, int height, string name)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            texture.name = name;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        private Sprite CreateSprite(Texture2D texture, string name)
        {
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), texture.width);
            sprite.name = name;
            generatedAssets.Add(sprite);
            return sprite;
        }

        private Material CreateMaterial(Shader shader, Texture2D texture, Color tint, bool additive, string name)
        {
            Material material = new Material(shader);
            material.name = name;
            material.hideFlags = HideFlags.HideAndDontSave;
            material.SetTexture("_MainTex", texture);
            material.SetColor("_TintColor", tint);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.renderQueue = (int)RenderQueue.Transparent;
            generatedAssets.Add(material);
            return material;
        }

        private static Vector3 CubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float oneMinusT = 1f - t;
            return oneMinusT * oneMinusT * oneMinusT * p0
                + 3f * oneMinusT * oneMinusT * t * p1
                + 3f * oneMinusT * t * t * p2
                + t * t * t * p3;
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static float SmoothRange(float edge0, float edge1, float value)
        {
            return Smooth01(Mathf.InverseLerp(edge0, edge1, value));
        }

        private static float EaseOut(float value)
        {
            value = Mathf.Clamp01(value);
            float inverse = 1f - value;
            return 1f - inverse * inverse * inverse;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static void SetSprite(SpriteRenderer renderer, Vector3 scale, float alpha)
        {
            if (renderer == null)
                return;

            renderer.enabled = alpha > 0.002f;
            renderer.transform.localScale = scale;
            Color color = renderer.color;
            color.a = Mathf.Clamp01(alpha);
            renderer.color = color;
        }

        private static void HideSprite(SpriteRenderer renderer)
        {
            if (renderer == null)
                return;
            renderer.enabled = false;
            Color color = renderer.color;
            color.a = 0f;
            renderer.color = color;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < generatedAssets.Count; i++)
            {
                if (generatedAssets[i] != null)
                    Destroy(generatedAssets[i]);
            }
            generatedAssets.Clear();

            DestroyGeneratedTexture(softTexture);
            DestroyGeneratedTexture(ringTexture);
            DestroyGeneratedTexture(shardTexture);
        }

        private static void DestroyGeneratedTexture(Texture2D texture)
        {
            if (texture != null)
                Destroy(texture);
        }
    }
}
