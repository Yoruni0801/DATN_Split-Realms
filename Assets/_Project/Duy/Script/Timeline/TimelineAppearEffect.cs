using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class TimelineAppearEffect : MonoBehaviour
{
    [Header("Target Setup")]
    [SerializeField] private GameObject targetObject;
    [SerializeField] private AnimationSpine targetSpine;
    [SerializeField] private AnimationSpine.State spineState = AnimationSpine.State.None;

    [Header("Shader Materialize Settings")]
    [SerializeField] private Material appearMaterial;
    [SerializeField] private string fallbackShaderName = "Custom/2D/MonsterMaterialize";
    [ColorUsage(true, true)] [SerializeField] private Color edgeColor = new Color(1.65f, 0.08f, 0.65f, 1f);
    [ColorUsage(true, true)] [SerializeField] private Color glowColor = new Color(0.35f, 0.02f, 0.40f, 1f);
    [ColorUsage(true, true)] [SerializeField] private Color sparkColor = new Color(2.8f, 1.05f, 0.08f, 1f);
    [SerializeField] private float dissolveNoiseScale = 8f;
    [SerializeField, Range(0.001f, 0.3f)] private float rimWidth = 0.08f;
    [SerializeField, Range(0f, 5f)] private float glowIntensity = 1.5f;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float startDelay = 0f;
    [SerializeField, Min(0f)] private float appearDuration = 0.8f;

    [Header("Effects & Audio")]
    [SerializeField] private ParticleSystem[] particleSystems;
    [SerializeField] private AudioSource[] audioSources;

    [Header("VFX Prefab")]
    [SerializeField] private GameObject vfxPrefab;
    [SerializeField] private Transform vfxSpawnPoint;
    [SerializeField] private Vector3 localOffset = Vector3.zero;
    [SerializeField, Min(0f)] private float vfxLifetime = 2f;

    [Header("Settings & Callback")]
    [SerializeField] private bool keepTargetActive = true;
    [SerializeField] private UnityEvent onAppearComplete;

    private Coroutine activeAppearRoutine;

    private struct RendererMaterialBackup
    {
        public Renderer renderer;
        public Material[] originalMaterials;
    }

    public void PlayAppear()
    {
        if (activeAppearRoutine != null)
        {
            StopCoroutine(activeAppearRoutine);
            activeAppearRoutine = null;
        }

        activeAppearRoutine = StartCoroutine(AppearRoutine());
    }

    private IEnumerator AppearRoutine()
    {
        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        GameObject target = targetObject != null ? targetObject : gameObject;
        if (target == null)
        {
            yield break;
        }

        target.SetActive(true);

        TriggerSpine(target);
        PlayParticles(target);
        PlayAudio(target);
        SpawnVfxPrefab(target);

        Shader materializeShader = appearMaterial != null ? appearMaterial.shader : Shader.Find(fallbackShaderName);
        List<RendererMaterialBackup> backups = new List<RendererMaterialBackup>();
        List<Material> runtimeMaterials = new List<Material>();

        if (materializeShader != null)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.enabled)
                {
                    continue;
                }

                Material[] origMats = r.sharedMaterials;
                backups.Add(new RendererMaterialBackup { renderer = r, originalMaterials = origMats });

                Material[] newMats = new Material[origMats.Length];
                for (int j = 0; j < origMats.Length; j++)
                {
                    Material origMat = origMats[j];
                    Material mat = appearMaterial != null ? new Material(appearMaterial) : new Material(materializeShader);

                    if (origMat != null)
                    {
                        if (origMat.HasProperty("_MainTex"))
                        {
                            mat.SetTexture("_MainTex", origMat.GetTexture("_MainTex"));
                        }
                        else if (origMat.mainTexture != null)
                        {
                            mat.mainTexture = origMat.mainTexture;
                        }
                    }

                    mat.SetColor("_EdgeColor", edgeColor);
                    mat.SetColor("_GlowColor", glowColor);
                    mat.SetColor("_SparkColor", sparkColor);
                    mat.SetFloat("_NoiseScale", dissolveNoiseScale);
                    mat.SetFloat("_EdgeWidth", rimWidth);
                    mat.SetFloat("_GlowIntensity", glowIntensity);
                    mat.SetFloat("_AppearAmount", 0f);
                    mat.SetFloat("_Alpha", 1f);

                    newMats[j] = mat;
                    runtimeMaterials.Add(mat);
                }

                r.materials = newMats;
            }
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, appearDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < runtimeMaterials.Count; i++)
            {
                if (runtimeMaterials[i] != null)
                {
                    runtimeMaterials[i].SetFloat("_AppearAmount", progress);
                }
            }

            yield return null;
        }

        for (int i = 0; i < backups.Count; i++)
        {
            if (backups[i].renderer != null)
            {
                backups[i].renderer.materials = backups[i].originalMaterials;
            }
        }

        for (int i = 0; i < runtimeMaterials.Count; i++)
        {
            if (runtimeMaterials[i] != null)
            {
                Destroy(runtimeMaterials[i]);
            }
        }

        onAppearComplete?.Invoke();

        if (!keepTargetActive && target != null)
        {
            target.SetActive(false);
        }

        activeAppearRoutine = null;
    }

    private void TriggerSpine(GameObject target)
    {
        if (targetSpine == null)
        {
            targetSpine = target.GetComponentInChildren<AnimationSpine>();
        }

        if (targetSpine == null || spineState == AnimationSpine.State.None)
        {
            return;
        }

        targetSpine.Play(spineState, false);
    }

    private void PlayParticles(GameObject target)
    {
        ParticleSystem[] systems = particleSystems != null && particleSystems.Length > 0
            ? particleSystems
            : target.GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] != null)
            {
                systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                systems[i].Play();
            }
        }
    }

    private void PlayAudio(GameObject target)
    {
        AudioSource[] sources = audioSources != null && audioSources.Length > 0
            ? audioSources
            : target.GetComponentsInChildren<AudioSource>(true);

        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] != null)
            {
                sources[i].Stop();
                sources[i].Play();
            }
        }
    }

    private void SpawnVfxPrefab(GameObject target)
    {
        if (vfxPrefab == null)
        {
            return;
        }

        Transform spawnTr = vfxSpawnPoint != null ? vfxSpawnPoint : target.transform;
        Vector3 spawnPos = spawnTr.position + spawnTr.TransformVector(localOffset);
        Quaternion spawnRot = spawnTr.rotation;

        GameObject vfxInstance = EffectPool.Spawn(vfxPrefab, spawnPos, spawnRot);
        if (vfxInstance != null && vfxLifetime > 0f)
        {
            int version = EffectPool.GetVersion(vfxInstance);
            StartCoroutine(DelayedReleaseVfx(vfxInstance, version, vfxLifetime));
        }
    }

    private static IEnumerator DelayedReleaseVfx(GameObject instance, int version, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (instance != null)
        {
            EffectPool.Release(instance, version);
        }
    }
}
