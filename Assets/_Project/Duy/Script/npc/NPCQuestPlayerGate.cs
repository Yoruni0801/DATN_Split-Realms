using UnityEngine;
using Tien.Minigames;

public sealed class NPCQuestPlayerGate
{
    private readonly NPCQuestGiver owner;
    private bool isPlayerWaitingForNPC;

    public NPCQuestPlayerGate(NPCQuestGiver owner)
    {
        this.owner = owner;
    }

    public bool Tick(Transform currentTarget, NPCQuestMovementController movement)
    {
        if (currentTarget == null || owner.playerTransform == null)
        {
            return false;
        }

        float distanceToPlayer = Vector2.Distance(owner.transform.position, owner.playerTransform.position);
        if (distanceToPlayer > owner.maxPlayerDistance)
        {
            float playerToTarget = Vector2.Distance(owner.playerTransform.position, currentTarget.position);
            float npcToTarget = Vector2.Distance(owner.transform.position, currentTarget.position);

            if (playerToTarget < npcToTarget)
            {
                AskPlayerToWaitForNPC();
            }
            else
            {
                movement.StopHorizontal();
                return true;
            }
        }

        if (!isPlayerWaitingForNPC)
        {
            return false;
        }

        if (distanceToPlayer <= 4f)
        {
            isPlayerWaitingForNPC = false;
            SetPlayerInput(true);
            return false;
        }

        movement.MoveTowardsTarget(owner.playerTransform, 3f, owner.currentState, false);
        return true;
    }

    private void AskPlayerToWaitForNPC()
    {
        if (isPlayerWaitingForNPC)
        {
            return;
        }

        isPlayerWaitingForNPC = true;
        SetPlayerInput(false);

        if (QuestUIManager.Instance != null)
        {
            QuestUIManager.Instance.ShowDialogue(
                "Cái lưng già của tôi... Cậu thanh niên đi chậm lại đợi lão với!",
                3.5f,
                null,
                owner.npcAvatar);
        }
    }

    private void SetPlayerInput(bool active)
    {
        Player player = owner.playerTransform != null ? owner.playerTransform.GetComponent<Player>() : null;
        if (player != null)
        {
            player.SetInputActive(active);
        }
    }
}
