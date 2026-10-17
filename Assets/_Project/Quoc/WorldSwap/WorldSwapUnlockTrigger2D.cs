using UnityEngine;
using UnityEngine.Playables;

[RequireComponent(typeof(Collider2D))]
public class WorldSwapUnlockTrigger2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorldSwapManager worldSwapManager;
    [SerializeField] private PlayableDirector timeline;
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Collider2D playerCollider;

    [Header("Trigger Options")]
    [SerializeField] private bool unlockOnEnter = true;
    [SerializeField] private bool unlockWhenTimelineFinished;
    [SerializeField] private bool lockOnExit;
    [SerializeField] private bool triggerOnce = true;

    private bool triggered;

    private void Reset()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        timeline = GetComponent<PlayableDirector>();
    }

    private void OnEnable()
    {
        if (timeline != null)
        {
            timeline.stopped += HandleTimelineStopped;
        }
    }

    private void OnDisable()
    {
        if (timeline != null)
        {
            timeline.stopped -= HandleTimelineStopped;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggerOnce && triggered)
        {
            return;
        }

        if (!IsPlayer(other))
        {
            return;
        }

        triggered = true;

        if (unlockOnEnter)
        {
            UnlockSwap();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!lockOnExit || !IsPlayer(other))
        {
            return;
        }

        worldSwapManager?.LockSwap();
    }

    private void HandleTimelineStopped(PlayableDirector director)
    {
        if (!unlockWhenTimelineFinished || director == null || !HasTimelineFinished(director))
        {
            return;
        }

        UnlockSwap();
    }

    public void UnlockSwap()
    {
        worldSwapManager?.UnlockSwap();
    }

    private static bool HasTimelineFinished(PlayableDirector director)
    {
        double duration = director.duration;
        return double.IsNaN(duration)
            || double.IsInfinity(duration)
            || duration <= 0d
            || director.time >= duration - 0.001d;
    }

    private bool IsPlayer(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        if (playerCollider != null)
        {
            return other == playerCollider;
        }

        if (playerRoot != null)
        {
            return other.transform == playerRoot || other.transform.IsChildOf(playerRoot);
        }

        Player p = other.GetComponent<Player>() ?? other.GetComponentInParent<Player>();
        return p != null;
    }
}
