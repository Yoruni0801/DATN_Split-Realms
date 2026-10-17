using System.Collections;
using UnityEngine;
using Tien.Minigames;

public sealed class NPCQuestDialogueDirector
{
    private readonly NPCQuestGiver owner;

    public NPCQuestDialogueDirector(NPCQuestGiver owner)
    {
        this.owner = owner;
    }

    public IEnumerator JustPanCameraToBridge(float duration)
    {
        SetPlayerInput(false);

        if (owner.bridgeVirtualCamera != null)
        {
            owner.bridgeVirtualCamera.SetActive(true);
            yield return new WaitForSeconds(duration);
            owner.bridgeVirtualCamera.SetActive(false);
        }

        ShowDialogue(
            "Ha ha! Lão nhìn lầm rồi, không ngờ một tay kiếm khách như cậu cầm búa đóng đinh cũng ra dáng thợ mộc đấy chứ! Cây cầu lại vững như bàn thạch rồi.",
            6f,
            owner.npcAvatar);
        yield return new WaitForSeconds(6f);

        ShowDialogue("Cũng thường thôi. Giờ thì tránh ra một chút để tôi qua cầu nào.", 4f, owner.playerAvatar);
        yield return new WaitForSeconds(4f);

        ShowDialogue(
            "Coi như chúng ta hòa nhau. Giờ thì lão đi đường của lão, cậu đi đường của cậu. Chúc may mắn trên chuyến hành trình nhé, lữ khách!",
            5f,
            owner.npcAvatar);
        yield return new WaitForSeconds(5.5f);

        SetPlayerInput(true);
        owner.currentState = NPCQuestGiver.NPCState.QuestCompleted;

        if (owner.exclamationMark != null)
        {
            owner.exclamationMark.SetActive(false);
        }
    }

    public IEnumerator AutoBridgeIntroRoutine()
    {
        SetPlayerInput(false);

        ShowDialogue(
            "Cây cầu phía trước bị hỏng rồi. Cậu lại xem giúp lão một chút nhé.",
            3.5f,
            owner.npcAvatar);
        yield return new WaitForSeconds(3.5f);

        if (owner.bridgeVirtualCamera != null)
        {
            owner.bridgeVirtualCamera.SetActive(true);
            yield return new WaitForSeconds(owner.cameraPanDuration);
            owner.bridgeVirtualCamera.SetActive(false);
        }
        else
        {
            yield return new WaitForSeconds(1f);
        }

        ShowDialogue(
            "Chỗ đó cần được sửa lại trước khi qua được. Mau chuẩn bị nhé.",
            3.5f,
            owner.npcAvatar);
        yield return new WaitForSeconds(3.5f);

        SetPlayerInput(true);

        if (owner.puzzleManager != null && !owner.puzzleManager.isCompleted)
        {
            owner.puzzleManager.gameObject.SetActive(true);
            owner.puzzleManager.OpenPuzzle();
        }
    }

    private void SetPlayerInput(bool active)
    {
        if (owner.playerTransform == null)
        {
            return;
        }

        Player player = owner.playerTransform.GetComponent<Player>();
        if (player != null)
        {
            player.SetInputActive(active);
        }
    }

    private void ShowDialogue(string text, float duration, Sprite avatar)
    {
        if (QuestUIManager.Instance != null)
        {
            QuestUIManager.Instance.ShowDialogue(text, duration, null, avatar);
        }
    }
}
