using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DG.Tweening;

namespace Bao.Hints
{
    public class HintAppearEffect : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpriteRenderer[] targetRenderers;
        [SerializeField] private Light2D targetLight;
        [SerializeField] private Transform transformToAnimate;

        [Header("Animation Settings")]
        [SerializeField] private float duration = 2.5f;
        [SerializeField] private AnimationCurve revealCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Shader Materialize Settings")]
        [SerializeField] private Material appearMaterial;
        [SerializeField] private string fallbackShaderName = "Custom/2D/MonsterMaterialize";
        [ColorUsage(true, true)] [SerializeField] private Color edgeColor = new Color(3.0f, 0.25f, 0.25f, 1f);
        [ColorUsage(true, true)] [SerializeField] private Color glowColor = new Color(1.2f, 0.05f, 0.05f, 1f);
        [ColorUsage(true, true)] [SerializeField] private Color sparkColor = new Color(2.8f, 1.05f, 0.08f, 1f);
        [SerializeField] private float dissolveNoiseScale = 8f;
        [SerializeField, Range(0.001f, 0.3f)] private float rimWidth = 0.08f;
        [SerializeField, Range(0f, 5f)] private float glowIntensity = 3.0f;

        [Header("Glow / Bloom")]
        [SerializeField] private VolumeProfile bloomProfile;

        [Header("Scale Animation")]
        [SerializeField] private bool animateScale = true;
        [SerializeField] private Vector3 targetScale = Vector3.one;
        [SerializeField] private Ease scaleEase = Ease.OutBack;

        [Header("Light Options")]
        [SerializeField] private float targetLightOuterRadius = 5f;
        [SerializeField] private float targetLightInnerRadius = 1f;

        [Header("Options")]
        [SerializeField] private bool playOnStart = false;

        private Coroutine revealRoutine;

        private struct RendererMaterialBackup
        {
            public Renderer renderer;
            public Material[] originalMaterials;
        }

        private GameObject localVolumeGo;

        private void Awake()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
                targetRenderers = GetComponentsInChildren<SpriteRenderer>(true);

            if (targetLight == null)
                targetLight = GetComponentInChildren<Light2D>(true);

            if (transformToAnimate == null)
                transformToAnimate = transform;

            if (targetLight != null)
            {
                targetLight.pointLightOuterRadius = 0f;
                targetLight.pointLightInnerRadius = 0f;
            }

            if (animateScale && transformToAnimate != null)
                transformToAnimate.localScale = Vector3.zero;

            CreateLocalBloom();
        }

        private void CreateLocalBloom()
        {
            if (bloomProfile == null) return;

            localVolumeGo = new GameObject("HintBloomVolume");
            localVolumeGo.transform.SetParent(transform, false);
            var volume = localVolumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = bloomProfile;
            volume.priority = 100f;
        }

        private void Start()
        {
            if (playOnStart)
                PlayAppearAnimation();
        }

        [ContextMenu("Play Hint Appear")]
        public void PlayAppearAnimation()
        {
            if (revealRoutine != null)
                StopCoroutine(revealRoutine);

            revealRoutine = StartCoroutine(AnimateRevealRoutine());

            if (animateScale && transformToAnimate != null)
            {
                transformToAnimate.DOKill();
                transformToAnimate.localScale = Vector3.zero;
                transformToAnimate.DOScale(targetScale, duration).SetEase(scaleEase);
            }
        }

        private IEnumerator AnimateRevealRoutine()
        {
            Shader materializeShader = appearMaterial != null ? appearMaterial.shader : Shader.Find(fallbackShaderName);
            if (materializeShader == null)
            {
                revealRoutine = null;
                yield break;
            }

            var backups = new List<RendererMaterialBackup>();
            var runtimeMaterials = new List<Material>();

            if (targetRenderers != null)
            {
                for (int i = 0; i < targetRenderers.Length; i++)
                {
                    var sr = targetRenderers[i];
                    if (sr == null || !sr.enabled) continue;

                    Material[] orig = sr.sharedMaterials;
                    backups.Add(new RendererMaterialBackup { renderer = sr, originalMaterials = orig });

                    Material[] newMats = new Material[orig.Length];
                    for (int j = 0; j < orig.Length; j++)
                    {
                        Material origMat = orig[j];
                        Material mat = appearMaterial != null ? new Material(appearMaterial) : new Material(materializeShader);

                        if (origMat != null)
                        {
                            if (origMat.HasProperty("_MainTex"))
                                mat.SetTexture("_MainTex", origMat.GetTexture("_MainTex"));
                            else if (origMat.mainTexture != null)
                                mat.mainTexture = origMat.mainTexture;
                        }

                        mat.SetColor("_EdgeColor", edgeColor);
                        mat.SetColor("_GlowColor", glowColor);
                        mat.SetColor("_SparkColor", sparkColor);
                        mat.SetFloat("_NoiseScale", dissolveNoiseScale);
                        mat.SetFloat("_EdgeWidth", rimWidth);
                        mat.SetFloat("_GlowIntensity", glowIntensity);
                        mat.SetFloat("_Alpha", 1f);
                        mat.SetFloat("_AppearAmount", 0f);

                        newMats[j] = mat;
                        runtimeMaterials.Add(mat);
                    }

                    sr.materials = newMats;
                }
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float curveValue = revealCurve.Evaluate(Mathf.Clamp01(elapsed / duration));

                for (int i = 0; i < runtimeMaterials.Count; i++)
                {
                    if (runtimeMaterials[i] != null)
                        runtimeMaterials[i].SetFloat("_AppearAmount", curveValue);
                }

                if (targetLight != null)
                {
                    targetLight.pointLightOuterRadius = curveValue * targetLightOuterRadius;
                    targetLight.pointLightInnerRadius = curveValue * targetLightInnerRadius;
                }

                yield return null;
            }

            for (int i = 0; i < runtimeMaterials.Count; i++)
            {
                if (runtimeMaterials[i] != null)
                    runtimeMaterials[i].SetFloat("_AppearAmount", 1f);
            }

            if (targetLight != null)
            {
                targetLight.pointLightOuterRadius = targetLightOuterRadius;
                targetLight.pointLightInnerRadius = targetLightInnerRadius;
            }

            // Restore original materials and release runtime instances.
            for (int i = 0; i < backups.Count; i++)
            {
                if (backups[i].renderer != null)
                    backups[i].renderer.materials = backups[i].originalMaterials;
            }

            for (int i = 0; i < runtimeMaterials.Count; i++)
            {
                if (runtimeMaterials[i] != null)
                    Destroy(runtimeMaterials[i]);
            }

            revealRoutine = null;
        }
    }
}
