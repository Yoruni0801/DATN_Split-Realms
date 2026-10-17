using UnityEngine;
using System.Collections;

namespace Bao.Bosses
{
    /// <summary>
    /// Drives NidusCocoonBossShader and NidusBurrowShader parameters in real-time
    /// based on the boss's health, phase, and state machine transitions.
    /// 
    /// SETUP: Attach this script to the same GameObject as the SpriteRenderer(s).
    /// It will auto-detect the boss and the renderers.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class NidusBossShaderController : MonoBehaviour
    {
        [Header("Shader References")]
        [Tooltip("The Material using NidusCocoonBossShader for normal display")]
        [SerializeField] private Material bodyMaterial;
        [Tooltip("The Material using NidusBurrowShader for burrow/emerge transitions")]
        [SerializeField] private Material burrowMaterial;

        [Header("Hit Flash")]
        [SerializeField] private float hitFlashDuration = 0.12f;
        [SerializeField] private float hitFlashIntensity = 1.0f;
        [SerializeField] private Color hitFlashColor = new Color(1.8f, 1.8f, 1.8f, 1f);

        [Header("Phase Transition")]
        [SerializeField, Min(0f)] private float transitionCoreBoost = 0.55f;
        [SerializeField, Min(0f)] private float transitionVeinBoost = 0.65f;
        [SerializeField, Min(0f)] private float transitionAuraBoost = 0.38f;
        [SerializeField, Min(0f)] private float transitionFlashIntensity = 0.26f;

        [Header("Core Exposure")]
        [SerializeField, Min(0f)] private float exposedCoreBoost = 1.05f;
        [SerializeField, Min(0f)] private float exposedVeinBoost = 1.15f;
        [SerializeField, Min(0f)] private float exposedAuraBoost = 0.40f;
        [SerializeField, Min(0.1f)] private float exposedBlendSpeed = 8f;

        [Header("Desperation")]
        [SerializeField, Min(0f)] private float desperationCoreBoost = 0.38f;
        [SerializeField, Min(0f)] private float desperationVeinBoost = 0.60f;
        [SerializeField, Min(0f)] private float desperationAuraBoost = 0.28f;
        [SerializeField, Min(0.1f)] private float desperationBlendSpeed = 3.5f;

        [Header("Burrow/Emerge Timing")]
        [SerializeField] private float burrowDissolveDuration = 0.5f;
        [SerializeField] private float emergeDissolveDuration = 0.35f;

        // Internal state
        private SpriteRenderer[] renderers;
        private NidusCocoonBoss boss;

        private Material[] bodyMatInstances;  // Per-renderer material instances
        private Material[] burrowMatInstances;
        private Coroutine hitFlashCoroutine;
        private Coroutine phaseTransitionCoroutine;
        private Coroutine burrowVisualCoroutine;
        private float hitFlashAmount;
        private float phaseTransitionBoost;
        private float phaseTransitionFlash;
        private float coreExposureBlend;
        private float desperationBlend;
        private bool coreExposureTarget;
        private bool desperationTarget;

        public bool HasBurrowMaterial => burrowMaterial != null;

        // ── Shader property IDs (cached for performance) ──
        private static readonly int ID_PhaseLevel     = Shader.PropertyToID("_PhaseLevel");
        private static readonly int ID_HitFlash       = Shader.PropertyToID("_HitFlash");
        private static readonly int ID_DeathDissolve  = Shader.PropertyToID("_DeathDissolve");
        private static readonly int ID_CorePulseIntensity = Shader.PropertyToID("_CorePulseIntensity");
        private static readonly int ID_VeinIntensity  = Shader.PropertyToID("_VeinIntensity");
        private static readonly int ID_AuraColor      = Shader.PropertyToID("_AuraColor");
        private static readonly int ID_AuraIntensity  = Shader.PropertyToID("_AuraIntensity");
        private static readonly int ID_BurrowAmount   = Shader.PropertyToID("_BurrowAmount");
        private static readonly int ID_EmergeAmount   = Shader.PropertyToID("_EmergeAmount");

        private void Awake()
        {
            // Find boss component (could be on parent or sibling)
            boss = GetComponentInParent<NidusCocoonBoss>();
            if (boss == null)
                boss = FindFirstObjectByType<NidusCocoonBoss>();

            renderers = GetComponentsInChildren<SpriteRenderer>(true);

            // Create per-renderer material instances so we don't modify shared assets
            bodyMatInstances  = new Material[renderers.Length];
            burrowMatInstances = new Material[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                if (bodyMaterial != null)
                    bodyMatInstances[i]  = new Material(bodyMaterial);
                if (burrowMaterial != null)
                    burrowMatInstances[i] = new Material(burrowMaterial);

                // Start with body material
                if (bodyMatInstances[i] != null)
                    renderers[i].material = bodyMatInstances[i];
            }
        }

        private void Update()
        {
            if (boss == null) return;

            // ── Drive Phase Level ──
            float phaseValue = boss.CurrentPhase == BossPhase.Phase1 ? 1.0f : 2.0f;
            SetBodyFloat(ID_PhaseLevel, phaseValue);

            coreExposureBlend = Mathf.MoveTowards(
                coreExposureBlend,
                coreExposureTarget ? 1f : 0f,
                Time.deltaTime * exposedBlendSpeed);
            desperationBlend = Mathf.MoveTowards(
                desperationBlend,
                desperationTarget ? 1f : 0f,
                Time.deltaTime * desperationBlendSpeed);

            // ── Drive Glow Intensity based on Health + encounter mechanics ──
            float healthRatio = (float)boss.CurrentHealth / Mathf.Max(boss.MaxHealth, 1);
            float lowHealthBoost = (1.0f - healthRatio) * 2.5f;

            float desperationPulse = desperationBlend > 0f
                ? (0.82f + Mathf.Sin(Time.time * 9.5f) * 0.18f) * desperationBlend
                : 0f;

            float coreIntensity =
                0.6f
                + lowHealthBoost * 0.8f
                + phaseTransitionBoost * transitionCoreBoost
                + coreExposureBlend * exposedCoreBoost
                + desperationPulse * desperationCoreBoost;

            float veinIntensity =
                1.5f
                + lowHealthBoost
                + (phaseValue - 1.0f) * 2.5f
                + phaseTransitionBoost * transitionVeinBoost
                + coreExposureBlend * exposedVeinBoost
                + desperationPulse * desperationVeinBoost;

            float auraIntensity =
                0.55f
                + phaseTransitionBoost * transitionAuraBoost
                + coreExposureBlend * exposedAuraBoost
                + desperationPulse * desperationAuraBoost;

            SetBodyFloat(ID_CorePulseIntensity, Mathf.Min(coreIntensity, 3.2f));
            SetBodyFloat(ID_VeinIntensity, Mathf.Min(veinIntensity, 4.5f));
            SetBodyFloat(ID_AuraIntensity, Mathf.Min(auraIntensity, 1.85f));
            SetBodyFloat(ID_HitFlash, Mathf.Max(hitFlashAmount, phaseTransitionFlash));

            // ── Aura color: Phase 2 / Exposed Core / Desperation share the same toxic grammar ──
            float flickerSpeed = desperationBlend > 0f ? 9f : 5f;
            float flickerT = Mathf.Sin(Time.time * flickerSpeed) * 0.1f + 0.9f;
            Color auraColor = boss.CurrentPhase == BossPhase.Phase2
                ? new Color(0.28f * flickerT, 1.35f * flickerT, 0.035f, 1f)
                : new Color(0.16f * flickerT, 1.05f * flickerT, 0.03f, 1f);

            if (phaseTransitionBoost > 0f)
            {
                Color transitionColor = new Color(0.62f, 1.85f, 0.08f, 1f);
                auraColor = Color.Lerp(auraColor, transitionColor, phaseTransitionBoost * 0.72f);
            }

            if (coreExposureBlend > 0f)
            {
                Color exposedColor = new Color(0.82f, 1.95f, 0.10f, 1f);
                auraColor = Color.Lerp(auraColor, exposedColor, coreExposureBlend * 0.78f);
            }

            if (desperationBlend > 0f)
            {
                Color desperateColor = new Color(0.58f, 1.72f, 0.045f, 1f);
                auraColor = Color.Lerp(auraColor, desperateColor, desperationBlend * 0.45f);
            }

            SetBodyColor(ID_AuraColor, auraColor);
        }

        // ── Public methods called by the boss state machine ──

        /// <summary>Trigger a white hit flash on all body renderers.</summary>
        public void TriggerHitFlash()
        {
            if (hitFlashCoroutine != null)
                StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
        }

        public void SetCoreExposed(bool exposed)
        {
            coreExposureTarget = exposed;
        }

        public void SetDesperation(bool enabled)
        {
            desperationTarget = enabled;
        }

        /// <summary>
        /// Drives a controlled energy surge on the body shader during the
        /// phase transition. The peak is aligned with the VFX shell break.
        /// </summary>
        public void StartPhaseTransitionPulse(float duration)
        {
            if (phaseTransitionCoroutine != null)
                StopCoroutine(phaseTransitionCoroutine);
            phaseTransitionCoroutine = StartCoroutine(PhaseTransitionPulseRoutine(Mathf.Max(0.6f, duration)));
        }

        /// <summary>Animate the boss dissolving into the ground (burrow).</summary>
        public Coroutine StartBurrowDissolve(float duration = -1f)
        {
            if (burrowVisualCoroutine != null)
                StopCoroutine(burrowVisualCoroutine);

            float visualDuration = duration > 0f ? duration : burrowDissolveDuration;
            burrowVisualCoroutine = StartCoroutine(BurrowDissolveRoutine(Mathf.Max(0.1f, visualDuration)));
            return burrowVisualCoroutine;
        }

        /// <summary>Animate the boss rising from the ground (emerge).</summary>
        public Coroutine StartEmergeDissolve(float duration = -1f)
        {
            if (burrowVisualCoroutine != null)
                StopCoroutine(burrowVisualCoroutine);

            float visualDuration = duration > 0f ? duration : emergeDissolveDuration;
            burrowVisualCoroutine = StartCoroutine(EmergeDissolveRoutine(Mathf.Max(0.1f, visualDuration)));
            return burrowVisualCoroutine;
        }

        public void CancelBurrowVisuals()
        {
            if (burrowVisualCoroutine != null)
            {
                StopCoroutine(burrowVisualCoroutine);
                burrowVisualCoroutine = null;
            }

            SetBurrowFloat(ID_BurrowAmount, 0f);
            SetBurrowFloat(ID_EmergeAmount, 1f);
            SetRenderersEnabled(true);
            ApplyBodyMaterials();
        }

        /// <summary>Animate the death dissolve effect (bottom-to-top fade).</summary>
        public Coroutine StartDeathDissolve(float duration = 1.5f)
        {
            return StartCoroutine(DeathDissolveRoutine(duration));
        }

        // ── Coroutines ──

        private IEnumerator HitFlashRoutine()
        {
            hitFlashAmount = hitFlashIntensity;
            yield return new WaitForSeconds(hitFlashDuration);
            hitFlashAmount = 0f;
        }

        private IEnumerator PhaseTransitionPulseRoutine(float duration)
        {
            float elapsed = 0f;
            float burstTime = duration * 0.70f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (elapsed <= burstTime)
                {
                    phaseTransitionBoost = Mathf.SmoothStep(0f, 1f, elapsed / burstTime);
                    phaseTransitionFlash = 0f;
                }
                else
                {
                    float afterBurst = Mathf.Clamp01((elapsed - burstTime) / Mathf.Max(0.08f, duration * 0.30f));
                    phaseTransitionBoost = 1f - Mathf.SmoothStep(0f, 1f, afterBurst);
                    float flashDistance = Mathf.Abs(elapsed - burstTime);
                    phaseTransitionFlash = Mathf.Clamp01(1f - flashDistance / 0.095f) * transitionFlashIntensity;
                }

                yield return null;
            }

            phaseTransitionBoost = 0f;
            phaseTransitionFlash = 0f;
            phaseTransitionCoroutine = null;
        }

