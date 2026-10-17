using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[AddComponentMenu("Timeline/Timeline Fast Forward To End")]
public sealed class TimelineFastForwardToEnd : MonoBehaviour
{
    [SerializeField] private PlayableDirector director;
    [SerializeField] private string timelineId;
    [SerializeField] private bool applyOnAwake = true;
    [SerializeField] private bool petrifyTargetsOnFinish = true;
    [SerializeField] private bool excludePlayer = true;
    [SerializeField] private bool includeInactiveTargets = true;
    [SerializeField] private bool markAsPlayedInStateManager = true;
    [SerializeField] private bool deactivateTimelineObjectAfter;

    [Header("Next Timeline Transition")]
    [SerializeField] private TimelinePlayController nextTimelineController;
    [SerializeField] private PlayableDirector nextDirector;
    [SerializeField] private UnityEvent onSkipFinished;

    private void Reset()
    {
        director = GetComponent<PlayableDirector>();
    }

    private void Awake()
    {
        if (director == null)
        {
            director = GetComponent<PlayableDirector>();
        }
    }

    private void Start()
    {
        if (applyOnAwake)
        {
            FastForwardToEnd();
        }
    }

    public void FastForwardToEnd()
    {
        ExecuteSkipToEnd();
    }

    private void ExecuteSkipToEnd()
    {
        if (director != null && director.playableAsset != null)
        {
            director.playOnAwake = false;

            if (!director.playableGraph.IsValid())
            {
                director.RebuildGraph();
            }

            // Explicit seek suppresses TimelineLoadSceneBehaviour and triggers deterministic seek repositioning
            TimelineSeekContext.BeginExplicitSeek(director);
            try
            {
                director.time = director.duration;
                director.Evaluate();
            }
            finally
            {
                TimelineSeekContext.EndExplicitSeek(director);
            }

            director.Stop();
        }

        if (petrifyTargetsOnFinish)
        {
            PetrifyAll();
        }

        Player player = PlayerRegistry.Current;
        if (player != null)
        {
            player.SetControlLocked(false);
        }

        if (markAsPlayedInStateManager)
        {
            TimelinePlayStateManager.MarkPlayed(GetSceneName(), GetTimelineId());
        }

        if (nextTimelineController != null)
        {
            nextTimelineController.PlayTimeline();
        }
        else if (nextDirector != null)
        {
            nextDirector.Play();
        }

        onSkipFinished?.Invoke();

        if (deactivateTimelineObjectAfter)
        {
            gameObject.SetActive(false);
        }
    }

    private void PetrifyAll()
    {
        PetrifyController[] targets = Object.FindObjectsByType<PetrifyController>(
            includeInactiveTargets ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < targets.Length; i++)
        {
            PetrifyController target = targets[i];
            if (target == null)
            {
                continue;
            }

            if (excludePlayer && (target.GetComponent<Player>() != null || target.GetComponentInParent<Player>() != null))
            {
                target.Unpetrify();
                continue;
            }

            target.Petrify();
        }
    }

    private string GetTimelineId()
    {
        if (!string.IsNullOrWhiteSpace(timelineId))
        {
            return timelineId.Trim();
        }

        return director != null
            ? $"{GetSceneName()}/{director.gameObject.name}"
            : $"{GetSceneName()}/{gameObject.name}";
    }

    private string GetSceneName()
    {
        return gameObject.scene.IsValid()
            ? gameObject.scene.name
            : SceneManager.GetActiveScene().name;
    }
}
