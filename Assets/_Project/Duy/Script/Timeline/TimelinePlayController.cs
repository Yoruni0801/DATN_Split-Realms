using System.Collections;
using System.Collections.Generic;
using CutsceneEngine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

public sealed class TimelineSceneResumeState
{
    public readonly string SceneName;
    public readonly string TimelineId;
    public readonly double ResumeTime;
    public readonly PlayableDirector SourceDirector;

    public TimelineSceneResumeState(
        string sceneName,
        string timelineId,
        double resumeTime,
        PlayableDirector sourceDirector)
    {
        SceneName = sceneName;
        TimelineId = timelineId;
        ResumeTime = resumeTime;
        SourceDirector = sourceDirector;
    }
}

[DisallowMultipleComponent]
public class TimelinePlayController : MonoBehaviour
{
    [SerializeField] private string timelineId;
    [SerializeField] private string selectedCheckpointId;
    [SerializeField] private PlayableDirector timeline;
    [SerializeField] private GameObject timelineObject;
    [SerializeField] private KeyCode fastForwardKey = KeyCode.RightArrow;
    [SerializeField] private KeyCode alternateFastForwardKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode skipKey = KeyCode.Space;

    [SerializeField] private bool playOnStart;
    [SerializeField] private bool resetTimeOnPlay = true;
    [SerializeField] private bool deactivateObjectWhenStopped;
    [SerializeField] private bool playWhenPlayerEnterTrigger;
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private bool lockPlayerWhilePlaying;
    [SerializeField] private bool resumeAfterReturningFromLoadedScene = true;
    [SerializeField] private bool enableArrowKeyFastForward = true;
    [SerializeField] private bool lockPlayerInputWhileTimelinePlaying = true;
    [SerializeField] private bool enableSkipKey = true;
    [SerializeField] private bool resumePlaybackAfterSkip = true;
    [SerializeField] private bool enableBlackTransitionOnSkip = true;

    [SerializeField, Min(0f)] private float sceneReturnResumeOffsetSeconds = 0.05f;
    [SerializeField, Min(1f)] private float fastForwardSpeed = 3f;
    [SerializeField, Min(0f)] private float skipTransitionDuration = 0.25f;

    private bool isSeekingWithTransition;
    private GameObject activeSkipTransitionCanvas;

    private Player player;
    private bool triggered;
    private bool resettingForPlay;
    private bool forcePlaying;
    private bool hasStartedPlaying;
    private bool playerLockedByController;
    private Player lockedPlayer;
    private bool timelineSpeedOverridden;
    private double previousTimelineSpeed = 1d;

    private Player CurrentPlayer => player != null ? player : TimelineTrigger2D.CurrentPlayer;

    public static bool TryCaptureSceneReturnResume(Scene scene, out TimelineSceneResumeState resumeState)
    {
        return TryCaptureSceneReturnResume(scene, null, out resumeState);
    }

    public static bool TryCaptureSceneReturnResume(
        Scene scene,
        PlayableDirector sourceDirector,
        out TimelineSceneResumeState resumeState)
    {
        resumeState = null;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return false;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null)
            {
                continue;
            }

            TimelinePlayController[] controllers = roots[i].GetComponentsInChildren<TimelinePlayController>(true);
            for (int j = 0; j < controllers.Length; j++)
            {
                TimelinePlayController controller = controllers[j];
                if (controller == null
                    || (sourceDirector != null && controller.timeline != sourceDirector))
                {
                    continue;
                }

                if (controller.TryCreateSceneReturnResumeState(out resumeState))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool ResumeFromSceneReturn(TimelineSceneResumeState resumeState)
    {
        if (resumeState == null || string.IsNullOrWhiteSpace(resumeState.SceneName))
        {
            return false;
        }

        Scene scene = SceneManager.GetSceneByName(resumeState.SceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return false;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null)
            {
                continue;
            }

            TimelinePlayController[] controllers = roots[i].GetComponentsInChildren<TimelinePlayController>(true);
            for (int j = 0; j < controllers.Length; j++)
            {
                TimelinePlayController controller = controllers[j];
                if (controller != null && controller.MatchesResumeState(resumeState))
                {
                    controller.ResumeTimelineFromSceneReturn(resumeState);
                    return true;
                }
            }
        }

        return false;
    }


