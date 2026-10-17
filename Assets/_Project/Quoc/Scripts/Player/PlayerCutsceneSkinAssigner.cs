using System;
using System.Reflection;
using CutsceneEngine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public class PlayerCutsceneSkinAssigner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Cutscene cutscene;
    [SerializeField] private SimpleSpineSkinAssigner skinAssigner;
    [SerializeField] private SkeletonDataAsset skeletonDataAsset;

    [Header("Skin Assigner State")]
    [SerializeField] private bool disableAssignerWhileCutscenePlaying = true;
    [SerializeField] private bool enableAssignerAfterCutscene = true;
    [SerializeField] private bool applySkinWhenCutsceneFinished = true;

    [Header("Skin After Cutscene")]
    [SpineSkin(dataField: nameof(skeletonDataAsset))]
    [SerializeField] private string bodySkin;

    [SpineSkin(dataField: nameof(skeletonDataAsset))]
    [SerializeField] private string hairSkin;

    [SpineSkin(dataField: nameof(skeletonDataAsset))]
    [SerializeField] private string headSkin;

    [SpineSkin(dataField: nameof(skeletonDataAsset))]
    [SerializeField] private string leftHandWeaponSkin;

    [SpineSkin(dataField: nameof(skeletonDataAsset))]
    [SerializeField] private string rightHandWeaponSkin;

    private static readonly BindingFlags InstanceMethodFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private bool isSkinAppliedAfterCutscene = false;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        SubscribeCutsceneEvents();

        if (cutscene != null)
        {
            ApplyAssignerState(cutscene.state);
        }
    }

    private void Start()
    {
        CacheReferences();

        if (cutscene != null && cutscene.state == CutsceneState.Playing)
        {
            ApplyAssignerState(CutsceneState.Playing);
        }
        else
        {
            // If no cutscene is playing on Start, ensure default state is applied
            if (enableAssignerAfterCutscene)
            {
                SetSkinAssignerEnabled(true);
            }

            if (applySkinWhenCutsceneFinished && !isSkinAppliedAfterCutscene)
            {
                ApplyConfiguredSkin();
            }
        }
    }

    private void OnDisable()
    {
        UnsubscribeCutsceneEvents();
    }

    private void SubscribeCutsceneEvents()
    {
        if (cutscene != null)
        {
            cutscene.onStateChanged -= HandleCutsceneStateChanged;
            cutscene.onStateChanged += HandleCutsceneStateChanged;

            if (cutscene.director != null)
            {
                cutscene.director.stopped -= HandleDirectorStopped;
                cutscene.director.stopped += HandleDirectorStopped;
            }
        }

        Cutscene.onCutsceneStopped -= HandleGlobalCutsceneStopped;
        Cutscene.onCutsceneStopped += HandleGlobalCutsceneStopped;
    }

    private void UnsubscribeCutsceneEvents()
    {
        if (cutscene != null)
        {
            cutscene.onStateChanged -= HandleCutsceneStateChanged;

            if (cutscene.director != null)
            {
                cutscene.director.stopped -= HandleDirectorStopped;
            }
        }

        Cutscene.onCutsceneStopped -= HandleGlobalCutsceneStopped;
    }

    private void HandleDirectorStopped(PlayableDirector director)
    {
        OnCutsceneFinished();
    }

    private void HandleGlobalCutsceneStopped(Cutscene stoppedCutscene)
    {
        if (stoppedCutscene == cutscene || cutscene == null)
        {
            OnCutsceneFinished();
        }
    }

    public void SetSkinAssignerEnabled(bool enabled)
    {
        if (skinAssigner == null)
        {
            CacheReferences();
        }

        if (skinAssigner != null)
        {
            skinAssigner.enabled = enabled;
        }
    }

    public void ApplyConfiguredSkin()
    {
        if (skinAssigner == null)
        {
            CacheReferences();
        }

        if (skinAssigner == null)
        {
            Debug.LogWarning("[PlayerCutsceneSkinAssigner] Missing SimpleSpineSkinAssigner reference.", this);
            return;
        }

        skinAssigner.bodySkin = bodySkin;
        skinAssigner.hairSkin = hairSkin;
        skinAssigner.headSkin = headSkin;
        skinAssigner.leftHandWeaponSkin = leftHandWeaponSkin;
        skinAssigner.rightHandWeaponSkin = rightHandWeaponSkin;

        InvokeSkinAssignerMethod("InitializeSkeleton");
        InvokeSkinAssignerMethod("AssignSkins");
        InvokeSkinAssignerMethod("UpdateSkeletonPose");

        // Force Spine skeleton & animation update so the new skins and weapon attachments display instantly
        var skeletonAnim = skinAssigner.GetComponent<SkeletonAnimation>();
        if (skeletonAnim != null && skeletonAnim.valid && skeletonAnim.Skeleton != null)
        {
            skeletonAnim.Skeleton.SetSlotsToSetupPose();
            skeletonAnim.Update(0f);
            skeletonAnim.LateUpdate();
        }

        isSkinAppliedAfterCutscene = true;
    }

    public void ApplySkin(
        string newBodySkin,
        string newHairSkin,
        string newHeadSkin,
        string newLeftHandWeaponSkin = "",
        string newRightHandWeaponSkin = "")
    {
        bodySkin = newBodySkin;
        hairSkin = newHairSkin;
        headSkin = newHeadSkin;
        leftHandWeaponSkin = newLeftHandWeaponSkin;
        rightHandWeaponSkin = newRightHandWeaponSkin;

        ApplyConfiguredSkin();
    }

    public void OnCutsceneFinished()
    {
        if (enableAssignerAfterCutscene)
        {
            SetSkinAssignerEnabled(true);
        }

        if (applySkinWhenCutsceneFinished)
        {
            ApplyConfiguredSkin();
        }
    }

    private void HandleCutsceneStateChanged(CutsceneState state)
    {
        ApplyAssignerState(state);

        if (state == CutsceneState.None)
        {
            OnCutsceneFinished();
        }
    }

    private void ApplyAssignerState(CutsceneState state)
    {
        if (skinAssigner == null)
        {
            CacheReferences();
            if (skinAssigner == null) return;
        }

        if (disableAssignerWhileCutscenePlaying && state == CutsceneState.Playing)
        {
            skinAssigner.enabled = false;
        }
        else if (enableAssignerAfterCutscene && state == CutsceneState.None)
        {
            skinAssigner.enabled = true;
        }
    }

    private void CacheReferences()
    {
        if (skinAssigner == null)
        {
            skinAssigner = GetComponent<SimpleSpineSkinAssigner>();
            if (skinAssigner == null)
            {
                skinAssigner = GetComponentInChildren<SimpleSpineSkinAssigner>(true);
            }
            if (skinAssigner == null)
            {
                skinAssigner = GetComponentInParent<SimpleSpineSkinAssigner>();
            }
            if (skinAssigner == null && PlayerRegistry.Current != null)
            {
                skinAssigner = PlayerRegistry.Current.GetComponentInChildren<SimpleSpineSkinAssigner>(true);
            }
            if (skinAssigner == null)
            {
#if UNITY_2023_1_OR_NEWER
                skinAssigner = UnityEngine.Object.FindFirstObjectByType<SimpleSpineSkinAssigner>();
#else
                skinAssigner = UnityEngine.Object.FindObjectOfType<SimpleSpineSkinAssigner>();
#endif
            }
        }

        if (cutscene == null)
        {
            cutscene = GetComponent<Cutscene>();
            if (cutscene == null)
            {
                cutscene = GetComponentInChildren<Cutscene>(true);
            }
            if (cutscene == null)
            {
#if UNITY_2023_1_OR_NEWER
                cutscene = UnityEngine.Object.FindFirstObjectByType<Cutscene>();
#else
                cutscene = UnityEngine.Object.FindObjectOfType<Cutscene>();
#endif
            }
        }

        if (skeletonDataAsset == null)
        {
            var skeletonDataOwner = skinAssigner != null
                ? skinAssigner.GetComponent<IHasSkeletonDataAsset>()
                : GetComponent<IHasSkeletonDataAsset>();

            skeletonDataAsset = skeletonDataOwner?.SkeletonDataAsset;
        }
    }

    private void InvokeSkinAssignerMethod(string methodName)
    {
        if (skinAssigner == null) return;

        MethodInfo methodInfo = typeof(SimpleSpineSkinAssigner).GetMethod(methodName, InstanceMethodFlags);
        if (methodInfo == null)
        {
            Debug.LogWarning($"[PlayerCutsceneSkinAssigner] Method not found: {methodName}", this);
            return;
        }

        methodInfo.Invoke(skinAssigner, null);
    }

    private void OnValidate()
    {
        CacheReferences();
    }
}
