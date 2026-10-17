using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bao.Bosses
{
    public class DreadwormPhaseTransitionVFX : MonoBehaviour
    {
        [Header("Phase Transition Settings")]
        [SerializeField, Min(0.35f)] private float gatherDuration = 1f;
        [SerializeField, Min(0.35f)] private float explosionDuration = 0.9f;
        [SerializeField, Min(0.5f)] private float intensity = 3f;

        [Header("Color Settings")]
        [ColorUsage(true, true)]
        [SerializeField] private Color innerColor = new Color(2.15f, 0.24f, 0.38f, 1f);
        [ColorUsage(true, true)]
        [SerializeField] private Color outerColor = new Color(2.35f, 0.88f, 0.18f, 1f);

        private readonly List<Material> ownedMaterials = new List<Material>();
        private readonly List<ParticleSystem> particleSystems = new List<ParticleSystem>();

        private DreadwormBoss owner;
        private bool initialized;
        private bool playing;
        private float effectRadius = 4.5f;
        private float groundOffset = -1.2f;

        private Material frostMaterial;
        private Material brightMaterial;
        private Material stormMaterial;
        private Material rubyShardMaterial;
        private Material amberShardMaterial;
        private Material mistMaterial;

        private SpriteRenderer coreGlow;
        private SpriteRenderer innerRing;
        private SpriteRenderer middleRing;
        private SpriteRenderer outerRing;
        private SpriteRenderer groundSeal;
        private SpriteRenderer flash;
        private SpriteRenderer shockwaveA;
        private SpriteRenderer shockwaveB;
        private LineRenderer[] lightningArcs;

        private ParticleSystem inwardCrystals;
        private ParticleSystem orbitSparks;
        private ParticleSystem burstShards;
        private ParticleSystem burstAmberShards;
        private ParticleSystem groundMist;

        private void Awake()
        {
            CreateMaterials();
        }

        private void Start()
        {
            if (!initialized)
            {
                owner = Object.FindFirstObjectByType<DreadwormBoss>();
                ConfigureFromOwner();
                Play();
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < ownedMaterials.Count; i++)
            {
                if (ownedMaterials[i] != null)
                    Destroy(ownedMaterials[i]);
            }
            ownedMaterials.Clear();
        }

        public void Initialize(DreadwormBoss dreadworm, float gameplayDuration)
        {
            owner = dreadworm;
            initialized = true;

            float safeDuration = Mathf.Max(0.8f, gameplayDuration);
            gatherDuration = Mathf.Clamp(safeDuration * 0.68f, 0.65f, 1.25f);
            explosionDuration = Mathf.Clamp(safeDuration * 0.62f, 0.55f, 1.05f);
            ConfigureFromOwner();
            Play();
        }

        private void ConfigureFromOwner()
        {
            if (owner == null || owner.Collider2D == null)
                return;

            Bounds bounds = owner.Collider2D.bounds;
            effectRadius = Mathf.Max(4.5f, Mathf.Max(bounds.extents.x * 1.5f, bounds.extents.y * 2.1f));
            groundOffset = bounds.min.y - bounds.center.y + 0.08f;
            transform.position = bounds.center;
        }

        private void Play()
        {
            if (playing)
                return;

            playing = true;
            DreadwormVFXUtility.ConfigureSortingGroup(gameObject, 10);
            BuildSpriteLayers();
            BuildLightningArcs();
            BuildParticleLayers();
            StartCoroutine(PlaySequence());
        }

        private void CreateMaterials()
        {
            float visualIntensity = Mathf.Clamp(intensity, 1f, 1.75f);
            Color ruby = DreadwormVFXUtility.BloodRubyCore * visualIntensity;
            Color amber = DreadwormVFXUtility.MoltenAmber * visualIntensity;

            frostMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                ruby,
                true,
                DreadwormVFXUtility.RingTexture,
                "DreadwormPhase_RubyRing"));
            brightMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                DreadwormVFXUtility.GlintFrostApex * visualIntensity,
                true,
                DreadwormVFXUtility.SoftDiscTexture,
                "DreadwormPhase_BrightCore"));
            stormMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                amber,
                true,
                DreadwormVFXUtility.RingTexture,
                "DreadwormPhase_AmberRing"));
            rubyShardMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                ruby,
                true,
                DreadwormVFXUtility.ShardTexture,
                "DreadwormPhase_RubyShards"));
            amberShardMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                amber,
                true,
                DreadwormVFXUtility.ShardTexture,
                "DreadwormPhase_AmberShards"));
            mistMaterial = Track(DreadwormVFXUtility.CreateMaterial(
                new Color(0.38f, 0.16f, 0.12f, 0.65f),
                false,
                DreadwormVFXUtility.SoftDiscTexture,
                "DreadwormPhase_Mist"));
        }

        private void BuildSpriteLayers()
        {
            coreGlow = CreateLayer(
                "CompressionCore",
                DreadwormVFXUtility.SoftDiscSprite,
                brightMaterial,
                18);
            innerRing = CreateLayer(
                "CrystalRing_Inner",
                DreadwormVFXUtility.RingSprite,
                frostMaterial,
                14);
            middleRing = CreateLayer(
                "CrystalRing_Middle",
                DreadwormVFXUtility.RingSprite,
                stormMaterial,
                13);
            outerRing = CreateLayer(
                "CrystalRing_Outer",
                DreadwormVFXUtility.RingSprite,
                frostMaterial,
                12);
            groundSeal = CreateLayer(
                "SubterraneanSeal",
                DreadwormVFXUtility.RingSprite,
                stormMaterial,
                11);
            flash = CreateLayer(
                "PhaseFlash",
                DreadwormVFXUtility.SoftDiscSprite,
                brightMaterial,
                20);
            shockwaveA = CreateLayer(
                "PhaseShockwave_A",
                DreadwormVFXUtility.RingSprite,
                stormMaterial,
                19);
            shockwaveB = CreateLayer(
                "PhaseShockwave_B",
                DreadwormVFXUtility.RingSprite,
                frostMaterial,
                18);

            groundSeal.transform.localPosition = Vector3.up * groundOffset;
            HideLayer(coreGlow);
            HideLayer(innerRing);
            HideLayer(middleRing);
            HideLayer(outerRing);
            HideLayer(groundSeal);
            HideLayer(flash);
            HideLayer(shockwaveA);
            HideLayer(shockwaveB);
        }

        private void BuildLightningArcs()
        {
            lightningArcs = new LineRenderer[9];
            for (int i = 0; i < lightningArcs.Length; i++)
            {
                GameObject lineObject = new GameObject("StormArc_" + i);
                lineObject.transform.SetParent(transform, false);
                LineRenderer line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 10;
                line.sharedMaterial = i % 3 == 0 ? brightMaterial : (i % 2 == 0 ? frostMaterial : stormMaterial);
                line.textureMode = LineTextureMode.Stretch;
                line.numCapVertices = 2;
                line.startWidth = i % 3 == 0 ? 0.085f : 0.055f;
                line.endWidth = 0.014f;
                line.startColor = Color.clear;
                line.endColor = Color.clear;
                DreadwormVFXUtility.ConfigureRenderer(line, 16 + i % 2);
                lightningArcs[i] = line;
            }
        }

        private void BuildParticleLayers()
        {
            inwardCrystals = DreadwormVFXUtility.CreateParticleSystem(
                gameObject,
                "InwardCrystalFragments",
                rubyShardMaterial,
                16);
            particleSystems.Add(inwardCrystals);
            var inwardMain = inwardCrystals.main;
            inwardMain.duration = gatherDuration;
            inwardMain.loop = false;
            inwardMain.startLifetime = new ParticleSystem.MinMaxCurve(
                gatherDuration * 0.52f,
                gatherDuration * 0.92f);
            inwardMain.startSpeed = new ParticleSystem.MinMaxCurve(
                -effectRadius * 1.8f,
                -effectRadius * 0.95f);
            inwardMain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
            inwardMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            inwardMain.maxParticles = 220;
            var inwardEmission = inwardCrystals.emission;
            inwardEmission.rateOverTime = 86f;
            var inwardShape = inwardCrystals.shape;
            inwardShape.shapeType = ParticleSystemShapeType.Circle;
            inwardShape.radius = effectRadius * 1.15f;
            inwardShape.radiusThickness = 0.18f;
            var inwardRotation = inwardCrystals.rotationOverLifetime;
            inwardRotation.enabled = true;
            inwardRotation.z = new ParticleSystem.MinMaxCurve(-7f, 7f);
            ConfigureFade(inwardCrystals, 0.08f, 0.62f);

            orbitSparks = DreadwormVFXUtility.CreateParticleSystem(
                gameObject,
                "OrbitingStormSparks",
                amberShardMaterial,
                17);
            particleSystems.Add(orbitSparks);
            var sparkMain = orbitSparks.main;
            sparkMain.duration = gatherDuration;
            sparkMain.loop = false;
            sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.62f);
            sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
            sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.16f);
            sparkMain.maxParticles = 150;
            var sparkEmission = orbitSparks.emission;
            sparkEmission.rateOverTime = 62f;
            var sparkShape = orbitSparks.shape;
            sparkShape.shapeType = ParticleSystemShapeType.Donut;
            sparkShape.radius = effectRadius * 0.56f;
            sparkShape.donutRadius = effectRadius * 0.12f;
            var sparkVelocity = orbitSparks.velocityOverLifetime;
            sparkVelocity.enabled = true;
            sparkVelocity.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            sparkVelocity.orbitalY = new ParticleSystem.MinMaxCurve(0f, 0f);
            sparkVelocity.orbitalZ = new ParticleSystem.MinMaxCurve(-5f, 5f);
            var sparkNoise = orbitSparks.noise;
            sparkNoise.enabled = true;
            sparkNoise.strength = 0.48f;
            sparkNoise.frequency = 1.8f;
            ConfigureFade(orbitSparks, 0.04f, 0.5f);

            burstShards = DreadwormVFXUtility.CreateParticleSystem(
                gameObject,
                "PhaseEruptionRubyShards",
                rubyShardMaterial,
                22);
            particleSystems.Add(burstShards);
            var burstMain = burstShards.main;
            burstMain.duration = 0.2f;
            burstMain.loop = false;
            burstMain.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1.05f);
            burstMain.startSpeed = new ParticleSystem.MinMaxCurve(
                effectRadius * 1.1f,
                effectRadius * 2.35f);
            burstMain.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.46f);
            burstMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            burstMain.gravityModifier = 0.32f;
            burstMain.maxParticles = 100;
            var burstEmission = burstShards.emission;
            burstEmission.rateOverTime = 0f;
            burstEmission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 32, 45)
            });
            var burstShape = burstShards.shape;
            burstShape.shapeType = ParticleSystemShapeType.Circle;
            burstShape.radius = effectRadius * 0.18f;
            burstShape.radiusThickness = 1f;
            var burstRotation = burstShards.rotationOverLifetime;
            burstRotation.enabled = true;
            burstRotation.z = new ParticleSystem.MinMaxCurve(-9f, 9f);
            ConfigureFade(burstShards, 0.02f, 0.58f);

            burstAmberShards = DreadwormVFXUtility.CreateParticleSystem(
                gameObject,
                "PhaseEruptionAmberShards",
                amberShardMaterial,
                21);
            particleSystems.Add(burstAmberShards);
            var amberMain = burstAmberShards.main;
            amberMain.duration = 0.2f;
            amberMain.loop = false;
            amberMain.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.85f);
            amberMain.startSpeed = new ParticleSystem.MinMaxCurve(
                effectRadius * 1.4f,
                effectRadius * 2.8f);
            amberMain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.35f);
            amberMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            amberMain.gravityModifier = 0.45f;
            amberMain.maxParticles = 80;
            var amberEmission = burstAmberShards.emission;
            amberEmission.rateOverTime = 0f;
            amberEmission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 24, 35)
            });
            var amberShape = burstAmberShards.shape;
            amberShape.shapeType = ParticleSystemShapeType.Circle;
            amberShape.radius = effectRadius * 0.22f;
            amberShape.radiusThickness = 1f;
            var amberRotation = burstAmberShards.rotationOverLifetime;
            amberRotation.enabled = true;
            amberRotation.z = new ParticleSystem.MinMaxCurve(-12f, 12f);
            ConfigureFade(burstAmberShards, 0.02f, 0.52f);

            groundMist = DreadwormVFXUtility.CreateParticleSystem(
                gameObject,
                "ColdGroundMist",
                mistMaterial,
                9);
            groundMist.transform.localPosition = Vector3.up * groundOffset;
            particleSystems.Add(groundMist);
            var mistMain = groundMist.main;
            mistMain.duration = gatherDuration + explosionDuration;
            mistMain.loop = false;
            mistMain.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.35f);
            mistMain.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.75f);
            mistMain.startSize = new ParticleSystem.MinMaxCurve(0.55f, 1.45f);
            mistMain.startColor = new Color(1f, 1f, 1f, 0.48f);
            mistMain.maxParticles = 130;
            var mistEmission = groundMist.emission;
            mistEmission.rateOverTime = 28f;
            var mistShape = groundMist.shape;
            mistShape.shapeType = ParticleSystemShapeType.Circle;
            mistShape.radius = effectRadius * 0.68f;
            mistShape.radiusThickness = 0.75f;
            mistShape.scale = new Vector3(1f, 0.12f, 1f);
            var mistVelocity = groundMist.velocityOverLifetime;
            mistVelocity.enabled = true;
            // Unity requires X/Y/Z velocity curves in this module to use the same mode.
            // X and Y use TwoConstants, so keep the stationary Z axis TwoConstants too.
            mistVelocity.x = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);
            mistVelocity.y = new ParticleSystem.MinMaxCurve(0.08f, 0.38f);
            mistVelocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            ConfigureFade(groundMist, 0.04f, 0.42f);
        }

        private IEnumerator PlaySequence()
        {
            inwardCrystals.Play();
            orbitSparks.Play();
            groundMist.Play();

            float elapsed = 0f;
            while (elapsed < gatherDuration)
            {
                elapsed += Time.deltaTime;
                FollowOwner();
                float normalized = Mathf.Clamp01(elapsed / gatherDuration);
                float eased = DreadwormVFXUtility.EaseInOut(normalized);
                float pulse = 0.84f + Mathf.Sin(Time.time * 12f) * 0.16f;

                SetLayer(
                    coreGlow,
                    Vector3.one * effectRadius * Mathf.Lerp(0.08f, 0.45f, eased) * pulse,
                    new Color(2.15f, 0.24f, 0.38f, Mathf.Lerp(0.15f, 0.85f, eased)));
                SetRingGather(innerRing, effectRadius * 0.92f, 0.88f, 160f, eased, pulse, DreadwormVFXUtility.BloodRubyCore);
                SetRingGather(middleRing, effectRadius * 1.32f, 0.72f, -115f, eased, pulse, DreadwormVFXUtility.MoltenAmber);
                SetRingGather(outerRing, effectRadius * 1.72f, 0.55f, 85f, eased, pulse, DreadwormVFXUtility.BloodRubyCore);

                groundSeal.transform.localPosition = Vector3.up * groundOffset;
                groundSeal.transform.localRotation = Quaternion.identity;
                groundSeal.transform.localScale = new Vector3(
                    effectRadius * 2.2f * Mathf.Lerp(1.25f, 0.92f, eased),
                    effectRadius * 0.52f * Mathf.Lerp(1.25f, 0.92f, eased),
                    1f);
                groundSeal.color = new Color(2.35f, 0.88f, 0.18f, Mathf.Lerp(0.12f, 0.78f, eased));

                UpdateLightning(eased, false);
                yield return null;
            }

            burstShards.Play();
            burstAmberShards.Play();
            if (CameraController.Instance != null)
                CameraController.Instance.ShakeCamera(0.28f, 0.18f);

            elapsed = 0f;
            while (elapsed < explosionDuration)
            {
                elapsed += Time.deltaTime;
                FollowOwner();
                float normalized = Mathf.Clamp01(elapsed / explosionDuration);
                float eased = DreadwormVFXUtility.EaseOutCubic(normalized);
                float fade = 1f - normalized;

                flash.transform.localScale = Vector3.one * effectRadius * Mathf.Lerp(0.15f, 0.85f, eased);
                flash.color = new Color(2.55f, 2.15f, 1.85f, (1f - eased) * 0.75f);

                AnimateShockwave(
                    shockwaveA,
                    effectRadius * 2.45f,
                    effectRadius * 0.75f,
                    eased,
                    fade * 0.75f,
                    DreadwormVFXUtility.MoltenAmber);
                AnimateShockwave(
                    shockwaveB,
                    effectRadius * 1.85f,
                    effectRadius * 0.48f,
                    Mathf.Clamp01(normalized * 1.25f),
                    fade * 0.65f,
                    DreadwormVFXUtility.BloodRubyCore);

                coreGlow.transform.localScale = Vector3.one * effectRadius * Mathf.Lerp(0.45f, 0.75f, eased);
                coreGlow.color = new Color(2.15f, 0.24f, 0.38f, fade * 0.45f);
                innerRing.color = new Color(2.15f, 0.24f, 0.38f, fade * 0.6f);
                middleRing.color = new Color(2.35f, 0.88f, 0.18f, fade * 0.52f);
                outerRing.color = new Color(2.15f, 0.24f, 0.38f, fade * 0.4f);
                groundSeal.color = new Color(2.05f, 0.72f, 0.15f, fade * 0.45f);

                UpdateLightning(fade, true);
                yield return null;
            }

            ClearLightning();
            Destroy(gameObject, 0.55f);
        }

        private void SetRingGather(
            SpriteRenderer ring,
            float startingScale,
            float alpha,
            float rotationSpeed,
            float progress,
            float pulse,
            Color baseColor)
        {
            float scale = startingScale * Mathf.Lerp(1.25f, 0.34f, progress);
            ring.transform.localScale = Vector3.one * scale * pulse;
            ring.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * rotationSpeed);
            ring.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha * Mathf.Lerp(0.12f, 1f, progress));
        }

        private static void AnimateShockwave(
            SpriteRenderer ring,
            float targetX,
            float targetY,
            float progress,
            float alpha,
            Color color)
        {
            float eased = DreadwormVFXUtility.EaseOutCubic(progress);
            ring.transform.localScale = new Vector3(
                Mathf.Lerp(0.2f, targetX, eased),
                Mathf.Lerp(0.1f, targetY, eased),
                1f);
            ring.transform.localRotation = Quaternion.identity;
            ring.color = new Color(color.r, color.g, color.b, alpha);
        }

        private void UpdateLightning(float strength, bool erupting)
        {
            for (int arcIndex = 0; arcIndex < lightningArcs.Length; arcIndex++)
            {
                LineRenderer line = lightningArcs[arcIndex];
                float angle = arcIndex * Mathf.PI * 2f / lightningArcs.Length + Time.time * 0.42f;
                Vector2 radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 tangent = new Vector2(-radial.y, radial.x);
                float startRadius = erupting
                    ? effectRadius * 0.12f
                    : effectRadius * Mathf.Lerp(1.12f, 0.42f, strength);
                float endRadius = erupting
                    ? effectRadius * Mathf.Lerp(0.35f, 1.38f, 1f - strength)
                    : effectRadius * 0.08f;

                for (int pointIndex = 0; pointIndex < line.positionCount; pointIndex++)
                {
                    float normalized = pointIndex / (line.positionCount - 1f);
                    float radius = Mathf.Lerp(startRadius, endRadius, normalized);
                    float jagged = Mathf.Sin(
                        pointIndex * 8.73f +
                        arcIndex * 3.17f +
                        Time.time * 26f) * effectRadius * 0.035f * strength;
                    Vector2 point = radial * radius + tangent * jagged;
                    point.y *= erupting ? 0.82f : 1f;
                    line.SetPosition(pointIndex, point);
                }

                float flicker = 0.62f + Mathf.Sin(Time.time * 37f + arcIndex * 2.4f) * 0.38f;
                float alpha = Mathf.Clamp01(strength * flicker);
                Color arcColor = arcIndex % 3 == 0
                    ? DreadwormVFXUtility.GlintFrostApex
                    : (arcIndex % 2 == 0 ? DreadwormVFXUtility.BloodRubyCore : DreadwormVFXUtility.MoltenAmber);
                line.startColor = new Color(arcColor.r, arcColor.g, arcColor.b, alpha);
                line.endColor = new Color(arcColor.r * 0.5f, arcColor.g * 0.5f, arcColor.b * 0.5f, alpha * 0.18f);
                line.startWidth = Mathf.Lerp(0.018f, arcIndex % 3 == 0 ? 0.115f : 0.075f, strength);
            }
        }

        private void FollowOwner()
        {
            if (owner != null && owner.Collider2D != null)
                transform.position = owner.Collider2D.bounds.center;
        }

        private void ClearLightning()
        {
            if (lightningArcs == null)
                return;

            for (int i = 0; i < lightningArcs.Length; i++)
            {
                if (lightningArcs[i] == null)
                    continue;
                lightningArcs[i].startColor = Color.clear;
                lightningArcs[i].endColor = Color.clear;
            }
        }

        private SpriteRenderer CreateLayer(
            string objectName,
            Sprite sprite,
            Material material,
            int sortingOffset)
        {
            GameObject layer = new GameObject(objectName);
            layer.transform.SetParent(transform, false);
            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            DreadwormVFXUtility.ConfigureRenderer(renderer, sortingOffset);
            return renderer;
        }

        private static void HideLayer(SpriteRenderer renderer)
        {
            renderer.transform.localScale = Vector3.zero;
            renderer.color = Color.clear;
        }

        private static void SetLayer(SpriteRenderer renderer, Vector3 scale, Color color)
        {
            renderer.transform.localScale = scale;
            renderer.color = color;
        }

        private static void ConfigureFade(
            ParticleSystem particleSystem,
            float fadeInTime,
            float fadeOutStart)
        {
            var color = particleSystem.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.18f, 0.58f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, fadeInTime),
                    new GradientAlphaKey(0.82f, fadeOutStart),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = gradient;

            var size = particleSystem.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.3f),
                    new Keyframe(0.16f, 1f),
                    new Keyframe(1f, 0.08f)));
        }

        private Material Track(Material material)
        {
            if (material != null)
                ownedMaterials.Add(material);
            return material;
        }
    }
}