    private void Awake()
    {
        if (timeline == null)
        {
            timeline = GetComponent<PlayableDirector>();
        }

        if (timelineObject == null && timeline != null)
        {
            timelineObject = timeline.gameObject;
        }

    }

    private void OnEnable()
    {
        if (timeline != null)
        {
            timeline.played += HandleTimelinePlayed;
            timeline.stopped += HandleTimelineStopped;
        }
    }

    private void Start()
    {
        if (triggerOnce && !TimelinePlayStateManager.CanPlay(GetSceneName(), GetTimelineId()))
        {
            triggered = true;
            if (deactivateObjectWhenStopped && timelineObject != null)
            {
                timelineObject.SetActive(false);
            }
            return;
        }

        if (playOnStart)
        {
            PlayTimeline();
        }
    }

    private void OnDisable()
    {
        if (hasStartedPlaying && !resettingForPlay && !TimelineSceneTravelManager.IsTransitionInProgress)
        {
            double resumeTime = GetLastCheckpointTimeBeforeCurrent();
            TimelinePlayStateManager.MarkInterrupted(GetSceneName(), GetTimelineId(), resumeTime);
        }

        if (timeline != null)
        {
            timeline.played -= HandleTimelinePlayed;
            timeline.stopped -= HandleTimelineStopped;
        }

        CleanupSkipTransitionCanvas();
        isSeekingWithTransition = false;
        RestoreTimelineSpeed();
        UnlockPlayer();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!playWhenPlayerEnterTrigger || (triggered && triggerOnce))
        {
            return;
        }

        Player triggeringPlayer = other.GetComponentInParent<Player>();
        if (triggeringPlayer == null)
        {
            return;
        }

        player = triggeringPlayer;

        if (!CanPlayTimeline())
        {
            return;
        }

