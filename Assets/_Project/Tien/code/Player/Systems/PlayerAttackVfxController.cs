using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerAttackVfxController : MonoBehaviour
{
    [Serializable]
    private class AnimationVfx
    {
        public AnimationSpine.State animation = AnimationSpine.State.NormalAttack1;
        public GameObject vfxPrefab;
        public Transform attachPoint;
        [Min(0f)] public float startDelay;
        [Tooltip("Set to 0 to keep the VFX active until the animation ends.")]
        [Min(0f)] public float activeDuration;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
    }

    [SerializeField] private List<AnimationVfx> animationVfx = new List<AnimationVfx>();

    private readonly List<GameObject> activeVfx = new List<GameObject>();
    private Coroutine playbackRoutine;

    public void Play(AnimationSpine.State animation)
    {
        StopPlayback();

        AnimationVfx configuration = FindConfiguration(animation);
        if (configuration == null || configuration.vfxPrefab == null)
        {
            return;
        }

        playbackRoutine = StartCoroutine(PlayRoutine(configuration));
    }

    public void Stop()
    {
        StopPlayback();
    }

    private IEnumerator PlayRoutine(AnimationVfx configuration)
    {
        if (configuration.startDelay > 0f)
        {
            yield return new WaitForSeconds(configuration.startDelay);
        }

        Transform parent = configuration.attachPoint != null ? configuration.attachPoint : transform;
        GameObject instance = Instantiate(configuration.vfxPrefab, parent);
        instance.transform.localPosition = configuration.localPosition;
        instance.transform.localRotation = Quaternion.Euler(configuration.localEulerAngles);
        instance.transform.localScale = configuration.localScale;

        int targetLayer = parent.gameObject.layer;
        if (targetLayer != 0)
        {
            SetLayerRecursively(instance, targetLayer);
        }

        Renderer parentRenderer = GetComponentInParent<Renderer>();
        if (parentRenderer != null)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sortingLayerID = parentRenderer.sortingLayerID;
                if (renderers[i].sortingOrder < 1000)
                {
                    renderers[i].sortingOrder += parentRenderer.sortingOrder;
                }
            }
        }

        RestartParticles(instance);
        activeVfx.Add(instance);

        if (configuration.activeDuration > 0f)
        {
            yield return new WaitForSeconds(configuration.activeDuration);

            StopAndDestroy(instance);
            activeVfx.Remove(instance);
            playbackRoutine = null;
        }
    }

    private void OnDisable()
    {
        StopPlayback();
    }

    private AnimationVfx FindConfiguration(AnimationSpine.State animation)
    {
        for (int i = 0; i < animationVfx.Count; i++)
        {
            AnimationVfx configuration = animationVfx[i];
            if (configuration != null && configuration.animation == animation)
            {
                return configuration;
            }
        }

        return null;
    }

    private void StopPlayback()
    {
        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
            playbackRoutine = null;
        }

        for (int i = activeVfx.Count - 1; i >= 0; i--)
        {
            StopAndDestroy(activeVfx[i]);
        }

        activeVfx.Clear();
    }

    private static void RestartParticles(GameObject instance)
    {
        ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            var main = particles[i].main;
            main.useUnscaledTime = true;
            particles[i].Clear(true);
            particles[i].Play(true);
        }
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null) return;
        target.layer = layer;
        Transform transform = target.transform;
        for (int i = 0; i < transform.childCount; i++)
        {
            SetLayerRecursively(transform.GetChild(i).gameObject, layer);
        }
    }

    private static void StopAndDestroy(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        Destroy(instance);
    }
}
