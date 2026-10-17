using System;
using System.IO;
using System.Reflection;
using Bao.FX.AcidLake;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Bao.FX.AcidLake.Editor
{
    public static class MapHangAcidLakeSetup
    {
        private const string RootDirectory = "Assets/_Project/Bao/FX/AcidLake";
        private const string MaterialDirectory = RootDirectory + "/Materials";
        private const string PrefabDirectory = RootDirectory + "/Prefabs";
        private const string SurfaceMaterialPath = MaterialDirectory + "/M_AcidLakeSurface.mat";
        private const string AlphaMaterialPath = MaterialDirectory + "/M_AcidLakeParticleAlpha.mat";
        private const string AdditiveMaterialPath = MaterialDirectory + "/M_AcidLakeParticleAdd.mat";
        private const string PrefabPath = PrefabDirectory + "/PF_MapHangAcidLakeVFX.prefab";
        private const string SmokeTexturePath = "Assets/_Project/Bao/FX/DemonicMistAura/Textures/T_DemonicMist_SoftSmoke.png";
        private const string GlowTexturePath = "Assets/_Project/Bao/FX/TempleWard/T_SoftGlow.png";
        private const string SmallSurfaceBubbleTemplateName = "Particle Bubbles (1) Variant";
        private const string LargeSurfaceBubbleTemplateName = "Particle Bubbles (1) Variant (1)";
        private const string ActivitySortingLayer = "1";

        [MenuItem("Tools/Bao/Setup MapHang Acid Lake VFX")]
        public static void Setup()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[AcidLake] Exit Play Mode before applying the scene VFX.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.name.StartsWith("MapHang", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("[AcidLake] Open MapHang before running setup.");
                return;
            }

            WaterDrain lake = ResolvePuzzleWiredLake();
            if (lake == null)
            {
                Debug.LogError("[AcidLake] Could not resolve an active WaterDrain from PuzzleManager.waterSystems.");
                return;
            }

            SpriteRenderer lakeRenderer = lake.GetComponent<SpriteRenderer>();
            BoxCollider2D trigger = lake.GetComponent<BoxCollider2D>();
            if (lakeRenderer == null || trigger == null || !trigger.isTrigger)
            {
                Debug.LogError("[AcidLake] Target must retain its SpriteRenderer and root trigger BoxCollider2D.", lake);
                return;
            }

            EnsureFolders();
            Shader surfaceShader = Shader.Find("Bao/Acid Lake Surface");
            Shader alphaShader = Shader.Find("Bao/Acid Lake Particle Alpha");
            Shader additiveShader = Shader.Find("Bao/Acid Lake Particle Additive");
            if (surfaceShader == null || alphaShader == null || additiveShader == null)
            {
                Debug.LogError("[AcidLake] Acid Lake shaders are not compiled yet. Wait for Unity to finish compiling.");
                return;
            }

            Texture2D smoke = AssetDatabase.LoadAssetAtPath<Texture2D>(SmokeTexturePath);
            Texture2D glow = AssetDatabase.LoadAssetAtPath<Texture2D>(GlowTexturePath);
            if (smoke == null || glow == null)
            {
                Debug.LogError("[AcidLake] Required Bao particle textures are missing.");
                return;
            }

            Material surfaceMaterial = EnsureMaterial(SurfaceMaterialPath, surfaceShader, null);
            Material alphaMaterial = EnsureMaterial(AlphaMaterialPath, alphaShader, smoke);
            Material additiveMaterial = EnsureMaterial(AdditiveMaterialPath, additiveShader, glow);
            if (surfaceMaterial == null || alphaMaterial == null || additiveMaterial == null) return;

            if (!HasBubbleTemplate(lake.transform, SmallSurfaceBubbleTemplateName) || !HasBubbleTemplate(lake.transform, LargeSurfaceBubbleTemplateName))
            {
                Debug.LogError("[AcidLake] Surface bubble templates are missing from the puzzle-wired Nước 1. Existing AcidLakeVFX was not rebuilt.", lake);
                return;
            }

            Undo.RecordObject(lakeRenderer, "Apply Acid Lake Surface Material");
            lakeRenderer.sharedMaterial = surfaceMaterial;

            Transform oldRoot = lake.transform.Find("AcidLakeVFX");
            if (oldRoot != null) Undo.DestroyObjectImmediate(oldRoot.gameObject);

            GameObject vfxRoot = new("AcidLakeVFX");
            Undo.RegisterCreatedObjectUndo(vfxRoot, "Create Acid Lake VFX");
            vfxRoot.transform.SetParent(lake.transform, false);
            vfxRoot.transform.localPosition = Vector3.zero;
            Vector3 lossy = lake.transform.lossyScale;
            vfxRoot.transform.localScale = new Vector3(
                1f / Mathf.Max(Mathf.Abs(lossy.x), 0.0001f),
                1f / Mathf.Max(Mathf.Abs(lossy.y), 0.0001f),
                1f / Mathf.Max(Mathf.Abs(lossy.z), 0.0001f));

            SpriteRenderer accent = MakeLayer(vfxRoot.transform, "SurfaceAccent", lakeRenderer.sprite, surfaceMaterial, lakeRenderer.sortingLayerName, lakeRenderer.sortingOrder + 1, new Color(1f, 1f, 1f, 0.42f));
            SpriteRenderer rim = MakeLayer(vfxRoot.transform, "CorrosiveRim", lakeRenderer.sprite, surfaceMaterial, lakeRenderer.sortingLayerName, lakeRenderer.sortingOrder + 2, new Color(1f, 1f, 1f, 0.6f));
            accent.transform.localScale = lossy;
            rim.transform.localScale = lossy;

            ParticleSystem simmer = MakeTemplateBubbleSystem(lake.transform, vfxRoot.transform, SmallSurfaceBubbleTemplateName, additiveMaterial, "PS_SimmerBubbles", 0);
            ConfigureSurfaceBubbles(simmer, 15f, 38, new Vector2(0.16f, 0.34f), new Vector2(0.72f, 1.25f), 32f, -0.42f, new Color(0.24f, 1f, 0.05f, 0.88f), 0.19f);

            ParticleSystem large = MakeTemplateBubbleSystem(lake.transform, vfxRoot.transform, LargeSurfaceBubbleTemplateName, additiveMaterial, "PS_LargeBubbles", 1);
            ConfigureSurfaceBubbles(large, 2.1f, 16, new Vector2(0.48f, 0.94f), new Vector2(1.05f, 1.75f), 29f, -0.18f, new Color(0.78f, 1f, 0.1f, 0.96f), 0.12f);

            ParticleSystem mist = MakeParticleSystem(vfxRoot.transform, "PS_ToxicMist", alphaMaterial, ActivitySortingLayer, 2);
            ConfigureMist(mist, 3.5f, 24, 32f, new Color(0.22f, 0.82f, 0.035f, 0.55f));

            ParticleSystem pops = MakeParticleSystem(vfxRoot.transform, "PS_RarePops", additiveMaterial, ActivitySortingLayer, 3);
            ConfigurePops(pops, 16, new Color(0.78f, 1f, 0.1f, 1f));

            ParticleSystem motes = MakeParticleSystem(vfxRoot.transform, "PS_AcidMotes", additiveMaterial, ActivitySortingLayer, 4);
            ConfigureMotes(motes, 1.2f, 20, 30f, new Color(0.74f, 1f, 0.08f, 0.82f));

            Light2D light = lake.GetComponentInChildren<Light2D>(true);
            MapHangAcidLakeVfxController controller = Undo.AddComponent<MapHangAcidLakeVfxController>(vfxRoot);
            AssignController(controller, lakeRenderer, accent, rim, light, simmer, large, mist, pops, motes);
            DisableLegacyController(lake.transform);
            SavePrefabTemplate(vfxRoot);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AcidLake] Surface-bubble setup complete on '{lake.name}'. Cloned both requested templates into 5 acid particle systems (cap 114), activity on sorting layer '{ActivitySortingLayer}'.", lake);
        }

        private static WaterDrain ResolvePuzzleWiredLake()
        {
            FieldInfo waterSystems = typeof(PuzzleManager).GetField("waterSystems", BindingFlags.Instance | BindingFlags.NonPublic);
            if (waterSystems == null) return null;
            foreach (PuzzleManager manager in Resources.FindObjectsOfTypeAll<PuzzleManager>())
            {
                if (manager.gameObject.scene != SceneManager.GetActiveScene()) continue;
                if (waterSystems.GetValue(manager) is not WaterDrain[] drains) continue;
                foreach (WaterDrain drain in drains)
                    if (drain != null && drain.gameObject.activeInHierarchy) return drain;
            }
            return null;
        }

        private static bool HasBubbleTemplate(Transform lake, string templateName)
        {
            return lake.Find(templateName) is { } source && source.GetComponent<ParticleSystem>() != null;
        }

        private static ParticleSystem MakeTemplateBubbleSystem(Transform lake, Transform parent, string templateName, Material material, string name, int sortingOrder)
        {
            Transform source = lake.Find(templateName);
            if (source == null || source.GetComponent<ParticleSystem>() == null)
                throw new InvalidOperationException($"[AcidLake] Missing required bubble template '{templateName}'.");

            GameObject clone = UnityEngine.Object.Instantiate(source.gameObject, parent);
            clone.name = name;
            clone.transform.localPosition = new Vector3(0f, -0.35f, 0f);
            clone.transform.localRotation = Quaternion.identity;
            clone.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(clone, "Clone Acid Bubble Motion Template");

            ParticleSystem system = clone.GetComponent<ParticleSystem>();
            ParticleSystemRenderer renderer = clone.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingLayerName = ActivitySortingLayer;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return system;
        }

        private static void DisableLegacyController(Transform root)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.GetType().Name != "AcidVfxController") continue;
                if (component is Behaviour behaviour && behaviour.enabled)
                {
                    Undo.RecordObject(behaviour, "Disable Legacy Acid VFX Controller");
                    behaviour.enabled = false;
                }
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/Bao");
            EnsureFolder("Assets/_Project/Bao/FX");
            EnsureFolder(RootDirectory);
            EnsureFolder(MaterialDirectory);
            EnsureFolder(PrefabDirectory);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name)) AssetDatabase.CreateFolder(parent, name);
        }

        private static Material EnsureMaterial(string path, Shader shader, Texture2D texture)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }
            if (texture != null)
            {
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            }
            return material;
        }

        private static SpriteRenderer MakeLayer(Transform parent, string name, Sprite sprite, Material material, string sortingLayer, int sortingOrder, Color color)
        {
            GameObject layer = new(name);
            layer.transform.SetParent(parent, false);
            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = sortingOrder;
            renderer.color = color;
            return renderer;
        }

        private static ParticleSystem MakeParticleSystem(Transform parent, string name, Material material, string sortingLayer, int sortingOrder)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            ParticleSystem system = child.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.gravityModifier = 0f;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        private static void ConfigureSurfaceBubbles(ParticleSystem system, float rate, int cap, Vector2 size, Vector2 lifetime, float width, float surfaceY, Color color, float riseSpeed)
        {
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.gravityModifier = 0f;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.maxParticles = cap;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = color;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0f, surfaceY, 0f);
            shape.scale = new Vector3(width, 0.24f, 0f);

            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(riseSpeed);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.18f;
            noise.frequency = 0.55f;
            noise.scrollSpeed = 0.16f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = FadeGradient(color, 0.82f);

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.18f), new Keyframe(0.22f, 0.92f), new Keyframe(0.68f, 1.24f), new Keyframe(1f, 0f)));
        }

        private static void ConfigureBubbleTemplate(ParticleSystem system, float rate, int cap, Vector2 size, Vector2 lifetime, Vector2 speed, float width, float height, Color color)
        {
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.gravityModifier = 0f;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.maxParticles = cap;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = color;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0f, -0.35f, 0f);
            shape.scale = new Vector3(width, height, 0f);

            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            // Unity validates that all three axes use the same curve mode. The
            // particle start-speed range plus Noise already provides variation,
            // so use Constant curves on every axis and avoid a mixed-mode rig.
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.34f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.26f;
            noise.frequency = 0.72f;
            noise.scrollSpeed = 0.24f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = FadeGradient(color, 0.74f);

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.28f), new Keyframe(0.22f, 0.95f), new Keyframe(0.66f, 1.18f), new Keyframe(1f, 0.08f)));
        }

        private static void ConfigureMist(ParticleSystem system, float rate, int cap, float width, Color color)
        {
            ParticleSystem.MainModule main = system.main;
            main.maxParticles = cap;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.72f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.35f, 2.5f);
            main.startColor = color;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = rate;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0f, -0.1f, 0f);
            shape.scale = new Vector3(width, 0.9f, 0f);
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.56f;
            noise.frequency = 0.28f;
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = FadeGradient(color, 0.58f);
            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.42f), new Keyframe(0.45f, 1f), new Keyframe(1f, 1.3f)));
        }

        private static void ConfigurePops(ParticleSystem system, int cap, Color color)
        {
            ParticleSystem.MainModule main = system.main;
            main.maxParticles = cap;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.65f, 1.25f);
            main.startColor = color;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = FadeGradient(color, 0.92f);
            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.16f), new Keyframe(0.24f, 1.28f), new Keyframe(1f, 0f)));
        }

        private static void ConfigureMotes(ParticleSystem system, float rate, int cap, float width, Color color)
        {
            ParticleSystem.MainModule main = system.main;
            main.maxParticles = cap;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 2.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.16f, 0.38f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
            main.startColor = color;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = rate;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0f, -0.15f, 0f);
            shape.scale = new Vector3(width, 0.75f, 0f);
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.22f;
            noise.frequency = 0.75f;
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = FadeGradient(color, 0.76f);
        }

        private static ParticleSystem.MinMaxCurve ConstantRangeCurves(float minimum, float maximum)
        {
            return new ParticleSystem.MinMaxCurve(
                1f,
                AnimationCurve.Constant(0f, 1f, minimum),
                AnimationCurve.Constant(0f, 1f, maximum));
        }

        private static Gradient FadeGradient(Color color, float peakAlpha)
        {
            Gradient gradient = new();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 0.56f), new GradientColorKey(Color.Lerp(color, Color.white, 0.18f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.16f), new GradientAlphaKey(peakAlpha, 0.68f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        private static void AssignController(MapHangAcidLakeVfxController controller, SpriteRenderer lake, SpriteRenderer accent, SpriteRenderer rim, Light2D light, ParticleSystem simmer, ParticleSystem large, ParticleSystem mist, ParticleSystem pops, ParticleSystem motes)
        {
            SetField(controller, "lakeRenderer", lake);
            SetField(controller, "surfaceAccent", accent);
            SetField(controller, "corrosiveRim", rim);
            SetField(controller, "accentLight", light);
            SetField(controller, "simmerBubbles", simmer);
            SetField(controller, "largeBubbles", large);
            SetField(controller, "toxicMist", mist);
            SetField(controller, "rarePops", pops);
            SetField(controller, "acidMotes", motes);
            EditorUtility.SetDirty(controller);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            typeof(MapHangAcidLakeVfxController).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
        }

        private static void SavePrefabTemplate(GameObject instance)
        {
            GameObject temporary = UnityEngine.Object.Instantiate(instance);
            temporary.name = "PF_MapHangAcidLakeVFX";
            temporary.transform.SetParent(null, true);
            MapHangAcidLakeVfxController controller = temporary.GetComponent<MapHangAcidLakeVfxController>();
            if (controller != null)
            {
                SetField(controller, "lakeRenderer", null);
                SetField(controller, "accentLight", null);
            }
            PrefabUtility.SaveAsPrefabAsset(temporary, PrefabPath);
            UnityEngine.Object.DestroyImmediate(temporary);
        }
    }
}
