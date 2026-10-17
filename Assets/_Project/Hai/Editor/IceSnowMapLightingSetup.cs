using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Hai.EditorTools
{
    /// <summary>
    /// Selective lighting pass for Map_IceandSnow 1. Accent lights stay close to visible light
    /// sources (rune cubes, the star and frozen waterfalls) instead of washing every map sprite.
    /// </summary>
    public static class IceSnowMapLightingSetup
    {
        private const string ScenePath = "Assets/_Project/Hai/Scene/Map_IceandSnow 1.unity";
        private const string LightingFolder = "Assets/_Project/Hai/Lighting";
        private const string ProfilePath = LightingFolder + "/Map_IceandSnow_LightingProfile.asset";
        private const string LightingRootName = "[Lighting] Ice & Snow";

        private static readonly LightPoint[] LightPoints =
        {
            new("01 Crystal - West Rune Cube", new Vector2(-193.30f, 53.21f), new Color(0.28f, 0.84f, 1.00f), 1.15f, 6f, 1.5f, -1432123075),
            new("02 Crystal - Ridge Cube", new Vector2(-114.76f, 48.88f), new Color(0.34f, 0.82f, 1.00f), 1.05f, 6f, 1.5f, 0),
            new("03 Crystal - Crevasse Cube", new Vector2(77.03f, 2.22f), new Color(0.20f, 0.92f, 1.00f), 1.15f, 6f, 1.5f, 0),
            new("04 Falls - First Icefall", new Vector2(211f, 11f), new Color(0.34f, 0.74f, 1.00f), 0.82f, 14f, 3f, 0),
            new("05 Crystal - Lone Cube", new Vector2(379.87f, 9.90f), new Color(0.30f, 0.82f, 1.00f), 1.05f, 6f, 1.5f, 0),
            new("06 Falls - Split Icefall", new Vector2(421f, 23f), new Color(0.34f, 0.76f, 1.00f), 0.82f, 12f, 3f, 0),
            new("07 Falls - Deep West", new Vector2(468f, -40.5f), new Color(0.25f, 0.80f, 1.00f), 0.88f, 10f, 2.5f, 0),
            new("08 Crystal - Deep Cube", new Vector2(491.47f, -44.81f), new Color(0.18f, 0.90f, 1.00f), 1.15f, 7f, 1.5f, 0),
            new("09 Falls - Deep East", new Vector2(505.5f, -44f), new Color(0.25f, 0.80f, 1.00f), 0.88f, 10f, 2.5f, 0),
            new("10 Crystal - Upper Rune Cube", new Vector2(530.60f, 34.48f), new Color(0.28f, 0.86f, 1.00f), 1.05f, 6f, 1.5f, 0),
            new("11 Star - Upper Beacon", new Vector2(532.82f, 28.07f), new Color(0.58f, 0.90f, 1.00f), 1.25f, 5f, 1.25f, 0),
            new("12 Falls - Upper Icefall", new Vector2(564f, 33.7f), new Color(0.32f, 0.78f, 1.00f), 0.82f, 10f, 2.5f, 0),
            new("13 Falls - Lower Exit", new Vector2(610f, -22.5f), new Color(0.34f, 0.76f, 1.00f), 0.82f, 10f, 2.5f, 0),
        };

        [MenuItem("Tools/Hai/Decorate Ice & Snow Map Lighting")]
        public static void Apply()
        {
            Scene scene = GetTargetSceneWithoutDiscardingWork();
            if (!scene.IsValid())
                return;

            EnsureFolder(LightingFolder);
            VolumeProfile profile = BuildVolumeProfile();

            GameObject oldRoot = FindInScene(scene, LightingRootName);
            if (oldRoot != null)
                UnityEngine.Object.DestroyImmediate(oldRoot);

            GameObject root = new(LightingRootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            Light2D ambient = GetOrCreateSingleGlobalLight(scene);
            ambient.gameObject.name = "Ambient - Neutral Baseline";
            ambient.transform.SetParent(root.transform, true);
            ambient.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            ambient.transform.localScale = Vector3.one;
            ambient.lightType = Light2D.LightType.Global;
            ambient.blendStyleIndex = 0;
            ambient.color = Color.white;
            ambient.intensity = 1f;
            ambient.falloffIntensity = 0.5f;
            ambient.overlapOperation = Light2D.OverlapOperation.Additive;
            ambient.shadowsEnabled = false;
            ambient.volumetricEnabled = false;
            ApplyToEverySortingLayer(ambient);

            GameObject accentRoot = new("Accent Lights - Selected Emitters Only");
            accentRoot.transform.SetParent(root.transform, false);
            foreach (LightPoint point in LightPoints)
                CreatePointLight(accentRoot.transform, point);

            GameObject volumeObject = new("Post FX - Bloom & Vignette");
            volumeObject.transform.SetParent(root.transform, false);
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.weight = 1f;
            volume.sharedProfile = profile;

            Camera mainCamera = FindComponentsInScene<Camera>(scene).FirstOrDefault(camera => camera.CompareTag("MainCamera"));
            if (mainCamera != null)
            {
                mainCamera.allowHDR = true;
                UniversalAdditionalCameraData cameraData = mainCamera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = true;
                cameraData.stopNaN = true;
                cameraData.dithering = true;
                EditorUtility.SetDirty(mainCamera);
                EditorUtility.SetDirty(cameraData);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = scene.path == ScenePath
                ? EditorSceneManager.SaveScene(scene)
                : EditorSceneManager.SaveScene(scene, ScenePath, false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Validate(scene, profile, mainCamera);
            CapturePreviews(mainCamera);
            SceneView.RepaintAll();

            if (!saved)
                throw new InvalidOperationException($"Could not save lighting changes to {ScenePath}.");

            Debug.Log($"[IceSnowLighting] Applied a neutral baseline plus {LightPoints.Length} source-bound accent lights, selective HDR Bloom and a light Vignette to {ScenePath}.");
        }

        [MenuItem("Tools/Hai/Validate Ice & Snow Map Lighting")]
        public static void ValidateOnly()
        {
            Scene scene = GetTargetSceneWithoutDiscardingWork();
            if (!scene.IsValid())
                return;

            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            Camera mainCamera = FindComponentsInScene<Camera>(scene).FirstOrDefault(camera => camera.CompareTag("MainCamera"));
            Validate(scene, profile, mainCamera);
            CapturePreviews(mainCamera);
        }

        private static VolumeProfile BuildVolumeProfile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "Map_IceandSnow_LightingProfile";
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            Bloom bloom = GetOrAdd<Bloom>(profile);
            bloom.active = true;
            bloom.threshold.Override(1.20f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.60f);
            bloom.tint.Override(new Color(0.72f, 0.90f, 1.00f, 1f));
            bloom.highQualityFiltering.Override(true);

            Vignette vignette = GetOrAdd<Vignette>(profile);
            vignette.active = true;
            vignette.color.Override(new Color(0.015f, 0.035f, 0.09f, 1f));
            vignette.center.Override(new Vector2(0.5f, 0.5f));
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.60f);
            vignette.rounded.Override(false);

            RemoveIfPresent<ColorAdjustments>(profile);
            RemoveIfPresent<Tonemapping>(profile);

            profile.Reset();
            EditorUtility.SetDirty(profile);
            foreach (VolumeComponent component in profile.components)
                EditorUtility.SetDirty(component);
            return profile;
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component))
                return component;

            component = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static void RemoveIfPresent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
                return;

            profile.components.Remove(component);
            UnityEngine.Object.DestroyImmediate(component, true);
        }

        private static Light2D GetOrCreateSingleGlobalLight(Scene scene)
        {
            List<Light2D> globalLights = FindComponentsInScene<Light2D>(scene)
                .Where(light => light.lightType == Light2D.LightType.Global)
                .ToList();

            Light2D result;
            if (globalLights.Count > 0)
            {
                result = globalLights[0];
                for (int i = 1; i < globalLights.Count; i++)
                    UnityEngine.Object.DestroyImmediate(globalLights[i].gameObject);
            }
            else
            {
                GameObject lightObject = new("Ambient - Neutral Baseline");
                SceneManager.MoveGameObjectToScene(lightObject, scene);
                result = lightObject.AddComponent<Light2D>();
            }

            return result;
        }

        private static void CreatePointLight(Transform parent, LightPoint point)
        {
            GameObject lightObject = new(point.Name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = new Vector3(point.Position.x, point.Position.y, 0f);

            Light2D light = lightObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.blendStyleIndex = 0;
            light.color = point.Color;
            light.intensity = point.Intensity;
            light.falloffIntensity = 0.72f;
            light.pointLightInnerRadius = point.InnerRadius;
            light.pointLightOuterRadius = point.OuterRadius;
            light.pointLightInnerAngle = 360f;
            light.pointLightOuterAngle = 360f;
            light.overlapOperation = Light2D.OverlapOperation.Additive;
            light.shadowsEnabled = false;
            light.volumetricEnabled = false;
            ApplyToSortingLayers(light, point.SortingLayerIds);

            SerializedObject serializedLight = new(light);
            SerializedProperty normalQuality = serializedLight.FindProperty("m_NormalMapQuality");
            if (normalQuality != null)
                normalQuality.intValue = (int)Light2D.NormalMapQuality.Disabled;
            SerializedProperty useNormalMap = serializedLight.FindProperty("m_UseNormalMap");
            if (useNormalMap != null)
                useNormalMap.boolValue = false;
            serializedLight.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(light);
        }

        private static void ApplyToEverySortingLayer(Light2D light)
        {
            ApplyToSortingLayers(light, SortingLayer.layers.Select(layer => layer.id).ToArray());
        }

        private static void ApplyToSortingLayers(Light2D light, IReadOnlyList<int> sortingLayerIds)
        {
            SerializedObject serializedLight = new(light);
            SerializedProperty property = serializedLight.FindProperty("m_ApplyToSortingLayers");
            property.arraySize = sortingLayerIds.Count;
            for (int i = 0; i < sortingLayerIds.Count; i++)
                property.GetArrayElementAtIndex(i).intValue = sortingLayerIds[i];
            serializedLight.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(light);
        }

        private static void Validate(Scene scene, VolumeProfile profile, Camera mainCamera)
        {
            var issues = new List<string>();
            List<Light2D> lights = FindComponentsInScene<Light2D>(scene).ToList();
            int globalCount = lights.Count(light => light.lightType == Light2D.LightType.Global);
            int pointCount = lights.Count(light => light.lightType == Light2D.LightType.Point && IsChildOfLightingRoot(light.transform));

            if (globalCount != 1)
                issues.Add($"expected exactly one Global Light 2D, found {globalCount}");
            if (pointCount != LightPoints.Length)
                issues.Add($"expected {LightPoints.Length} generated point lights, found {pointCount}");
            if (profile == null || !profile.TryGet(out Bloom bloom) || !bloom.active || bloom.intensity.value <= 0f)
                issues.Add("Bloom is missing or disabled");
            if (profile == null || !profile.TryGet(out Vignette vignette) || !vignette.active || vignette.intensity.value <= 0f)
                issues.Add("Vignette is missing or disabled");
            if (profile != null && profile.TryGet(out ColorAdjustments _))
                issues.Add("Color Adjustments must stay disabled because they modify every map sprite");
            if (profile != null && profile.TryGet(out Tonemapping _))
                issues.Add("Tonemapping must stay disabled because it modifies the whole frame");
            if (mainCamera == null)
                issues.Add("Main Camera was not found");
            else
            {
                UniversalAdditionalCameraData cameraData = mainCamera.GetUniversalAdditionalCameraData();
                if (!mainCamera.allowHDR)
                    issues.Add("Main Camera HDR is disabled");
                if (!cameraData.renderPostProcessing)
                    issues.Add("Main Camera post-processing is disabled");
            }

            if (issues.Count > 0)
                throw new InvalidOperationException("[IceSnowLighting] Validation failed: " + string.Join("; ", issues));

            Debug.Log($"[IceSnowLighting] Validation passed: {globalCount} global light, {pointCount} point lights, HDR camera, Bloom and Vignette active.");
        }

        private static bool IsChildOfLightingRoot(Transform transform)
        {
            while (transform != null)
            {
                if (transform.name == LightingRootName)
                    return true;
                transform = transform.parent;
            }
            return false;
        }

        private static void CapturePreviews(Camera camera)
        {
            if (camera == null)
                return;

            Vector3 originalPosition = camera.transform.position;
            RenderTexture originalTarget = camera.targetTexture;
            float originalAspect = camera.aspect;
            try
            {
                CapturePreview(camera, new Vector2(-193f, 53f), "/tmp/datn_ice_snow_lighting_crystal.png");
                CapturePreview(camera, new Vector2(215f, 14f), "/tmp/datn_ice_snow_lighting_falls.png");
                CapturePreview(camera, new Vector2(490f, -46f), "/tmp/datn_ice_snow_lighting_deep.png");
                CapturePreview(camera, new Vector2(533f, 28f), "/tmp/datn_ice_snow_lighting_star.png");
                Debug.Log("[IceSnowLighting] Preview renders written to /tmp/datn_ice_snow_lighting_{crystal,falls,deep,star}.png.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[IceSnowLighting] Preview capture skipped: {exception.Message}");
            }
            finally
            {
                camera.transform.position = originalPosition;
                camera.targetTexture = originalTarget;
                camera.aspect = originalAspect;
                RenderTexture.active = null;
            }
        }

        private static void CapturePreview(Camera camera, Vector2 position, string outputPath)
        {
            const int width = 1280;
            const int height = 720;
            camera.transform.position = new Vector3(position.x, position.y, camera.transform.position.z);
            camera.aspect = (float)width / height;

            RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGBHalf);
            Texture2D texture = new(width, height, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(renderTexture);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static Scene GetTargetSceneWithoutDiscardingWork()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene candidate = SceneManager.GetSceneAt(i);
                if (LooksLikeTargetScene(candidate))
                    return candidate;
            }

            bool hasDirtyScene = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Any(scene => scene.IsValid() && scene.isLoaded && scene.isDirty);
            if (hasDirtyScene)
            {
                Debug.LogError("[IceSnowLighting] The target map is not open and another scene has unsaved work. Open Map_IceandSnow 1, then run the tool again.");
                return default;
            }

            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static bool LooksLikeTargetScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return false;
            if (scene.path == ScenePath)
                return true;
            return FindInScene(scene, "World1") != null
                && FindInScene(scene, "TrangTri") != null
                && FindComponentsInScene<Camera>(scene).Any(camera => camera.CompareTag("MainCamera"));
        }

        private static GameObject FindInScene(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform match = root.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(transform => transform.name == objectName);
                if (match != null)
                    return match.gameObject;
            }
            return null;
        }

        private static IEnumerable<T> FindComponentsInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            string folderName = Path.GetFileName(folderPath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private readonly struct LightPoint
        {
            public LightPoint(string name, Vector2 position, Color color, float intensity, float outerRadius, float innerRadius, params int[] sortingLayerIds)
            {
                Name = name;
                Position = position;
                Color = color;
                Intensity = intensity;
                OuterRadius = outerRadius;
                InnerRadius = innerRadius;
                SortingLayerIds = sortingLayerIds;
            }

            public string Name { get; }
            public Vector2 Position { get; }
            public Color Color { get; }
            public float Intensity { get; }
            public float OuterRadius { get; }
            public float InnerRadius { get; }
            public IReadOnlyList<int> SortingLayerIds { get; }
        }
    }
}
