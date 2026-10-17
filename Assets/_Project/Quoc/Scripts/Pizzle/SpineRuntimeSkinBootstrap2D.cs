using System.Collections;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SkeletonAnimation))]
[RequireComponent(typeof(SimpleSpineSkinAssigner))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class SpineRuntimeSkinBootstrap2D : MonoBehaviour
{
    [SerializeField] private bool applyOnEnable = true;
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool reapplyAfterFirstFrame = true;

    private SkeletonAnimation skeletonAnimation;
    private SimpleSpineSkinAssigner skinAssigner;
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;

    private void Awake()
    {
        CacheComponents();
        ApplyRuntimeSkin();
    }

    private void OnEnable()
    {
        if (!applyOnEnable)
        {
            return;
        }

        CacheComponents();
        ApplyRuntimeSkin();
    }

    private void Start()
    {
        if (!applyOnStart)
        {
            return;
        }

        ApplyRuntimeSkin();

        if (reapplyAfterFirstFrame)
        {
            StartCoroutine(ReapplyAfterSpineStartup());
        }
    }

    private IEnumerator ReapplyAfterSpineStartup()
    {
        yield return null;
        ApplyRuntimeSkin();
        yield return new WaitForEndOfFrame();
        ApplyRuntimeSkin();
    }

    private void CacheComponents()
    {
        if (skeletonAnimation == null)
        {
            skeletonAnimation = GetComponent<SkeletonAnimation>();
        }

        if (skinAssigner == null)
        {
            skinAssigner = GetComponent<SimpleSpineSkinAssigner>();
        }

        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }
    }

    public void ApplyRuntimeSkin()
    {
        if (skeletonAnimation == null || skinAssigner == null)
        {
            return;
        }

        if (meshRenderer != null)
        {
            meshRenderer.enabled = true;
            EnsureAtlasMaterialsAssigned();
        }

        skeletonAnimation.updateWhenInvisible = UpdateMode.FullUpdate;
        skeletonAnimation.UpdateMode = UpdateMode.FullUpdate;
        skeletonAnimation.Initialize(!skeletonAnimation.valid);

        Skeleton skeleton = skeletonAnimation.Skeleton;
        if (!skeletonAnimation.valid || skeleton == null)
        {
            return;
        }

        Skin combinedSkin = new Skin("runtime-combined-skin");
        bool hasAnySkin = false;

        hasAnySkin |= TryAddSkin(combinedSkin, skeleton, skinAssigner.bodySkin);
        hasAnySkin |= TryAddSkin(combinedSkin, skeleton, skinAssigner.hairSkin);
        hasAnySkin |= TryAddSkin(combinedSkin, skeleton, skinAssigner.headSkin);
        hasAnySkin |= TryAddSkin(combinedSkin, skeleton, skinAssigner.leftHandWeaponSkin);
        hasAnySkin |= TryAddSkin(combinedSkin, skeleton, skinAssigner.rightHandWeaponSkin);

        if (hasAnySkin)
        {
            skeleton.SetSkin(combinedSkin);
        }

        skeleton.SetSlotsToSetupPose();
        skeletonAnimation.Update(0f);
        skeletonAnimation.LateUpdate();
        EnsureAtlasMaterialsAssigned();

        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            meshFilter.sharedMesh.RecalculateBounds();
        }
    }

    private void EnsureAtlasMaterialsAssigned()
    {
        if (meshRenderer == null || skeletonAnimation == null || skeletonAnimation.skeletonDataAsset == null)
        {
            return;
        }

        Material[] currentMaterials = meshRenderer.sharedMaterials;
        if (currentMaterials != null && currentMaterials.Length > 0 && currentMaterials[0] != null)
        {
            return;
        }

        List<Material> atlasMaterials = new List<Material>();
        AtlasAssetBase[] atlasAssets = skeletonAnimation.skeletonDataAsset.atlasAssets;
        if (atlasAssets == null)
        {
            return;
        }

        foreach (AtlasAssetBase atlasAsset in atlasAssets)
        {
            if (atlasAsset == null || atlasAsset.Materials == null)
            {
                continue;
            }

            foreach (Material material in atlasAsset.Materials)
            {
                if (material != null)
                {
                    atlasMaterials.Add(material);
                }
            }
        }

        if (atlasMaterials.Count > 0)
        {
            meshRenderer.sharedMaterials = atlasMaterials.ToArray();
        }
    }

    private static bool TryAddSkin(Skin combinedSkin, Skeleton skeleton, string skinName)
    {
        if (combinedSkin == null || skeleton == null || string.IsNullOrWhiteSpace(skinName))
        {
            return false;
        }

        if (string.Equals(skinName, "none", System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Skin skin = skeleton.Data.FindSkin(skinName);
        if (skin == null)
        {
            Debug.LogWarning($"Spine skin not found in build/runtime: {skinName}");
            return false;
        }

        combinedSkin.AddSkin(skin);
        return true;
    }
}
