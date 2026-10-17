using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SplitRealms.EditorVFX
{
    /// <summary>Editor-only recipe. The saved prefab needs no runtime script.</summary>
    public static class AetherMotesAuthoring
    {
        public const string PrefabPath = "Assets/_Source/Lana Studio/Environment VFX pack/Prefabs/Rain/Rain_heavy.prefab";
        private const string Folder = "Assets/_Project/Duy/VFX/AetherMotes";

        [MenuItem("Tools/Split Realms/VFX/Rebuild Rain Heavy as Aether Motes")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Rebuild in Edit Mode only.");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Folder + "/AetherMotes.shader");
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("AetherMotes shader is missing or has compiler errors.");

            var pollen = Material("Pollen", shader, 2.20f, 0.62f, 0.32f, 0.90f, false);
            var firefly = Material("Firefly", shader, 3.30f, 0.88f, 0.65f, 1.05f, false);
            var distant = Material("DistantDust", shader, 0.62f, 0.42f, 0.28f, 0.55f, false);
            var foreground = Material("ForegroundGlow", shader, 0.70f, 0.08f, 0.40f, 0.65f, false);
            var mist = Material("GroundVeil", shader, 0.32f, 0, 0.20f, 0.30f, true);

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var systems = root.GetComponentsInChildren<ParticleSystem>(true);
                if (systems.Length != 5)
                    throw new InvalidOperationException("Expected the five original particle systems; refusing to replace an unknown hierarchy.");
                // Keep original object/component IDs and child names for existing scene overrides.
                Configure(root.GetComponent<ParticleSystem>(), pollen, 16f, 6f, 10f, 0.16f, 0.34f, 180,
                    new Vector3(30, 11, 0.25f), new Vector3(0, -3, 0), -5, 0.80f, 0.13f, -0.13f, 1731);
                Configure(root.transform.Find("Rings").GetComponent<ParticleSystem>(), firefly, 2.6f, 8f, 13f, 0.45f, 0.80f, 40,
                    new Vector3(30, 7, 0.3f), new Vector3(0, -3.5f, 0), -2, 0.95f, 0.23f, 0.025f, 2971);
                Configure(root.transform.Find("Spray ").GetComponent<ParticleSystem>(), distant, 10f, 7f, 12f, 0.08f, 0.19f, 130,
                    new Vector3(30, 12, 0.3f), new Vector3(0, -2, 0), -20, 0.42f, 0.09f, -0.055f, 3917);
                Configure(root.transform.Find("Spray2").GetComponent<ParticleSystem>(), foreground, 1.1f, 8f, 14f, 0.50f, 0.95f, 20,
                    new Vector3(30, 11, 0.2f), new Vector3(0, -3, 0), 20, 0.32f, 0.16f, -0.10f, 4931);
                var fog = root.transform.Find("Fog").GetComponent<ParticleSystem>();
                Configure(fog, mist, 0.45f, 12f, 16f, 5f, 8f, 10,
                    new Vector3(30, 1.2f, 0.2f), new Vector3(0, -1, 0), -12, 0.15f, 0.035f, 0.01f, 5923);
                fog.transform.localPosition = new Vector3(0, -5.5f, 0);
                var fm = fog.main;
                fm.startRotation = new ParticleSystem.MinMaxCurve(-0.10f, 0.10f);
                var fr = fog.rotationOverLifetime; fr.enabled = false;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log(Validate());
        }

        private static Material Material(string name, Shader shader, float glow, float core, float pulse, float speed, bool mist)
        {
            string path = Folder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader;
            mat.SetColor("_Tint", Color.white);
            mat.SetFloat("_Glow", glow); mat.SetFloat("_Core", core);
            mat.SetFloat("_Pulse", pulse); mat.SetFloat("_PulseSpeed", speed);
            mat.SetFloat("_Mist", mist ? 1 : 0);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssetIfDirty(mat);
            return mat;
        }

        private static void Configure(ParticleSystem ps, Material mat, float rate, float lifeMin, float lifeMax,
            float sizeMin, float sizeMax, int cap, Vector3 volume, Vector3 center,
            int sorting, float opacity, float wandering, float verticalDrift, uint seed)
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.gameObject.SetActive(true);
            if (ps.transform.parent != null) ps.transform.localPosition = Vector3.zero;
            ps.transform.localRotation = Quaternion.identity;
            ps.transform.localScale = Vector3.one;
            ps.useAutoRandomSeed = false; ps.randomSeed = seed;
            var main = ps.main;
            main.duration = 16; main.loop = true; main.prewarm = true; main.playOnAwake = true;
            main.startDelay = 0; main.simulationSpeed = 1; main.useUnscaledTime = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            // Only the spawn volume follows the instance's 3x X scale, not the fleck sizes.
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;
            main.stopAction = ParticleSystemStopAction.None;
            main.ringBufferMode = ParticleSystemRingBufferMode.Disabled;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = 0; main.gravityModifier = 0;
            main.startSize3D = false; main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startRotation3D = false; main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.flipRotation = 0;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1, 0.65f, 0.28f, opacity * 0.65f), new Color(1, 0.93f, 0.67f, opacity));
            main.maxParticles = cap;

            var emission = ps.emission; emission.enabled = true;
            emission.rateOverTime = rate; emission.rateOverDistance = 0;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = center; shape.rotation = Vector3.zero; shape.scale = volume;
            shape.randomDirectionAmount = 0; shape.sphericalDirectionAmount = 0; shape.alignToDirection = false;
            shape.texture = null;

            var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.045f, 0.09f);
            velocity.y = new ParticleSystem.MinMaxCurve(verticalDrift - 0.035f, verticalDrift + 0.035f);
            velocity.z = new ParticleSystem.MinMaxCurve(0, 0);
            velocity.orbitalX = velocity.orbitalY = velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0, 0);
            velocity.orbitalOffsetX = velocity.orbitalOffsetY = velocity.orbitalOffsetZ = new ParticleSystem.MinMaxCurve(0, 0);
            velocity.radial = new ParticleSystem.MinMaxCurve(0, 0);
            velocity.speedModifier = new ParticleSystem.MinMaxCurve(1, 1);
            var noise = ps.noise; noise.enabled = true; noise.separateAxes = true;
            noise.strengthX = wandering; noise.strengthY = wandering * 0.65f; noise.strengthZ = 0;
            noise.frequency = 0.22f; noise.scrollSpeed = 0.055f;
            noise.damping = true; noise.octaveCount = 1; noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.remapEnabled = false; noise.positionAmount = 1; noise.rotationAmount = 0; noise.sizeAmount = 0;

            var color = ps.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] {new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1, 0.84f, 0.59f), 1)},
                new[] {new GradientAlphaKey(0, 0), new GradientAlphaKey(0.7f, 0.16f), new GradientAlphaKey(1, 0.4f), new GradientAlphaKey(0.7f, 0.7f), new GradientAlphaKey(0, 1)});
            color.color = gradient;
            var size = ps.sizeOverLifetime; size.enabled = true; size.separateAxes = false;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0.65f), new Keyframe(0.25f, 1), new Keyframe(0.75f, 0.95f), new Keyframe(1, 0.65f)));
            var rotation = ps.rotationOverLifetime; rotation.enabled = true; rotation.separateAxes = false;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);

            var collision = ps.collision; collision.enabled = false;
            var trigger = ps.trigger; trigger.enabled = false;
            var sub = ps.subEmitters; sub.enabled = false;
            while (sub.subEmittersCount > 0) sub.RemoveSubEmitter(0);
            var tex = ps.textureSheetAnimation; tex.enabled = false;
            var trails = ps.trails; trails.enabled = false;
            var lights = ps.lights; lights.enabled = false;
            var force = ps.forceOverLifetime; force.enabled = false;
            var external = ps.externalForces; external.enabled = false;
            var limit = ps.limitVelocityOverLifetime; limit.enabled = false;
            var inherit = ps.inheritVelocity; inherit.enabled = false;
            var speed = ps.colorBySpeed; speed.enabled = false;
            var speedSize = ps.sizeBySpeed; speedSize.enabled = false;
            var speedRotation = ps.rotationBySpeed; speedRotation.enabled = false;
            var lifetime = ps.lifetimeByEmitterSpeed; lifetime.enabled = false;

            var custom = ps.customData; custom.enabled = false;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.enabled = true; renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            // TE uses layer "3" for characters; Default is behind its scenery.
            string layer = Array.Exists(SortingLayer.layers, l => l.name == "3") ? "3" : "Default";
            renderer.sharedMaterial = mat; renderer.sortingLayerName = layer; renderer.sortingOrder = sorting;
            if (ps.transform.parent != null)
            {
                // Preserve TE's existing root SortingGroup. Child layers escape it so
                // the near glow can pass in front while fireflies stay behind actors.
                var group = ps.GetComponent<SortingGroup>();
                if (group == null) group = ps.gameObject.AddComponent<SortingGroup>();
                group.sortingLayerName = layer; group.sortingOrder = sorting; group.sortAtRoot = true;
            }
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.minParticleSize = 0; renderer.maxParticleSize = 0.5f;
            renderer.pivot = Vector3.zero; renderer.flip = Vector3.zero;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV});
        }

        public static string Validate()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var report = new StringBuilder("Aether motes validation:\n");
                int budget = 0;
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var r = ps.GetComponent<ParticleSystemRenderer>();
                    if (r.sharedMaterial == null || r.sharedMaterial.shader == null || ShaderUtil.ShaderHasError(r.sharedMaterial.shader))
                        throw new InvalidOperationException(ps.name + ": material/shader failure");
                    if (ps.collision.enabled || ps.subEmitters.enabled || ps.textureSheetAnimation.enabled || r.renderMode != ParticleSystemRenderMode.Billboard)
                        throw new InvalidOperationException(ps.name + ": legacy rain module remains enabled");
                    if (ps.main.scalingMode != ParticleSystemScalingMode.Shape || ps.main.startSpeed.constant != 0)
                        throw new InvalidOperationException(ps.name + ": scale/speed mismatch");
                    budget += ps.main.maxParticles;
                    report.AppendLine(ps.name + ": " + ps.emission.rateOverTime.constant + "/s; cap=" + ps.main.maxParticles + "; material=" + r.sharedMaterial.name + "; sorting=" + r.sortingOrder);
                }
                if (budget > 500) throw new InvalidOperationException("Particle budget exceeded: " + budget);
                report.AppendLine("Total cap=" + budget + "; no runtime MonoBehaviour required.");
                return report.ToString();
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
