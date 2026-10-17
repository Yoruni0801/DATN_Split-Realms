using UnityEngine;

public sealed class NPCQuestStateMachine
{
    private readonly NPCQuestGiver owner;
    private readonly NPCQuestMovementController movement;
    private readonly NPCQuestDialogueDirector dialogue;
    private bool bridgeIntroStarted;

    public NPCQuestStateMachine(
        NPCQuestGiver owner,
        NPCQuestMovementController movement,
        NPCQuestDialogueDirector dialogue)
    {
        this.owner = owner;
        this.movement = movement;
        this.dialogue = dialogue;
    }

    public void Tick()
    {
        TickCurrentState();
        movement.StopWhenNotMovingState(owner.currentState);
    }

    public void UpdateQuestUI()
    {
        // Bridge-only NPC no longer tracks wood quest progress.
    }

    private void TickCurrentState()
    {
        switch (owner.currentState)
        {
            case NPCQuestGiver.NPCState.Idle_WaitingForTrigger:
                StartBridgeIntroIfPlayerInRange();
                break;

            case NPCQuestGiver.NPCState.QuestCompleted:
                if (owner.exclamationMark != null)
                {
                    owner.exclamationMark.SetActive(false);
                }
                break;
        }
    }

    private void StartBridgeIntroIfPlayerInRange()
    {
        if (bridgeIntroStarted || owner.playerTransform == null)
        {
            return;
        }

        float distance = Vector2.Distance(owner.transform.position, owner.playerTransform.position);
        if (distance > owner.activationRadius)
        {
            return;
        }

        bridgeIntroStarted = true;
        owner.currentState = NPCQuestGiver.NPCState.Idle_ReadyToRepair;

        if (owner.exclamationMark != null)
        {
            owner.exclamationMark.SetActive(false);
        }

        owner.StartCoroutine(dialogue.AutoBridgeIntroRoutine());
    }
}
