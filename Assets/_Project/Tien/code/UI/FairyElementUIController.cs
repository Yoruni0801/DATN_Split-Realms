using System;
using UnityEngine;
using UnityEngine.UI;

public readonly struct FairyElementUIState
{
    public readonly bool isWaterUnlocked;
    public readonly bool isFireUnlocked;
    public readonly bool isEarthUnlocked;
    public readonly bool isWindUnlocked;
    public readonly bool isWorldSwapUnlocked;
    public readonly bool isPossessionActive;
    public readonly FairyElement activeElement;

    public FairyElementUIState(bool water, bool fire, bool earth, bool wind, bool swap, bool possession, FairyElement active)
    {
        isWaterUnlocked = water;
        isFireUnlocked = fire;
        isEarthUnlocked = earth;
        isWindUnlocked = wind;
        isWorldSwapUnlocked = swap;
        isPossessionActive = possession;
        activeElement = active;
    }

    public bool HasAnyUnlock => isWaterUnlocked || isFireUnlocked || isEarthUnlocked || isWindUnlocked || isWorldSwapUnlocked;
    public bool HasAnyFairyUnlock => isWaterUnlocked || isFireUnlocked || isEarthUnlocked || isWindUnlocked;

    public bool IsElementUnlocked(FairyElement element) => element switch
    {
        FairyElement.Water => isWaterUnlocked,
        FairyElement.Fire => isFireUnlocked,
        FairyElement.Earth => isEarthUnlocked,
        FairyElement.Wind => isWindUnlocked,
        _ => false,
    };

    public static FairyElementUIState Capture(Player player, FairyPlayerCompanionController fairyController)
    {
        // Quét xem những tinh linh nào ĐANG BAY THEO.
        bool hasWaterFairy = false;
        bool hasFireFairy = false;
        bool hasEarthFairy = false;
        bool hasWindFairy = false;

        var activeFairies = FairyPlayerCompanionController.AllActiveFairies;
        if (activeFairies != null)
        {
            for (int i = 0; i < activeFairies.Count; i++)
            {
                if (activeFairies[i] == null || activeFairies[i].ActivePlayer == null) continue;
                switch (activeFairies[i].CurrentElement)
                {
                    case FairyElement.Water: hasWaterFairy = true; break;
                    case FairyElement.Fire: hasFireFairy = true; break;
                    case FairyElement.Earth: hasEarthFairy = true; break;
                    case FairyElement.Wind: hasWindFairy = true; break;
                }
            }
        }

        return new FairyElementUIState(
            hasWaterFairy,
            hasFireFairy,
            hasEarthFairy,
            hasWindFairy,
            hasWaterFairy || hasFireFairy || hasEarthFairy || hasWindFairy,
            FairyPossessionController.IsPossessionActive,
            ResolveActiveElement(player, fairyController)
        );
    }

    private static FairyElement ResolveActiveElement(Player player, FairyPlayerCompanionController fairyController)
    {
        if (player != null)
        {
            if (player.CurrentSkillSet != null && TryResolveSkillElement(player.CurrentSkillSet, out SkillElementType elem))
            {
                return MapSkillElementToFairy(elem);
            }
            return (FairyElement)Mathf.Clamp(player.CurrentElementIndex, 0, 3);
        }

        if (PlayerRegistry.Current != null)
        {
            if (PlayerRegistry.Current.CurrentSkillSet != null && TryResolveSkillElement(PlayerRegistry.Current.CurrentSkillSet, out SkillElementType elem))
            {
                return MapSkillElementToFairy(elem);
            }
            return (FairyElement)Mathf.Clamp(PlayerRegistry.Current.CurrentElementIndex, 0, 3);
        }

        if (fairyController != null)
        {
            return fairyController.CurrentElement;
        }

        return FairyElement.Water;
    }

    private static bool TryResolveSkillElement(SkillSetSO skillSet, out SkillElementType elementType)
    {
        elementType = SkillElementType.Water;
        if (skillSet == null) return false;

        for (int i = 0; i < skillSet.SkillCount; i++)
        {
            if (skillSet.TryGetSkill(i, out SkillData skill) && skill != null)
            {
                elementType = skill.ElementType;
                return true;
            }
        }
        return false;
    }

    public static FairyElement MapSkillElementToFairy(SkillElementType elementType) => elementType switch
    {
        SkillElementType.Fire => FairyElement.Fire,
        SkillElementType.Earth => FairyElement.Earth,
        SkillElementType.Wind => FairyElement.Wind,
        SkillElementType.Water => FairyElement.Water,
        _ => FairyElement.Water,
    };
}

