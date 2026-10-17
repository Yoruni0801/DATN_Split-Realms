using System.Collections.Generic;
using System.Collections;
using UnityEngine;

[System.Serializable]
public struct FairyElementVfxSetting
{
    public GameObject prefab;
    public Vector3 localPosition;
    public Vector3 localEulerAngles;
    public Vector3 localScale;
}

public sealed class FairyElementVfxController : MonoBehaviour
{
    private const string ElementProperty = "_Element";
    private const string SceneLightInfluenceProperty = "_SceneLightInfluence";
    private const string SelfEmissionIntensityProperty = "_SelfEmissionIntensity";
    private const string GlowColorProperty = "_GlowColor";
    private const string GlowIntensityProperty = "_GlowIntensity";
    private const string GlowPulseAmountProperty = "_GlowPulseAmount";
    private const string GlowPulseSpeedProperty = "_GlowPulseSpeed";
    private const string OuterGlowSizeProperty = "_OuterGlowSize";
    private const string AuraInstanceName = "Element Aura VFX";
    private const string TransitionInstancePrefix = "Element Transition";

    [HideInInspector]
    [SerializeField] private FairyElement element = FairyElement.Water;
    [SerializeField] private FairyElementVfxProfile vfxProfile;
    [SerializeField] private FairyElementTransitionProfile transitionProfile;
    [SerializeField] private FairySelfLitGlowProfile selfLitGlowProfile;

    private Renderer[] targetRenderers;

    [Header("Element Materials")]
    private Material waterMaterial;
    private Material windMaterial;
    private Material fireMaterial;
    private Material earthMaterial;

    [Header("Element VFX Root")]
    [SerializeField] private Transform vfxRoot;
    [SerializeField] private Transform beamOrigin;

    private MaterialPropertyBlock propertyBlock;
    private GameObject activeVfx;
    private FairyElement activeVfxElement;
    private Coroutine transitionRoutine;
    private FairyElement requestedElement;
    private FairyElement appliedElement;

    public FairyElement Element
    {
        get => element;
        set
        {
            RequestElement(value);
        }
    }

    public FairyElementVfxProfile VfxProfile => vfxProfile;
    public FairyElementTransitionProfile TransitionProfile => transitionProfile;

    public void SetElement(FairyElement nextElement)
    {
        RequestElement(nextElement);
    }

    public Transform BeamOrigin
    {
        get
        {
            if (beamOrigin != null)
            {
                return beamOrigin;
            }

            if (activeVfx != null)
            {
                return activeVfx.transform;
            }

            return vfxRoot != null ? vfxRoot : transform;
        }
    }

    public void PlayElementTransition(FairyElement nextElement)
    {
        requestedElement = nextElement;
        if (Application.isPlaying && !isActiveAndEnabled)
        {
            element = nextElement;
            appliedElement = nextElement;
            return;
        }

        if (!CanRunTransition())
        {
            ApplyElement(nextElement);
            return;
        }

        StopTransition();
        transitionRoutine = StartCoroutine(ChangeElementRoutine(nextElement));
    }

    public void SetElementIndex(int nextElement)
    {
        RequestElement((FairyElement)Mathf.Clamp(nextElement, 0, 3));
    }

    public void CycleNextElement()
    {
        SetElementIndex(((int)element + 1) % 4);
    }

    public void CyclePreviousElement()
    {
        SetElementIndex(((int)element + 3) % 4);
    }

    public void SetElementImmediate(FairyElement nextElement)
    {
        if (Application.isPlaying && !isActiveAndEnabled)
        {
            requestedElement = nextElement;
            element = nextElement;
            appliedElement = nextElement;
            return;
        }

        StopTransition();
        ApplyElement(nextElement);
    }

    public void SetElementIndexImmediate(int nextElement)
    {
        SetElementImmediate((FairyElement)Mathf.Clamp(nextElement, 0, 3));
    }

    private void OnEnable()
    {
        appliedElement = element;
        targetRenderers = null;
        if (!Application.isPlaying)
        {
            Refresh();
            return;
        }

        DestroyRuntimeVfxChildren();
        if (Application.isPlaying && transitionProfile != null && transitionProfile.HasTransition)
        {
            StopTransition();
            transitionRoutine = StartCoroutine(ChangeElementRoutine(element));
            return;
        }

        Refresh();
    }

    private void OnDisable()
    {
        StopTransition();
        ClearElementProperty();
        DestroyActiveVfx();
        DestroyTransitionVfxChildren();
    }

