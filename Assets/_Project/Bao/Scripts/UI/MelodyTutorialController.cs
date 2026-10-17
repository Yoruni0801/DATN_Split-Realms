using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý hiển thị UI Hướng dẫn chơi cho Minigame Âm nhạc (MelodyManager).
/// Tự động lắng nghe sự kiện OnPlayerTurnStarted để bật popup hướng dẫn người chơi
/// cách bấm phím 1-6 hoặc nhảy lên các cột nốt nhạc để gảy lại bài hát.
/// </summary>
[DisallowMultipleComponent]
public class MelodyTutorialController : MonoBehaviour
{
    [Header("Manager & UI References")]
    [Tooltip("Tham chiếu tới MelodyManager (nếu để trống script tự tìm trên cùng GameObject hoặc Scene)")]
    [SerializeField] private MelodyManager melodyManager;
    [Tooltip("Panel UI Popup hướng dẫn (Kéo Prefab MelodyTutorialPopup_GUIPro hoặc đối tượng trên Canvas vào đây)")]
    [SerializeField] private GameObject tutorialPopupPanel;
    [Tooltip("Tự động tìm kiếm Popup trong scene nếu để trống")]
    [SerializeField] private bool autoFindMissingPopup = true;

    [Header("Settings")]
    [Tooltip("Chỉ hiển thị hướng dẫn 1 lần duy nhất trong màn chơi (tránh hiện lại khi người chơi đánh sai phải thử lại)")]
    [SerializeField] private bool showOnlyOnce = true;
    [Tooltip("Khóa di chuyển của Player khi đang mở popup (Mặc định: false để người chơi vẫn chơi bình thường khi popup hiện)")]
    [SerializeField] private bool lockPlayerWhileOpen = false;
    [Tooltip("Tự động tắt hoàn toàn popup khi hoàn thành minigame âm nhạc")]
    [SerializeField] private bool autoCloseOnWin = true;
    [Tooltip("Tạm dừng thời gian game (Time.timeScale = 0) khi mở popup")]
    [SerializeField] private bool pauseGameTime = false;

    [Header("Input & Buttons")]
    [Tooltip("Phím chính để đóng popup và bắt đầu chơi")]
    [SerializeField] private KeyCode closeKey = KeyCode.Space;
    [Tooltip("Phím phụ để đóng popup")]
    [SerializeField] private KeyCode alternateCloseKey = KeyCode.E;
    [Tooltip("Nút đóng chữ X (nếu để trống script tự tìm)")]
    [SerializeField] private Button closeButton;
    [Tooltip("Nút xác nhận ĐÃ HIỂU (nếu để trống script tự tìm)")]
    [SerializeField] private Button confirmButton;

    [Header("Tutorial Content Override")]
    [Tooltip("Tiêu đề hướng dẫn")]
    [SerializeField] private string customTitle = "HƯỚNG DẪN ÂM NHẠC";
    [TextArea(3, 8)]
    [Tooltip("Nội dung hướng dẫn chi tiết")]
    [SerializeField] private string customDescription =
        "1. Ghi nhớ giai điệu mà linh hồn vừa nhảy mẫu.\n" +
        "2. Nhấn các phím số [1] - [6] hoặc tự nhảy lên các cột theo đúng thứ tự để gảy lại bản nhạc.\n" +
        "3. Đánh đúng toàn bộ các nốt để phá vỡ lồng băng!\n" +
        "(Mẹo: Bấm [TAB] để tự động hoàn thành minigame)";
    [Tooltip("Gợi ý phím tắt đóng popup")]
    [SerializeField] private string customKeyHint = "Bấm [SPACE], [E] để bắt đầu | Phím [TAB] tự động chơi";
    [Tooltip("Icon minh họa (tùy chọn)")]
    [SerializeField] private Sprite customIcon;

    [Header("Animation")]
    [SerializeField] private bool animatePopup = true;
    [Min(0.01f)]
    [SerializeField] private float animationDuration = 0.25f;

    private static readonly System.Collections.Generic.List<MelodyTutorialController> activeTutorialControllers = new System.Collections.Generic.List<MelodyTutorialController>();

    private RectTransform popupRect;
    private CanvasGroup popupCanvasGroup;
    private Coroutine popupCoroutine;
    private Player cachedPlayer;
    private TutorialPopupUI cachedPopupUI;
    private bool isPopupOpen;
    private bool isWaitingForInput;
    private bool wasPaused;
    private bool hasShownOnce;
    private bool initialized;

    public static void CloseAllActivePopups()
    {
        var controllersToDismiss = new System.Collections.Generic.List<MelodyTutorialController>(activeTutorialControllers);
        activeTutorialControllers.Clear();
        for (int i = 0; i < controllersToDismiss.Count; i++)
        {
            if (controllersToDismiss[i] != null)
            {
                controllersToDismiss[i].DismissPopup();
            }
        }
    }

