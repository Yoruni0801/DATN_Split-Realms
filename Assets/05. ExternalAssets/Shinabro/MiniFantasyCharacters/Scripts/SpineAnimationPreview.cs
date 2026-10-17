using System.Collections.Generic;
using UnityEngine;
using Spine;
using Spine.Unity;
using UnityEngine.UI;

public class SpineAnimationPreview : MonoBehaviour
{
    private SkeletonAnimation spineAnimation;
    private int currentAnimationIndex = 0;

    public Text animationNameText;
    public Dropdown animationDropdown;  // Dropdown 추가
    private List<string> animationNames = new List<string>();  // 애니메이션 목록을 동적으로 관리

    void Start()
    {
        spineAnimation = GetComponent<SkeletonAnimation>();
        if (spineAnimation != null && spineAnimation.skeletonDataAsset != null)
        {
            InitializeAnimationNames();  // 애니메이션 목록 초기화
            InitializeDropdown();  // Dropdown 초기화
            if (animationNames.Count > 0)
            {
                PlayAnimationLoop(animationNames[currentAnimationIndex]);
                UpdateAnimationNameUI();
            }
        }
    }

    // SkeletonDataAsset에서 애니메이션 목록을 동적으로 가져옴
    private void InitializeAnimationNames()
    {
        animationNames.Clear();  // 기존 애니메이션 목록 초기화
        var skeletonData = spineAnimation.skeletonDataAsset.GetSkeletonData(false);
        if (skeletonData != null)
        {
            foreach (var animation in skeletonData.Animations)
            {
                animationNames.Add(animation.Name);
            }
        }
    }

    // Dropdown 초기화 및 애니메이션 목록 추가
    private void InitializeDropdown()
    {
        if (animationDropdown != null)
        {
            animationDropdown.ClearOptions();
            animationDropdown.AddOptions(animationNames);

            // Dropdown이 변경될 때 애니메이션 변경 리스너 추가
            animationDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
        }
    }

    // Dropdown에서 선택된 애니메이션 재생
    private void OnDropdownValueChanged(int index)
    {
        currentAnimationIndex = index;
        PlayAnimationLoop(animationNames[currentAnimationIndex]);
        UpdateAnimationNameUI();
    }

    public void PlayNextAnimation()
    {
        if (spineAnimation == null || spineAnimation.AnimationState == null || animationNames.Count == 0)
        {
            return;
        }

        currentAnimationIndex = (currentAnimationIndex + 1) % animationNames.Count;
        spineAnimation.AnimationState.SetAnimation(0, animationNames[currentAnimationIndex], true);
        UpdateAnimationNameUI();

        // Dropdown에서도 애니메이션 변경을 반영
        if (animationDropdown != null)
        {
            animationDropdown.value = currentAnimationIndex;
        }
    }

    private void PlayAnimationLoop(string animationName)
    {
        if (spineAnimation == null || spineAnimation.AnimationState == null)
        {
            return;
        }

        spineAnimation.AnimationState.SetAnimation(0, animationName, true);
    }

    private void UpdateAnimationNameUI()
    {
        if (animationNameText != null && animationNames.Count > 0)
        {
            animationNameText.text = animationNames[currentAnimationIndex];
        }

        // Dropdown의 선택을 업데이트
        if (animationDropdown != null)
        {
            animationDropdown.value = currentAnimationIndex;
        }
    }
}
