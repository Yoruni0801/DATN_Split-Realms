using System.Collections;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

public sealed class BossSpineHitFlash : MonoBehaviour
{
    private static readonly int FillPropertyId = Shader.PropertyToID("_Fill");

    [SerializeField, Min(0f)] private float flashDuration = 0.1f;
    [SerializeField, Range(0f, 1f)] private float flashAmount = 1f;
    [SerializeField, Range(0f, 1f)] private float restoreFill = 0f;
    [SerializeField] private bool useUnscaledTime;

    private AnimationSpine animationSpine;
    private SkeletonAnimation skeletonAnimation;
    private Material[] originalMaterials;
    private Material[] runtimeMaterials;
    private readonly List<Material> createdMaterials = new List<Material>();
    private Coroutine flashRoutine;
    private Color originalSkeletonColor = Color.white;
    private bool hasOriginalSkeletonColor;
    private bool usesMaterialFill;

    private void Awake()
    {
        Initialize(GetComponentInParent<AnimationSpine>());
    }

    private void OnDisable()
    {
        StopFlash();
        RestoreFlash();
    }

    private void OnDestroy()
    {
        StopFlash();
        RestoreFlash();

        for (int i = 0; i < createdMaterials.Count; i++)
        {
            Material material = createdMaterials[i];
            if (material != null)
            {
                Destroy(material);
            }
        }

        createdMaterials.Clear();
    }

    public void Initialize(AnimationSpine sourceAnimationSpine)
    {
        animationSpine = sourceAnimationSpine != null ? sourceAnimationSpine : animationSpine;
        skeletonAnimation = animationSpine != null ? animationSpine.SkeletonAnimation : null;
        if (skeletonAnimation == null)
        {
            skeletonAnimation = GetComponentInParent<SkeletonAnimation>();
        }

        CacheSkeletonColor();
        CacheRuntimeMaterials();
    }

    public void Play()
    {
        if (!enabled)
        {
            return;
        }

        if (skeletonAnimation == null)
        {
            Initialize(animationSpine);
        }

        StopFlash();
        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        ApplyFlash();

        float timer = 0f;
        while (timer < flashDuration)
        {
            timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        RestoreFlash();
        flashRoutine = null;
    }

    private void StopFlash()
    {
        if (flashRoutine == null)
        {
            return;
        }

        StopCoroutine(flashRoutine);
        flashRoutine = null;
    }

    private void ApplyFlash()
    {
        if (usesMaterialFill)
        {
            SetRuntimeFill(flashAmount);
            return;
        }

        CacheSkeletonColor();
        if (skeletonAnimation != null && skeletonAnimation.Skeleton != null)
        {
            skeletonAnimation.Skeleton.SetColor(Color.white);
        }
    }

    private void RestoreFlash()
    {
        if (usesMaterialFill)
        {
            SetRuntimeFill(restoreFill);
            return;
        }

        if (hasOriginalSkeletonColor && skeletonAnimation != null && skeletonAnimation.Skeleton != null)
        {
            skeletonAnimation.Skeleton.SetColor(originalSkeletonColor);
        }
    }

    private void CacheSkeletonColor()
    {
        if (hasOriginalSkeletonColor || skeletonAnimation == null)
        {
            return;
        }

        if (skeletonAnimation.Skeleton == null)
        {
            skeletonAnimation.Initialize(false);
        }

        if (skeletonAnimation.Skeleton == null)
        {
            return;
        }

        originalSkeletonColor = skeletonAnimation.Skeleton.GetColor();
        hasOriginalSkeletonColor = true;
    }

    private void CacheRuntimeMaterials()
    {
        if (runtimeMaterials != null || skeletonAnimation == null)
        {
            return;
        }

        originalMaterials = skeletonAnimation.CustomMaterialOverride.Keys.Count > 0
            ? null
            : skeletonAnimation.GetComponent<Renderer>()?.sharedMaterials;

        if (originalMaterials == null || originalMaterials.Length == 0)
        {
            return;
        }

        runtimeMaterials = new Material[originalMaterials.Length];
        for (int i = 0; i < originalMaterials.Length; i++)
        {
            Material original = originalMaterials[i];
            if (original == null || !original.HasProperty(FillPropertyId))
            {
                continue;
            }

            Material runtime = new Material(original)
            {
                name = $"{name}_HitFlashRuntime_{i}",
                hideFlags = HideFlags.DontSave
            };
            runtime.SetFloat(FillPropertyId, restoreFill);
            runtimeMaterials[i] = runtime;
            createdMaterials.Add(runtime);
            skeletonAnimation.CustomMaterialOverride[original] = runtime;
            usesMaterialFill = true;
        }
    }

    private void SetRuntimeFill(float value)
    {
        if (runtimeMaterials == null)
        {
            return;
        }

        for (int i = 0; i < runtimeMaterials.Length; i++)
        {
            Material material = runtimeMaterials[i];
            if (material != null && material.HasProperty(FillPropertyId))
            {
                material.SetFloat(FillPropertyId, value);
            }
        }
    }
}
