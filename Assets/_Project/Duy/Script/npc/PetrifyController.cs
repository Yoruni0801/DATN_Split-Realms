using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PetrifyController : MonoBehaviour
{
    public const float PetrifyStartedAmount = 0.001f;

    [SerializeField] private bool freezeAnimationWhenPetrified = true;
    [SerializeField] private bool disablePatrolComponentWhenPetrified = true;

    private SkeletonAnimation spineAnimation;
    [SerializeField] private bool refreshRenderersOnApply;

    [SerializeField] private Material petrifyMaterial;
    [SerializeField] private bool createMaterialFromShaderIfMissing = true;
    [SerializeField] private Shader fallbackShader;
    [SerializeField] private string fallbackShaderName = "Custom/2D/Petrify Bottom Up Group Improved";

    private bool lockPatrolWhenPetrified = true; // always true

    private const string LegacyFallbackShaderName = "Custom/2D/Petrify Bottom Up Group";

    private static readonly int PetrifyAmountId = Shader.PropertyToID("_PetrifyAmount");
    private static readonly int GroupBottomYId = Shader.PropertyToID("_GroupBottomY");
    private static readonly int GroupTopYId = Shader.PropertyToID("_GroupTopY");

    private SpineColorState[] spineColorStates;
    private AnimatorState[] animatorStates;
    private SpineAnimationState[] spineAnimationStates;
    private Rigidbody2D cachedBody;
    private NPCRandomPatrol cachedPatrol;
    private RigidbodyState rigidbodyState;
    private readonly List<Material> runtimeSpineMaterials = new List<Material>();
    private bool hasCachedOriginals;
    private bool animationFrozen;
    private bool hasRigidbodyState;
    private bool rigidbodyApplied;
    private bool movementLocked;
    private bool patrolWasEnabledBeforePetrify;
    private float petrifyAmount;
    private float applyRigidbodyAtAmount = 0.999f;
    private float freezeAnimationAtAmount = PetrifyStartedAmount;
    public float PetrifyAmount => petrifyAmount;
    private float lockMovementAtAmount = PetrifyStartedAmount;
    public bool IsPetrified => petrifyAmount >= 0.999f;
    public bool IsPetrifying => petrifyAmount >= PetrifyStartedAmount;
    private bool updateBoundsEveryFrame = true;
    private Color spineStoneColor = new Color(0.55f, 0.55f, 0.52f, 1f);

    private void Reset()
    {
        CollectRenderers();
    }

    private void Awake()
    {
        CollectRenderersIfNeeded();
        CacheOriginals();
        Apply();
        ApplyAnimationFreeze();
        ApplyPetrifiedPhysics();
        ApplyMovementLock();
    }

    private void LateUpdate()
    {
        if (petrifyAmount < PetrifyStartedAmount)
        {
            return;
        }

        if (updateBoundsEveryFrame)
        {
            Apply();
        }

        ApplyAnimationFreeze();
        ApplyPetrifiedPhysics();
        ApplyMovementLock();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        RestoreOriginals();
        RestoreAnimationPlayback();
        RestoreRigidbodyState();
        RestoreMovementLock();
    }

    private void OnDestroy()
    {
        RestoreOriginals();
        RestoreAnimationPlayback();
        RestoreRigidbodyState();
        RestoreMovementLock();
        DestroyRuntimeMaterial();
    }

    private void OnValidate()
    {
        petrifyAmount = Mathf.Clamp01(petrifyAmount);

        if (spineAnimation == null)
        {
            CollectRenderers();
        }
    }

    [ContextMenu("Collect Renderers")]
    public void CollectRenderers()
    {
        if (hasCachedOriginals)
        {
            RestoreOriginals();
        }

        DestroyRuntimeMaterial();
        spineAnimation = GetComponentInChildren<SkeletonAnimation>(true);
        hasCachedOriginals = false;
    }

    public void SetPetrifyAmount(float amount)
    {
        petrifyAmount = Mathf.Clamp01(amount);
        Apply();
        ApplyAnimationFreeze();
        ApplyPetrifiedPhysics();
        ApplyMovementLock();
    }

    public void BeginPetrifying()
    {
        SetPetrifyAmount(Mathf.Max(petrifyAmount, PetrifyStartedAmount));
    }

    public void Petrify()
    {
        SetPetrifyAmount(1f);
    }

    public void Unpetrify()
    {
        SetPetrifyAmount(0f);
    }

    private void Apply()
    {
        CollectRenderersIfNeeded();
        CacheOriginals();

        bool hasSpineRenderer = spineColorStates != null && spineColorStates.Length > 0;
        if (!hasSpineRenderer)
        {
            return;
        }

        if (petrifyAmount < PetrifyStartedAmount)
        {
            RestoreOriginals();
            RestoreAnimationPlayback();
            RestoreRigidbodyState();
            RestoreMovementLock();
            return;
        }

        Bounds groupBounds = GetGroupBounds();
        float bottomY = groupBounds.min.y;
        float topY = groupBounds.max.y;
        ApplySpinePetrify(bottomY, topY);
    }

    private void CollectRenderersIfNeeded()
    {
        if (spineAnimation == null)
        {
            CollectRenderers();
            return;
        }

        if (refreshRenderersOnApply && !hasCachedOriginals)
        {
            CollectRenderers();
        }
    }

    private void CacheOriginals()
    {
        if (hasCachedOriginals)
        {
            return;
        }

        SkeletonAnimation[] skeletonAnimations = GetComponentsInChildren<SkeletonAnimation>(true);
        spineColorStates = new SpineColorState[skeletonAnimations.Length];

        for (int i = 0; i < skeletonAnimations.Length; i++)
        {
            SkeletonAnimation currentSpineAnimation = skeletonAnimations[i];
            if (currentSpineAnimation != null && currentSpineAnimation.Skeleton == null)
            {
                currentSpineAnimation.Initialize(false);
            }

            Renderer spineRenderer = currentSpineAnimation != null
                ? currentSpineAnimation.GetComponent<Renderer>()
                : null;

            Material[] originalMaterials = spineRenderer != null ? spineRenderer.sharedMaterials : null;
            Material[] previousOverrideMaterials;
            bool[] hadPreviousOverride;
            GetExistingSpineOverrides(
                currentSpineAnimation,
                originalMaterials,
                out previousOverrideMaterials,
                out hadPreviousOverride);

            spineColorStates[i] = new SpineColorState(
                currentSpineAnimation,
                currentSpineAnimation != null && currentSpineAnimation.Skeleton != null
                    ? currentSpineAnimation.Skeleton.GetColor()
                    : Color.white,
                spineRenderer,
                originalMaterials,
                previousOverrideMaterials,
                hadPreviousOverride);
        }

        hasCachedOriginals = true;
    }

    private void GetExistingSpineOverrides(
        SkeletonAnimation spineAnimation,
        Material[] originalMaterials,
        out Material[] previousOverrideMaterials,
        out bool[] hadPreviousOverride)
    {
        if (originalMaterials == null)
        {
            previousOverrideMaterials = null;
            hadPreviousOverride = null;
            return;
        }

        previousOverrideMaterials = new Material[originalMaterials.Length];
        hadPreviousOverride = new bool[originalMaterials.Length];
        if (spineAnimation == null)
        {
            return;
        }

        for (int i = 0; i < originalMaterials.Length; i++)
        {
            Material originalMaterial = originalMaterials[i];
            if (originalMaterial != null
                && spineAnimation.CustomMaterialOverride.TryGetValue(
                    originalMaterial,
                    out Material previousOverride))
            {
                previousOverrideMaterials[i] = previousOverride;
                hadPreviousOverride[i] = true;
            }
        }
    }

    private void ApplySpinePetrify(float bottomY, float topY)
    {
        if (spineColorStates == null)
        {
            return;
        }

        for (int i = 0; i < spineColorStates.Length; i++)
        {
            SpineColorState state = spineColorStates[i];
            ApplySpinePetrify(ref state, bottomY, topY);
            spineColorStates[i] = state;
        }
    }

    private void ApplySpinePetrify(ref SpineColorState state, float bottomY, float topY)
    {
        SkeletonAnimation targetSpineAnimation = state.SkeletonAnimation;
        if (targetSpineAnimation == null)
        {
            return;
        }

        if (targetSpineAnimation.Skeleton == null)
        {
            targetSpineAnimation.Initialize(false);
        }

        if (targetSpineAnimation.Skeleton == null)
        {
            return;
        }

        Material[] runtimeMaterials = GetRuntimeSpineMaterials(ref state);
        if (runtimeMaterials != null && runtimeMaterials.Length > 0)
        {
            ApplySpineMaterialOverride(state, runtimeMaterials);
            SetPetrifyMaterialValues(runtimeMaterials, bottomY, topY);
            targetSpineAnimation.Skeleton.SetColor(state.OriginalColor);
            return;
        }

        Color color = Color.Lerp(state.OriginalColor, spineStoneColor, petrifyAmount);
        targetSpineAnimation.Skeleton.SetColor(color);
    }

    private Material[] GetRuntimeSpineMaterials(ref SpineColorState state)
    {
        if (state.RuntimeMaterials != null && state.RuntimeMaterials.Length > 0)
        {
            return state.RuntimeMaterials;
        }

        Material[] originalMaterials = state.OriginalMaterials;
        if (originalMaterials == null || originalMaterials.Length == 0)
        {
            RefreshOriginalMaterials(ref state);
            originalMaterials = state.OriginalMaterials;
            if (originalMaterials == null || originalMaterials.Length == 0)
            {
                return null;
            }
        }

        if (petrifyMaterial == null && !createMaterialFromShaderIfMissing)
        {
            return null;
        }

        Shader shader = petrifyMaterial == null ? GetPetrifyShader() : null;
        if (petrifyMaterial == null && shader == null)
        {
            return null;
        }

        Material[] materials = new Material[originalMaterials.Length];
        for (int i = 0; i < originalMaterials.Length; i++)
        {
            Material originalMaterial = originalMaterials[i];
            Material material = CreateSpinePetrifyMaterial(originalMaterial, shader, i);
            materials[i] = material;
        }

        state.RuntimeMaterials = materials;
        return materials;
    }

    private Material CreateSpinePetrifyMaterial(Material originalMaterial, Shader shader, int index)
    {
        Material material = petrifyMaterial != null
            ? new Material(petrifyMaterial)
            : new Material(shader);

        material.name = $"{name}_SpinePetrifyRuntime_{index}";
        material.hideFlags = HideFlags.DontSave;

        CopyOriginalMaterialData(originalMaterial, material);
        runtimeSpineMaterials.Add(material);
        return material;
    }

    private void RefreshOriginalMaterials(ref SpineColorState state)
    {
        Renderer renderer = state.Renderer;
        if (renderer == null)
        {
            SkeletonAnimation targetSpineAnimation = state.SkeletonAnimation;
            renderer = targetSpineAnimation != null
                ? targetSpineAnimation.GetComponent<Renderer>()
                : null;
            state.Renderer = renderer;
        }

        Material[] originalMaterials = renderer != null ? renderer.sharedMaterials : null;
        if (originalMaterials == null || originalMaterials.Length == 0)
        {
            return;
        }

        Material[] previousOverrideMaterials;
        bool[] hadPreviousOverride;
        GetExistingSpineOverrides(
            state.SkeletonAnimation,
            originalMaterials,
            out previousOverrideMaterials,
            out hadPreviousOverride);

        state.OriginalMaterials = originalMaterials;
        state.PreviousOverrideMaterials = previousOverrideMaterials;
        state.HadPreviousOverride = hadPreviousOverride;
    }

    private void CopyOriginalMaterialData(Material originalMaterial, Material targetMaterial)
    {
        if (originalMaterial == null || targetMaterial == null)
        {
            return;
        }

        if (originalMaterial.HasProperty("_MainTex") && targetMaterial.HasProperty("_MainTex"))
        {
            targetMaterial.SetTexture("_MainTex", originalMaterial.GetTexture("_MainTex"));
        }

        if (originalMaterial.HasProperty("_Color") && targetMaterial.HasProperty("_Color"))
        {
            targetMaterial.SetColor("_Color", originalMaterial.GetColor("_Color"));
        }

        targetMaterial.renderQueue = originalMaterial.renderQueue;
    }

    private void ApplySpineMaterialOverride(SpineColorState state, Material[] runtimeMaterials)
    {
        SkeletonAnimation spineAnimation = state.SkeletonAnimation;
        Material[] originalMaterials = state.OriginalMaterials;
        if (spineAnimation == null || originalMaterials == null)
        {
            return;
        }

        for (int i = 0; i < originalMaterials.Length && i < runtimeMaterials.Length; i++)
        {
            Material originalMaterial = originalMaterials[i];
            Material runtimeMaterial = runtimeMaterials[i];
            if (originalMaterial != null && runtimeMaterial != null)
            {
                spineAnimation.CustomMaterialOverride[originalMaterial] = runtimeMaterial;
            }
        }
    }

    private void RemoveSpineMaterialOverride(SpineColorState state)
    {
        SkeletonAnimation spineAnimation = state.SkeletonAnimation;
        Material[] originalMaterials = state.OriginalMaterials;
        if (spineAnimation == null || originalMaterials == null)
        {
            return;
        }

        for (int i = 0; i < originalMaterials.Length; i++)
        {
            Material originalMaterial = originalMaterials[i];
            if (originalMaterial == null)
            {
                continue;
            }

            bool restorePrevious = state.HadPreviousOverride != null
                && i < state.HadPreviousOverride.Length
                && state.HadPreviousOverride[i];
            if (restorePrevious)
            {
                Material previousOverride = state.PreviousOverrideMaterials != null
                    && i < state.PreviousOverrideMaterials.Length
                    ? state.PreviousOverrideMaterials[i]
                    : null;

                if (previousOverride != null)
                {
                    spineAnimation.CustomMaterialOverride[originalMaterial] = previousOverride;
                    continue;
                }
            }

            spineAnimation.CustomMaterialOverride.Remove(originalMaterial);
        }
    }

    private void SetPetrifyMaterialValues(Material[] materials, float bottomY, float topY)
    {
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null)
            {
                continue;
            }

            if (material.HasProperty(PetrifyAmountId))
            {
                material.SetFloat(PetrifyAmountId, petrifyAmount);
            }

            if (material.HasProperty(GroupBottomYId))
            {
                material.SetFloat(GroupBottomYId, bottomY);
            }

            if (material.HasProperty(GroupTopYId))
            {
                material.SetFloat(GroupTopYId, topY);
            }
        }
    }

    private Bounds GetGroupBounds()
    {
        Bounds bounds = new Bounds(transform.position, Vector3.one);

        if (spineColorStates == null || spineColorStates.Length == 0)
        {
            return bounds;
        }

        bool hasBounds = false;
        for (int i = 0; i < spineColorStates.Length; i++)
        {
            Renderer spineRenderer = spineColorStates[i].Renderer;
            if (spineRenderer == null || !spineRenderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = spineRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(spineRenderer.bounds);
            }
        }

        return bounds;
    }

    private Shader GetPetrifyShader()
    {
        if (fallbackShader != null)
        {
            return fallbackShader;
        }

        Shader shader = Shader.Find(fallbackShaderName);
        if (shader == null)
        {
            shader = Shader.Find(LegacyFallbackShaderName);
        }

        return shader;
    }

    private void RestoreOriginals()
    {
        if (!hasCachedOriginals)
        {
            return;
        }

        if (spineColorStates == null)
        {
            return;
        }

        for (int i = 0; i < spineColorStates.Length; i++)
        {
            SpineColorState state = spineColorStates[i];
            SkeletonAnimation targetSpineAnimation = state.SkeletonAnimation;
            if (targetSpineAnimation != null)
            {
                if (targetSpineAnimation.Skeleton == null)
                {
                    targetSpineAnimation.Initialize(false);
                }

                if (targetSpineAnimation.Skeleton != null)
                {
                    targetSpineAnimation.Skeleton.SetColor(state.OriginalColor);
                }

                RemoveSpineMaterialOverride(state);
            }
        }
    }

    private void DestroyRuntimeMaterial()
    {
        DestroyRuntimeSpineMaterials();
    }

    private void DestroyRuntimeSpineMaterials()
    {
        for (int i = 0; i < runtimeSpineMaterials.Count; i++)
        {
            DestroyMaterial(runtimeSpineMaterials[i]);
        }

        runtimeSpineMaterials.Clear();
    }

    private void DestroyMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(material);
        }
        else
        {
            DestroyImmediate(material);
        }
    }

    private void ApplyAnimationFreeze()
    {
        if (!freezeAnimationWhenPetrified)
        {
            RestoreAnimationPlayback();
            return;
        }

        if (petrifyAmount >= freezeAnimationAtAmount)
        {
            FreezeAnimationPlayback();
        }
        else
        {
            RestoreAnimationPlayback();
        }
    }

    private void FreezeAnimationPlayback()
    {
        if (animationFrozen)
        {
            KeepAnimationFrozen();
            return;
        }

        Animator[] animators = GetComponentsInChildren<Animator>(true);
        animatorStates = new AnimatorState[animators.Length];
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            animatorStates[i] = new AnimatorState(
                animator,
                animator != null ? animator.speed : 1f);

            if (animator != null)
            {
                animator.speed = 0f;
            }
        }

        SkeletonAnimation[] skeletonAnimations = GetComponentsInChildren<SkeletonAnimation>(true);
        spineAnimationStates = new SpineAnimationState[skeletonAnimations.Length];
        for (int i = 0; i < skeletonAnimations.Length; i++)
        {
            SkeletonAnimation skeletonAnimation = skeletonAnimations[i];
            spineAnimationStates[i] = new SpineAnimationState(
                skeletonAnimation,
                skeletonAnimation != null ? skeletonAnimation.timeScale : 1f);

            if (skeletonAnimation != null)
            {
                skeletonAnimation.timeScale = 0f;
            }
        }

        animationFrozen = true;
    }

    private void KeepAnimationFrozen()
    {
        if (animatorStates != null)
        {
            for (int i = 0; i < animatorStates.Length; i++)
            {
                Animator animator = animatorStates[i].Animator;
                if (animator != null)
                {
                    animator.speed = 0f;
                }
            }
        }

        if (spineAnimationStates != null)
        {
            for (int i = 0; i < spineAnimationStates.Length; i++)
            {
                SkeletonAnimation skeletonAnimation = spineAnimationStates[i].SkeletonAnimation;
                if (skeletonAnimation != null)
                {
                    skeletonAnimation.timeScale = 0f;
                }
            }
        }
    }

    private void RestoreAnimationPlayback()
    {
        if (!animationFrozen)
        {
            return;
        }

        if (animatorStates != null)
        {
            for (int i = 0; i < animatorStates.Length; i++)
            {
                Animator animator = animatorStates[i].Animator;
                if (animator != null)
                {
                    animator.speed = animatorStates[i].OriginalSpeed;
                }
            }
        }

        if (spineAnimationStates != null)
        {
            for (int i = 0; i < spineAnimationStates.Length; i++)
            {
                SkeletonAnimation skeletonAnimation = spineAnimationStates[i].SkeletonAnimation;
                if (skeletonAnimation != null)
                {
                    skeletonAnimation.timeScale = spineAnimationStates[i].OriginalTimeScale;
                }
            }
        }

        animatorStates = null;
        spineAnimationStates = null;
        animationFrozen = false;
    }

    private void ApplyPetrifiedPhysics()
    {
        if (petrifyAmount >= applyRigidbodyAtAmount)
        {
            ApplyRigidbodyState();
        }
        else
        {
            RestoreRigidbodyState();
        }
    }

    private void ApplyRigidbodyState()
    {
        if (rigidbodyApplied)
        {
            KeepRigidbodyPetrified();
            return;
        }

        cachedBody = GetComponent<Rigidbody2D>();
        if (cachedBody == null)
        {
            return;
        }

        rigidbodyState = new RigidbodyState(
            cachedBody.bodyType,
            cachedBody.constraints,
            cachedBody.simulated,
            cachedBody.gravityScale,
            cachedBody.linearVelocity,
            cachedBody.angularVelocity);

        hasRigidbodyState = true;
        rigidbodyApplied = true;
        KeepRigidbodyPetrified();
    }

    private void KeepRigidbodyPetrified()
    {
        if (cachedBody == null)
        {
            return;
        }

        cachedBody.simulated = true;
        cachedBody.bodyType = RigidbodyType2D.Dynamic;

        cachedBody.WakeUp();
    }

    private void RestoreRigidbodyState()
    {
        if (!rigidbodyApplied)
        {
            return;
        }

        if (cachedBody != null)
        {
            if (hasRigidbodyState)
            {
                cachedBody.bodyType = rigidbodyState.BodyType;
                cachedBody.constraints = rigidbodyState.Constraints;
                cachedBody.simulated = rigidbodyState.Simulated;
                cachedBody.gravityScale = rigidbodyState.GravityScale;
                cachedBody.linearVelocity = rigidbodyState.LinearVelocity;
                cachedBody.angularVelocity = rigidbodyState.AngularVelocity;
            }
        }

        cachedBody = null;
        hasRigidbodyState = false;
        rigidbodyApplied = false;
    }

    private void ApplyMovementLock()
    {
        if (!lockPatrolWhenPetrified)
        {
            RestoreMovementLock();
            return;
        }

        if (petrifyAmount >= GetMovementLockThreshold())
        {
            LockMovement();
        }
        else
        {
            RestoreMovementLock();
        }
    }

    private void LockMovement()
    {
        if (movementLocked)
        {
            KeepMovementLocked();
            return;
        }

        cachedPatrol = GetComponent<NPCRandomPatrol>();
        if (cachedPatrol == null)
        {
            return;
        }

        patrolWasEnabledBeforePetrify = cachedPatrol.enabled;
        cachedPatrol.SetTimelinePaused(true, false);

        if (disablePatrolComponentWhenPetrified)
        {
            cachedPatrol.enabled = false;
        }

        movementLocked = true;
        KeepMovementLocked();
    }

    private float GetMovementLockThreshold()
    {
        return Mathf.Min(lockMovementAtAmount, PetrifyStartedAmount);
    }

    private void KeepMovementLocked()
    {
        if (cachedPatrol != null)
        {
            cachedPatrol.SetTimelinePaused(true, false);

            if (disablePatrolComponentWhenPetrified)
            {
                cachedPatrol.enabled = false;
            }
        }

        if (cachedBody != null)
        {
            cachedBody.linearVelocity = new Vector2(0f, cachedBody.linearVelocity.y);
        }
    }

    private void RestoreMovementLock()
    {
        if (!movementLocked)
        {
            return;
        }

        if (cachedPatrol != null)
        {
            cachedPatrol.enabled = patrolWasEnabledBeforePetrify;

            if (patrolWasEnabledBeforePetrify)
            {
                cachedPatrol.SetTimelinePaused(false);
            }
        }

        cachedPatrol = null;
        movementLocked = false;
        patrolWasEnabledBeforePetrify = false;
    }

    private struct SpineColorState
    {
        public SkeletonAnimation SkeletonAnimation;
        public Color OriginalColor;
        public Renderer Renderer;
        public Material[] OriginalMaterials;
        public Material[] PreviousOverrideMaterials;
        public bool[] HadPreviousOverride;
        public Material[] RuntimeMaterials;

        public SpineColorState(
            SkeletonAnimation skeletonAnimation,
            Color originalColor,
            Renderer renderer,
            Material[] originalMaterials,
            Material[] previousOverrideMaterials,
            bool[] hadPreviousOverride)
        {
            SkeletonAnimation = skeletonAnimation;
            OriginalColor = originalColor;
            Renderer = renderer;
            OriginalMaterials = originalMaterials;
            PreviousOverrideMaterials = previousOverrideMaterials;
            HadPreviousOverride = hadPreviousOverride;
            RuntimeMaterials = null;
        }
    }

    private struct AnimatorState
    {
        public Animator Animator;
        public float OriginalSpeed;

        public AnimatorState(Animator animator, float originalSpeed)
        {
            Animator = animator;
            OriginalSpeed = originalSpeed;
        }
    }

    private struct SpineAnimationState
    {
        public SkeletonAnimation SkeletonAnimation;
        public float OriginalTimeScale;

        public SpineAnimationState(SkeletonAnimation skeletonAnimation, float originalTimeScale)
        {
            SkeletonAnimation = skeletonAnimation;
            OriginalTimeScale = originalTimeScale;
        }
    }

    private struct RigidbodyState
    {
        public RigidbodyType2D BodyType;
        public RigidbodyConstraints2D Constraints;
        public bool Simulated;
        public float GravityScale;
        public Vector2 LinearVelocity;
        public float AngularVelocity;

        public RigidbodyState(
            RigidbodyType2D bodyType,
            RigidbodyConstraints2D constraints,
            bool simulated,
            float gravityScale,
            Vector2 linearVelocity,
            float angularVelocity)
        {
            BodyType = bodyType;
            Constraints = constraints;
            Simulated = simulated;
            GravityScale = gravityScale;
            LinearVelocity = linearVelocity;
            AngularVelocity = angularVelocity;
        }
    }
}
