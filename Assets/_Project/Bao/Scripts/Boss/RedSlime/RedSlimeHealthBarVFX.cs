using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bao.Bosses
{
    /// <summary>
    /// Runtime presentation for the Red Slime boss bar. The authored UI artwork stays intact while
    /// the component adds a viscous fill shader, delayed-damage layer, organic panel and droplets.
    /// It also preserves the optional multi-layer x4/x3/x2/x1 health-bar behaviour.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RedSlimeHealthBarVFX : MonoBehaviour
    {
        [Header("Multi-Layer Health")]
        [SerializeField] private bool enableMultiLayer = true;
        [SerializeField, Range(1, 10)] private int totalLayers = 4;

        [Header("Core References")]
        [SerializeField] private Slider mainSlider;
        [SerializeField] private Slider easeSlider;
        [SerializeField] private Image fillImage;
        [SerializeField] private RectTransform barContainer;
        [SerializeField] private Image borderImage;
        [SerializeField] private TMP_Text bossNameText;

        [Header("Layer Badge (x4 / x3 / x2 / x1)")]
        [SerializeField] private TMP_Text layerBadgeText;
        [SerializeField] private bool showBadgeInBossName = true;

        [Header("Shaders")]
        [SerializeField] private Shader healthBarShader;
        [SerializeField] private Shader panelShader;
        [SerializeField] private Shader ornamentShader;

        [Header("Layer Palette")]
        [SerializeField] private Color layer4Color = new Color(1f, 0.08f, 0.055f, 1f);
        [SerializeField] private Color layer3Color = new Color(0.02f, 0.78f, 1f, 1f);
        [SerializeField] private Color layer2Color = new Color(1f, 0.56f, 0.025f, 1f);
        [SerializeField] private Color layer1Color = new Color(1f, 0.025f, 0.42f, 1f);

        [Header("Damage Feedback")]
        [SerializeField, Min(0.05f)] private float damageFlashDuration = 0.18f;
        [SerializeField, Min(0f)] private float delayedHold = 0.16f;
        [SerializeField, Min(0.1f)] private float delayedFollowSpeed = 5.8f;
        [SerializeField, Min(1f)] private float punchScale = 1.04f;
        [SerializeField, Min(0.1f)] private float punchRecovery = 9f;
        [SerializeField, Min(0f)] private float shakeMagnitude = 2.7f;
        [SerializeField, Min(0.05f)] private float shakeDuration = 0.16f;

        [Header("Living Slime Presentation")]
        [SerializeField, Min(0.2f)] private float phaseTransitionDuration = 1.05f;
        [SerializeField, Range(6, 28)] private int ambientDropletCount = 18;
        [SerializeField] private Color phase1TitleColor = new Color(1f, 0.72f, 0.62f, 1f);
        [SerializeField] private Color phase2TitleColor = new Color(1f, 0.35f, 0.72f, 1f);

        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
        private static readonly int PhaseAmountId = Shader.PropertyToID("_PhaseAmount");
        private static readonly int DamageFlashId = Shader.PropertyToID("_DamageFlash");
        private static readonly int TransitionProgressId = Shader.PropertyToID("_TransitionProgress");
        private static readonly int CriticalPulseId = Shader.PropertyToID("_CriticalPulse");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int PrimaryColorId = Shader.PropertyToID("_PrimaryColor");
        private static readonly int SecondaryColorId = Shader.PropertyToID("_SecondaryColor");
        private static readonly int AccentColorId = Shader.PropertyToID("_AccentColor");
        private static readonly int DeepColorId = Shader.PropertyToID("_DeepColor");

        private Material healthMaterial;
        private Material delayedMaterial;
        private Material panelMaterial;
        private Material ornamentMaterial;
        private Image delayedImage;
        private Image panelImage;
        private RectTransform panelRect;
        private RectTransform particleLayer;
        private CanvasGroup canvasGroup;
        private Sprite softParticleSprite;
        private Texture2D softParticleTexture;

        private readonly List<Image> ornamentImages = new List<Image>();
        private readonly List<SlimeParticle> particles = new List<SlimeParticle>();
        private System.Random random;

        private Vector2 basePosition;
        private Vector3 baseScale = Vector3.one;
        private Vector2 titleBasePosition;
        private int cachedMaxHealth = 1;
        private int cachedCurrentHealth = 1;
        private int currentLayer = 4;
        private float currentLayerFill = 1f;
        private float overallNormalized = 1f;
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
        private string originalBossName = "Red Slime";
        private Coroutine transitionRoutine;
        private Coroutine revealRoutine;

        private sealed class SlimeParticle
        {
            public RectTransform Rect;
            public Image Image;
            public float Speed;
            public float Drift;
            public float Phase;
            public float Life;
            public float MaxLife;
            public float BaseSize;
            public float RotationSpeed;
            public bool Bubble;
        }

        public bool EnableMultiLayer
        {
            get => enableMultiLayer;
            set => enableMultiLayer = value;
        }

        public int TotalLayers
        {
            get => totalLayers;
            set => totalLayers = Mathf.Max(1, value);
        }

        private void Awake()
        {
            ResolveReferences();
            BuildVisuals();
            ApplyCurrentPalette();
        }

        public void Configure(
            Slider slider,
            Image healthFill,
            TMP_Text title,
            RectTransform panelRoot,
            TMP_Text badgeText = null,
            Slider delaySlider = null)
        {
            if (slider != null) mainSlider = slider;
            if (delaySlider != null) easeSlider = delaySlider;
            if (healthFill != null) fillImage = healthFill;
            if (title != null) bossNameText = title;
            if (badgeText != null) layerBadgeText = badgeText;
            if (barContainer == null)
                barContainer = mainSlider != null ? mainSlider.transform.parent as RectTransform : panelRoot;

            ResolveReferences();
            BuildVisuals();
            ApplyCurrentPalette();
        }

        public void SetMultiLayerMode(bool enabled, int layers = 4)
        {
            enableMultiLayer = enabled;
            totalLayers = Mathf.Max(1, layers);
            UpdateDisplay(cachedCurrentHealth, cachedMaxHealth, true);
        }

        public void Initialize(int maxHealth)
        {
            cachedMaxHealth = Mathf.Max(1, maxHealth);
            cachedCurrentHealth = cachedMaxHealth;
            currentLayer = enableMultiLayer ? totalLayers : 1;
            currentLayerFill = 1f;
            overallNormalized = 1f;
            delayedNormalized = 1f;
            CaptureOriginalBossName();
            SetPhaseImmediate(1);
            UpdateDisplay(cachedCurrentHealth, cachedMaxHealth, true);
        }

        public void NotifyHealthChanged(int currentHealth, int maxHealth)
        {
            if (maxHealth > 0) cachedMaxHealth = maxHealth;
            int previousHealth = cachedCurrentHealth;
            int previousLayer = currentLayer;
            cachedCurrentHealth = Mathf.Clamp(currentHealth, 0, cachedMaxHealth);

            UpdateDisplay(cachedCurrentHealth, cachedMaxHealth, false);

            if (cachedCurrentHealth < previousHealth)
                PlayDamageFeedback();

            if (currentLayer != previousLayer)
            {
                delayedNormalized = 1f;
                if (delayedImage != null) delayedImage.fillAmount = 1f;
                PlayLayerTransition();
            }
        }

        public void SetPhase(int phaseIndex)
        {
            bool wantsPhaseTwo = phaseIndex >= 2;
            float target = wantsPhaseTwo ? 1f : 0f;
            if (wantsPhaseTwo == phaseTwo && Mathf.Approximately(phaseAmount, target)) return;

            phaseTwo = wantsPhaseTwo;
            if (transitionRoutine != null) StopCoroutine(transitionRoutine);
            transitionRoutine = StartCoroutine(PhaseTransitionRoutine(target));
        }

        public void PlayEngageReveal()
        {
            if (!isActiveAndEnabled || canvasGroup == null || barContainer == null) return;
            if (revealRoutine != null) StopCoroutine(revealRoutine);
            revealRoutine = StartCoroutine(RevealRoutine());
        }

        public void PlayDeathCollapse()
        {
            if (!isActiveAndEnabled || canvasGroup == null || barContainer == null) return;
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
                    if (text == layerBadgeText) continue;
                    string lowerName = text.name.ToLowerInvariant();
                    if (lowerName.Contains("boss") || lowerName.Contains("tên") || lowerName.Contains("name"))
                    {
                        bossNameText = text;
                        break;
                    }
                }

                if (bossNameText == null)
                {
                    foreach (TMP_Text text in texts)
                    {
                        if (text == layerBadgeText) continue;
                        bossNameText = text;
                        break;
                    }
                }
            }

            if (barContainer == null && mainSlider != null)
                barContainer = mainSlider.transform.parent as RectTransform;
            if (barContainer == null) barContainer = transform as RectTransform;
            CaptureOriginalBossName();
        }

        private void CaptureOriginalBossName()
        {
            if (bossNameText == null || string.IsNullOrWhiteSpace(bossNameText.text)) return;
            string capturedName = bossNameText.text;
            int badgeIndex = capturedName.IndexOf("<color", StringComparison.OrdinalIgnoreCase);
            if (badgeIndex > 0) capturedName = capturedName.Substring(0, badgeIndex).Trim();
            if (!string.IsNullOrWhiteSpace(capturedName)) originalBossName = capturedName;
        }

        private void BuildVisuals()
        {
            if (visualsBuilt || fillImage == null || barContainer == null) return;
            visualsBuilt = true;
            random = new System.Random(GetInstanceID());
            basePosition = barContainer.anchoredPosition;
            baseScale = barContainer.localScale;
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            BuildHealthMaterials();
            BuildDelayedFill();
            BuildLivingPanel();
            CacheOrnaments();
            BuildAmbientParticles();

            if (bossNameText != null)
            {
                titleBasePosition = bossNameText.rectTransform.anchoredPosition;
                Outline outline = bossNameText.GetComponent<Outline>();
                if (outline == null) outline = bossNameText.gameObject.AddComponent<Outline>();
                outline.effectDistance = new Vector2(1.7f, -1.7f);
                outline.useGraphicAlpha = true;
            }
        }

        private void BuildHealthMaterials()
        {
            Shader shader = healthBarShader != null ? healthBarShader : Shader.Find("Bao/UI/RedSlimeHealthBar");
            if (shader == null)
            {
                Debug.LogWarning("[RedSlimeHealthBarVFX] Shader Bao/UI/RedSlimeHealthBar not found, using default UI material fallback.", this);
            }
            else
            {
                healthMaterial = new Material(shader)
                {
                    name = "Red Slime Health Runtime",
                    hideFlags = HideFlags.DontSave
                };
                ConfigureHealthMaterial(healthMaterial, false);
                fillImage.material = healthMaterial;

                delayedMaterial = new Material(shader)
                {
                    name = "Red Slime Delayed Health Runtime",
                    hideFlags = HideFlags.DontSave
                };
                ConfigureHealthMaterial(delayedMaterial, true);
            }
            fillImage.color = Color.white;
        }

        private static void ConfigureHealthMaterial(Material material, bool delayed)
        {
            material.SetFloat("_FlowSpeed", delayed ? 0.75f : 2.15f);
            material.SetFloat("_CellScale", delayed ? 9f : 16f);
            material.SetFloat("_MembraneWidth", delayed ? 0.018f : 0.045f);
            material.SetFloat("_GlowIntensity", delayed ? 0.85f : 2.6f);
            material.SetFloat("_PulseSpeed", delayed ? 2.1f : 4.8f);
            material.SetFloat("_PulseStrength", delayed ? 0.07f : 0.24f);
            material.SetFloat("_LowThreshold", 0.24f);
        }

        private void BuildDelayedFill()
        {
            if (delayedImage == null && fillImage != null && fillImage.transform.parent != null)
            {
                Transform existingDelayed = fillImage.transform.parent.Find("RedSlime_DelayedGel");
                GameObject delayedObject = existingDelayed != null ? existingDelayed.gameObject : Instantiate(fillImage.gameObject, fillImage.transform.parent);
                delayedObject.name = "RedSlime_DelayedGel";
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
        }

        private void BuildLivingPanel()
        {
            if (panelRect == null && barContainer != null)
            {
                Transform existingPanel = barContainer.Find("RedSlime_LivingGelPanel");
                GameObject panelObject = existingPanel != null ? existingPanel.gameObject : new GameObject(
                    "RedSlime_LivingGelPanel",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                panelRect = panelObject.GetComponent<RectTransform>();
                panelRect.SetParent(barContainer, false);
                panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.anchoredPosition = new Vector2(0f, -35f);
                panelRect.sizeDelta = new Vector2(938f, 162f);
                panelRect.SetAsFirstSibling();

                panelImage = panelObject.GetComponent<Image>();
                panelImage.raycastTarget = false;
                panelImage.color = Color.white;
                Shader pShader = panelShader != null ? panelShader : Shader.Find("Bao/UI/RedSlimePanel");
                if (pShader != null)
                {
                    panelMaterial = new Material(pShader)
                    {
                        name = "Red Slime Living Panel Runtime",
                        hideFlags = HideFlags.DontSave
                    };
                    panelImage.material = panelMaterial;
                }

                Transform existingParticles = panelRect.Find("RedSlime_UI_Droplets");
                GameObject particlesObject = existingParticles != null ? existingParticles.gameObject : new GameObject("RedSlime_UI_Droplets", typeof(RectTransform));
                particleLayer = particlesObject.GetComponent<RectTransform>();
                particleLayer.SetParent(panelRect, false);
                particleLayer.anchorMin = Vector2.zero;
                particleLayer.anchorMax = Vector2.one;
                particleLayer.offsetMin = Vector2.zero;
                particleLayer.offsetMax = Vector2.zero;
            }
        }

        private void CacheOrnaments()
        {
            Shader oShader = ornamentShader != null ? ornamentShader : Shader.Find("Bao/UI/RedSlimeOrnament");
            if (oShader != null)
            {
                ornamentMaterial = new Material(oShader)
                {
                    name = "Red Slime Ornament Runtime",
                    hideFlags = HideFlags.DontSave
                };
            }

            Image[] images = barContainer.GetComponentsInChildren<Image>(true);
            foreach (Image image in images)
            {
                if (image == null || image == fillImage || image == delayedImage || image == panelImage) continue;
                string lowerName = image.name.ToLowerInvariant();
                if (lowerName.Contains("bg") || lowerName.Contains("background") || lowerName.Contains("fill")) continue;
                if (image.sprite == null || !image.gameObject.activeSelf) continue;

                ornamentImages.Add(image);
                if (ornamentMaterial != null)
                {
                    float alpha = image.color.a;
                    image.material = ornamentMaterial;
                    image.color = new Color(1f, 1f, 1f, alpha);
                }
            }
        }

        private void BuildAmbientParticles()
        {
            if (particleLayer == null) return;
            softParticleSprite = CreateSoftParticleSprite(out softParticleTexture);

            for (int i = 0; i < ambientDropletCount; i++)
            {
                GameObject go = new GameObject("GelDroplet_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform rect = go.GetComponent<RectTransform>();
                rect.SetParent(particleLayer, false);
                Image image = go.GetComponent<Image>();
                image.sprite = softParticleSprite;
                image.raycastTarget = false;

                SlimeParticle particle = new SlimeParticle
                {
                    Rect = rect,
                    Image = image,
                    Speed = RandomRange(7f, 21f),
                    Drift = RandomRange(4f, 16f),
                    Phase = RandomRange(0f, Mathf.PI * 2f),
                    MaxLife = RandomRange(4.2f, 8.5f),
                    BaseSize = RandomRange(4f, 12f),
                    RotationSpeed = RandomRange(-35f, 35f),
                    Bubble = i % 3 == 0
                };
                ResetParticle(particle, true);
                particles.Add(particle);
            }
        }

        private void UpdateDisplay(int currentHealth, int maxHealth, bool isInitial)
        {
            if (maxHealth <= 0) return;
            overallNormalized = Mathf.Clamp01((float)currentHealth / maxHealth);

            if (!enableMultiLayer || totalLayers <= 1)
            {
                currentLayer = 1;
                currentLayerFill = overallNormalized;
            }
            else
            {
                float segment = (float)maxHealth / totalLayers;
                if (currentHealth <= 0)
                {
                    currentLayer = 1;
                    currentLayerFill = 0f;
                }
                else
                {
                    currentLayer = Mathf.Clamp(
                        Mathf.FloorToInt((currentHealth - 0.001f) / segment) + 1,
                        1,
                        totalLayers);
                    float healthInLayer = currentHealth - (currentLayer - 1) * segment;
                    currentLayerFill = Mathf.Clamp01(healthInLayer / segment);
                }
            }

            if (mainSlider != null)
            {
                mainSlider.maxValue = 1f;
                if (isInitial) mainSlider.value = currentLayerFill;
            }

            if (isInitial)
            {
                delayedNormalized = currentLayerFill;
                if (delayedImage != null) delayedImage.fillAmount = delayedNormalized;
                if (easeSlider != null)
                {
                    easeSlider.maxValue = 1f;
                    easeSlider.value = currentLayerFill;
                }
            }

            UpdateLayerBadge();
            ApplyCurrentPalette();
        }

        private void UpdateLayerBadge()
        {
            Color layerColor = GetLayerColor(currentLayer);
            bool showLayer = enableMultiLayer && totalLayers > 1;

            if (layerBadgeText != null)
            {
                layerBadgeText.gameObject.SetActive(showLayer);
                if (showLayer)
                {
                    layerBadgeText.text = $"x{currentLayer}";
                    layerBadgeText.color = Color.Lerp(layerColor, Color.white, 0.18f);
                }
            }

            if (bossNameText != null)
            {
                if (showLayer && showBadgeInBossName && layerBadgeText == null)
                {
                    string hex = ColorUtility.ToHtmlStringRGB(layerColor);
                    bossNameText.text = $"{originalBossName}  <color=#{hex}><b>x{currentLayer}</b></color>";
                }
                else
                {
                    bossNameText.text = originalBossName;
                }
            }
        }

        private void Update()
        {
            if (!visualsBuilt || barContainer == null) return;
            float dt = Time.unscaledDeltaTime;

            flashTimer = Mathf.Max(0f, flashTimer - dt);
            shakeTimer = Mathf.Max(0f, shakeTimer - dt);
            damageDelayTimer = Mathf.Max(0f, damageDelayTimer - dt);
            currentPunch = Mathf.MoveTowards(currentPunch, 1f, punchRecovery * dt);

            if (damageDelayTimer <= 0f)
            {
                float follow = 1f - Mathf.Exp(-delayedFollowSpeed * dt);
                delayedNormalized = Mathf.Lerp(delayedNormalized, currentLayerFill, follow);
            }
            else
            {
                delayedNormalized = Mathf.Max(delayedNormalized, currentLayerFill);
            }

            if (delayedImage != null) delayedImage.fillAmount = delayedNormalized;
            if (easeSlider != null) easeSlider.value = delayedNormalized;

            if (mainSlider != null)
            {
                mainSlider.value = Mathf.Lerp(mainSlider.value, currentLayerFill, delayedFollowSpeed * 0.5f * dt);
            }

            float critical = Mathf.InverseLerp(0.28f, 0.055f, overallNormalized);
            float pulseSpeed = phaseTwo ? 8.2f : 5.1f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed);

            ApplyCurrentPalette();
            UpdateMaterials(critical * pulse);
            UpdateTransformFeedback(critical, pulse);
            UpdateTextAndOrnaments(critical, pulse);
            UpdateParticles(dt, critical, pulse);
        }

        private void PlayDamageFeedback()
        {
            flashTimer = damageFlashDuration;
            shakeTimer = shakeDuration;
            currentPunch = punchScale;
            damageDelayTimer = delayedHold;
        }

        private void PlayLayerTransition()
        {
            if (!isActiveAndEnabled) return;
            if (transitionRoutine != null) StopCoroutine(transitionRoutine);
            transitionRoutine = StartCoroutine(LayerTransitionRoutine());
        }

        private void UpdateMaterials(float criticalPulse)
        {
            float flash = damageFlashDuration > 0f ? flashTimer / damageFlashDuration : 0f;
            SetMaterialMotion(healthMaterial, currentLayerFill, flash, criticalPulse);
            SetMaterialMotion(delayedMaterial, delayedNormalized, 0f, criticalPulse * 0.32f);

            if (panelMaterial != null)
            {
                panelMaterial.SetFloat(PhaseAmountId, phaseAmount);
                panelMaterial.SetFloat(DamageFlashId, flash);
                panelMaterial.SetFloat(TransitionProgressId, transitionSweep);
                panelMaterial.SetFloat(CriticalPulseId, criticalPulse);
                panelMaterial.SetFloat(IntensityId, 1f + phaseAmount * 0.28f + phaseShock * 0.22f);
            }

            if (ornamentMaterial != null)
            {
                ornamentMaterial.SetFloat(PhaseAmountId, phaseAmount);
                ornamentMaterial.SetFloat(CriticalPulseId, criticalPulse);
                ornamentMaterial.SetFloat(TransitionProgressId, transitionSweep);
            }
        }

        private void SetMaterialMotion(Material material, float fill, float flash, float criticalPulse)
        {
            if (material == null) return;
            material.SetFloat(FillAmountId, fill);
            material.SetFloat(PhaseAmountId, phaseAmount);
            material.SetFloat(DamageFlashId, flash);
            material.SetFloat(TransitionProgressId, transitionSweep);
            material.SetFloat(CriticalPulseId, criticalPulse);
        }

        private void ApplyCurrentPalette()
        {
            Color primary = GetLayerColor(currentLayer);
            Color secondary = GetSecondaryLayerColor(currentLayer);
            Color accent = GetAccentLayerColor(currentLayer);
            Color deep = new Color(primary.r * 0.10f, primary.g * 0.07f, primary.b * 0.09f, 1f);

            primary = Color.Lerp(primary, new Color(1.25f, 0.015f, 0.32f, 1f), phaseAmount);
            secondary = Color.Lerp(secondary, new Color(1.45f, 0.11f, 0.62f, 1f), phaseAmount);
            accent = Color.Lerp(accent, new Color(1.5f, 0.58f, 1.15f, 1f), phaseAmount);
            deep = Color.Lerp(deep, new Color(0.10f, 0.001f, 0.035f, 1f), phaseAmount);

            ApplyPalette(healthMaterial, primary, secondary, accent, deep, false);
            ApplyPalette(
                delayedMaterial,
                Dim(primary, 0.30f),
                Dim(secondary, 0.24f),
                Dim(accent, 0.34f),
                Dim(deep, 0.72f),
                true);
            ApplyPalette(panelMaterial, primary, secondary, accent, deep, false);
            ApplyPalette(ornamentMaterial, primary, secondary, accent, deep, false);

            if (fillImage != null) fillImage.color = Color.white;
            if (delayedImage != null) delayedImage.color = Color.white;
        }

        private static void ApplyPalette(
            Material material,
            Color primary,
            Color secondary,
            Color accent,
            Color deep,
            bool delayed)
        {
            if (material == null) return;
            material.SetColor(PrimaryColorId, primary);
            material.SetColor(SecondaryColorId, secondary);
            material.SetColor(AccentColorId, accent);
            material.SetColor(DeepColorId, deep);
            if (material.HasProperty(IntensityId))
                material.SetFloat(IntensityId, delayed ? 0.72f : 1f);
        }

        private void UpdateTransformFeedback(float critical, float pulse)
        {
            float damageStrength = shakeDuration > 0f ? shakeTimer / shakeDuration : 0f;
            float breathing = 1f + critical * pulse * 0.009f;
            barContainer.localScale = baseScale * currentPunch * breathing * (1f + phaseShock * 0.045f);

            float shake = shakeMagnitude * damageStrength + phaseShock * 5.2f;
            if (shake <= 0.01f)
            {
                barContainer.anchoredPosition = basePosition;
                return;
            }

            float time = Time.unscaledTime * 43f;
            barContainer.anchoredPosition = basePosition + new Vector2(
                Mathf.Sin(time * 1.21f) * shake,
                Mathf.Cos(time * 1.67f) * shake * 0.38f);
        }

        private void UpdateTextAndOrnaments(float critical, float pulse)
        {
            Color layerColor = GetLayerColor(currentLayer);
            Color titleColor = Color.Lerp(phase1TitleColor, phase2TitleColor, phaseAmount);
            titleColor = Color.Lerp(titleColor, layerColor, enableMultiLayer ? 0.42f : 0.18f);
            titleColor = Color.Lerp(titleColor, Color.white, critical * pulse * 0.28f);

            if (bossNameText != null)
            {
                bossNameText.color = titleColor;
                Outline outline = bossNameText.GetComponent<Outline>();
                if (outline != null)
                {
                    Color outlineColor = Color.Lerp(
                        new Color(0.13f, 0.006f, 0.004f, 0.96f),
                        new Color(0.20f, 0.002f, 0.08f, 0.98f),
                        phaseAmount);
                    outline.effectColor = outlineColor;
                }

                float wobble = phaseShock * 2.8f;
                bossNameText.rectTransform.anchoredPosition = titleBasePosition + new Vector2(
                    Mathf.Sin(Time.unscaledTime * 61f) * wobble,
                    Mathf.Cos(Time.unscaledTime * 47f) * wobble * 0.35f);
            }

            foreach (Image image in ornamentImages)
            {
                if (image == null) continue;
                if (ornamentMaterial != null)
                {
                    Color neutral = image.color;
                    neutral.r = neutral.g = neutral.b = 1f;
                    image.color = neutral;
                }
                else
                {
                    Color ornamentColor = Color.Lerp(layerColor, Color.white, 0.24f + critical * pulse * 0.18f);
                    ornamentColor.a = image.color.a;
                    image.color = ornamentColor;
                }
            }

            if (borderImage != null)
                borderImage.color = Color.Lerp(layerColor, Color.white, 0.18f);
        }

        private void UpdateParticles(float dt, float critical, float pulse)
        {
            float activity = 0.78f + phaseAmount * 0.72f + critical * pulse * 0.38f;
            Color primary = GetLayerColor(currentLayer);
            Color accent = GetAccentLayerColor(currentLayer);

            foreach (SlimeParticle particle in particles)
            {
                particle.Life += dt * activity;
                if (particle.Life >= particle.MaxLife)
                {
                    ResetParticle(particle, false);
                    continue;
                }

                Vector2 position = particle.Rect.anchoredPosition;
                position.y += particle.Speed * dt * activity;
                position.x += Mathf.Sin(Time.unscaledTime * 1.7f + particle.Phase) * particle.Drift * dt;
                particle.Rect.anchoredPosition = position;
                particle.Rect.Rotate(0f, 0f, particle.RotationSpeed * dt);

                float life01 = particle.Life / particle.MaxLife;
                float fade = Mathf.Sin(life01 * Mathf.PI);
                float wobble = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f + particle.Phase);
                Color color = Color.Lerp(primary, accent, particle.Bubble ? 0.72f : 0.28f);
                color.a = fade * (particle.Bubble ? 0.13f : 0.24f) * activity;
                particle.Image.color = color;

                float scale = Mathf.Lerp(0.55f, 1.22f, life01);
                float squash = particle.Bubble ? 1f : Mathf.Lerp(0.62f, 1.18f, wobble);
                particle.Rect.localScale = new Vector3(scale * squash, scale / Mathf.Max(0.7f, squash), 1f);
            }
        }

        private void ResetParticle(SlimeParticle particle, bool randomizeLife)
        {
            particle.Life = randomizeLife ? RandomRange(0f, particle.MaxLife) : 0f;
            particle.Rect.anchoredPosition = new Vector2(RandomRange(-438f, 438f), RandomRange(-72f, -40f));
            float width = particle.Bubble ? particle.BaseSize : particle.BaseSize * RandomRange(0.55f, 0.9f);
            float height = particle.Bubble ? particle.BaseSize : particle.BaseSize * RandomRange(1.15f, 1.8f);
            particle.Rect.sizeDelta = new Vector2(width, height);
            particle.Rect.localRotation = Quaternion.Euler(0f, 0f, RandomRange(-22f, 22f));
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
            ApplyCurrentPalette();
            transitionRoutine = null;
        }

        private IEnumerator LayerTransitionRoutine()
        {
            const float duration = 0.72f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transitionSweep = t;
                phaseShock = Mathf.Sin(t * Mathf.PI) * 0.82f;
                yield return null;
            }

            transitionSweep = 0f;
            phaseShock = 0f;
            transitionRoutine = null;
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
                barContainer.localScale = baseScale * Mathf.Lerp(0.91f, 1f, eased);
                yield return null;
            }

            canvasGroup.alpha = 1f;
            revealRoutine = null;
        }

        private IEnumerator DeathRoutine()
        {
            float elapsed = 0f;
            const float duration = 0.72f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                canvasGroup.alpha = 1f - t;
                barContainer.localScale = baseScale * Mathf.Lerp(1f, 0.86f, t);
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
            ApplyCurrentPalette();
        }

        private Color GetLayerColor(int layer)
        {
            if (!enableMultiLayer || totalLayers <= 1) return layer4Color;
            switch (layer)
            {
                case 1: return layer1Color;
                case 2: return layer2Color;
                case 3: return layer3Color;
                default: return layer4Color;
            }
        }

        private Color GetSecondaryLayerColor(int layer)
        {
            if (!enableMultiLayer || totalLayers <= 1) return new Color(1.38f, 0.12f, 0.035f, 1f);
            switch (layer)
            {
                case 1: return new Color(1.42f, 0.08f, 0.78f, 1f);
                case 2: return new Color(1.45f, 0.22f, 0.018f, 1f);
                case 3: return new Color(0.04f, 1.25f, 1.35f, 1f);
                default: return new Color(1.38f, 0.12f, 0.035f, 1f);
            }
        }

        private Color GetAccentLayerColor(int layer)
        {
            if (!enableMultiLayer || totalLayers <= 1) return new Color(1.55f, 0.62f, 0.22f, 1f);
            switch (layer)
            {
                case 1: return new Color(1.55f, 0.58f, 1.08f, 1f);
                case 2: return new Color(1.6f, 1.08f, 0.28f, 1f);
                case 3: return new Color(0.58f, 1.48f, 1.55f, 1f);
                default: return new Color(1.55f, 0.62f, 0.22f, 1f);
            }
        }

        private static Color Dim(Color color, float amount)
        {
            return new Color(color.r * amount, color.g * amount, color.b * amount, color.a);
        }

        private float RandomRange(float minimum, float maximum)
        {
            if (random == null) random = new System.Random(GetInstanceID());
            return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
        }

        private static Sprite CreateSoftParticleSprite(out Texture2D texture)
        {
            const int size = 28;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Red Slime UI Soft Droplet",
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
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 1.8f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = "Red Slime UI Soft Droplet";
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private void OnDestroy()
        {
            if (healthMaterial != null) Destroy(healthMaterial);
            if (delayedMaterial != null) Destroy(delayedMaterial);
            if (panelMaterial != null) Destroy(panelMaterial);
            if (ornamentMaterial != null) Destroy(ornamentMaterial);
            if (softParticleSprite != null) Destroy(softParticleSprite);
            if (softParticleTexture != null) Destroy(softParticleTexture);
        }
    }
}
