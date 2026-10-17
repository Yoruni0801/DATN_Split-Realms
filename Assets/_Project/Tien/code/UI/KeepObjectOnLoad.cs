using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
public class KeepObjectOnLoad : MonoBehaviour
{
    private const float DefaultFadeDuration = 0.25f;
    private const float InstantFadeSpeed = 1000f;
    private const float AlphaTolerance = 0.001f;

    public static KeepObjectOnLoad Instance { get; private set; }
    public static Canvas HudCanvas { get; private set; }

    [Header("Scenes Configuration")]
    [Tooltip("Các scene luôn luôn ẩn UI HUD")]
    [SerializeField] private string[] additionalHiddenScenes;
    [SerializeField] private bool autoHideAnyMiniGameScene = true;

    private static readonly string[] DefaultHiddenScenes =
    {
        "MainMenu",
        "LoadingScene",
        "TheStart",
        "MiniGameBook",
        "AddMiniGame",
        "AddMinigame2",
        "PuzzleQuest",
        "CrystalMarble",
        "LaserMirror",
        "DispersalCartridge",
        "MiniGameAmNhac",
        "MiniGameDaLan",
        "MiniGameTachAnBang",
        "MiniGameTest",
        "Sua_Minigame",
        "suaminigame",
        "#giaicutinhlinh"
    };

    [Header("Fade Settings")]
    [SerializeField] private float fadeDuration = DefaultFadeDuration;

    [Header("Prefabs")]
    [SerializeField] private GameObject enemyHealthBarPrefab;

    private readonly HashSet<PlayableDirector> knownDirectors = new HashSet<PlayableDirector>();
    private readonly HashSet<PlayableDirector> runningDirectors = new HashSet<PlayableDirector>();
    private readonly List<PlayableDirector> deadDirectorsCache = new List<PlayableDirector>();
    private readonly HashSet<Component> forceHideRequests = new HashSet<Component>();
    private HashSet<string> hiddenScenesSet;

    private CanvasGroup canvasGroup;
    private float visibleAlpha = 1f;
    private bool isCurrentSceneHidden;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        HudCanvas = GetComponent<Canvas>();
        if (HudCanvas == null) HudCanvas = GetComponentInParent<Canvas>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        visibleAlpha = canvasGroup.alpha;

        transform.SetParent(null);
        transform.localScale = Vector3.one;
        DontDestroyOnLoad(gameObject);