    private void OnValidate()
    {
        targetRenderers = null;
        FillMissingElementMaterials();
        if (Application.isPlaying)
        {
            if (isActiveAndEnabled)
            {
                RequestElement(element);
            }

            return;
        }

        Refresh();
    }

    public void Refresh()
    {
        ResolveRenderers();
        ApplyElementMaterial();
        ApplyElementToRenderers();
        if (Application.isPlaying)
        {
            RefreshVfx();
        }
    }

    private void RequestElement(FairyElement nextElement)
    {
        requestedElement = nextElement;
        if (Application.isPlaying && !isActiveAndEnabled)
        {
            element = nextElement;
            appliedElement = nextElement;
            return;
        }

        if (appliedElement == nextElement && activeVfx != null && transitionRoutine == null)
        {
            return;
        }

        if (!CanRunTransition())
        {
            ApplyElement(nextElement);
            return;
        }

        StopTransition();
        transitionRoutine = StartCoroutine(ChangeElementRoutine(nextElement));
    }

    private bool CanRunTransition()
    {
        return Application.isPlaying
            && isActiveAndEnabled
            && transitionProfile != null
            && transitionProfile.HasTransition;
    }

    private IEnumerator ChangeElementRoutine(FairyElement nextElement)
    {
        FairyElementTransitionSetting transitionSetting = transitionProfile.GetSetting(nextElement);
        HideFairyForTransition();
        SpawnTransitionVfx(transitionSetting.startVfx, nextElement, "Element Transition Start VFX");

        float delay = Mathf.Max(0f, transitionProfile.SwitchDelay);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        SpawnTransitionVfx(transitionSetting.endVfx, nextElement, "Element Transition End VFX");
        ApplyElement(nextElement);

        float cleanupDelay = Mathf.Max(0.05f, transitionProfile.CleanupDelay);
        yield return new WaitForSeconds(cleanupDelay);
        DestroyTransitionVfxChildren();
        transitionRoutine = null;
    }

    private void ApplyElement(FairyElement nextElement)
    {
        element = nextElement;
        appliedElement = nextElement;
        Refresh();
        SetFairyVisible(true);
    }

    private void HideFairyForTransition()
    {
        ResolveRenderers();
        SetFairyVisible(false);
        DestroyActiveVfx();
    }

