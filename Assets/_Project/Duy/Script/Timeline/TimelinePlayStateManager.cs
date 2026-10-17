using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class TimelinePlayStateManager : MonoBehaviour
{
    private static TimelinePlayStateManager instance;
    private static bool isShuttingDown;

    private readonly Dictionary<string, HashSet<string>> committedPlayedTimelineIdsByScene = new Dictionary<string, HashSet<string>>();
    private readonly Dictionary<string, HashSet<string>> pendingPlayedTimelineIdsByScene = new Dictionary<string, HashSet<string>>();

    private readonly Dictionary<string, Dictionary<string, double>> committedInterruptedTimelinesByScene = new Dictionary<string, Dictionary<string, double>>();
    private readonly Dictionary<string, Dictionary<string, double>> pendingInterruptedTimelinesByScene = new Dictionary<string, Dictionary<string, double>>();
    private readonly HashSet<string> scenesAllowingInterruption = new HashSet<string>();

    public static TimelinePlayStateManager Instance
    {
        get
        {
            if (!Application.isPlaying)
            {
                return null;
            }

            if (isShuttingDown)
            {
                return null;
            }

            if (instance != null)
            {
                return instance;
            }

            TimelinePlayStateManager existing = FindAnyObjectByType<TimelinePlayStateManager>();
            if (existing != null)
            {
                instance = existing;
                return instance;
            }

            GameObject managerObject = new GameObject(nameof(TimelinePlayStateManager));
            instance = managerObject.AddComponent<TimelinePlayStateManager>();
            return instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
        isShuttingDown = false;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        isShuttingDown = false;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
    }

    private void OnApplicationQuit()
    {
        isShuttingDown = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            instance = null;
            isShuttingDown = true;
        }
    }

    public static bool CanPlay(string sceneName, string timelineId)
    {
        return !IsPlayed(sceneName, timelineId);
    }

    public static bool IsPlayed(string sceneName, string timelineId)
    {
        string validScene = NormalizeId(sceneName);
        string validId = NormalizeId(timelineId);
        if (string.IsNullOrEmpty(validScene) || string.IsNullOrEmpty(validId))
        {
            return false;
        }

        TimelinePlayStateManager manager = Instance;
        if (manager == null)
        {
            return false;
        }

        if (manager.committedPlayedTimelineIdsByScene.TryGetValue(validScene, out HashSet<string> committed)
            && committed.Contains(validId))
        {
            return true;
        }

        if (manager.pendingPlayedTimelineIdsByScene.TryGetValue(validScene, out HashSet<string> pending)
            && pending.Contains(validId))
        {
            return true;
        }

        return false;
    }

    public static void BeginSceneChange(string targetSceneName, bool continueFromCheckpoint)
    {
        TimelinePlayStateManager manager = Instance;
        if (manager == null)
        {
            return;
        }

        string activeScene = NormalizeId(SceneManager.GetActiveScene().name);
        if (continueFromCheckpoint && !string.IsNullOrEmpty(activeScene))
        {
            manager.scenesAllowingInterruption.Add(activeScene);
        }

        if (!continueFromCheckpoint)
        {
            manager.ResetSceneState(NormalizeId(targetSceneName));
        }
    }

    public static void MarkPlayed(string sceneName, string timelineId)
    {
        string validScene = NormalizeId(sceneName);
        string validId = NormalizeId(timelineId);
        if (string.IsNullOrEmpty(validScene) || string.IsNullOrEmpty(validId))
        {
            return;
        }

        TimelinePlayStateManager manager = Instance;
        if (manager == null)
        {
            return;
        }

        if (!manager.pendingPlayedTimelineIdsByScene.TryGetValue(validScene, out HashSet<string> sceneTimelineIds))
        {
            sceneTimelineIds = new HashSet<string>();
            manager.pendingPlayedTimelineIdsByScene.Add(validScene, sceneTimelineIds);
        }

        sceneTimelineIds.Add(validId);
        manager.RemoveInterruptedTimeline(validScene, validId);
    }

    public static void MarkInterrupted(string sceneName, string timelineId, double resumeTime)
    {
        string validScene = NormalizeId(sceneName);
        string validId = NormalizeId(timelineId);
        if (string.IsNullOrEmpty(validScene) || string.IsNullOrEmpty(validId))
        {
            return;
        }

        TimelinePlayStateManager manager = Instance;
        if (manager == null || !manager.scenesAllowingInterruption.Contains(validScene))
        {
            return;
        }

        if (!manager.pendingInterruptedTimelinesByScene.TryGetValue(validScene, out Dictionary<string, double> sceneTimelines))
        {
            sceneTimelines = new Dictionary<string, double>();
            manager.pendingInterruptedTimelinesByScene.Add(validScene, sceneTimelines);
        }

        sceneTimelines[validId] = resumeTime;
    }

    public static bool TryTakeInterruptedTime(string sceneName, string timelineId, out double resumeTime)
    {
        resumeTime = 0d;
        string validScene = NormalizeId(sceneName);
        string validId = NormalizeId(timelineId);
        TimelinePlayStateManager manager = Instance;
        if (manager != null
            && manager.committedInterruptedTimelinesByScene.TryGetValue(validScene, out Dictionary<string, double> sceneTimelines)
            && sceneTimelines.TryGetValue(validId, out resumeTime))
        {
            sceneTimelines.Remove(validId);
            if (sceneTimelines.Count == 0)
            {
                manager.committedInterruptedTimelinesByScene.Remove(validScene);
            }
            return true;
        }
        return false;
    }

    public static void ResetTimeline(string timelineId)
    {
        string validId = NormalizeId(timelineId);
        if (string.IsNullOrEmpty(validId) || instance == null)
        {
            return;
        }

        foreach (HashSet<string> sceneTimelineIds in instance.committedPlayedTimelineIdsByScene.Values)
        {
            sceneTimelineIds.Remove(validId);
        }
        foreach (HashSet<string> sceneTimelineIds in instance.pendingPlayedTimelineIdsByScene.Values)
        {
            sceneTimelineIds.Remove(validId);
        }
        foreach (Dictionary<string, double> sceneInterruptedTimelines in instance.committedInterruptedTimelinesByScene.Values)
        {
            sceneInterruptedTimelines.Remove(validId);
        }
        foreach (Dictionary<string, double> sceneInterruptedTimelines in instance.pendingInterruptedTimelinesByScene.Values)
        {
            sceneInterruptedTimelines.Remove(validId);
        }
    }

    public static void ResetAll()
    {
        if (instance == null)
        {
            return;
        }

        instance.committedPlayedTimelineIdsByScene.Clear();
        instance.pendingPlayedTimelineIdsByScene.Clear();
        instance.committedInterruptedTimelinesByScene.Clear();
        instance.pendingInterruptedTimelinesByScene.Clear();
        instance.scenesAllowingInterruption.Clear();
    }

    private static string NormalizeId(string timelineId)
    {
        return string.IsNullOrWhiteSpace(timelineId) ? string.Empty : timelineId.Trim();
    }

    private void ResetSceneState(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            return;
        }

        committedPlayedTimelineIdsByScene.Remove(sceneName);
        committedInterruptedTimelinesByScene.Remove(sceneName);
        pendingInterruptedTimelinesByScene.Remove(sceneName);
        pendingPlayedTimelineIdsByScene.Remove(sceneName);
    }

    private void RemoveInterruptedTimeline(string sceneName, string timelineId)
    {
        if (committedInterruptedTimelinesByScene.TryGetValue(sceneName, out Dictionary<string, double> committed))
        {
            committed.Remove(timelineId);
        }

        if (pendingInterruptedTimelinesByScene.TryGetValue(sceneName, out Dictionary<string, double> pending))
        {
            pending.Remove(timelineId);
        }
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        string sceneName = NormalizeId(scene.name);
        if (string.IsNullOrEmpty(sceneName))
        {
            return;
        }

        if (pendingPlayedTimelineIdsByScene.TryGetValue(sceneName, out HashSet<string> sceneTimelineIds))
        {
            if (!committedPlayedTimelineIdsByScene.TryGetValue(sceneName, out HashSet<string> committedTimelineIds))
            {
                committedTimelineIds = new HashSet<string>();
                committedPlayedTimelineIdsByScene.Add(sceneName, committedTimelineIds);
            }

            foreach (string timelineId in sceneTimelineIds)
            {
                committedTimelineIds.Add(timelineId);
            }
            pendingPlayedTimelineIdsByScene.Remove(sceneName);
        }

        if (pendingInterruptedTimelinesByScene.TryGetValue(sceneName, out Dictionary<string, double> sceneInterruptedTimelines))
        {
            if (!committedInterruptedTimelinesByScene.TryGetValue(sceneName, out Dictionary<string, double> committedTimelines))
            {
                committedTimelines = new Dictionary<string, double>();
                committedInterruptedTimelinesByScene.Add(sceneName, committedTimelines);
            }

            foreach (var kvp in sceneInterruptedTimelines)
            {
                committedTimelines[kvp.Key] = kvp.Value;
            }
            pendingInterruptedTimelinesByScene.Remove(sceneName);
        }

        scenesAllowingInterruption.Remove(sceneName);
    }
}
