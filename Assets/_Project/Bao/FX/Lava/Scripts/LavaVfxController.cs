using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Bao.FX.Lava
{
    [DisallowMultipleComponent]
    public sealed class LavaVfxController : MonoBehaviour
    {
        private static readonly int MainColorId = Shader.PropertyToID("_MainColor");
        private static readonly int DeepColorId = Shader.PropertyToID("_DeepColor");
        private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        private static readonly int CrustColorId = Shader.PropertyToID("_CrustColor");
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int UseColorRampId = Shader.PropertyToID("_UseColorRamp");
        private static readonly int RampPowId = Shader.PropertyToID("_RampPow");
        private static readonly int GradientBlendId = Shader.PropertyToID("_GradientBlend");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int PosterizeLevelsId = Shader.PropertyToID("_PosterizeLevels");
        private static readonly int RippleSpeedId = Shader.PropertyToID("_RippleSpeed");
        private static readonly int RippleScaleId = Shader.PropertyToID("_RippleScale");
        private static readonly int ScrollSpeedXId = Shader.PropertyToID("_ScrollSpeedX");
        private static readonly int ScrollSpeedYId = Shader.PropertyToID("_ScrollSpeedY");
        private static readonly int DistortionId = Shader.PropertyToID("_DistortionStrength");
        private static readonly int NoiseAmountId = Shader.PropertyToID("_NoiseAmount");
        private static readonly int BubbleStrengthId = Shader.PropertyToID("_BubbleStrength");
        private static readonly int SurfaceFoamId = Shader.PropertyToID("_SurfaceFoam");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionStrength");
        private static readonly int CellScaleId = Shader.PropertyToID("_CellScale");
        private static readonly int CellSpeedId = Shader.PropertyToID("_CellSpeed");
        private static readonly int RingWidthId = Shader.PropertyToID("_RingWidth");
        private static readonly int RingThresholdId = Shader.PropertyToID("_RingThreshold");
        private static readonly int CrustContrastId = Shader.PropertyToID("_CrustContrast");
        private static readonly int ShapeContrastId = Shader.PropertyToID("_ShapeContrast");
        private static readonly int FissureGlowStrengthId = Shader.PropertyToID("_FissureGlowStrength");
        private static readonly int VertexWaveAmountId = Shader.PropertyToID("_VertexWaveAmount");
        private static readonly int VertexWaveSpeedId = Shader.PropertyToID("_VertexWaveSpeed");
        private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int GlobalAlphaId = Shader.PropertyToID("_GlobalAlpha");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        [Header("Renderers")]
        [SerializeField] private SpriteRenderer lavaRenderer;
        [SerializeField] private SpriteRenderer surfaceAccent;
        [SerializeField] private SpriteRenderer moltenRim;
        [SerializeField] private Light2D glowLight;

        [Header("Particles")]
        [SerializeField] private ParticleSystem lavaBubbles;
        [SerializeField] private ParticleSystem lavaEmbers;
        [SerializeField] private ParticleSystem heatSmoke;
        [SerializeField] private ParticleSystem rareBursts;

        [Header("Vibrant Map 5 Magma Palette")]
        [ColorUsage(true, true)] [SerializeField] private Color mainColor = new(0.96f, 0.38f, 0.02f, 1.0f);
        [ColorUsage(true, true)] [SerializeField] private Color deepColor = new(0.32f, 0.03f, 0.01f, 1.0f);
        [ColorUsage(true, true)] [SerializeField] private Color edgeColor = new(1.4f, 0.75f, 0.08f, 1.0f);
        [ColorUsage(true, true)] [SerializeField] private Color crustColor = new(0.12f, 0.03f, 0.02f, 1.0f);
        [Range(0f, 1f)] [SerializeField] private float surfaceAlpha = 1.0f;
        [Range(0f, 3f)] [SerializeField] private float intensity = 0.65f;
        [Range(0f, 3f)] [SerializeField] private float brightness = 0.95f;

        [Header("Tornado Style Remapping & Dynamics")]
        [Range(0.1f, 5f)] [SerializeField] private float rampPower = 1.35f;
        [Range(0f, 1f)] [SerializeField] private float gradientBlend = 0.92f;
        [Range(0f, 16f)] [SerializeField] private float posterizeLevels = 0f;
        [Range(0f, 2f)] [SerializeField] private float rippleSpeed = 0.22f;
        [Range(1f, 30f)] [SerializeField] private float rippleScale = 4.0f;
        [Range(-2f, 2f)] [SerializeField] private float secondaryScrollX = 0.08f;
        [Range(-2f, 2f)] [SerializeField] private float secondaryScrollY = -0.03f;
        [Range(0f, 0.08f)] [SerializeField] private float distortionStrength = 0.015f;
        [Range(0f, 2f)] [SerializeField] private float noiseAmount = 0.70f;

        [Header("Boiling Bubbles & Dense Crust")]
        [Range(0f, 2f)] [SerializeField] private float bubbleStrength = 1.2f;
        [Range(0f, 1f)] [SerializeField] private float surfaceFoam = 0.85f;
        [Range(1f, 48f)] [SerializeField] private float cellScale = 9.0f;
        [Range(0f, 4f)] [SerializeField] private float cellSpeed = 0.38f;
        [Range(0.01f, 0.35f)] [SerializeField] private float ringWidth = 0.09f;
        [Range(0f, 1f)] [SerializeField] private float ringThreshold = 0.48f;
        [Range(0.1f, 5f)] [SerializeField] private float crustContrast = 2.0f;
        [Range(0.1f, 4f)] [SerializeField] private float shapeContrast = 1.4f;
        [Range(0f, 2f)] [SerializeField] private float fissureGlowStrength = 0.85f;

        [Header("Vertex Jitter / Wave")]
        [Range(0f, 0.1f)] [SerializeField] private float vertexWaveAmount = 0.006f;
        [Range(0f, 6f)] [SerializeField] private float vertexWaveSpeed = 0.8f;

        [Header("Rare Magma Burst Events")]
        [SerializeField] private Vector2 burstInterval = new(3.0f, 6.0f);
        [SerializeField] private Vector2Int burstCount = new(3, 6);
        [SerializeField] private float burstWidth = 25f;
        [SerializeField] private float lightPulseAmount = 0.25f;

        private MaterialPropertyBlock block;
        private ParticleSystem[] continuousSystems;
        private float[] baselineRates;
        private float baseLightIntensity;
        private Coroutine burstRoutine;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            continuousSystems = new[] { lavaBubbles, lavaEmbers, heatSmoke };
            CaptureParticleBaselines();
            if (glowLight != null) baseLightIntensity = glowLight.intensity;
            ApplyVisualState();
        }

        private void OnEnable()
        {
            if (block == null) block = new MaterialPropertyBlock();
            ApplyVisualState();
            if (Application.isPlaying && burstRoutine == null) burstRoutine = StartCoroutine(PlayRareBursts());
        }

        private void OnDisable()
        {
            if (burstRoutine == null) return;
            StopCoroutine(burstRoutine);
            burstRoutine = null;
        }

        private void OnValidate()
        {
            if (!Application.isPlaying && block != null) ApplyVisualState();
        }

        private void CaptureParticleBaselines()
        {
            if (continuousSystems == null) return;
            baselineRates = new float[continuousSystems.Length];
            for (int i = 0; i < continuousSystems.Length; i++)
            {
                if (continuousSystems[i] != null) baselineRates[i] = continuousSystems[i].emission.rateOverTimeMultiplier;
            }
        }

        public void ApplyVisualState()
        {
            ApplySurface(lavaRenderer, 1f);
            ApplySurface(surfaceAccent, 0.35f);
            ApplySurface(moltenRim, 0.45f);
            ApplyParticleRenderers(lavaBubbles, edgeColor, edgeColor, 0.85f);
            ApplyParticleRenderers(lavaEmbers, mainColor, edgeColor, 0.9f);
            ApplyParticleRenderers(heatSmoke, crustColor, mainColor, 0.5f);
            ApplyParticleRenderers(rareBursts, edgeColor, edgeColor, 0.9f);

            if (glowLight != null)
            {
                glowLight.color = new Color(0.96f, 0.38f, 0.05f, 1f);
                glowLight.intensity = intensity * 1.3f;
            }

            if (continuousSystems == null || baselineRates == null) return;
            for (int i = 0; i < continuousSystems.Length; i++)
            {
                if (continuousSystems[i] == null) continue;
                var emission = continuousSystems[i].emission;
                emission.rateOverTimeMultiplier = baselineRates[i] * (intensity * 0.7f + 0.3f);
            }
        }

        private void ApplySurface(SpriteRenderer renderer, float layerIntensity)
        {
            if (renderer == null || renderer.sharedMaterial == null) return;
            Material material = renderer.sharedMaterial;
            renderer.GetPropertyBlock(block);
            if (material.HasProperty(MainColorId)) block.SetColor(MainColorId, mainColor);
            if (material.HasProperty(DeepColorId)) block.SetColor(DeepColorId, deepColor);
            if (material.HasProperty(EdgeColorId)) block.SetColor(EdgeColorId, edgeColor);
            if (material.HasProperty(CrustColorId)) block.SetColor(CrustColorId, crustColor);
            if (material.HasProperty(AlphaId)) block.SetFloat(AlphaId, surfaceAlpha);
            if (material.HasProperty(BrightnessId)) block.SetFloat(BrightnessId, brightness);
            if (material.HasProperty(RampPowId)) block.SetFloat(RampPowId, rampPower);
            if (material.HasProperty(GradientBlendId)) block.SetFloat(GradientBlendId, gradientBlend);
            if (material.HasProperty(PosterizeLevelsId)) block.SetFloat(PosterizeLevelsId, posterizeLevels);
            if (material.HasProperty(RippleSpeedId)) block.SetFloat(RippleSpeedId, rippleSpeed);
            if (material.HasProperty(RippleScaleId)) block.SetFloat(RippleScaleId, rippleScale * (layerIntensity < 1f ? 1.2f : 1f));
            if (material.HasProperty(ScrollSpeedXId)) block.SetFloat(ScrollSpeedXId, secondaryScrollX);
            if (material.HasProperty(ScrollSpeedYId)) block.SetFloat(ScrollSpeedYId, secondaryScrollY);
            if (material.HasProperty(DistortionId)) block.SetFloat(DistortionId, distortionStrength);
            if (material.HasProperty(NoiseAmountId)) block.SetFloat(NoiseAmountId, noiseAmount);
            if (material.HasProperty(BubbleStrengthId)) block.SetFloat(BubbleStrengthId, bubbleStrength * layerIntensity);
            if (material.HasProperty(SurfaceFoamId)) block.SetFloat(SurfaceFoamId, surfaceFoam * layerIntensity);
            if (material.HasProperty(EmissionId)) block.SetFloat(EmissionId, intensity * layerIntensity);
            if (material.HasProperty(CellScaleId)) block.SetFloat(CellScaleId, cellScale * (layerIntensity < 1f ? 1.1f : 1f));
            if (material.HasProperty(CellSpeedId)) block.SetFloat(CellSpeedId, cellSpeed);
            if (material.HasProperty(RingWidthId)) block.SetFloat(RingWidthId, ringWidth);
            if (material.HasProperty(RingThresholdId)) block.SetFloat(RingThresholdId, ringThreshold);
            if (material.HasProperty(CrustContrastId)) block.SetFloat(CrustContrastId, crustContrast);
            if (material.HasProperty(ShapeContrastId)) block.SetFloat(ShapeContrastId, shapeContrast);
            if (material.HasProperty(FissureGlowStrengthId)) block.SetFloat(FissureGlowStrengthId, fissureGlowStrength);
            if (material.HasProperty(VertexWaveAmountId)) block.SetFloat(VertexWaveAmountId, vertexWaveAmount);
            if (material.HasProperty(VertexWaveSpeedId)) block.SetFloat(VertexWaveSpeedId, vertexWaveSpeed);
            renderer.SetPropertyBlock(block);
        }

        private void ApplyParticleRenderers(ParticleSystem system, Color tint, Color emission, float alpha)
        {
            if (system == null) return;
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            if (renderer == null || renderer.sharedMaterial == null) return;
            renderer.GetPropertyBlock(block);
            block.SetColor(TintColorId, tint);
            block.SetColor(EmissionColorId, emission);
            block.SetFloat(GlobalAlphaId, alpha);
            block.SetFloat(IntensityId, intensity);
            renderer.SetPropertyBlock(block);
        }

        private IEnumerator PlayRareBursts()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(Mathf.Min(burstInterval.x, burstInterval.y), Mathf.Max(burstInterval.x, burstInterval.y)));
                TriggerBurst();
            }
        }

        private void TriggerBurst()
        {
            if (rareBursts == null) return;
            int count = Random.Range(Mathf.Min(burstCount.x, burstCount.y), Mathf.Max(burstCount.x, burstCount.y) + 1);
            var emit = new ParticleSystem.EmitParams
            {
                position = new Vector3(Random.Range(-burstWidth * 0.5f, burstWidth * 0.5f), 0.1f, 0f),
                startColor = Color.Lerp(mainColor, edgeColor, Random.Range(0.6f, 1f))
            };
            rareBursts.Emit(emit, count);
            if (glowLight != null) StartCoroutine(PulseLight());
        }

        private IEnumerator PulseLight()
        {
            float start = baseLightIntensity;
            float peak = start * (1f + lightPulseAmount);
            for (float t = 0f; t < 0.12f; t += Time.deltaTime)
            {
                glowLight.intensity = Mathf.Lerp(start, peak, t / 0.12f);
                yield return null;
            }
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                glowLight.intensity = Mathf.Lerp(peak, start, t / 0.35f);
                yield return null;
            }
            glowLight.intensity = start;
        }
    }
}
