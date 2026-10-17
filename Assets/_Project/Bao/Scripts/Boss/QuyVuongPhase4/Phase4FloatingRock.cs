using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Bao.Bosses.QuyVuongPhase4
{
    /// <summary>
    /// Keeps the magma bomb anchored while it is waiting to be launched.
    /// The presentation component is deliberately separate so disabling this
    /// movement behaviour during the homing flight does not disable the VFX.
    /// </summary>
    public class Phase4FloatingRock : MonoBehaviour
    {
        [Header("Premium Magma VFX")]
        [SerializeField] private Material surfaceMaterial;
        [SerializeField] private Material glowMaterial;
        [SerializeField] private Material ringMaterial;
        [SerializeField] private Material trailMaterial;

        private Vector3 initialPos;
        private float floatOffset;
        private float bobbingAmplitude = 0.18f;
        private float bobbingSpeed = 2.5f;
        private bool isInitialized;

        internal Material SurfaceMaterial => surfaceMaterial;
        internal Material GlowMaterial => glowMaterial;
        internal Material RingMaterial => ringMaterial;
        internal Material TrailMaterial => trailMaterial;

        private void Awake()
        {
            if (GetComponent<Phase4FloatingRockVFX>() == null)
                gameObject.AddComponent<Phase4FloatingRockVFX>();
        }

        public void SetAnchorPosition(Vector3 anchor, float amplitude = 0.18f, float speed = 2.5f)
        {
            initialPos = anchor;
            bobbingAmplitude = amplitude;
            bobbingSpeed = speed;
            isInitialized = true;
        }

        private void Start()
        {
            if (!isInitialized)
                initialPos = transform.position;

            floatOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            if (!isInitialized) return;

            float bob = Mathf.Sin(Time.time * bobbingSpeed + floatOffset) * bobbingAmplitude;
            float microBob = Mathf.Sin(Time.time * bobbingSpeed * 2.17f + floatOffset) * bobbingAmplitude * 0.12f;
            transform.position = initialPos + new Vector3(0f, bob + microBob, 0f);
        }
    }

    /// <summary>
    /// Runtime presentation rig for the floating magma bomb. It enriches the
    /// authored prefab with layered molten light, orbiting obsidian fragments,
    /// directional embers and a launch trail while reusing its existing art and
    /// materials. No gameplay collider or damage values are changed here.
    /// </summary>
    internal sealed class Phase4FloatingRockVFX : MonoBehaviour
    {
        private const int DebrisCount = 5;

        private static readonly Color WhiteHot = new(1f, 0.94f, 0.62f, 1f);
        private static readonly Color MoltenGold = new(1f, 0.56f, 0.055f, 1f);
        private static readonly Color MagmaOrange = new(1f, 0.19f, 0.018f, 1f);
        private static readonly Color EmberRed = new(0.55f, 0.035f, 0.008f, 1f);

        private SpriteRenderer bombSprite;
        private Phase4FloatingRock owner;
        private ParticleSystem sparks;
        private ParticleSystem heatAura;
        private ParticleSystem launchEmbers;
        private ParticleSystem launchFlash;
        private TrailRenderer launchTrail;
        private Transform[] debris;
        private SpriteRenderer[] debrisRenderers;

        private Vector3 spriteBaseScale;
        private Vector3 previousPosition;
        private float visualSeed;
        private float flightBlend;
        private bool wasTravellingFast;
        private bool legacyLightsDisabled;

        private void Awake()
        {
            visualSeed = Random.Range(0f, 100f);
            previousPosition = transform.position;
            owner = GetComponent<Phase4FloatingRock>();

            FindAuthoredElements();
            ApplyPremiumMaterials();
            TuneAuthoredParticles();
            BuildLayeredGlow();
            BuildEnergyRings();
            BuildOrbitingDebris();
            BuildLaunchTrail();
            BuildLaunchEmbers();
            BuildLaunchFlash();
            DisableAttached2DLights();
        }

        private void FindAuthoredElements()
        {
            Transform spriteTransform = transform.Find("MagmaBombSprite");
            if (spriteTransform != null)
                bombSprite = spriteTransform.GetComponent<SpriteRenderer>();

            sparks = FindParticleSystem("BombSparks");
            heatAura = FindParticleSystem("BombHeatAura");

            if (bombSprite != null)
                spriteBaseScale = bombSprite.transform.localScale;
        }

        private void ApplyPremiumMaterials()
        {
            if (bombSprite != null && owner != null && owner.SurfaceMaterial != null)
                bombSprite.sharedMaterial = owner.SurfaceMaterial;

            Material glow = owner != null ? owner.GlowMaterial : null;
            if (glow == null) return;

            ParticleSystemRenderer sparkRenderer = sparks != null
                ? sparks.GetComponent<ParticleSystemRenderer>()
                : null;
            if (sparkRenderer != null)
                sparkRenderer.sharedMaterial = glow;

            ParticleSystemRenderer auraRenderer = heatAura != null
                ? heatAura.GetComponent<ParticleSystemRenderer>()
                : null;
            if (auraRenderer != null)
                auraRenderer.sharedMaterial = glow;
        }

        private ParticleSystem FindParticleSystem(string childName)
        {
            Transform child = transform.Find(childName);
            return child != null ? child.GetComponent<ParticleSystem>() : null;
        }

        private void TuneAuthoredParticles()
        {
            if (sparks != null)
            {
                ParticleSystem.MainModule main = sparks.main;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.62f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 3.4f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.12f);
                main.startColor = new ParticleSystem.MinMaxGradient(WhiteHot, MoltenGold);
                main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.12f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 64;

                ParticleSystem.EmissionModule emission = sparks.emission;
                emission.enabled = true;
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(16f);

                ParticleSystem.ShapeModule shape = sparks.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.5f;
                shape.radiusThickness = 0.7f;

                ParticleSystem.ColorOverLifetimeModule color = sparks.colorOverLifetime;
                color.enabled = true;
                color.color = new ParticleSystem.MinMaxGradient(CreateFireGradient(1f));

                ParticleSystem.SizeOverLifetimeModule size = sparks.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0.35f),
                    new Keyframe(0.18f, 1f),
                    new Keyframe(1f, 0f)));

                ParticleSystemRenderer renderer = sparks.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    renderer.sortingOrder = 17;
                    renderer.renderMode = ParticleSystemRenderMode.Stretch;
                    renderer.velocityScale = 0.045f;
                    renderer.lengthScale = 1.9f;
                }
            }

            if (heatAura != null)
            {
                ParticleSystem.MainModule main = heatAura.main;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.15f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.22f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.75f, 1.25f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(1f, 0.34f, 0.035f, 0.34f),
                    new Color(1f, 0.08f, 0.005f, 0.16f));
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.maxParticles = 24;

                ParticleSystem.EmissionModule emission = heatAura.emission;
                emission.enabled = true;
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(8f);

                ParticleSystem.ShapeModule shape = heatAura.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.24f;

                ParticleSystem.ColorOverLifetimeModule color = heatAura.colorOverLifetime;
                color.enabled = true;
                color.color = new ParticleSystem.MinMaxGradient(CreateHeatGradient(0.38f));

                ParticleSystem.SizeOverLifetimeModule size = heatAura.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0.58f),
                    new Keyframe(0.42f, 1f),
                    new Keyframe(1f, 1.34f)));

                ParticleSystemRenderer renderer = heatAura.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                    renderer.sortingOrder = 12;
            }
        }

        private void BuildLayeredGlow()
        {
            Material premiumGlow = owner != null ? owner.GlowMaterial : null;
            Material sparkMaterial = premiumGlow ?? GetParticleMaterial(sparks);
            Material heatMaterial = premiumGlow ?? GetParticleMaterial(heatAura) ?? sparkMaterial;

            CreateGlowLayer(
                "MoltenCoreGlow",
                sparkMaterial,
                0.68f,
                1.02f,
                new Color(1f, 0.96f, 0.68f, 1f),
                18,
                7.5f);

            CreateGlowLayer(
                "InnerMagmaCorona",
                heatMaterial,
                1.18f,
                1.72f,
                new Color(1f, 0.31f, 0.025f, 0.62f),
                15,
                4.4f);

            CreateGlowLayer(
                "OuterHeatHalo",
                heatMaterial,
                1.9f,
                2.65f,
                new Color(1f, 0.095f, 0.008f, 0.24f),
                11,
                2.5f);
        }

        private void CreateGlowLayer(
            string layerName,
            Material material,
            float minSize,
            float maxSize,
            Color color,
            int sortingOrder,
            float emissionRate)
        {
            GameObject layer = new(layerName);
            layer.transform.SetParent(transform, false);

            ParticleSystem system = layer.AddComponent<ParticleSystem>();
            // AddComponent starts a ParticleSystem immediately by default in Unity 6.
            // It must be fully stopped before changing duration or other main-module
            // values, otherwise Unity rejects the configuration while it is playing.
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.42f, 0.68f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 8;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = emissionRate;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(CreateAuraGradient(color));

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.72f),
                new Keyframe(0.45f, 1.08f),
                new Keyframe(1f, 0.86f)));

            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = sortingOrder;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            if (material != null)
                renderer.sharedMaterial = material;

            system.Play();
        }

        private void BuildEnergyRings()
        {
            Material material = owner != null ? owner.RingMaterial : null;
            if (material == null)
                material = owner != null ? owner.GlowMaterial : null;
            if (material == null) return;

            CreateEnergyRing(
                "InnerArcRing",
                material,
                1.72f,
                new Color(1f, 0.72f, 0.16f, 0.9f),
                17);

            CreateEnergyRing(
                "OuterArcRing",
                material,
                2.34f,
                new Color(1f, 0.16f, 0.015f, 0.52f),
                14);
        }

        private void CreateEnergyRing(
            string objectName,
            Material material,
            float size,
            Color color,
            int sortingOrder)
        {
            GameObject ringObject = new(objectName);
            ringObject.transform.SetParent(transform, false);

            ParticleSystem ring = ringObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ring.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 9999f;
            main.startSpeed = 0f;
            main.startSize = size;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 1;

            ParticleSystem.EmissionModule emission = ring.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = ring.shape;
            shape.enabled = false;

            ParticleSystemRenderer renderer = ring.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = sortingOrder;

            ring.Emit(1);
        }

        private void BuildOrbitingDebris()
        {
            if (bombSprite == null || bombSprite.sprite == null) return;

            GameObject orbitRoot = new("ObsidianDebrisOrbit");
            orbitRoot.transform.SetParent(transform, false);

            debris = new Transform[DebrisCount];
            debrisRenderers = new SpriteRenderer[DebrisCount];

            for (int i = 0; i < DebrisCount; i++)
            {
                GameObject shard = new($"MoltenShard_{i + 1:00}");
                shard.transform.SetParent(orbitRoot.transform, false);

                SpriteRenderer renderer = shard.AddComponent<SpriteRenderer>();
                renderer.sprite = bombSprite.sprite;
                renderer.sharedMaterial = bombSprite.sharedMaterial;
                renderer.sortingLayerID = bombSprite.sortingLayerID;
                renderer.sortingOrder = i % 2 == 0 ? 13 : 16;
                renderer.color = i % 2 == 0
                    ? new Color(0.42f, 0.17f, 0.09f, 0.9f)
                    : new Color(1f, 0.34f, 0.055f, 0.82f);

                float shardScale = 0.045f + i * 0.008f;
                shard.transform.localScale = new Vector3(shardScale, shardScale * (0.62f + i * 0.07f), 1f);

                debris[i] = shard.transform;
                debrisRenderers[i] = renderer;
            }
        }

        private void BuildLaunchTrail()
        {
            GameObject trailObject = new("WhiteHotLaunchTrail");
            trailObject.transform.SetParent(transform, false);

            launchTrail = trailObject.AddComponent<TrailRenderer>();
            launchTrail.time = 0.26f;
            launchTrail.minVertexDistance = 0.035f;
            launchTrail.widthMultiplier = 0.72f;
            launchTrail.widthCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.3f, 0.68f),
                new Keyframe(1f, 0f));
            launchTrail.colorGradient = CreateTrailGradient();
            launchTrail.textureMode = LineTextureMode.Stretch;
            launchTrail.alignment = LineAlignment.View;
            launchTrail.numCornerVertices = 4;
            launchTrail.numCapVertices = 3;
            launchTrail.sortingOrder = 10;
            launchTrail.emitting = false;

            Material material = owner != null ? owner.TrailMaterial : null;
            material ??= GetParticleMaterial(sparks) ?? GetParticleMaterial(heatAura);
            if (material != null)
                launchTrail.sharedMaterial = material;
        }

        private void BuildLaunchEmbers()
        {
            GameObject emberObject = new("LaunchEmberWake");
            emberObject.transform.SetParent(transform, false);

            launchEmbers = emberObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = launchEmbers.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.52f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.85f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.095f);
            main.startColor = new ParticleSystem.MinMaxGradient(WhiteHot, MagmaOrange);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;

            ParticleSystem.EmissionModule emission = launchEmbers.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = launchEmbers.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.24f;

            ParticleSystem.ColorOverLifetimeModule color = launchEmbers.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(CreateFireGradient(0.88f));

            ParticleSystem.SizeOverLifetimeModule size = launchEmbers.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            ParticleSystemRenderer renderer = launchEmbers.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 18;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.035f;
            renderer.lengthScale = 2.2f;

            Material material = owner != null ? owner.TrailMaterial : null;
            material ??= GetParticleMaterial(sparks);
            if (material != null)
                renderer.sharedMaterial = material;

            launchEmbers.Play();
        }

        private void BuildLaunchFlash()
        {
            Material material = owner != null ? owner.RingMaterial : null;
            if (material == null) return;

            GameObject flashObject = new("LaunchShockRing");
            flashObject.transform.SetParent(transform, false);

            launchFlash = flashObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = launchFlash.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.32f;
            main.startSpeed = 0f;
            main.startSize = 1.1f;
            main.startColor = new Color(1f, 0.78f, 0.2f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 3;

            ParticleSystem.EmissionModule emission = launchFlash.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = launchFlash.shape;
            shape.enabled = false;

            ParticleSystem.SizeOverLifetimeModule size = launchFlash.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.55f),
                new Keyframe(0.18f, 1.15f),
                new Keyframe(1f, 3.25f)));

            ParticleSystem.ColorOverLifetimeModule color = launchFlash.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(WhiteHot, 0f),
                    new GradientColorKey(MoltenGold, 0.35f),
                    new GradientColorKey(MagmaOrange, 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.08f),
                    new GradientAlphaKey(0f, 1f)
                }
            });

            ParticleSystemRenderer renderer = launchFlash.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 20;
        }

        private void DisableAttached2DLights()
        {
            Light2D[] attachedLights = GetComponentsInChildren<Light2D>(true);
            foreach (Light2D light in attachedLights)
                if (light != null) light.enabled = false;

            legacyLightsDisabled = true;
        }

        private void Update()
        {
            // Also clears lights created by the preceding version when Unity
            // recompiles scripts during an active Play Mode session.
            if (!legacyLightsDisabled)
                DisableAttached2DLights();

            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            float speed = Vector3.Distance(transform.position, previousPosition) / deltaTime;
            previousPosition = transform.position;

            bool travellingFast = speed > 2.2f;
            flightBlend = Mathf.MoveTowards(flightBlend, travellingFast ? 1f : 0f, deltaTime * 7f);

            if (travellingFast && !wasTravellingFast)
                EmitLaunchAccent();
            wasTravellingFast = travellingFast;

            AnimateBombBody();
            AnimateDebris(speed);
            UpdateFlightEffects(speed);
        }

        private void AnimateBombBody()
        {
            if (bombSprite == null) return;

            float slowPulse = (Mathf.Sin((Time.time + visualSeed) * 5.4f) + 1f) * 0.5f;
            float hotFlicker = Mathf.PerlinNoise(visualSeed, Time.time * 8.5f);
            float pulseScale = 0.965f + slowPulse * 0.075f + hotFlicker * 0.018f + flightBlend * 0.035f;

            bombSprite.transform.localScale = spriteBaseScale * pulseScale;
            bombSprite.color = Color.Lerp(Color.white, new Color(1f, 0.76f, 0.42f, 1f), slowPulse * 0.24f);

        }

        private void EmitLaunchAccent()
        {
            if (launchFlash != null)
                launchFlash.Emit(1);

            if (launchEmbers != null)
                launchEmbers.Emit(22);
        }

        private void AnimateDebris(float speed)
        {
            if (debris == null) return;

            float angularSpeed = Mathf.Lerp(52f, 170f, flightBlend) + Mathf.Min(speed * 1.5f, 36f);
            float time = Time.time + visualSeed;

            for (int i = 0; i < debris.Length; i++)
            {
                if (debris[i] == null) continue;

                float normalizedIndex = i / (float)debris.Length;
                float angle = time * angularSpeed + normalizedIndex * 360f;
                float radians = angle * Mathf.Deg2Rad;
                float radius = 0.68f + i * 0.055f + Mathf.Sin(time * 3.2f + i) * 0.055f;
                float squash = 0.58f + (i % 2) * 0.13f;

                debris[i].localPosition = new Vector3(
                    Mathf.Cos(radians) * radius,
                    Mathf.Sin(radians) * radius * squash,
                    0f);
                debris[i].localRotation = Quaternion.Euler(0f, 0f, -angle * (1.15f + i * 0.09f));

                if (debrisRenderers != null && debrisRenderers[i] != null)
                {
                    float shimmer = (Mathf.Sin(time * 7f + i * 1.7f) + 1f) * 0.5f;
                    Color dark = new(0.28f, 0.085f, 0.035f, 0.72f);
                    Color hot = new(1f, 0.31f, 0.035f, 0.94f);
                    debrisRenderers[i].color = Color.Lerp(dark, hot, shimmer * 0.62f + flightBlend * 0.2f);
                }
            }
        }

        private void UpdateFlightEffects(float speed)
        {
            if (launchTrail != null)
            {
                launchTrail.emitting = flightBlend > 0.08f;
                launchTrail.widthMultiplier = Mathf.Lerp(0.28f, 0.76f, flightBlend);
                launchTrail.time = Mathf.Lerp(0.12f, 0.29f, flightBlend);
            }

            if (launchEmbers != null)
            {
                ParticleSystem.EmissionModule emission = launchEmbers.emission;
                emission.rateOverTime = Mathf.Lerp(0f, 44f + Mathf.Min(speed, 20f), flightBlend);
            }

            if (sparks != null)
            {
                ParticleSystem.EmissionModule emission = sparks.emission;
                emission.rateOverTime = Mathf.Lerp(16f, 36f, flightBlend);
            }
        }

        private static Material GetParticleMaterial(ParticleSystem system)
        {
            if (system == null) return null;
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            return renderer != null ? renderer.sharedMaterial : null;
        }

        private static Gradient CreateFireGradient(float alpha)
        {
            Gradient gradient = new();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(WhiteHot, 0f),
                    new GradientColorKey(MoltenGold, 0.28f),
                    new GradientColorKey(MagmaOrange, 0.67f),
                    new GradientColorKey(EmberRed, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(alpha, 0.09f),
                    new GradientAlphaKey(alpha * 0.82f, 0.56f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private static Gradient CreateHeatGradient(float alpha)
        {
            Gradient gradient = new();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(MoltenGold, 0f),
                    new GradientColorKey(MagmaOrange, 0.52f),
                    new GradientColorKey(EmberRed, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(alpha, 0.18f),
                    new GradientAlphaKey(alpha * 0.55f, 0.62f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private static Gradient CreateAuraGradient(Color color)
        {
            Gradient gradient = new();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(Color.Lerp(color, MagmaOrange, 0.4f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(color.a, 0.15f),
                    new GradientAlphaKey(color.a * 0.8f, 0.68f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private static Gradient CreateTrailGradient()
        {
            Gradient gradient = new();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.98f, 0.76f), 0f),
                    new GradientColorKey(MoltenGold, 0.22f),
                    new GradientColorKey(MagmaOrange, 0.64f),
                    new GradientColorKey(EmberRed, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.96f, 0f),
                    new GradientAlphaKey(0.72f, 0.34f),
                    new GradientAlphaKey(0.2f, 0.78f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }
    }
}
