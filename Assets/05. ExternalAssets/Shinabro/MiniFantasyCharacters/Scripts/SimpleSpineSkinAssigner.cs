using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Spine;
using Spine.Unity;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class SimpleSpineSkinAssigner : MonoBehaviour
{
    [SpineSkin(dataField: "skeletonDataAsset")] public string bodySkin;
    [SpineSkin(dataField: "skeletonDataAsset")] public string hairSkin;
    [SpineSkin(dataField: "skeletonDataAsset")] public string headSkin;
    [SpineSkin(dataField: "skeletonDataAsset")] public string leftHandWeaponSkin;
    [SpineSkin(dataField: "skeletonDataAsset")] public string rightHandWeaponSkin;

    private ISkeletonComponent skeletonComponent;
    private SkeletonDataAsset skeletonDataAsset;
    private Skin combinedSkin;
    private bool suppressPoseUpdateInAssignSkins;

#if UNITY_EDITOR
    private bool editorPoseUpdateQueued;
#endif

    void Awake()
    {
        InitializeSkeleton();
        AssignSkins();
        UpdateSkeletonPose();
    }

    void Start()
    {
        if (Application.isPlaying)
        {
            // Trong Play mode, SkeletonGraphic đã được Awake khởi tạo.
            // Nếu InitializeSkeleton/AssignSkins/UpdateSkeletonPose gọi lại Initialize(true)
            // hoặc UpdateMesh thủ công sẽ làm mất sync các CanvasRenderer của Spine.
            // Do đó chúng ta chỉ cần gán lại Skin trên SkeletonGraphic.
            if (skeletonComponent is SkeletonGraphic skeletonGraphic)
            {
                if (skeletonGraphic.IsValid)
                {
                    combinedSkin = new Skin("combinedSkin");
                    AddSkinIfExists(bodySkin);
                    AddSkinIfExists(hairSkin);
                    AddSkinIfExists(headSkin);
                    AddSkinIfExists(leftHandWeaponSkin);
                    AddSkinIfExists(rightHandWeaponSkin);
                    skeletonGraphic.Skeleton.SetSkin(combinedSkin);
                    skeletonGraphic.Skeleton.SetSlotsToSetupPose();
                }
            }
        }
    }

    void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            InitializeSkeleton();
            AssignSkins();
            UpdateSkeletonPose();
            // Editor에서 매 프레임 업데이트를 통해 스켈레톤을 강제로 업데이트
            EditorApplication.update += EditorUpdate;
        }
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        // Editor에서 업데이트 콜백 제거
        EditorApplication.update -= EditorUpdate;
#endif
    }

    // 매 프레임마다 스켈레톤 상태를 갱신하는 메서드
    private void EditorUpdate()
    {
        if (!Application.isPlaying)
        {
            InitializeSkeleton();
            AssignSkins();
            UpdateSkeletonPose();
        }
    }

    void OnValidate()
    {
#if UNITY_EDITOR
        QueueEditorPoseUpdate();
        return;
#else
        InitializeSkeleton();
        AssignSkins();
        UpdateSkeletonPose();
#endif
    }

#if UNITY_EDITOR
    private void QueueEditorPoseUpdate()
    {
        if (editorPoseUpdateQueued)
        {
            return;
        }

        editorPoseUpdateQueued = true;
        EditorApplication.delayCall += () =>
        {
            editorPoseUpdateQueued = false;
            if (this == null || Application.isPlaying)
            {
                return;
            }

            InitializeSkeleton();
            suppressPoseUpdateInAssignSkins = true;
            AssignSkins();
            suppressPoseUpdateInAssignSkins = false;
            EditorApplication.QueuePlayerLoopUpdate();
        };
    }
