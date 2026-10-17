using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public sealed class TimelineEditorSkipSequence : MonoBehaviour
{
    [Tooltip("PlayableDirectors checked in order by the editor-only skip shortcut.")]
    [SerializeField] private List<PlayableDirector> timelinesInOrder = new List<PlayableDirector>();

    public bool TryStopFirstPlaying()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            return false;
        }

        for (int i = 0; i < timelinesInOrder.Count; i++)
        {
            PlayableDirector director = timelinesInOrder[i];
            if (director == null
                || !director.isActiveAndEnabled
                || !director.gameObject.activeInHierarchy
                || director.state != PlayState.Playing)
            {
                continue;
            }

            director.Stop();
            return true;
        }
#endif
        return false;
    }
}
