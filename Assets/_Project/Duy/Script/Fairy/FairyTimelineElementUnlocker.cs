using UnityEngine;

public sealed class FairyTimelineElementUnlocker : MonoBehaviour
{
    [SerializeField] private FairyElementUnlockController target;
    [SerializeField] private SkillElementType elementToUnlock = SkillElementType.Water;
    [SerializeField] private bool useCurrentPlayerWhenTargetMissing = true;
    [SerializeField] private bool addUnlockControllerIfMissing = true;

    public void UnlockConfiguredElement()
    {
        Unlock(elementToUnlock);
    }

    public void UnlockFire() => Unlock(SkillElementType.Fire);
    public void UnlockWater() => Unlock(SkillElementType.Water);
    public void UnlockEarth() => Unlock(SkillElementType.Earth);
    public void UnlockWind() => Unlock(SkillElementType.Wind);

    private void Unlock(SkillElementType element)
    {
        FairyElementUnlockController controller = ResolveTarget();
        if (controller == null)
        {
            Debug.LogWarning($"[FairyTimelineElementUnlocker] Cannot unlock {element}: no FairyElementUnlockController target found.", this);
            return;
        }

        controller.UnlockElement(element);
    }

    private FairyElementUnlockController ResolveTarget()
    {
        if (target != null)
        {
            return target;
        }

        if (!useCurrentPlayerWhenTargetMissing || PlayerRegistry.Current == null)
        {
            return null;
        }

        FairyPlayerCompanionController fairy = ResolveCurrentPlayerFairy();
        if (fairy != null)
        {
            target = fairy.GetComponent<FairyElementUnlockController>();
            if (target == null && addUnlockControllerIfMissing)
            {
                target = fairy.gameObject.AddComponent<FairyElementUnlockController>();
            }

            return target;
        }

        Player player = PlayerRegistry.Current;
        if (!player.TryGetComponent(out target) && addUnlockControllerIfMissing)
        {
            target = player.gameObject.AddComponent<FairyElementUnlockController>();
        }

        return target;
    }

    private static FairyPlayerCompanionController ResolveCurrentPlayerFairy()
    {
        Player player = PlayerRegistry.Current;
        if (player == null)
        {
            return null;
        }

#if UNITY_2023_1_OR_NEWER
        FairyPlayerCompanionController[] companions =
            Object.FindObjectsByType<FairyPlayerCompanionController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
        FairyPlayerCompanionController[] companions =
            Object.FindObjectsOfType<FairyPlayerCompanionController>();
#endif

        for (int i = 0; i < companions.Length; i++)
        {
            FairyPlayerCompanionController companion = companions[i];
            if (companion != null && companion.ActivePlayer == player)
            {
                return companion;
            }
        }

#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<FairyPlayerCompanionController>();
#else
        return Object.FindObjectOfType<FairyPlayerCompanionController>();
#endif
    }
}