        triggered = true;
        PlayTimelineInternal();
    }

    private void Update()
    {
        KeepPlayerLockedWhileTimelinePlaying();
        HandleKeyboardFastForward();
        HandleKeyboardSkip();
    }

    public void PlayTimeline()
    {
        if (!CanPlayTimeline())
        {
            return;
        }

        PlayTimelineInternal();
    }

    public void ForcePlayTimeline()
    {
        forcePlaying = true;
        PlayTimelineInternal();
    }

    private void PlayTimelineInternal()
    {
        if (timeline == null)
        {
            forcePlaying = false;
            return;
        }

        if (timelineObject != null && !timelineObject.activeSelf)
        {
            timelineObject.SetActive(true);
        }

        if (resetTimeOnPlay)
        {
            RestoreTimelineSpeed();
            resettingForPlay = true;
            timeline.Stop();
            resettingForPlay = false;
            if (TimelinePlayStateManager.TryTakeInterruptedTime(
                    GetSceneName(), GetTimelineId(), out double resumeTime))
            {
                timeline.time = resumeTime;
            }
            else
            {
                timeline.time = 0;
            }
        }
        else
        {
            if (TimelinePlayStateManager.TryTakeInterruptedTime(
                    GetSceneName(), GetTimelineId(), out double resumeTime))
            {
                timeline.time = resumeTime;
            }
        }

        EnsureTrackBindings();
        hasStartedPlaying = true;
        timeline.Play();
        forcePlaying = false;
    }

    private void EnsureTrackBindings()
    {
        if (timeline == null)
        {
            return;
        }

        TimelineAsset timelineAsset = timeline.playableAsset as TimelineAsset;
        if (timelineAsset == null)
        {
            return;
        }

        Unity.Cinemachine.CinemachineCamera sceneCamera = null;
        foreach (TrackAsset track in timelineAsset.GetOutputTracks())
        {
            if (track is TimelineCameraFollowTrack)
            {
                Object currentBinding = timeline.GetGenericBinding(track);
                if (currentBinding == null)
                {
                    if (sceneCamera == null)
                    {
#if UNITY_2023_1_OR_NEWER
                        sceneCamera = Object.FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>();
#else
                        sceneCamera = Object.FindObjectOfType<Unity.Cinemachine.CinemachineCamera>();
#endif
                    }

                    if (sceneCamera != null)
                    {
                        timeline.SetGenericBinding(track, sceneCamera);
                    }
                }
            }
        }
    }

    public void StopTimeline()
    {
        if (timeline == null)
        {
            return;
        }

        MarkTimelinePlayed();
        hasStartedPlaying = false;
        RestoreTimelineSpeed();
        timeline.Stop();
        UnlockPlayer();

        if (deactivateObjectWhenStopped && timelineObject != null)
        {
            timelineObject.SetActive(false);
        }
    }

    public void SetFastForwardActive(bool active)
    {
        if (active && CanFastForwardTimeline())
        {
            ApplyFastForwardSpeed();
            return;
        }

        RestoreTimelineSpeed();
    }

    public void SkipToSelectedCheckpoint()
    {
        SkipToCheckpoint(selectedCheckpointId);
    }

    public void SkipToCheckpoint(string checkpointId)
    {
        if (!TryFindCheckpoint(checkpointId, out TimelineCheckpoint checkpoint))
        {
            Debug.LogWarning(
                $"{nameof(TimelinePlayController)} could not find checkpoint '{checkpointId}' on '{GetTimelineId()}'.",
                this);
            return;
        }

        SeekToCheckpoint(checkpoint);
    }

    public void SkipToNextCheckpoint()
    {
        List<TimelineCheckpoint> checkpoints = GetCheckpoints();
        double currentTime = timeline != null ? timeline.time : 0d;

        for (int i = 0; i < checkpoints.Count; i++)
        {
            if (checkpoints[i].Time > currentTime + 0.05d)
            {
                SeekToCheckpoint(checkpoints[i]);
                return;
            }
        }

        // If no more checkpoints exist, skip straight to the end of the timeline
        SkipToEnd();
    }

    public void SkipToEnd()
    {
        if (timeline == null || isSeekingWithTransition)
        {
            return;
        }

        if (enableBlackTransitionOnSkip && skipTransitionDuration > 0f && Application.isPlaying)
        {
            StartCoroutine(SkipToEndWithTransitionRoutine());
            return;
        }

        PerformSkipToEnd();
    }

    private void PerformSkipToEnd()
    {
        if (timeline == null)
        {
            return;
        }

        RestoreTimelineSpeed();
        timeline.Pause();

        TimelineSeekContext.BeginExplicitSeek(timeline);
        try
        {
            timeline.time = timeline.duration;
            timeline.Evaluate();
        }
        finally
        {
            TimelineSeekContext.EndExplicitSeek(timeline);
        }

        triggered = true;
        StopTimeline();
    }

    private IEnumerator SkipToEndWithTransitionRoutine()
    {
        isSeekingWithTransition = true;
        CleanupSkipTransitionCanvas();

        GameObject canvasObj = new GameObject("TimelineSkipTransitionCanvas");
        activeSkipTransitionCanvas = canvasObj;
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;

        CanvasGroup canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 0f;

        GameObject imageObj = new GameObject("BlackOverlay");
        imageObj.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Image image = imageObj.AddComponent<UnityEngine.UI.Image>();
        image.color = Color.black;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;

        float halfDuration = skipTransitionDuration * 0.5f;
        float elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Clamp01(elapsed / halfDuration);
            }
            yield return null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        PerformSkipToEnd();
        yield return null;

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Clamp01(1f - (elapsed / halfDuration));
            }
            yield return null;
        }

        CleanupSkipTransitionCanvas();
        isSeekingWithTransition = false;
    }

    public string[] GetCheckpointIds()
    {
        List<TimelineCheckpoint> checkpoints = GetCheckpoints();
        string[] checkpointIds = new string[checkpoints.Count];
        for (int i = 0; i < checkpoints.Count; i++)
        {
            checkpointIds[i] = checkpoints[i].Id;
        }

        return checkpointIds;
    }

    public void PauseTimeline()
    {
        if (timeline != null)
        {
            RestoreTimelineSpeed();
            timeline.Pause();

            // A paused timeline should give control back, otherwise the Update lock keeps the player frozen.
            UnlockPlayer();
        }
    }

    public void ResumeTimeline()
    {
        if (timeline != null)
        {
            timeline.Resume();
            LockPlayerIfNeeded();
        }
    }

    public void ResetTrigger()
    {
        triggered = false;
    }

    public void LockPlayerInput()
    {
        LockPlayerIfNeeded();
    }

    public void UnlockPlayerInput()
    {
        UnlockPlayer();
    }

    public string TimelineId => GetTimelineId();

    private void HandleTimelinePlayed(PlayableDirector director)
    {
        LockPlayerIfNeeded();

        // onTimelinePlayed?.Invoke();
    }

    private void HandleTimelineStopped(PlayableDirector director)
    {
        if (resettingForPlay)
        {
            return;
        }

        if (TimelineSceneTravelManager.IsTransitionInProgress)
        {
            RestoreTimelineSpeed();
            return;
        }

        MarkTimelinePlayed();
        hasStartedPlaying = false;
        RestoreTimelineSpeed();
        UnlockPlayer();

        if (deactivateObjectWhenStopped && timelineObject != null)
        {
            timelineObject.SetActive(false);
        }

        // onTimelineStopped?.Invoke();
    }

    private void LockPlayerIfNeeded()
    {
        Player targetPlayer = CurrentPlayer;
        if (!ShouldLockPlayerInput() || targetPlayer == null)
        {
            return;
        }

        targetPlayer.CancelActiveSkillForTimeline();
        targetPlayer.SetControlLocked(true);
        lockedPlayer = targetPlayer;
        playerLockedByController = true;
    }

    private void KeepPlayerLockedWhileTimelinePlaying()
    {
        if (timeline == null || timeline.state != PlayState.Playing)
        {
            return;
        }

        LockPlayerIfNeeded();
    }

    private void UnlockPlayer()
    {
        if (playerLockedByController && lockedPlayer != null)
        {
            lockedPlayer.SetControlLocked(false);
        }

        lockedPlayer = null;
        playerLockedByController = false;
    }

    private bool ShouldLockPlayerInput()
    {
        return lockPlayerWhilePlaying || lockPlayerInputWhileTimelinePlaying;
    }

    private void HandleKeyboardFastForward()
    {
        if (!enableArrowKeyFastForward)
        {
            RestoreTimelineSpeed();
            return;
        }

        bool keyHeld = Input.GetKey(fastForwardKey) || Input.GetKey(alternateFastForwardKey);
        SetFastForwardActive(keyHeld);
    }

    private void HandleKeyboardSkip()
    {
        if (!enableSkipKey || timeline == null || timeline.state != PlayState.Playing)
        {
            return;
        }

        if (SubtitleSkipInput.HasActiveSubtitle(timeline))
        {
            return;
        }

        bool isSkipPressed = Input.GetKeyDown(skipKey) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#if ENABLE_INPUT_SYSTEM && INPUT_SYSTEM
        if (!isSkipPressed && Keyboard.current != null)
        {
            isSkipPressed = Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame;
        }
#endif

        if (isSkipPressed)
        {
            SkipToNextCheckpoint();
        }
    }

    private bool CanFastForwardTimeline()
    {
        return timeline != null
            && timeline.state == PlayState.Playing
            && timeline.playableGraph.IsValid();
    }

    private void ApplyFastForwardSpeed()
    {
        if (!timelineSpeedOverridden)
        {
            previousTimelineSpeed = GetTimelineSpeed();
            timelineSpeedOverridden = true;
        }

        SetTimelineSpeed(System.Math.Max(1d, fastForwardSpeed));
    }

    private void RestoreTimelineSpeed()
    {
        if (!timelineSpeedOverridden)
        {
            return;
        }

        SetTimelineSpeed(previousTimelineSpeed);
        timelineSpeedOverridden = false;
    }

    private double GetTimelineSpeed()
    {
        if (timeline == null || !timeline.playableGraph.IsValid())
        {
            return 1d;
        }

        int rootCount = timeline.playableGraph.GetRootPlayableCount();
        for (int i = 0; i < rootCount; i++)
        {
            Playable rootPlayable = timeline.playableGraph.GetRootPlayable(i);
            if (rootPlayable.IsValid())
            {
                return rootPlayable.GetSpeed();
            }
        }

        return 1d;
    }

    private void SetTimelineSpeed(double speed)
    {
        if (timeline == null || !timeline.playableGraph.IsValid())
        {
            return;
        }

        int rootCount = timeline.playableGraph.GetRootPlayableCount();
        for (int i = 0; i < rootCount; i++)
        {
            Playable rootPlayable = timeline.playableGraph.GetRootPlayable(i);
            if (rootPlayable.IsValid())
            {
                rootPlayable.SetSpeed(speed);
            }
        }
    }

    private bool TryCreateSceneReturnResumeState(out TimelineSceneResumeState resumeState)
    {
        resumeState = null;
        if (!resumeAfterReturningFromLoadedScene
            || timeline == null
            || (timeline.state != PlayState.Playing && !hasStartedPlaying))
        {
            return false;
        }

        double resumeTime = GetSceneReturnResumeTime();
        resumeState = new TimelineSceneResumeState(
            GetSceneName(),
            GetTimelineId(),
            resumeTime,
            timeline);
        return true;
    }

    private bool MatchesResumeState(TimelineSceneResumeState resumeState)
    {
        return resumeState != null
            && string.Equals(GetSceneName(), resumeState.SceneName, System.StringComparison.Ordinal)
            && string.Equals(GetTimelineId(), resumeState.TimelineId, System.StringComparison.Ordinal)
            && (resumeState.SourceDirector == null || timeline == resumeState.SourceDirector);
    }

    private void ResumeTimelineFromSceneReturn(TimelineSceneResumeState resumeState)
    {
        if (timeline == null)
        {
            return;
        }

        if (timelineObject != null && !timelineObject.activeSelf)
        {
            timelineObject.SetActive(true);
        }

        RestoreTimelineSpeed();
        resettingForPlay = true;
        timeline.Stop();
        resettingForPlay = false;

        triggered = true;
        hasStartedPlaying = true;
        timeline.time = ClampTimelineTime(resumeState.ResumeTime);
        timeline.Play();
    }

    private double ClampTimelineTime(double time)
    {
        double duration = timeline != null ? timeline.duration : 0d;
        if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0d)
        {
            return System.Math.Max(0d, time);
        }

        return System.Math.Max(0d, System.Math.Min(time, duration));
    }

    private double GetSceneReturnResumeTime()
    {
        double currentTime = timeline.time;
        double resumeTime = currentTime + sceneReturnResumeOffsetSeconds;
        TimelineAsset timelineAsset = timeline.playableAsset as TimelineAsset;
        if (timelineAsset == null)
        {
            return ClampTimelineTime(resumeTime);
        }

        foreach (TrackAsset track in timelineAsset.GetOutputTracks())
        {
            if (track == null)
            {
                continue;
            }

            foreach (TimelineClip clip in track.GetClips())
            {
                if (clip == null || !(clip.asset is TimelineLoadSceneClip))
                {
                    continue;
                }

                if (currentTime >= clip.start && currentTime <= clip.end)
                {
                    resumeTime = System.Math.Max(resumeTime, clip.end + sceneReturnResumeOffsetSeconds);
                }
            }
        }

        return ClampTimelineTime(resumeTime);
    }

    private bool TryFindCheckpoint(string checkpointId, out TimelineCheckpoint checkpoint)
    {
        checkpoint = default;
        if (string.IsNullOrWhiteSpace(checkpointId))
        {
            return false;
        }

        string validId = checkpointId.Trim();
        List<TimelineCheckpoint> checkpoints = GetCheckpoints();
        for (int i = 0; i < checkpoints.Count; i++)
        {
            if (string.Equals(checkpoints[i].Id, validId, System.StringComparison.Ordinal))
            {
                checkpoint = checkpoints[i];
                return true;
            }
        }

        return false;
    }

    private List<TimelineCheckpoint> GetCheckpoints()
    {
        List<TimelineCheckpoint> checkpoints = new List<TimelineCheckpoint>();
        TimelineAsset timelineAsset = timeline != null ? timeline.playableAsset as TimelineAsset : null;
        if (timelineAsset == null)
        {
            return checkpoints;
        }

        foreach (TrackAsset track in timelineAsset.GetOutputTracks())
        {
            AddTrackCheckpoints(track, checkpoints);
        }

        if (timelineAsset.markerTrack != null)
        {
            AddTrackCheckpoints(timelineAsset.markerTrack, checkpoints);
        }

        checkpoints.Sort((a, b) =>
        {
            int timeComparison = a.Time.CompareTo(b.Time);
            return timeComparison != 0
                ? timeComparison
                : string.CompareOrdinal(a.Id, b.Id);
        });
        return checkpoints;
    }

    private static void AddTrackCheckpoints(TrackAsset track, List<TimelineCheckpoint> checkpoints)
    {
        if (track == null)
        {
            return;
        }

        foreach (IMarker marker in track.GetMarkers())
        {
            TimelineEmbedMarker checkpointMarker = marker as TimelineEmbedMarker;
            if (checkpointMarker == null
                || !checkpointMarker.isSkipCheckpoint
                || string.IsNullOrWhiteSpace(checkpointMarker.checkpointId))
            {
                continue;
            }

            checkpoints.Add(new TimelineCheckpoint(
                checkpointMarker.checkpointId.Trim(),
                checkpointMarker.time));
        }
    }

    private void SeekToCheckpoint(TimelineCheckpoint checkpoint)
    {
        if (timeline == null || isSeekingWithTransition)
        {
            return;
        }

        if (enableBlackTransitionOnSkip && skipTransitionDuration > 0f && Application.isPlaying)
        {
            StartCoroutine(SeekToCheckpointWithTransitionRoutine(checkpoint));
            return;
        }

        PerformSeekToCheckpoint(checkpoint);
    }

    private void PerformSeekToCheckpoint(TimelineCheckpoint checkpoint)
    {
        bool shouldResume = resumePlaybackAfterSkip
            || timeline.state == PlayState.Playing
            || hasStartedPlaying;

        RestoreTimelineSpeed();
        timeline.Pause();
        TimelineSeekContext.BeginExplicitSeek(timeline);
        try
        {
            timeline.time = ClampTimelineTime(checkpoint.Time);
            timeline.Evaluate();
        }
        finally
        {
            TimelineSeekContext.EndExplicitSeek(timeline);
        }

        triggered = true;
        if (shouldResume)
        {
            hasStartedPlaying = true;
            timeline.Play();
            LockPlayerIfNeeded();
        }
    }

    private IEnumerator SeekToCheckpointWithTransitionRoutine(TimelineCheckpoint checkpoint)
    {
        isSeekingWithTransition = true;
        CleanupSkipTransitionCanvas();

        GameObject canvasObj = new GameObject("TimelineSkipTransitionCanvas");
        activeSkipTransitionCanvas = canvasObj;
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;

        CanvasGroup canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 0f;

        GameObject imageObj = new GameObject("BlackOverlay");
        imageObj.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Image image = imageObj.AddComponent<UnityEngine.UI.Image>();
        image.color = Color.black;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;

        float halfDuration = skipTransitionDuration * 0.5f;
        float elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Clamp01(elapsed / halfDuration);
            }
            yield return null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        PerformSeekToCheckpoint(checkpoint);
        yield return null;

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Clamp01(1f - (elapsed / halfDuration));
            }
            yield return null;
        }

        CleanupSkipTransitionCanvas();
        isSeekingWithTransition = false;
    }

    private void CleanupSkipTransitionCanvas()
    {
        if (activeSkipTransitionCanvas != null)
        {
            Destroy(activeSkipTransitionCanvas);
            activeSkipTransitionCanvas = null;
        }
    }

    private bool CanPlayTimeline()
    {
        if (forcePlaying)
        {
            return true;
        }

        if (triggerOnce && !TimelinePlayStateManager.CanPlay(GetSceneName(), GetTimelineId()))
        {
            return false;
        }

        return true;
    }

    private void MarkTimelinePlayed()
    {
        TimelinePlayStateManager.MarkPlayed(GetSceneName(), GetTimelineId());
    }

    private double GetLastCheckpointTimeBeforeCurrent()
    {
        if (timeline == null)
        {
            return 0d;
        }

        double currentTime = timeline.time;
        double resumeTime = 0d;
        List<TimelineCheckpoint> checkpoints = GetCheckpoints();
        for (int i = checkpoints.Count - 1; i >= 0; i--)
        {
            if (checkpoints[i].Time <= currentTime)
            {
                resumeTime = checkpoints[i].Time;
                break;
            }
        }
        return resumeTime;
    }

    private string GetTimelineId()
    {
        if (!string.IsNullOrWhiteSpace(timelineId))
        {
            return timelineId.Trim();
        }

        return $"{GetSceneName()}/{gameObject.name}";
    }

    private string GetSceneName()
    {
        return gameObject.scene.IsValid()
            ? gameObject.scene.name
            : SceneManager.GetActiveScene().name;
    }

    private readonly struct TimelineCheckpoint
    {
        public readonly string Id;
        public readonly double Time;

        public TimelineCheckpoint(string id, double time)
        {
            Id = id;
            Time = time;
        }
    }
}
