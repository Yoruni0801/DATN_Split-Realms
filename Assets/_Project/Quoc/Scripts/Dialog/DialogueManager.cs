using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    [System.Serializable]
    public class DialogueLine
    {
        public string speakerName;

        [TextArea(2, 5)]
        public string dialogueText;

        public Sprite portrait;
    }

    [Header("UI")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private Button nextButton;

    [Header("Dialogue")]
    [SerializeField] private List<DialogueLine> dialogueLines;

    private int currentLineIndex;
    private bool isDialoguePlaying;

    public bool IsDialoguePlaying => isDialoguePlaying;

    private void Start()
    {
        dialoguePanel.SetActive(false);

        if (nextButton != null)
        {
            nextButton.onClick.AddListener(NextDialogue);
        }
    }

    private void Update()
{
    if (!isDialoguePlaying)
        return;

    if (Input.GetKeyDown(KeyCode.Return) ||
        Input.GetKeyDown(KeyCode.KeypadEnter))
    {
        NextDialogue();
    }
}

    public void StartDialogue()
{
    Debug.Log("StartDialogue đã được gọi");

    if (dialoguePanel == null)
    {
        Debug.LogError("Chưa kéo Dialogue Panel vào DialogueManager");
        return;
    }

    if (dialogueLines == null || dialogueLines.Count == 0)
    {
        Debug.LogError("Chưa thêm câu thoại trong Dialogue Lines");
        return;
    }

    currentLineIndex = 0;
    isDialoguePlaying = true;

    dialoguePanel.SetActive(true);
    ShowCurrentLine();
}

    public void NextDialogue()
    {
        if (!isDialoguePlaying)
            return;

        currentLineIndex++;

        if (currentLineIndex >= dialogueLines.Count)
        {
            EndDialogue();
            return;
        }

        ShowCurrentLine();
    }

    private void ShowCurrentLine()
    {
        DialogueLine currentLine = dialogueLines[currentLineIndex];

        speakerNameText.text = currentLine.speakerName;
        dialogueText.text = currentLine.dialogueText;

        if (portraitImage != null)
        {
            if (currentLine.portrait != null)
            {
                portraitImage.gameObject.SetActive(true);
                portraitImage.sprite = currentLine.portrait;
            }
            else
            {
                portraitImage.gameObject.SetActive(false);
            }
        }
    }

    private void EndDialogue()
    {
        isDialoguePlaying = false;
        dialoguePanel.SetActive(false);
    }
}
