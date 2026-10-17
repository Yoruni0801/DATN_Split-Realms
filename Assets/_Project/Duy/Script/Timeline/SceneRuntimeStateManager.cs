using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class SceneRuntimeStateManager : MonoBehaviour
{
    private static SceneRuntimeStateManager instance;
    private static bool isShuttingDown;

    private readonly Dictionary<string, Vector3> committedPlayerPositionsByScene = new Dictionary<string, Vector3>();
    private readonly Dictionary<string, Vector3> pendingPlayerPositionsByScene = new Dictionary<string, Vector3>();
    private readonly Dictionary<string, Vector3> checkpointsByScene = new Dictionary<string, Vector3>();
    private readonly HashSet<string> scenesSkippingNextPlayerPositionSave = new HashSet<string>();

    public static SceneRuntimeStateManager Instance
    {
        get
        {
            if (isShuttingDown)
            {
                return null;
            }

            if (instance != null)
            {
                return instance;
            }

            SceneRuntimeStateManager existing = FindAnyObjectByType<SceneRuntimeStateManager>();
            if (existing != null)
            {
                instance = existing;
                return instance;
            }

            GameObject managerObject = new GameObject(nameof(SceneRuntimeStateManager));
            instance = managerObject.AddComponent<SceneRuntimeStateManager>();
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

    public static void SavePlayerPosition(string sceneName, Vector3 position)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        string validScene = sceneName.Trim();
        SceneRuntimeStateManager manager = Instance;
        if (manager == null || manager.scenesSkippingNextPlayerPositionSave.Contains(validScene))
        {
            return;
        }

        manager.pendingPlayerPositionsByScene[validScene] = position;
    }

    public static bool TryGetPlayerPosition(string sceneName, out Vector3 position)
    {
        position = default;
        SceneRuntimeStateManager manager = Instance;
        return manager != null
            && !string.IsNullOrWhiteSpace(sceneName)
            && manager.committedPlayerPositionsByScene.TryGetValue(sceneName.Trim(), out position);
    }

    public static void SaveCheckpoint(string sceneName, Vector3 position)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        SceneRuntimeStateManager manager = Instance;
        if (manager != null)
        {
            manager.checkpointsByScene[sceneName.Trim()] = position;
        }
    }

    public static bool TryGetCheckpoint(string sceneName, out Vector3 position)
    {
        position = default;
        SceneRuntimeStateManager manager = Instance;
        return manager != null
            && !string.IsNullOrWhiteSpace(sceneName)
            && manager.checkpointsByScene.TryGetValue(sceneName.Trim(), out position);
    }

    public static void ResetScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        string validScene = sceneName.Trim();
        SceneRuntimeStateManager manager = Instance;
        if (manager == null)
        {
            return;
        }

        manager.committedPlayerPositionsByScene.Remove(validScene);
        manager.pendingPlayerPositionsByScene.Remove(validScene);
        manager.checkpointsByScene.Remove(validScene);
        manager.scenesSkippingNextPlayerPositionSave.Add(validScene);
    }

    public static void ResetAll()
    {
        if (instance == null)
        {
            return;
        }

        instance.committedPlayerPositionsByScene.Clear();
        instance.pendingPlayerPositionsByScene.Clear();
        instance.checkpointsByScene.Clear();
        instance.scenesSkippingNextPlayerPositionSave.Clear();
    }

    public static void ClearAll()
    {
        ResetAll();
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        string sceneName = scene.name;
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        sceneName = sceneName.Trim();
        scenesSkippingNextPlayerPositionSave.Remove(sceneName);

        if (!pendingPlayerPositionsByScene.TryGetValue(sceneName, out Vector3 position))
        {
            return;
        }

        committedPlayerPositionsByScene[sceneName] = position;
        pendingPlayerPositionsByScene.Remove(sceneName);
    }
}
