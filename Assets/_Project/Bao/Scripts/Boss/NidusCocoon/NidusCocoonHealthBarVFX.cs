using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bao.Bosses
{
    /// <summary>
    /// Presentation layer for the Nidus boss UI. All generated objects are runtime-only so the
    /// original decorative artwork remains editable while the organic motion stays consistent.
    /// </summary>
    [DisallowMultipleComponent]
    public class NidusCocoonHealthBarVFX : MonoBehaviour
    {
        [Header("Core References")]
        [SerializeField] private Slider mainSlider;
        [SerializeField] private Image fillImage;
        [SerializeField] private RectTransform barContainer;
        [SerializeField] private Image borderImage;
        [SerializeField] private TMP_Text bossNameText;

        [Header("Shaders")]
        [SerializeField] private Shader healthBarShader;
        [SerializeField] private Shader panelShader;
        [SerializeField] private Shader ornamentShader;

        [Header("Damage Feedback")]
        [SerializeField, Min(0.05f)] private float damageFlashDuration = 0.18f;
        [SerializeField, Min(0f)] private float delayedHold = 0.16f;
        [SerializeField, Min(0.1f)] private float delayedFollowSpeed = 5.5f;
        [SerializeField, Min(1f)] private float punchScale = 1.035f;
        [SerializeField, Min(0.1f)] private float punchRecovery = 9f;
        [SerializeField, Min(0f)] private float shakeMagnitude = 2.6f;
        [SerializeField, Min(0.05f)] private float shakeDuration = 0.16f;

        [Header("Phase Presentation")]
        [SerializeField, Min(0.2f)] private float phaseTransitionDuration = 1.15f;
        [SerializeField] private Color phase1TitleColor = new Color(0.84f, 0.97f, 0.70f, 1f);
        [SerializeField] private Color phase2TitleColor = new Color(1f, 0.34f, 0.10f, 1f);
        [SerializeField] private Color phase1OrnamentColor = new Color(0.48f, 0.86f, 0.12f, 1f);
        [SerializeField] private Color phase2OrnamentColor = new Color(1f, 0.18f, 0.035f, 1f);
        [SerializeField, Range(4, 24)] private int sporeCount = 14;

        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
        private static readonly int PhaseAmountId = Shader.PropertyToID("_PhaseAmount");
        private static readonly int DamageFlashId = Shader.PropertyToID("_DamageFlash");
        private static readonly int TransitionProgressId = Shader.PropertyToID("_TransitionProgress");
        private static readonly int CriticalPulseId = Shader.PropertyToID("_CriticalPulse");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        private Material healthMaterial;
        private Material delayedMaterial;
        private Material panelMaterial;
        private Material ornamentMaterial;
        private Image delayedImage;
        private Image panelImage;
        private RectTransform panelRect;
        private RectTransform sporeLayer;
        private CanvasGroup canvasGroup;
        private readonly List<Image> ornamentImages = new List<Image>();
        private readonly List<Spore> spores = new List<Spore>();

        private Vector2 basePosition;
        private Vector3 baseScale = Vector3.one;
        private Vector2 titleBasePosition;
        private float previousNormalized = 1f;
        private float delayedNormalized = 1f;
        private float damageDelayTimer;
        private float flashTimer;
        private float shakeTimer;
        private float currentPunch = 1f;
        private float phaseAmount;
        private float transitionSweep;
        private float phaseShock;
        private bool phaseTwo;
        private bool visualsBuilt;
        private Coroutine phaseRoutine;
        private Coroutine revealRoutine;

        private sealed class Spore
        {
            public RectTransform Rect;
            public Image Image;
            public float Speed;
            public float Drift;
            public float Phase;
            public float Life;
            public float MaxLife;
        }

        private void Awake()
        {
            ResolveReferences();
            BuildVisuals();
            ApplyPhasePresentation();
        }

        public void Configure(Slider slider, Image healthFill, TMP_Text title, RectTransform panelRoot)
        {
            if (slider != null) mainSlider = slider;
            if (healthFill != null) fillImage = healthFill;
            if (title != null) bossNameText = title;
            if (barContainer == null)
                barContainer = mainSlider != null ? mainSlider.transform.parent as RectTransform : panelRoot;

            ResolveReferences();
            BuildVisuals();
            ApplyPhasePresentation();
        }

        public void Initialize(int maxHealth)
        {
            float normalized = mainSlider != null && mainSlider.maxValue > 0f
                ? Mathf.Clamp01(mainSlider.value / mainSlider.maxValue)
                : 1f;

            previousNormalized = normalized;
            delayedNormalized = normalized;
            if (delayedImage != null) delayedImage.fillAmount = normalized;
            SetPhaseImmediate(1);
        }

        public void NotifyHealthChanged(int currentHealth, int maxHealth)
        {
            float normalized = maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f;
            if (normalized < previousNormalized - 0.0001f)
                PlayDamageFeedback();

            previousNormalized = normalized;
            damageDelayTimer = delayedHold;
        }

        public void SetPhase(int phaseIndex)
        {
            bool wantsPhaseTwo = phaseIndex >= 2;
            if (wantsPhaseTwo == phaseTwo && Mathf.Approximately(phaseAmount, wantsPhaseTwo ? 1f : 0f))
                return;

            phaseTwo = wantsPhaseTwo;
            if (phaseRoutine != null) StopCoroutine(phaseRoutine);
            phaseRoutine = StartCoroutine(PhaseTransitionRoutine(wantsPhaseTwo ? 1f : 0f));
        }

        public void PlayEngageReveal()
        {
            if (!isActiveAndEnabled) return;
            if (revealRoutine != null) StopCoroutine(revealRoutine);
            revealRoutine = StartCoroutine(RevealRoutine());
        }

        public void PlayDeathCollapse()
        {
            if (!isActiveAndEnabled) return;
            if (revealRoutine != null) StopCoroutine(revealRoutine);
            revealRoutine = StartCoroutine(DeathRoutine());
        }

        private void ResolveReferences()
        {
            if (mainSlider == null) mainSlider = GetComponentInChildren<Slider>(true);
            if (fillImage == null && mainSlider != null && mainSlider.fillRect != null)
                fillImage = mainSlider.fillRect.GetComponent<Image>();
            if (bossNameText == null)
            {
                TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
                foreach (TMP_Text text in texts)
                {
                    if (text.name.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        text.name.IndexOf("tên", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        bossNameText = text;
                        break;
                    }
                }
                if (bossNameText == null && texts.Length > 0) bossNameText = texts[0];
            }
            if (barContainer == null && mainSlider != null)
                barContainer = mainSlider.transform.parent as RectTransform;
            if (barContainer == null) barContainer = transform as RectTransform;
        }

        private void BuildVisuals()
        {
            if (visualsBuilt || fillImage == null || barContainer == null) return;
            visualsBuilt = true;

            basePosition = barContainer.anchoredPosition;
            baseScale = barContainer.localScale;
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            BuildHealthMaterials();
            BuildDelayedFill();
            BuildOrganicPanel();
            CacheOrnaments();
            BuildSpores();

            if (bossNameText != null)
            {
                RectTransform titleRect = bossNameText.rectTransform;
                titleBasePosition = titleRect.anchoredPosition;
                Outline outline = bossNameText.GetComponent<Outline>();
                if (outline == null) outline = bossNameText.gameObject.AddComponent<Outline>();
                outline.effectDistance = new Vector2(1.6f, -1.6f);
                outline.useGraphicAlpha = true;
            }
        }

        private void BuildHealthMaterials()
        {
            Shader shader = healthBarShader != null ? healthBarShader : Shader.Find("Bao/UI/NidusCocoonHealthBar");
            if (shader == null)
            {
                Debug.LogWarning("[NidusCocoonHealthBarVFX] Shader Bao/UI/NidusCocoonHealthBar not found, using default UI material fallback.", this);
            }
            else
            {
                healthMaterial = new Material(shader) { name = "Nidus Health Runtime", hideFlags = HideFlags.DontSave };
                ConfigureHealthMaterial(healthMaterial, false);
                fillImage.material = healthMaterial;

                delayedMaterial = new Material(shader) { name = "Nidus Delayed Health Runtime", hideFlags = HideFlags.DontSave };
                ConfigureHealthMaterial(delayedMaterial, true);
            }
            fillImage.color = Color.white;
        }

        private static void ConfigureHealthMaterial(Material material, bool delayed)
        {
            // Phase one follows Nidus' acid glow; phase two is blood red.
            material.SetColor("_CoreColor", delayed ? new Color(0.12f, 0.28f, 0.02f, 1f) : new Color(0.45f, 1.25f, 0.08f, 1f));
            material.SetColor("_ToxicColor", delayed ? new Color(0.28f, 0.42f, 0.04f, 1f) : new Color(0.72f, 1.45f, 0.12f, 1f));
            material.SetColor("_PhaseColor", delayed ? new Color(0.38f, 0.018f, 0.006f, 1f) : new Color(1.25f, 0.07f, 0.01f, 1f));
            material.SetColor("_VeinColor", delayed ? new Color(0.02f, 0.09f, 0.005f, 1f) : new Color(0.025f, 0.22f, 0.005f, 1f));
            material.SetColor("_GlowColor", delayed ? new Color(0.28f, 0.55f, 0.04f, 1f) : new Color(0.64f, 1.25f, 0.09f, 1f));
            material.SetFloat("_FlowSpeed", delayed ? 1.35f : 2.8f);
            material.SetFloat("_VeinScale", delayed ? 13f : 21f);
            material.SetFloat("_GlowWidth", delayed ? 0.014f : 0.032f);
            material.SetFloat("_GlowIntensity", delayed ? 1.1f : 2.7f);
            material.SetFloat("_PulseSpeed", delayed ? 2.2f : 4.1f);
            material.SetFloat("_PulseStrength", delayed ? 0.10f : 0.28f);
        }

        private void BuildDelayedFill()
        {
            GameObject delayedObject = Instantiate(fillImage.gameObject, fillImage.transform.parent);
            delayedObject.name = "Nidus_DelayedBiomass";
            delayedImage = delayedObject.GetComponent<Image>();
            delayedImage.raycastTarget = false;
            delayedImage.color = Color.white;
            delayedImage.type = Image.Type.Filled;
            delayedImage.fillMethod = Image.FillMethod.Horizontal;
            delayedImage.fillOrigin = 0;
            delayedImage.fillClockwise = true;
            delayedImage.fillAmount = 1f;
            if (delayedMaterial != null) delayedImage.material = delayedMaterial;
            delayedObject.transform.SetSiblingIndex(fillImage.transform.GetSiblingIndex());
        }

        private void BuildOrganicPanel()
        {
            GameObject panelObject = new GameObject("Nidus_OrganicPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.SetParent(barContainer, false);
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, -36f);
            panelRect.sizeDelta = new Vector2(930f, 158f);
            panelRect.SetAsFirstSibling();

            panelImage = panelObject.GetComponent<Image>();
            panelImage.raycastTarget = false;
            panelImage.color = Color.white;
            Shader shader = panelShader != null ? panelShader : Shader.Find("Bao/UI/NidusCocoonPanel");
            if (shader != null)
            {
                panelMaterial = new Material(shader) { name = "Nidus Organic Panel Runtime", hideFlags = HideFlags.DontSave };
                panelImage.material = panelMaterial;
            }

            GameObject sporesObject = new GameObject("Nidus_Spores", typeof(RectTransform));
            sporeLayer = sporesObject.GetComponent<RectTransform>();
            sporeLayer.SetParent(panelRect, false);
            sporeLayer.anchorMin = Vector2.zero;
            sporeLayer.anchorMax = Vector2.one;
            sporeLayer.offsetMin = Vector2.zero;
            sporeLayer.offsetMax = Vector2.zero;
        }

        private void CacheOrnaments()
        {
            Shader ornamentShaderRef = ornamentShader != null ? ornamentShader : Shader.Find("Bao/UI/NidusCocoonOrnament");
            if (ornamentShaderRef != null)
                ornamentMaterial = new Material(ornamentShaderRef) { name = "Nidus Ornament Runtime", hideFlags = HideFlags.DontSave };

            Image[] images = barContainer.GetComponentsInChildren<Image>(true);
            foreach (Image image in images)
            {
                if (image == null || image == fillImage || image == delayedImage || image == panelImage) continue;
                string lowerName = image.name.ToLowerInvariant();
                if (lowerName.Contains("bg") || lowerName.Contains("background") || lowerName.Contains("fill")) continue;
                if (image.sprite != null && image.gameObject.activeSelf)
                {
                    ornamentImages.Add(image);
                    if (ornamentMaterial != null)
                    {
                        float alpha = image.color.a;
                        image.material = ornamentMaterial;
                        image.color = new Color(1f, 1f, 1f, alpha);
                    }
                }
            }
        }

        private void BuildSpores()
        {
            if (sporeLayer == null) return;
            Sprite sprite = GetSporeSprite();
            System.Random random = new System.Random(GetInstanceID());
            for (int i = 0; i < sporeCount; i++)
            {
                GameObject go = new GameObject("Spore_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform rect = go.GetComponent<RectTransform>();
                rect.SetParent(sporeLayer, false);
                float size = Mathf.Lerp(3f, 9f, (float)random.NextDouble());
                rect.sizeDelta = new Vector2(size, size);
                Image image = go.GetComponent<Image>();
                image.sprite = sprite;
                image.raycastTarget = false;
                Spore spore = new Spore
                {
                    Rect = rect,
                    Image = image,
                    Speed = Mathf.Lerp(8f, 20f, (float)random.NextDouble()),
                    Drift = Mathf.Lerp(4f, 13f, (float)random.NextDouble()),
                    Phase = Mathf.Lerp(0f, Mathf.PI * 2f, (float)random.NextDouble()),
                    Life = Mathf.Lerp(0f, 7f, (float)random.NextDouble()),
                    MaxLife = Mathf.Lerp(4.5f, 8f, (float)random.NextDouble())
                };
                ResetSpore(spore, random, true);
                spores.Add(spore);
            }
        }

        private void Update()
        {
            if (!visualsBuilt) return;
            float dt = Time.unscaledDeltaTime;
            float normalized = GetNormalizedHealth();

            if (normalized < previousNormalized - 0.001f)
            {
                PlayDamageFeedback();
                previousNormalized = normalized;
            }

            flashTimer = Mathf.Max(0f, flashTimer - dt);
            shakeTimer = Mathf.Max(0f, shakeTimer - dt);
            damageDelayTimer = Mathf.Max(0f, damageDelayTimer - dt);
            currentPunch = Mathf.MoveTowards(currentPunch, 1f, punchRecovery * dt);

            if (damageDelayTimer <= 0f)
                delayedNormalized = Mathf.Lerp(delayedNormalized, normalized, 1f - Mathf.Exp(-delayedFollowSpeed * dt));
            else
                delayedNormalized = Mathf.Max(delayedNormalized, normalized);

            Color phaseTint = Color.Lerp(
                new Color(0.62f, 1f, 0.08f, 1f),
                new Color(1f, 0.02f, 0.01f, 1f),
                phaseAmount);
            if (fillImage != null) fillImage.color = phaseTint;
            if (delayedImage != null)
            {
                delayedImage.fillAmount = delayedNormalized;
                delayedImage.color = phaseTint;
            }

            float critical = Mathf.InverseLerp(0.34f, 0.08f, normalized);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (phaseTwo ? 8.5f : 4.2f));
            UpdateMaterials(normalized, critical * pulse);
            UpdateTransformFeedback(critical, pulse);
            UpdateTextAndOrnaments(critical, pulse);
            UpdateSpores(dt);
        }

        private float GetNormalizedHealth()
        {
            if (mainSlider == null || mainSlider.maxValue <= 0f) return 1f;
            return Mathf.Clamp01(mainSlider.value / mainSlider.maxValue);
        }

        private void PlayDamageFeedback()
        {
            flashTimer = damageFlashDuration;
            shakeTimer = shakeDuration;
            currentPunch = punchScale;
            damageDelayTimer = delayedHold;
        }

        private void UpdateMaterials(float normalized, float criticalPulse)
        {
            float flash = damageFlashDuration > 0f ? flashTimer / damageFlashDuration : 0f;
            if (healthMaterial != null)
            {
                healthMaterial.SetFloat(FillAmountId, normalized);
                healthMaterial.SetFloat(PhaseAmountId, phaseAmount);
                healthMaterial.SetFloat(DamageFlashId, flash);
                healthMaterial.SetFloat(TransitionProgressId, transitionSweep);
                healthMaterial.SetFloat(CriticalPulseId, criticalPulse);
            }
            if (delayedMaterial != null)
            {
                delayedMaterial.SetFloat(FillAmountId, delayedNormalized);
                delayedMaterial.SetFloat(PhaseAmountId, phaseAmount);
                delayedMaterial.SetFloat(TransitionProgressId, transitionSweep);
                delayedMaterial.SetFloat(CriticalPulseId, criticalPulse * 0.35f);
            }
            if (panelMaterial != null)
            {
                panelMaterial.SetFloat(PhaseAmountId, phaseAmount);
                panelMaterial.SetFloat(DamageFlashId, flash);
                panelMaterial.SetFloat(TransitionProgressId, transitionSweep);
                panelMaterial.SetFloat(CriticalPulseId, criticalPulse);
                panelMaterial.SetFloat(IntensityId, phaseTwo ? 1.28f : 1f);
            }
            if (ornamentMaterial != null)
            {
                ornamentMaterial.SetFloat(PhaseAmountId, phaseAmount);
                ornamentMaterial.SetFloat(CriticalPulseId, criticalPulse);
                ornamentMaterial.SetFloat(TransitionProgressId, transitionSweep);
            }
        }

        private void UpdateTransformFeedback(float critical, float pulse)
        {
            float damageStrength = shakeDuration > 0f ? shakeTimer / shakeDuration : 0f;
            float criticalScale = 1f + critical * pulse * (phaseTwo ? 0.012f : 0.005f);
            barContainer.localScale = baseScale * currentPunch * criticalScale * (1f + phaseShock * 0.045f);

            float shake = shakeMagnitude * damageStrength + phaseShock * 5.5f;
            if (shake <= 0.01f)
            {
                barContainer.anchoredPosition = basePosition;
                return;
            }

            float time = Time.unscaledTime * 42f;
            barContainer.anchoredPosition = basePosition + new Vector2(
                Mathf.Sin(time * 1.17f) * shake,
                Mathf.Cos(time * 1.63f) * shake * 0.38f);
        }

        private void UpdateTextAndOrnaments(float critical, float pulse)
        {
            Color titleColor = Color.Lerp(phase1TitleColor, phase2TitleColor, phaseAmount);
            titleColor = Color.Lerp(titleColor, new Color(1f, 0.82f, 0.38f, 1f), critical * pulse * 0.32f);
            if (bossNameText != null)
            {
                bossNameText.color = titleColor;
                Outline outline = bossNameText.GetComponent<Outline>();
                if (outline != null)
                    outline.effectColor = Color.Lerp(new Color(0.015f, 0.10f, 0.01f, 0.94f), new Color(0.25f, 0.008f, 0f, 0.98f), phaseAmount);
                float glitch = phaseShock * 2.8f;
                bossNameText.rectTransform.anchoredPosition = titleBasePosition + new Vector2(
                    Mathf.Sin(Time.unscaledTime * 67f) * glitch,
                    Mathf.Cos(Time.unscaledTime * 53f) * glitch * 0.35f);
            }

            Color ornamentColor = Color.Lerp(phase1OrnamentColor, phase2OrnamentColor, phaseAmount);
            ornamentColor = Color.Lerp(ornamentColor, Color.white, critical * pulse * 0.18f);
            foreach (Image image in ornamentImages)
            {
                if (image == null) continue;
                if (ornamentMaterial != null)
                {
                    Color neutral = image.color;
                    neutral.r = neutral.g = neutral.b = 1f;
                    image.color = neutral;
                    continue;
                }
                Color color = ornamentColor;
                color.a = image.color.a;
                image.color = color;
            }

            if (borderImage != null)
                borderImage.color = ornamentColor;
        }

        private void UpdateSpores(float dt)
        {
            float activity = Mathf.Lerp(0.65f, 1.75f, phaseAmount);
            Color color = Color.Lerp(new Color(0.52f, 1f, 0.12f, 0.20f), new Color(1f, 0.18f, 0.025f, 0.36f), phaseAmount);
            for (int i = 0; i < spores.Count; i++)
            {
                Spore spore = spores[i];
                spore.Life += dt * activity;
                if (spore.Life >= spore.MaxLife)
                {
                    ResetSpore(spore, null, false);
                    continue;
                }

                Vector2 position = spore.Rect.anchoredPosition;
                position.y += spore.Speed * dt * activity;
                position.x += Mathf.Sin(Time.unscaledTime * 1.8f + spore.Phase) * spore.Drift * dt;
                spore.Rect.anchoredPosition = position;
                float life01 = spore.Life / spore.MaxLife;
                float fade = Mathf.Sin(life01 * Mathf.PI);
                Color current = color;
                current.a *= fade;
                spore.Image.color = current;
                float scale = Mathf.Lerp(0.65f, 1.25f, life01) * (1f + phaseAmount * 0.25f);
                spore.Rect.localScale = Vector3.one * scale;
            }
        }

        private void ResetSpore(Spore spore, System.Random random, bool randomizeLife)
        {
            float rx = random != null ? (float)random.NextDouble() : Mathf.Repeat(Mathf.Sin(spore.Phase * 17.31f + Time.unscaledTime) * 43758.54f, 1f);
            float ry = random != null ? (float)random.NextDouble() : Mathf.Repeat(Mathf.Sin(spore.Phase * 9.71f + Time.unscaledTime * 0.37f) * 19341.17f, 1f);
            spore.Life = randomizeLife ? ry * spore.MaxLife : 0f;
            spore.Rect.anchoredPosition = new Vector2(Mathf.Lerp(-430f, 430f, rx), Mathf.Lerp(-68f, -42f, ry));
        }

        private IEnumerator PhaseTransitionRoutine(float target)
        {
            float start = phaseAmount;
            float elapsed = 0f;
            while (elapsed < phaseTransitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / phaseTransitionDuration);
                float eased = t * t * (3f - 2f * t);
                phaseAmount = Mathf.Lerp(start, target, eased);
                transitionSweep = t;
                phaseShock = Mathf.Sin(t * Mathf.PI) * (target > start ? 1f : 0.55f);
                yield return null;
            }

            phaseAmount = target;
            transitionSweep = 0f;
            phaseShock = 0f;
            ApplyPhasePresentation();
            phaseRoutine = null;
        }

        private IEnumerator RevealRoutine()
        {
            canvasGroup.alpha = 0f;
            float elapsed = 0f;
            const float duration = 0.55f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                canvasGroup.alpha = eased;
                barContainer.localScale = baseScale * Mathf.Lerp(0.92f, 1f, eased);
                yield return null;
            }
            canvasGroup.alpha = 1f;
            revealRoutine = null;
        }

        private IEnumerator DeathRoutine()
        {
            float elapsed = 0f;
            const float duration = 0.7f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                canvasGroup.alpha = 1f - t;
                barContainer.localScale = baseScale * Mathf.Lerp(1f, 0.88f, t);
                yield return null;
            }
            revealRoutine = null;
        }

        private void SetPhaseImmediate(int phaseIndex)
        {
            phaseTwo = phaseIndex >= 2;
            phaseAmount = phaseTwo ? 1f : 0f;
            transitionSweep = 0f;
            phaseShock = 0f;
            ApplyPhasePresentation();
        }

        private void ApplyPhasePresentation()
        {
            if (fillImage != null)
            {
                Color phaseTint = Color.Lerp(
                    new Color(0.62f, 1f, 0.08f, 1f),
                    new Color(1f, 0.02f, 0.01f, 1f),
                    phaseAmount);
                fillImage.color = phaseTint;
            }
        }

        private static Sprite GetSporeSprite()
        {
            const int size = 24;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Nidus UI Spore",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.3f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = "Nidus UI Spore";
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private void OnDestroy()
        {
            if (healthMaterial != null) Destroy(healthMaterial);
            if (delayedMaterial != null) Destroy(delayedMaterial);
            if (panelMaterial != null) Destroy(panelMaterial);
            if (ornamentMaterial != null) Destroy(ornamentMaterial);
        }
    }
}
