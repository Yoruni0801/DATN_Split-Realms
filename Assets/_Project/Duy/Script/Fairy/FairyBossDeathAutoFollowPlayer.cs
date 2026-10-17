using UnityEngine;
using Bao.Bosses;
using Quoc.PuzzleQuest;

[DisallowMultipleComponent]
public sealed class FairyBossDeathAutoFollowPlayer : MonoBehaviour
{
    [SerializeField] private FairyPlayerCompanionController companionController;
    [SerializeField] private bool teleportOnDefeated = false;

    private void Awake()
    {
        if (companionController == null)
        {
            companionController = GetComponent<FairyPlayerCompanionController>();
        }
    }

    private void OnEnable()
    {
        PuzzleQuestController.OnPuzzleBossDefeated += HandleBossDefeated;
        LithosMonolithBoss.OnGlobalLithosBossDied += HandleBossDefeated;

        if (PuzzleQuestController.IsPuzzleBossDefeated)
        {
            ApplyFollowPlayer();
        }
    }

    private void OnDisable()
    {
        PuzzleQuestController.OnPuzzleBossDefeated -= HandleBossDefeated;
        LithosMonolithBoss.OnGlobalLithosBossDied -= HandleBossDefeated;
    }

    private void HandleBossDefeated()
    {
        ApplyFollowPlayer();
    }

    public void ApplyFollowPlayer()
    {
        FairyPlayerCompanionController target = companionController != null
            ? companionController
            : FindFirstObjectByType<FairyPlayerCompanionController>();

        if (target == null) return;

        target.AutoFindPlayer = true;

        if (teleportOnDefeated)
        {
            target.TeleportToPlayer();
        }
    }
}