public class FairyElementUIController : MonoBehaviour
{
    [System.Serializable]
    public struct FairyUIMapping
    {
        public FairyElement element;
        public Image fairyImage;
    }

    [Header("UI Settings")]
    [SerializeField] private FairyUIMapping[] fairyMappings;

    [SerializeField] private Color activeColor = Color.white;

    [SerializeField] private Color inactiveColor = new Color(0.30196f, 0.30196f, 0.30196f, 1f);

    [Header("References")]
    private Player player;
    private FairyPlayerCompanionController fairyController;

    private FairyElementUnlockController unlockController;

    [Header("World Swap")]
    [SerializeField] private GameObject worldSwapOwnedObject;

    private void OnEnable()
    {
        PlayerRegistry.OnCurrentChanged += HandlePlayerChanged;
        player = PlayerRegistry.Current;
        SubscribeEvents();
        UpdateUI();
    }

    private void OnDisable()
    {
        PlayerRegistry.OnCurrentChanged -= HandlePlayerChanged;
        UnsubscribeEvents();
    }

    private void HandlePlayerChanged(Player newPlayer)
    {
        UnsubscribeEvents();
        player = newPlayer;
        SubscribeEvents();
        UpdateUI();
    }

    private void SubscribeEvents()
    {
        if (player != null)
        {
            player.OnElementChanged += HandlePlayerElementChanged;
            if (unlockController == null)
            {
                player.TryGetComponent(out unlockController);
            }
        }

        if (unlockController != null)
        {
            unlockController.ElementUnlocked += HandleElementUnlocked;
        }

        FairyElementUnlockController.GlobalElementUnlocked += HandleElementUnlocked;
        WorldSwapManager.GlobalSwapUnlockStateChanged += HandleGlobalSwapUnlock;
        FairyPossessionController.GlobalPossessionStateChanged += HandlePossessionStateChanged;
        FairyPlayerCompanionController.OnActiveFairiesChanged += HandleActiveFairiesChanged;
    }

    private void UnsubscribeEvents()
    {
        if (player != null)
        {
            player.OnElementChanged -= HandlePlayerElementChanged;
        }

        if (unlockController != null)
        {
            unlockController.ElementUnlocked -= HandleElementUnlocked;
        }

        FairyElementUnlockController.GlobalElementUnlocked -= HandleElementUnlocked;
        WorldSwapManager.GlobalSwapUnlockStateChanged -= HandleGlobalSwapUnlock;
        FairyPossessionController.GlobalPossessionStateChanged -= HandlePossessionStateChanged;
        FairyPlayerCompanionController.OnActiveFairiesChanged -= HandleActiveFairiesChanged;
    }

    private void HandleActiveFairiesChanged()
    {
        UpdateUI();
    }

    private void HandlePlayerElementChanged(SkillSetSO skillSet, int index, int count)
    {
        UpdateUI();
    }

    private void HandleElementUnlocked(SkillElementType element)
    {
        UpdateUI();
    }

    private void HandleGlobalSwapUnlock(bool _)
    {
        UpdateUI();
    }

    private void HandlePossessionStateChanged(bool _)
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        FairyElementUIState state = FairyElementUIState.Capture(player, fairyController);

        if (worldSwapOwnedObject != null)
        {
            worldSwapOwnedObject.SetActive(state.isWorldSwapUnlocked);
        }

        if (fairyMappings != null)
        {
            bool hasAnyUnlocked = state.HasAnyFairyUnlock || state.isPossessionActive;

            Debug.Log($"[FairyUI] UpdateUI - Water:{state.isWaterUnlocked} Fire:{state.isFireUnlocked} Earth:{state.isEarthUnlocked} Wind:{state.isWindUnlocked} Active:{state.activeElement} Player={player!=null}");

            for (int i = 0; i < fairyMappings.Length; i++)
            {
                var mapping = fairyMappings[i];
                if (mapping.fairyImage != null)
                {
                    bool thisElementUnlocked = state.IsElementUnlocked(mapping.element);
                    bool isActive = hasAnyUnlocked && (thisElementUnlocked || state.isPossessionActive) && mapping.element == state.activeElement;
                    mapping.fairyImage.gameObject.SetActive(thisElementUnlocked || (state.isPossessionActive && mapping.element == state.activeElement));
                    mapping.fairyImage.color = isActive ? activeColor : inactiveColor;
                }
            }
        }
    }
}
