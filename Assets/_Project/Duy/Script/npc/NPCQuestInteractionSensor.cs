using UnityEngine;

public sealed class NPCQuestInteractionSensor
{
    private readonly NPCQuestGiver owner;
    private bool isPlayerNearInteract;

    public NPCQuestInteractionSensor(NPCQuestGiver owner)
    {
        this.owner = owner;
    }

    public void SetupCollision()
    {
        Collider2D mainCollider = owner.GetComponent<Collider2D>();
        if (mainCollider == null)
        {
            return;
        }

        mainCollider.isTrigger = false;

        if (owner.GetComponents<Collider2D>().Length == 1)
        {
            BoxCollider2D triggerCollider = owner.gameObject.AddComponent<BoxCollider2D>();
            triggerCollider.isTrigger = true;
            triggerCollider.size = mainCollider is BoxCollider2D box ? box.size * 1.5f : new Vector2(2f, 2f);
        }

        Player player = PlayerRegistry.Current ?? Object.FindFirstObjectByType<Player>();
        if (player == null)
        {
            return;
        }

        foreach (Collider2D playerCollider in player.GetComponentsInChildren<Collider2D>())
        {
            Physics2D.IgnoreCollision(mainCollider, playerCollider, true);
        }
    }

    public void CheckInteraction(System.Action interactionAction)
    {
        if (isPlayerNearInteract && Input.GetKeyDown(KeyCode.E))
        {
            interactionAction?.Invoke();
        }
    }

    public void SetPlayerNear(Collider collider, bool isNear)
    {
        if (collider != null && collider.CompareTag("Player"))
        {
            isPlayerNearInteract = isNear;
        }
    }

    public void SetPlayerNear(Collider2D collider, bool isNear)
    {
        if (IsPlayer(collider))
        {
            isPlayerNearInteract = isNear;
        }
    }

    private bool IsPlayer(Collider2D collider)
    {
        return collider != null
            && (collider.CompareTag("Player") || collider.GetComponentInParent<Player>() != null);
    }
}
