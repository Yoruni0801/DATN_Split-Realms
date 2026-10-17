using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class TimelineTrigger2D : MonoBehaviour
{
    public static Player CurrentPlayer => PlayerRegistry.Current;

    [SerializeField] private string timelineId;
    [SerializeField] private PlayableDirector timeline;
    [SerializeField] private GameObject timelineObject;
    [SerializeField] private bool preventPlayOnAwake = true;
    [SerializeField] private bool resetTimeOnPlay = true;
    [SerializeField] private bool stopBeforePlay = true;
    [SerializeField] private bool deactivateObjectWhenStopped;

    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private bool disableTriggerAfterPlay;
    [SerializeField] private bool prewarmOnStart;
    private string playerTag = "Player";

    private Player player;
    private Player ResolvedPlayer => player != null ? player : CurrentPlayer;

    [SerializeField] private bool lockPlayerWhilePlaying = true;

    [Tooltip("Fired the moment the player enters this trigger, including when it resumes a paused timeline.")]
    [SerializeField] private UnityEvent onPlayerEnteredTrigger;

    private Collider2D triggerCollider;
    private bool triggered;
    private bool hasLockedPlayer;
    private Player timelineLockedPlayer;

    public static void RegisterPlayer(Player player) => PlayerRegistry.Register(player);
    public static void UnregisterPlayer(Player player) => PlayerRegistry.Unregister(player);

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        triggerCollider.isTrigger = true;

        if (timeline == null)
        {
            timeline = GetComponent<PlayableDirector>();
        }

        if (timelineObject == null && timeline != null)
        {
            timelineObject = timeline.gameObject;
        }

        if (preventPlayOnAwake && timeline != null)
        {
            timeline.playOnAwake = false;
            timeline.Stop();
            timeline.time = 0;
        }
    }

    private void Start()
    {
        if (triggerOnce && !CanPlayTriggerTimeline())
        {
            triggered = true;
            if (disableTriggerAfterPlay && triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }
            if (deactivateObjectWhenStopped && timelineObject != null)
            {
                timelineObject.SetActive(false);
            }
            return;
        }

        if (prewarmOnStart && timeline != null)
        {
            bool wasActive = timelineObject != null && timelineObject.activeSelf;
            
            if (timelineObject != null && !wasActive)
            {
                timelineObject.SetActive(true);
            }

            timeline.RebuildGraph();
            timeline.time = 0;
            timeline.Evaluate();
            timeline.Stop();

            if (timelineObject != null && !wasActive)
            {
                timelineObject.SetActive(false);
            }
        }
    }

    private void OnDisable()
    {
        UnlockPlayerIfNeeded();
    }

    /// <summary>
    /// The director no longer reports back through its played and stopped callbacks, so the lock is released here
    /// once it is not playing any more. A director paused midway gives control back but keeps its object alive.
    /// </summary>
    private void Update()
    {
        if (!hasLockedPlayer)
        {
            return;
        }

        if (timeline != null && timeline.state == PlayState.Playing)
        {
            return;
        }

        bool pausedMidway = timeline != null && IsPausedMidway();
        UnlockPlayerIfNeeded();

        if (!pausedMidway)
        {
            MarkTriggerTimelinePlayed();
            if (deactivateObjectWhenStopped && timelineObject != null)
            {
                timelineObject.SetActive(false);
            }
        }
    }

    private bool IsTimelineStopped()
    {
        if (timeline == null)
        {
            return true;
        }

        return timeline.state != PlayState.Playing && timeline.state != PlayState.Paused;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered && triggerOnce)
        {
            return;
        }

        if (triggerOnce && !CanPlayTriggerTimeline())
        {
            triggered = true;
            if (disableTriggerAfterPlay && triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }
            return;
        }

        Player enteredPlayer = ResolvePlayer(other);
        if (enteredPlayer == null)
        {
            return;
        }

        player = enteredPlayer;
        RegisterPlayer(enteredPlayer);
        onPlayerEnteredTrigger?.Invoke();

        triggered = true;
        PlayTimeline();

        if (disableTriggerAfterPlay && triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }
    }

    public void PlayTimeline()
    {
        Player playerToRegister = ResolvedPlayer;
        if (playerToRegister != null)
        {
            PlayerRegistry.Register(playerToRegister);
        }

        if (timeline == null)
        {
            Debug.LogWarning("[TimelineTrigger2D] Missing PlayableDirector.", this);
            return;
        }

        if (timelineObject != null && !timelineObject.activeSelf)
        {
            timelineObject.SetActive(true);
        }

        // A director paused midway keeps its time, so entering the trigger again continues instead of restarting.
        if (IsPausedMidway())
        {
            // Resume keeps the stored pause time exactly, where a fresh Play call can re-evaluate and skip ahead.
            LockPlayerIfNeeded();
            timeline.Resume();
            return;
        }

        if (stopBeforePlay)
        {
            timeline.Stop();
        }

        if (resetTimeOnPlay)
        {
            timeline.time = 0;
        }

        LockPlayerIfNeeded();
        timeline.Play();
    }

    public void ResetTrigger()
    {
        triggered = false;

        if (triggerCollider != null)
        {
            triggerCollider.enabled = true;
        }
    }

    /// <summary>
    /// True when the director was paused partway through, so it should continue from its current time instead of
    /// being stopped and replayed from zero. A director sitting at the very end counts as finished, not paused.
    /// </summary>
    private bool IsPausedMidway()
    {
        if (timeline.state == PlayState.Playing || timeline.time <= 0d)
        {
            return false;
        }

        double duration = timeline.duration;
        if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0d)
        {
            return true;
        }

        return timeline.time < duration;
    }

    private Player ResolvePlayer(Collider2D other)
    {
        if (other == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag))
        {
            Player taggedParentPlayer = other.GetComponentInParent<Player>();
            return taggedParentPlayer != null && taggedParentPlayer.CompareTag(playerTag)
                ? taggedParentPlayer
                : null;
        }

        return other.GetComponentInParent<Player>();
    }

    private void LockPlayerIfNeeded()
    {
        Player targetPlayer = ResolvedPlayer;
        if (!lockPlayerWhilePlaying || targetPlayer == null)
        {
            return;
        }

        targetPlayer.CancelActiveSkillForTimeline();
        targetPlayer.SetControlLocked(true);
        timelineLockedPlayer = targetPlayer;
        hasLockedPlayer = true;
    }

    private void UnlockPlayerIfNeeded()
    {
        if (!hasLockedPlayer)
        {
            timelineLockedPlayer = null;
            return;
        }

        if (timelineLockedPlayer != null)
        {
            timelineLockedPlayer.SetControlLocked(false);
        }

        timelineLockedPlayer = null;
        hasLockedPlayer = false;
    }

    private string GetTimelineId()
    {
        if (!string.IsNullOrWhiteSpace(timelineId))
        {
            return timelineId.Trim();
        }

        if (timeline != null)
        {
            return $"{GetSceneName()}/{timeline.gameObject.name}";
        }

        return $"{GetSceneName()}/{gameObject.name}";
    }

    private string GetSceneName()
    {
        return gameObject.scene.IsValid()
            ? gameObject.scene.name
            : SceneManager.GetActiveScene().name;
    }

    private bool CanPlayTriggerTimeline()
    {
        return TimelinePlayStateManager.CanPlay(GetSceneName(), GetTimelineId());
    }

    private void MarkTriggerTimelinePlayed()
    {
        TimelinePlayStateManager.MarkPlayed(GetSceneName(), GetTimelineId());
    }
}
