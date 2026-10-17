using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TimelineEditorSkipShortcut
{
    [Shortcut(
        "Timeline/Stop First Configured Playing Timeline",
        KeyCode.F10,
        ShortcutModifiers.Action | ShortcutModifiers.Shift)]
    static void StopFirstConfiguredPlayingTimeline()
    {
        if (!EditorApplication.isPlaying)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        TimelineEditorSkipSequence sequence = null;
        int sequenceCount = 0;

        GameObject[] roots = activeScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            TimelineEditorSkipSequence[] sequences =
                roots[i].GetComponentsInChildren<TimelineEditorSkipSequence>(true);

            for (int j = 0; j < sequences.Length; j++)
            {
                if (!sequences[j].isActiveAndEnabled)
                {
                    continue;
                }

                sequence = sequences[j];
                sequenceCount++;
            }
        }

        if (sequenceCount != 1)
        {
            Debug.LogWarning(
                sequenceCount == 0
                    ? "Ctrl+Shift+F10 requires one active TimelineEditorSkipSequence in the active scene."
                    : "Ctrl+Shift+F10 found multiple active TimelineEditorSkipSequence components in the active scene.");
            return;
        }

        sequence.TryStopFirstPlaying();
    }
}
