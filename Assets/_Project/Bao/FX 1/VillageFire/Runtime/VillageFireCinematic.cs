using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace DATN.Duy.VillageFire
{
    /// <summary>
    /// Legacy runtime installer retained only for source history. Village roof fire is now
    /// authored as a placeable prefab and must be positioned manually in the scene.
    /// </summary>
    internal static class VillageFireCinematicBootstrap
    {
        private const string ControllerName = "__Duy_VillageFireCinematic";
        private static bool _hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _hooked = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterSceneLoad()
        {
            // Deliberately empty: do not create particle systems at runtime.
            // Use VFX_VillageRoofFire.prefab under each NhaChay object instead.
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InstallForScene(scene);
        }

        private static void InstallForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            // This VFX is intentionally scoped to the village cutscene scene.
            GameObject villageRoot = scene.GetRootGameObjects()
                .FirstOrDefault(root => string.Equals(root.name, "Nha", StringComparison.Ordinal));
            if (villageRoot == null)
                return;

            if (scene.GetRootGameObjects().Any(root =>
                    string.Equals(root.name, ControllerName, StringComparison.Ordinal)))
                return;

            GameObject host = new GameObject(ControllerName)
            {
                hideFlags = HideFlags.DontSave
            };
            SceneManager.MoveGameObjectToScene(host, scene);
            VillageFireCinematicController controller = host.AddComponent<VillageFireCinematicController>();
            controller.Initialize(villageRoot);
        }
    }

    /// <summary>
    /// Builds a small, layered particle rig for each NhaChay SpriteRenderer.
    /// The rig is parented to the burned-house renderer, so it inherits the exact
    /// world Z and transform used by the map while remaining independent of the scene YAML.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-400)]
    public sealed class VillageFireCinematicController : MonoBehaviour
    {
        // The generated v2 atlas is optional while artists iterate.  Falling back keeps
        // the scene playable if the new image has not been imported yet.
        private const string PainterlyAtlasResourceName = "T_VillageFire_PainterlyAtlas_v2";
        private const string LegacyAtlasResourceName = "T_VillageFire_RoofAtlas";
        private const string FlameShaderName = "DATN/Duy/VillageFire/Flame";
        private const string ProceduralShaderName = "DATN/Duy/VillageFire/ProceduralParticle";

        private readonly List<ParticleSystem> _systems = new List<ParticleSystem>(96);
        private readonly List<Material> _runtimeMaterials = new List<Material>(8);

        private GameObject _villageRoot;
        private bool _playing;
        private int _timelineSettleFrames;
        private Material _flameMaterial;
        private Material _flameAdditiveMaterial;
        private Material _glowMaterial;
        private Material _smokeMaterial;
        private Material _emberMaterial;

        public void Initialize(GameObject villageRoot)
        {
            if (villageRoot == null)
            {
                enabled = false;
                return;
            }

            _villageRoot = villageRoot;
            BuildMaterials();
            if (_flameMaterial == null || _glowMaterial == null)
            {
                Debug.LogError("[VillageFire] Required shaders or atlas could not be loaded.");
                enabled = false;
                return;
            }

            BuildVillageRigs();
            // The serialized scene state has Nha active before Timeline evaluates its
            // Activation Track. Keep the newly-created systems cold for two frames so
            // they do not all prewarm at scene start, then mirror Timeline normally.
            SetPlaying(false);
            _timelineSettleFrames = 2;
        }

        private void Update()
        {
            if (_villageRoot == null)
                return;

            if (_timelineSettleFrames > 0)
            {
                _timelineSettleFrames--;
                if (_timelineSettleFrames > 0)
                    return;
            }

            bool shouldPlay = _villageRoot.activeInHierarchy;
            bool hasStoppedWhileActive = shouldPlay && _systems.Any(system =>
                system != null && !system.isPlaying && system.gameObject.activeInHierarchy);
            if (shouldPlay != _playing || hasStoppedWhileActive)
                SetPlaying(shouldPlay);
        }

        private void BuildMaterials()
        {
            Texture2D atlas = Resources.Load<Texture2D>(PainterlyAtlasResourceName) ??
                              Resources.Load<Texture2D>(LegacyAtlasResourceName);
            Shader flameShader = Resources.Load<Shader>("Shaders/VillageFireFlame") ?? Shader.Find(FlameShaderName);
            Shader proceduralShader = Resources.Load<Shader>("Shaders/VillageFireProcedural") ?? Shader.Find(ProceduralShaderName);
            if (atlas == null || flameShader == null || proceduralShader == null)
                return;

            _flameMaterial = CreateMaterial(flameShader, "VillageFire_FlameBody");
            _flameMaterial.SetTexture("_MainTex", atlas);
            _flameMaterial.SetColor("_Tint", new Color(1f, 0.88f, 0.68f, 0.94f));
            _flameMaterial.SetColor("_HotColor", new Color(1f, 0.98f, 0.82f, 0.34f));
            _flameMaterial.SetColor("_EdgeColor", new Color(0.84f, 0.16f, 0.018f, 0.76f));
            _flameMaterial.SetFloat("_Emission", 0.86f);
            _flameMaterial.SetFloat("_FlickerSpeed", 2.6f);
            _flameMaterial.SetFloat("_FlickerAmount", 0.0065f);
            _flameMaterial.SetFloat("_EdgeStrength", 0.24f);
            _flameMaterial.SetFloat("_SoftClip", 0.075f);
            _flameMaterial.SetVector("_AtlasTiles", new Vector4(4f, 2f, 0f, 0f));
            SetBlend(_flameMaterial, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha);

            _flameAdditiveMaterial = CreateMaterial(flameShader, "VillageFire_FlameLight");
            _flameAdditiveMaterial.SetTexture("_MainTex", atlas);
            _flameAdditiveMaterial.SetColor("_Tint", new Color(1f, 0.64f, 0.24f, 0.16f));
            _flameAdditiveMaterial.SetColor("_HotColor", new Color(1f, 0.96f, 0.76f, 0.22f));
            _flameAdditiveMaterial.SetColor("_EdgeColor", new Color(0.96f, 0.24f, 0.025f, 0.20f));
            _flameAdditiveMaterial.SetFloat("_Emission", 0.42f);
            _flameAdditiveMaterial.SetFloat("_FlickerSpeed", 3.35f);
            _flameAdditiveMaterial.SetFloat("_FlickerAmount", 0.009f);
            _flameAdditiveMaterial.SetFloat("_EdgeStrength", 0.12f);
            _flameAdditiveMaterial.SetFloat("_SoftClip", 0.10f);
            _flameAdditiveMaterial.SetVector("_AtlasTiles", new Vector4(4f, 2f, 0f, 0f));
            SetBlend(_flameAdditiveMaterial, BlendMode.SrcAlpha, BlendMode.One);

            _glowMaterial = CreateProceduralMaterial(proceduralShader, "VillageFire_RoofGlow",
                mode: 0f,
                colorA: new Color(1f, 0.23f, 0.035f, 0.38f),
                colorB: new Color(0.75f, 0.12f, 0.015f, 0.01f),
                emission: 0.52f,
                softness: 3.9f,
                noiseScale: 3.2f,
                source: BlendMode.SrcAlpha,
                destination: BlendMode.One);

            _smokeMaterial = CreateProceduralMaterial(proceduralShader, "VillageFire_SootMist",
                mode: 1f,
                colorA: new Color(0.13f, 0.11f, 0.10f, 0.36f),
                colorB: new Color(0.035f, 0.03f, 0.028f, 0.01f),
                emission: 0.55f,
                softness: 1.9f,
                noiseScale: 3.8f,
                source: BlendMode.SrcAlpha,
                destination: BlendMode.OneMinusSrcAlpha);

            _emberMaterial = CreateProceduralMaterial(proceduralShader, "VillageFire_Ember",
                mode: 2f,
                colorA: new Color(1f, 0.58f, 0.13f, 0.76f),
                colorB: new Color(0.92f, 0.10f, 0.015f, 0.03f),
                emission: 0.92f,
                softness: 3.1f,
                noiseScale: 1f,
                source: BlendMode.SrcAlpha,
                destination: BlendMode.One);
        }

        private Material CreateProceduralMaterial(
            Shader shader,
            string materialName,
            float mode,
            Color colorA,
            Color colorB,
            float emission,
            float softness,
            float noiseScale,
            BlendMode source,
            BlendMode destination)
        {
            Material material = CreateMaterial(shader, materialName);
            material.SetFloat("_Mode", mode);
            material.SetColor("_ColorA", colorA);
            material.SetColor("_ColorB", colorB);
            material.SetFloat("_Emission", emission);
            material.SetFloat("_Softness", softness);
            material.SetFloat("_NoiseScale", noiseScale);
            SetBlend(material, source, destination);
            return material;
        }

        private Material CreateMaterial(Shader shader, string materialName)
        {
            Material material = new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.DontSave
            };
            _runtimeMaterials.Add(material);
            return material;
        }

        private static void SetBlend(Material material, BlendMode source, BlendMode destination)
        {
            material.SetInt("_SrcBlend", (int)source);
            material.SetInt("_DstBlend", (int)destination);
        }

        private void BuildVillageRigs()
        {
            SpriteRenderer[] burnedHouseRenderers = _villageRoot
                .GetComponentsInChildren<SpriteRenderer>(true)
                .Where(renderer => renderer.sprite != null &&
                                   renderer.gameObject.name.StartsWith("NhaChay", StringComparison.OrdinalIgnoreCase))
                .OrderBy(renderer => renderer.bounds.center.x)
                .ToArray();

            for (int index = 0; index < burnedHouseRenderers.Length; index++)
                BuildHouseRig(burnedHouseRenderers[index], index);

            if (burnedHouseRenderers.Length == 0)
                Debug.LogWarning("[VillageFire] No NhaChay SpriteRenderers were found under Nha.");
            else
                Debug.Log($"[VillageFire] Built {burnedHouseRenderers.Length} roof fire rigs under Nha.");
        }

        private void BuildHouseRig(SpriteRenderer houseRenderer, int index)
        {
            Bounds spriteBounds = houseRenderer.sprite.bounds;
            float width = Mathf.Max(spriteBounds.size.x, 1f);
            float height = Mathf.Max(spriteBounds.size.y, 1f);
            float roofY = spriteBounds.min.y + height * 0.58f;
            float roofSpan = width * 0.45f;
            float intensity = 0.86f + Deterministic01(index * 17 + 3) * 0.28f;
            int baseOrder = houseRenderer.sortingOrder;

            GameObject rig = new GameObject($"VFX_FireRig_{index:00}")
            {
                hideFlags = HideFlags.DontSave
            };
            rig.transform.SetParent(houseRenderer.transform, false);
            rig.transform.localPosition = new Vector3(0f, 0f, -0.035f);

            // The glow is broad and sits just behind the visible flame tongues.
            CreateGlowSystem(rig.transform, new Vector3(0f, roofY - height * 0.03f, 0f),
                width, height, intensity, houseRenderer, baseOrder - 1, index);

            // Three overlapping body emitters create a continuous roofline without a flat strip.
            CreateFlameSystem(rig.transform, "Fire_Body_Center",
                new Vector3(0f, roofY, 0f), roofSpan, height, intensity, houseRenderer, baseOrder + 1, index, false);
            CreateFlameSystem(rig.transform, "Fire_Body_Left",
                new Vector3(-roofSpan * 0.42f, roofY - height * 0.035f, -0.01f), roofSpan * 0.72f,
                height * 0.82f, intensity * 0.78f, houseRenderer, baseOrder + 1, index + 19, false);
            CreateFlameSystem(rig.transform, "Fire_Body_Right",
                new Vector3(roofSpan * 0.42f, roofY - height * 0.02f, -0.012f), roofSpan * 0.72f,
                height * 0.86f, intensity * 0.82f, houseRenderer, baseOrder + 1, index + 31, false);

            // Small additive tongues and sparks sell the hand-painted fire at map scale.
            CreateFlameSystem(rig.transform, "Fire_Licks",
                new Vector3(0f, roofY + height * 0.025f, -0.02f), roofSpan * 1.05f,
                height * 0.52f, intensity * 0.72f, houseRenderer, baseOrder + 2, index + 43, true);
            CreateEmberSystem(rig.transform, new Vector3(0f, roofY + height * 0.10f, -0.025f),
                roofSpan, height, intensity, houseRenderer, baseOrder + 3, index + 59);
            CreateSmokeSystem(rig.transform, new Vector3(0f, roofY + height * 0.19f, 0.01f),
                roofSpan * 0.9f, height, intensity, houseRenderer, baseOrder - 2, index + 71);
        }

        private void CreateFlameSystem(
            Transform parent,
            string name,
            Vector3 localPosition,
            float span,
            float height,
            float intensity,
            SpriteRenderer sourceRenderer,
            int sortingOrder,
            int seed,
            bool additive)
        {
            GameObject go = CreateChild(parent, name, localPosition);
            ParticleSystem particleSystem = go.AddComponent<ParticleSystem>();
            PrepareParticleSystem(particleSystem);
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            Material material = additive ? _flameAdditiveMaterial : _flameMaterial;
            ConfigureRenderer(renderer, sourceRenderer, sortingOrder, material);

            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = true;
            main.playOnAwake = false;
            main.prewarm = true;
            main.duration = additive ? 1.06f : 1.42f;
            main.startLifetime = additive
                ? new ParticleSystem.MinMaxCurve(0.58f, 1.02f)
                : new ParticleSystem.MinMaxCurve(1.05f, 1.46f);
            main.startSpeed = additive
                ? new ParticleSystem.MinMaxCurve(0.008f, 0.055f)
                : new ParticleSystem.MinMaxCurve(0.012f, 0.068f);
            main.startSize = additive
                ? new ParticleSystem.MinMaxCurve(height * 0.13f, height * 0.21f)
                : new ParticleSystem.MinMaxCurve(height * 0.27f, height * 0.39f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.045f, 0.045f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                additive ? new Color(1f, 0.66f, 0.32f, 0.40f) : new Color(1f, 0.84f, 0.57f, 0.84f));
            main.maxParticles = additive ? 5 : 8;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.cullingMode = ParticleSystemCullingMode.Automatic;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(
                (additive ? 1.35f : 1.10f) * intensity);

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(Mathf.Max(span, 0.3f), Mathf.Max(height * 0.035f, 0.025f), 0.02f);

            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-height * 0.035f, height * 0.035f);
            velocity.y = new ParticleSystem.MinMaxCurve(height * 0.018f, height * 0.075f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            ParticleSystem.ColorOverLifetimeModule color = particleSystem.colorOverLifetime;
            color.enabled = true;
            color.color = FlameFade(additive ? 0.56f : 0.92f);

            ParticleSystem.SizeOverLifetimeModule size = particleSystem.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, FlameSize(additive));

            ConfigureAtlas(particleSystem);
            particleSystem.useAutoRandomSeed = false;
            particleSystem.randomSeed = (uint)(seed * 7919 + 17);
            go.SetActive(true);
            _systems.Add(particleSystem);
        }

        private void CreateGlowSystem(
            Transform parent,
            Vector3 localPosition,
            float width,
            float height,
            float intensity,
            SpriteRenderer sourceRenderer,
            int sortingOrder,
            int seed)
        {
            GameObject go = CreateChild(parent, "Fire_RoofGlow", localPosition);
            ParticleSystem particleSystem = go.AddComponent<ParticleSystem>();
            PrepareParticleSystem(particleSystem);
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            ConfigureRenderer(renderer, sourceRenderer, sortingOrder, _glowMaterial);

            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = true;
            main.playOnAwake = false;
            main.prewarm = true;
            main.duration = 2.8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.9f, 3.1f);
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(width * 0.43f, width * 0.61f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(height * 0.14f, height * 0.23f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(0.01f, 0.01f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.29f, 0.05f, 0.16f));
            main.maxParticles = 3;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(0.34f * intensity);
            ParticleSystem.ColorOverLifetimeModule color = particleSystem.colorOverLifetime;
            color.enabled = true;
            color.color = GlowFade();
            ParticleSystem.NoiseModule noise = particleSystem.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.045f);
            noise.frequency = 0.24f;
            noise.scrollSpeed = 0.13f;

            particleSystem.useAutoRandomSeed = false;
            particleSystem.randomSeed = (uint)(seed * 6151 + 101);
            go.SetActive(true);
            _systems.Add(particleSystem);
        }

        private void CreateSmokeSystem(
            Transform parent,
            Vector3 localPosition,
            float span,
            float height,
            float intensity,
            SpriteRenderer sourceRenderer,
            int sortingOrder,
            int seed)
        {
            GameObject go = CreateChild(parent, "Fire_SootMist", localPosition);
            ParticleSystem particleSystem = go.AddComponent<ParticleSystem>();
            PrepareParticleSystem(particleSystem);
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            ConfigureRenderer(renderer, sourceRenderer, sortingOrder, _smokeMaterial);

            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = true;
            main.playOnAwake = false;
            main.prewarm = false;
            main.duration = 4.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.8f, 5.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(height * 0.025f, height * 0.075f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(height * 0.18f, height * 0.30f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(height * 0.28f, height * 0.46f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(0.01f, 0.01f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.16f, 0.14f, 0.125f, 0.22f));
            main.maxParticles = 3;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(0.22f * intensity);
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(Mathf.Max(span, 0.3f), height * 0.05f, 0.02f);

            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-height * 0.028f, height * 0.028f);
            velocity.y = new ParticleSystem.MinMaxCurve(height * 0.026f, height * 0.085f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            ParticleSystem.ColorOverLifetimeModule color = particleSystem.colorOverLifetime;
            color.enabled = true;
            color.color = SmokeFade();
            ParticleSystem.SizeOverLifetimeModule size = particleSystem.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, SmokeSize());

            ParticleSystem.NoiseModule noise = particleSystem.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.10f);
            noise.frequency = 0.18f;
            noise.scrollSpeed = 0.075f;
            noise.octaveCount = 2;

            particleSystem.useAutoRandomSeed = false;
            particleSystem.randomSeed = (uint)(seed * 6151 + 211);
            go.SetActive(true);
            _systems.Add(particleSystem);
        }

        private void CreateEmberSystem(
            Transform parent,
            Vector3 localPosition,
            float span,
            float height,
            float intensity,
            SpriteRenderer sourceRenderer,
            int sortingOrder,
            int seed)
        {
            GameObject go = CreateChild(parent, "Fire_Embers", localPosition);
            ParticleSystem particleSystem = go.AddComponent<ParticleSystem>();
            PrepareParticleSystem(particleSystem);
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            ConfigureRenderer(renderer, sourceRenderer, sortingOrder, _emberMaterial);

            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = true;
            main.playOnAwake = false;
            main.prewarm = false;
            main.duration = 1.9f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 1.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(height * 0.13f, height * 0.31f);
            main.startSize = new ParticleSystem.MinMaxCurve(height * 0.013f, height * 0.031f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.57f, 0.15f, 0.72f));
            main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.08f);
            main.maxParticles = 8;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(0.68f * intensity);
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(Mathf.Max(span, 0.3f), height * 0.04f, 0.02f);

            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-height * 0.12f, height * 0.12f);
            velocity.y = new ParticleSystem.MinMaxCurve(height * 0.10f, height * 0.30f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            ParticleSystem.ColorOverLifetimeModule color = particleSystem.colorOverLifetime;
            color.enabled = true;
            color.color = EmberFade();

            particleSystem.useAutoRandomSeed = false;
            particleSystem.randomSeed = (uint)(seed * 7919 + 307);
            go.SetActive(true);
            _systems.Add(particleSystem);
        }

        private static GameObject CreateChild(Transform parent, string name, Vector3 localPosition)
        {
            GameObject child = new GameObject(name)
            {
                hideFlags = HideFlags.DontSave
            };
            // Keep the object inactive while ParticleSystem modules are configured.
            // This prevents Unity's implicit PlayOnAwake from racing the setup code.
            child.SetActive(false);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child;
        }

        private static void PrepareParticleSystem(ParticleSystem particleSystem)
        {
            // AddComponent can schedule PlayOnAwake before the rest of the module is
            // configured. Stop it immediately so duration, curves and random seed are
            // assigned while the system is cold and Unity emits no module warnings.
            ParticleSystem.MainModule main = particleSystem.main;
            main.playOnAwake = false;
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static void ConfigureRenderer(
            ParticleSystemRenderer renderer,
            SpriteRenderer sourceRenderer,
            int sortingOrder,
            Material material)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.material = material;
            renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            renderer.sortingOrder = sortingOrder;
            renderer.minParticleSize = 0.001f;
            renderer.maxParticleSize = 0.5f;
        }

        private static void ConfigureAtlas(ParticleSystem particleSystem)
        {
            ParticleSystem.TextureSheetAnimationModule sheet = particleSystem.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 4;
            sheet.numTilesY = 2;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.cycleCount = 1;
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.875f);
            AnimationCurve frameCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, frameCurve);
        }

        private static AnimationCurve FlameSize(bool additive)
        {
            return new AnimationCurve(
                new Keyframe(0f, additive ? 0.58f : 0.62f),
                new Keyframe(0.24f, 0.92f),
                new Keyframe(0.70f, additive ? 0.80f : 0.86f),
                new Keyframe(1f, 0.18f));
        }

        private static Gradient FlameFade(float peakAlpha)
        {
            return MakeGradient(
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f),
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(peakAlpha * 0.72f, 0.16f),
                new GradientAlphaKey(peakAlpha, 0.50f),
                new GradientAlphaKey(peakAlpha * 0.74f, 0.78f),
                new GradientAlphaKey(0f, 1f));
        }

        private static Gradient GlowFade()
        {
            return MakeGradient(
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0.96f, 0.29f, 0.045f), 1f),
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.32f, 0.28f),
                new GradientAlphaKey(0.19f, 0.62f),
                new GradientAlphaKey(0f, 1f));
        }

        private static Gradient SmokeFade()
        {
            return MakeGradient(
                new GradientColorKey(new Color(0.18f, 0.15f, 0.13f), 0f),
                new GradientColorKey(new Color(0.08f, 0.065f, 0.055f), 1f),
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.22f, 0.22f),
                new GradientAlphaKey(0.10f, 0.62f),
                new GradientAlphaKey(0f, 1f));
        }

        private static AnimationCurve SmokeSize()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0.48f),
                new Keyframe(0.38f, 0.78f),
                new Keyframe(1f, 1.18f));
        }

        private static Gradient EmberFade()
        {
            return MakeGradient(
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(1f, 0.12f, 0.01f), 1f),
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.74f, 0.14f),
                new GradientAlphaKey(0.52f, 0.58f),
                new GradientAlphaKey(0f, 1f));
        }

        private static Gradient MakeGradient(
            GradientColorKey colorA,
            GradientColorKey colorB,
            params GradientAlphaKey[] alphaKeys)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(new[] { colorA, colorB }, alphaKeys);
            return gradient;
        }

        private static float Deterministic01(int value)
        {
            unchecked
            {
                uint x = (uint)value;
                x ^= x * 0x3d20adea;
                x ^= x >> 16;
                return (x & 0x00ffffff) / 16777215f;
            }
        }

        private void SetPlaying(bool shouldPlay)
        {
            _playing = shouldPlay;
            for (int index = 0; index < _systems.Count; index++)
            {
                ParticleSystem particleSystem = _systems[index];
                if (particleSystem == null)
                    continue;

                if (shouldPlay)
                    particleSystem.Play(true);
                else
                    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void OnDestroy()
        {
            for (int index = 0; index < _runtimeMaterials.Count; index++)
            {
                if (_runtimeMaterials[index] != null)
                    Destroy(_runtimeMaterials[index]);
            }
            _runtimeMaterials.Clear();
            _systems.Clear();
        }
    }
}
