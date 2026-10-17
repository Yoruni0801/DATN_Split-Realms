using System.IO;
using Bao.FX.Lava;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bao.FX.Lava.Editor
{
    public static class LavaVfxSetup
    {
        private const string RootDirectory = "Assets/_Project/Bao/FX/Lava";
        private const string MaterialDirectory = RootDirectory + "/Materials";
        private const string SurfaceMaterialPath = MaterialDirectory + "/M_LavaSurface.mat";
        private const string EmbersMaterialPath = MaterialDirectory + "/M_LavaParticle_Embers.mat";
        private const string SmokeMaterialPath = MaterialDirectory + "/M_LavaParticle_Smoke.mat";
        private const string LavaPrefabPath = "Assets/_Source/2D Fantasy sprite bundle/Lava dungeon pack/Prefabs/LAVA.prefab";

        [MenuItem("Tools/Bao/Setup LAVA Prefab VFX")]
        public static void SetupLavaPrefab()
        {
            EnsureFolders();
            Material surfaceMat = AssetDatabase.LoadAssetAtPath<Material>(SurfaceMaterialPath);
            Material embersMat = AssetDatabase.LoadAssetAtPath<Material>(EmbersMaterialPath);
            Material smokeMat = AssetDatabase.LoadAssetAtPath<Material>(SmokeMaterialPath);

            if (surfaceMat == null || embersMat == null || smokeMat == null)
            {
                Debug.LogError("[LavaVFX] Required Lava materials are missing. Please ensure shaders and materials are compiled.");
                return;
            }

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(LavaPrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError($"[LavaVFX] Could not load prefab at {LavaPrefabPath}");
                return;
            }

            string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
            using (var editScope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                GameObject root = editScope.prefabContentsRoot;

                // 1. Assign surface material to existing sprite renderers
                foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr.gameObject.name == "Lava front" || sr.gameObject.name == "Lava A" || sr.gameObject.name == "Lava front (2)")
                    {
                        sr.sharedMaterial = surfaceMat;
                    }
                }

                // 2. Clear old LavaVFX child if exists
                Transform oldVfx = root.transform.Find("LavaVFX");
                if (oldVfx != null)
                {
                    Object.DestroyImmediate(oldVfx.gameObject);
                }

                // 3. Create LavaVFX child container
                GameObject vfxRoot = new("LavaVFX");
                vfxRoot.transform.SetParent(root.transform, false);
                vfxRoot.transform.localPosition = Vector3.zero;
                vfxRoot.transform.localRotation = Quaternion.identity;
                vfxRoot.transform.localScale = Vector3.one;

                // Find main front renderer for reference
                SpriteRenderer frontRenderer = root.transform.Find("Lava front")?.GetComponent<SpriteRenderer>();
                Sprite baseSprite = frontRenderer != null ? frontRenderer.sprite : null;

                // Surface Accent Layer (Subtle secondary magma flow)
                SpriteRenderer accent = MakeLayer(vfxRoot.transform, "SurfaceAccent", baseSprite, surfaceMat, "Default", 3, new Color(1f, 1f, 1f, 0.35f));
                accent.transform.localPosition = new Vector3(3.2f, -10.25f, 122.04f);
                accent.transform.localScale = new Vector3(33.9f, 1f, 1f);

                // Molten Rim Layer (Vibrant top crest)
                SpriteRenderer rim = MakeLayer(vfxRoot.transform, "MoltenRim", baseSprite, surfaceMat, "Default", 4, new Color(1f, 1f, 1f, 0.45f));
                rim.transform.localPosition = new Vector3(7.2f, -10.25f, 122.04f);
                rim.transform.localScale = new Vector3(41.5f, 1f, 1f);

                // Particles (Vivid Warm Orange, Slow Magma Bubbles & Soft Smoke)
                ParticleSystem bubbles = MakeParticleSystem(vfxRoot.transform, "PS_LavaBubbles", embersMat, "Default", 5);
                ConfigureLavaBubbles(bubbles, 12f, 35, new Vector2(0.25f, 0.55f), new Vector2(1.2f, 2.2f), 36f, -10.2f, new Color(0.96f, 0.45f, 0.05f, 1.0f), 0.12f);

                ParticleSystem embers = MakeParticleSystem(vfxRoot.transform, "PS_LavaEmbers", embersMat, "Default", 6);
                ConfigureLavaEmbers(embers, 16f, 50, 36f, -10.2f, new Color(0.98f, 0.55f, 0.06f, 0.9f));

                ParticleSystem smoke = MakeParticleSystem(vfxRoot.transform, "PS_LavaHeatSmoke", smokeMat, "Default", 2);
                ConfigureHeatSmoke(smoke, 3.5f, 20, 36f, -10.0f, new Color(0.28f, 0.10f, 0.05f, 0.45f));

                ParticleSystem bursts = MakeParticleSystem(vfxRoot.transform, "PS_RareBursts", embersMat, "Default", 7);
                ConfigureBursts(bursts, 16, new Color(1.0f, 0.65f, 0.10f, 0.9f));

                // Controller
                LavaVfxController controller = vfxRoot.AddComponent<LavaVfxController>();
                SetField(controller, "lavaRenderer", frontRenderer);
                SetField(controller, "surfaceAccent", accent);
                SetField(controller, "moltenRim", rim);
                SetField(controller, "glowLight", null);
                SetField(controller, "lavaBubbles", bubbles);
                SetField(controller, "lavaEmbers", embers);
                SetField(controller, "heatSmoke", smoke);
                SetField(controller, "rareBursts", bursts);

                controller.ApplyVisualState();
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[LavaVFX] Successfully applied Vibrant Map 5 Lava VFX & Shader to LAVA.prefab!");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/Bao");
            EnsureFolder("Assets/_Project/Bao/FX");
            EnsureFolder(RootDirectory);
            EnsureFolder(MaterialDirectory);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name)) AssetDatabase.CreateFolder(parent, name);
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

        private static void ConfigureLavaBubbles(ParticleSystem system, float rate, int cap, Vector2 size, Vector2 lifetime, float width, float surfaceY, Color color, float riseSpeed)
        {
            ParticleSystem.MainModule main = system.main;
            main.maxParticles = cap;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = color;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = rate;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0f, surfaceY, 0f);
            shape.scale = new Vector3(width, 0.35f, 0f);

            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(riseSpeed);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.20f;
            noise.frequency = 0.5f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = FadeGradient(color, 0.95f);

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.2f), new Keyframe(0.25f, 1.0f), new Keyframe(0.75f, 1.35f), new Keyframe(1f, 0f)));
        }

        private static void ConfigureLavaEmbers(ParticleSystem system, float rate, int cap, float width, float surfaceY, Color color)
        {
            ParticleSystem.MainModule main = system.main;
            main.maxParticles = cap;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.0f, 4.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
            main.startColor = color;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = rate;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0f, surfaceY, 0f);
            shape.scale = new Vector3(width, 0.5f, 0f);

            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.35f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.35f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = FadeGradient(color, 0.9f);
        }

        private static void ConfigureHeatSmoke(ParticleSystem system, float rate, int cap, float width, float surfaceY, Color color)
        {
            ParticleSystem.MainModule main = system.main;
            main.maxParticles = cap;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.0f, 5.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.40f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3.0f);
            main.startColor = color;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = rate;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0f, surfaceY, 0f);
            shape.scale = new Vector3(width, 0.8f, 0f);

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.30f;
            noise.frequency = 0.25f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = FadeGradient(color, 0.5f);

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.4f), new Keyframe(0.5f, 1.0f), new Keyframe(1f, 1.4f)));
        }

        private static void ConfigureBursts(ParticleSystem system, int cap, Color color)
        {
            ParticleSystem.MainModule main = system.main;
            main.maxParticles = cap;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.85f);
            main.startColor = color;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = FadeGradient(color, 1.0f);

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.2f), new Keyframe(0.3f, 1.3f), new Keyframe(1f, 0f)));
        }

        private static Gradient FadeGradient(Color color, float peakAlpha)
        {
            Gradient gradient = new();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 0.6f), new GradientColorKey(Color.Lerp(color, Color.yellow, 0.35f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.2f), new GradientAlphaKey(peakAlpha, 0.7f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            typeof(LavaVfxController).GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(target, value);
        }
    }
}
