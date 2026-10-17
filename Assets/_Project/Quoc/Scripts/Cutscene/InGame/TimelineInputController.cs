using UnityEngine;
using UnityEngine.Playables;

public class TimelineInputController : MonoBehaviour
{
    public static TimelineInputController Instance;

    public static TimelineInputController GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        Instance = FindFirstObjectByType<TimelineInputController>();
        if (Instance != null)
        {
            return Instance;
        }

        GameObject controllerObject = new GameObject("TimelineInputController");
        Instance = controllerObject.AddComponent<TimelineInputController>();
        return Instance;
    }

    [Header("Player")]
    [SerializeField]
    private GameObject player;

    [Header("QTE UI mặc định")]
    [SerializeField]
    private GameObject qteUI;

    private GameObject activeQteUI;

    // UI riêng được bật sau khi người chơi bấm đúng phím.
    // UI này không bị điều khiển bởi việc Pause/Play của Timeline.
    private GameObject activeContinueUI;

    private PlayableDirector currentDirector;

    private KeyCode requiredKey;

    private GameObject actionObject;

    private ITimelineInputAction currentAction;

    private bool waitingForInput;

    private bool inputLocked;

    // Không Pause PlayableDirector. Thay vào đó đóng băng tốc độ các root playable.
    // Director vẫn ở trạng thái Playing nên các PlayableBehaviour (ví dụ camera shake)
    // vẫn được evaluate mỗi frame.
    private double[] frozenRootSpeeds;
    private bool timelineGraphFrozen;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        SetObjectVisible(qteUI, false);
    }

    private void Update()
    {
        if (!waitingForInput)
            return;

        CheckInput();
    }

    private void CheckInput()
    {
        // CHỈ nhận đúng phím được yêu cầu
        if (!Input.GetKeyDown(requiredKey))
            return;

        CompleteInput();
    }

    public void StartWaitingForInput(
        PlayableDirector director,
        KeyCode key,
        GameObject targetObject,
        GameObject qteUiOverride,
        GameObject continueUiOverride)
    {
        if (waitingForInput)
        {
            Debug.LogWarning(
                "TimelineInputController: Đang chờ input khác."
            );

            return;
        }

        currentDirector = director;

        requiredKey = key;

        actionObject = targetObject;
        activeQteUI = qteUiOverride != null ? qteUiOverride : qteUI;
        activeContinueUI = continueUiOverride;

        // Continue UI phải ẩn trong lúc đang chờ bấm phím.
        SetObjectVisible(activeContinueUI, false);

        currentAction = null;

        // Tìm function Execute()
        if (actionObject != null)
        {
            currentAction =
                actionObject.GetComponent<ITimelineInputAction>();

            if (currentAction == null)
            {
                Debug.LogError(
                    $"Object '{actionObject.name}' không implement ITimelineInputAction."
                );
            }
        }
        else
        {
            Debug.LogWarning(
                "Timeline Input Clip chưa có Action Object."
            );
        }

        waitingForInput = true;

        LockPlayerInput();
        SetObjectVisible(activeQteUI, true);

        // KHÔNG gọi currentDirector.Pause().
        // Pause() sẽ ngừng evaluate PlayableGraph, khiến camera shake nằm trong Timeline cũng đứng.
        // Ta chỉ khóa tốc độ Timeline = 0; graph vẫn evaluate mỗi frame nên shake vẫn hoạt động.
        FreezeTimelineGraph();
    }

    private void CompleteInput()
    {
        if (!waitingForInput)
            return;

        waitingForInput = false;

        // Tắt UI yêu cầu bấm phím.
        SetObjectVisible(activeQteUI, false);

        // Bật UI riêng ngay khi người chơi đã bấm đúng phím.
        // Sau khi bật, ClearCurrentInput chỉ bỏ reference chứ KHÔNG tắt GameObject này.
        SetObjectVisible(activeContinueUI, true);

        // Thực hiện chức năng của Object
        if (currentAction != null)
        {
            currentAction.Execute();
        }

        // Mở lại input Player
        UnlockPlayerInput();

        // Chạy Timeline tiếp bằng cách trả tốc độ PlayableGraph về như trước.
        // Director vốn vẫn đang Playing nên không cần Pause/Play lại.
        UnfreezeTimelineGraph();

        ClearCurrentInput();
    }


    private void FreezeTimelineGraph()
    {
        if (timelineGraphFrozen || currentDirector == null)
            return;

        PlayableGraph graph = currentDirector.playableGraph;
        if (!graph.IsValid())
        {
            Debug.LogWarning(
                "TimelineInputController: PlayableGraph không hợp lệ, không thể đóng băng Timeline."
            );
            return;
        }

        int rootCount = graph.GetRootPlayableCount();
        if (rootCount <= 0)
            return;

        frozenRootSpeeds = new double[rootCount];

        for (int i = 0; i < rootCount; i++)
        {
            Playable root = graph.GetRootPlayable(i);
            frozenRootSpeeds[i] = root.GetSpeed();
            root.SetSpeed(0d);
        }

        timelineGraphFrozen = true;
    }

    private void UnfreezeTimelineGraph()
    {
        if (!timelineGraphFrozen)
            return;

        if (currentDirector != null)
        {
            PlayableGraph graph = currentDirector.playableGraph;

            if (graph.IsValid())
            {
                int rootCount = graph.GetRootPlayableCount();
                int savedCount = frozenRootSpeeds != null ? frozenRootSpeeds.Length : 0;
                int restoreCount = Mathf.Min(rootCount, savedCount);

                for (int i = 0; i < restoreCount; i++)
                {
                    Playable root = graph.GetRootPlayable(i);
                    root.SetSpeed(frozenRootSpeeds[i]);
                }

                // Nếu graph có thêm root mới trong lúc đang chờ, cho chúng chạy bình thường.
                for (int i = restoreCount; i < rootCount; i++)
                {
                    graph.GetRootPlayable(i).SetSpeed(1d);
                }
            }
        }

        frozenRootSpeeds = null;
        timelineGraphFrozen = false;
    }

    // Có thể gọi hàm này từ Unity UI Button -> OnClick nếu muốn dùng nút UI
    // thay vì chỉ dùng requiredKey.
    public void ContinueTimeline()
    {
        if (!waitingForInput)
            return;

        CompleteInput();
    }

    private void LockPlayerInput()
    {
        if (inputLocked)
            return;

        inputLocked = true;

        ResolvePlayer();

        if (player == null)
        {
            Debug.LogWarning(
                "TimelineInputController: Chưa gán Player."
            );

            return;
        }

        IPlayerInputLock inputLock =
            player.GetComponent<IPlayerInputLock>();

        if (inputLock != null)
        {
            inputLock.SetInputLocked(true);
        }
        else
        {
            Debug.LogWarning(
                "Player không implement IPlayerInputLock."
            );
        }
    }

    private void UnlockPlayerInput()
    {
        if (!inputLocked)
            return;

        inputLocked = false;

        if (player == null)
            return;

        IPlayerInputLock inputLock =
            player.GetComponent<IPlayerInputLock>();

        if (inputLock != null)
        {
            inputLock.SetInputLocked(false);
        }
    }

    private void ResolvePlayer()
    {
        if (player != null)
        {
            return;
        }

        Player registeredPlayer = PlayerRegistry.Current;
        if (registeredPlayer != null)
        {
            player = registeredPlayer.gameObject;
            return;
        }

        Player scenePlayer = FindFirstObjectByType<Player>();
        if (scenePlayer != null)
        {
            player = scenePlayer.gameObject;
        }
    }

    private void ClearCurrentInput()
    {
        currentDirector = null;

        actionObject = null;

        currentAction = null;

        requiredKey = KeyCode.None;
        activeQteUI = null;

        // Chỉ bỏ reference, KHÔNG SetActive(false), để Continue UI tiếp tục chạy.
        activeContinueUI = null;
    }

    private static void SetObjectVisible(GameObject target, bool visible)
    {
        if (target != null)
        {
            target.SetActive(visible);
        }
    }

    public bool IsWaitingForInput()
    {
        return waitingForInput;
    }

    public KeyCode GetRequiredKey()
    {
        return requiredKey;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        UnfreezeTimelineGraph();
        UnlockPlayerInput();
        SetObjectVisible(qteUI, false);
        SetObjectVisible(activeQteUI, false);

        // Không tắt activeContinueUI ở đây vì UI này được yêu cầu tiếp tục chạy độc lập.
    }
}