        private IEnumerator BurrowDissolveRoutine(float duration)
        {
            ApplyBurrowMaterials();
            SetRenderersEnabled(true);
            SetBurrowFloat(ID_EmergeAmount, 1f);
            SetBurrowFloat(ID_BurrowAmount, 0f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                SetBurrowFloat(ID_BurrowAmount, t);
                yield return null;
            }

            SetBurrowFloat(ID_BurrowAmount, 1f);
            burrowVisualCoroutine = null;
        }

        private IEnumerator EmergeDissolveRoutine(float duration)
        {
            ApplyBurrowMaterials();
            SetRenderersEnabled(true);
            SetBurrowFloat(ID_BurrowAmount, 0f);
            SetBurrowFloat(ID_EmergeAmount, 0f);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                SetBurrowFloat(ID_EmergeAmount, t);
                yield return null;
            }

            SetBurrowFloat(ID_EmergeAmount, 1f);
            ApplyBodyMaterials();
            burrowVisualCoroutine = null;
        }

        private void ApplyBurrowMaterials()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (burrowMatInstances[i] != null)
                    renderers[i].material = burrowMatInstances[i];
            }
        }

        private void ApplyBodyMaterials()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (bodyMatInstances[i] != null)
                    renderers[i].material = bodyMatInstances[i];
            }
        }

        private void SetRenderersEnabled(bool enabled)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].enabled = enabled;
            }
        }

        private IEnumerator DeathDissolveRoutine(float duration)
        {
            // Body material can handle death dissolve directly
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                SetBodyFloat(ID_DeathDissolve, t);
                yield return null;
            }
        }

        // ── Helper setters ──

        private void SetBodyFloat(int propertyId, float value)
        {
            if (bodyMatInstances == null) return;
            foreach (var m in bodyMatInstances)
                if (m != null) m.SetFloat(propertyId, value);
        }

        private void SetBodyColor(int propertyId, Color color)
        {
            if (bodyMatInstances == null) return;
            foreach (var m in bodyMatInstances)
                if (m != null) m.SetColor(propertyId, color);
        }

        private void SetBurrowFloat(int propertyId, float value)
        {
            if (burrowMatInstances == null) return;
            foreach (var m in burrowMatInstances)
                if (m != null) m.SetFloat(propertyId, value);
        }

#if UNITY_EDITOR
        [Header("Editor Preview (non-play)")]
        [Range(0, 1)] public float previewBurrow = 0f;
        [Range(0, 1)] public float previewEmerge = 1f;
        [Range(0, 1)] public float previewDeath = 0f;
        [Range(1, 2)] public float previewPhase = 1f;
        public bool previewHitFlash = false;

        private void OnValidate()
        {
            // Allow previewing shader params in editor without entering Play Mode
            if (renderers == null) renderers = GetComponentsInChildren<SpriteRenderer>(true);

            if (bodyMaterial != null)
            {
                bodyMaterial.SetFloat(ID_PhaseLevel, previewPhase);
                bodyMaterial.SetFloat(ID_DeathDissolve, previewDeath);
                bodyMaterial.SetFloat(ID_HitFlash, previewHitFlash ? 1f : 0f);
            }
            if (burrowMaterial != null)
            {
                burrowMaterial.SetFloat(ID_BurrowAmount, previewBurrow);
                burrowMaterial.SetFloat(ID_EmergeAmount, previewEmerge);
            }
        }
#endif
    }
}
