using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FairyElementUnlockController : MonoBehaviour
{
    [Header("Default Unlocks")]
    [SerializeField] private bool fireUnlockedByDefault;
    [SerializeField] private bool waterUnlockedByDefault;
    [SerializeField] private bool earthUnlockedByDefault;
    [SerializeField] private bool windUnlockedByDefault;

    [Header("Debug")]
    [SerializeField] private bool logBlockedSkills = true;

    private readonly HashSet<SkillElementType> unlockedElements = new HashSet<SkillElementType>();
    private bool initialized;

    private static readonly List<FairyElementUnlockController> activeControllers = new List<FairyElementUnlockController>();

    public event Action<SkillElementType> ElementUnlocked;

    public static event Action<SkillElementType> GlobalElementUnlocked;

    private void OnEnable()
    {
        activeControllers.Add(this);
    }

    private void OnDisable()
    {
        activeControllers.Remove(this);
    }

    public static bool IsAnyElementUnlockedGlobally(SkillElementType element)
    {
        for (int i = 0; i < activeControllers.Count; i++)
        {
            var ctrl = activeControllers[i];
            if (ctrl != null && ctrl.IsElementUnlocked(element))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsElementUnlocked(SkillElementType element)
    {
        EnsureInitialized();
        return unlockedElements.Contains(element);
    }

    public bool CanUseSkill(SkillData skill)
    {
        return skill != null && IsElementUnlocked(skill.ElementType);
    }

    public void UnlockElement(SkillElementType element)
    {
        EnsureInitialized();
        if (!unlockedElements.Add(element))
        {
            return;
        }

        ElementUnlocked?.Invoke(element);
        GlobalElementUnlocked?.Invoke(element);
        Debug.Log($"[FairyElementUnlock] Unlocked {element}.", this);
    }

    public void LockElement(SkillElementType element)
    {
        EnsureInitialized();
        unlockedElements.Remove(element);
    }

    public void ResetToDefaultUnlocks()
    {
        initialized = false;
        EnsureInitialized();
    }

    public bool IsElementUnlocked(FairyElement element)
    {
        return IsElementUnlocked(MapFairyElement(element));
    }

    public bool IsElementUnlocked(int elementIndex)
    {
        return IsElementUnlocked(ResolveElementFromInt(elementIndex));
    }

    public void UnlockElement(FairyElement element)
    {
        UnlockElement(MapFairyElement(element));
    }

    public void UnlockElement(int elementIndex)
    {
        UnlockElement(ResolveElementFromInt(elementIndex));
    }

    public void UnlockElement(string elementName)
    {
        if (TryParseElement(elementName, out SkillElementType element))
        {
            UnlockElement(element);
        }
    }

    public void LockElement(FairyElement element)
    {
        LockElement(MapFairyElement(element));
    }

    public void LockElement(int elementIndex)
    {
        LockElement(ResolveElementFromInt(elementIndex));
    }

    public void UnlockFire() => UnlockElement(SkillElementType.Fire);
    public void UnlockWater() => UnlockElement(SkillElementType.Water);
    public void UnlockEarth() => UnlockElement(SkillElementType.Earth);
    public void UnlockWind() => UnlockElement(SkillElementType.Wind);

    public static SkillElementType MapFairyElement(FairyElement fairyElement)
    {
        switch (fairyElement)
        {
            case FairyElement.Fire:
                return SkillElementType.Fire;
            case FairyElement.Earth:
                return SkillElementType.Earth;
            case FairyElement.Wind:
                return SkillElementType.Wind;
            case FairyElement.Water:
            default:
                return SkillElementType.Water;
        }
    }

    public static SkillElementType ResolveElementFromInt(int value)
    {
        if (Enum.IsDefined(typeof(SkillElementType), value))
        {
            return (SkillElementType)value;
        }

        if (Enum.IsDefined(typeof(FairyElement), value))
        {
            return MapFairyElement((FairyElement)value);
        }

        return SkillElementType.Water;
    }

    public static bool TryParseElement(string elementName, out SkillElementType element)
    {
        element = SkillElementType.Water;
        if (string.IsNullOrWhiteSpace(elementName))
        {
            return false;
        }

        string trimmed = elementName.Trim().ToLowerInvariant();
        if (trimmed.Contains("earth") || trimmed.Contains("đất") || trimmed.Contains("dat"))
        {
            element = SkillElementType.Earth;
            return true;
        }

        if (trimmed.Contains("fire") || trimmed.Contains("hỏa") || trimmed.Contains("hoa"))
        {
            element = SkillElementType.Fire;
            return true;
        }

        if (trimmed.Contains("wind") || trimmed.Contains("phong"))
        {
            element = SkillElementType.Wind;
            return true;
        }

        if (trimmed.Contains("water") || trimmed.Contains("nước") || trimmed.Contains("nuoc"))
        {
            element = SkillElementType.Water;
            return true;
        }

        if (Enum.TryParse(elementName, true, out SkillElementType parsed))
        {
            element = parsed;
            return true;
        }

        return false;
    }

    public void LogBlockedSkill(SkillData skill)
    {
        if (!logBlockedSkills || skill == null)
        {
            return;
        }

        Debug.Log($"[FairyElementUnlock] Skill '{skill.SkillName}' is blocked because {skill.ElementType} is locked.", this);
    }

    private void Awake()
    {
        ResetToDefaultUnlocks();
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        unlockedElements.Clear();
        AddDefaultUnlock(SkillElementType.Fire, fireUnlockedByDefault);
        AddDefaultUnlock(SkillElementType.Water, waterUnlockedByDefault);
        AddDefaultUnlock(SkillElementType.Earth, earthUnlockedByDefault);
        AddDefaultUnlock(SkillElementType.Wind, windUnlockedByDefault);
        initialized = true;
    }

    private void AddDefaultUnlock(SkillElementType element, bool unlocked)
    {
        if (unlocked)
        {
            unlockedElements.Add(element);
        }
    }
}
