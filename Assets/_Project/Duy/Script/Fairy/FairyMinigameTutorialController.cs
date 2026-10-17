using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

/// <summary>
/// Quản lý hiển thị Bảng Hướng dẫn chơi Minigame Lửa & Nước (MapSaMac):
/// - Khi người chơi chạm vào Trigger timeline:
///   + Phát Cutscene giới thiệu (nếu có).
///   + Ngay khi Cutscene kết thúc -> LẬP TỨC BẮT ĐẦU CHƠI MINIGAME (chuyển sang điều khiển Tinh linh).
///   + ĐỒNG THỜI hiển thị Bảng hướng dẫn trên màn hình trong suốt quá trình chơi để người chơi vừa chơi vừa xem hướng dẫn.
///   + KHÔNG KHÓA điều khiển của Tinh linh.
/// - Khi giải xong minigame (BeamReceptaclePuzzle hoàn thành):
///   + Bảng hướng dẫn TỰ ĐỘNG TẮT HOÀN TOÀN và vô hiệu hóa trigger vĩnh viễn.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class FairyMinigameTutorialController : MonoBehaviour
{
    private static readonly List<FairyMinigameTutorialController> activeControllers = new List<FairyMinigameTutorialController>();
    public static bool IsMinigameSolved { get; private set; } = false;

    [Header("=== REFERENCES ===")]
    [Tooltip("PlayableDirector của Cutscene giới thiệu minigame (nếu để trống tự tìm trên GameObject)")]
    [SerializeField] private PlayableDirector cutsceneDirector;
    [Tooltip("Panel UI Popup hướng dẫn (Kéo Prefab FairyMinigameTutorialPopup_GUIPro hoặc đối tượng trên Canvas)")]
    [SerializeField] private GameObject tutorialPopupPanel;
    [Tooltip("Tự động tìm hoặc sinh Popup trên Canvas nếu để trống")]
    [SerializeField] private bool autoFindMissingPopup = true;
    [Tooltip("Tham chiếu FairyPossessionController để bắt đầu điều khiển tinh linh")]
    [SerializeField] private FairyPossessionController possessionController;

    [Header("=== SETTINGS ===")]
    [Tooltip("Chỉ kích hoạt cutscene/hướng dẫn 1 lần")]
    [SerializeField] private bool triggerOnlyOnce = true;
    [Tooltip("Tự động ẩn sau một khoảng thời gian (giây) nếu > 0. Nếu = 0 sẽ hiện cho tới khi giải xong minigame.")]
    [SerializeField] private float autoHideDuration = 0f;

    [Header("=== BUTTONS & MANUAL CLOSE (OPTIONAL) ===")]
    [Tooltip("Cho phép người chơi click nút đóng thủ công nếu muốn tắt sớm")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button confirmButton;

    [Header("=== TUTORIAL CONTENT OVERRIDE ===")]
    [SerializeField] private string customTitle = "THỬ THÁCH NGUYÊN TỐ: LỬA & NƯỚC";
    [TextArea(4, 10)]
    [SerializeField] private string customDescription =
        "1. Nhấn <color=#FFE680>[F]</color> để chuyển đổi quyền điều khiển giữa <color=#FF6644>Tinh linh Lửa</color> và <color=#44CCFF>Tinh linh Nước</color>.\n" +
        "2. <color=#FF6644>Tinh linh Lửa</color>: Dùng thân mình che chắn <color=#FF4400>Bẫy Phun Lửa</color> để mở đường cho Tinh linh Nước vượt qua an toàn.\n" +
        "3. <color=#44CCFF>Tinh linh Nước</color>: Dễ tổn thương bởi lửa, có khả năng <color=#66DDFF>khúc xạ & kích hoạt tia sáng laser</color>.\n" +
        "4. Dẫn tia sáng vào <color=#FFEE55>Bia Nhận Năng Lượng</color> để mở đường vượt qua màn chơi!";
    [SerializeField] private string customKeyHint = "Nhấn [F] để đổi tinh linh • [Space] để dùng kỹ năng";
    [SerializeField] private Sprite customIcon;

    [Header("=== POSITION SETTINGS ===")]
    [Tooltip("Bật nếu muốn gán vị trí cố định bằng code từ Controller. Tắt để giữ nguyên vị trí tự bạn căn chỉnh trên RectTransform của UI")]
    [SerializeField] private bool overrideCustomPosition = false;
    [Tooltip("Tọa độ Anchored Position khi bật overrideCustomPosition")]
    [SerializeField] private Vector2 customAnchoredPosition = Vector2.zero;

    [Header("=== ANIMATION ===")]
    [SerializeField] private bool animatePopup = true;
    [SerializeField] private float animationDuration = 0.25f;

    private Collider2D triggerCollider;
    private RectTransform popupRect;
    private CanvasGroup popupCanvasGroup;
    private Coroutine animationCoroutine;
    private Coroutine autoHideCoroutine;
    private Player cachedPlayer;
    private TutorialPopupUI cachedPopupUI;
    private bool hasTriggered;
    private bool initialized;
    private bool cutsceneHasPlayed;
    private bool cutsceneIsRunning;

    /// <summary>
    /// Gọi khi minigame giải xong để tắt hoàn toàn popup và vô hiệu hóa trigger vĩnh viễn.
    /// </summary>
    public static void OnMinigameCompleted()
    {
        IsMinigameSolved = true;
        for (int i = activeControllers.Count - 1; i >= 0; i--)
        {
            if (activeControllers[i] != null)
            {
                activeControllers[i].ShutdownForever();
            }
        }
    }

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        if (cutsceneDirector == null)
        {
            cutsceneDirector = GetComponent<PlayableDirector>() ?? GetComponentInChildren<PlayableDirector>();
        }

        if (possessionController == null)
        {
            possessionController = FindAnyObjectByType<FairyPossessionController>();
        }

        InitializePopup();
    }

    private void OnEnable()
    {
        if (!activeControllers.Contains(this))
        {
            activeControllers.Add(this);
        }

        if (IsMinigameSolved)
        {
            ShutdownForever();
            return;
        }

        InitializePopup();

        if (cutsceneDirector != null)
        {
            cutsceneDirector.played += OnCutscenePlayed;
            cutsceneDirector.stopped += OnCutsceneStopped;
            cutsceneIsRunning = cutsceneDirector.state == PlayState.Playing;
            if (cutsceneIsRunning)
            {
                cutsceneHasPlayed = true;
            }
        }
    }

    private void OnDisable()
    {
        activeControllers.Remove(this);

        if (cutsceneDirector != null)
        {
            cutsceneDirector.played -= OnCutscenePlayed;
            cutsceneDirector.stopped -= OnCutsceneStopped;
        }

        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }

        if (autoHideCoroutine != null)
        {
            StopCoroutine(autoHideCoroutine);
            autoHideCoroutine = null;
        }

        UnbindButtons();
        UnlockPlayer();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsMinigameSolved) return;
        if (hasTriggered && triggerOnlyOnce) return;

        if (!IsPlayerCollider(other)) return;

        hasTriggered = true;

        // Nếu có cutscene và chưa chạy -> Phát cutscene trước
        if (cutsceneDirector != null && !cutsceneHasPlayed && cutsceneDirector.state != PlayState.Playing)
        {
            cutsceneHasPlayed = true;
            cutsceneIsRunning = true;
            LockPlayer();
            cutsceneDirector.time = 0;
            cutsceneDirector.Play();
            return;
        }

        // Nếu không có cutscene hoặc cutscene đã chạy xong -> Vào minigame & hiện hướng dẫn luôn
        if (!cutsceneIsRunning)
        {
            StartMinigameAndShowTutorial();
        }
    }

    private bool IsPlayerCollider(Collider2D col)
    {
        if (col == null) return false;
        if (col.CompareTag("Player")) return true;
        if (col.GetComponentInParent<Player>() != null) return true;
        return false;
    }

    private void LateUpdate()
    {
        if (IsMinigameSolved) return;
        if (!cutsceneIsRunning || !cutsceneHasPlayed) return;

        // Khi cutscene chạy đến gần cuối -> Bắt đầu chơi minigame & hiện hướng dẫn
        if (HasCutsceneReachedEnd())
        {
            cutsceneIsRunning = false;
            StartMinigameAndShowTutorial();
        }
    }

    private void OnCutscenePlayed(PlayableDirector director)
    {
        cutsceneHasPlayed = true;
        cutsceneIsRunning = true;
    }

    private void OnCutsceneStopped(PlayableDirector director)
    {
        if (IsMinigameSolved) return;
        if (!cutsceneHasPlayed) return;

        cutsceneIsRunning = false;
        StartMinigameAndShowTutorial();
    }

    private bool HasCutsceneReachedEnd()
    {
        if (cutsceneDirector == null || !cutsceneHasPlayed) return false;

        double duration = cutsceneDirector.duration;
        if (duration <= 0.05d) return false;

        return cutsceneDirector.time >= duration - 0.08d;
    }

    /// <summary>
    /// Bắt đầu chơi minigame (chuyển sang Tinh linh) và hiển thị Bảng hướng dẫn đồng thời.
    /// </summary>
    public void StartMinigameAndShowTutorial()
    {
        if (IsMinigameSolved) return;

        UnlockPlayer();

        // 1. Chuyển quyền điều khiển sang Tinh linh để chơi minigame ngay lập tức
        if (possessionController == null)
        {
            possessionController = FindAnyObjectByType<FairyPossessionController>();
        }

        if (possessionController != null)
        {
            possessionController.StartFairyPossession();
        }

        // 2. Hiển thị Bảng hướng dẫn trên màn hình trong lúc chơi
        InitializePopup();

        if (tutorialPopupPanel != null)
        {
            ApplyCustomContent();
            tutorialPopupPanel.transform.SetAsLastSibling();
            tutorialPopupPanel.SetActive(true);

            if (popupRect != null && overrideCustomPosition)
            {
                popupRect.anchoredPosition = customAnchoredPosition;
            }

            if (animationCoroutine != null) StopCoroutine(animationCoroutine);
            if (animatePopup)
            {
                animationCoroutine = StartCoroutine(AnimatePopupIn());
            }
            else
            {
                if (popupCanvasGroup != null) popupCanvasGroup.alpha = 1f;
                if (popupRect != null) popupRect.localScale = Vector3.one;
            }

            if (autoHideDuration > 0f)
            {
                if (autoHideCoroutine != null) StopCoroutine(autoHideCoroutine);
                autoHideCoroutine = StartCoroutine(AutoHideRoutine(autoHideDuration));
            }
        }
    }

    /// <summary>
    /// Đóng thủ công (nếu người chơi click nút X).
    /// </summary>
    public void ClosePopupManual()
    {
        if (tutorialPopupPanel == null || !tutorialPopupPanel.activeSelf) return;

        if (animationCoroutine != null) StopCoroutine(animationCoroutine);
        if (animatePopup)
        {
            animationCoroutine = StartCoroutine(AnimatePopupOut());
        }
        else
        {
            tutorialPopupPanel.SetActive(false);
        }
    }

    private IEnumerator AutoHideRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        ClosePopupManual();
    }

    public void ShutdownForever()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }

        if (autoHideCoroutine != null)
        {
            StopCoroutine(autoHideCoroutine);
            autoHideCoroutine = null;
        }

        if (tutorialPopupPanel != null)
        {
            tutorialPopupPanel.SetActive(false);
        }

        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        enabled = false;
    }

    private IEnumerator AnimatePopupIn()
    {
        float duration = Mathf.Max(0.01f, animationDuration);
        float elapsed = 0f;

        Vector3 startScale = Vector3.one * 0.75f;
        Vector3 targetScale = Vector3.one;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (popupRect != null)
            {
                popupRect.localScale = Vector3.Lerp(startScale, targetScale, t);
            }

            if (popupCanvasGroup != null)
            {
                popupCanvasGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
            }

            yield return null;
        }

        if (popupRect != null) popupRect.localScale = Vector3.one;
        if (popupCanvasGroup != null) popupCanvasGroup.alpha = 1f;
        animationCoroutine = null;
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

        if (tutorialPopupPanel != null)
        {
            tutorialPopupPanel.SetActive(false);
        }

        animationCoroutine = null;
    }

    private void InitializePopup()
    {
        if (initialized && tutorialPopupPanel != null) return;

        // 1. Tìm Popup đã có trên Canvas trong Scene
        if (tutorialPopupPanel == null && autoFindMissingPopup)
        {
            TutorialPopupUI foundUI = FindAnyObjectByType<TutorialPopupUI>(FindObjectsInactive.Include);
            if (foundUI != null)
            {
                tutorialPopupPanel = foundUI.gameObject;
            }
        }

        // 2. Nếu chưa có trên Scene, tự động nạp từ Prefab trong Project
        if (tutorialPopupPanel == null && autoFindMissingPopup)
        {
#if UNITY_EDITOR
            GameObject prefabAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Duy/Prefab/Prefab-UI/FairyMinigameTutorialPopup_GUIPro.prefab");
            if (prefabAsset != null)
            {
                tutorialPopupPanel = prefabAsset;
            }
#endif
        }

        if (tutorialPopupPanel == null) return;

        // 3. Nếu là Prefab Asset trong Project -> Instantiate vào Canvas của Scene
        if (!tutorialPopupPanel.scene.IsValid())
        {
            Canvas targetCanvas = FindAnyObjectByType<Canvas>();
            if (targetCanvas != null)
            {
                tutorialPopupPanel = Instantiate(tutorialPopupPanel, targetCanvas.transform, false);
            }
            else
            {
                tutorialPopupPanel = Instantiate(tutorialPopupPanel);
            }
            tutorialPopupPanel.name = "FairyMinigameTutorialPopup_GUIPro";
        }

        popupRect = tutorialPopupPanel.GetComponent<RectTransform>();
        if (popupRect != null && overrideCustomPosition)
        {
            popupRect.anchoredPosition = customAnchoredPosition;
        }

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
            closeButton.onClick.RemoveListener(ClosePopupManual);
            closeButton.onClick.AddListener(ClosePopupManual);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(ClosePopupManual);
            confirmButton.onClick.AddListener(ClosePopupManual);
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
            closeButton.onClick.RemoveListener(ClosePopupManual);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(ClosePopupManual);
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
