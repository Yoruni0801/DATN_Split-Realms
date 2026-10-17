using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rota.VFX
{
    [DisallowMultipleComponent]
    public class ShieldAppearanceController : MonoBehaviour
    {
        [Header("Appearance Settings")]
        [SerializeField] private float duration = 2.0f;
        [SerializeField] private AnimationCurve emergenceCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool resetOnDisable = true;

        [Header("Particle Cascade Delays (0-1 progress)")]
        [Range(0f, 1f)] [SerializeField] private float topParticlesThreshold = 0.02f;    // Canopy Dust, God Rays, Fireflies
        [Range(0f, 1f)] [SerializeField] private float midParticlesThreshold = 0.35f;    // Runes, Energy Wisps, Embers
        [Range(0f, 1f)] [SerializeField] private float bottomParticlesThreshold = 0.85f; // Base Mist

        [Header("Leading Edge Falling Particles")]
        [SerializeField] private ParticleSystem firefliesParticleSystem;
        [SerializeField] private bool followWipeEdge = true;
        [SerializeField] private float localTopY = 17.5f;
        [SerializeField] private float localBottomY = -17.5f;

        [Header("Renderers (Auto-found if null)")]
        [SerializeField] private SpriteRenderer shieldSpriteRenderer;
        [SerializeField] private MeshRenderer godRaysRenderer;
        [SerializeField] private List<ParticleSystem> particleSystems = new List<ParticleSystem>();

        private static readonly int TopDownCutPropertyId = Shader.PropertyToID("_TopDownCut");
        private MaterialPropertyBlock spritePropertyBlock;
        private MaterialPropertyBlock godRaysPropertyBlock;
        private Coroutine emergenceCoroutine;
        private bool isAppeared = false;

        private void Awake()
        {
            spritePropertyBlock = new MaterialPropertyBlock();
            godRaysPropertyBlock = new MaterialPropertyBlock();
            CacheComponents();
        }

        private void OnEnable()
        {
            if (playOnEnable)
            {
                PlayEmergence();
            }
        }

        private void OnDisable()
        {
            if (emergenceCoroutine != null)
            {
                StopCoroutine(emergenceCoroutine);
                emergenceCoroutine = null;
            }

            if (resetOnDisable)
            {
                SetCutoffProgress(0f);
                StopAllParticles();
            }
        }

        [ContextMenu("Cache Components")]
        public void CacheComponents()
        {
            if (shieldSpriteRenderer == null)
            {
                shieldSpriteRenderer = GetComponent<SpriteRenderer>();
                if (shieldSpriteRenderer == null)
                {
                    shieldSpriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
                }
            }

            if (godRaysRenderer == null)
            {
                var vfxGodRays = transform.Find("shield_FX/VFX_GodRays");
                if (vfxGodRays != null)
                {
                    godRaysRenderer = vfxGodRays.GetComponent<MeshRenderer>();
                }
                else
                {
                    godRaysRenderer = GetComponentInChildren<MeshRenderer>(true);
                }
            }

            if (particleSystems == null || particleSystems.Count == 0)
            {
                particleSystems = new List<ParticleSystem>(GetComponentsInChildren<ParticleSystem>(true));
            }

            if (firefliesParticleSystem == null)
            {
                var ff = transform.Find("shield_FX/PS_Fireflies");
                if (ff != null)
                {
                    firefliesParticleSystem = ff.GetComponent<ParticleSystem>();
                }
            }
        }

        public void PlayEmergence(float customDuration = -1f)
        {
            CacheComponents();
            if (emergenceCoroutine != null)
            {
                StopCoroutine(emergenceCoroutine);
            }

            float playDuration = customDuration > 0f ? customDuration : duration;
            emergenceCoroutine = StartCoroutine(EmergenceRoutine(playDuration));
        }

        private IEnumerator EmergenceRoutine(float targetDuration)
        {
            isAppeared = false;
            float elapsed = 0f;

            SetCutoffProgress(0f);
            StopAllParticles();

            Vector3 initialFirefliesLocalPos = Vector3.zero;
            if (firefliesParticleSystem != null)
            {
                initialFirefliesLocalPos = firefliesParticleSystem.transform.localPosition;
            }

            HashSet<ParticleSystem> triggeredParticles = new HashSet<ParticleSystem>();

            while (elapsed < targetDuration)
            {
                elapsed += Time.deltaTime;
                float rawT = Mathf.Clamp01(elapsed / targetDuration);
                float progress = emergenceCurve != null ? emergenceCurve.Evaluate(rawT) : rawT;

                SetCutoffProgress(progress);
                UpdateParticleCascade(progress, triggeredParticles);

                if (followWipeEdge && firefliesParticleSystem != null)
                {
                    // Di chuyển PS_Fireflies bám theo đường ranh giới quét rơi dần từ đỉnh xuống đáy
                    float currentY = Mathf.Lerp(localTopY, localBottomY, progress);
                    Vector3 pos = firefliesParticleSystem.transform.localPosition;
                    pos.y = currentY;
                    firefliesParticleSystem.transform.localPosition = pos;
                }

                yield return null;
            }

            SetCutoffProgress(1f);
            UpdateParticleCascade(1f, triggeredParticles);

            if (firefliesParticleSystem != null)
            {
                firefliesParticleSystem.transform.localPosition = initialFirefliesLocalPos;
            }

            isAppeared = true;
            emergenceCoroutine = null;
        }

        private void SetCutoffProgress(float progress)
        {
            if (shieldSpriteRenderer != null)
            {
                shieldSpriteRenderer.GetPropertyBlock(spritePropertyBlock);
                spritePropertyBlock.SetFloat(TopDownCutPropertyId, progress);
                shieldSpriteRenderer.SetPropertyBlock(spritePropertyBlock);
            }

            if (godRaysRenderer != null)
            {
                godRaysRenderer.GetPropertyBlock(godRaysPropertyBlock);
                godRaysPropertyBlock.SetFloat(TopDownCutPropertyId, progress);
                godRaysRenderer.SetPropertyBlock(godRaysPropertyBlock);
            }
        }

        private void UpdateParticleCascade(float progress, HashSet<ParticleSystem> triggered)
        {
            for (int i = 0; i < particleSystems.Count; i++)
            {
                var ps = particleSystems[i];
                if (ps == null || triggered.Contains(ps)) continue;

                string psName = ps.gameObject.name;
                bool shouldTrigger = false;

                if (psName.Contains("Canopy") || psName.Contains("GodRays") || psName.Contains("Fireflies"))
                {
                    shouldTrigger = progress >= topParticlesThreshold;
                }
                else if (psName.Contains("Mist") || psName.Contains("Base"))
                {
                    shouldTrigger = progress >= bottomParticlesThreshold;
                }
                else
                {
                    shouldTrigger = progress >= midParticlesThreshold;
                }

                if (shouldTrigger)
                {
                    ps.Play(true);
                    triggered.Add(ps);
                }
            }
        }

        private void StopAllParticles()
        {
            for (int i = 0; i < particleSystems.Count; i++)
            {
                if (particleSystems[i] != null)
                {
                    particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (duration < 0.1f) duration = 0.1f;
        }
#endif
    }
}
