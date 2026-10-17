using UnityEngine;
using UnityEngine.UI;
using TMPro; 
using DG.Tweening;

public class SkillUIManager : MonoBehaviour
{
    [Header("Tham chiếu đến Player")]
    public Player player;

    [Header("Giao diện Hồi chiêu")]
    public Image[] cooldownImages = new Image[4];

    [Header("Icon Kỹ Năng")]
    public Image[] skillIcons = new Image[4];

    private bool[] isCoolingDown = new bool[4];
    private GameObject[] skillRoots = new GameObject[4];
    private SkillData[] cachedSkills = new SkillData[4];

    private void Start()
    {
        for (int i = 0; i < cooldownImages.Length; i++)
        {
            if (cooldownImages[i] != null)
            {
                cooldownImages[i].enabled = false;
                cooldownImages[i].fillAmount = 0f;
                skillRoots[i] = cooldownImages[i].transform.parent.gameObject;
            }
        }

        PlayerRegistry.OnCurrentChanged += HandlePlayerChanged;
        player = PlayerRegistry.Current;
        SubscribeToPlayer();
    }

    private void HandlePlayerChanged(Player newPlayer)
    {
        UnsubscribeFromPlayer();
        player = newPlayer;
        SubscribeToPlayer();
    }

    private void SubscribeToPlayer()
    {
        if (player != null && !hasSubscribed)
        {
            player.OnElementChanged += HandleElementChanged;
            hasSubscribed = true;

            if (player.currentState != null)
            {
                HandleElementChanged(player.currentState, player.CurrentElementIndex, player.ElementCount);
            }
        }
    }

    private void UnsubscribeFromPlayer()
    {
        if (player != null && hasSubscribed)
        {
            player.OnElementChanged -= HandleElementChanged;
            hasSubscribed = false;
        }
    }

    private void OnDestroy()
    {
        PlayerRegistry.OnCurrentChanged -= HandlePlayerChanged;
        UnsubscribeFromPlayer();
    }

    private bool hasSubscribed = false;

    private void HandleElementChanged(SkillSetSO currentSkillSet, int currentIndex, int count)
    {
        if (player == null) return;

        for (int i = 0; i < 4; i++)
        {
            SkillSetSO skillSet = player.GetSkillSet(i);
            SkillData skillData = null;
            if (skillSet != null && skillSet.TryGetSkill(0, out skillData) && skillData != null && skillData.Icon != null)
            {
                cachedSkills[i] = skillData;
                if (skillIcons[i] != null)
                {
                    skillIcons[i].sprite = skillData.Icon;
                    skillIcons[i].enabled = true;
                }
            }
            else
            {
                cachedSkills[i] = null;
                if (skillIcons[i] != null)
                {
                    skillIcons[i].sprite = null;
                    skillIcons[i].enabled = false;
                }
            }
        }
    }

    private void Update()
    {
        if (player == null || player.currentState == null) return;

        var activeFairies = FairyPlayerCompanionController.AllActiveFairies;
        bool hasFairy = activeFairies != null && activeFairies.Count > 0;

        // Tính toán trước trạng thái từng hệ dựa trên Tinh linh đang đi theo
        bool waterActive = false;
        bool fireActive = false;
        bool windActive = false;
        bool earthActive = false;

        if (hasFairy)
        {
            foreach (var fairy in activeFairies)
            {
                if (fairy == null || fairy.ActivePlayer == null) continue;
                switch (fairy.CurrentElement)
                {
                    case FairyElement.Water: waterActive = true; break;
                    case FairyElement.Fire: fireActive = true; break;
                    case FairyElement.Wind: windActive = true; break;
                    case FairyElement.Earth: earthActive = true; break;
                }
            }
        }

        for (int i = 0; i < 4; i++)
        {
            if (cooldownImages[i] != null && skillRoots[i] != null)
            {
                SkillData skill = cachedSkills[i];
                bool hasSkill = skill != null;
                bool isUnlocked = false;

                if (hasSkill)
                {
                    switch (skill.ElementType)
                    {
                        case SkillElementType.Water: isUnlocked = waterActive; break;
                        case SkillElementType.Fire: isUnlocked = fireActive; break;
                        case SkillElementType.Wind: isUnlocked = windActive; break;
                        case SkillElementType.Earth: isUnlocked = earthActive; break;
                    }
                }

                bool shouldBeActive = hasFairy && hasSkill && isUnlocked;
                if (skillRoots[i].activeSelf != shouldBeActive)
                {
                    skillRoots[i].SetActive(shouldBeActive);
                }

                if (!shouldBeActive) continue;

                float currentTimer = player.GetSkillCooldownTimer(i);
                float maxCooldown = skill.Cooldown;

                if (currentTimer > 0)
                {
                    if (!isCoolingDown[i])
                    {
                        isCoolingDown[i] = true;
                        Transform iconTransform = cooldownImages[i].transform.parent;
                        if (iconTransform != null)
                        {
                            iconTransform.DOKill();
                            iconTransform.localScale = Vector3.one;
                            iconTransform.DOPunchScale(new Vector3(-0.2f, -0.2f, 0f), 0.15f, 1, 0.5f);
                        }
                    }

                    if (!cooldownImages[i].enabled) cooldownImages[i].enabled = true;
                    cooldownImages[i].fillAmount = currentTimer / maxCooldown;
                }
                else
                {
                    if (isCoolingDown[i])
                    {
                        isCoolingDown[i] = false;
                        cooldownImages[i].enabled = false;
                        cooldownImages[i].fillAmount = 0f;
                    }
                }
            }
        }
    }
}