using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DialogTriggerZone : MonoBehaviour
{
    [SerializeField] private Dialog dialog;
    [SerializeField] private DialogConversation conversation;
    [SerializeField] private Player targetPlayer;

    private bool playerInside;
    private Player cachedPlayer;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void Awake()
    {
        if (dialog == null)
        {
            dialog = FindFirstObjectByType<Dialog>();
        }

        if (conversation == null)
        {
            conversation = GetComponentInParent<DialogConversation>();
        }

        if (targetPlayer == null)
        {
            cachedPlayer = PlayerRegistry.Current ?? FindFirstObjectByType<Player>();
        }
        else
        {
            cachedPlayer = targetPlayer;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Player player = other.GetComponentInParent<Player>();
        if (player == null)
        {
            return;
        }

        if (cachedPlayer != null && player != cachedPlayer)
        {
            return;
        }

        playerInside = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<Player>() == null)
        {
            return;
        }

        playerInside = false;
    }

    private void Update()
    {
        if (!playerInside || dialog == null)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (conversation != null)
            {
                dialog.SetConversation(conversation);
            }

            dialog.Enable();
        }
    }
}
