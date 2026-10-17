using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Duy/Dialog Conversation", fileName = "DialogConversation")]
public class DialogConversation : ScriptableObject
{
    [SerializeField] private List<DialogLine> lines = new List<DialogLine>();

    private int currentIndex = -1;

    public bool HasCurrentLine => currentIndex >= 0 && currentIndex < lines.Count;
    public bool HasChoice => HasCurrentLine && CurrentLine.choice.IsValid;

    public DialogSpeakerSide CurrentSpeakerSide => HasCurrentLine ? CurrentLine.speakerSide : DialogSpeakerSide.Left;
    public string CurrentText => HasCurrentLine ? CurrentLine.text : string.Empty;
    public DialogChoice CurrentChoice => HasChoice ? CurrentLine.choice : DialogChoice.Empty;

    public bool StartConversation()
    {
        if (lines == null || lines.Count == 0)
        {
            currentIndex = -1;
            return false;
        }

        currentIndex = 0;
        return true;
    }

    public bool Advance()
    {
        if (lines == null || lines.Count == 0)
        {
            return false;
        }

        int nextIndex = currentIndex + 1;
        if (nextIndex >= lines.Count)
        {
            currentIndex = -1;
            return false;
        }

        currentIndex = nextIndex;
        return true;
    }

    public bool SelectChoice()
    {
        if (!HasChoice)
        {
            return false;
        }

        int nextIndex = CurrentLine.choice.nextLineIndex;
        if (nextIndex < 0 || nextIndex >= lines.Count)
        {
            currentIndex = -1;
            return false;
        }

        currentIndex = nextIndex;
        return true;
    }

    private DialogLine CurrentLine => HasCurrentLine ? lines[currentIndex] : default;
}

[Serializable]
public struct DialogLine
{
    public DialogSpeakerSide speakerSide;

    [TextArea(2, 4)]
    public string text;

    public DialogChoice choice;
}

[Serializable]
public struct DialogChoice
{
    public static DialogChoice Empty => new DialogChoice { label = string.Empty, nextLineIndex = -1 };

    public string label;
    public int nextLineIndex;

    public bool IsValid => !string.IsNullOrEmpty(label);
}

public enum DialogSpeakerSide
{
    Left = 0,
    Right = 1
}
