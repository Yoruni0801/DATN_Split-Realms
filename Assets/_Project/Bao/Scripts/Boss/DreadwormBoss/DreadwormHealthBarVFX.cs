using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bao.Bosses
{
    /// <summary>
    /// Runtime presentation for the Dreadworm boss UI: frozen cyan in phase one and
    /// an unstable violet storm in phase two. Generated visuals stay runtime-only.
    /// </summary>
    [DisallowMultipleComponent]
    public class DreadwormHealthBarVFX : MonoBehaviour
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
        [SerializeField, Min(0f)] private float delayedHold = 0.14f;
        [SerializeField, Min(0.1f)] private float delayedFollowSpeed = 6f;
        [SerializeField, Min(1f)] private float punchScale = 1.038f;
        [SerializeField, Min(0.1f)] private float punchRecovery = 10f;
        [SerializeField, Min(0f)] private float shakeMagnitude = 2.8f;
        [SerializeField, Min(0.05f)] private float shakeDuration = 0.15f;

        [Header("Phase Presentation")]
        [SerializeField, Min(0.2f)] private float phaseTransitionDuration = 1.1f;
        [SerializeField] private Color phase1TitleColor = new Color(0.78f, 0.98f, 1f, 1f);
        [SerializeField] private Color phase2TitleColor = new Color(0.94f, 0.72f, 1f, 1f);
        [SerializeField] private Color phase1OrnamentColor = new Color(0.17f, 0.92f, 1f, 1f);
        [SerializeField] private Color phase2OrnamentColor = new Color(0.72f, 0.12f, 1f, 1f);
        [SerializeField, Range(6, 28)] private int ambientShardCount = 16;
        [SerializeField, Range(2, 10)] private int lightningArcCount = 5;

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
        private RectTransform particleLayer;
        private CanvasGroup canvasGroup;
        private readonly List<Image> ornamentImages = new List<Image>();
        private readonly List<IceShard> shards = new List<IceShard>();
        private readonly List<LightningArc> lightningArcs = new List<LightningArc>();

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
        private System.Random random;

        private sealed class IceShard
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Velocity;
            public float AngularVelocity;
            public float Life;
            public float MaxLife;
            public float Phase;
            public bool Burst;
        }

        private sealed class LightningArc
        {
            public RectTransform Rect;
            public Image Image;
            public float Timer;
            public float Duration;
            public float Delay;
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
            float target = wantsPhaseTwo ? 1f : 0f;
            if (wantsPhaseTwo == phaseTwo && Mathf.Approximately(phaseAmount, target)) return;

            phaseTwo = wantsPhaseTwo;
            if (phaseRoutine != null) StopCoroutine(phaseRoutine);
            phaseRoutine = StartCoroutine(PhaseTransitionRoutine(target));
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
            BurstShards(Mathf.Max(8, ambientShardCount / 2));
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
                        text.name.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0 ||
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
            random = new System.Random(GetInstanceID());
            basePosition = barContainer.anchoredPosition;
            baseScale = barContainer.localScale;
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            BuildHealthMaterials();
            BuildDelayedFill();
            BuildFrozenPanel();
            CacheOrnaments();
            BuildAmbientShards();
            BuildLightningArcs();

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
            Shader shader = healthBarShader != null ? healthBarShader : Shader.Find("Bao/UI/DreadwormHealthBar");
            if (shader == null)
            {
                Debug.LogWarning("[DreadwormHealthBarVFX] Shader Bao/UI/DreadwormHealthBar not found, using default UI material fallback.", this);
            }
            else
            {
                healthMaterial = new Material(shader) { name = "Dreadworm Health Runtime", hideFlags = HideFlags.DontSave };
                ConfigureHealthMaterial(healthMaterial, false);
                fillImage.material = healthMaterial;

                delayedMaterial = new Material(shader) { name = "Dreadworm Delayed Health Runtime", hideFlags = HideFlags.DontSave };
                ConfigureHealthMaterial(delayedMaterial, true);
            }
            fillImage.color = Color.white;
        }

        private static void ConfigureHealthMaterial(Material material, bool delayed)
        {
            // Phase one follows Dreadworm's crystal glow; phase two is blood red.
            material.SetColor("_IceColor", delayed ? new Color(0.08f, 0.16f, 0.18f, 1f) : new Color(0.32f, 0.70f, 0.78f, 1f));
            material.SetColor("_CoreColor", delayed ? new Color(0.16f, 0.28f, 0.31f, 1f) : new Color(0.5605f, 0.8333f, 0.8868f, 1f));
            material.SetColor("_StormColor", delayed ? new Color(0.03f, 0.08f, 0.12f, 1f) : new Color(0.102f, 0.2463f, 1f, 1f));
            material.SetColor("_PhaseColor", delayed ? new Color(0.30f, 0.008f, 0.004f, 1f) : new Color(1.18f, 0.055f, 0.012f, 1f));
            material.SetColor("_CrackColor", delayed ? new Color(0.40f, 0.08f, 0.025f, 1f) : new Color(1.32f, 0.34f, 0.075f, 1f));
            material.SetFloat("_FlowSpeed", delayed ? 1.2f : 3.1f);
            material.SetFloat("_CrackScale", delayed ? 16f : 25f);
            material.SetFloat("_LightningWidth", delayed ? 0.018f : 0.035f);
            material.SetFloat("_GlowIntensity", delayed ? 0.9f : 2.9f);
            material.SetFloat("_PulseSpeed", delayed ? 2.2f : 5.2f);
            material.SetFloat("_PulseStrength", delayed ? 0.08f : 0.27f);
        }

        private void BuildDelayedFill()
        {
            GameObject delayedObject = Instantiate(fillImage.gameObject, fillImage.transform.parent);
            delayedObject.name = "Dreadworm_DelayedIce";
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

        private void BuildFrozenPanel()
        {
            GameObject panelObject = new GameObject("Dreadworm_FrozenStormPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.SetParent(barContainer, false);
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, -35f);
            panelRect.sizeDelta = new Vector2(930f, 160f);
            panelRect.SetAsFirstSibling();

            panelImage = panelObject.GetComponent<Image>();
            panelImage.raycastTarget = false;
            panelImage.color = Color.white;
            Shader shader = panelShader != null ? panelShader : Shader.Find("Bao/UI/DreadwormPanel");
            if (shader != null)
            {
                panelMaterial = new Material(shader) { name = "Dreadworm Frozen Storm Panel Runtime", hideFlags = HideFlags.DontSave };
                panelImage.material = panelMaterial;
            }

            GameObject particles = new GameObject("Dreadworm_UIParticles", typeof(RectTransform));
            particleLayer = particles.GetComponent<RectTransform>();
            particleLayer.SetParent(panelRect, false);
            particleLayer.anchorMin = Vector2.zero;
            particleLayer.anchorMax = Vector2.one;
            particleLayer.offsetMin = Vector2.zero;
            particleLayer.offsetMax = Vector2.zero;
        }

        private void CacheOrnaments()
        {
            Shader shader = ornamentShader != null ? ornamentShader : Shader.Find("Bao/UI/DreadwormOrnament");
            if (shader != null)
                ornamentMaterial = new Material(shader) { name = "Dreadworm Ornament Runtime", hideFlags = HideFlags.DontSave };

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

        private void BuildAmbientShards()
        {
            if (particleLayer == null) return;
            for (int i = 0; i < ambientShardCount; i++)
            {
                GameObject go = new GameObject("IceShard_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform rect = go.GetComponent<RectTransform>();
                rect.SetParent(particleLayer, false);
                Image image = go.GetComponent<Image>();
                image.sprite = DreadwormVFXUtility.ShardSprite;
                image.raycastTarget = false;
                IceShard shard = new IceShard { Rect = rect, Image = image };
                ResetAmbientShard(shard, true);
                shards.Add(shard);
            }
        }

        private void BuildLightningArcs()
        {
            if (particleLayer == null) return;
            for (int i = 0; i < lightningArcCount; i++)
            {
                GameObject go = new GameObject("StormArc_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform rect = go.GetComponent<RectTransform>();
                rect.SetParent(particleLayer, false);
                Image image = go.GetComponent<Image>();
                image.sprite = DreadwormVFXUtility.SoftDiscSprite;
                image.raycastTarget = false;
                image.color = Color.clear;
                lightningArcs.Add(new LightningArc
                {
                    Rect = rect,
                    Image = image,
                    Timer = 0f,
                    Duration = RandomRange(0.035f, 0.09f),
                    Delay = RandomRange(0.2f, 1.2f)
                });
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
                new Color(0.56f, 0.83f, 0.89f, 1f),
                new Color(1f, 0.02f, 0.01f, 1f),
                phaseAmount);
            if (fillImage != null) fillImage.color = phaseTint;
            if (delayedImage != null)
            {
                delayedImage.fillAmount = delayedNormalized;
                delayedImage.color = phaseTint;
            }

            float critical = Mathf.InverseLerp(0.32f, 0.06f, normalized);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (phaseTwo ? 9.4f : 3.8f));
            UpdateMaterials(normalized, critical * pulse);
            UpdateTransformFeedback(critical, pulse);
            UpdateTextAndOrnaments(critical, pulse);
            UpdateShards(dt);
            UpdateLightning(dt, critical);
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
            if (phaseTwo) TriggerLightningBurst();
        }

        private void UpdateMaterials(float normalized, float criticalPulse)
        {
            float flash = damageFlashDuration > 0f ? flashTimer / damageFlashDuration : 0f;
            SetHealthMaterialProperties(healthMaterial, normalized, flash, criticalPulse);
            SetHealthMaterialProperties(delayedMaterial, delayedNormalized, 0f, criticalPulse * 0.3f);

            if (panelMaterial != null)
            {
                panelMaterial.SetFloat(PhaseAmountId, phaseAmount);
                panelMaterial.SetFloat(DamageFlashId, flash);
                panelMaterial.SetFloat(TransitionProgressId, transitionSweep);
                panelMaterial.SetFloat(CriticalPulseId, criticalPulse);
                panelMaterial.SetFloat(IntensityId, Mathf.Lerp(0.92f, 1.32f, phaseAmount));
            }
            if (ornamentMaterial != null)
            {
                ornamentMaterial.SetFloat(PhaseAmountId, phaseAmount);
                ornamentMaterial.SetFloat(CriticalPulseId, criticalPulse);
                ornamentMaterial.SetFloat(TransitionProgressId, transitionSweep);
            }
        }

        private void SetHealthMaterialProperties(Material material, float fill, float flash, float criticalPulse)
        {
            if (material == null) return;
            material.SetFloat(FillAmountId, fill);
            material.SetFloat(PhaseAmountId, phaseAmount);
            material.SetFloat(DamageFlashId, flash);
            material.SetFloat(TransitionProgressId, transitionSweep);
            material.SetFloat(CriticalPulseId, criticalPulse);
        }

        private void UpdateTransformFeedback(float critical, float pulse)
        {
            float damageStrength = shakeDuration > 0f ? shakeTimer / shakeDuration : 0f;
            float criticalScale = 1f + critical * pulse * Mathf.Lerp(0.004f, 0.014f, phaseAmount);
            barContainer.localScale = baseScale * currentPunch * criticalScale * (1f + phaseShock * 0.038f);

            float shake = shakeMagnitude * damageStrength + phaseShock * 5.2f;
            if (shake <= 0.01f)
            {
                barContainer.anchoredPosition = basePosition;
                return;
            }

            float time = Time.unscaledTime * 46f;
            barContainer.anchoredPosition = basePosition + new Vector2(
                Mathf.Sin(time * 1.21f) * shake,
                Mathf.Cos(time * 1.71f) * shake * 0.36f);
        }

        private void UpdateTextAndOrnaments(float critical, float pulse)
        {
            Color titleColor = Color.Lerp(phase1TitleColor, phase2TitleColor, phaseAmount);
            titleColor = Color.Lerp(titleColor, Color.white, critical * pulse * 0.32f);
            if (bossNameText != null)
            {
                bossNameText.color = titleColor;
                Outline outline = bossNameText.GetComponent<Outline>();
                if (outline != null)
                    outline.effectColor = Color.Lerp(new Color(0.005f, 0.09f, 0.14f, 0.98f), new Color(0.12f, 0f, 0.25f, 0.98f), phaseAmount);
                float glitch = phaseShock * 3.1f + critical * phaseAmount * pulse * 0.45f;
                bossNameText.rectTransform.anchoredPosition = titleBasePosition + new Vector2(
                    Mathf.Sin(Time.unscaledTime * 71f) * glitch,
                    Mathf.Cos(Time.unscaledTime * 59f) * glitch * 0.32f);
            }

            Color ornamentColor = Color.Lerp(phase1OrnamentColor, phase2OrnamentColor, phaseAmount);
            ornamentColor = Color.Lerp(ornamentColor, Color.white, critical * pulse * 0.2f);
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
                    Color color = ornamentColor;
                    color.a = image.color.a;
                    image.color = color;
                }
            }
            if (borderImage != null) borderImage.color = ornamentColor;
        }

        private void UpdateShards(float dt)
        {
            Color phaseColor = Color.Lerp(new Color(0.58f, 0.98f, 1f, 0.34f), new Color(0.78f, 0.34f, 1f, 0.58f), phaseAmount);
            for (int i = 0; i < shards.Count; i++)
            {
                IceShard shard = shards[i];
                shard.Life += dt;
                if (shard.Life >= shard.MaxLife)
                {
                    ResetAmbientShard(shard, false);
                    continue;
                }

                float speedBoost = Mathf.Lerp(1f, 2.25f, phaseAmount);
                shard.Rect.anchoredPosition += shard.Velocity * (dt * speedBoost);
                shard.Rect.Rotate(0f, 0f, shard.AngularVelocity * dt * speedBoost);
                float life01 = shard.Life / shard.MaxLife;
                float fade = Mathf.Sin(life01 * Mathf.PI);
                Color color = phaseColor;
                color.a *= fade * (shard.Burst ? 1.35f : 1f);
                shard.Image.color = color;
            }
        }

        private void ResetAmbientShard(IceShard shard, bool randomizeLife)
        {
            float size = RandomRange(4f, 11f) * Mathf.Lerp(1f, 1.22f, phaseAmount);
            shard.Rect.sizeDelta = new Vector2(size * 0.55f, size * 1.7f);
            shard.Rect.anchoredPosition = new Vector2(RandomRange(-440f, 440f), RandomRange(-74f, 68f));
            shard.Rect.localRotation = Quaternion.Euler(0f, 0f, RandomRange(-70f, 70f));
            shard.Velocity = phaseTwo
                ? new Vector2(RandomRange(-44f, 44f), RandomRange(-18f, 38f))
                : new Vector2(RandomRange(-7f, 7f), RandomRange(-16f, -6f));
            shard.AngularVelocity = RandomRange(-85f, 85f);
            shard.MaxLife = RandomRange(2.8f, 6.2f);
            shard.Life = randomizeLife ? RandomRange(0f, shard.MaxLife) : 0f;
            shard.Phase = RandomRange(0f, Mathf.PI * 2f);
            shard.Burst = false;
        }

        private void BurstShards(int count)
        {
            if (shards.Count == 0) return;
            count = Mathf.Min(count, shards.Count);
            for (int i = 0; i < count; i++)
            {
                IceShard shard = shards[i];
                float angle = RandomRange(-165f, 15f) * Mathf.Deg2Rad;
                float speed = RandomRange(95f, 230f);
                shard.Rect.anchoredPosition = new Vector2(RandomRange(-60f, 60f), RandomRange(-25f, 28f));
                shard.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                shard.AngularVelocity = RandomRange(-360f, 360f);
                shard.Life = 0f;
                shard.MaxLife = RandomRange(0.55f, 1.05f);
                shard.Burst = true;
            }
        }

        private void UpdateLightning(float dt, float critical)
        {
            float activity = Mathf.Lerp(0.03f, 1f, phaseAmount) + critical * phaseAmount * 0.75f;
            for (int i = 0; i < lightningArcs.Count; i++)
            {
                LightningArc arc = lightningArcs[i];
                if (arc.Delay > 0f)
                {
                    arc.Delay -= dt * activity;
                    arc.Image.color = Color.clear;
                    continue;
                }

                arc.Timer += dt;
                if (arc.Timer >= arc.Duration)
                {
                    ResetLightningArc(arc, activity);
                    continue;
                }

                float flash = 1f - arc.Timer / arc.Duration;
                arc.Image.color = new Color(0.86f, 0.62f, 1f, flash * (0.35f + 0.65f * phaseAmount));
            }
        }

        private void ResetLightningArc(LightningArc arc, float activity)
        {
            arc.Timer = 0f;
            arc.Duration = RandomRange(0.025f, 0.075f);
            arc.Delay = RandomRange(0.15f, 1.15f) / Mathf.Max(0.15f, activity);
            arc.Rect.sizeDelta = new Vector2(RandomRange(90f, 310f), RandomRange(2f, 4.5f));
            arc.Rect.anchoredPosition = new Vector2(RandomRange(-350f, 350f), RandomRange(-56f, 55f));
            arc.Rect.localRotation = Quaternion.Euler(0f, 0f, RandomRange(-14f, 14f));
        }

        private void TriggerLightningBurst()
        {
            foreach (LightningArc arc in lightningArcs)
                arc.Delay = RandomRange(0f, 0.08f);
        }

        private IEnumerator PhaseTransitionRoutine(float target)
        {
            float start = phaseAmount;
            float elapsed = 0f;
            bool burstTriggered = false;
            while (elapsed < phaseTransitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / phaseTransitionDuration);
                float eased = t * t * (3f - 2f * t);
                phaseAmount = Mathf.Lerp(start, target, eased);
                transitionSweep = t;
                phaseShock = Mathf.Sin(t * Mathf.PI) * (target > start ? 1f : 0.55f);

                if (!burstTriggered && t >= 0.38f)
                {
                    burstTriggered = true;
                    BurstShards(ambientShardCount);
                    TriggerLightningBurst();
                }
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
            const float duration = 0.58f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                canvasGroup.alpha = eased;
                barContainer.localScale = baseScale * Mathf.Lerp(0.90f, 1f, eased);
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
                barContainer.localScale = baseScale * Mathf.Lerp(1f, 0.84f, t);
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
                    new Color(0.56f, 0.83f, 0.89f, 1f),
                    new Color(1f, 0.02f, 0.01f, 1f),
                    phaseAmount);
                fillImage.color = phaseTint;
            }
        }

        private float RandomRange(float minimum, float maximum)
        {
            if (random == null) random = new System.Random(GetInstanceID());
            return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
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
