using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

internal interface ITimelinePlayerSpawnPoint
{
    string MarkerId { get; }
}

[DisallowMultipleComponent]
public class TimelineSceneTravelManager : MonoBehaviour
{
    private const float SingleLoadTimeoutSeconds = 120f;
    private const int HighUploadTimeSlice = 16;
    private const int HighUploadBufferSize = 64;
    private const int FadeCanvasSortingOrder = 32767;

    private static TimelineSceneTravelManager instance;

    private readonly Stack<SceneTravelState> travelStack = new Stack<SceneTravelState>();
    private bool transitionInProgress;

    private Canvas fadeCanvas;
    private CanvasGroup fadeCanvasGroup;


    public static TimelineSceneTravelManager Instance
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            TimelineSceneTravelManager existing = FindAnyObjectByType<TimelineSceneTravelManager>();
            if (existing != null)
            {
                instance = existing;
                return instance;
            }

            GameObject managerObject = new GameObject(nameof(TimelineSceneTravelManager));
            instance = managerObject.AddComponent<TimelineSceneTravelManager>();
            return instance;
        }
    }

    public static bool IsTransitionInProgress => instance != null && instance.transitionInProgress;

    public static bool HasPreviousScene => Instance.travelStack.Count > 0;


    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureFadeCanvas();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void EnsureFadeCanvas()
    {
        if (fadeCanvas != null) return;

        GameObject canvasObj = new GameObject("TimelineTransitionCanvas");
        canvasObj.transform.SetParent(transform, false);

        fadeCanvas = canvasObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = FadeCanvasSortingOrder;

        canvasObj.AddComponent<CanvasScaler>();

        fadeCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
        fadeCanvasGroup.interactable = false;

        GameObject imgObj = new GameObject("BlackOverlay");
        imgObj.transform.SetParent(canvasObj.transform, false);

        Image fadeImage = imgObj.AddComponent<Image>();
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = false;

        RectTransform rt = imgObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
    }

    public static void LoadSceneAdditive(
        string sceneName,
        bool freezeCurrentScene,
        bool setLoadedSceneActive,
        PlayableDirector sourceDirector = null,
        string playerSpawnId = null,
        bool useFade = true,
        float fadeDuration = 0.2f)
    {
        Instance.BeginLoadSceneAdditive(
            sceneName,
            freezeCurrentScene,
            setLoadedSceneActive,
            sourceDirector,
            playerSpawnId,
            useFade,
            fadeDuration);
    }

    public static void LoadSceneSingle(
        string sceneName,
        string playerSpawnId = null,
        bool useFade = true,
        float fadeDuration = 0.2f)
    {
        Instance.BeginLoadSceneSingle(sceneName, playerSpawnId, useFade, fadeDuration);
    }

    public static void ReturnToPreviousScene(bool useFade = true, float fadeDuration = 0.2f)
    {
        Instance.BeginReturnToPreviousScene(useFade, fadeDuration);
    }

    public static void ReturnToScene(string sceneName, bool useFade = true, float fadeDuration = 0.2f)
    {
        Instance.BeginReturnToScene(sceneName, useFade, fadeDuration);
    }

    private void BeginLoadSceneAdditive(
        string sceneName,
        bool freezeCurrentScene,
        bool setLoadedSceneActive,
        PlayableDirector sourceDirector,
        string playerSpawnId,
        bool useFade,
        float fadeDuration)
    {
        if (transitionInProgress || string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        StartCoroutine(LoadSceneAdditiveRoutine(
            sceneName.Trim(),
            freezeCurrentScene,
            setLoadedSceneActive,
            sourceDirector,
            playerSpawnId,
            useFade,
            fadeDuration));
    }

    private void BeginLoadSceneSingle(
        string sceneName,
        string playerSpawnId,
        bool useFade,
        float fadeDuration)
    {
        if (transitionInProgress || string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        StartCoroutine(LoadSceneSingleRoutine(sceneName.Trim(), playerSpawnId, useFade, fadeDuration));
    }

    private void BeginReturnToPreviousScene(bool useFade, float fadeDuration)
    {
        if (transitionInProgress || travelStack.Count == 0)
        {
            return;
        }

        StartCoroutine(ReturnToPreviousSceneRoutine(useFade, fadeDuration));
    }

    private void BeginReturnToScene(string sceneName, bool useFade, float fadeDuration)
    {
        if (transitionInProgress || string.IsNullOrWhiteSpace(sceneName) || travelStack.Count == 0)
        {
            return;
        }

        StartCoroutine(ReturnToSceneRoutine(sceneName.Trim(), useFade, fadeDuration));
    }

    private IEnumerator LoadSceneAdditiveRoutine(
        string sceneName,
        bool freezeCurrentScene,
        bool setLoadedSceneActive,
        PlayableDirector sourceDirector,
        string playerSpawnId,
        bool useFade,
        float fadeDuration)
    {
        transitionInProgress = true;
        PlayerTransitionState playerState = CaptureAndFreezePlayer();

        Scene fromScene = SceneManager.GetActiveScene();
        if (!fromScene.IsValid())
        {
            Debug.LogWarning($"{nameof(TimelineSceneTravelManager)} could not capture the active scene.");
            transitionInProgress = false;
            yield break;
        }

        TimelineSceneResumeState resumeState = null;
        if (freezeCurrentScene)
        {
            TimelinePlayController.TryCaptureSceneReturnResume(fromScene, sourceDirector, out resumeState);
        }

        if (useFade && fadeDuration > 0f)
        {
            yield return FadeRoutine(0f, 1f, fadeDuration);
        }

        int prevTimeSlice = QualitySettings.asyncUploadTimeSlice;
        int prevBufferSize = QualitySettings.asyncUploadBufferSize;
        ThreadPriority prevPriority = Application.backgroundLoadingPriority;

        QualitySettings.asyncUploadTimeSlice = HighUploadTimeSlice;
        QualitySettings.asyncUploadBufferSize = HighUploadBufferSize;
        Application.backgroundLoadingPriority = ThreadPriority.High;

        Scene loadedScene = SceneManager.GetSceneByName(sceneName);
        try
        {
            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
            {
                AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

                if (loadOperation == null)
                {
                    Debug.LogWarning($"{nameof(TimelineSceneTravelManager)} failed to load scene '{sceneName}'.");
                    transitionInProgress = false;
                    if (useFade) yield return FadeRoutine(1f, 0f, fadeDuration);
                    yield break;
                }

                while (!loadOperation.isDone)
                {
                    yield return null;
                }

                loadedScene = SceneManager.GetSceneByName(sceneName);
            }
        }
        finally
        {
            QualitySettings.asyncUploadTimeSlice = prevTimeSlice;
            QualitySettings.asyncUploadBufferSize = prevBufferSize;
            Application.backgroundLoadingPriority = prevPriority;
        }

        if (!loadedScene.IsValid() || !loadedScene.isLoaded)
        {
            Debug.LogWarning($"{nameof(TimelineSceneTravelManager)} could not find loaded scene '{sceneName}'.");
            transitionInProgress = false;
            if (useFade) yield return FadeRoutine(1f, 0f, fadeDuration);
            yield break;
        }

        SceneTravelState state = new SceneTravelState(
            fromScene.name,
            loadedScene.name,
            CaptureRootStates(fromScene, null),
            resumeState,
            playerState);
        travelStack.Push(state);

        if (setLoadedSceneActive)
        {
            SceneManager.SetActiveScene(loadedScene);
        }

        Player destinationPlayer = FindScenePlayer(loadedScene);

        if (freezeCurrentScene)
        {
            SetRootObjectsActive(state.RootStates, false);
        }
        else if (playerState != null && playerState.Player != null)
        {
            playerState.Player.gameObject.SetActive(false);
        }

        TryPlacePlayerInScene(loadedScene, playerSpawnId, destinationPlayer);

        yield return null;

        if (useFade && fadeDuration > 0f)
        {
            yield return FadeRoutine(1f, 0f, fadeDuration);
        }

        transitionInProgress = false;
    }

    private IEnumerator LoadSceneSingleRoutine(
        string sceneName,
        string playerSpawnId,
        bool useFade,
        float fadeDuration)
    {
        transitionInProgress = true;
        travelStack.Clear();
        PlayerTransitionState playerState = CaptureAndFreezePlayer();

        bool loadStarted = true;
        try
        {
            Tien.AdvancedLoading.Core.SceneLoader.Load(sceneName);
        }
        catch (System.Exception exception)
        {
            loadStarted = false;
            Debug.LogException(exception);
        }

        if (!loadStarted)
        {
            transitionInProgress = false;
            yield break;
        }

        float timeoutAt = Time.realtimeSinceStartup + SingleLoadTimeoutSeconds;
        Scene loadedScene = SceneManager.GetSceneByName(sceneName);
        while ((!loadedScene.IsValid()
                || !loadedScene.isLoaded
                || SceneManager.GetActiveScene() != loadedScene
                || Tien.AdvancedLoading.Core.SceneLoader.IsLoading)
               && Time.realtimeSinceStartup < timeoutAt)
        {
            yield return null;
            loadedScene = SceneManager.GetSceneByName(sceneName);
        }

        if (!loadedScene.IsValid()
            || !loadedScene.isLoaded
            || SceneManager.GetActiveScene() != loadedScene)
        {
            Debug.LogWarning(
                $"{nameof(TimelineSceneTravelManager)} timed out waiting for scene '{sceneName}'. "
                + "The player remains frozen for safety.");
            transitionInProgress = false;
            yield break;
        }

        TryPlacePlayerInScene(loadedScene, playerSpawnId, FindScenePlayer(loadedScene));
        transitionInProgress = false;
    }

    private IEnumerator ReturnToPreviousSceneRoutine(bool useFade = true, float fadeDuration = 0.2f)
    {
        transitionInProgress = true;
        FreezeCurrentPlayer();

        if (useFade && fadeDuration > 0f)
        {
            yield return FadeRoutine(0f, 1f, fadeDuration);
        }

        SceneTravelState state = travelStack.Pop();
        yield return UnloadSceneIfLoaded(state.ToSceneName);
        RestoreRootObjects(state.RootStates);
        SetActiveSceneIfLoaded(state.FromSceneName);
        RestorePlayerForReturn(state.PlayerState);

        yield return null;
        TimelinePlayController.ResumeFromSceneReturn(state.TimelineResumeState);

        if (useFade && fadeDuration > 0f)
        {
            yield return FadeRoutine(1f, 0f, fadeDuration);
        }

        transitionInProgress = false;
    }

    private IEnumerator ReturnToSceneRoutine(string sceneName, bool useFade = true, float fadeDuration = 0.2f)
    {
        transitionInProgress = true;
        FreezeCurrentPlayer();

        if (useFade && fadeDuration > 0f)
        {
            yield return FadeRoutine(0f, 1f, fadeDuration);
        }

        TimelineSceneResumeState resumeState = null;
        PlayerTransitionState playerState = null;

        while (travelStack.Count > 0)
        {
            SceneTravelState state = travelStack.Pop();
            yield return UnloadSceneIfLoaded(state.ToSceneName);
            RestoreRootObjects(state.RootStates);
            SetActiveSceneIfLoaded(state.FromSceneName);
            resumeState = state.TimelineResumeState;
            playerState = state.PlayerState;

            if (string.Equals(state.FromSceneName, sceneName, System.StringComparison.Ordinal))
            {
                break;
            }
        }

        RestorePlayerForReturn(playerState);
        yield return null;
        TimelinePlayController.ResumeFromSceneReturn(resumeState);

        if (useFade && fadeDuration > 0f)
        {
            yield return FadeRoutine(1f, 0f, fadeDuration);
        }

        transitionInProgress = false;
    }

    private IEnumerator FadeRoutine(float startAlpha, float targetAlpha, float duration)
    {
        EnsureFadeCanvas();
        if (fadeCanvasGroup == null) yield break;

        fadeCanvasGroup.blocksRaycasts = targetAlpha > 0.01f;
        if (duration <= 0.01f)
        {
            fadeCanvasGroup.alpha = targetAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        fadeCanvasGroup.alpha = targetAlpha;
        fadeCanvasGroup.blocksRaycasts = targetAlpha > 0.01f;
    }

    private IEnumerator UnloadSceneIfLoaded(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            yield break;
        }

        AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(scene);
        if (unloadOperation == null)
        {
            yield break;
        }

        while (!unloadOperation.isDone)
        {
            yield return null;
        }
    }

    private static List<RootObjectState> CaptureRootStates(
        Scene scene,
        GameObject excludedRoot)
    {
        List<RootObjectState> rootStates = new List<RootObjectState>();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return rootStates;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && roots[i] != excludedRoot)
            {
                rootStates.Add(new RootObjectState(roots[i], roots[i].activeSelf));
            }
        }

        return rootStates;
    }

    private static void SetRootObjectsActive(List<RootObjectState> rootStates, bool active)
    {
        for (int i = 0; i < rootStates.Count; i++)
        {
            GameObject root = rootStates[i].Root;
            if (root != null)
            {
                root.SetActive(active);
            }
        }
    }

    private static void RestoreRootObjects(List<RootObjectState> rootStates)
    {
        for (int i = 0; i < rootStates.Count; i++)
        {
            RootObjectState rootState = rootStates[i];
            if (rootState.Root != null)
            {
                rootState.Root.SetActive(rootState.WasActive);
            }
        }
    }

    private static void SetActiveSceneIfLoaded(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (scene.IsValid() && scene.isLoaded)
        {
            SceneManager.SetActiveScene(scene);
        }
    }

    private static PlayerTransitionState CaptureAndFreezePlayer()
    {
        Player player = ResolveActivePlayer();
        if (player == null)
        {
            Debug.LogWarning($"{nameof(TimelineSceneTravelManager)} could not find the active player.");
            return null;
        }

        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        PlayerTransitionState state = new PlayerTransitionState(
            player,
            body,
            player.transform.position,
            player.IsControlLocked,
            body != null && body.simulated,
            body != null ? body.gravityScale : 0f,
            body != null ? body.linearVelocity : Vector2.zero,
            body != null ? body.angularVelocity : 0f);

        FreezePlayer(player, body);
        return state;
    }

    private static void FreezeCurrentPlayer()
    {
        Player player = ResolveActivePlayer();
        if (player != null)
        {
            FreezePlayer(player, player.GetComponent<Rigidbody2D>());
        }
    }

    private static Player ResolveActivePlayer()
    {
        Player player = PlayerRegistry.Current;
        if (player == null)
        {
            player = TimelineTrigger2D.CurrentPlayer;
        }

#if UNITY_2023_1_OR_NEWER
        if (player == null)
        {
            player = Object.FindFirstObjectByType<Player>();
        }
#else
        if (player == null)
        {
            player = Object.FindObjectOfType<Player>();
        }
#endif

        return player;
    }

    private static void FreezePlayer(Player player, Rigidbody2D body)
    {
        player.CancelActiveSkillForTimeline();
        player.SetControlLocked(true);
        player.StopCutsceneMoveInput();

        if (body == null)
        {
            return;
        }

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.simulated = false;
    }

    private static void TryPlacePlayerInScene(
        Scene scene,
        string playerSpawnId,
        Player player)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        if (player == null)
        {
            player = FindScenePlayer(scene);
        }

        if (player == null)
        {
            Debug.LogWarning(
                $"{nameof(TimelineSceneTravelManager)} could not find a placed Player "
                + $"in scene '{scene.name}'. The player remains at its restored position.");
            return;
        }

        MonoBehaviour spawnPoint = FindSpawnPoint(scene, playerSpawnId);
        if (spawnPoint != null)
        {
            player.transform.position = spawnPoint.transform.position;
        }
        else if (!string.IsNullOrWhiteSpace(playerSpawnId))
        {
            Debug.LogWarning(
                $"{nameof(TimelineSceneTravelManager)} could not find marker "
                + $"'{playerSpawnId.Trim()}' in scene '{scene.name}'. "
                + "The player arrives at its restored position instead.");
        }

        PlayerRegistry.Register(player);
    }

    private static MonoBehaviour FindSpawnPoint(Scene scene, string playerSpawnId)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        string requestedId = string.IsNullOrWhiteSpace(playerSpawnId)
            ? string.Empty
            : playerSpawnId.Trim();
        MonoBehaviour firstMarker = null;
        GameObject[] roots = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            MonoBehaviour[] markers =
                roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
            for (int markerIndex = 0; markerIndex < markers.Length; markerIndex++)
            {
                MonoBehaviour markerComponent = markers[markerIndex];
                if (!(markerComponent is ITimelinePlayerSpawnPoint marker))
                {
                    continue;
                }

                firstMarker ??= markerComponent;
                if (!string.IsNullOrEmpty(requestedId)
                    && string.Equals(
                        marker.MarkerId?.Trim(),
                        requestedId,
                        System.StringComparison.Ordinal))
                {
                    return markerComponent;
                }
            }
        }

        return string.IsNullOrEmpty(requestedId) ? firstMarker : null;
    }

    private static Player FindScenePlayer(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            Player[] players = roots[rootIndex].GetComponentsInChildren<Player>(true);
            for (int playerIndex = 0; playerIndex < players.Length; playerIndex++)
            {
                Player candidate = players[playerIndex];
                if (candidate != null && candidate.gameObject.scene == scene)
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static void RestorePlayerForReturn(PlayerTransitionState state)
    {
        if (state == null || state.Player == null)
        {
            return;
        }

        state.Player.transform.position = state.Position;
        RestorePlayerPhysicsAndControl(state, restoreVelocity: true);
    }

    private static void RestorePlayerPhysicsAndControl(
        PlayerTransitionState state,
        bool restoreVelocity)
    {
        if (state.Body != null)
        {
            state.Body.gravityScale = state.GravityScale;
            state.Body.simulated = state.WasSimulated;
            state.Body.linearVelocity = restoreVelocity ? state.LinearVelocity : Vector2.zero;
            state.Body.angularVelocity = restoreVelocity ? state.AngularVelocity : 0f;
        }

        state.Player.SetControlLocked(state.WasControlLocked);
    }

    private sealed class SceneTravelState
    {
        public readonly string FromSceneName;
        public readonly string ToSceneName;
        public readonly List<RootObjectState> RootStates;
        public readonly TimelineSceneResumeState TimelineResumeState;
        public readonly PlayerTransitionState PlayerState;

        public SceneTravelState(
            string fromSceneName,
            string toSceneName,
            List<RootObjectState> rootStates,
            TimelineSceneResumeState timelineResumeState,
            PlayerTransitionState playerState)
        {
            FromSceneName = fromSceneName;
            ToSceneName = toSceneName;
            RootStates = rootStates;
            TimelineResumeState = timelineResumeState;
            PlayerState = playerState;
        }
    }

    private sealed class PlayerTransitionState
    {
        public readonly Player Player;
        public readonly Rigidbody2D Body;
        public readonly Vector3 Position;
        public readonly bool WasControlLocked;
        public readonly bool WasSimulated;
        public readonly float GravityScale;
        public readonly Vector2 LinearVelocity;
        public readonly float AngularVelocity;

        public PlayerTransitionState(
            Player player,
            Rigidbody2D body,
            Vector3 position,
            bool wasControlLocked,
            bool wasSimulated,
            float gravityScale,
            Vector2 linearVelocity,
            float angularVelocity)
        {
            Player = player;
            Body = body;
            Position = position;
            WasControlLocked = wasControlLocked;
            WasSimulated = wasSimulated;
            GravityScale = gravityScale;
            LinearVelocity = linearVelocity;
            AngularVelocity = angularVelocity;
        }
    }

    private struct RootObjectState
    {
        public readonly GameObject Root;
        public readonly bool WasActive;

        public RootObjectState(GameObject root, bool wasActive)
        {
            Root = root;
            WasActive = wasActive;
        }
    }
}
