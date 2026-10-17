using UnityEngine;

public class DialogueTrigger2D : MonoBehaviour
{
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private GameObject interactUI;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    private bool playerInRange;

    private void Start()
    {
        SetInteractUI(false);
    }

    private void Update()
    {
        bool canInteract = playerInRange && !IsDialoguePlaying();
        SetInteractUI(canInteract);

        if (canInteract && Input.GetKeyDown(interactKey))
        {
            StartDialogue();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInRange = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInRange = false;
        SetInteractUI(false);
    }

    private void OnDisable()
    {
        SetInteractUI(false);
    }

    private void StartDialogue()
    {
        if (dialogueManager == null)
        {
            Debug.LogError("Chua keo DialogueManager vao DialogueTrigger2D");
            return;
        }

        SetInteractUI(false);
        dialogueManager.StartDialogue();
    }

    private bool IsDialoguePlaying()
    {
        return dialogueManager != null && dialogueManager.IsDialoguePlaying;
    }

    private void SetInteractUI(bool active)
    {
        if (interactUI != null && interactUI.activeSelf != active)
        {
            interactUI.SetActive(active);
        }
    }
}