        BuildHiddenScenesCache();
        UpdateSceneState(SceneManager.GetActiveScene());
        InitializeSharedHealthBar();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        ScanTimelineDirectors();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        UnsubscribeTimelineDirectors();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            HudCanvas = null;
        }
    }

    private void Update()
    {
        if (isCurrentSceneHidden)
        {
            if (canvasGroup != null && (canvasGroup.alpha > 0f || canvasGroup.blocksRaycasts || canvasGroup.interactable))
            {
                canvasGroup.alpha = 0f;
                SetCanvasGroupInteractable(false);
            }
            return;
        }

        // Kiểm tra state của các director đã biết
        CheckForRunningDirectors();
        FadeCanvas();
    }

    private void CheckForRunningDirectors()
    {
        deadDirectorsCache.Clear();

        foreach (PlayableDirector director in knownDirectors)
        {
            if (director == null)
            {
                deadDirectorsCache.Add(director);
                continue;
            }

            if (director.state == PlayState.Playing)
            {
                runningDirectors.Add(director);
            }
            else
            {
                runningDirectors.Remove(director);
            }
        }

        // Dọn dẹp các director đã bị destroy khỏi tập theo dõi
        for (int i = 0; i < deadDirectorsCache.Count; i++)
        {
            PlayableDirector dead = deadDirectorsCache[i];
            knownDirectors.Remove(dead);
            runningDirectors.Remove(dead);
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateSceneState(SceneManager.GetActiveScene());
        UnsubscribeTimelineDirectors();
        ScanTimelineDirectors();
    }

    private void HandleActiveSceneChanged(Scene current, Scene next)
    {
        UpdateSceneState(next.IsValid() ? next : SceneManager.GetActiveScene());
        UnsubscribeTimelineDirectors();
        ScanTimelineDirectors();
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        UpdateSceneState(SceneManager.GetActiveScene());
        UnsubscribeTimelineDirectors();
        ScanTimelineDirectors();
    }

    private void UpdateSceneState(Scene scene)
    {
        isCurrentSceneHidden = IsSceneHidden(scene.name);
        if (canvasGroup == null)
        {
            return;
        }

        if (isCurrentSceneHidden)
        {
            canvasGroup.alpha = 0f;
            SetCanvasGroupInteractable(false);
        }
        else
        {
            SetCanvasGroupInteractable(true);
        }
    }

    private bool IsSceneHidden(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;

        if (hiddenScenesSet != null && hiddenScenesSet.Contains(sceneName))
        {
            return true;
        }

        if (autoHideAnyMiniGameScene)
        {
            if (sceneName.IndexOf("MiniGame", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sceneName.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sceneName.IndexOf("Loading", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private void BuildHiddenScenesCache()
    {
        hiddenScenesSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Thêm danh sách mặc định không thể thiếu
        for (int i = 0; i < DefaultHiddenScenes.Length; i++)
        {
            if (!string.IsNullOrEmpty(DefaultHiddenScenes[i]))
            {
                hiddenScenesSet.Add(DefaultHiddenScenes[i]);
            }
        }

        // 2. Thêm danh sách bổ sung cấu hình từ Inspector nếu có
        if (additionalHiddenScenes != null)
        {
            for (int i = 0; i < additionalHiddenScenes.Length; i++)
            {
                if (!string.IsNullOrEmpty(additionalHiddenScenes[i]))
                {
                    hiddenScenesSet.Add(additionalHiddenScenes[i]);
                }
            }
        }
    }

    private void ScanTimelineDirectors()
    {
        PlayableDirector[] directors = FindObjectsByType<PlayableDirector>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < directors.Length; i++)
        {
            PlayableDirector director = directors[i];
            if (director == null)
            {
                continue;
            }

            // Bỏ qua PlayableDirector nếu có đính kèm IgnoreHUDHide (trên cùng GameObject hoặc GameObject cha)
            if (director.GetComponentInParent<IgnoreHUDHide>() != null)
            {
                continue;
            }

            if (!knownDirectors.Add(director))
            {
                continue;
            }

            director.played += HandleTimelinePlayed;
            director.stopped += HandleTimelineStopped;

            if (director.state == PlayState.Playing)
            {
                runningDirectors.Add(director);
            }
        }
    }

    private void HandleTimelinePlayed(PlayableDirector director)
    {
        if (director != null)
        {
            runningDirectors.Add(director);
        }
    }

    private void HandleTimelineStopped(PlayableDirector director)
    {
        if (director != null)
        {
            runningDirectors.Remove(director);
        }
    }

    private void UnsubscribeTimelineDirectors()
    {
        foreach (PlayableDirector director in knownDirectors)
        {
            if (director == null)
            {
                continue;
            }

            director.played -= HandleTimelinePlayed;
            director.stopped -= HandleTimelineStopped;
        }

        knownDirectors.Clear();
        runningDirectors.Clear();
    }

    public void RequestForceHideUI(Component requester)
    {
        if (requester != null)
        {
            forceHideRequests.Add(requester);
        }
    }

    public void RemoveForceHideUIRequest(Component requester)
    {
        if (requester != null)
        {
            forceHideRequests.Remove(requester);
        }
    }

    private void FadeCanvas()
    {
        if (canvasGroup == null)
        {
            return;
        }

        // Cleanup null requesters in case they were destroyed
        forceHideRequests.RemoveWhere(req => req == null);

        bool isTimelineRunning = runningDirectors.Count > 0;
        bool isFairyPossessed = FairyPossessionController.IsPossessionActive;
        bool isForcedHidden = forceHideRequests.Count > 0;
        bool shouldHideUI = isTimelineRunning || isFairyPossessed || isForcedHidden;

        float targetAlpha = shouldHideUI ? 0f : visibleAlpha;
        bool canInteract = !shouldHideUI && targetAlpha > AlphaTolerance;

        if (canvasGroup.interactable != canInteract)
        {
            SetCanvasGroupInteractable(canInteract);
        }

        if (Mathf.Approximately(canvasGroup.alpha, targetAlpha))
        {
            return;
        }

        float duration = fadeDuration > 0f ? fadeDuration : DefaultFadeDuration;
        float speed = shouldHideUI ? InstantFadeSpeed : (Mathf.Abs(visibleAlpha) / duration);

        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, speed * Time.unscaledDeltaTime);
    }

    private void SetCanvasGroupInteractable(bool state)
    {
        if (canvasGroup == null) return;
        canvasGroup.interactable = state;
        canvasGroup.blocksRaycasts = state;
    }

    private void InitializeSharedHealthBar()
    {
        if (HudCanvas == null || Bao.Enemies.WorldSpaceHealthBar.Instance != null)
        {
            return;
        }

        GameObject prefab = enemyHealthBarPrefab;
        if (prefab == null) prefab = Resources.Load<GameObject>("UI/EnemyHealthBarCanvas");
        if (prefab == null) prefab = Resources.Load<GameObject>("EnemyHealthBarCanvas");

        if (prefab != null)
        {
            GameObject instanceGO = Instantiate(prefab, HudCanvas.transform);
            instanceGO.name = "EnemyHealthBar_StaticShared";
            instanceGO.SetActive(true);

            var instance = instanceGO.GetComponent<Bao.Enemies.WorldSpaceHealthBar>();
            if (instance != null)
            {
                Bao.Enemies.WorldSpaceHealthBar.RegisterInstance(instance, HudCanvas);
            }
        }
        else
        {
            Debug.LogError("[KeepObjectOnLoad] Failed to load EnemyHealthBarCanvas prefab from Resources (UI/EnemyHealthBarCanvas)!");
        }
    }
}
