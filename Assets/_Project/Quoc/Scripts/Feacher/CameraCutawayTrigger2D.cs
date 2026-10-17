using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class CameraCutawayTrigger2D : MonoBehaviour
{
    public enum TutorialPopupTiming
    {
        AfterAllCutscenesAndCameraReturn = 0, // Mặc định: Chờ toàn bộ cutscene và camera lia về xong hết mới hiện (Giá trị 0 an toàn tuyệt đối)
        ImmediatelyOnTrigger = 1,            // Hiện ngay khi chạm trigger
        AfterCameraMove = 2,                 // Hiện ngay sau khi camera di chuyển tới đích (trước cutscene)
        AfterFirstCutsceneOnly = 3           // Hiện sau cutscene nhưng trước khi camera lia về Player
    }

    [Header("References")]
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Transform playerReturnTarget;
    [SerializeField] private PlayableDirector cutsceneDirector;
    [SerializeField] private GameObject cutsceneObject;
    [Tooltip("Cutscene thứ 2 (nếu có chuỗi 2 cutscene liên tiếp)")]
    [SerializeField] private PlayableDirector secondaryCutsceneDirector;
    [Tooltip("GameObject Cutscene thứ 2 (nếu có)")]
    [SerializeField] private GameObject secondaryCutsceneObject;
    [SerializeField] private bool autoFindMissingReferences = true;

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool triggerOnce = true;

    [Header("Camera Move")]
    [Min(0f)]
    [SerializeField] private float moveDuration = 0.75f;
    [SerializeField] private AnimationCurve moveCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool useUnscaledTime;

    [Header("Camera Zoom")]
    [SerializeField] private bool changeZoom = true;
    [Min(0f)]
    [SerializeField] private float zoomDuration = 0.5f;
    [SerializeField] private float targetOrthographicSize = 7f;
    [SerializeField] private float targetFieldOfView = 45f;
    [SerializeField] private AnimationCurve zoomCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Tutorial Popup Panel")]
    [Tooltip("Panel UI popup hướng dẫn chơi (Kéo Prefab hoặc GameObject từ Canvas vào đây)")]
    [SerializeField] private GameObject tutorialPopupPanel;
    [Tooltip("Thời điểm hiển thị popup (Mặc định: AfterAllCutscenesAndCameraReturn - Hết toàn bộ cutscene và camera về Player mới hiện)")]
    [SerializeField] private TutorialPopupTiming popupShowTiming = TutorialPopupTiming.AfterAllCutscenesAndCameraReturn;
    [Tooltip("Thời gian trễ trước khi hiện popup sau khi cutscene kết thúc (giây)")]
    [Min(0f)]
    [SerializeField] private float delayBeforeShowPopup = 0.3f;
    [Tooltip("Tự động ẩn popup khi bắt đầu game")]
    [SerializeField] private bool hidePopupOnStart = true;
    [Tooltip("Nút UI đóng popup (nếu để trống, script sẽ tự tìm trên panel)")]
    [SerializeField] private Button closeButton;
    [Tooltip("Chờ người chơi bấm phím hoặc nút để đóng popup")]
    [SerializeField] private bool waitForPlayerInputToClose = true;
    [Tooltip("Phím chính để đóng popup")]
    [SerializeField] private KeyCode closeKey = KeyCode.Space;
    [Tooltip("Phím phụ để đóng popup")]
    [SerializeField] private KeyCode alternateCloseKey = KeyCode.E;
    [Tooltip("Tự động đóng sau số giây (0 = không tự đóng)")]
    [Min(0f)]
    [SerializeField] private float autoCloseDuration = 0f;
    [Tooltip("Khóa điều khiển Player trong lúc popup đang mở (Mặc định: false - Người chơi vẫn điều khiển và chơi minigame bình thường khi popup hiện)")]
    [SerializeField] private bool lockPlayerWhilePopupOpen = false;
    [Tooltip("Tự động tắt popup khi minigame Plinko hoàn thành (đá phá vỡ đích)")]
    [SerializeField] private bool autoCloseOnMinigameComplete = true;
    [Tooltip("Tạm dừng thời gian game (Time.timeScale = 0) khi đang mở popup")]
    [SerializeField] private bool pauseGameTimeWhilePopupOpen = false;
    [Tooltip("Bật hiệu ứng phóng to mượt mà cho popup")]
    [SerializeField] private bool animatePopup = true;
    [Min(0.01f)]
    [SerializeField] private float popupAnimationDuration = 0.25f;

    [Header("Tutorial Content Override (Tùy chọn)")]
    [Tooltip("Tiêu đề tùy chỉnh cho vị trí này (để trống nếu dùng mặc định của UI)")]
    [SerializeField] private string customTitle;
    [TextArea(2, 6)]
    [Tooltip("Nội dung hướng dẫn chi tiết cho vị trí này")]
    [SerializeField] private string customDescription;
    [Tooltip("Gợi ý phím bấm")]
    [SerializeField] private string customKeyHint;
    [Tooltip("Ảnh icon / minh họa")]
    [SerializeField] private Sprite customIcon;

    [Header("Restore")]
    [SerializeField] private bool restoreCameraFollowOnDisable;
    [SerializeField] private bool lockPlayerDuringCutscene = true;
    [SerializeField] private bool forceUnlockPlayerOnFinish = true;
    [SerializeField] private bool playCutsceneAfterCameraMove = true;
    [SerializeField] private bool rewindCutsceneBeforePlay = true;
    [SerializeField] private bool deactivateCutsceneObjectWhenFinished = true;
    [SerializeField] private GameObject objectToShowAfterCutscene;
    [SerializeField] private bool hideObjectUntilCutsceneFinished = true;
    [SerializeField] private bool returnToPlayerAfterIceGroundBreak = true;
    [SerializeField] private string iceGroundTag = "IceGround";
    [SerializeField] private string alternateIceGroundTag = "iceground";
    [SerializeField] private bool hideIceGroundObjectsAfterBreak = true;
    [SerializeField] private bool ignoreIceGroundObjectsWithAnimator = true;
    [Min(0f)]
    [SerializeField] private float returnDelayAfterIceGroundBreak = 2f;
    [Min(0f)]
    [SerializeField] private float returnMoveDuration = 0.5f;
    [SerializeField] private AnimationCurve returnCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private static readonly System.Collections.Generic.List<CameraCutawayTrigger2D> activeCutawayTriggers = new System.Collections.Generic.List<CameraCutawayTrigger2D>();

    private Collider2D triggerCollider;
    private Coroutine cutawayCoroutine;
    private Transform previousFollow;
    private bool previousFollowCached;
    private LensSettings previousLens;
    private bool previousLensCached;
    private Player cachedPlayer;
    private GameObject followProxyObject;
    private Transform followProxy;
    private bool hasTriggered;

    private RectTransform popupRectTransform;
    private CanvasGroup popupCanvasGroup;
    private TutorialPopupUI cachedPopupUI;
    private bool isWaitingForPopupInput;
    private bool isPopupOpen;
    private bool wasPausedByPopup;
    private bool popupInitialized;
    private bool hasShownTutorial = false;

    public static void CloseAllActivePopups()
    {
        for (int i = activeCutawayTriggers.Count - 1; i >= 0; i--)
        {
            if (activeCutawayTriggers[i] != null)
            {
                activeCutawayTriggers[i].DismissPopup();
            }
        }
    }

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        EnsureTriggerCollider();
        ResolveReferences();
        PreparePostCutsceneObject();
        EnsurePopupInstance();
    }

    private void Start()
    {
        // Đảm bảo tuyệt đối popup không được bật lúc bắt đầu màn chơi
        if (tutorialPopupPanel != null && !isPopupOpen)
        {
            if (popupCanvasGroup != null) popupCanvasGroup.alpha = 0f;
            tutorialPopupPanel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (!activeCutawayTriggers.Contains(this))
        {
            activeCutawayTriggers.Add(this);
        }

        PlinkoBreakTarget.OnPlinkoCompleted -= OnPlinkoCompletedHandler;
        PlinkoBreakTarget.OnPlinkoCompleted += OnPlinkoCompletedHandler;

        EnsureTriggerCollider();
        ResolveReferences();
        PreparePostCutsceneObject();
        EnsurePopupInstance();
    }

    private void OnDisable()
    {
        activeCutawayTriggers.Remove(this);
        PlinkoBreakTarget.OnPlinkoCompleted -= OnPlinkoCompletedHandler;

        if (cutawayCoroutine != null)
        {
            StopCoroutine(cutawayCoroutine);
            cutawayCoroutine = null;
        }

        if (wasPausedByPopup)
        {
            Time.timeScale = 1f;
            wasPausedByPopup = false;
        }

        UnbindCloseButtons();

        if (cinemachineCamera != null
            && restoreCameraFollowOnDisable
            && previousFollowCached)
        {
            cinemachineCamera.Follow = previousFollow;
        }

        RestorePreviousLens();
        UnlockPlayerControl();

        DestroyFollowProxy();
    }

    private void OnPlinkoCompletedHandler()
    {
        if (autoCloseOnMinigameComplete)
        {
            DismissPopup();
        }
    }

    private void EnsurePopupInstance()
    {
        if (popupInitialized && tutorialPopupPanel != null)
        {
            if (!isPopupOpen)
            {
                if (popupCanvasGroup != null) popupCanvasGroup.alpha = 0f;
                tutorialPopupPanel.SetActive(false);
            }
            return;
        }

        if (tutorialPopupPanel == null && autoFindMissingReferences)
        {
            TutorialPopupUI foundUI = FindAnyObjectByType<TutorialPopupUI>(FindObjectsInactive.Include);
            if (foundUI != null)
            {
                tutorialPopupPanel = foundUI.gameObject;
            }
        }

        if (tutorialPopupPanel == null)
        {
            return;
        }

        // Nếu là Prefab asset từ Project chưa Instantiate trong Scene
        if (!tutorialPopupPanel.scene.IsValid())
        {
            Canvas targetCanvas = FindAnyObjectByType<Canvas>();
            tutorialPopupPanel = Instantiate(tutorialPopupPanel, targetCanvas != null ? targetCanvas.transform : null);
            tutorialPopupPanel.SetActive(false);
        }

        popupRectTransform = tutorialPopupPanel.GetComponent<RectTransform>();
        popupCanvasGroup = tutorialPopupPanel.GetComponent<CanvasGroup>();
        if (popupCanvasGroup == null)
        {
            popupCanvasGroup = tutorialPopupPanel.AddComponent<CanvasGroup>();
        }

        // Luôn đảm bảo popup bị ẩn hoàn toàn và alpha = 0 khi khởi tạo
        if (!isPopupOpen)
        {
            popupCanvasGroup.alpha = 0f;
            tutorialPopupPanel.SetActive(false);
        }

        if (tutorialPopupPanel.TryGetComponent(out cachedPopupUI))
        {
            ApplyCustomPopupContent();
            if (closeButton == null && cachedPopupUI.CloseButton != null)
            {
                closeButton = cachedPopupUI.CloseButton;
            }

            if (cachedPopupUI.ConfirmButton != null)
            {
                cachedPopupUI.ConfirmButton.onClick.RemoveListener(CloseTutorialPopup);
                cachedPopupUI.ConfirmButton.onClick.AddListener(CloseTutorialPopup);
            }
        }

        if (closeButton == null)
        {
            closeButton = tutorialPopupPanel.GetComponentInChildren<Button>(true);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseTutorialPopup);
            closeButton.onClick.AddListener(CloseTutorialPopup);
        }

        popupInitialized = true;
    }

    private void ApplyCustomPopupContent()
    {
        if (cachedPopupUI == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(customTitle)
            || !string.IsNullOrEmpty(customDescription)
            || customIcon != null
            || !string.IsNullOrEmpty(customKeyHint))
        {
            cachedPopupUI.SetContent(customTitle, customDescription, customIcon, customKeyHint);
        }
    }

    private void UnbindCloseButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseTutorialPopup);
        }

        if (cachedPopupUI != null && cachedPopupUI.ConfirmButton != null)
        {
            cachedPopupUI.ConfirmButton.onClick.RemoveListener(CloseTutorialPopup);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (cutawayCoroutine != null || isPopupOpen || (hasTriggered && triggerOnce))
        {
            return;
        }

        if (!IsPlayerCollider(other))
        {
            return;
        }

        Player enteredPlayer = other.GetComponentInParent<Player>();
        if (enteredPlayer != null)
        {
            cachedPlayer = enteredPlayer;
            if (playerReturnTarget == null)
            {
                playerReturnTarget = enteredPlayer.transform;
            }
        }

        ResolveReferences();
        EnsurePopupInstance();

        // Đảm bảo không bị chớp bật popup khi vừa chạm trigger
        if (tutorialPopupPanel != null)
        {
            if (popupCanvasGroup != null) popupCanvasGroup.alpha = 0f;
            tutorialPopupPanel.SetActive(false);
        }

        hasTriggered = true;
        cutawayCoroutine = StartCoroutine(RunCutaway());
    }

    private IEnumerator RunCutaway()
    {
        LockPlayerControl();
        CachePreviousFollow();
        CachePreviousLens();

        // Đảm bảo tuyệt đối popup không được bật lúc bắt đầu cutscene
        if (tutorialPopupPanel != null && !isPopupOpen)
        {
            if (popupCanvasGroup != null) popupCanvasGroup.alpha = 0f;
            tutorialPopupPanel.SetActive(false);
        }

        bool hasCameraSetup = cinemachineCamera != null && cameraTarget != null;

        // Kiểm tra nếu minigame đã xong trước đó thì không hiện popup
        if (autoCloseOnMinigameComplete && PlinkoBreakTarget.GlobalHasBroken)
        {
            UnlockPlayerControl();
            RestorePreviousLens();
            yield break;
        }

        // Chỉ hiện ngay nếu người dùng cố ý cấu hình ImmediatelyOnTrigger
        if (popupShowTiming == TutorialPopupTiming.ImmediatelyOnTrigger)
        {
            yield return ShowTutorialPopupAndWait();
        }

        if (hasCameraSetup)
        {
            yield return MoveCamera();

            if (changeZoom)
            {
                yield return ZoomCamera();
            }

            cinemachineCamera.Follow = cameraTarget;

            if (popupShowTiming == TutorialPopupTiming.AfterCameraMove)
            {
                if (!autoCloseOnMinigameComplete || !PlinkoBreakTarget.GlobalHasBroken)
                {
                    yield return ShowTutorialPopupAndWait();
                }
            }

            // 1. Chạy Cutscene 1 (Timeline / Animator)
            if (playCutsceneAfterCameraMove)
            {
                yield return PlayCutscene();
            }

            // 2. Chạy Cutscene 2 (nếu có)
            if (secondaryCutsceneDirector != null || secondaryCutsceneObject != null)
            {
                yield return PlaySecondaryCutscene();
            }

            // 3. Chờ tất cả PlayableDirector khác đang chạy trong scene (nếu có) kết thúc
            yield return WaitForAllActiveTimelinesInScene();

            if (popupShowTiming == TutorialPopupTiming.AfterFirstCutsceneOnly)
            {
                if (!autoCloseOnMinigameComplete || !PlinkoBreakTarget.GlobalHasBroken)
                {
                    yield return ShowTutorialPopupAndWait();
                }
            }

            // 4. Chờ vỡ băng / đá lăn / animation tương tác
            if (returnToPlayerAfterIceGroundBreak)
            {
                yield return WaitForIceGroundBreak();
                HideIceGroundObjects();

                if (returnDelayAfterIceGroundBreak > 0f)
                {
                    yield return new WaitForSeconds(returnDelayAfterIceGroundBreak);
                }

                yield return ReturnCameraToPlayer();
                UnlockPlayerControl();
            }
            else
            {
                RestorePreviousLens();
                UnlockPlayerControl();
            }

            // 5. TOÀN BỘ TẤT CẢ CUTSCENE & CAMERA LIA VỀ XONG HẾT -> MỚI HIỆN POPUP (MẶC ĐỊNH)
            if (popupShowTiming == TutorialPopupTiming.AfterAllCutscenesAndCameraReturn)
            {
                if (!autoCloseOnMinigameComplete || !PlinkoBreakTarget.GlobalHasBroken)
                {
                    if (delayBeforeShowPopup > 0f)
                    {
                        yield return new WaitForSeconds(delayBeforeShowPopup);
                    }
                    yield return ShowTutorialPopupAndWait();
                }
            }
        }
        else
        {
            if (playCutsceneAfterCameraMove)
            {
                yield return PlayCutscene();
            }

            if (secondaryCutsceneDirector != null || secondaryCutsceneObject != null)
            {
                yield return PlaySecondaryCutscene();
            }

            yield return WaitForAllActiveTimelinesInScene();

            if (!autoCloseOnMinigameComplete || !PlinkoBreakTarget.GlobalHasBroken)
            {
                if (delayBeforeShowPopup > 0f)
                {
                    yield return new WaitForSeconds(delayBeforeShowPopup);
                }

                // Toàn bộ cutscene xong -> Hiện popup
                yield return ShowTutorialPopupAndWait();
            }
        }

        // Mở khóa điều khiển Player sau khi tất cả đã hoàn thành
        UnlockPlayerControl();
        RestorePreviousLens();
        yield return null;
        RestorePreviousLens();

        cutawayCoroutine = null;

        if (triggerOnce)
        {
            enabled = false;
        }
    }

    private IEnumerator WaitForAllActiveTimelinesInScene()
    {
        // Kiểm tra xem trong scene có PlayableDirector nào đang chạy không
        PlayableDirector[] allDirectors = FindObjectsByType<PlayableDirector>(FindObjectsSortMode.None);
        if (allDirectors == null || allDirectors.Length == 0)
        {
            yield break;
        }

        bool anyPlaying = true;
        while (anyPlaying)
        {
            anyPlaying = false;
            foreach (PlayableDirector dir in allDirectors)
            {
                if (dir != null && dir.gameObject.activeInHierarchy && dir.state == PlayState.Playing)
                {
                    anyPlaying = true;
                    break;
                }
            }

            if (anyPlaying)
            {
                yield return null;
            }
        }
    }

    public void CloseTutorialPopup()
    {
        isWaitingForPopupInput = false;
    }

    public void DismissPopup()
    {
        isWaitingForPopupInput = false;
        isPopupOpen = false;

        if (tutorialPopupPanel != null)
        {
            if (popupCanvasGroup != null) popupCanvasGroup.alpha = 0f;
            tutorialPopupPanel.SetActive(false);
        }

        if (wasPausedByPopup)
        {
            Time.timeScale = 1f;
            wasPausedByPopup = false;
        }

        UnlockPlayerControl();
    }

    private IEnumerator ShowTutorialPopupAndWait()
    {
        // 1. Chỉ hiển thị đúng 1 lần duy nhất
        if (hasShownTutorial)
        {
            yield break;
        }

        EnsurePopupInstance();

        if (tutorialPopupPanel == null)
        {
            yield break;
        }

        if (autoCloseOnMinigameComplete && PlinkoBreakTarget.GlobalHasBroken)
        {
            DismissPopup();
            yield break;
        }

        hasShownTutorial = true;
        ApplyCustomPopupContent();

        // 2. Bỏ phần tắt đi: Ẩn các nút đóng để người chơi không phải bấm
        if (closeButton != null) closeButton.gameObject.SetActive(false);
        if (cachedPopupUI != null)
        {
            if (cachedPopupUI.CloseButton != null) cachedPopupUI.CloseButton.gameObject.SetActive(false);
            if (cachedPopupUI.ConfirmButton != null) cachedPopupUI.ConfirmButton.gameObject.SetActive(false);
        }

        isPopupOpen = true;
        isWaitingForPopupInput = true;

        // Luôn mở khóa người chơi để họ vừa xem popup hướng dẫn vừa chơi được minigame
        UnlockPlayerControl();

        if (pauseGameTimeWhilePopupOpen)
        {
            wasPausedByPopup = true;
            Time.timeScale = 0f;
        }

        // Đảm bảo alpha = 0 trước khi active để không bị giật nhấp nháy
        if (popupCanvasGroup != null)
        {
            popupCanvasGroup.alpha = 0f;
        }

        tutorialPopupPanel.transform.SetAsLastSibling();
        tutorialPopupPanel.SetActive(true);

        if (popupRectTransform != null)
        {
            popupRectTransform.anchoredPosition = Vector2.zero;
        }

        if (animatePopup)
        {
            yield return AnimatePopupIn();
        }
        else
        {
            if (popupRectTransform != null) popupRectTransform.localScale = Vector3.one;
            if (popupCanvasGroup != null) popupCanvasGroup.alpha = 1f;
        }

        float timer = autoCloseDuration;
        while (isWaitingForPopupInput)
        {
            // 3. Khi chơi xong nó tự ẩn đi:
            if (autoCloseOnMinigameComplete && PlinkoBreakTarget.GlobalHasBroken)
            {
                isWaitingForPopupInput = false;
                break;
            }

            // Vẫn giữ lại phần đóng bằng phím tắt phòng khi người chơi muốn chủ động đóng sớm
            if (waitForPlayerInputToClose)
            {
                if (Input.GetKeyDown(closeKey)
                    || Input.GetKeyDown(alternateCloseKey)
                    || Input.GetKeyDown(KeyCode.Return)
                    || Input.GetKeyDown(KeyCode.Escape))
                {
                    isWaitingForPopupInput = false;
                    break;
                }
            }

            if (autoCloseDuration > 0f)
            {
                timer -= Time.unscaledDeltaTime;
                if (timer <= 0f)
                {
                    isWaitingForPopupInput = false;
                    break;
                }
            }

            yield return null;
        }

        if (animatePopup)
        {
            yield return AnimatePopupOut();
        }

        if (popupCanvasGroup != null)
        {
            popupCanvasGroup.alpha = 0f;
        }

        tutorialPopupPanel.SetActive(false);

        if (wasPausedByPopup)
        {
            Time.timeScale = 1f;
            wasPausedByPopup = false;
        }

        UnlockPlayerControl();
        isPopupOpen = false;
    }

    private IEnumerator AnimatePopupIn()
    {
        float duration = Mathf.Max(0.01f, popupAnimationDuration);
        float elapsed = 0f;

        Vector3 startScale = Vector3.one * 0.7f;
        Vector3 targetScale = Vector3.one;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scaleT = 1f + 0.15f * Mathf.Sin(t * Mathf.PI * 1.5f) * (1f - t);

            if (popupRectTransform != null)
            {
                popupRectTransform.localScale = Vector3.LerpUnclamped(startScale, targetScale, t) * (t < 1f ? scaleT : 1f);
            }

            if (popupCanvasGroup != null)
            {
                popupCanvasGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
            }

            yield return null;
        }

        if (popupRectTransform != null) popupRectTransform.localScale = Vector3.one;
        if (popupCanvasGroup != null) popupCanvasGroup.alpha = 1f;
    }

    private IEnumerator AnimatePopupOut()
    {
        float duration = Mathf.Max(0.01f, popupAnimationDuration * 0.6f);
        float elapsed = 0f;

        Vector3 startScale = Vector3.one;
        Vector3 endScale = Vector3.one * 0.8f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (popupRectTransform != null)
            {
                popupRectTransform.localScale = Vector3.Lerp(startScale, endScale, t);
            }

            if (popupCanvasGroup != null)
            {
                popupCanvasGroup.alpha = 1f - t;
            }

            yield return null;
        }

        if (popupRectTransform != null) popupRectTransform.localScale = Vector3.one;
        if (popupCanvasGroup != null) popupCanvasGroup.alpha = 1f;
    }

    private IEnumerator MoveCamera()
    {
        if (cinemachineCamera == null || cameraTarget == null)
        {
            yield break;
        }

        float duration = Mathf.Max(0f, moveDuration);
        if (duration <= 0f)
        {
            cinemachineCamera.Follow = cameraTarget;
            yield break;
        }

        EnsureFollowProxy();

        Vector3 startPosition = GetCurrentFollowPosition();
        Vector3 endPosition = cameraTarget.position;
        Quaternion startRotation = cinemachineCamera.transform.rotation;
        Quaternion endRotation = cameraTarget.rotation;

        followProxy.position = startPosition;
        followProxy.rotation = Quaternion.identity;
        cinemachineCamera.Follow = followProxy;
        cinemachineCamera.ForceCameraPosition(startPosition, startRotation);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += GetDeltaTime();
            float t = EvaluateCurve(moveCurve, elapsed, duration);
            Vector3 cameraPosition = Vector3.LerpUnclamped(startPosition, endPosition, t);
            Quaternion cameraRotation = Quaternion.Slerp(startRotation, endRotation, t);

            followProxy.position = cameraPosition;
            cinemachineCamera.ForceCameraPosition(cameraPosition, cameraRotation);
            yield return null;
        }

        followProxy.position = endPosition;
        cinemachineCamera.ForceCameraPosition(endPosition, endRotation);
        DestroyFollowProxy();
    }

    private IEnumerator ReturnCameraToPlayer()
    {
        ResolveReferences();

        Transform returnTarget = ResolveReturnTarget();
        if (cinemachineCamera == null || returnTarget == null)
        {
            RestorePreviousLens();
            yield break;
        }

        float duration = Mathf.Max(0f, returnMoveDuration);
        if (duration <= 0f)
        {
            cinemachineCamera.Follow = returnTarget;
            RestorePreviousLens();
            yield break;
        }

        EnsureFollowProxy();

        Vector3 startPosition = GetCurrentFollowPosition();
        Vector3 endPosition = returnTarget.position;
        Quaternion startRotation = cinemachineCamera.transform.rotation;
        Quaternion endRotation = returnTarget.rotation;
        LensSettings startLens = cinemachineCamera.Lens;

        followProxy.position = startPosition;
        followProxy.rotation = Quaternion.identity;
        cinemachineCamera.Follow = followProxy;
        cinemachineCamera.ForceCameraPosition(startPosition, startRotation);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += GetDeltaTime();
            float t = EvaluateCurve(returnCurve, elapsed, duration);
            Vector3 cameraPosition = Vector3.LerpUnclamped(startPosition, endPosition, t);
            Quaternion cameraRotation = Quaternion.Slerp(startRotation, endRotation, t);

            followProxy.position = cameraPosition;
            cinemachineCamera.ForceCameraPosition(cameraPosition, cameraRotation);
            ApplyLensRestore(startLens, t);
            yield return null;
        }

        followProxy.position = endPosition;
        cinemachineCamera.ForceCameraPosition(endPosition, endRotation);
        cinemachineCamera.Follow = returnTarget;
        RestorePreviousLens();
        cinemachineCamera.PreviousStateIsValid = false;
        DestroyFollowProxy();
    }

    private IEnumerator ZoomCamera()
    {
        if (cinemachineCamera == null)
        {
            yield break;
        }

        float duration = Mathf.Max(0f, zoomDuration);
        if (duration <= 0f)
        {
            ApplyFinalZoom();
            yield break;
        }

        LensSettings startLens = cinemachineCamera.Lens;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += GetDeltaTime();
            ApplyZoom(startLens, EvaluateCurve(zoomCurve, elapsed, duration));
            yield return null;
        }

        ApplyFinalZoom();
    }

    private void ApplyFinalZoom()
    {
        if (cinemachineCamera == null)
        {
            return;
        }

        ApplyZoom(cinemachineCamera.Lens, 1f);
    }

    private void ApplyZoom(LensSettings startLens, float t)
    {
        if (cinemachineCamera == null)
        {
            return;
        }

        LensSettings lens = startLens;
        if (lens.Orthographic)
        {
            lens.OrthographicSize = Mathf.Lerp(
                startLens.OrthographicSize,
                targetOrthographicSize,
                t);
        }
        else
        {
            lens.FieldOfView = Mathf.Lerp(
                startLens.FieldOfView,
                targetFieldOfView,
                t);
        }

        cinemachineCamera.Lens = lens;
    }

    private void ResolveReferences()
    {
        if (cinemachineCamera == null && autoFindMissingReferences)
        {
            cinemachineCamera = FindAnyObjectByType<CinemachineCamera>();
        }

        if (playerReturnTarget == null && autoFindMissingReferences)
        {
            cachedPlayer = FindAnyObjectByType<Player>();
            if (cachedPlayer != null)
            {
                playerReturnTarget = cachedPlayer.transform;
            }
        }
        else if (cachedPlayer == null && playerReturnTarget != null)
        {
            cachedPlayer = playerReturnTarget.GetComponentInParent<Player>();
        }

        if (cutsceneDirector == null && cutsceneObject != null)
        {
            cutsceneDirector = cutsceneObject.GetComponent<PlayableDirector>();
            if (cutsceneDirector == null)
            {
                cutsceneDirector = cutsceneObject.GetComponentInChildren<PlayableDirector>();
            }
        }

        if (cutsceneDirector == null && autoFindMissingReferences)
        {
            cutsceneDirector = GetComponent<PlayableDirector>();
            if (cutsceneDirector == null)
            {
                cutsceneDirector = GetComponentInChildren<PlayableDirector>();
            }
        }

        if (secondaryCutsceneDirector == null && secondaryCutsceneObject != null)
        {
            secondaryCutsceneDirector = secondaryCutsceneObject.GetComponent<PlayableDirector>();
            if (secondaryCutsceneDirector == null)
            {
                secondaryCutsceneDirector = secondaryCutsceneObject.GetComponentInChildren<PlayableDirector>();
            }
        }
    }

    private IEnumerator PlayCutscene()
    {
        if (cutsceneObject != null && !cutsceneObject.activeSelf)
        {
            cutsceneObject.SetActive(true);
        }

        if (cutsceneDirector != null)
        {
            if (rewindCutsceneBeforePlay)
            {
                cutsceneDirector.Stop();
                cutsceneDirector.time = 0;
                cutsceneDirector.Evaluate();
            }

            cutsceneDirector.Play();
            yield return null;

            double duration = cutsceneDirector.duration;
            while (cutsceneDirector != null)
            {
                if (!cutsceneDirector.gameObject.activeInHierarchy || cutsceneDirector.state != PlayState.Playing)
                {
                    break;
                }

                if (duration > 0.05d && cutsceneDirector.time >= duration - 0.02d)
                {
                    break;
                }

                yield return null;
            }
        }
        else if (cutsceneObject != null)
        {
            Animator anim = cutsceneObject.GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                anim.Play(0, 0, 0f);
                anim.Update(0f);
                yield return null;
                while (anim != null && (anim.IsInTransition(0) || anim.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f))
                {
                    yield return null;
                }
            }
        }

        FinishCutscenePlayback();
    }

    private IEnumerator PlaySecondaryCutscene()
    {
        if (secondaryCutsceneObject != null && !secondaryCutsceneObject.activeSelf)
        {
            secondaryCutsceneObject.SetActive(true);
        }

        if (secondaryCutsceneDirector != null)
        {
            if (rewindCutsceneBeforePlay)
            {
                secondaryCutsceneDirector.Stop();
                secondaryCutsceneDirector.time = 0;
                secondaryCutsceneDirector.Evaluate();
            }

            secondaryCutsceneDirector.Play();
            yield return null;

            double duration = secondaryCutsceneDirector.duration;
            while (secondaryCutsceneDirector != null)
            {
                if (!secondaryCutsceneDirector.gameObject.activeInHierarchy || secondaryCutsceneDirector.state != PlayState.Playing)
                {
                    break;
                }

                if (duration > 0.05d && secondaryCutsceneDirector.time >= duration - 0.02d)
                {
                    break;
                }

                yield return null;
            }
        }
        else if (secondaryCutsceneObject != null)
        {
            Animator anim = secondaryCutsceneObject.GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                anim.Play(0, 0, 0f);
                anim.Update(0f);
                yield return null;
                while (anim != null && (anim.IsInTransition(0) || anim.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f))
                {
                    yield return null;
                }
            }
        }
    }

    private void FinishCutscenePlayback()
    {
        if (deactivateCutsceneObjectWhenFinished && cutsceneObject != null)
        {
            cutsceneObject.SetActive(false);
        }

        if (objectToShowAfterCutscene != null)
        {
            objectToShowAfterCutscene.SetActive(true);
        }
    }

    private void PreparePostCutsceneObject()
    {
        if (hideObjectUntilCutsceneFinished && objectToShowAfterCutscene != null && !hasTriggered)
        {
            objectToShowAfterCutscene.SetActive(false);
        }
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(playerTag)
            && (other.CompareTag(playerTag) || other.GetComponentInParent<Player>() != null))
        {
            return true;
        }

        return string.IsNullOrEmpty(playerTag)
               && other.GetComponentInParent<Player>() != null;
    }

    private void CachePreviousFollow()
    {
        if (cinemachineCamera == null || previousFollowCached)
        {
            return;
        }

        previousFollow = cinemachineCamera.Follow;
        previousFollowCached = true;
    }

    private void CachePreviousLens()
    {
        if (cinemachineCamera == null || previousLensCached)
        {
            return;
        }

        previousLens = cinemachineCamera.Lens;
        previousLensCached = true;
    }

    private void RestorePreviousLens()
    {
        if (cinemachineCamera == null || !previousLensCached)
        {
            return;
        }

        cinemachineCamera.Lens = previousLens;
    }

    private void UnlockPlayerControl()
    {
        if (!forceUnlockPlayerOnFinish && !lockPlayerDuringCutscene)
        {
            return;
        }

        if (cachedPlayer == null && playerReturnTarget != null)
        {
            cachedPlayer = playerReturnTarget.GetComponentInParent<Player>();
        }

        if (cachedPlayer == null)
        {
            cachedPlayer = FindAnyObjectByType<Player>();
        }

        if (cachedPlayer != null)
        {
            cachedPlayer.SetControlLocked(false);
        }
    }

    private void LockPlayerControl()
    {
        if (!lockPlayerDuringCutscene)
        {
            return;
        }

        if (cachedPlayer == null && playerReturnTarget != null)
        {
            cachedPlayer = playerReturnTarget.GetComponentInParent<Player>();
        }

        if (cachedPlayer == null)
        {
            cachedPlayer = FindAnyObjectByType<Player>();
        }

        if (cachedPlayer != null)
        {
            cachedPlayer.SetControlLocked(true);
        }
    }

    private void ApplyLensRestore(LensSettings startLens, float t)
    {
        if (cinemachineCamera == null || !previousLensCached)
        {
            return;
        }

        LensSettings lens = startLens;
        if (lens.Orthographic)
        {
            lens.OrthographicSize = Mathf.Lerp(
                startLens.OrthographicSize,
                previousLens.OrthographicSize,
                t);
        }
        else
        {
            lens.FieldOfView = Mathf.Lerp(
                startLens.FieldOfView,
                previousLens.FieldOfView,
                t);
        }

        cinemachineCamera.Lens = lens;
    }

    private Transform ResolveReturnTarget()
    {
        if (playerReturnTarget != null)
        {
            return playerReturnTarget;
        }

        if (previousFollowCached && previousFollow != null)
        {
            return previousFollow;
        }

        return null;
    }

    private IEnumerator WaitForIceGroundBreak()
    {
        while (!AreIceGroundObjectsBroken())
        {
            yield return null;
        }
    }

    private bool AreIceGroundObjectsBroken()
    {
        if (!AreTaggedIceGroundObjectsBroken(iceGroundTag))
        {
            return false;
        }

        if (!string.Equals(alternateIceGroundTag, iceGroundTag, System.StringComparison.Ordinal)
            && !AreTaggedIceGroundObjectsBroken(alternateIceGroundTag))
        {
            return false;
        }

        return true;
    }

    private void HideIceGroundObjects()
    {
        if (!hideIceGroundObjectsAfterBreak)
        {
            return;
        }

        HideTaggedIceGroundObjects(iceGroundTag);

        if (!string.Equals(alternateIceGroundTag, iceGroundTag, System.StringComparison.Ordinal))
        {
            HideTaggedIceGroundObjects(alternateIceGroundTag);
        }
    }

    private void HideTaggedIceGroundObjects(string targetTag)
    {
        if (string.IsNullOrEmpty(targetTag))
        {
            return;
        }

        GameObject[] objects;
        try
        {
            objects = GameObject.FindGameObjectsWithTag(targetTag);
        }
        catch (UnityException)
        {
            return;
        }

        foreach (GameObject targetObject in objects)
        {
            if (targetObject != null)
            {
                if (ShouldIgnoreIceGroundObject(targetObject))
                {
                    continue;
                }

                HideRenderersAndColliders(targetObject);
            }
        }
    }

    private static void HideRenderersAndColliders(GameObject targetObject)
    {
        Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer != null)
            {
                targetRenderer.enabled = false;
            }
        }

        Collider2D[] colliders = targetObject.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D targetCollider in colliders)
        {
            if (targetCollider != null)
            {
                targetCollider.enabled = false;
            }
        }
    }

    private bool AreTaggedIceGroundObjectsBroken(string targetTag)
    {
        if (string.IsNullOrEmpty(targetTag))
        {
            return true;
        }

        GameObject[] objects;
        try
        {
            objects = GameObject.FindGameObjectsWithTag(targetTag);
        }
        catch (UnityException)
        {
            return true;
        }

        foreach (GameObject targetObject in objects)
        {
            if (targetObject == null)
            {
                continue;
            }

            if (ShouldIgnoreIceGroundObject(targetObject))
            {
                continue;
            }

            if (HasActiveRenderer(targetObject))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasActiveRenderer(GameObject targetObject)
    {
        Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer != null
                && targetRenderer.gameObject.activeInHierarchy
                && targetRenderer.enabled)
            {
                return true;
            }
        }

        return false;
    }

    private bool ShouldIgnoreIceGroundObject(GameObject targetObject)
    {
        if (!ignoreIceGroundObjectsWithAnimator || targetObject == null)
        {
            return false;
        }

        return targetObject.GetComponentInChildren<Animator>(true) != null;
    }

    private Vector3 GetCurrentFollowPosition()
    {
        if (cinemachineCamera.PreviousStateIsValid)
        {
            return cinemachineCamera.State.GetFinalPosition();
        }

        return cinemachineCamera.transform.position;
    }

    private float GetDeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private static float EvaluateCurve(AnimationCurve curve, float elapsed, float duration)
    {
        if (duration <= 0f)
        {
            return 1f;
        }

        float t = Mathf.Clamp01(elapsed / duration);
        return curve != null ? curve.Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);
    }

    private void EnsureFollowProxy()
    {
        if (followProxy != null)
        {
            return;
        }

        followProxyObject = new GameObject("Camera Cutaway Follow Proxy");
        followProxyObject.hideFlags = HideFlags.HideAndDontSave;
        followProxy = followProxyObject.transform;
    }

    private void DestroyFollowProxy()
    {
        if (followProxyObject == null)
        {
            followProxy = null;
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(followProxyObject);
        }
        else
        {
            DestroyImmediate(followProxyObject);
        }

        followProxyObject = null;
        followProxy = null;
    }

    private void EnsureTriggerCollider()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider2D>();
        }

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void OnValidate()
    {
        triggerCollider = GetComponent<Collider2D>();
        EnsureTriggerCollider();
    }
}
