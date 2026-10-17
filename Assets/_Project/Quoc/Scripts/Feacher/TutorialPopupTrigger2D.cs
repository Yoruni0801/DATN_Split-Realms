using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class TutorialPopupTrigger2D : MonoBehaviour
{
    [Header("UI Popup Panel")]
    [Tooltip("Panel UI popup hướng dẫn chơi cần hiển thị (Kéo Prefab hoặc GameObject từ Canvas vào đây)")]
    [SerializeField] private GameObject tutorialPopupPanel;
    [Tooltip("Tự động tìm kiếm popup UI nếu để trống")]
    [SerializeField] private bool autoFindMissingPopup = true;
    [Tooltip("Tự động ẩn popup khi bắt đầu game")]
    [SerializeField] private bool hidePopupOnStart = true;
    [Tooltip("Nút UI đóng popup (nếu để trống, script sẽ tự tìm trên panel)")]
    [SerializeField] private Button closeButton;

    [Header("Trigger Settings")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool triggerOnce = true;

    [Header("Input & Close")]
    [Tooltip("Chờ người chơi nhấn phím hoặc nút đóng")]
    [SerializeField] private bool waitForPlayerInput = true;
    [Tooltip("Phím chính để đóng popup")]
    [SerializeField] private KeyCode closeKey = KeyCode.Space;
    [Tooltip("Phím phụ để đóng popup")]
    [SerializeField] private KeyCode alternateCloseKey = KeyCode.E;
    [Tooltip("Tự động đóng sau số giây (0 = không tự đóng)")]
    [Min(0f)]
    [SerializeField] private float autoCloseDuration = 0f;

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

    [Header("Player & Time Control")]
    [Tooltip("Khóa di chuyển của Player khi đang mở popup")]
    [SerializeField] private bool lockPlayerMovement = false;
    [Tooltip("Tự động tắt popup khi minigame hoàn thành (PlinkoBreakTarget)")]
    [SerializeField] private bool autoCloseOnMinigameComplete = true;
    [Tooltip("Tạm dừng thời gian game (Time.timeScale = 0) khi mở popup")]
    [SerializeField] private bool pauseGameTime = false;

    [Header("Animation")]
    [SerializeField] private bool animatePopup = true;
    [Min(0.01f)]
    [SerializeField] private float animationDuration = 0.25f;

    private Collider2D triggerCollider;
    private RectTransform popupRectTransform;
    private CanvasGroup popupCanvasGroup;
    private Coroutine popupCoroutine;
    private Player cachedPlayer;
    private TutorialPopupUI cachedPopupUI;
    private bool isWaitingForInput;
    private bool isPopupOpen;
    private bool wasPaused;
    private bool hasTriggered;
    private bool popupInitialized;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        EnsureTriggerCollider();
        EnsurePopupInstance();
    }

    private void OnEnable()
    {
        PlinkoBreakTarget.OnPlinkoCompleted -= OnPlinkoCompletedHandler;
        PlinkoBreakTarget.OnPlinkoCompleted += OnPlinkoCompletedHandler;

        EnsureTriggerCollider();
        EnsurePopupInstance();
    }

    private void OnDisable()
    {
        PlinkoBreakTarget.OnPlinkoCompleted -= OnPlinkoCompletedHandler;

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

        UnbindCloseButtons();
        UnlockPlayer();
    }

    private void OnPlinkoCompletedHandler()
    {
        if (autoCloseOnMinigameComplete)
        {
            DismissPopup();
        }
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

    private void EnsurePopupInstance()
    {
        if (popupInitialized && tutorialPopupPanel != null)
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

        popupRectTransform = tutorialPopupPanel.GetComponent<RectTransform>();
        popupCanvasGroup = tutorialPopupPanel.GetComponent<CanvasGroup>();
        if (popupCanvasGroup == null)
        {
            popupCanvasGroup = tutorialPopupPanel.AddComponent<CanvasGroup>();
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
                cachedPopupUI.ConfirmButton.onClick.RemoveListener(ClosePopup);
                cachedPopupUI.ConfirmButton.onClick.AddListener(ClosePopup);
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

        if (hidePopupOnStart && !hasTriggered)
        {
            tutorialPopupPanel.SetActive(false);
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
            closeButton.onClick.RemoveListener(ClosePopup);
        }

        if (cachedPopupUI != null && cachedPopupUI.ConfirmButton != null)
        {
            cachedPopupUI.ConfirmButton.onClick.RemoveListener(ClosePopup);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isPopupOpen || (hasTriggered && triggerOnce))
        {
            return;
        }

        if (!IsPlayerCollider(other))
        {
            return;
        }

        cachedPlayer = other.GetComponent<Player>();
        if (cachedPlayer == null)
        {
            cachedPlayer = other.GetComponentInParent<Player>();
        }

        if (cachedPlayer == null)
        {
            cachedPlayer = FindAnyObjectByType<Player>();
        }

        ShowPopup();
    }

    public void ShowPopup()
    {
        EnsurePopupInstance();

        if (tutorialPopupPanel == null || (hasTriggered && triggerOnce))
        {
            return;
        }

        hasTriggered = true;
        if (popupCoroutine != null)
        {
            StopCoroutine(popupCoroutine);
        }

        popupCoroutine = StartCoroutine(RunPopupRoutine());
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

    private IEnumerator RunPopupRoutine()
    {
        if (autoCloseOnMinigameComplete && PlinkoBreakTarget.GlobalHasBroken)
        {
            DismissPopup();
            yield break;
        }

        ApplyCustomPopupContent();

        isPopupOpen = true;
        isWaitingForInput = true;

        if (lockPlayerMovement)
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
        while (isWaitingForInput)
        {
            if (autoCloseOnMinigameComplete && PlinkoBreakTarget.GlobalHasBroken)
            {
                isWaitingForInput = false;
                break;
            }

            if (waitForPlayerInput)
            {
                if (Input.GetKeyDown(closeKey)
                    || Input.GetKeyDown(alternateCloseKey)
                    || Input.GetKeyDown(KeyCode.Return)
                    || Input.GetKeyDown(KeyCode.Escape))
                {
                    isWaitingForInput = false;
                    break;
                }
            }

            if (autoCloseDuration > 0f)
            {
                timer -= Time.unscaledDeltaTime;
                if (timer <= 0f)
                {
                    isWaitingForInput = false;
                    break;
                }
            }

            yield return null;
        }

        if (animatePopup)
        {
            yield return AnimatePopupOut();
        }

        tutorialPopupPanel.SetActive(false);

        if (wasPaused)
        {
            Time.timeScale = 1f;
            wasPaused = false;
        }

        UnlockPlayer();

        isPopupOpen = false;
        popupCoroutine = null;

        if (triggerOnce)
        {
            enabled = false;
        }
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
        float duration = Mathf.Max(0.01f, animationDuration * 0.6f);
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

    private void LockPlayer()
    {
        if (cachedPlayer == null)
        {
            cachedPlayer = FindAnyObjectByType<Player>();
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
            cachedPlayer = FindAnyObjectByType<Player>();
        }

        if (cachedPlayer != null)
        {
            cachedPlayer.SetControlLocked(false);
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

    private void OnValidate()
    {
        triggerCollider = GetComponent<Collider2D>();
        EnsureTriggerCollider();
    }
}
