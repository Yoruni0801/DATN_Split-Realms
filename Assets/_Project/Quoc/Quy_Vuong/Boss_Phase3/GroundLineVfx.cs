using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Quoc.QuyVuong.Boss_Phase3
{
    internal static class GroundLineVfxUtility
    {
        private const string ResourceShaderPath = "Shaders/GroundRiftAdditive";
        private const string ShaderName = "Quoc/Phase2/Ground Rift Additive";

        public static Material CreateMaterial(Color edgeColor, Color coreColor, float intensity)
        {
            Shader shader = Resources.Load<Shader>(ResourceShaderPath);
            if (shader == null) shader = Shader.Find(ShaderName);
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;

            Material material = new Material(shader)
            {
                name = "GroundRift_Runtime_Material",
                hideFlags = HideFlags.DontSave
            };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", edgeColor);
            if (material.HasProperty("_CoreColor")) material.SetColor("_CoreColor", coreColor);
            if (material.HasProperty("_Intensity")) material.SetFloat("_Intensity", intensity);
            if (material.HasProperty("_FlowSpeed")) material.SetFloat("_FlowSpeed", 8f);
            if (material.HasProperty("_NoiseScale")) material.SetFloat("_NoiseScale", 15f);
            if (material.HasProperty("_PulseSpeed")) material.SetFloat("_PulseSpeed", 9f);
            return material;
        }

        public static LineRenderer CreateLine(
            Transform parent,
            string objectName,
            Material material,
            int sortingOrder,
            float width)
        {
            GameObject lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(parent, false);
            lineObject.transform.localPosition = Vector3.zero;

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Tile;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.widthMultiplier = width;
            line.sortingOrder = sortingOrder;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            if (material != null) line.sharedMaterial = material;
            return line;
        }

        public static void SetEllipse(LineRenderer line, float radiusX, float radiusY, int segments = 48)
        {
            if (line == null) return;
            line.loop = true;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * radiusX,
                    Mathf.Sin(angle) * radiusY,
                    0f));
            }
        }

        public static ParticleSystem CreateBurst(
            Transform parent,
            string objectName,
            Material material,
            Color startColor,
            int count,
            float minSpeed,
            float maxSpeed,
            float minSize,
            float maxSize,
            float minLifetime,
            float maxLifetime,
            float gravity,
            int sortingOrder,
            float radius = 0.18f)
        {
            GameObject particleObject = new GameObject(objectName);
            particleObject.transform.SetParent(parent, false);
            particleObject.transform.localPosition = Vector3.zero;

            ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();

            // AddComponent starts a playOnAwake system immediately. Stop and clear it
            // before touching duration/loop to avoid Unity's runtime warning.
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startColor = startColor;
            main.gravityModifier = gravity;
            main.maxParticles = Mathf.Max(32, count * 2);

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 1, short.MaxValue))
            });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fadeGradient = new Gradient();
            fadeGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(1f, 0.48f, 0.08f), 0.55f),
                    new GradientColorKey(new Color(0.24f, 0.11f, 0.04f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.85f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = fadeGradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.35f),
                    new Keyframe(0.18f, 1f),
                    new Keyframe(1f, 0f)));

            ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (material != null) renderer.sharedMaterial = material;

            particles.Play(true);
            return particles;
        }

        public static void DestroyMaterials(IEnumerable<Material> materials)
        {
            if (materials == null) return;
            foreach (Material material in materials)
            {
                if (material != null) UnityEngine.Object.Destroy(material);
            }
        }
    }

    internal sealed class GroundLineImpactBurst : MonoBehaviour
    {
        private readonly List<Material> runtimeMaterials = new();
        private float elapsed;
        private float lifetime;

        public static void Spawn(Vector3 position, bool hitPlayer, bool terrainImpact)
        {
            GameObject effectObject = new GameObject(hitPlayer
                ? "GroundLine_PlayerImpact_VFX"
                : "GroundLine_TerrainImpact_VFX");
            effectObject.transform.position = new Vector3(position.x, position.y, -0.2f);
            GroundLineImpactBurst burst = effectObject.AddComponent<GroundLineImpactBurst>();
            burst.Build(hitPlayer, terrainImpact);
        }

        private void Build(bool hitPlayer, bool terrainImpact)
        {
            lifetime = terrainImpact ? 1.05f : 0.72f;
            Material ringMaterial = GroundLineVfxUtility.CreateMaterial(
                new Color(1f, 0.1f, 0.01f, 0.95f),
                new Color(1f, 0.95f, 0.4f, 1f), terrainImpact ? 6.5f : 5.2f);
            Material debrisMaterial = GroundLineVfxUtility.CreateMaterial(
                new Color(0.42f, 0.12f, 0.015f, 0.95f),
                new Color(1f, 0.5f, 0.055f, 1f), 3.2f);
            if (ringMaterial != null) runtimeMaterials.Add(ringMaterial);
            if (debrisMaterial != null) runtimeMaterials.Add(debrisMaterial);

            GroundLineVfxUtility.CreateBurst(transform, "Impact_Golden_Sparks", ringMaterial,
                Color.white, terrainImpact ? 20 : 12, 2.5f, terrainImpact ? 8f : 5.5f,
                0.04f, 0.14f, 0.18f, 0.48f, 0.55f, 34, 0.12f);
            GroundLineVfxUtility.CreateBurst(transform, "Impact_Rock_Chunks", debrisMaterial,
                new Color(0.75f, 0.31f, 0.07f, 1f), terrainImpact ? 13 : 7,
                1.2f, terrainImpact ? 5.2f : 3.6f, 0.10f, 0.28f,
                0.35f, 0.75f, 1.2f, 29, 0.22f);

            if (CameraController.Instance != null)
            {
                CameraController.Instance.ShakeCamera(terrainImpact ? 0.13f : 0.08f,
                    terrainImpact ? 0.095f : 0.055f);
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= lifetime) Destroy(gameObject);
        }

        private void OnDestroy() => GroundLineVfxUtility.DestroyMaterials(runtimeMaterials);
    }

    internal sealed class GroundLineDusthornEmergence : MonoBehaviour
    {
        private readonly List<Material> runtimeMaterials = new();
        private GameObject dusthornPrefab;
        private Action<GameObject> onSpawned;
        private Action onCancelled;
        private float delay;
        private bool resolved;

        public void Initialize(GameObject prefab, Vector3 position, float spawnDelay,
            Action<GameObject> spawnedCallback, Action cancelledCallback)
        {
            dusthornPrefab = prefab;
            onSpawned = spawnedCallback;
            onCancelled = cancelledCallback;
            delay = Mathf.Max(0.1f, spawnDelay);
            transform.position = new Vector3(position.x, position.y, -0.18f);

            Material material = GroundLineVfxUtility.CreateMaterial(
                new Color(0.55f, 0.08f, 0.005f, 0.9f),
                new Color(1f, 0.72f, 0.13f, 1f), 4.4f);
            if (material != null) runtimeMaterials.Add(material);
            GroundLineVfxUtility.CreateBurst(transform, "Dusthorn_Emergence_Dust", material,
                new Color(0.75f, 0.25f, 0.035f, 0.85f), 18, 0.3f, 2.1f,
                0.10f, 0.32f, 0.30f, 0.72f, 0.7f, 29, 0.7f);
            StartCoroutine(SpawnRoutine());
        }

        private IEnumerator SpawnRoutine()
        {
            yield return new WaitForSeconds(delay);
            GameObject dusthorn = dusthornPrefab != null
                ? Instantiate(dusthornPrefab, new Vector3(transform.position.x, transform.position.y, 0f), Quaternion.identity)
                : null;
            resolved = true;
            onSpawned?.Invoke(dusthorn);
            GroundLineImpactBurst.Spawn(transform.position, false, true);
            Destroy(gameObject, 0.3f);
        }

        private void OnDestroy()
        {
            if (!resolved) onCancelled?.Invoke();
            GroundLineVfxUtility.DestroyMaterials(runtimeMaterials);
        }
    }
}
