using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Bao.FX.AcidLake
{
    [DisallowMultipleComponent]
    public sealed class MapHangAcidLakeVfxController : MonoBehaviour
    {
        private static readonly int MainColorId = Shader.PropertyToID("_MainColor");
        private static readonly int DeepColorId = Shader.PropertyToID("_DeepColor");
        private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int RippleSpeedId = Shader.PropertyToID("_RippleSpeed");
        private static readonly int RippleScaleId = Shader.PropertyToID("_RippleScale");
        private static readonly int DistortionId = Shader.PropertyToID("_DistortionStrength");
        private static readonly int BubbleStrengthId = Shader.PropertyToID("_BubbleStrength");
        private static readonly int SurfaceFoamId = Shader.PropertyToID("_SurfaceFoam");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionStrength");
        private static readonly int CellScaleId = Shader.PropertyToID("_CellScale");
        private static readonly int CellSpeedId = Shader.PropertyToID("_CellSpeed");
        private static readonly int RingWidthId = Shader.PropertyToID("_RingWidth");
        private static readonly int RingThresholdId = Shader.PropertyToID("_RingThreshold");
        private static readonly int FoamContrastId = Shader.PropertyToID("_FoamContrast");
        private static readonly int HighlightStrengthId = Shader.PropertyToID("_HighlightStrength");
        private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int GlobalAlphaId = Shader.PropertyToID("_GlobalAlpha");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        [Header("Renderers")]
        [SerializeField] private SpriteRenderer lakeRenderer;
        [SerializeField] private SpriteRenderer surfaceAccent;
        [SerializeField] private SpriteRenderer corrosiveRim;
        [SerializeField] private Light2D accentLight;

        [Header("Particles")]
        [SerializeField] private ParticleSystem simmerBubbles;
        [SerializeField] private ParticleSystem largeBubbles;
        [SerializeField] private ParticleSystem toxicMist;
        [SerializeField] private ParticleSystem rarePops;
        [SerializeField] private ParticleSystem acidMotes;

        [Header("Surface Palette")]
        [ColorUsage(true, true)] [SerializeField] private Color mainColor = new(0.10f, 0.58f, 0.025f, 0.9f);
        [ColorUsage(true, true)] [SerializeField] private Color deepColor = new(0.004f, 0.055f, 0.012f, 0.95f);
        [ColorUsage(true, true)] [SerializeField] private Color edgeColor = new(0.72f, 1.6f, 0.08f, 1f);
        [Range(0f, 1f)] [SerializeField] private float surfaceAlpha = 0.9f;
        [Range(0f, 2f)] [SerializeField] private float intensity = 1f;
        [Range(0f, 3f)] [SerializeField] private float rippleSpeed = 1.1f;
        [Range(1f, 30f)] [SerializeField] private float rippleScale = 6.8f;
        [Range(0f, 0.08f)] [SerializeField] private float distortionStrength = 0.018f;
        [Range(0f, 1.5f)] [SerializeField] private float bubbleStrength = 1.05f;
        [Range(0f, 1f)] [SerializeField] private float surfaceFoam = 0.72f;
        [Range(1f, 48f)] [SerializeField] private float cellScale = 13f;
        [Range(0f, 4f)] [SerializeField] private float cellSpeed = 1.15f;
        [Range(0.01f, 0.35f)] [SerializeField] private float ringWidth = 0.09f;
        [Range(0f, 1f)] [SerializeField] private float ringThreshold = 0.55f;
        [Range(0.1f, 5f)] [SerializeField] private float foamContrast = 1.6f;
        [Range(0f, 2f)] [SerializeField] private float highlightStrength = 0.6f;

        [Header("Rare Boil Events")]
        [SerializeField] private Vector2 popInterval = new(1.8f, 3.5f);
        [SerializeField] private Vector2Int popBurstCount = new(3, 6);
        [SerializeField] private float popWidth = 30f;
        [SerializeField] private float lightPulseAmount = 0.18f;

        private MaterialPropertyBlock block;
        private ParticleSystem[] continuousSystems;
        private float[] baselineRates;
        private float baseLightIntensity;
        private Coroutine popRoutine;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            continuousSystems = new[] { simmerBubbles, largeBubbles, toxicMist, acidMotes };
            CaptureParticleBaselines();
            if (accentLight != null) baseLightIntensity = accentLight.intensity;
            ApplyVisualState();
        }

        private void OnEnable()
        {
            if (block == null) block = new MaterialPropertyBlock();
            ApplyVisualState();
            if (Application.isPlaying && popRoutine == null) popRoutine = StartCoroutine(PlayRarePops());
        }

        private void OnDisable()
        {
            if (popRoutine == null) return;
            StopCoroutine(popRoutine);
            popRoutine = null;
        }

        private void OnValidate()
        {
            if (!Application.isPlaying && block != null) ApplyVisualState();
        }

        [ContextMenu("Validate Acid Lake References")]
        private void ValidateReferences()
        {
            Debug.Log($"[AcidLake] {name}: surface={(lakeRenderer ? "ok" : "missing")}, particles={(simmerBubbles && largeBubbles && toxicMist && rarePops && acidMotes ? "ok" : "missing")}", this);
            if (lakeRenderer != null && (lakeRenderer.sharedMaterial == null || !lakeRenderer.sharedMaterial.HasProperty(MainColorId)))
                Debug.LogWarning("[AcidLake] Surface material does not expose acid properties.", lakeRenderer);
        }

        private void CaptureParticleBaselines()
        {
            if (continuousSystems == null) return;
            baselineRates = new float[continuousSystems.Length];
            for (int i = 0; i < continuousSystems.Length; i++)
                if (continuousSystems[i] != null) baselineRates[i] = continuousSystems[i].emission.rateOverTimeMultiplier;
        }

        private void ApplyVisualState()
        {
            ApplySurface(lakeRenderer, 1f);
            ApplySurface(surfaceAccent, 0.52f);
            ApplySurface(corrosiveRim, 0.82f);
            ApplyParticleRenderers(simmerBubbles, mainColor, edgeColor, 1f);
            ApplyParticleRenderers(largeBubbles, edgeColor, edgeColor, 1f);
            ApplyParticleRenderers(toxicMist, mainColor, edgeColor, 0.72f);
            ApplyParticleRenderers(rarePops, edgeColor, edgeColor, 1f);
            ApplyParticleRenderers(acidMotes, edgeColor, edgeColor, 0.9f);

            if (continuousSystems == null || baselineRates == null) return;
            for (int i = 0; i < continuousSystems.Length; i++)
            {
                if (continuousSystems[i] == null) continue;
                var emission = continuousSystems[i].emission;
                emission.rateOverTimeMultiplier = baselineRates[i] * intensity;
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
            if (material.HasProperty(AlphaId)) block.SetFloat(AlphaId, surfaceAlpha * layerIntensity);
            if (material.HasProperty(RippleSpeedId)) block.SetFloat(RippleSpeedId, rippleSpeed);
            if (material.HasProperty(RippleScaleId)) block.SetFloat(RippleScaleId, rippleScale * (layerIntensity < 1f ? 1.35f : 1f));
            if (material.HasProperty(DistortionId)) block.SetFloat(DistortionId, distortionStrength);
            if (material.HasProperty(BubbleStrengthId)) block.SetFloat(BubbleStrengthId, bubbleStrength * layerIntensity);
            if (material.HasProperty(SurfaceFoamId)) block.SetFloat(SurfaceFoamId, surfaceFoam * layerIntensity);
            if (material.HasProperty(EmissionId)) block.SetFloat(EmissionId, intensity * layerIntensity);
            if (material.HasProperty(CellScaleId)) block.SetFloat(CellScaleId, cellScale * (layerIntensity < 1f ? 1.18f : 1f));
            if (material.HasProperty(CellSpeedId)) block.SetFloat(CellSpeedId, cellSpeed);
            if (material.HasProperty(RingWidthId)) block.SetFloat(RingWidthId, ringWidth);
            if (material.HasProperty(RingThresholdId)) block.SetFloat(RingThresholdId, ringThreshold);
            if (material.HasProperty(FoamContrastId)) block.SetFloat(FoamContrastId, foamContrast);
            if (material.HasProperty(HighlightStrengthId)) block.SetFloat(HighlightStrengthId, highlightStrength * layerIntensity);
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

        private IEnumerator PlayRarePops()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(Mathf.Min(popInterval.x, popInterval.y), Mathf.Max(popInterval.x, popInterval.y)));
                TriggerPop();
            }
        }

        private void TriggerPop()
        {
            if (rarePops == null) return;
            int count = Random.Range(Mathf.Min(popBurstCount.x, popBurstCount.y), Mathf.Max(popBurstCount.x, popBurstCount.y) + 1);
            var emit = new ParticleSystem.EmitParams
            {
                position = new Vector3(Random.Range(-popWidth * 0.5f, popWidth * 0.5f), -0.35f, 0f),
                startColor = Color.Lerp(mainColor, edgeColor, Random.Range(0.55f, 1f))
            };
            rarePops.Emit(emit, count);
            if (accentLight != null) StartCoroutine(PulseLight());
        }

        private IEnumerator PulseLight()
        {
            float start = baseLightIntensity;
            float peak = start * (1f + lightPulseAmount);
            for (float t = 0f; t < 0.14f; t += Time.deltaTime)
            {
                accentLight.intensity = Mathf.Lerp(start, peak, t / 0.14f);
                yield return null;
            }
            for (float t = 0f; t < 0.26f; t += Time.deltaTime)
            {
                accentLight.intensity = Mathf.Lerp(peak, start, t / 0.26f);
                yield return null;
            }
            accentLight.intensity = start;
        }
    }
}
