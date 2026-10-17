using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Spine;
using Spine.Unity;
using UnityEngine.UI;

public class SpineCombineSkins : MonoBehaviour
{
    [SpineSkin] public string[] availableHairSkins;
    [SpineSkin] public string[] availableHeadSkins;
    [SpineSkin] public string[] availableBodySkins;
    [SpineSkin] public string[] availableRightHandWeapons;
    [SpineSkin] public string[] availableLeftHandWeapons;

    public SkeletonDataAsset skeletonDataAsset;

    private SkeletonAnimation spineObject;
    private Skin combinedSkin;

    public Dropdown hairDropdown;
    public Dropdown headDropdown;
    public Dropdown bodyDropdown;
    public Dropdown rightHandWeaponDropdown;
    public Dropdown leftHandWeaponDropdown;

    public Button randomizeButton;
    public Toggle hideLeftWeaponToggle; // 새로 추가된 토글 버튼

    void Start()
    {
        spineObject = this.GetComponent<SkeletonAnimation>();
        if (spineObject == null || skeletonDataAsset == null)
        {
            Debug.LogError("SkeletonAnimation component or SkeletonDataAsset is missing.");
            return;
        }

        combinedSkin = new Skin("combinedSkin");

        // 스킨 자동 설정
        PopulateAvailableSkins();

        InitializeDropdowns();

        // 시작할 때 랜덤으로 스킨 변경
        RandomizeSkins();

        CombineSkins();
        spineObject.skeleton.SetSlotsToSetupPose();
        spineObject.LateUpdate();

        // 랜덤 버튼에 리스너 추가
        if (randomizeButton != null)
        {
            randomizeButton.onClick.AddListener(RandomizeSkins);
        }

        // 왼손 무기 숨기기 토글에 리스너 추가
        if (hideLeftWeaponToggle != null)
        {
            hideLeftWeaponToggle.onValueChanged.AddListener((isOn) => {
                ToggleLeftHandWeapon(isOn);
                ChangeSkins();
            });
        }
    }

    private void PopulateAvailableSkins()
    {
        if (skeletonDataAsset == null) return;

        var skeletonData = skeletonDataAsset.GetSkeletonData(false);
        if (skeletonData == null) return;

        List<string> hairSkins = new List<string>();
        List<string> headSkins = new List<string>();
        List<string> bodySkins = new List<string>();
        List<string> rightHandWeapons = new List<string>();
        List<string> leftHandWeapons = new List<string>();

        foreach (var skin in skeletonData.Skins)
        {
            string skinName = skin.Name.ToLower();

            if (skinName.Contains("hair"))
            {
                hairSkins.Add(skin.Name);
            }
            else if (skinName.Contains("head"))
            {
                headSkins.Add(skin.Name);
            }
            else if (skinName.Contains("body"))
            {
                bodySkins.Add(skin.Name);
            }
            else if (skinName.Contains("righthand"))
            {
                rightHandWeapons.Add(skin.Name);
            }
            else if (skinName.Contains("lefthand"))
            {
                leftHandWeapons.Add(skin.Name);
            }
        }

        availableHairSkins = hairSkins.ToArray();
        availableHeadSkins = headSkins.ToArray();
        availableBodySkins = bodySkins.ToArray();
        availableRightHandWeapons = rightHandWeapons.ToArray();
        availableLeftHandWeapons = leftHandWeapons.ToArray();
    }

    public void ChangeSkins()
    {
        CombineSkins();
    }

    private void InitializeDropdowns()
    {
        InitializeDropdown(hairDropdown, availableHairSkins, "Hair");
        InitializeDropdown(headDropdown, availableHeadSkins, "Head");
        InitializeDropdown(bodyDropdown, availableBodySkins, "Body");
        InitializeDropdown(rightHandWeaponDropdown, availableRightHandWeapons, "Right Hand");
        InitializeDropdown(leftHandWeaponDropdown, availableLeftHandWeapons, "Left Hand");
    }

