using UnityEngine;
using UnityEngine.Playables;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayableDirector))]
public sealed class DeactivateTimelineOnStopped : MonoBehaviour
{
    [SerializeField] private PlayableDirector timeline;
    [SerializeField] private GameObject objectToDeactivate;
    [SerializeField] private bool lockPlayerMovement = true;
    [SerializeField] private Player playerToLock;

    private bool playerLocked;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (timeline == null)
        {
            return;
        }

        timeline.played += HandleTimelinePlayed;
        timeline.stopped += HandleTimelineStopped;

        if (timeline.state == PlayState.Playing)
        {
            LockPlayerMovement();
        }
    }

    private void OnDisable()
    {
        if (timeline != null)
        {
            timeline.played -= HandleTimelinePlayed;
            timeline.stopped -= HandleTimelineStopped;
        }

        UnlockPlayerMovement();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (timeline == null)
        {
            timeline = GetComponent<PlayableDirector>();
        }

        if (objectToDeactivate == null)
        {
            objectToDeactivate = gameObject;
        }
    }

    private void HandleTimelineStopped(PlayableDirector stoppedTimeline)
    {
        if (stoppedTimeline != timeline)
        {
            return;
        }

        UnlockPlayerMovement();

        if (objectToDeactivate != null)
        {
            objectToDeactivate.SetActive(false);
        }
    }

    private void HandleTimelinePlayed(PlayableDirector playedTimeline)
    {
        if (playedTimeline == timeline)
        {
            LockPlayerMovement();
        }
    }

    private void LockPlayerMovement()
    {
        if (!lockPlayerMovement)
        {
            return;
        }

        ResolvePlayer();
        if (playerToLock == null)
        {
            return;
        }

        playerToLock.SetInputActive(false);
        playerLocked = true;
    }

    private void UnlockPlayerMovement()
    {
        if (!playerLocked || playerToLock == null)
        {
            return;
        }

        playerToLock.SetInputActive(true);
        playerLocked = false;
    }

    private void ResolvePlayer()
    {
        if (playerToLock == null)
        {
            playerToLock = FindAnyObjectByType<Player>();
        }
    }
}
