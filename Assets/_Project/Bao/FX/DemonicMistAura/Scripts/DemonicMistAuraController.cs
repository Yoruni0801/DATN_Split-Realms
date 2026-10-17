using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bao.FX
{
    public enum DemonicMistAuraPreset { Dormant, Idle, Threat, Combat, Cinematic }

    [DisallowMultipleComponent]
    public sealed class DemonicMistAuraController : MonoBehaviour
    {
        [System.Serializable]
        private struct Profile
        {
            [Range(0, 2.5f)] public float intensity;
            [Range(0, 1)] public float alpha;
            [Min(0)] public float density;
            [Min(0)] public float motion;
            public Profile(float intensity, float alpha, float density, float motion)
            {
                this.intensity = intensity;
                this.alpha = alpha;
                this.density = density;
                this.motion = motion;
            }
        }

        [System.Serializable]
        private struct VisualLayer
        {
            public Renderer renderer;
            [ColorUsage(true, true)] public Color tint;
            [ColorUsage(true, true)] public Color emission;
            [Range(0, 2)] public float intensity;
            [Range(0, 1)] public float alpha;
        }

        [Header("References")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private Transform torsoAnchor;
        [SerializeField] private Transform groundAnchor;
        [SerializeField] private ParticleSystem[] idleSystems;
        [SerializeField] private ParticleSystem[] accentSystems;
        [SerializeField] private ParticleSystem[] burstSystems;
        [SerializeField] private VisualLayer[] visualLayers;
        [SerializeField] private Renderer[] controlledRenderers;
        [SerializeField] private SortingGroup sortingGroup;
        [SerializeField] private DemonicMistAttachment attachment;
        [SerializeField] private DemonicMistAuraPremiumLayer premiumLayer;

        [Header("Placement")]
        [SerializeField] private Vector3 localOffset;
        [SerializeField] private bool followPosition = true;
        [SerializeField] private bool followScale;
        [SerializeField] private bool counterScaleWithTarget;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int baseSortingOrder = 80;

        [Header("Fallback Palette")]
        [ColorUsage(true, true)] [SerializeField] private Color mistColor = new(0.18f, 0.025f, 0.38f, 1);
        [ColorUsage(true, true)] [SerializeField] private Color accentColor = new(0.7f, 0.08f, 2.2f, 1);

        [Header("Profiles")]
        [SerializeField] private Profile idle = new(0.84f, 0.76f, 0.76f, 0.72f);
        [SerializeField] private Profile threat = new(1.0f, 0.86f, 0.96f, 0.92f);
        [SerializeField] private Profile combat = new(1.16f, 0.94f, 1.14f, 1.12f);
        [SerializeField] private Profile cinematic = new(1.3f, 1f, 1.3f, 1.22f);
        [SerializeField] private float fadeDuration = 0.55f;

        private MaterialPropertyBlock propertyBlock;
        private Coroutine transition;
        private Profile current;
        private float alpha;
        private bool visible;
        private float[] idleRates;
        private float[] accentRates;
        private float[] idleSpeeds;
        private float[] accentSpeeds;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            if (sortingGroup == null) sortingGroup = GetComponent<SortingGroup>();
            if (attachment == null) attachment = GetComponent<DemonicMistAttachment>();
            if (premiumLayer == null) premiumLayer = GetComponent<DemonicMistAuraPremiumLayer>();
            CaptureParticleBaselines();
            ApplySorting();
            current = idle;
            alpha = 0;
            visible = false;
            ApplyVisuals();
            StopSystems(burstSystems, true);
        }

        private void Start() { PlayIdle(); Show(); }
        private void OnEnable() { if (visible) PlaySystems(); }
        private void LateUpdate() { FollowTarget(); }

        public void Show() { SetVisible(true); }
        public void Hide() { SetVisible(false); }
        public void SetVisible(bool isVisible)
        {
            visible = isVisible;
            StartTransition(isVisible ? current.alpha : 0, fadeDuration, isVisible);
        }
        public void FadeIn() { FadeIn(fadeDuration); }
        public void FadeIn(float duration) { visible = true; StartTransition(current.alpha, duration, true); }
        public void FadeOut() { FadeOut(fadeDuration); }
        public void FadeOut(float duration) { visible = false; StartTransition(0, duration, false); }
        public void SetIntensity(float value) { current.intensity = Mathf.Clamp(value, 0, 2.5f); ApplyVisuals(); }
        public void SetMasterAlpha(float value) { alpha = Mathf.Clamp01(value); ApplyVisuals(); }
        public void SetPreset(DemonicMistAuraPreset value)
        {
            current = GetProfile(value);
            ApplyParticleDynamics();
            StartTransition(current.alpha, 0.32f, value != DemonicMistAuraPreset.Dormant);
        }
        public void PlayIdle() { SetPreset(DemonicMistAuraPreset.Idle); }
        public void PlayThreat() { SetPreset(DemonicMistAuraPreset.Threat); }
        public void PlayCombat() { SetPreset(DemonicMistAuraPreset.Combat); }
        public void PlayCinematic() { SetPreset(DemonicMistAuraPreset.Cinematic); }
        public void StopAllImmediate() { visible = false; alpha = 0; StopSystems(idleSystems, true); StopSystems(accentSystems, true); StopSystems(burstSystems, true); ApplyVisuals(); }
        public void TriggerSurge() { TriggerSurge(1f); }
        public void TriggerSurge(float strength)
        {
            if (burstSystems != null)
                foreach (ParticleSystem ps in burstSystems)
                    if (ps) ps.Emit(Mathf.RoundToInt(Mathf.Lerp(8, 26, Mathf.Clamp01(strength))));
            if (premiumLayer) premiumLayer.PlayRupture();
        }
        public void TriggerRelease()
        {
            if (burstSystems != null)
                foreach (ParticleSystem ps in burstSystems)
                    if (ps) ps.Emit(20);
            if (premiumLayer) premiumLayer.PlayRupture();
        }
        public void TriggerHitPulse() { TriggerSurge(0.55f); }
        public void PlayGather() { if (premiumLayer) premiumLayer.PlayGather(); }
        public void PlayCompress() { if (premiumLayer) premiumLayer.PlayCompress(); }
        public void PlayRupture() { TriggerSurge(); }
        public void PlayPhaseTransition() { if (premiumLayer) premiumLayer.PlayPhaseTransition(); }
        public void StopPhaseTransition() { if (premiumLayer) premiumLayer.StopSequence(); }
        public void AttachTo(Transform target) { followTarget = target; if (attachment) attachment.AttachTo(target, DemonicMistAttachmentMode.BoundsFitted); }
        public void Possess(GameObject target) { if (!target) return; AttachTo(target.transform); PlayThreat(); Show(); }
        public void ReleasePossession(float duration) { FadeOut(duration); if (attachment) attachment.ClearTarget(); followTarget = null; }
        public void SetFollowTarget(Transform target) { followTarget = target; if (attachment) attachment.AttachTo(target, DemonicMistAttachmentMode.FollowTransform); }
        public void SetAnchors(Transform torso, Transform ground) { torsoAnchor = torso; groundAnchor = ground; if (attachment) attachment.SetAnchors(torso, ground); }
        public void SetSorting(string layer, int order) { sortingLayerName = layer; baseSortingOrder = order; ApplySorting(); }

        public void TimelineShow() { Show(); }
        public void TimelineHide() { Hide(); }
        public void TimelineIdle() { PlayIdle(); }
        public void TimelineThreat() { PlayThreat(); }
        public void TimelineCombat() { PlayCombat(); }
        public void TimelineCinematic() { PlayCinematic(); }
        public void TimelineSurge() { TriggerSurge(); }
        public void TimelineRelease() { TriggerRelease(); }
        public void TimelineGather() { PlayGather(); }
        public void TimelineCompress() { PlayCompress(); }
        public void TimelineRupture() { PlayRupture(); }
        public void TimelinePossess() { Show(); PlayThreat(); }
        public void TimelineReleasePossession() { ReleasePossession(fadeDuration); }

        private Profile GetProfile(DemonicMistAuraPreset preset)
        {
            return preset switch
            {
                DemonicMistAuraPreset.Threat => threat,
                DemonicMistAuraPreset.Combat => combat,
                DemonicMistAuraPreset.Cinematic => cinematic,
                DemonicMistAuraPreset.Dormant => new Profile(0, 0, 0, 0),
                _ => idle
            };
        }

        private void StartTransition(float target, float duration, bool play)
        {
            if (transition != null) StopCoroutine(transition);
            transition = StartCoroutine(Transition(target, Mathf.Max(0.01f, duration), play));
        }

        private IEnumerator Transition(float target, float duration, bool play)
        {
            if (play) PlaySystems();
            float start = alpha;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0, 1, t / duration));
                ApplyVisuals();
                yield return null;
            }
            alpha = target;
            ApplyVisuals();
            if (!play) StopSystems(idleSystems, false);
            transition = null;
        }

        private void PlaySystems() { PlaySystems(idleSystems); PlaySystems(accentSystems); }
        private static void PlaySystems(ParticleSystem[] systems)
        {
            if (systems == null) return;
            foreach (ParticleSystem ps in systems) if (ps && !ps.isPlaying) ps.Play();
        }
        private static void StopSystems(ParticleSystem[] systems, bool clear)
        {
            if (systems == null) return;
            foreach (ParticleSystem ps in systems)
                if (ps) ps.Stop(true, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
        }

        private void FollowTarget()
        {
            if (followTarget == null) return;
            if (followPosition) transform.position = followTarget.position + localOffset;
            if (followScale) transform.localScale = counterScaleWithTarget ? Vector3.one : followTarget.lossyScale;
        }

        private void ApplyVisuals()
        {
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            float strength = current.intensity * alpha;
            if (visualLayers != null && visualLayers.Length > 0)
            {
                foreach (VisualLayer layer in visualLayers)
                {
                    if (!layer.renderer) continue;
                    layer.renderer.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetFloat("_GlobalAlpha", alpha * layer.alpha);
                    propertyBlock.SetFloat("_Intensity", strength * layer.intensity);
                    propertyBlock.SetColor("_TintColor", layer.tint);
                    propertyBlock.SetColor("_EmissionColor", layer.emission);
                    layer.renderer.SetPropertyBlock(propertyBlock);
                }
            }
            else if (controlledRenderers != null)
            {
                foreach (Renderer renderer in controlledRenderers)
                {
                    if (!renderer) continue;
                    renderer.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetFloat("_GlobalAlpha", alpha);
                    propertyBlock.SetFloat("_Intensity", strength);
                    propertyBlock.SetColor("_TintColor", mistColor);
                    propertyBlock.SetColor("_EmissionColor", accentColor);
                    renderer.SetPropertyBlock(propertyBlock);
                }
            }
            if (premiumLayer) premiumLayer.SetStrength(Mathf.Clamp01(0.22f + strength * 0.42f));
        }

        private void CaptureParticleBaselines()
        {
            Capture(idleSystems, out idleRates, out idleSpeeds);
            Capture(accentSystems, out accentRates, out accentSpeeds);
        }

        private static void Capture(ParticleSystem[] systems, out float[] rates, out float[] speeds)
        {
            int count = systems == null ? 0 : systems.Length;
            rates = new float[count];
            speeds = new float[count];
            for (int i = 0; i < count; i++)
            {
                if (!systems[i]) continue;
                rates[i] = systems[i].emission.rateOverTime.constant;
                speeds[i] = systems[i].main.simulationSpeed;
            }
        }

        private void ApplyParticleDynamics()
        {
            ApplyDynamics(idleSystems, idleRates, idleSpeeds, current.density, current.motion);
            ApplyDynamics(accentSystems, accentRates, accentSpeeds, current.density, current.motion);
        }

        private static void ApplyDynamics(ParticleSystem[] systems, float[] rates, float[] speeds, float density, float motion)
        {
            if (systems == null || rates == null || speeds == null) return;
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];
                if (!ps) continue;
                ParticleSystem.EmissionModule emission = ps.emission;
                emission.rateOverTime = rates[i] * density;
                ParticleSystem.MainModule main = ps.main;
                main.simulationSpeed = speeds[i] * motion;
            }
        }

        private void ApplySorting()
        {
            if (sortingGroup)
            {
                sortingGroup.sortingLayerName = sortingLayerName;
                sortingGroup.sortingOrder = baseSortingOrder;
            }
        }
    }
}