    private void InitializeDropdown(Dropdown dropdown, string[] items, string categoryName)
    {
        if (dropdown != null)
        {
            dropdown.ClearOptions();
            List<string> options = new List<string> { "None" };
            options.AddRange(items.Select(GetLastWord));
            dropdown.AddOptions(options);
            dropdown.onValueChanged.AddListener((index) => { ChangeSkins(); });
        }
        else
        {
            Debug.LogWarning($"{categoryName} dropdown is not assigned.");
        }
    }

    private string GetLastWord(string fullPath)
    {
        string[] parts = fullPath.Split('/');
        return parts[parts.Length - 1];
    }

    public void CombineSkins()
    {
        if (spineObject == null || spineObject.skeleton == null || spineObject.skeleton.Data == null)
        {
            Debug.LogWarning("Skeleton or Skeleton Data is not initialized.");
            return;
        }
        combinedSkin = new Skin("combinedSkin");

        AddSelectedSkin(hairDropdown, availableHairSkins);
        AddSelectedSkin(headDropdown, availableHeadSkins);
        AddSelectedSkin(bodyDropdown, availableBodySkins);
        AddSelectedSkin(rightHandWeaponDropdown, availableRightHandWeapons);

        // 토글 버튼이 꺼져있지 않을 때만 왼손 무기 추가
        if (hideLeftWeaponToggle == null || !hideLeftWeaponToggle.isOn)
        {
            AddSelectedSkin(leftHandWeaponDropdown, availableLeftHandWeapons);
        }

        spineObject.skeleton.SetSkin(combinedSkin);
        spineObject.skeleton.SetSlotsToSetupPose();
        spineObject.LateUpdate();
    }

    private void AddSelectedSkin(Dropdown dropdown, string[] skins)
    {
        if (dropdown != null && dropdown.value > 0)
        {
            string skinName = skins[dropdown.value - 1];
            var skin = spineObject.skeleton.Data.FindSkin(skinName);
            if (skin != null)
            {
                combinedSkin.AddSkin(skin);
            }
        }
    }

    private void RandomizeSkins()
    {
        RandomizeDropdown(hairDropdown, availableHairSkins);
        RandomizeDropdown(headDropdown, availableHeadSkins);
        RandomizeDropdown(bodyDropdown, availableBodySkins);
        RandomizeDropdown(rightHandWeaponDropdown, availableRightHandWeapons);
        RandomizeDropdown(leftHandWeaponDropdown, availableLeftHandWeapons);

        CombineSkins();
    }

    private void RandomizeDropdown(Dropdown dropdown, string[] skins)
    {
        if (dropdown != null && skins.Length > 0)
        {
            int randomIndex = Random.Range(1, skins.Length + 1);
            dropdown.value = randomIndex;
        }
    }

    // 왼손 무기 슬롯을 찾아서 숨기거나 표시하는 함수
    public void ToggleLeftHandWeapon(bool hideWeapon)
    {
        if (spineObject == null || spineObject.skeleton == null) return;

        // 모든 슬롯을 검색하고 "lefthand" 또는 "left_hand"가 포함된 슬롯을 찾아 처리
        foreach (Slot slot in spineObject.skeleton.Slots)
        {
            string slotName = slot.Data.Name.ToLower();
            if (slotName.Contains("lefthand") || slotName.Contains("left_hand"))
            {
                // 무기 슬롯이면 표시 여부 설정
                slot.Attachment = hideWeapon ? null : slot.Data.AttachmentName != null
                    ? spineObject.skeleton.GetAttachment(slot.Data.Index, slot.Data.AttachmentName)
                    : null;
            }
        }

        // 변경사항 적용
        spineObject.skeleton.SetSlotsToSetupPose();
        spineObject.LateUpdate();
    }
}

public class SkinPrefabManager : MonoBehaviour
{
    public GameObject skinPrefab;

    public void InstantiateAndApplySkin()
    {
        GameObject newObject = Instantiate(skinPrefab);
        SpineCombineSkins spineCombineSkins = newObject.GetComponent<SpineCombineSkins>();

        if (spineCombineSkins != null)
        {
            spineCombineSkins.ChangeSkins();
        }
    }
}