    private void Awake()
    {
        if (melodyManager == null)
        {
            melodyManager = GetComponent<MelodyManager>();
            if (melodyManager == null)
            {
                melodyManager = FindAnyObjectByType<MelodyManager>();
            }
        }

        InitializePopup();
    }

    private void OnEnable()
    {
        if (!activeTutorialControllers.Contains(this))
        {
            activeTutorialControllers.Add(this);
        }

        InitializePopup();

        if (melodyManager != null)
        {
            melodyManager.OnPlayerTurnStarted.RemoveListener(HandlePlayerTurnStarted);
            melodyManager.OnPlayerTurnStarted.AddListener(HandlePlayerTurnStarted);

            melodyManager.OnWin.RemoveListener(HandleMelodyWon);
            melodyManager.OnWin.AddListener(HandleMelodyWon);
        }
    }

    private void OnDisable()
    {
        activeTutorialControllers.Remove(this);

        if (melodyManager != null)
        {
            melodyManager.OnPlayerTurnStarted.RemoveListener(HandlePlayerTurnStarted);
            melodyManager.OnWin.RemoveListener(HandleMelodyWon);
        }

        if (popupCoroutine != null)
        {
            StopCoroutine(popupCoroutine);
            popupCoroutine = null;
        }

        if (wasPaused)
        {
            Time.timeScale = 1f;
            wasPaused = false;
        }

        UnbindButtons();
        UnlockPlayer();
    }

    private void HandleMelodyWon()
    {
        if (autoCloseOnWin)
        {
            DismissPopup();
        }
    }