#endif

    void InitializeSkeleton()
    {
        skeletonComponent = GetComponent<ISkeletonComponent>();
        skeletonDataAsset = (skeletonComponent as IHasSkeletonDataAsset)?.SkeletonDataAsset;

        if (skeletonComponent == null)
        {
            Debug.LogWarning("SkeletonAnimation, SkeletonMecanim or SkeletonGraphic component is missing. Please add one to the GameObject.");
            return;
        }

        if (skeletonDataAsset == null)
        {
            Debug.LogWarning("SkeletonDataAsset could not be found on the component. Please check the assigned component.");
            return;
        }

        if (skeletonComponent is SkeletonGraphic skeletonGraphic)
        {
            skeletonGraphic.allowMultipleCanvasRenderers = true;
        }

        if (skeletonComponent.Skeleton == null)
        {
            if (skeletonComponent is SkeletonAnimation skeletonAnimation)
            {
                skeletonAnimation.Initialize(true);  // 스켈레톤을 강제로 초기화
            }
            else if (skeletonComponent is SkeletonMecanim skeletonMecanim)
            {
                skeletonMecanim.Initialize(true);  // 스켈레톤을 강제로 초기화
            }
            else if (skeletonComponent is SkeletonGraphic sg)
            {
                sg.Initialize(true);
            }
        }

        if (skeletonComponent.Skeleton == null)
        {
            Debug.LogWarning("Skeleton could not be initialized. Please check the SkeletonDataAsset.");
            return;
        }

        combinedSkin = new Skin("combinedSkin");
    }

    void AssignSkins()
    {
        if (skeletonDataAsset == null)
        {
            Debug.LogWarning("SkeletonDataAsset is missing. Please assign it in the inspector.");
            return;
        }

        var skeletonData = skeletonDataAsset.GetSkeletonData(true);
        if (skeletonData == null)
        {
            Debug.LogError("SkeletonData could not be loaded.");
            return;
        }

        if (combinedSkin == null)
        {
            combinedSkin = new Skin("combinedSkin");
        }
        else
        {
            combinedSkin.Clear();
        }

        AddSkinIfExists(bodySkin);
        AddSkinIfExists(hairSkin);
        AddSkinIfExists(headSkin);
        AddSkinIfExists(leftHandWeaponSkin);
        AddSkinIfExists(rightHandWeaponSkin);

        if (skeletonComponent.Skeleton != null)
        {
            skeletonComponent.Skeleton.SetSkin(combinedSkin);
            skeletonComponent.Skeleton.SetSlotsToSetupPose();
            if (!suppressPoseUpdateInAssignSkins)
            {
                UpdateSkeletonPose();
            }
        }
    }

    void AddSkinIfExists(string skinName)
    {
        if (!string.IsNullOrEmpty(skinName))
        {
            var skin = skeletonComponent.Skeleton?.Data.FindSkin(skinName);
            if (skin != null)
            {
                combinedSkin.AddSkin(skin);
            }
            else
            {
                Debug.LogWarning($"Skin not found: {skinName}");
            }
        }
    }

    void UpdateSkeletonPose()
    {
        if (skeletonComponent != null && skeletonComponent.Skeleton != null)
        {
            skeletonComponent.Skeleton.SetSlotsToSetupPose();
            if (skeletonComponent is SkeletonAnimation skeletonAnimation)
            {
                skeletonAnimation.Update(0);  // 강제로 업데이트
                skeletonAnimation.LateUpdate();  // LateUpdate 호출로 스켈레톤 상태를 갱신
            }
            else if (skeletonComponent is SkeletonMecanim skeletonMecanim)
            {
                skeletonMecanim.Update();
                skeletonMecanim.LateUpdate();
            }
            else if (skeletonComponent is SkeletonGraphic skeletonGraphic)
            {
                skeletonGraphic.Update(0);
                skeletonGraphic.UpdateMesh();
            }
        }
    }

    private string savedLeftHandWeaponSkin;
    private string savedRightHandWeaponSkin;
    private bool isWeaponsHiddenForSwimming;

    public bool IsWeaponsHiddenForSwimming => isWeaponsHiddenForSwimming;

    public void SetWeaponsHiddenForSwimming(bool hidden)
    {
        if (isWeaponsHiddenForSwimming == hidden) return;

        isWeaponsHiddenForSwimming = hidden;

        if (hidden)
        {
            savedLeftHandWeaponSkin = leftHandWeaponSkin;
            savedRightHandWeaponSkin = rightHandWeaponSkin;
            leftHandWeaponSkin = string.Empty;
            rightHandWeaponSkin = string.Empty;
        }
        else
        {
            leftHandWeaponSkin = savedLeftHandWeaponSkin;
            rightHandWeaponSkin = savedRightHandWeaponSkin;
        }

        InitializeSkeleton();
        AssignSkins();
        UpdateSkeletonPose();
    }
}