    private void SetFairyVisible(bool visible)
    {
        ResolveRenderers();
        if (targetRenderers == null)
        {
            return;
        }

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer targetRenderer = targetRenderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            targetRenderer.enabled = visible;
        }
    }

    private void StopTransition()
    {
        if (transitionRoutine == null)
        {
            return;
        }

        StopCoroutine(transitionRoutine);
        transitionRoutine = null;
        DestroyTransitionVfxChildren();
    }

    private void ResolveRenderers()
    {
        if (targetRenderers != null && targetRenderers.Length > 0)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        List<Renderer> validRenderers = new List<Renderer>(renderers.Length);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer == null || IsVfxRenderer(targetRenderer.transform))
            {
                continue;
            }

            Material material = targetRenderer.sharedMaterial;
            if (material == null || !material.HasProperty(ElementProperty))
            {
                continue;
            }

            validRenderers.Add(targetRenderer);
        }

        targetRenderers = validRenderers.ToArray();
    }

    private void FillMissingElementMaterials()
    {
        if (waterMaterial == null)
        {
            waterMaterial = ResolveCurrentRendererMaterial();
        }

        if (windMaterial == null)
        {
            windMaterial = waterMaterial;
        }

        if (fireMaterial == null)
        {
            fireMaterial = waterMaterial;
        }

        if (earthMaterial == null)
        {
            earthMaterial = waterMaterial;
        }
    }

    private Material ResolveCurrentRendererMaterial()
    {
        ResolveRenderers();
        if (targetRenderers == null)
        {
            return null;
        }

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer targetRenderer = targetRenderers[i];
            if (targetRenderer == null || targetRenderer.sharedMaterial == null)
            {
                continue;
            }

            return targetRenderer.sharedMaterial;
        }

        return null;
    }

    private void ApplyElementMaterial()
    {
        Material material = ResolveMaterial(element);
        if (material == null || targetRenderers == null)
        {
            return;
        }

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer targetRenderer = targetRenderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            Material[] materials = targetRenderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                targetRenderer.sharedMaterial = material;
                continue;
            }

            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                materials[materialIndex] = material;
            }

            targetRenderer.sharedMaterials = materials;
        }
    }

    private void ApplyElementToRenderers()
    {
        if (targetRenderers == null)
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer targetRenderer = targetRenderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(ElementProperty, (float)element);
            ApplySelfLitProperties(targetRenderer, propertyBlock);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void ApplySelfLitProperties(Renderer targetRenderer, MaterialPropertyBlock targetBlock)
    {
        if (targetRenderer == null || targetBlock == null)
        {
            return;
        }

        Material material = targetRenderer.sharedMaterial;
        if (material == null)
        {
            return;
        }

        SetFloatIfSupported(material, targetBlock, SceneLightInfluenceProperty, ResolveSceneLightInfluence());
        SetFloatIfSupported(material, targetBlock, SelfEmissionIntensityProperty, ResolveSelfEmissionIntensity());
        SetColorIfSupported(material, targetBlock, GlowColorProperty, ResolveGlowColor());
        SetFloatIfSupported(material, targetBlock, GlowIntensityProperty, ResolveGlowIntensity());
        SetFloatIfSupported(material, targetBlock, GlowPulseAmountProperty, ResolveGlowPulseAmount());
        SetFloatIfSupported(material, targetBlock, GlowPulseSpeedProperty, ResolveGlowPulseSpeed());
        SetFloatIfSupported(material, targetBlock, OuterGlowSizeProperty, ResolveOuterGlowSize());
    }

    private float ResolveSceneLightInfluence()
    {
        return selfLitGlowProfile != null
            ? selfLitGlowProfile.SceneLightInfluence
            : FairySelfLitGlowProfile.DefaultSceneLightInfluence;
    }

    private float ResolveSelfEmissionIntensity()
    {
        return selfLitGlowProfile != null
            ? selfLitGlowProfile.SelfEmissionIntensity
            : FairySelfLitGlowProfile.DefaultSelfEmissionIntensity;
    }

    private Color ResolveGlowColor()
    {
        return selfLitGlowProfile != null
            ? selfLitGlowProfile.GlowColor
            : FairySelfLitGlowProfile.DefaultGlowColor;
    }

    private float ResolveGlowIntensity()
    {
        return selfLitGlowProfile != null
            ? selfLitGlowProfile.GlowIntensity
            : FairySelfLitGlowProfile.DefaultGlowIntensity;
    }

    private float ResolveGlowPulseAmount()
    {
        return selfLitGlowProfile != null
            ? selfLitGlowProfile.GlowPulseAmount
            : FairySelfLitGlowProfile.DefaultGlowPulseAmount;
    }

    private float ResolveGlowPulseSpeed()
    {
        return selfLitGlowProfile != null
            ? selfLitGlowProfile.GlowPulseSpeed
            : FairySelfLitGlowProfile.DefaultGlowPulseSpeed;
    }

    private float ResolveOuterGlowSize()
    {
        return selfLitGlowProfile != null
            ? selfLitGlowProfile.OuterGlowSize
            : FairySelfLitGlowProfile.DefaultOuterGlowSize;
    }

    private static void SetFloatIfSupported(
        Material material,
        MaterialPropertyBlock targetBlock,
        string propertyName,
        float value)
    {
        if (material.HasProperty(propertyName))
        {
            targetBlock.SetFloat(propertyName, value);
        }
    }

    private static void SetColorIfSupported(
        Material material,
        MaterialPropertyBlock targetBlock,
        string propertyName,
        Color value)
    {
        if (material.HasProperty(propertyName))
        {
            targetBlock.SetColor(propertyName, value);
        }
    }

    private void ClearElementProperty()
    {
        if (targetRenderers == null)
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer targetRenderer = targetRenderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            targetRenderer.SetPropertyBlock(null);
        }
    }

    private void RefreshVfx()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        FairyElementVfxSetting setting = ResolveVfxSetting(element);
        GameObject prefab = setting.prefab;
        if (activeVfx != null && activeVfxElement == element)
        {
            ApplyVfxTransform(activeVfx.transform, setting);
            return;
        }

        DestroyActiveVfx();
        if (prefab == null)
        {
            return;
        }

        Transform parent = vfxRoot != null ? vfxRoot : transform;
        activeVfx = Instantiate(prefab, parent);
        activeVfx.name = AuraInstanceName;
        activeVfxElement = element;
        ApplyVfxTransform(activeVfx.transform, setting);
    }

    private void SpawnTransitionVfx(FairyTransitionVfxSetting setting, FairyElement nextElement, string instanceName)
    {
        if (!Application.isPlaying || setting.prefab == null)
        {
            return;
        }

        Transform parent = vfxRoot != null ? vfxRoot : transform;
        GameObject vfx = Instantiate(setting.prefab, parent);
        vfx.name = instanceName;
        ApplyTransitionVfxTransform(vfx.transform, setting);
        ApplyElementToTransitionVfx(vfx, nextElement);

        if (setting.lifetime > 0f)
        {
            Destroy(vfx, setting.lifetime);
        }
    }

    private void ApplyTransitionVfxTransform(Transform vfxTransform, FairyTransitionVfxSetting setting)
    {
        if (vfxTransform == null)
        {
            return;
        }

        vfxTransform.localPosition = setting.localPosition;
        vfxTransform.localRotation = Quaternion.Euler(setting.localEulerAngles);
        vfxTransform.localScale = setting.localScale == Vector3.zero ? Vector3.one : setting.localScale;
    }

    private void ApplyElementToTransitionVfx(GameObject vfx, FairyElement nextElement)
    {
        if (vfx == null)
        {
            return;
        }

        MaterialPropertyBlock transitionBlock = new MaterialPropertyBlock();
        Renderer[] renderers = vfx.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer == null || targetRenderer.sharedMaterial == null || !targetRenderer.sharedMaterial.HasProperty(ElementProperty))
            {
                continue;
            }

            targetRenderer.GetPropertyBlock(transitionBlock);
            transitionBlock.SetFloat(ElementProperty, (float)nextElement);
            targetRenderer.SetPropertyBlock(transitionBlock);
        }
    }

    private void DestroyTransitionVfxChildren()
    {
        Transform parent = vfxRoot != null ? vfxRoot : transform;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child == null || !child.name.StartsWith(TransitionInstancePrefix, System.StringComparison.Ordinal))
            {
                continue;
            }

            DestroyVfxObject(child.gameObject);
        }
    }

    private void DestroyRuntimeVfxChildren()
    {
        Transform parent = vfxRoot != null ? vfxRoot : transform;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child == null || !IsRuntimeVfxRoot(child))
            {
                continue;
            }

            DestroyVfxObject(child.gameObject);
        }

        activeVfx = null;
    }

    private Material ResolveMaterial(FairyElement fairyElement)
    {
        switch (fairyElement)
        {
            case FairyElement.Wind:
                return windMaterial;

            case FairyElement.Fire:
                return fireMaterial;

            case FairyElement.Earth:
                return earthMaterial;

            default:
                return waterMaterial;
        }
    }

    private FairyElementVfxSetting ResolveVfxSetting(FairyElement fairyElement)
    {
        return vfxProfile != null
            ? vfxProfile.GetSetting(fairyElement)
            : new FairyElementVfxSetting { localScale = Vector3.one };
    }

    private GameObject ResolveVfxPrefab(FairyElement fairyElement)
    {
        return ResolveVfxSetting(fairyElement).prefab;
    }

    private bool IsVfxRenderer(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        if (activeVfx != null && target.IsChildOf(activeVfx.transform))
        {
            return true;
        }

        Transform parent = vfxRoot != null ? vfxRoot : transform;
        while (target != null && target != transform.parent)
        {
            if (target.parent == parent && IsRuntimeVfxRoot(target))
            {
                return true;
            }

            if (IsRuntimeVfxRoot(target))
            {
                return true;
            }

            target = target.parent;
        }

        return false;
    }

    private static bool IsRuntimeVfxRoot(Transform target)
    {
        return target != null &&
            (target.name == AuraInstanceName ||
             target.name.StartsWith(TransitionInstancePrefix, System.StringComparison.Ordinal));
    }

    private void ApplyVfxTransform(Transform vfxTransform, FairyElementVfxSetting setting)
    {
        if (vfxTransform == null)
        {
            return;
        }

        vfxTransform.localPosition = setting.localPosition;
        vfxTransform.localRotation = Quaternion.Euler(setting.localEulerAngles);
        vfxTransform.localScale = setting.localScale == Vector3.zero ? Vector3.one : setting.localScale;
    }

    private void DestroyActiveVfx()
    {
        if (activeVfx == null)
        {
            return;
        }

        DestroyVfxObject(activeVfx);
        activeVfx = null;
    }

    private void DestroyVfxObject(GameObject vfxObject)
    {
        if (vfxObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(vfxObject);
        }
        else
        {
            DestroyImmediate(vfxObject);
        }
    }
}