    private void Update()
    {
        if (autoCloseOnWin && melodyManager != null && melodyManager.HasWon)
        {
            if (isWaitingForInput || isPopupOpen)
            {
                DismissPopup();
                return;
            }
        }

        if (!isWaitingForInput) return;

        if (Input.GetKeyDown(closeKey)
            || Input.GetKeyDown(alternateCloseKey)
            || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.Escape))
        {
            ClosePopup();
        }
    }

    private void HandlePlayerTurnStarted()
    {
        if (autoCloseOnWin && melodyManager != null && melodyManager.HasWon)
        {
            return;
        }

        if (melodyManager != null)
        {
            melodyManager.FocusCameraOnTarget();
        }

        if (hasShownOnce && showOnlyOnce)
        {
            return;
        }

        ShowTutorialPopup();
    }

    public void ShowTutorialPopup()
    {
        InitializePopup();

        if (melodyManager != null)
        {
            melodyManager.FocusCameraOnTarget();
        }

        if (tutorialPopupPanel == null)
        {
            Debug.LogWarning("[MelodyTutorialController] Chưa gán tutorialPopupPanel và không tìm thấy popup trên Canvas!", this);
            return;
        }

        if (hasShownOnce && showOnlyOnce)
        {
            return;
        }

        if (autoCloseOnWin && melodyManager != null && melodyManager.HasWon)
        {
            DismissPopup();
            return;
        }

        hasShownOnce = true;
        if (popupCoroutine != null)
        {
            StopCoroutine(popupCoroutine);
        }

        popupCoroutine = StartCoroutine(PopupRoutine());
    }

    public void ClosePopup()
    {
        isWaitingForInput = false;
    }

    public void DismissPopup()
    {
        isWaitingForInput = false;
        isPopupOpen = false;

        if (popupCoroutine != null)
        {
            StopCoroutine(popupCoroutine);
            popupCoroutine = null;
        }

        if (tutorialPopupPanel != null)
        {
            if (popupCanvasGroup != null) popupCanvasGroup.alpha = 0f;
            tutorialPopupPanel.SetActive(false);
        }

        if (wasPaused)
        {
            Time.timeScale = 1f;
            wasPaused = false;
        }

        UnlockPlayer();
    }

    private IEnumerator PopupRoutine()
    {
        if (autoCloseOnWin && melodyManager != null && melodyManager.HasWon)
        {
            DismissPopup();
            yield break;
        }

        ApplyCustomContent();

        isPopupOpen = true;
        isWaitingForInput = true;

        if (lockPlayerWhileOpen)
        {
            LockPlayer();
        }
        else
        {
            UnlockPlayer();
        }

        if (pauseGameTime)
        {
            wasPaused = true;
            Time.timeScale = 0f;
        }

        tutorialPopupPanel.transform.SetAsLastSibling();
        tutorialPopupPanel.SetActive(true);

        if (popupRect != null)
        {
            popupRect.anchoredPosition = Vector2.zero;
        }

        if (animatePopup)
        {
            yield return AnimatePopupIn();
        }
        else
        {
            if (popupRect != null) popupRect.localScale = Vector3.one;
            if (popupCanvasGroup != null) popupCanvasGroup.alpha = 1f;
        }

        while (isWaitingForInput)
        {
            if (autoCloseOnWin && melodyManager != null && melodyManager.HasWon)
            {
                isWaitingForInput = false;
                break;
            }

            yield return null;
        }

        if (animatePopup)
        {
            yield return AnimatePopupOut();
        }

        if (popupCanvasGroup != null) popupCanvasGroup.alpha = 0f;
        tutorialPopupPanel.SetActive(false);

        if (wasPaused)
        {
            Time.timeScale = 1f;
            wasPaused = false;
        }

        UnlockPlayer();

        isPopupOpen = false;
        popupCoroutine = null;
    }

    private IEnumerator AnimatePopupIn()
    {
        float duration = Mathf.Max(0.01f, animationDuration);
        float elapsed = 0f;

        Vector3 startScale = Vector3.one * 0.7f;
        Vector3 targetScale = Vector3.one;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scaleT = 1f + 0.15f * Mathf.Sin(t * Mathf.PI * 1.5f) * (1f - t);

            if (popupRect != null)
            {
                popupRect.localScale = Vector3.LerpUnclamped(startScale, targetScale, t) * (t < 1f ? scaleT : 1f);
            }

            if (popupCanvasGroup != null)
            {
                popupCanvasGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
            }

            yield return null;
        }

        if (popupRect != null) popupRect.localScale = Vector3.one;
        if (popupCanvasGroup != null) popupCanvasGroup.alpha = 1f;
    }

    private IEnumerator AnimatePopupOut()
    {
        float duration = Mathf.Max(0.01f, animationDuration * 0.6f);
        float elapsed = 0f;

        Vector3 startScale = Vector3.one;
        Vector3 endScale = Vector3.one * 0.8f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (popupRect != null)
            {
                popupRect.localScale = Vector3.Lerp(startScale, endScale, t);
            }

            if (popupCanvasGroup != null)
            {
                popupCanvasGroup.alpha = 1f - t;
            }

            yield return null;
        }

        if (popupRect != null) popupRect.localScale = Vector3.one;
        if (popupCanvasGroup != null) popupCanvasGroup.alpha = 1f;
    }

    private void InitializePopup()
    {
        if (initialized && tutorialPopupPanel != null)
        {
            return;
        }

        if (tutorialPopupPanel == null && autoFindMissingPopup)
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

        if (!tutorialPopupPanel.scene.IsValid())
        {
            Canvas targetCanvas = FindAnyObjectByType<Canvas>();
            if (targetCanvas != null)
            {
                tutorialPopupPanel = Instantiate(tutorialPopupPanel, targetCanvas.transform);
            }
            else
            {
                tutorialPopupPanel = Instantiate(tutorialPopupPanel);
            }
        }

        popupRect = tutorialPopupPanel.GetComponent<RectTransform>();
        popupCanvasGroup = tutorialPopupPanel.GetComponent<CanvasGroup>();
        if (popupCanvasGroup == null)
        {
            popupCanvasGroup = tutorialPopupPanel.AddComponent<CanvasGroup>();
        }

        if (tutorialPopupPanel.TryGetComponent(out cachedPopupUI))
        {
            ApplyCustomContent();
            if (closeButton == null && cachedPopupUI.CloseButton != null)
            {
                closeButton = cachedPopupUI.CloseButton;
            }

            if (confirmButton == null && cachedPopupUI.ConfirmButton != null)
            {
                confirmButton = cachedPopupUI.ConfirmButton;
            }
        }

        if (closeButton == null)
        {
            closeButton = tutorialPopupPanel.GetComponentInChildren<Button>(true);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePopup);
            closeButton.onClick.AddListener(ClosePopup);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(ClosePopup);
            confirmButton.onClick.AddListener(ClosePopup);
        }

        tutorialPopupPanel.SetActive(false);
        initialized = true;
    }

    private void ApplyCustomContent()
    {
        if (cachedPopupUI == null) return;

        if (!string.IsNullOrEmpty(customTitle)
            || !string.IsNullOrEmpty(customDescription)
            || customIcon != null
            || !string.IsNullOrEmpty(customKeyHint))
        {
            cachedPopupUI.SetContent(customTitle, customDescription, customIcon, customKeyHint);
        }
    }

    private void UnbindButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePopup);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(ClosePopup);
        }
    }

    private void LockPlayer()
    {
        if (cachedPlayer == null)
        {
            cachedPlayer = PlayerRegistry.Current ?? FindAnyObjectByType<Player>();
        }

        if (cachedPlayer != null)
        {
            cachedPlayer.SetControlLocked(true);
        }
    }

    private void UnlockPlayer()
    {
        if (cachedPlayer == null)
        {
            cachedPlayer = PlayerRegistry.Current ?? FindAnyObjectByType<Player>();
        }

        if (cachedPlayer != null)
        {
            cachedPlayer.SetControlLocked(false);
        }
    }
}
