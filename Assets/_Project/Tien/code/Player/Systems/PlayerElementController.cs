using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerElementController
{
    private List<SkillSetSO> elementSkillSets;
    private readonly Func<SkillElementType, bool> isElementUnlocked;
    private int elementIndex;

    public SkillSetSO CurrentSkillSet { get; private set; }
    public int CurrentIndex => elementIndex;
    public int ElementCount => elementSkillSets != null ? elementSkillSets.Count : 0;

    public event Action<SkillSetSO, int, int> OnElementChanged;

    public PlayerElementController(
        List<SkillSetSO> skillSets,
        int startingIndex,
        Func<SkillElementType, bool> isElementUnlocked)
    {
        elementSkillSets = skillSets;
        elementIndex = startingIndex;
        this.isElementUnlocked = isElementUnlocked;
    }

    public void Init()
    {
        if (elementSkillSets == null || elementSkillSets.Count == 0) return;

        elementIndex = Mathf.Clamp(elementIndex, 0, elementSkillSets.Count - 1);
        if (!CanUseElementAtIndex(elementIndex))
        {
            int firstUnlockedIndex = FindFirstUnlockedElementIndex();
            if (firstUnlockedIndex >= 0)
            {
                elementIndex = firstUnlockedIndex;
            }
        }

        ApplyElement(elementIndex);
    }

    public bool CycleElement()
    {
        if (elementSkillSets == null || elementSkillSets.Count == 0) return false;

        int nextIndex = FindNextElementIndex();
        if (nextIndex < 0) return false;

        ApplyElement(nextIndex);
        return true;
    }

    public bool TryChangeElement(SkillElementType elementType)
    {
        if (elementSkillSets == null || elementSkillSets.Count == 0) return false;

        for (int i = 0; i < elementSkillSets.Count; i++)
        {
            SkillSetSO skillSet = elementSkillSets[i];
            if (!TryResolveSkillElement(skillSet, out SkillElementType resolvedElement)
                || resolvedElement != elementType
                || !IsElementUnlocked(resolvedElement))
            {
                continue;
            }

            if (i == elementIndex)
            {
                return false;
            }

            ApplyElement(i);
            return true;
        }

        return false;
    }

    public bool EnsureCurrentElementIsUnlocked()
    {
        if (elementSkillSets == null || elementSkillSets.Count == 0)
        {
            return false;
        }

        if (CanUseElementAtIndex(elementIndex))
        {
            return false;
        }

        int firstUnlockedIndex = FindFirstUnlockedElementIndex();
        if (firstUnlockedIndex < 0)
        {
            return false;
        }

        ApplyElement(firstUnlockedIndex);
        return true;
    }

    public void ChangeElement(SkillSetSO newSkillSet)
    {
        if (newSkillSet == null || elementSkillSets == null) return;
        int index = elementSkillSets.IndexOf(newSkillSet);
        if (index >= 0 && CanUseElementAtIndex(index))
        {
            ApplyElement(index);
        }
    }

    private void ApplyElement(int index)
    {
        if (elementSkillSets == null || elementSkillSets.Count == 0) return;

        elementIndex = Mathf.Clamp(index, 0, elementSkillSets.Count - 1);
        CurrentSkillSet = elementSkillSets[elementIndex];
        OnElementChanged?.Invoke(CurrentSkillSet, elementIndex, elementSkillSets.Count);
    }

    private int FindNextElementIndex()
    {
        int count = elementSkillSets.Count;
        for (int offset = 1; offset <= count; offset++)
        {
            int nextIndex = (elementIndex + offset) % count;
            if (CanUseElementAtIndex(nextIndex))
            {
                return nextIndex;
            }
        }
        return -1;
    }

    private int FindFirstUnlockedElementIndex()
    {
        if (elementSkillSets == null)
        {
            return -1;
        }

        for (int i = 0; i < elementSkillSets.Count; i++)
        {
            if (CanUseElementAtIndex(i))
            {
                return i;
            }
        }

        return -1;
    }

    private bool CanUseElementAtIndex(int index)
    {
        if (elementSkillSets == null || index < 0 || index >= elementSkillSets.Count)
        {
            return false;
        }

        SkillSetSO skillSet = elementSkillSets[index];
        return TryResolveSkillElement(skillSet, out SkillElementType elementType)
            && IsElementUnlocked(elementType);
    }

    private bool IsElementUnlocked(SkillElementType elementType)
    {
        return isElementUnlocked == null || isElementUnlocked(elementType);
    }

    private static bool TryResolveSkillElement(SkillSetSO skillSet, out SkillElementType elementType)
    {
        elementType = SkillElementType.Water;
        if (skillSet == null)
        {
            return false;
        }

        for (int i = 0; i < skillSet.SkillCount; i++)
        {
            if (!skillSet.TryGetSkill(i, out SkillData skill) || skill == null)
            {
                continue;
            }

            elementType = skill.ElementType;
            return true;
        }

        return false;
    }
}
